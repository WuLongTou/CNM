namespace ZombieWars.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static ZombieWars.Core.GameEnums;
using Player = ZombieWars.Player.Player;
using Zombie = ZombieWars.Enemy.Zombie;
using WeaponDeck = ZombieWars.Weapons.WeaponDeck;
using UIManager = ZombieWars.UI.UIManager;

/// <summary>
/// Game Manager — orchestrates game state, round flow, zombie spawning, and difficulty scaling.
/// </summary>
public partial class GameManager : Node
{
    public event Action<int> RoundStarted = delegate {};
    public event Action<int> RoundEnded = delegate {};
    public event Action WeaponSelectionStarted = delegate {};
    public event Action WeaponSelectionEnded = delegate {};
    public event Action GameOver = delegate {};

    [Export] public int ZombiesPerRoundBase = 3;
    [Export] public int ZombiesIncreasePerRound = 1;
    [Export] public int ZombiesMaxPerRound = 40;

    [Export] public float SpawnInterval = 2.0f;
    [Export] public float SpawnIntervalMin = 0.8f;
    [Export] public float SpawnIntervalDecay = 0.06f;

    [Export] public float RoundMultiplierBase = 0.04f;
    [Export] public float EliteChanceBase = 0.02f;
    [Export] public float EliteChancePerRound = 0.010f;
    [Export] public float EliteChanceMax = 0.18f; // Balanced from 0.25f

    [Export] public bool HealBetweenRounds = true;
    [Export] public float HealPercent = 0.20f;

    public GameState CurrentState { get; private set; } = GameState.WeaponSelection;
    public int CurrentRound { get; private set; } = 0;

    private int _zombiesToSpawn;
    private int _zombiesSpawned;
    private int _zombiesAlive;
    private float _spawnTimer;
    private float _currentSpawnInterval;

    // Per-round battle stats
    private int _roundKills;
    private int _roundDamageDealt;
    private int _roundDamageTaken;
    private int _totalKills;
    private int _totalDamageDealt;

    public Player? PlayerRef { get; private set; }
    public Zombie? BossZombie { get; private set; }
    private WeaponDeck? _weaponDeck;
    private Player _player = null!;
    private PackedScene _zombieScene = null!;
    private List<Vector2> _spawnPoints = new();

    private UIManager _uiManager = null!;

    public int Score { get; private set; }
    public int HighScore { get; private set; }

    private float _edgeIndicatorTimer;

    private static readonly Dictionary<int, string> TypeNames = new()
    {
        { 0, "基础" }, { 1, "快速" }, { 2, "坦克" }, { 3, "爆炸" },
        { 4, "隐身" }, { 5, "喷毒" }, { 6, "治疗" }, { 7, "召唤师" }, { 8, "射手" }
    };

    private static readonly Dictionary<int, int> TypeScore = new()
    {
        { 0, 10 }, { 1, 15 }, { 2, 25 }, { 3, 30 },
        { 4, 35 }, { 5, 40 }, { 6, 50 }, { 7, 60 }, { 8, 45 }
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
        new() { MaxRound = 9999, Weights = new() { { 0, 0.06f }, { 1, 0.08f }, { 2, 0.15f }, { 3, 0.12f }, { 4, 0.10f }, { 5, 0.12f }, { 6, 0.10f }, { 7, 0.07f }, { 8, 0.10f } } },
    };

    public override async void _Ready()
    {
        AddToGroup("game_manager");

        if (ResourceLoader.Exists("res://scenes/Zombie.tscn"))
        {
            _zombieScene = GD.Load<PackedScene>("res://scenes/Zombie.tscn");
        }

        _UpdateSpawnPoints();
        GetViewport().SizeChanged += _OnViewportSizeChanged;

        await ToSignal(GetTree(), "process_frame");

        if (_uiManager != null)
        {
            while (_uiManager != null && _IsWelcomeVisible())
            {
                await ToSignal(GetTree(), "process_frame");
            }
        }

        var saveManager = GetNodeOrNull("/root/Main/SaveManager");
        if (saveManager != null)
        {
            HighScore = _GetHighScore(saveManager);
            _AddGamePlayed(saveManager);
            LogUtils.Print($"最高分: {HighScore}");
        }

        StartNewRound();
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
            _HandleCombat(delta);
        }
    }

    private void _HandleCombat(double delta)
    {
        if (_zombiesSpawned < _zombiesToSpawn)
        {
            _spawnTimer -= (float)delta;
            if (_spawnTimer <= 0)
            {
                SpawnZombie();
                _spawnTimer = _currentSpawnInterval;
            }
        }

        _edgeIndicatorTimer += (float)delta;
        if (_edgeIndicatorTimer >= GameConfig.EdgeIndicatorInterval && _player != null && _uiManager != null)
        {
            _edgeIndicatorTimer = 0.0f;
            _UpdateEdgeIndicators();
        }

        if (_zombiesSpawned >= _zombiesToSpawn && _zombiesAlive <= 0)
        {
            EndRound();
        }
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

    public void StartNewRound()
    {
        CurrentRound++;
        CurrentState = GameState.WeaponSelection;

        // Reset per-round stats
        _roundKills = 0;
        _roundDamageDealt = 0;
        _roundDamageTaken = 0;

        var roundFactor = Mathf.Sqrt((float)CurrentRound) * 1.5f;
        _zombiesToSpawn = ZombiesPerRoundBase + (int)roundFactor + (CurrentRound - 1) * ZombiesIncreasePerRound;
        _zombiesToSpawn = Mathf.Min(_zombiesToSpawn, ZombiesMaxPerRound);
        _zombiesSpawned = 0;
        _zombiesAlive = 0;

        _currentSpawnInterval = Mathf.Max(SpawnIntervalMin, SpawnInterval * Mathf.Pow(0.94f, Mathf.Max(0, CurrentRound - 1)));
        _spawnTimer = _currentSpawnInterval;

        if (HealBetweenRounds && _player != null && CurrentRound > 1)
        {
            var playerMaxHp = _player.MaxHealth;
            var healAmount = (int)(playerMaxHp * HealPercent);
            _player.Heal(healAmount);
            _uiManager?.ShowDamageNumber(_player.GlobalPosition, -healAmount, false);
        }

        RoundStarted?.Invoke(CurrentRound);
        WeaponSelectionStarted?.Invoke();

        if (_uiManager != null)
        {
            _uiManager.UpdateRoundDisplay(CurrentRound);
            _uiManager.UpdateZombiesDisplay(_GetRemainingZombies(), _zombiesToSpawn);
            if (_player != null)
            {
                _uiManager.UpdateHealthDisplay(_player.CurrentHealth, _player.MaxHealth);
            }
        }

        var currentScene = GetTree().CurrentScene;
        var bg = currentScene?.GetNodeOrNull("Background");
        if (bg != null && bg.HasMethod("set_tension"))
        {
            bg.Call("set_tension", Mathf.Clamp((float)(CurrentRound - 1) / 15.0f, 0.0f, 1.0f));
        }

        if (CurrentRound % 5 == 0 && _uiManager != null)
        {
            _uiManager.TriggerBossAtmosphere();
        }

        LogUtils.Print($"第 {CurrentRound} 回合开始！需要消灭 {_zombiesToSpawn} 只僵尸");
    }

    public async void EndRound()
    {
        CurrentState = GameState.RoundTransition;
        RoundEnded?.Invoke(CurrentRound);

        LogUtils.Print($"第 {CurrentRound} 回合结束！");

        _uiManager?.ShowRoundSummary(CurrentRound, _roundKills, _totalKills, Score);
        _uiManager?.ShowRoundBanner("回合完成");

        await ToSignal(GetTree().CreateTimer(GameConfig.RoundTransitionDelay), Timer.SignalName.Timeout);

        // Get ready phase - show countdown before weapon selection
        CurrentState = GameState.GetReady;
        _uiManager?.HideRoundSummary();
        await _ShowGetReadyCountdown();

        // Now start the new round with weapon selection
        StartNewRound();
    }

    private async Task _ShowGetReadyCountdown()
    {
        _uiManager?.ShowRoundBanner("准备!", true); // true = show as countdown style

        for (int i = (int)GameConfig.GetReadyDuration; i > 0; i--)
        {
            _uiManager?.UpdateCountdown(i);
            await ToSignal(GetTree().CreateTimer(1.0f), Timer.SignalName.Timeout);
        }

        _uiManager?.HideRoundBanner();
    }

    public void OnWeaponSelected(object? card)
    {
        if (CurrentState != GameState.WeaponSelection) return;

        if (_player != null)
        {
            if (card != null && card.GetType().Name.Contains("UpgradeCard"))
            {
                _player.ApplyUpgradeCard(card);
            }
            else
            {
                _player.ApplyWeaponEffect(card);
            }

            _uiManager?.UpdateHealthDisplay(_player.CurrentHealth, _player.MaxHealth);
        }

        CurrentState = GameState.Combat;
        WeaponSelectionEnded?.Invoke();
        _uiManager?.HideWeaponSelection();

        var cardName = card != null ? card.GetType().Name : "null";
        LogUtils.Print($"选择了: {cardName}，效果已生效！");
    }

    public void OnUpgradeSkipped()
    {
        if (CurrentState != GameState.WeaponSelection) return;

        CurrentState = GameState.Combat;
        WeaponSelectionEnded?.Invoke();
        _uiManager?.HideWeaponSelection();

        LogUtils.Print("跳过升级！直接战斗！");
    }

    public void SpawnZombie()
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

        if (_spawnPoints.Count == 0)
        {
            _UpdateSpawnPoints();
        }
        var spawnPos = _spawnPoints[(int)(GD.Randi() % _spawnPoints.Count)];
        var offset = new Vector2(
            GD.Randf() * 2.0f * GameConfig.SpawnOffsetRange - GameConfig.SpawnOffsetRange,
            GD.Randf() * 2.0f * GameConfig.SpawnOffsetRange - GameConfig.SpawnOffsetRange
        );
        zombieInstance.GlobalPosition = spawnPos + offset;

        zombieInstance.SetPlayerTarget(_player);
        zombieInstance.Died += _OnZombieDied;
        GetTree().CurrentScene.AddChild(zombieInstance);

        var roundMult = 1.0f + Mathf.Log(1.0f + CurrentRound) * RoundMultiplierBase * 2.0f;
        zombieInstance.ApplyRoundScaling(CurrentRound, roundMult);

        _zombiesSpawned++;
        _zombiesAlive++;

        _uiManager?.UpdateZombiesDisplay(_GetRemainingZombies(), _zombiesToSpawn);

        var typeName = TypeNames.TryGetValue((int)zombieType, out var name) ? name : "未知";
        LogUtils.Print($"生成僵尸 {_zombiesSpawned}/{_zombiesToSpawn} (类型: {typeName}, 强度: x{roundMult:F2})");
    }

    private void _SpawnBossZombie()
    {
        var zombieInstance = _zombieScene.Instantiate() as Zombie;
        if (zombieInstance == null) return;

        zombieInstance.Set("zombie_type", (int)ZombieType.Tank);

        var screenSize = GetViewport().GetVisibleRect().Size;
        zombieInstance.GlobalPosition = new Vector2(screenSize.X / 2.0f, screenSize.Y * 0.2f);

        zombieInstance.SetPlayerTarget(_player);
        zombieInstance.Died += _OnZombieDied;
        GetTree().CurrentScene.AddChild(zombieInstance);

        var roundMult = 1.0f + Mathf.Log(1.0f + CurrentRound) * RoundMultiplierBase * 2.0f;
        zombieInstance.ApplyRoundScaling(CurrentRound, roundMult);
        zombieInstance.SetBoss();
        BossZombie = zombieInstance;

        _zombiesSpawned++;
        _zombiesAlive++;

        _uiManager?.UpdateZombiesDisplay(_GetRemainingZombies(), _zombiesToSpawn);
        _uiManager?.ShowRoundBanner("⚠ BOSS 来袭 ⚠");

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

    private void _OnZombieDied(Zombie zombie)
    {
        _zombiesAlive--;
        _roundKills++;
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
        }

        Score += pts;

        var saveManager = GetNodeOrNull("/root/Main/SaveManager");
        if (saveManager != null) _AddKill(saveManager);

        _uiManager?.UpdateZombiesDisplay(_GetRemainingZombies(), _zombiesToSpawn);
        _uiManager?.UpdateScoreDisplay(Score);
        _uiManager?.AddCombo();
        _uiManager?.ShowKillEffect(zombie.GlobalPosition, pts);

        // Track combo in UI
        if (_player?.Combat != null)
            _uiManager?.ShowComboNotification(_player.Combat.CurrentCombo);
    }

    private void _OnPlayerDied()
    {
        CurrentState = GameState.GameOver;
        GameOver?.Invoke();

        foreach (var zombie in ZombieRegistry.All)
        {
            if (IsInstanceValid(zombie))
            {
                zombie.SetPhysicsProcess(false);
                zombie.SetProcess(false);
            }
        }

        _spawnTimer = 999999.0f;
        _zombiesToSpawn = 0;
        _zombiesAlive = 0;
        ZombieRegistry.Clear();

        LogUtils.Print($"游戏结束！坚持了 {CurrentRound} 回合，得分: {Score}");

        var saveManager = GetNodeOrNull("/root/Main/SaveManager");
        if (saveManager != null)
        {
            _SaveHighScore(saveManager, Score);
            _AddRoundsSurvived(saveManager, CurrentRound);
        }
    }

    private void _UpdateSpawnPoints()
    {
        _spawnPoints.Clear();
        var worldSize = GameConfig.WorldSize;
        var margin = GameConfig.SpawnMargin;
        var topMargin = GameConfig.SpawnTopMarginFallback;

        // Spawn around the edges of the larger world
        var topEdge = topMargin + margin;
        var bottomEdge = worldSize.Y - margin;
        var leftEdge = margin;
        var rightEdge = worldSize.X - margin;

        // Top and bottom edges
        for (int i = 0; i < 6; i++)
            _spawnPoints.Add(new Vector2(i * worldSize.X / 5.0f, topEdge));
        for (int i = 0; i < 6; i++)
            _spawnPoints.Add(new Vector2(i * worldSize.X / 5.0f, bottomEdge));

        // Left and right edges
        for (int i = 0; i < 5; i++)
            _spawnPoints.Add(new Vector2(leftEdge, topEdge + i * (worldSize.Y - topEdge - bottomEdge) / 4.0f));
        for (int i = 0; i < 5; i++)
            _spawnPoints.Add(new Vector2(rightEdge, topEdge + i * (worldSize.Y - topEdge - bottomEdge) / 4.0f));
    }

    private void _OnViewportSizeChanged()
    {
        _UpdateSpawnPoints();
    }

    public void SetPlayer(Node playerNode)
    {
        _player = (Player)playerNode;
        PlayerRef = playerNode as Player;
        _player.Died += _OnPlayerDied;
    }

    public void SetWeaponDeck(Node deck)
    {
        _weaponDeck = (WeaponDeck)deck;
    }

    public int GetCurrentRound() => CurrentRound;
    public int GetZombiesRemaining() => _zombiesAlive;
    private int _GetRemainingZombies() => Mathf.Max(0, _zombiesToSpawn - _zombiesSpawned + _zombiesAlive);

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

        var roundMult = 1.0f + Mathf.Log(1.0f + CurrentRound) * RoundMultiplierBase * 2.0f;
        minion.ApplyRoundScaling(CurrentRound, roundMult * 0.6f);

        _zombiesAlive++;
        _uiManager?.UpdateZombiesDisplay(_GetRemainingZombies(), _zombiesToSpawn);
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
            GameConfig.WorldSize.X / 2.0f + (float)GD.RandRange(-100, 100),
            GameConfig.WorldSize.Y / 2.0f + (float)GD.RandRange(-100, 100)
        );

        var roundMult = 1.0f + Mathf.Log(1.0f + CurrentRound) * RoundMultiplierBase * 1.5f;
        zombieInstance.ApplyRoundScaling(CurrentRound, roundMult);

        GetTree().CurrentScene.AddChild(zombieInstance);
        _zombiesAlive++;
    }

    private void _OnMinionDied(Zombie zombie)
    {
        _zombiesAlive--;
        _roundKills++;
        _totalKills++;
        Score += 5;

        var saveManager = GetNodeOrNull("/root/Main/SaveManager");
        if (saveManager != null) _AddKill(saveManager);

        _uiManager?.UpdateScoreDisplay(Score);
        _uiManager?.AddCombo();
    }

    private static int _GetHighScore(Node sm) => sm.HasMethod("get_high_score") ? (int)sm.Call("get_high_score") : 0;
    private static void _AddGamePlayed(Node sm) { if (sm.HasMethod("add_game_played")) sm.Call("add_game_played"); }
    private static void _AddKill(Node sm) { if (sm.HasMethod("add_kill")) sm.Call("add_kill"); }
    private static void _SaveHighScore(Node sm, int score) { if (sm.HasMethod("save_high_score")) sm.Call("save_high_score", score); }
    private static void _AddRoundsSurvived(Node sm, int rounds) { if (sm.HasMethod("add_rounds_survived")) sm.Call("add_rounds_survived", rounds); }

    public void SetUIManager(UIManager ui)
    {
        _uiManager = ui;
    }
}
