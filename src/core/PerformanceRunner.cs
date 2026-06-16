namespace ZombieWars.Core;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using ZombieWars.Enemy;
using static ZombieWars.Core.GameEnums;
using Player = ZombieWars.Player.Player;

/// <summary>
/// 性能压力测试：生成大量僵尸并采样帧率，定位 CPU 逻辑瓶颈（导航 / 分离力 / GC）。
/// 用法：Godot --headless -- --perftest [--perf-count N]
/// 说明：headless 无渲染，FPS 反映逻辑帧率，用于对比优化前后，不等于真实游戏帧率。
/// </summary>
public partial class PerformanceRunner : Node
{
    private GameManager? _gameManager;
    private Player? _player;
    private int _targetCount = 200;
    private bool _armed;
    private float _elapsed;
    private float _sampleTimer;
    private readonly List<float> _fpsSamples = new();
    private const float Warmup = 1.5f;       // 让僵尸聚拢围攻
    private const float TestDuration = 8.0f;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        var args = OS.GetCmdlineUserArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--perf-count" && int.TryParse(args[i + 1], out var c))
                _targetCount = Mathf.Clamp(c, 20, 600);
        }
        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        _gameManager = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        _player = GetTree().GetFirstNodeInGroup("player") as Player
            ?? GetTree().CurrentScene?.GetNodeOrNull<Player>("Player");
        if (_gameManager == null || _player == null)
        {
            LogUtils.PrintErr("[PERF] 缺少 GameManager/Player");
            GetTree().Quit(2);
            return;
        }

        // 推进到 Combat
        for (int i = 0; i < 300; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_gameManager.CurrentState == GameState.WeaponSelection)
                _gameManager.OnUpgradeSkipped();
            else if (_gameManager.CurrentState == GameState.Combat)
                break;
        }

        if (_gameManager.CurrentState != GameState.Combat)
        {
            LogUtils.PrintErr("[PERF] 未能进入 Combat");
            GetTree().Quit(2);
            return;
        }

        // 玩家置于中心并持续无敌，僵尸围绕生成形成密集围攻（最吃 CPU 的场景）
        _player.GlobalPosition = MapContext.CurrentWorldSize / 2.0f;

        // 直接实例化绕过 SpawnExtraZombie 的 50 上限
        var scene = GD.Load<PackedScene>("res://scenes/Zombie.tscn");
        if (scene != null)
        {
            var center = _player.GlobalPosition;
            for (int i = 0; i < _targetCount; i++)
            {
                var z = scene.Instantiate() as Zombie;
                if (z == null) continue;
                z.ZombieType = ZombieType.Basic;
                z.GlobalPosition = center + new Vector2(
                    (float)GD.RandRange(-350, 350), (float)GD.RandRange(-350, 350));
                z.SetPlayerTarget(_player);
                GetTree().CurrentScene.AddChild(z);
                ZombieRegistry.Register(z);
            }
        }

        _armed = true;
        LogUtils.Print($"[PERF] 目标 {_targetCount} | 实际生成 {ZombieRegistry.Count} | 预热 {Warmup}s + 采样 {TestDuration}s");
    }

    public override void _Process(double delta)
    {
        if (!_armed || _gameManager == null) return;

        // 玩家持续无敌，避免被围攻打断采样
        if (_player != null)
            _player.InvincibleTimer = 1.0f;

        _elapsed += (float)delta;
        if (_elapsed > Warmup)
        {
            _sampleTimer += (float)delta;
            if (_sampleTimer >= 0.5f)
            {
                _sampleTimer = 0;
                _fpsSamples.Add((float)Engine.GetFramesPerSecond());
            }
        }

        if (_elapsed >= Warmup + TestDuration)
        {
            if (_fpsSamples.Count > 0)
            {
                float mn = _fpsSamples.Min(), mx = _fpsSamples.Max(), avg = (float)_fpsSamples.Average();
                LogUtils.Print($"[PERF] 结果 | 存活 {ZombieRegistry.Count} | FPS min/avg/max = {mn:F0}/{avg:F0}/{mx:F0}");
            }
            else
            {
                LogUtils.Print("[PERF] 无有效采样");
            }
            GetTree().Quit(0);
        }
    }
}
