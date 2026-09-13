namespace ZombieWars.Core;

using System;
using System.Collections.Generic;
using Godot;
using static ZombieWars.Core.GameEnums;
using Player = ZombieWars.Player.Player;
using Zombie = ZombieWars.Enemy.Zombie;
using WeaponDeck = ZombieWars.Weapons.WeaponDeck;
using UIManager = ZombieWars.UI.UIManager;
using WaveResonanceSystem = ZombieWars.Math.WaveResonanceSystem;
using VectorFieldSystem = ZombieWars.Math.VectorFieldSystem;
using ZombieType = ZombieWars.Core.GameEnums.ZombieType;
using FractalSystem = ZombieWars.Math.FractalSystem;
using StrategySystem = ZombieWars.Math.StrategySystem;

/// <summary>
/// Game Manager — orchestrates game state, explore-mode spawning, and difficulty scaling.
/// </summary>
public partial class GameManager : Node
{
    public event Action GameOver = delegate { };
    /// <summary>威胁阶数推进时触发（参数=新阶数）。外部指令接口据此向订阅端推送事件。</summary>
    public event Action<int> ThreatTierAdvanced = delegate { };

    [Export] public int ZombiesMaxPerRound = 40;

    [Export] public float RoundMultiplierBase = 0.04f;
    [Export] public float EliteChanceBase = 0.02f;
    [Export] public float EliteChancePerRound = 0.010f;
    [Export] public float EliteChanceMax = 0.18f; // Balanced from 0.25f

    public GameState CurrentState { get; private set; } = GameState.WeaponSelection;
    public int CurrentRound { get; private set; } = 0;

    private int _zombiesSpawned; // 累计生成数（Boss 首刷判定与波形相位用）
    private int _zombiesAlive;
    private int _currentRoundTotalZombies; // 本局累计生成僵尸数（含天气额外生成）
    // 持续探索模式（饥荒式）：维持世界僵尸数 + 时间推进难度，替代波次
    private float _exploreMaintainTimer;
    private float _difficultyRampTimer;

    // 战斗统计（持续探索模式按整局累计）
    private int _totalKills;
    private int _totalDamageDealt;

    // 提取的独立组件
    private PickupSpawner _pickupSpawner = null!;
    private SpawnPointGrid _spawnPointGrid = null!;
    private DirectiveSystem _directiveSystem = null!;

    public Player? PlayerRef { get; private set; }
    public Zombie? BossZombie { get; private set; }
    private int _bossPhase; // Boss 阶段：0=无/已死，1/2/3=三阶段（66%/33% 血量切换）

    // ══ 数学玩法系统 ══
    public WaveResonanceSystem WaveSystem { get; private set; } = null!;
    public VectorFieldSystem FieldSystem { get; private set; } = null!;
    public FractalSystem FractalSys { get; private set; } = null!;
    public StrategySystem StrategySys { get; private set; } = null!;

    private WeaponDeck? _weaponDeck;
    private Player _player = null!;
    private PackedScene _zombieScene = null!;

    private UIManager _uiManager = null!;

    public int Score { get; private set; }
    public int HighScore { get; private set; }
    public int TotalKills => _totalKills;

    private float _survivalTime;
    private float _edgeIndicatorTimer;

    private static readonly Dictionary<int, string> TypeNames = new()
    {
        { 0, "基础" }, { 1, "快速" }, { 2, "坦克" }, { 3, "爆炸" },
        { 4, "隐身" }, { 5, "喷毒" }, { 6, "治疗" }, { 7, "召唤师" }, { 8, "射手" },
        { 9, "飞船" }, { 10, "机甲" }, { 11, "骑士" }, { 12, "飞龙" }, { 13, "虫群" }
    };

    private static readonly Dictionary<int, int> TypeScore = new()
    {
        { 0, 10 }, { 1, 15 }, { 2, 25 }, { 3, 30 },
        { 4, 35 }, { 5, 40 }, { 6, 50 }, { 7, 60 }, { 8, 45 },
        { 9, 55 }, { 10, 65 }, { 11, 50 }, { 12, 70 }, { 13, 20 }
    };

    private struct SpawnEntry
    {
        public int MaxRound;
        public Dictionary<int, float> Weights;
    }

    private static readonly SpawnEntry[] SpawnTable = new SpawnEntry[]
    {
        new() { MaxRound = 2,  Weights = new() { { 0, 1.00f } } },
        new() { MaxRound = 4,  Weights = new() { { 0, 0.75f }, { 1, 0.25f } } },
        new() { MaxRound = 6,  Weights = new() { { 0, 0.45f }, { 1, 0.30f }, { 2, 0.15f }, { 3, 0.10f } } },
        new() { MaxRound = 10, Weights = new() { { 0, 0.25f }, { 1, 0.20f }, { 2, 0.20f }, { 3, 0.15f }, { 4, 0.10f }, { 5, 0.10f } } },
        new() { MaxRound = 14, Weights = new() { { 0, 0.08f }, { 1, 0.10f }, { 2, 0.18f }, { 3, 0.12f }, { 4, 0.10f }, { 5, 0.10f }, { 6, 0.08f }, { 7, 0.06f }, { 8, 0.08f } } },
        new() { MaxRound = 18, Weights = new() { { 0, 0.05f }, { 1, 0.08f }, { 2, 0.12f }, { 3, 0.10f }, { 4, 0.08f }, { 5, 0.10f }, { 6, 0.08f }, { 7, 0.07f }, { 8, 0.08f }, { 9, 0.08f }, { 10, 0.05f }, { 11, 0.05f } } },
        new() { MaxRound = 9999, Weights = new() { { 0, 0.04f }, { 1, 0.06f }, { 2, 0.10f }, { 3, 0.08f }, { 4, 0.07f }, { 5, 0.08f }, { 6, 0.07f }, { 7, 0.06f }, { 8, 0.07f }, { 9, 0.10f }, { 10, 0.07f }, { 11, 0.06f }, { 12, 0.08f }, { 13, 0.06f } } },
    };

    public override async void _Ready()
    {
        AddToGroup("game_manager");

        // 命中停顿控制器（打击感凝固）—— ProcessMode=Always，墙钟驱动，绝不卡死 TimeScale
        var hitStop = new HitStopController { Name = "HitStop" };
        AddChild(hitStop);

        // 初始化提取的独立组件
        _pickupSpawner = new PickupSpawner { Name = "PickupSpawner" };
        AddChild(_pickupSpawner);
        _spawnPointGrid = new SpawnPointGrid { Name = "SpawnPointGrid" };
        AddChild(_spawnPointGrid);
        _directiveSystem = new DirectiveSystem { Name = "DirectiveSystem" };
        AddChild(_directiveSystem);

        // 初始化 4 大数学玩法系统
        if (GameConfig.MathSystemsEnabled)
        {
            WaveSystem = new WaveResonanceSystem { Name = "WaveSystem" };
            AddChild(WaveSystem);
            FieldSystem = new VectorFieldSystem { Name = "FieldSystem" };
            AddChild(FieldSystem);
            FractalSys = new FractalSystem { Name = "FractalSystem" };
            AddChild(FractalSys);
            StrategySys = new StrategySystem { Name = "StrategySystem" };
            AddChild(StrategySys);
        }

        if (ResourceLoader.Exists("res://scenes/Zombie.tscn"))
        {
            _zombieScene = GD.Load<PackedScene>("res://scenes/Zombie.tscn");
        }

        _spawnPointGrid.Rebuild();

        await ToSignal(GetTree(), "process_frame");

        // 炼金术阵法装饰层（await 后场景已就绪）
        if (GameConfig.MathSystemsEnabled)
        {
            var deco = new ZombieWars.UI.AlchemyDecoOverlay { Name = "AlchemyDeco" };
            GetTree().CurrentScene.AddChild(deco);
        }

        // 绑定波形系统到 UI 指示器
        if (GameConfig.MathSystemsEnabled && _uiManager != null && WaveSystem != null)
            _uiManager.BindWaveSystem(WaveSystem);

        if (_uiManager != null)
        {
            // 地面纹理按需加载（Background.LoadMap 时取当前地图一张），无需预热
            // 预加载武器图标纹理
            _weaponDeck?.PreWarmTextures();

            // JIT 预热
            _JitWarmup();

            while (_uiManager != null && _IsWelcomeVisible() && !GameConfig.IsAutotestMode)
            {
                await ToSignal(GetTree(), "process_frame");
            }
        }

        HighScore = SaveManager.GetHighScore();
        SaveManager.AddGamePlayed();
        LogUtils.Print($"最高分: {HighScore}");

        AchievementSystem.Instance?.ResetRoundStats();

        _StartExploring();
    }

    /// <summary>
    /// JIT 预热：在欢迎画面期间预实例化/调用关键战斗类和方法，
    /// 触发 .NET JIT 编译，避免首次选卡/战斗时一次性编译导致卡顿。
    /// </summary>
    private void _JitWarmup()
    {
        // 预实例化 Zombie/Projectile，触发类型 JIT
        try
        {
            var dummy = _zombieScene?.Instantiate() as Zombie;
            if (dummy != null)
            {
                dummy.ZombieType = ZombieType.Basic;
                dummy.QueueFree();
            }
        }
        catch { }

        try
        {
            var proj = new ZombieWars.Weapons.Projectile();
            proj.QueueFree();
        }
        catch { }

        // 预热选卡点击的完整调用链：ApplyWeaponEffect → RecalculateStats →
        // ShowUpgradeEffect → SpawnFloatingLabel → UpdatePlayerStatus → UpdateEffects
        // 这些方法首次调用时 JIT 编译耗时显著，提前触发后正式选卡时零开销。
        if (_player != null && _weaponDeck != null)
        {
            try
            {
                var warmupCard = _weaponDeck.GetRandomWeapon();
                if (warmupCard != null)
                {
                    _player.ApplyWeaponEffect(warmupCard);
                    // 立即移除预热用的卡片，恢复初始状态
                    var kept = new Godot.Collections.Array<ZombieWars.Weapons.WeaponCard>();
                    foreach (var e in _player.ActiveEffects)
                    {
                        if (e != warmupCard)
                            kept.Add(e);
                    }
                    _player.ActiveEffects = kept;
                    _player.Combat?.RecalculateStats();
                }
            }
            catch { }

            // 预热 VFX 反馈方法（创建即时销毁的临时节点，只触发 JIT 不留副作用）
            try { _player.Vfx?.ShowUpgradeEffect(); } catch { }
            try { ZombieWars.Vfx.VFXManager.SpawnFloatingLabel(
                GetTree().CurrentScene, _player.GlobalPosition, "warmup", Colors.Gray, 1); } catch { }
            try { _uiManager?.UpdatePlayerStatus(_player); } catch { }
            try { _uiManager?.ShowRoundBanner("warmup"); _uiManager?.HideRoundBanner(); } catch { }
            try { GameServices.Audio.PlayWeaponSelect(); } catch { }
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            HitStopController.Instance?.Reset();
            AchievementSystem.Instance?.OnGameEnded(_totalKills, _totalDamageDealt, _survivalTime);
        }
    }

    private bool _IsWelcomeVisible()
    {
        if (_uiManager == null) return false;
        return _uiManager.GetWelcomeVisible();
    }

    public override void _Process(double delta)
    {
        if (CurrentState == GameState.Combat)
        {
            _survivalTime += (float)delta;
            _HandleCombat(delta);

            // 数学系统：更新向量场节点衰减 + UI 指示器
            if (GameConfig.MathSystemsEnabled)
            {
                FieldSystem?.Update((float)delta);
                if (_uiManager != null)
                {
                    if (_player?.Combat != null)
                        _uiManager.UpdateWeaponFrequency(_player.Combat.GetCurrentWeaponFrequency());
                    _uiManager.UpdateStrategyIndicator(StrategySys);
                }
            }
        }
    }

    private void _HandleCombat(double delta)
    {
        // 持续探索模式：维持世界僵尸数（无波次、无回合结算）
        _MaintainZombies(delta);
        // 时间推进难度（替代波次结束推进 CurrentRound）
        _RampDifficultyOverTime(delta);

        _edgeIndicatorTimer += (float)delta;
        if (_edgeIndicatorTimer >= GameConfig.EdgeIndicatorInterval && _player != null && _uiManager != null)
        {
            _edgeIndicatorTimer = 0.0f;
            _UpdateEdgeIndicators();
        }

        _HandleBossPhases();
    }

    /// <summary>持续维持世界僵尸数：低于 Min 时补充，不超过 Max。</summary>
    private void _MaintainZombies(double delta)
    {
        // autotest 期间不持续刷怪，避免僵尸干扰攻击回归检查（攻击检查假设玩家血量可控）
        if (GameConfig.IsAutotestMode) return;
        if (_zombiesAlive >= GameConfig.WorldMaxZombies) return;
        _exploreMaintainTimer -= (float)delta;
        if (_exploreMaintainTimer > 0) return;
        _exploreMaintainTimer = GameConfig.ZombieSpawnCooldown;

        // 低于 Min 一次补到 Min；否则缓慢 +1，维持持续压力
        int target = _zombiesAlive < GameConfig.WorldMinZombies
            ? GameConfig.WorldMinZombies
            : _zombiesAlive + 1;
        int guard = 0;
        while (_zombiesAlive < target && _zombiesAlive < GameConfig.WorldMaxZombies && guard < 12)
        {
            SpawnZombieExplore();
            guard++;
        }
        _uiManager?.UpdateZombiesDisplay(_zombiesAlive, _currentRoundTotalZombies);
    }

    /// <summary>时间推进难度：每 DifficultyRampInterval 秒推进一"回合"，配置数学系统，Boss 周期触发。</summary>
    private void _RampDifficultyOverTime(double delta)
    {
        _difficultyRampTimer += (float)delta;
        if (_difficultyRampTimer < GameConfig.DifficultyRampInterval) return;
        _difficultyRampTimer = 0f;
        CurrentRound++;

        if (GameConfig.MathSystemsEnabled)
        {
            WaveSystem?.ConfigureForRound(CurrentRound);
            FieldSystem?.ConfigureForRound(CurrentRound);
            FractalSys?.ConfigureForRound(CurrentRound);
            StrategySys?.ConfigureForRound(CurrentRound);
        }
        _uiManager?.UpdateRoundDisplay(CurrentRound);
        _uiManager?.ShowRoundBanner($"威胁加剧 · 第 {CurrentRound} 阶");
        // 威胁阶数即回合并进：恢复回合成就/生存里程碑（随波次制删除后曾无触发源）
        AchievementSystem.Instance?.OnRoundSurvived(CurrentRound);
        ThreatTierAdvanced?.Invoke(CurrentRound);

        // Boss 周期触发（替代原每 5 回合），当前无 Boss 时才出
        if (CurrentRound % GameConfig.BossInterval == 0 && (BossZombie == null || !IsInstanceValid(BossZombie)))
            _SpawnBossZombie();
    }

    /// <summary>持续模式刷新单只僵尸到远离玩家的位置。</summary>
    private void SpawnZombieExplore()
    {
        SpawnZombie(_PickSpawnPointFarFromPlayer());
    }

    /// <summary>从刷怪网格中选一个远离玩家的点（屏幕外刷新，避免贴脸生成）。</summary>
    private Vector2 _PickSpawnPointFarFromPlayer()
    {
        if (_player == null) return _spawnPointGrid.PickRandom();
        Vector2 best = _spawnPointGrid.PickRandom();
        float bestDistSq = _player.GlobalPosition.DistanceSquaredTo(best);
        float minDistSq = GameConfig.ZombieSpawnMinDistFromPlayer * GameConfig.ZombieSpawnMinDistFromPlayer;
        for (int i = 0; i < 6; i++)
        {
            var p = _spawnPointGrid.PickRandom();
            float d = _player.GlobalPosition.DistanceSquaredTo(p);
            if (d > bestDistSq) { best = p; bestDistSq = d; }
            if (bestDistSq >= minDistSq) break;
        }
        return best;
    }

    // ── Boss 多阶段：血量跨越 66%/33% 时切换阶段，怒吼 + 加速 + 召唤援军 ──
    private void _HandleBossPhases()
    {
        if (BossZombie == null || !IsInstanceValid(BossZombie)) return;
        float ratio = (float)BossZombie.CurrentHealth / Mathf.Max(1, BossZombie.MaxHealth);

        if (_bossPhase == 1 && ratio <= 0.66f)
            _EnterBossPhase(2);
        else if (_bossPhase == 2 && ratio <= 0.33f)
            _EnterBossPhase(3);
    }

    private void _EnterBossPhase(int phase)
    {
        _bossPhase = phase;
        var boss = BossZombie;
        if (boss == null || !IsInstanceValid(boss)) return;

        // 怒吼演出：凝固 + 强震屏 + 橙红爆闪 + 警告横幅
        HitStopController.Instance?.Trigger(GameConfig.HitStopBossPhaseMs);
        _uiManager?.ShakeScreen(12.0f);
        _uiManager?.FlashBossPhase();
        GameServices.Audio.PlayLightning();
        _uiManager?.ShowRoundBanner(phase == 3 ? "BOSS 狂暴！" : "BOSS 暴怒！");

        // 狂暴加速
        boss.Speed *= phase == 3 ? 1.4f : 1.2f;

        // 召唤援军（阶段 3 召唤两波，加大压力）
        SpawnSwarmGroup(boss.GlobalPosition);
        if (phase == 3)
            SpawnSwarmGroup(boss.GlobalPosition);
    }

    private void _UpdateEdgeIndicators()
    {
        var screenSize = GetViewport().GetVisibleRect().Size;
        var screenRect = new Rect2(Vector2.Zero, screenSize).Grow(-20);
        var offscreenDir = Vector2.Zero;
        var offscreenCount = 0;

        foreach (var zombie in ZombieRegistry.All)
        {
            if (!IsInstanceValid(zombie))
                continue;
            if (!screenRect.HasPoint(zombie.GlobalPosition))
            {
                offscreenDir += (zombie.GlobalPosition - _player.GlobalPosition).Normalized();
                offscreenCount++;
            }
        }

        if (offscreenCount > GameConfig.EdgeIndicatorMinCount && offscreenDir.Length() > 0.5f)
        {
            var danger = Mathf.Clamp((float)offscreenCount / 10.0f, 0.0f, 1.0f);
            _uiManager.ShowOffscreenIndicator(offscreenDir.Normalized(), danger);
        }
    }

    /// <summary>持续探索模式启动（饥荒式）：直接进入战斗探索，不弹选卡，僵尸在世界持续刷新。</summary>
    private void _StartExploring()
    {
        CurrentRound = 1;
        _bossPhase = 0;
        CurrentState = GameState.Combat;

        if (GameConfig.MathSystemsEnabled)
        {
            WaveSystem?.ConfigureForRound(CurrentRound);
            FieldSystem?.ConfigureForRound(CurrentRound);
            FractalSys?.ConfigureForRound(CurrentRound);
            StrategySys?.ConfigureForRound(CurrentRound);
        }

        _directiveSystem.OnRoundStart(CurrentRound);
        _directiveSystem.OnCombatActivated();

        _exploreMaintainTimer = 0.5f;  // 短延迟后开始补充僵尸
        _difficultyRampTimer = 0f;

        if (_uiManager != null)
        {
            _uiManager.UpdateRoundDisplay(CurrentRound);
            _uiManager.UpdateZombiesDisplay(_zombiesAlive, _currentRoundTotalZombies);
            if (_player != null)
                _uiManager.UpdateHealthDisplay(_player.CurrentHealth, _player.MaxHealth);
        }
        Inventory.Clear(); // 新局重置库存
        LogUtils.Print("进入持续探索模式（饥荒式）— 自由探索，僵尸持续刷新");
    }

    /// <summary>测试专用跳关：回归/性能测试在 headless 下无欢迎画面点击时，
    /// 直接切入战斗态。正式流程经 _StartExploring 进入，此方法仅测试调用。</summary>
    public void DebugForceCombat()
    {
        if (CurrentState == GameState.GameOver) return;
        CurrentState = GameState.Combat;
        _directiveSystem.OnCombatActivated();
    }

    /// <summary>测试专用跳关见 DebugForceCombat。</summary>

    public void SpawnZombie(Vector2? forcedPos = null)
    {
        if (_zombieScene == null)
        {
            GD.PushWarning("僵尸场景未加载");
            return;
        }

        if (CurrentRound % GameConfig.BossInterval == 0 && _zombiesSpawned == 0 && CurrentRound > 0)
        {
            _SpawnBossZombie();
            return;
        }

        var zombieInstance = _zombieScene.Instantiate() as Zombie;
        if (zombieInstance == null)
        {
            GD.PushWarning("无法创建僵尸实例");
            return;
        }

        var zombieType = _PickZombieType();
        zombieInstance.ZombieType = zombieType;

        var eliteChance = Mathf.Min(EliteChanceMax, EliteChanceBase + EliteChancePerRound * Mathf.Max(0, CurrentRound - 1));
        if (GD.Randf() < eliteChance)
        {
            zombieInstance.SetElite(true);
        }

        var spawnPos = forcedPos ?? _spawnPointGrid.PickRandom();
        var offset = new Vector2(
            GD.Randf() * 2.0f * GameConfig.SpawnOffsetRange - GameConfig.SpawnOffsetRange,
            GD.Randf() * 2.0f * GameConfig.SpawnOffsetRange - GameConfig.SpawnOffsetRange
        );

        // 数学系统：波形共振——僵尸按傅里叶合成波分布生成
        // （持续探索模式无波次总量，进度固定 0，相位仍随累计生成数推进）
        Vector2 waveOffset = Vector2.Zero;
        if (GameConfig.MathSystemsEnabled && WaveSystem != null && WaveSystem.HarmonicCount > 0)
        {
            waveOffset = WaveSystem.GetSpawnOffset(0.0f, _zombiesSpawned * 0.5f);
        }

        // 世界边界 clamp：waveOffset 可能把僵尸推出世界外
        var worldSize = MapContext.CurrentWorldSize;
        var finalPos = spawnPos + offset + waveOffset;
        finalPos = new Vector2(
            Mathf.Clamp(finalPos.X, GameConfig.SpawnMargin, worldSize.X - GameConfig.SpawnMargin),
            Mathf.Clamp(finalPos.Y, GameConfig.SpawnTopMarginFallback, worldSize.Y - GameConfig.SpawnMargin));
        zombieInstance.GlobalPosition = finalPos;

        zombieInstance.SetPlayerTarget(_player);
        zombieInstance.Died += _OnZombieDied;
        GetTree().CurrentScene.AddChild(zombieInstance);

        var roundMult = _GetRoundMult();
        zombieInstance.ApplyRoundScaling(CurrentRound, roundMult);

        // 数学系统：分形——R7+ 普通僵尸 25% 概率标记为分形体
        if (GameConfig.MathSystemsEnabled && FractalSystem.IsFractalActive(CurrentRound)
            && !zombieInstance.IsElite && GD.Randf() < 0.25f)
        {
            zombieInstance.SetFractal(0, (int)GD.RandRange(2, 3));
        }

        _zombiesSpawned++;
        _zombiesAlive++;
        _currentRoundTotalZombies++;

        _uiManager?.UpdateZombiesDisplay(_zombiesAlive, _currentRoundTotalZombies);

        // 仅 Boss/精英/分形时打印日志，普通僵尸生成不刷屏
        if (zombieInstance.IsBoss || zombieInstance.IsElite || zombieInstance.IsFractal)
        {
            var typeName = TypeNames.TryGetValue((int)zombieType, out var name) ? name : "未知";
            LogUtils.Print($"生成僵尸 #{_zombiesSpawned} (类型: {typeName}, 强度: x{roundMult:F2})");
        }
    }

    /// <summary>难度系数：线性增长（取代对数），保证后期压力。</summary>
    private float _GetRoundMult(float factor = 1.0f) =>
        1.0f + CurrentRound * RoundMultiplierBase * 2.0f * factor;

    /// <summary>
    /// 生成分形僵尸的子体（由父体死亡触发）。
    /// </summary>
    public void SpawnFractalChild(Vector2 pos, int hp, int damage, float speed,
        ZombieType type, int depth, int branching)
    {
        if (_zombieScene == null) return;
        var child = _zombieScene.Instantiate() as Zombie;
        if (child == null) return;

        child.ZombieType = type;
        child.GlobalPosition = pos;
        child.SetPlayerTarget(_player);
        child.Died += _OnZombieDied;
        child.SetFractal(depth, branching);

        GetTree().CurrentScene.AddChild(child);

        var roundMult = _GetRoundMult();
        child.ApplyRoundScaling(CurrentRound, roundMult);
        child.OverrideStats(hp, damage, speed);

        // 深度越深越小越透明
        float sizeScale = Mathf.Pow(branching, -0.5f * depth);
        child.Scale = new Vector2(sizeScale, sizeScale);
        child.Modulate = new Color(1, 1, 1, ZombieWars.Math.FractalSystem.GetDepthAlpha(depth));

        _zombiesAlive++;
        _currentRoundTotalZombies++;
    }

    /// <summary>放置向量场力场节点（玩家请求）</summary>
    public bool PlaceFieldNode(Vector2 pos, GameEnums.FieldType type)
    {
        if (!GameConfig.MathSystemsEnabled || FieldSystem == null) return false;
        if (CurrentState != GameState.Combat) return false;
        if (!FieldSystem.PlaceNode(pos, type)) return false;

        var visual = new ZombieWars.World.VectorFieldNode();
        visual.Setup(type, type switch
        {
            GameEnums.FieldType.Attractor => GameConfig.FieldAttractorStrength,
            GameEnums.FieldType.Repulsor => GameConfig.FieldRepulsorStrength,
            GameEnums.FieldType.Vortex => GameConfig.FieldVortexStrength,
            _ => 0.0f
        }, GameConfig.FieldNodeDuration);
        visual.GlobalPosition = pos;
        GetTree().CurrentScene.AddChild(visual);

        _uiManager?.UpdateFieldCharges(FieldSystem.AvailableCharges, FieldSystem.MaxActiveNodes);
        return true;
    }

    private void _SpawnBossZombie()
    {
        var zombieInstance = _zombieScene.Instantiate() as Zombie;
        if (zombieInstance == null) return;

        // Boss 多样化：从高威胁类型中随机选择，不再固定 Tank
        var bossTypes = new[] { ZombieType.Tank, ZombieType.Mech, ZombieType.Knight, ZombieType.Dragon };
        zombieInstance.Set("zombie_type", (int)bossTypes[(int)(GD.Randi() % bossTypes.Length)]);

        var screenSize = GetViewport().GetVisibleRect().Size;
        zombieInstance.GlobalPosition = new Vector2(screenSize.X / 2.0f, screenSize.Y * 0.2f);

        zombieInstance.SetPlayerTarget(_player);
        zombieInstance.Died += _OnZombieDied;
        GetTree().CurrentScene.AddChild(zombieInstance);

        var roundMult = _GetRoundMult();
        zombieInstance.ApplyRoundScaling(CurrentRound, roundMult);
        zombieInstance.SetBoss();
        BossZombie = zombieInstance;

        _zombiesSpawned++;
        _zombiesAlive++;
        _currentRoundTotalZombies++;

        _uiManager?.UpdateZombiesDisplay(_zombiesAlive, _currentRoundTotalZombies);
        _uiManager?.ShowRoundBanner("BOSS 来袭");
        _uiManager?.PlayBossWarning(_BossTypeName(zombieInstance.ZombieType));

        // 出场演出：命中停顿 + 强震屏 + 全屏暗化，把"Boss 登场"砸出压迫感
        _bossPhase = 1;
        HitStopController.Instance?.Trigger(GameConfig.HitStopBossSpawnMs);
        _uiManager?.ShakeScreen(14.0f);
        _uiManager?.FlashBossSpawn();
        GameServices.Audio.PlayExplosion();

        LogUtils.Print($"BOSS 生成！回合 {CurrentRound}");
    }

    private ZombieType _PickZombieType()
    {
        Dictionary<int, float> weights = new();

        foreach (var entry in SpawnTable)
        {
            if (CurrentRound <= entry.MaxRound)
            {
                weights = entry.Weights;
                break;
            }
        }

        if (weights.Count == 0) return ZombieType.Basic;

        // 数学系统：博弈论——根据玩家武器偏好调整出兵权重（纳什均衡）
        if (GameConfig.MathSystemsEnabled && StrategySys != null
            && ZombieWars.Math.StrategySystem.IsStrategyActive(CurrentRound))
        {
            var baseWeights = new Dictionary<ZombieType, float>();
            foreach (var kvp in weights)
                baseWeights[(ZombieType)kvp.Key] = kvp.Value;
            var adjusted = StrategySys.AdjustWeights(baseWeights);
            weights.Clear();
            foreach (var kvp in adjusted)
                weights[(int)kvp.Key] = kvp.Value;
        }

        var total = 0.0f;
        foreach (var w in weights.Values) total += w;

        var roll = GD.Randf() * total;
        var cumulative = 0.0f;
        foreach (var kvp in weights)
        {
            cumulative += kvp.Value;
            if (roll <= cumulative) return (ZombieType)kvp.Key;
        }

        return ZombieType.Basic;
    }

    private void _TrySpawnSwarmGroup(Vector2 centerPos)
    {
        if (_zombieScene == null) return;
        // Spawn 3-5 swarm creatures around the center
        int count = (int)GD.RandRange(3, 6);
        for (int i = 0; i < count; i++)
        {
            var offset = new Vector2(
                (float)GD.RandRange(-40, 40),
                (float)GD.RandRange(-40, 40)
            );
            var swarm = _zombieScene.Instantiate() as Zombie;
            if (swarm == null) continue;
            swarm.ZombieType = ZombieType.Swarm;
            swarm.GlobalPosition = centerPos + offset;
            swarm.SetPlayerTarget(_player);
            swarm.Died += _OnMinionDied;
            GetTree().CurrentScene.AddChild(swarm);

            var roundMult = _GetRoundMult(0.8f);
            swarm.ApplyRoundScaling(CurrentRound, roundMult);
            _zombiesAlive++;
            _currentRoundTotalZombies++;
        }
    }

    private void _OnZombieDied(Zombie zombie)
    {
        _zombiesAlive = Math.Max(0, _zombiesAlive - 1);
        _totalKills++;

        // Notify achievement system
        AchievementSystem.Instance?.OnZombieKilled(zombie.ZombieType, zombie.IsElite, zombie.IsBoss);

        var zombieType = (int)zombie.ZombieType;
        var basePts = TypeScore.TryGetValue(zombieType, out var s) ? s : 10;
        var pts = zombie.IsElite
            ? (int)(basePts * GameConfig.EliteScoreMult)
            : basePts;

        if (zombie.IsBoss)
        {
            pts = (int)(pts * GameConfig.BossScoreMult);
            BossZombie = null;
            _bossPhase = 0;
            // 击杀大演出：慢动作凝固 + 极强震屏 + 白金强光，胜利的仪式感
            HitStopController.Instance?.Trigger(GameConfig.HitStopBossKillMs);
            _uiManager?.ShakeScreen(20.0f);
            _uiManager?.FlashBossKill();
            GameServices.Audio.PlayExplosion();

            // Boss 击杀视觉收尾：超大金色冲击波环 + 粒子爆发，史诗级处决感
            var scene = GetTree().CurrentScene;
            ZombieWars.Vfx.VFXManager.SpawnImpactRing(scene, zombie.GlobalPosition, 280.0f, new Color(1.0f, 0.92f, 0.55f), 6.0f);
            ZombieWars.Vfx.VFXManager.SpawnParticleBurst(scene, zombie.GlobalPosition, new Color(1.0f, 0.85f, 0.4f), GameConfig.VfxExplosionParticles);
        }
        else if (zombie.IsElite)
        {
            HitStopController.Instance?.Trigger(GameConfig.HitStopEliteKillMs);
        }

        Score += pts;

        SaveManager.AddKill();

        _uiManager?.UpdateZombiesDisplay(_zombiesAlive, _currentRoundTotalZombies);
        _uiManager?.UpdateScoreDisplay(Score);
        // 连击 UI 由 PlayerCombat.OnKill() 驱动（单一数据源），不再在此处 AddCombo
        _uiManager?.ShowKillEffect(zombie.GlobalPosition, pts);

        if (_player?.Combat != null)
            _uiManager?.ShowComboNotification(_player.Combat.CurrentCombo);

        // Try to drop a pickup
        _pickupSpawner.TryDrop(zombie, CurrentRound, _totalKills);
        int directiveBonus = _directiveSystem.OnZombieKilled(zombie, pts);
        if (directiveBonus > 0)
        {
            Score += directiveBonus;
            _uiManager?.UpdateScoreDisplay(Score);
        }
    }

    private void _OnPlayerDied()
    {
        // 游戏结束立即解除任何命中停顿，避免 GameOver 界面凝固
        HitStopController.Instance?.Reset();
        GameServices.Audio.ResetKillStreak();
        CurrentState = GameState.GameOver;
        GameOver?.Invoke();

        foreach (var zombie in ZombieRegistry.All)
        {
            if (IsInstanceValid(zombie))
                zombie.QueueFree();
        }

        _zombiesAlive = 0;
        _directiveSystem.OnPlayerDied();
        ZombieRegistry.Clear();

        LogUtils.Print($"游戏结束！坚持了 {CurrentRound} 回合，得分: {Score}");

        SaveManager.SaveHighScore(Score);
        SaveManager.AddRoundsSurvived(CurrentRound);
        // 跨局成长：按得分与击杀结算金币（得分/50 + 总击杀），供永久升级消费
        SaveManager.AddCoins(Score / 50 + _totalKills);
        SaveManager.Flush(); // 游戏结束立即写盘，避免数据丢失
    }



    public void SetPlayer(Node playerNode)
    {
        _player = (Player)playerNode;
        PlayerRef = playerNode as Player;
        _player.Died += _OnPlayerDied;
        _directiveSystem.Setup(_uiManager!, _player);
    }

    public void SetWeaponDeck(Node deck)
    {
        _weaponDeck = (WeaponDeck)deck;
    }

    /// <summary>宝箱卡牌奖励（选卡弱化）：给玩家一件随机武器，替代原每回合强制选卡。</summary>
    public void GrantChestReward()
    {
        if (_player == null || _weaponDeck == null) return;
        var card = _weaponDeck.GetRandomWeapon();
        if (card == null) return;
        _player.ApplyWeaponEffect(card);
        _uiManager?.UpdateHealthDisplay(_player.CurrentHealth, _player.MaxHealth);
        _uiManager?.ShowLevelUpNotification($"宝箱奖励: {card.CardName}", new Color(0.8f, 0.6f, 0.3f));
        GameServices.Audio?.PlayUpgrade();
    }

    public int GetCurrentRound() => CurrentRound;
    public int GetZombiesRemaining() => _zombiesAlive;
    public void SpawnMinionZombie(Vector2 pos)
    {
        if (_zombieScene == null || _zombiesAlive >= ZombiesMaxPerRound + 10) return;

        var minion = _zombieScene.Instantiate() as Zombie;
        if (minion == null) return;

        minion.ZombieType = ZombieType.Basic;
        minion.GlobalPosition = pos;
        minion.SetPlayerTarget(_player);
        minion.Died += _OnMinionDied;
        GetTree().CurrentScene.AddChild(minion);
        ZombieRegistry.Register(minion);

        var roundMult = _GetRoundMult(1.2f);
        minion.ApplyRoundScaling(CurrentRound, roundMult);

        _zombiesAlive++;
        _currentRoundTotalZombies++;
        _uiManager?.UpdateZombiesDisplay(_zombiesAlive, _currentRoundTotalZombies);
    }

    /// <summary>Called by WeatherSystem during storm to spawn an extra zombie.</summary>
    public void SpawnExtraZombie()
    {
        if (_zombieScene == null || CurrentState != GameState.Combat) return;
        if (_zombiesAlive >= ZombiesMaxPerRound + 10) return;

        var zombieInstance = _zombieScene.Instantiate() as Zombie;
        if (zombieInstance == null) return;

        // Spawn type based on current round progression
        zombieInstance.ZombieType = _PickZombieType();
        zombieInstance.SetPlayerTarget(_player);
        zombieInstance.Died += _OnMinionDied;

        // Use center spawn
        zombieInstance.GlobalPosition = new Vector2(
            MapContext.CurrentWorldSize.X / 2.0f + (float)GD.RandRange(-100, 100),
            MapContext.CurrentWorldSize.Y / 2.0f + (float)GD.RandRange(-100, 100)
        );

        var roundMult = _GetRoundMult(1.5f);
        zombieInstance.ApplyRoundScaling(CurrentRound, roundMult);

        GetTree().CurrentScene.AddChild(zombieInstance);
        ZombieRegistry.Register(zombieInstance);
        _zombiesAlive++;
        _currentRoundTotalZombies++;
    }

    /// <summary>Called by WeatherSystem when weather changes.</summary>
    public void OnWeatherChanged(int weatherType, int intensity)
    {
        LogUtils.Print($"天气效果变化: 类型={weatherType}, 强度={intensity}");
        // Apply weather modifiers to zombie speed/damage if needed
        // For now, WeatherSystem handles modifiers directly
    }

    private void _OnMinionDied(Zombie zombie)
    {
        _zombiesAlive = Math.Max(0, _zombiesAlive - 1);
        _totalKills++;

        AchievementSystem.Instance?.OnZombieKilled(zombie.ZombieType, false, false);

        Score += 5;

        SaveManager.AddKill();

        _uiManager?.UpdateScoreDisplay(Score);
        _uiManager?.UpdateZombiesDisplay(_zombiesAlive, _currentRoundTotalZombies);
        if (_player?.Combat != null)
            _uiManager?.ShowComboNotification(_player.Combat.CurrentCombo);
        int minionBonus = _directiveSystem.OnZombieKilled(zombie, 5);
        if (minionBonus > 0)
        {
            Score += minionBonus;
            _uiManager?.UpdateScoreDisplay(Score);
        }
    }

    public void SpawnSwarmGroup(Vector2 pos)
    {
        _TrySpawnSwarmGroup(pos);
    }

    public void SpawnShipGroup(Vector2 pos)
    {
        if (_zombieScene == null || _zombiesAlive >= ZombiesMaxPerRound + 5) return;

        int count = (int)GD.RandRange(2, 4);
        for (int i = 0; i < count; i++)
        {
            var offset = new Vector2(
                (float)GD.RandRange(-60, 60),
                (float)GD.RandRange(-40, 40)
            );
            var ship = _zombieScene.Instantiate() as Zombie;
            if (ship == null) continue;
            ship.ZombieType = ZombieType.Ship;
            ship.GlobalPosition = pos + offset;
            ship.SetPlayerTarget(_player);
            ship.Died += _OnMinionDied;
            GetTree().CurrentScene.AddChild(ship);
            ZombieRegistry.Register(ship);

            var roundMult = _GetRoundMult(0.7f);
            ship.ApplyRoundScaling(CurrentRound, roundMult);
            _zombiesAlive++;
            _currentRoundTotalZombies++;
        }
    }

    /// <summary>Spawn a pickup at the given position. Called when a zombie dies.</summary>

    public void SetUIManager(UIManager ui)
    {
        _uiManager = ui;
        _pickupSpawner?.Setup(ui);
        if (_player != null)
            _directiveSystem?.Setup(ui, _player);
    }

    /// <summary>把 ZombieType 翻译成中文显示名（Boss 出场警告横幅用）。</summary>
    private static string _BossTypeName(ZombieType type) => type switch
    {
        ZombieType.Tank   => "巨型坦克僵尸",
        ZombieType.Mech   => "重型机甲僵尸",
        ZombieType.Knight => "骑士僵尸领主",
        ZombieType.Dragon => "飞龙僵尸",
        _                 => "未知 BOSS",
    };
}
