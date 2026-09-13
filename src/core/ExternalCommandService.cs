namespace ZombieWars.Core;

using Godot;
using Godot.Collections;
using SysColGen = System.Collections.Generic;
using System.Text;

/// <summary>
/// 外部指令接口 —— 供自动化测试与外部程序实时控制游戏。
///
/// 传输：TCP 回环服务（默认关闭，只有传参才启用；只绑 127.0.0.1，不出本机）。
///   --cmd-port=&lt;端口&gt;  启动指令服务；多客户端可同时连接，各自收发独立，
///                           游戏侧 last-writer-wins（与多手柄同理）
///   --cmd='&lt;json&gt;'     启动后执行单条指令一次，回执打印到日志
///
/// 行协议（请求）：{"id":1,"client":"pytest","action":"status","args":{...}}
/// 行协议（回执）：{"id":1,"client":"pytest","ok":true,"data":{...}}
/// 事件推送（无需 id）：{"event":"game_over","data":{...}} /
///                     {"event":"threat_tier","data":{"tier":3}}
///
/// 动作集：
///   status      游戏状态（state/round/score/kills/僵尸数/玩家血量位置）
///   start       关闭欢迎画面进入游戏（等价点"开始游戏"）
///   force_combat 测试跳关直达战斗态（复用 GameManager.DebugForceCombat）
///   spawn       刷怪 args:{count,type("random"|"basic"),near("player"|"random"|"x,y")}
///   give        宝箱奖励随机武器 args:{count}
///   teleport    传送玩家 args:{x,y}
///   screenshot  视口截图 args:{path}（headless 无渲染时返回 saved:false）
///   quit        退出 args:{code}
///
/// 示例（python）：socket.create_connection(("127.0.0.1", 31750)) 收发 \n 分隔的 JSON。
/// </summary>
public partial class ExternalCommandService : Node
{
    private Godot.TcpServer? _server;
    private readonly SysColGen.Dictionary<Godot.StreamPeerTcp, string> _peerBuffers = new();
    private GameManager? _eventSource;
    private System.Action? _onGameOverHandler;
    private System.Action<int>? _onThreatTierHandler;

    /// <summary>启动 TCP 指令服务（只绑回环地址）。失败返回 false。</summary>
    public bool Listen(int port)
    {
        if (port < 1 || port > 65535)
        {
            LogUtils.PrintErr("[CMD] 非法端口: " + port);
            return false;
        }
        _server = new Godot.TcpServer();
        Error err = _server.Listen((ushort)port, "127.0.0.1");
        if (err != Error.Ok)
        {
            LogUtils.PrintErr("[CMD] 监听失败: " + port + " (" + err + ")");
            _server = null;
            return false;
        }
        _SubscribeEvents();
        LogUtils.Print("[CMD] tcp listen: 127.0.0.1:" + port);
        return true;
    }

    /// <summary>CLI 单条指令入口（Main --cmd 用），执行后返回回执字典。</summary>
    public Dictionary ExecuteJson(string json)
    {
        var parsed = Json.ParseString(json);
        var cmd = parsed.AsGodotDictionary();
        if (cmd == null)
            return _Resp(-1, "", false, null, "指令不是合法 JSON 对象");
        return Execute(cmd);
    }

    public override void _Process(double delta)
    {
        if (_server == null || !_server.IsListening()) return;
        while (_server.IsConnectionAvailable())
        {
            var peer = _server.TakeConnection();
            if (peer != null)
                _peerBuffers[peer] = "";
        }
        var dead = new SysColGen.List<Godot.StreamPeerTcp>();
        foreach (var kv in _peerBuffers)
        {
            var peer = kv.Key;
            if (peer.GetStatus() != Godot.StreamPeerSocket.Status.Connected)
            {
                dead.Add(peer);
                continue;
            }
            _Receive(peer);
        }
        foreach (var peer in dead)
            _peerBuffers.Remove(peer);
    }

    public override void _ExitTree()
    {
        if (_eventSource != null)
        {
            if (_onGameOverHandler != null)
                _eventSource.GameOver -= _onGameOverHandler;
            if (_onThreatTierHandler != null)
                _eventSource.ThreatTierAdvanced -= _onThreatTierHandler;
            _eventSource = null;
        }
        _server?.Stop();
        _server = null;
        _peerBuffers.Clear();
    }

    private void _Receive(Godot.StreamPeerTcp peer)
    {
        int available = peer.GetAvailableBytes();
        if (available <= 0) return;
        // 单行上限 64KB 防刷爆内存
        if (available > 65536) available = 65536;
        Godot.Collections.Array got = peer.GetData(available);
        if (got.Count < 2 || (int)got[0] != 0) return;
        byte[] bytes = got[1].AsByteArray();
        if (bytes.Length == 0) return;
        string buf = _peerBuffers[peer] + Encoding.UTF8.GetString(bytes);
        var lines = buf.Split("\n");
        bool endsComplete = buf.EndsWith("\n");
        int complete = endsComplete ? lines.Length : lines.Length - 1;
        _peerBuffers[peer] = endsComplete ? "" : lines[^1];
        for (int i = 0; i < complete; i++)
        {
            string line = lines[i].Trim().TrimEnd('\r');
            if (string.IsNullOrEmpty(line)) continue;
            var resp = ExecuteJson(line);
            _Send(peer, resp);
        }
    }

    private static void _Send(Godot.StreamPeerTcp peer, Dictionary resp)
    {
        if (peer.GetStatus() != Godot.StreamPeerSocket.Status.Connected) return;
        byte[] bytes = Encoding.UTF8.GetBytes(Json.Stringify(resp) + "\n");
        peer.PutData(bytes);
    }

    private void _Broadcast(Dictionary msg)
    {
        string line = Json.Stringify(msg) + "\n";
        byte[] bytes = Encoding.UTF8.GetBytes(line);
        var dead = new SysColGen.List<Godot.StreamPeerTcp>();
        foreach (var peer in _peerBuffers.Keys)
        {
            if (peer.GetStatus() != Godot.StreamPeerSocket.Status.Connected)
                dead.Add(peer);
            else
                peer.PutData(bytes);
        }
        foreach (var peer in dead)
            _peerBuffers.Remove(peer);
    }

    private void _SubscribeEvents()
    {
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm == null) return;
        _eventSource = gm;
        _onGameOverHandler = () =>
        {
            var ui = GetTree().GetFirstNodeInGroup("ui_manager") as UI.UIManager;
            _Broadcast(new Dictionary { ["event"] = "game_over", ["data"] = _Status(gm, ui) });
        };
        _onThreatTierHandler = (tier) =>
            _Broadcast(new Dictionary { ["event"] = "threat_tier", ["data"] = new Dictionary { ["tier"] = tier } });
        gm.GameOver += _onGameOverHandler;
        gm.ThreatTierAdvanced += _onThreatTierHandler;
    }

    /// <summary>执行单条指令字典，返回回执字典（供内部与测试复用）。</summary>
    public Dictionary Execute(Dictionary cmd)
    {
        int id = cmd.TryGetValue("id", out Variant idVar) ? idVar.AsInt32() : -1;
        string client = cmd.TryGetValue("client", out Variant clVar) ? clVar.AsString() : "";
        string action = cmd.TryGetValue("action", out Variant aVar) ? aVar.AsString() : "";
        var args = cmd.TryGetValue("args", out Variant argsVar) ? argsVar.AsGodotDictionary() : null;

        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        var ui = GetTree().GetFirstNodeInGroup("ui_manager") as UI.UIManager;

        try
        {
            return action switch
            {
                "status" => _Resp(id, client, true, _Status(gm, ui)),
                "start" => _Resp(id, client, true, _Start(ui)),
                "force_combat" => _Resp(id, client, true, _ForceCombat(gm)),
                "spawn" => _Resp(id, client, true, _Spawn(gm, args)),
                "give" => _Resp(id, client, true, _Give(gm, args)),
                "teleport" => _Resp(id, client, true, _Teleport(gm, args)),
                "screenshot" => _Resp(id, client, true, _Screenshot(args)),
                "quit" => _Quit(id, client, args),
                _ => _Resp(id, client, false, null, "未知动作: " + action),
            };
        }
        catch (System.Exception ex)
        {
            return _Resp(id, client, false, null, "执行异常: " + ex.Message);
        }
    }

    private static Dictionary _Resp(int id, string client, bool ok, Dictionary? data, string error = "")
    {
        var r = new Dictionary { ["id"] = id, ["ok"] = ok };
        if (!string.IsNullOrEmpty(client)) r["client"] = client;
        if (data != null) r["data"] = data;
        if (!ok) r["error"] = error;
        return r;
    }

    private static Dictionary _Status(GameManager? gm, UI.UIManager? ui)
    {
        var d = new Dictionary
        {
            ["state"] = gm?.CurrentState.ToString() ?? "NoGame",
            ["round"] = gm?.GetCurrentRound() ?? 0,
            ["score"] = gm?.Score ?? 0,
            ["kills"] = gm?.TotalKills ?? 0,
            ["zombies_alive"] = gm?.GetZombiesRemaining() ?? 0,
            ["welcome_visible"] = ui?.GetWelcomeVisible() ?? false,
        };
        var p = gm?.PlayerRef;
        if (p != null)
        {
            d["player_hp"] = p.CurrentHealth;
            d["player_max_hp"] = p.MaxHealth;
            d["player_pos"] = new Dictionary { ["x"] = p.GlobalPosition.X, ["y"] = p.GlobalPosition.Y };
        }
        return d;
    }

    private static Dictionary _Start(UI.UIManager? ui)
    {
        if (ui == null) return new Dictionary { ["started"] = false };
        ui.StartGame();
        return new Dictionary { ["started"] = true };
    }

    private static Dictionary _ForceCombat(GameManager? gm)
    {
        if (gm == null) return new Dictionary { ["combat"] = false };
        gm.DebugForceCombat();
        return new Dictionary { ["combat"] = gm.CurrentState == GameEnums.GameState.Combat };
    }

    private static Dictionary _Spawn(GameManager? gm, Dictionary? args)
    {
        if (gm?.PlayerRef == null) return new Dictionary { ["spawned"] = 0 };
        int count = Mathf.Clamp(args?.TryGetValue("count", out Variant c) == true ? c.AsInt32() : 1, 1, 50);
        string type = args?.TryGetValue("type", out Variant t) == true ? t.AsString() : "random";
        string near = args?.TryGetValue("near", out Variant n) == true ? n.AsString() : "random";
        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            Vector2? pos = _SpawnPos(gm, near);
            if (type == "basic")
                gm.SpawnMinionZombie(pos ?? gm.PlayerRef.GlobalPosition);
            else
                gm.SpawnZombie(pos);
            spawned++;
        }
        return new Dictionary { ["spawned"] = spawned };
    }

    private static Vector2? _SpawnPos(GameManager gm, string near)
    {
        var player = gm.PlayerRef;
        if (player == null) return null;
        if (near.StartsWith("player"))
        {
            float a = GD.Randf() * Mathf.Tau;
            float r = (float)GD.RandRange(200f, 400f);
            return player.GlobalPosition + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        if (near.Contains(","))
        {
            var parts = near.Split(",");
            if (parts.Length == 2 && float.TryParse(parts[0], out float x) && float.TryParse(parts[1], out float y))
                return new Vector2(x, y);
        }
        return null; // random：由刷怪网格决定
    }

    private static Dictionary _Give(GameManager? gm, Dictionary? args)
    {
        if (gm == null) return new Dictionary { ["given"] = 0 };
        int count = Mathf.Clamp(args?.TryGetValue("count", out Variant c) == true ? c.AsInt32() : 1, 1, 10);
        for (int i = 0; i < count; i++)
            gm.GrantChestReward();
        return new Dictionary { ["given"] = count };
    }

    private static Dictionary _Teleport(GameManager? gm, Dictionary? args)
    {
        var player = gm?.PlayerRef;
        if (player == null || args == null) return new Dictionary { ["teleported"] = false };
        float x = args.TryGetValue("x", out Variant xv) ? xv.AsSingle() : player.GlobalPosition.X;
        float y = args.TryGetValue("y", out Variant yv) ? yv.AsSingle() : player.GlobalPosition.Y;
        player.GlobalPosition = new Vector2(x, y);
        return new Dictionary
        {
            ["teleported"] = true,
            ["pos"] = new Dictionary { ["x"] = x, ["y"] = y },
        };
    }

    private Dictionary _Screenshot(Dictionary? args)
    {
        string path = args?.TryGetValue("path", out Variant p) == true ? p.AsString() : "/tmp/cnm_shot.png";
        var img = GetViewport().GetTexture().GetImage();
        if (img == null || img.IsEmpty())
            return new Dictionary { ["saved"] = false, ["path"] = path, ["note"] = "无渲染目标（headless 下不可用）" };
        Error err = img.SavePng(path);
        return new Dictionary { ["saved"] = err == Error.Ok, ["path"] = path };
    }

    private Dictionary _Quit(int id, string client, Dictionary? args)
    {
        int code = args?.TryGetValue("code", out Variant c) == true ? c.AsInt32() : 0;
        GD.Print("[CMD] quit(" + code + ")");
        GetTree().Quit(code);
        return _Resp(id, client, true, new Dictionary { ["quitting"] = true });
    }
}
