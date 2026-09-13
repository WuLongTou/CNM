namespace ZombieWars.Core;

using Godot;
using Godot.Collections;

/// <summary>
/// 外部指令接口 —— 供自动化测试与外部程序控制游戏。
///
/// 传输：JSONL 文件信箱（无网络、无常驻端口，默认关闭，只有传参才启用）。
///   --cmd-listen=&lt;path&gt;  轮询 path（0.2s 一次），逐行执行 JSON 指令，
///                             回执 append 到 &lt;path&gt;.resp
///   --cmd='&lt;json&gt;'       启动后执行单条指令一次（回执打印到日志；若同时
///                             开了 --cmd-listen 也会写入 resp 文件）
///
/// 行协议（请求）：{"id":1,"action":"status","args":{...}}
/// 行协议（回执）：{"id":1,"ok":true,"data":{...}} / {"id":1,"ok":false,"error":"..."}
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
/// 示例（shell）：echo '{"id":1,"action":"status"}' >> /tmp/cnm.cmd
/// </summary>
public partial class ExternalCommandService : Node
{
    private const float PollInterval = 0.2f;

    private string _cmdPath = "";
    private string _respPath = "";
    private float _pollTimer;
    private long _readOffset;
    private string _leftover = "";
    private FileAccess? _respFile; // 回执文件常驻追加句柄（避免反复 open/seek 的竞态）

    public void Setup(string cmdPath)
    {
        _cmdPath = cmdPath;
        _respPath = cmdPath + ".resp";
        _readOffset = 0;
        _leftover = "";
    }

    /// <summary>CLI 单条指令入口（Main --cmd 用），执行后返回回执字典。</summary>
    public Dictionary ExecuteJson(string json)
    {
        var parsed = Json.ParseString(json);
        var cmd = parsed.AsGodotDictionary();
        if (cmd == null)
            return _Resp(-1, false, null, "指令不是合法 JSON 对象");
        return Execute(cmd);
    }

    public override void _Process(double delta)
    {
        if (string.IsNullOrEmpty(_cmdPath)) return;
        _pollTimer -= (float)delta;
        if (_pollTimer > 0) return;
        _pollTimer = PollInterval;
        _PollFile();
    }

    private void _PollFile()
    {
        if (!FileAccess.FileExists(_cmdPath)) return;
        using var f = FileAccess.Open(_cmdPath, FileAccess.ModeFlags.Read);
        if (f == null) return;
        ulong len = f.GetLength();
        if (len < (ulong)_readOffset)
        {
            _readOffset = 0; // 文件被外部截断/重建，从头读
            _leftover = "";
        }
        if ((ulong)_readOffset >= len) return; // 无新增内容（Seek 到 EOF 再读会回绕，必须提前返回）
        f.Seek((ulong)_readOffset);
        // 注意：GetAsText() 恒返回整个文件（无视 Seek），必须用 GetBuffer 按位置读
        long remaining = (long)len - _readOffset;
        if (remaining <= 0) return;
        string text = f.GetBuffer(remaining).GetStringFromUtf8();
        if (string.IsNullOrEmpty(text) && string.IsNullOrEmpty(_leftover)) return;

        // 按行切分，末行不完整则留到下次；偏移只推进完整行消费掉的字节
        // （逐行 UTF8 计数，多字节字符安全；不依赖 GetPosition，避免驱动差异）
        string buf = _leftover + text;
        var lines = buf.Split("\n");
        bool endsComplete = buf.EndsWith("\n");
        int complete = endsComplete ? lines.Length : lines.Length - 1;
        _leftover = endsComplete ? "" : lines[^1];
        var utf8 = System.Text.Encoding.UTF8;
        for (int i = 0; i < complete; i++)
        {
            // Split 末尾空元素（"a\n"→["a",""]）不对应任何字节，跳过，
            // 否则偏移恒超文件 1 字节，每轮触发截断重置导致全量重放
            if (endsComplete && i == complete - 1 && lines[i].Length == 0) continue;
            _readOffset += utf8.GetByteCount(lines[i]) + 1; // +1 换行符 \n
        }

        for (int i = 0; i < complete; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            var resp = ExecuteJson(line);
            _AppendResp(resp);
        }
    }

    private void _AppendResp(Dictionary resp)
    {
        if (string.IsNullOrEmpty(_respPath)) return;
        if (_respFile == null)
        {
            if (!FileAccess.FileExists(_respPath))
            {
                using var c = FileAccess.Open(_respPath, FileAccess.ModeFlags.Write);
                if (c == null) return;
            }
            _respFile = FileAccess.Open(_respPath, FileAccess.ModeFlags.ReadWrite);
            if (_respFile == null) return;
            _respFile.SeekEnd();
        }
        _respFile.StoreLine(Json.Stringify(resp));
        _respFile.Flush();
    }

    public override void _ExitTree()
    {
        _respFile?.Close();
        _respFile = null;
    }

    /// <summary>执行单条指令字典，返回回执字典（供内部与测试复用）。</summary>
    public Dictionary Execute(Dictionary cmd)
    {
        int id = cmd.TryGetValue("id", out Variant idVar) ? idVar.AsInt32() : -1;
        string action = cmd.TryGetValue("action", out Variant aVar) ? aVar.AsString() : "";
        var args = cmd.TryGetValue("args", out Variant argsVar) ? argsVar.AsGodotDictionary() : null;

        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        var ui = GetTree().GetFirstNodeInGroup("ui_manager") as UI.UIManager;

        try
        {
            return action switch
            {
                "status" => _Resp(id, true, _Status(gm, ui)),
                "start" => _Resp(id, true, _Start(ui)),
                "force_combat" => _Resp(id, true, _ForceCombat(gm)),
                "spawn" => _Resp(id, true, _Spawn(gm, args)),
                "give" => _Resp(id, true, _Give(gm, args)),
                "teleport" => _Resp(id, true, _Teleport(gm, args)),
                "screenshot" => _Resp(id, true, _Screenshot(args)),
                "quit" => _Quit(id, args),
                _ => _Resp(id, false, null, $"未知动作: {action}"),
            };
        }
        catch (System.Exception ex)
        {
            return _Resp(id, false, null, $"执行异常: {ex.Message}");
        }
    }

    private static Dictionary _Resp(int id, bool ok, Dictionary? data, string error = "")
    {
        var r = new Dictionary { ["id"] = id, ["ok"] = ok };
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

    private Dictionary _Quit(int id, Dictionary? args)
    {
        int code = args?.TryGetValue("code", out Variant c) == true ? c.AsInt32() : 0;
        LogUtils.Print($"[CMD] quit({code})");
        GetTree().Quit(code);
        return _Resp(id, true, new Dictionary { ["quitting"] = true });
    }
}
