namespace ZombieWars.Enemy;

using System;
using System.Collections.Generic;
using Godot;
using ZombieWars.Core;
using ZombieWars.Vfx;
using static ZombieWars.Core.GameEnums;
using EnemyAbilitySystem = ZombieWars.Enemy.ZombieAbilitySystem;
using Player = ZombieWars.Player.Player;

/// <summary>
/// Zombie base class — streamlined facade.
/// Core logic (movement, attack, status effects, type abilities) kept here;
/// visual effects are delegated to ZombieVfx.
/// </summary>
public partial class Zombie : CharacterBody2D
{
    // ═══════════════════════════════════════════════
    // Signals & Events
    // ═══════════════════════════════════════════════

    public event Action<Zombie> Died = delegate {};

    // ═══════════════════════════════════════════════
    // Exported Properties
    // ═══════════════════════════════════════════════

    [Export] public ZombieType ZombieType { get; set; } = ZombieType.Basic;
    [Export] public int MaxHealth { get; set; } = 30;
    [Export] public int Damage { get; set; } = 10;
    [Export] public float Speed { get; set; } = 50.0f;
    [Export] public float AttackRange { get; set; } = GameConfig.ZombieDefaultAttackRange;

    // ═══════════════════════════════════════════════
    // Type Stats — initialized once
    // ═══════════════════════════════════════════════

    private static readonly Dictionary<ZombieType, (int Health, int Damage, float Speed)> TypeStats = new()
    {
        { ZombieType.Basic,     (25,  8,  45.0f) },
        { ZombieType.Fast,      (15,  6,  90.0f) },
        { ZombieType.Tank,      (100, 18, 22.0f) },
        { ZombieType.Exploder,  (20,  12, 55.0f) },
        { ZombieType.Invisible, (30,  10, 55.0f) },
        { ZombieType.Spitter,   (35,  8,  35.0f) },
        { ZombieType.Healer,    (45,  5,  30.0f) },
        { ZombieType.Summoner,  (40,  5,  28.0f) },
        { ZombieType.Archer,    (20,  10, 30.0f) }, // Ranged zombie - shoots arrows
    };

    // ═══════════════════════════════════════════════
    // State
    // ═══════════════════════════════════════════════

    public int CurrentHealth { get; set; }
    public Node2D Player { get; set; } = null!;

    // Status effects
    private bool _isFrozen;
    private float _freezeTimer;
    private bool _isStunned;
    private float _stunTimer;
    private float _slowMultiplier = 1.0f;
    private float _poisonRemaining;
    private float _poisonTickTimer;
    private float _poisonDamage;

    // Charm
    private bool _isCharmed;
    private float _charmTimer;
    private Node2D? _originalPlayer;

    // Type-specific state
    private float _invisibleTimer;
    private bool _isRevealed;
    private float _spitterCooldown;
    private float _spitterCooldownBase = GameConfig.SpitterCooldown;

    // Archer (ranged)
    private float _archerCooldown;
    private float _archerCooldownBase = 2.0f; // Shoots every 2 seconds
    private const float ArcherRange = 300.0f; // Can shoot from this range

    // Healer
    private float _healerCooldown;
    private float _healerCooldownBase = GameConfig.HealerCooldown;
    private float _healerRange = GameConfig.HealerRange;
    private int _healerAmount = GameConfig.HealerAmount;

    // Summoner
    private float _summonerCooldown;
    private float _summonerCooldownBase = GameConfig.SummonerCooldown;

    // Boss / Elite
    public bool IsBoss { get; private set; }
    public bool IsElite { get; private set; }
    private float _eliteMultiplier = GameConfig.ZombieEliteStatMult;
    private EliteAffix _eliteAffix = EliteAffix.None;
    private float _eliteRegenTimer;

    // ═══════════════════════════════════════════════
    // Node References
    // ═══════════════════════════════════════════════

    public Sprite2D? Sprite => _vfx?.Sprite;

    private CollisionShape2D _collision = null!;
    private Area2D _touchDamageArea = null!;
    private ZombieVfx _vfx = null!;
    private ZombieAbilitySystem? _abilitySystem;
    private float _touchDamageCooldown;
    private const float TouchDamageInterval = 1.0f; // Damage per second when touching

    [Export] private Timer? _attackTimer;

    // ═══════════════════════════════════════════════
    // Initialization
    // ═══════════════════════════════════════════════

    public override void _Ready()
    {
        _ApplyTypeStats();

        if (IsElite)
        {
            MaxHealth = (int)(MaxHealth * _eliteMultiplier);
            Damage = (int)(Damage * _eliteMultiplier);
            Speed = Speed * GameConfig.ZombieEliteSpeedMult;
        }

        CurrentHealth = MaxHealth;
        AddToGroup("zombies");
        ZombieRegistry.Register(this);
        CollisionLayer = 0b010; // layer 2

        // Collision shape
        _collision = new CollisionShape2D();
        var shape = new RectangleShape2D();
        shape.Size = GameConfig.ZombieCollisionSize;
        _collision.Shape = shape;
        AddChild(_collision);

        // VFX component
        _vfx = new ZombieVfx();
        _vfx.Name = "VFX";
        AddChild(_vfx);
        _vfx.Setup(this);
        _vfx.CreateVisuals();

        // Touch damage area for melee contact damage
        _touchDamageArea = new Area2D
        {
            Monitoring = true,
            Monitorable = false,
            CollisionLayer = 0,
            CollisionMask = 0b001 // layer 1 (player)
        };
        var touchShape = new CircleShape2D { Radius = GameConfig.ZombieMeleeTouchRange };
        var touchCol = new CollisionShape2D { Shape = touchShape };
        _touchDamageArea.AddChild(touchCol);
        AddChild(_touchDamageArea);
        _touchDamageArea.BodyEntered += _OnTouchDamageAreaEntered;

        // Attack timer
        if (_attackTimer != null)
        {
            _attackTimer.WaitTime = GameConfig.ZombieAttackCooldown;
            _attackTimer.Timeout += _OnAttackTimerTimeout;
        }

        _InitAbilitySystem();
    }

    private void _InitAbilitySystem()
    {
        _abilitySystem = new ZombieAbilitySystem { Name = "AbilitySystem" };
        AddChild(_abilitySystem);

        AbilityType? ability = ZombieType switch
        {
            ZombieType.Invisible => AbilityType.GhostPhase,
            ZombieType.Fast     => AbilityType.Teleport,
            ZombieType.Summoner => AbilityType.Cloner,
            ZombieType.Exploder => AbilityType.Bomber,
            _                   => null
        };

        if (ability.HasValue)
        {
            _abilitySystem.Setup(this, (EnemyAbilitySystem.AbilityType)(int)ability.Value);
            _abilitySystem.SetPlayerTarget(Player);
        }
    }

    private void _ApplyTypeStats()
    {
        if (TypeStats.TryGetValue(ZombieType, out var stats))
        {
            MaxHealth = stats.Health;
            Damage = stats.Damage;
            Speed = stats.Speed;
        }
    }

    // ═══════════════════════════════════════════════
    // Round Scaling
    // ═══════════════════════════════════════════════

    public void ApplyRoundScaling(int roundNumber, float multiplier)
    {
        MaxHealth = (int)((float)MaxHealth * multiplier);
        CurrentHealth = MaxHealth;
        Damage = (int)((float)Damage * multiplier);
        float speedMult = 1.0f + (multiplier - 1.0f) * GameConfig.RoundSpeedScaleFactor;
        Speed = Speed * speedMult;

        if (ZombieType == ZombieType.Spitter)
        {
            _spitterCooldownBase = Mathf.Max(GameConfig.SpitterMinCooldown,
                GameConfig.SpitterCooldown - roundNumber * GameConfig.SpitterCooldownDecay);
        }

        _vfx?.UpdateStatusLabels();
    }

    // ═══════════════════════════════════════════════
    // Physics Loop
    // ═══════════════════════════════════════════════

    public override void _PhysicsProcess(double delta)
    {
        float d = (float)delta;

        if (_isStunned)
        {
            _stunTimer -= d;
            if (_stunTimer <= 0)
            {
                _isStunned = false;
                _vfx?.ClearEffect();
            }
            return;
        }

        if (_isFrozen)
        {
            _freezeTimer -= d;
            if (_freezeTimer <= 0)
            {
                _isFrozen = false;
                _slowMultiplier = 1.0f;
                _vfx?.ClearEffect();
            }
        }

        if (_isCharmed)
        {
            _charmTimer -= d;
            if (_charmTimer <= 0)
            {
                _isCharmed = false;
                Player = _originalPlayer!;
            }
        }

        if (_poisonRemaining > 0)
        {
            _poisonRemaining -= d;
            _poisonTickTimer -= d;
            if (_poisonTickTimer <= 0 && _poisonRemaining > 0)
            {
                _poisonTickTimer = GameConfig.ZombiePoisonTick;
                TakeDamage((int)_poisonDamage);
            }
        }

        // Type-specific logic
        switch (ZombieType)
        {
            case ZombieType.Invisible:
                _HandleInvisible(d);
                break;
            case ZombieType.Spitter:
                _HandleSpitter(d);
                break;
            case ZombieType.Archer:
                _HandleArcher(d);
                break;
            case ZombieType.Healer:
                _HandleHealer(d);
                break;
            case ZombieType.Summoner:
                _HandleSummoner(d);
                break;
        }

        // Elite affix logic
        if (IsElite && _eliteAffix != EliteAffix.None)
        {
            _HandleEliteAffix(d);
        }

        _abilitySystem?._Process(d);

        if (Player != null)
        {
            _MoveTowardsPlayer();
            _CheckAttackRange();
            _HandleTouchDamage(delta); // Handle melee touch damage
        }

        // Update walk animation
        _vfx?.UpdateWalkAnimation(d);
    }

    // ═══════════════════════════════════════════════
    // Movement
    // ═══════════════════════════════════════════════

    private void _MoveTowardsPlayer()
    {
        if (_isCharmed)
        {
            var nearest = _FindNearestZombie(GameConfig.CharmSearchRange);
            if (nearest != null)
            {
                Velocity = (nearest.GlobalPosition - GlobalPosition).Normalized() * Speed;
            }
            else
            {
                Velocity = Vector2.Zero;
            }
        }
        else
        {
            Velocity = (Player.GlobalPosition - GlobalPosition).Normalized() * Speed * _slowMultiplier;
        }
        MoveAndSlide();
    }

    // ═══════════════════════════════════════════════
    // Type-Specific Behaviors
    // ═══════════════════════════════════════════════

    private void _HandleInvisible(float delta)
    {
        _invisibleTimer -= delta;
        if (_invisibleTimer <= 0)
        {
            _invisibleTimer = (float)GD.RandRange(GameConfig.ZombieInvisibleRevealMin, GameConfig.ZombieInvisibleRevealMax);
            _isRevealed = !_isRevealed;
            _vfx?.ShowInvisible(_isRevealed);
        }
    }

    private void _HandleSpitter(float delta)
    {
        _spitterCooldown -= delta;
        if (_spitterCooldown <= 0)
        {
            _spitterCooldown = _spitterCooldownBase;
            if (Player != null && GlobalPosition.DistanceTo(Player.GlobalPosition) <= GameConfig.SpitterRange)
            {
                _SpitPoison(Player);
            }
        }
    }

    private void _SpitPoison(Node target)
    {
        var proj = new Sprite2D();
        // Use cached poison texture for performance
        proj.Texture = _GetCachedPoisonTexture();
        proj.GlobalPosition = GlobalPosition;
        GetParent().AddChild(proj);

        var tw = CreateTween();
        tw.TweenProperty(proj, "global_position", ((Node2D)target).GlobalPosition, 0.5);
        tw.TweenCallback(Callable.From(() => _CompletePoisonSpit(target, proj)));
    }

    // Static cache for poison texture
    private static ImageTexture? _cachedPoisonTexture;
    private static ImageTexture _GetCachedPoisonTexture()
    {
        if (_cachedPoisonTexture != null)
            return _cachedPoisonTexture;

        var img = Image.CreateEmpty(10, 10, false, Image.Format.Rgba8);
        img.Fill(Colors.Green);
        _cachedPoisonTexture = ImageTexture.CreateFromImage(img);
        return _cachedPoisonTexture;
    }

    // Static cache for arrow texture
    private static ImageTexture? _cachedArrowTexture;
    private static ImageTexture _GetCachedArrowTexture()
    {
        if (_cachedArrowTexture != null)
            return _cachedArrowTexture;

        int bw = 20;
        int bh = 4;
        using var img = Image.CreateEmpty(bw, bh, false, Image.Format.Rgba8);

        for (int x = 0; x < bw; x++)
        {
            for (int y = 0; y < bh; y++)
            {
                // Arrow shaft
                if (x < bw - 5)
                {
                    img.SetPixel(x, y, new Color(0.5f, 0.3f, 0.1f));
                }
                // Arrow head (brighter tip)
                else
                {
                    img.SetPixel(x, y, new Color(0.8f, 0.6f, 0.2f));
                }
            }
        }

        _cachedArrowTexture = ImageTexture.CreateFromImage(img);
        return _cachedArrowTexture;
    }

    private void _HandleArcher(float delta)
    {
        _archerCooldown -= delta;
        if (_archerCooldown <= 0)
        {
            _archerCooldown = _archerCooldownBase;
            if (Player != null && GlobalPosition.DistanceTo(Player.GlobalPosition) <= ArcherRange)
            {
                _ShootArrow(Player);
            }
        }
    }

    private void _ShootArrow(Node target)
    {
        if (target == null) return;

        // Use cached arrow texture for performance
        var arrow = new Sprite2D
        {
            Texture = _GetCachedArrowTexture(),
            GlobalPosition = GlobalPosition,
            ZIndex = 10
        };

        // Calculate direction and rotate arrow
        Vector2 direction = ((Node2D)target).GlobalPosition - GlobalPosition;
        arrow.Rotation = direction.Angle();

        GetParent().AddChild(arrow);

        // Tween arrow to target
        var tw = CreateTween();
        tw.TweenProperty(arrow, "global_position", ((Node2D)target).GlobalPosition, 0.5f)
            .SetTrans(Tween.TransitionType.Linear);
        tw.TweenCallback(Callable.From(() => _CompleteArrowHit(arrow, target)));
    }

    private void _CompleteArrowHit(Node arrow, Node target)
    {
        if (IsInstanceValid(arrow))
            arrow.QueueFree();

        if (IsInstanceValid(target))
        {
            // Direct damage on hit - pass attacker for counterattack
            if (target is ZombieWars.Player.Player player)
            {
                player.TakeDamage(Damage, this);
            }
            else if (target.HasMethod("TakeDamage"))
            {
                target.Call("TakeDamage", Damage);
            }

            // Visual feedback
            VFXManager.SpawnHitEffect(GetParent(), ((Node2D)target).GlobalPosition, new Color(0.8f, 0.6f, 0.3f));
        }
    }

    private void _HandleHealer(float delta)
    {
        _healerCooldown -= delta;
        if (_healerCooldown <= 0)
        {
            _healerCooldown = _healerCooldownBase;
            _HealNearbyZombies();
        }
    }

    private void _HealNearbyZombies()
    {
        bool healedAny = false;

        foreach (var z in ZombieRegistry.All)
        {
            if (!IsInstanceValid(z) || z == this)
                continue;
            if (GlobalPosition.DistanceTo(z.GlobalPosition) <= _healerRange && z.CurrentHealth < z.MaxHealth)
            {
                z.CurrentHealth = Mathf.Min(z.MaxHealth, z.CurrentHealth + _healerAmount);
                z._vfx?.UpdateStatusLabels();
                healedAny = true;
                _vfx?.ShowHealEffect(z.GlobalPosition, _healerAmount);
            }
        }

        if (healedAny && _vfx != null)
        {
            _vfx.ShowHealPulse(_healerRange);
        }
    }

    private void _HandleSummoner(float delta)
    {
        _summonerCooldown -= delta;
        if (_summonerCooldown <= 0)
        {
            _summonerCooldown = _summonerCooldownBase;
            _SummonMinions();
        }
    }

    private void _SummonMinions()
    {
        var pos = GlobalPosition + new Vector2(
            (float)GD.RandRange(-50, 50),
            (float)GD.RandRange(-50, 50)
        );
        GetTree().CallGroup("game_manager", "spawn_minion_zombie", pos);

        if (GD.Randf() < 0.5f)
        {
            var pos2 = GlobalPosition + new Vector2(
                (float)GD.RandRange(-50, 50),
                (float)GD.RandRange(-50, 50)
            );
            GetTree().CallGroup("game_manager", "spawn_minion_zombie", pos2);
        }
    }

    private void _HandleEliteAffix(float delta)
    {
        switch (_eliteAffix)
        {
            case EliteAffix.Regenerating:
                _eliteRegenTimer += delta;
                if (_eliteRegenTimer >= 1.0f)
                {
                    _eliteRegenTimer = 0.0f;
                    int healAmt = Mathf.Max(1, (int)(MaxHealth * GameConfig.EliteRegenPercent));
                    CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + healAmt);
                    _vfx?.UpdateStatusLabels();
                }
                break;
            case EliteAffix.Swift:
                // Speed is handled in _ApplyEliteAffix
                break;
            case EliteAffix.Explosive:
                // Handled in Die()
                break;
        }
    }

    private void _ApplyEliteAffix()
    {
        if (!IsElite) return;

        var affixes = new[]
        {
            EliteAffix.Regenerating,
            EliteAffix.Swift,
            EliteAffix.Explosive
        };
        _eliteAffix = affixes[(int)(GD.Randi() % affixes.Length)];

        switch (_eliteAffix)
        {
            case EliteAffix.Swift:
                Speed *= GameConfig.EliteSwiftMult;
                break;
        }
    }

    // ═══════════════════════════════════════════════
    // Attack
    // ═══════════════════════════════════════════════

    private void _CheckAttackRange()
    {
        if (_isCharmed)
        {
            var nearest = _FindNearestZombie(AttackRange);
            if (nearest != null && (_attackTimer == null || _attackTimer.IsStopped()))
            {
                _attackTimer?.Start();
            }
        }
        else if (Player != null && GlobalPosition.DistanceTo(Player.GlobalPosition) <= AttackRange)
        {
            if (_attackTimer == null || _attackTimer.IsStopped())
            {
                _attackTimer?.Start();
            }
        }
    }

    private void _OnTouchDamageAreaEntered(Node body)
    {
        // This is called when player enters our touch damage area
        // Damage is handled in _Process based on cooldown
    }

    private void _HandleTouchDamage(double delta)
    {
        if (_touchDamageCooldown > 0)
        {
            _touchDamageCooldown -= (float)delta;
            return;
        }

        if (Player == null) return;

        // Check if player is within touch range
        float dist = GlobalPosition.DistanceTo(Player.GlobalPosition);
        if (dist <= GameConfig.ZombieMeleeTouchRange)
        {
            // Deal touch damage
            AttackPlayer();
            _touchDamageCooldown = TouchDamageInterval;
        }
    }

    private void _OnAttackTimerTimeout()
    {
        if (_isCharmed)
        {
            var nearest = _FindNearestZombie(AttackRange);
            if (nearest != null)
                AttackZombie(nearest);
        }
        else if (Player != null && GlobalPosition.DistanceTo(Player.GlobalPosition) <= AttackRange)
        {
            AttackPlayer();
        }
        // Restart timer for next attack cycle
        _attackTimer?.Start();
    }

    public void AttackZombie(Zombie target)
    {
        target?.TakeDamage(Damage);
    }

    public void AttackPlayer()
    {
        if (Player is Player p)
            p.TakeDamage(Damage, this);
    }

    // ═══════════════════════════════════════════════
    // Damage & Death
    // ═══════════════════════════════════════════════

    public void TakeDamage(int amount)
    {
        CurrentHealth -= amount;
        CurrentHealth = Mathf.Max(0, CurrentHealth);

        _vfx?.UpdateStatusLabels();
        _vfx?.FlashHit();
        _vfx?.ShowDamagePopup(amount);

        GameServices.Audio.PlayZombieHit();

        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    public void Die()
    {
        if (ZombieType == ZombieType.Exploder)
        {
            _Explode();
            GameServices.Audio.PlayExplosion();
        }

        if (IsElite && _eliteAffix == EliteAffix.Explosive)
        {
            _EliteExplosiveDeath();
        }

        _vfx?.SpawnDeathEffects();
        _abilitySystem?.OnDeath();

        Died?.Invoke(this);
        ZombieRegistry.Unregister(this);
        QueueFree();
    }

    private void _Explode()
    {
        if (Player != null && GlobalPosition.DistanceTo(Player.GlobalPosition) <= GameConfig.ExploderRange)
        {
            if (Player is Player p)
                p.TakeDamage((int)(Damage * GameConfig.ExploderDamageMult), this);
        }
    }

    private void _EliteExplosiveDeath()
    {
        // Damage player
        if (Player != null && GlobalPosition.DistanceTo(Player.GlobalPosition) <= GameConfig.EliteExplosiveRange)
        {
            if (Player is Player p)
                p.TakeDamage((int)(Damage * GameConfig.EliteExplosiveMult), this);
        }

        // Damage zombies
        foreach (var z in ZombieRegistry.All)
        {
            if (!IsInstanceValid(z) || z == this)
                continue;
            if (GlobalPosition.DistanceTo(z.GlobalPosition) <= GameConfig.EliteExplosiveRange)
            {
                z.TakeDamage((int)(Damage * GameConfig.EliteExplosiveMult));
            }
        }
    }

    // ═══════════════════════════════════════════════
    // Status Effect API
    // ═══════════════════════════════════════════════

    public void ApplyFreeze(float duration)
    {
        _isFrozen = true;
        _freezeTimer = duration;
        _slowMultiplier = GameConfig.FreezeSlowMult;
        _vfx?.ShowFrozen();
    }

    public void ApplyStun(float duration)
    {
        _isStunned = true;
        _stunTimer = duration;
        _vfx?.ShowStunned();
    }

    public void ApplyPoison(float damagePerSecond, float duration)
    {
        _poisonDamage = damagePerSecond;
        _poisonRemaining = duration;
        _poisonTickTimer = GameConfig.ZombiePoisonTick;
        _vfx?.ShowPoisoned();
    }

    public void ApplyCharm(float duration)
    {
        float resist = 0.0f;
        switch (ZombieType)
        {
            case ZombieType.Tank:
                resist = GameConfig.CharmResistTank;
                break;
            case ZombieType.Fast:
                resist = GameConfig.CharmResistFast;
                break;
        }

        float actual = Mathf.Max(GameConfig.CharmMinDuration, duration * (1.0f - resist));
        _isCharmed = true;
        _charmTimer = actual;
        _originalPlayer = Player;

        GameServices.Audio.PlayCharm();
        _vfx?.ShowCharmed();
    }

    public void SetPlayerTarget(Node targetPlayer)
    {
        Player = (Node2D)targetPlayer;
        _abilitySystem?.SetPlayerTarget(targetPlayer);
    }

    public void SetElite(bool enabled)
    {
        IsElite = enabled;
        if (enabled)
        {
            _ApplyEliteAffix();
        }
    }

    public void SetBoss()
    {
        IsBoss = true;
        IsElite = true;
        MaxHealth = (int)(MaxHealth * GameConfig.BossStatMult);
        Damage = (int)(Damage * GameConfig.BossStatMult);
        Speed = Speed * GameConfig.BossSpeedMult;
        CurrentHealth = MaxHealth;
        Scale = GameConfig.BossScale;
        _vfx?.UpdateStatusLabels();
    }

    // ═══════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════

    private Zombie? _FindNearestZombie(float maxRange)
    {
        Zombie? nearest = null;
        float nearestDist = maxRange;

        foreach (var z in ZombieRegistry.All)
        {
            if (!IsInstanceValid(z) || z == this)
                continue;
            float dist = GlobalPosition.DistanceTo(z.GlobalPosition);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = z;
            }
        }
        return nearest;
    }

    private void _CompletePoisonSpit(Node target, Node projectile)
    {
        if (IsInstanceValid(target))
        {
            // Deal direct damage plus poison
            if (target.HasMethod("TakeDamage"))
            {
                target.Call("TakeDamage", Damage);
            }
            target.Call("apply_poison", GameConfig.SpitterPoisonDps, GameConfig.SpitterPoisonDuration);
            target.Call("apply_buff_from_zombie", (int)BuffType.Poison, GameConfig.SpitterPoisonDuration, GameConfig.SpitterPoisonDps);
        }
        if (IsInstanceValid(projectile))
        {
            projectile.QueueFree();
        }
    }
}
