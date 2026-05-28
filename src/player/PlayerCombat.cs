namespace ZombieWars.Player;

using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

using ZombieWars.Core;
using ZombieWars.Enemy;
using ZombieWars.Vfx;
using ZombieWars.Weapons;
using static ZombieWars.Core.GameEnums;
using Zombie = ZombieWars.Enemy.Zombie;

/// <summary>
/// Performance optimization: Static texture cache to avoid regenerating textures every frame.
/// </summary>
public static class TextureCache
{
    private static readonly System.Collections.Generic.Dictionary<string, ImageTexture> _cache = new();

    public static ImageTexture? Get(string key) => _cache.TryGetValue(key, out var tex) ? tex : null;

    public static void Set(string key, ImageTexture tex)
    {
        if (!_cache.ContainsKey(key))
            _cache[key] = tex;
    }

    public static ImageTexture GetOrCreate(string key, Func<ImageTexture> factory)
    {
        if (!_cache.TryGetValue(key, out var tex))
        {
            // Always duplicate the image from the factory so the cached texture
            // owns its buffer — avoids shared-image aliasing bugs
            tex = factory();
            _cache[key] = tex;
        }
        return tex;
    }

    public static void Clear() => _cache.Clear();
}

public partial class PlayerCombat : Node
{
    public Player? Player { get; set; }

    private Area2D _attackArea = null!;
    private Timer _attackTimer = null!;
    public Timer AttackTimer => _attackTimer;
    public bool CanAttack { get; set; } = true;

    // Combo reward system
    private int _currentCombo = 0;
    private float _comboTimer = 0.0f;
    private float _damageBonusFromCombo = 0.0f;
    private int _maxComboThisRound = 0;

    public int CurrentCombo => _currentCombo;
    public float DamageBonusFromCombo => _damageBonusFromCombo;
    public int MaxComboThisRound => _maxComboThisRound;
    public int MaxComboEver { get; private set; }

    public override void _Process(double delta)
    {
        // Combo decay
        if (_comboTimer > 0)
        {
            _comboTimer -= (float)delta;
            if (_comboTimer <= 0)
            {
                _currentCombo = 0;
                _damageBonusFromCombo = 0.0f;
            }
        }
    }

    /// <summary>
    /// Called when player kills a zombie - triggers combo reward.
    /// </summary>
    public void OnKill()
    {
        _currentCombo++;
        _comboTimer = GameConfig.ComboDecayTime;
        if (_currentCombo > _maxComboThisRound)
            _maxComboThisRound = _currentCombo;

        // Combo damage bonus: each combo level adds 5% damage (max 50%)
        _damageBonusFromCombo = Mathf.Min(_currentCombo * 0.05f, 0.5f);

        // Track all-time max
        if (_currentCombo > MaxComboEver)
            MaxComboEver = _currentCombo;

        // Notify achievement system
        AchievementSystem.Instance?.OnComboAchieved(_currentCombo);
    }

    public void ResetComboStats()
    {
        _maxComboThisRound = 0;
    }

    public void Setup(Player p)
    {
        Player = p;
        _SetupAttackArea();
        _SetupAttackTimer();
    }

    private void _SetupAttackArea()
    {
        _attackArea = new Area2D
        {
            CollisionLayer = 0,
            CollisionMask = 0b010 // layer 2 — detect zombies
        };
        Player!.AddChild(_attackArea);
        var col = new CollisionShape2D();
        var shape = new CircleShape2D();
        shape.Radius = Core.GameConfig.BaseAttackRange;
        col.Shape = shape;
        _attackArea.AddChild(col);
    }

    private void _SetupAttackTimer()
    {
        _attackTimer = new Timer { WaitTime = Player!.AttackCooldown, OneShot = true };
        _attackTimer.Timeout += () => CanAttack = true;
        Player.AddChild(_attackTimer);
    }

    public void HandleManualAttack()
    {
        // Safety: recover CanAttack if timer finished but callback didn't fire
        if (!CanAttack && _attackTimer != null && _attackTimer.IsStopped())
            CanAttack = true;

        if (Player!.CurrentAttackType == AttackType.Magic)
        {
            // Magic: require manual trigger, aim at nearest zombie or mouse direction
            if (!CanAttack)
                return;
            if (Input.IsActionPressed("attack"))
            {
                _TryMagicAttack();
                return;
            }
            if (Input.IsMouseButtonPressed(MouseButton.Left))
            {
                Vector2 mousePos = Player.GetViewport().GetMousePosition();
                if (mousePos.Y > Core.GameConfig.TopUiMargin)
                    _TryMagicAttack();
            }
            return;
        }

        if (Input.IsActionPressed("attack") && CanAttack)
        {
            PerformAttack();
            return;
        }

        if (Input.IsMouseButtonPressed(MouseButton.Left))
        {
            Vector2 mousePos = Player.GetViewport().GetMousePosition();
            if (mousePos.Y > Core.GameConfig.TopUiMargin && CanAttack)
                PerformAttack();
        }
    }

    private void _TryMagicAttack()
    {
        Zombie? target = _FindTargetZombie(Core.GameConfig.MagicAttackRange);
        if (target != null)
        {
            GameServices.Audio.PlayMagicAttack();
            _PerformMagicAttack(target);
        }
        else
        {
            // No target: fire toward mouse direction
            Vector2 mousePos = Player!.GetViewport().GetMousePosition();
            if (mousePos.Y > Core.GameConfig.TopUiMargin)
            {
                GameServices.Audio.PlayMagicAttack();
                _FireMagicMissileToward(mousePos);
            }
        }
    }

    private void _FireMagicMissileToward(Vector2 worldPos)
    {
        Vector2 dir = (worldPos - Player!.GlobalPosition).Normalized();
        var missile = _CreateMagicMissile();
        missile.GlobalPosition = Player.GlobalPosition;
        missile.Target = null;
        missile.Damage = Player.CurrentDamage;
        missile.CharmDuration = Player.CharmDuration;
        missile.PushForce = Player.PushForce;
        missile.StunDuration = Player.StunDuration;
        missile.DotDamage = Player.DotDamage;
        missile.ChainCount = Player.ChainCount;
        missile.Shooter = Player;
        missile.Rotation = dir.Angle();
        Player.GetParent().AddChild(missile);
        _BeginAttack();
    }

    public void PerformAttack()
    {
        switch (Player!.CurrentAttackType)
        {
            case AttackType.Melee:
                GameServices.Audio.PlayMeleeAttack();
                _PerformMeleeAttack();
                break;
            case AttackType.Ranged:
                GameServices.Audio.PlayRangedAttack();
                _PerformRangedAttack();
                break;
            case AttackType.Area:
                GameServices.Audio.PlayRangedAttack();
                _PerformAreaAttack();
                break;
            case AttackType.Magic:
                GameServices.Audio.PlayMagicAttack();
                _TryMagicAttack();
                break;
            case AttackType.Mine:
                GameServices.Audio.PlayMeleeAttack();
                _PerformMineAttack();
                break;
        }
    }

    private void _PerformMeleeAttack()
    {
        if (!_CanPayHealthCost())
            return;

        // Use SpatialUtils for cleaner, consistent distance-based detection
        float range = Core.GameConfig.BaseAttackRange * Math.Max(Core.GameConfig.MinRangeMult, Player!.WeaponRangeModifier);
        var candidates = SpatialUtils.GetZombiesInRange(Player.GlobalPosition, range);
        var targets = new List<Zombie>(Player.MeleeTargetCount);
        for (int i = 0; i < Math.Min(candidates.Count, Player.MeleeTargetCount); i++)
            targets.Add(candidates[i]);

        // Always trigger cooldown — even a whiffed swing should cost the cooldown.
        // This prevents the bug where attacking with no targets in range leaves
        // CanAttack=false permanently (since _BeginAttack was never called).
        _BeginAttack();
        Player!.Vfx?.ShowAttackEffect(_GetAttackEffectTargetPosition(targets), Player.CurrentAttackType);

        if (targets.Count == 0)
            return;

        foreach (var zombie in targets)
            AttackZombie(zombie);
    }

    private void _PerformRangedAttack()
    {
        if (!_CanPayHealthCost())
            return;

        Vector2 mousePos = Player!.GetViewport().GetMousePosition();
        Zombie? target = _FindTargetZombie(Core.GameConfig.RangedAttackRange);

        if (target == null)
        {
            // No target: fire toward mouse direction
            if (mousePos.Y > Core.GameConfig.TopUiMargin)
            {
                _BeginAttack(notifyUi: true);
                _FireProjectile(mousePos);
                Player.Vfx?.CreateMuzzleFlash((mousePos - Player.GlobalPosition).Normalized());
                Player.Vfx?.ShowAttackEffect(mousePos, Player.CurrentAttackType);
            }
            return;
        }

        _BeginAttack(notifyUi: true);
        if (Player!.MultiShotCount > 1)
        {
            var targets = _GetNearestZombies(Core.GameConfig.RangedAttackRange, Player.MultiShotCount);
            foreach (var z in targets)
                _FireProjectile(z.GlobalPosition);

            int extra = Player.MultiShotCount - targets.Count;
            for (int i = 0; i < extra; i++)
            {
                _FireProjectile(target.GlobalPosition + new Vector2(
                    (float)GD.RandRange(-25, 25), (float)GD.RandRange(-25, 25)));
            }
        }
        else
        {
            _FireProjectile(target.GlobalPosition);
        }
        Player.Vfx?.ShowAttackEffect(target.GlobalPosition, Player.CurrentAttackType);
    }

    private void _PerformAreaAttack()
    {
        if (!_CanPayHealthCost())
            return;

        _BeginAttack(notifyUi: true);
        _CreateAreaExplosion();
        Player!.Vfx?.ShowAttackEffect(Player.GlobalPosition, Player.CurrentAttackType);
    }

    private void _PerformMagicAttack(Zombie target)
    {
        if (!_CanPayHealthCost() || target == null)
            return;

        _BeginAttack();
        _CastMagicMissile(target);
        Player!.Vfx?.ShowAttackEffect(target.GlobalPosition, Player.CurrentAttackType);
    }

    public void AttackZombie(Zombie zombie)
    {
        float damage = Player!.CurrentDamage;
        bool isCritical = GD.Randf() < (Player.CriticalChance / 100.0f);
        if (isCritical)
            damage *= Player.CritMultiplier;

        Vector2 hitPos = zombie.GlobalPosition;
        zombie.TakeDamage((int)damage);
        GameServices.UI.ShowDamageNumber(hitPos, (int)damage, isCritical);
        RegisterDamageDealt(zombie, damage);

        float shake = isCritical ? 8.0f : 3.0f;
        GameServices.UI.ShakeScreen(shake);
        VFXManager.FlashSprite(Player, Player.Sprite!, new Color(0.9f, 0.3f, 0.2f));

        if (zombie.CurrentHealth <= 0)
            return;

        GameServices.Audio.PlayZombieHit();

        if (Player.KnockbackForce > 0)
        {
            Vector2 pushDir = (zombie.GlobalPosition - Player.GlobalPosition).Normalized();
            zombie.Velocity += pushDir * Player.KnockbackForce;
        }

        if (Player.FreezeDuration > 0 && GD.Randf() < (Player.FreezeDuration / 100.0f))
            zombie.ApplyFreeze(Core.GameConfig.FreezeDurationDefault);

        if (Player.PoisonDamage > 0 && GD.Randf() < (Player.PoisonDamage / 100.0f))
        {
            float dps = 3.0f + Player.PoisonDamage * 0.2f;
            zombie.ApplyPoison(dps, Core.GameConfig.PoisonDurationDefault);
        }

        if (Player.SplashRadius > 0)
        {
            var targets = SpatialUtils.GetZombiesInRange(hitPos, Player.SplashRadius);
            foreach (var z in targets)
            {
                if (z == zombie)
                    continue;
                int splashDmg = (int)(damage * Core.GameConfig.SplashDamageMult);
                z.TakeDamage(splashDmg);
                GameServices.UI.ShowDamageNumber(z.GlobalPosition, splashDmg, false);
                RegisterDamageDealt(z, splashDmg);
            }
        }
    }

    private void _OnEnemyKilled(Vector2 pos, float damage = 0)
    {
        var p = Player!;
        GameServices.Audio.PlayZombieDie();
        ZombieWars.Audio.SoundManager.Instance?.NotifyKill();

        // Trigger combo reward
        OnKill();

        if (p.KillExplosionDamage > 0)
        {
            var targets = SpatialUtils.GetZombiesInRange(pos, Core.GameConfig.KillExplosionRadius);
            foreach (var z in targets)
            {
                z.TakeDamage(p.KillExplosionDamage);
                GameServices.UI.ShowDamageNumber(z.GlobalPosition, p.KillExplosionDamage, false);
                RegisterDamageDealt(z, p.KillExplosionDamage);
            }
        }

        if (p.KillShieldAmount > 0)
            p.ShieldAmount = Math.Min(p.ShieldAmount + p.KillShieldAmount, Core.GameConfig.MaxShield);
    }

    public Zombie? _FindTargetZombie(float maxRange)
    {
        if (ZombieRegistry.Count == 0)
            return null;

        var p = Player!;
        return p.TargetPriority switch
        {
            0 => SpatialUtils.FindNearestZombie(p.GlobalPosition, maxRange),
            1 => SpatialUtils.FindFarthestZombie(p.GlobalPosition, maxRange),
            2 => SpatialUtils.FindDensestZombie(p.GlobalPosition),
            _ => SpatialUtils.FindNearestZombie(p.GlobalPosition, maxRange)
        };
    }

    private List<Zombie> _GetNearestZombies(float maxRange, int count)
    {
        var inRange = SpatialUtils.GetZombiesInRange(Player!.GlobalPosition, maxRange);
        inRange.Sort((a, b) =>
            Player.GlobalPosition.DistanceSquaredTo(a.GlobalPosition)
                .CompareTo(Player.GlobalPosition.DistanceSquaredTo(b.GlobalPosition)));
        var result = new List<Zombie>(count);
        for (int i = 0; i < Math.Min(inRange.Count, count); i++)
            result.Add(inRange[i]);
        return result;
    }

    private void _FireProjectile(Vector2 targetPos)
    {
        Vector2 baseDir = (targetPos - Player!.GlobalPosition).Normalized();
        Vector2 direction = baseDir.Rotated(Mathf.DegToRad((float)GD.RandRange(-5.0, 5.0)));

        var bullet = _CreateBullet();
        bullet.GlobalPosition = Player.GlobalPosition;
        bullet.Velocity = direction * Core.GameConfig.BulletVelocity;
        bullet.Damage = Player.CurrentDamage;
        bullet.TrajectoryDamageRatio = Player.TrajectoryDamageRatio;
        bullet.PiercingCount = Player.PiercingCount;
        bullet.IsReturning = Player.ReturningProjectiles;
        bullet.BouncesLeft = Player.BounceCount;
        bullet.Shooter = Player;
        Player.GetParent().AddChild(bullet);
        Player.Vfx?.CreateMuzzleFlash(direction);
    }

    private Projectile _CreateBullet()
    {
        var bullet = new Projectile();

        // Use cached bullet texture for performance
        var tex = TextureCache.GetOrCreate("bullet", () =>
        {
            int bw = 14;
            int bh = 6;
            using var img = Image.CreateEmpty(bw, bh, false, Image.Format.Rgba8);

            for (int by = 0; by < bh; by++)
            {
                for (int bx = 0; bx < bw; bx++)
                {
                    if (bx > bw - 5)
                    {
                        float centerDist = Mathf.Abs(by - bh / 2.0f);
                        float maxDist = (bw - bx) * (bh / 2.0f) / 5.0f;
                        if (centerDist <= maxDist)
                            img.SetPixel(bx, by, new Color(0.9f, 0.85f, 0.7f));
                    }
                    else if (by >= (bh >> 1) - 1 && by <= (bh >> 1))
                    {
                        img.SetPixel(bx, by, new Color(0.6f, 0.45f, 0.3f));
                    }
                    if (bx < 3)
                    {
                        if (by == 0 || by == bh - 1)
                            img.SetPixel(bx, by, new Color(0.7f, 0.3f, 0.2f, 0.8f));
                    }
                }
            }
            return ImageTexture.CreateFromImage(img);
        });

        var sprite = new Sprite2D { Texture = tex };
        bullet.AddChild(sprite);

        var trail = new GpuParticles2D
        {
            Emitting = true,
            Amount = 8,
            Lifetime = 0.3f
        };
        var tmat = new ParticleProcessMaterial
        {
            Direction = new Vector3(-1, 0, 0),
            InitialVelocityMin = 10.0f,
            InitialVelocityMax = 30.0f,
            Gravity = Vector3.Zero,
            ScaleMin = 0.5f,
            ScaleMax = 1.5f,
            Color = new Color(0.8f, 0.6f, 0.3f, 0.6f)
        };
        trail.ProcessMaterial = tmat;
        bullet.AddChild(trail);
        return bullet;
    }

    private void _CastMagicMissile(Zombie target)
    {
        var missile = _CreateMagicMissile();
        missile.GlobalPosition = Player!.GlobalPosition;
        missile.Target = target;
        missile.Damage = Player.CurrentDamage;
        missile.CharmDuration = Player.CharmDuration;
        missile.PushForce = Player.PushForce;
        missile.StunDuration = Player.StunDuration;
        missile.DotDamage = Player.DotDamage;
        missile.ChainCount = Player.ChainCount;
        missile.Shooter = Player;
        Player.GetParent().AddChild(missile);
    }

    private MagicMissile _CreateMagicMissile()
    {
        var missile = new MagicMissile
        {
            CollisionLayer = 0,
            CollisionMask = 0b010 // Detect zombies (layer 2)
        };

        // Use cached magic missile texture for performance
        var tex = TextureCache.GetOrCreate("magic_missile", () =>
        {
            int size = 12;
            using var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float dist = new Vector2(x - size / 2.0f, y - size / 2.0f).Length();
                    if (dist < 2)
                        img.SetPixel(x, y, new Color(1.0f, 1.0f, 1.0f, 0.95f));
                    else if (dist < 4)
                    {
                        float t = (dist - 2) / 2.0f;
                        img.SetPixel(x, y, new Color(0.8f - t * 0.3f, 0.5f - t * 0.2f, 1.0f, 0.9f - t * 0.3f));
                    }
                    else if (dist < 6)
                    {
                        float t = (dist - 4) / 2.0f;
                        img.SetPixel(x, y, new Color(0.4f, 0.2f, 0.8f, 0.4f * (1.0f - t)));
                    }
                }
            }
            return ImageTexture.CreateFromImage(img);
        });

        var sprite = new Sprite2D { Texture = tex };
        missile.AddChild(sprite);

        var col = new CollisionShape2D();
        var colShape = new CircleShape2D { Radius = 8.0f };
        col.Shape = colShape;
        missile.AddChild(col);

        var trail = new GpuParticles2D
        {
            Emitting = true,
            Amount = 12,
            Lifetime = 0.4f
        };
        var tmat = new ParticleProcessMaterial
        {
            Direction = Vector3.Zero,
            InitialVelocityMin = 5.0f,
            InitialVelocityMax = 20.0f,
            Gravity = Vector3.Zero,
            ScaleMin = 1.0f,
            ScaleMax = 2.5f,
            Color = new Color(0.6f, 0.3f, 1.0f, 0.5f)
        };
        trail.ProcessMaterial = tmat;
        missile.AddChild(trail);
        missile.MarkVisualCreated();
        return missile;
    }

    private Vector2 _GetAttackEffectTargetPosition(List<Zombie> targets)
    {
        if (targets.Count > 0)
            return targets[0].GlobalPosition;

        Vector2 fallback = Player!.GlobalPosition + new Vector2(Player.Sprite?.FlipH == true ? -40.0f : 40.0f, 0.0f);
        Zombie? rangedTarget = _FindTargetZombie(Core.GameConfig.RangedAttackRange);
        return rangedTarget?.GlobalPosition ?? fallback;
    }

    private void _CreateAreaExplosion()
    {
        Vector2 pos = Player!.GlobalPosition;
        float range = Core.GameConfig.BaseAttackRange * Math.Max(Core.GameConfig.MinRangeMult, Player.WeaponRangeModifier);
        var zombies = Core.SpatialUtils.GetZombiesInRange(pos, range);
        foreach (var zombie in zombies)
        {
            float damage = Player.CurrentDamage * Core.GameConfig.AreaDamageMult;
            zombie.TakeDamage((int)damage);
            RegisterDamageDealt(zombie, damage);
        }

        VFXManager.SpawnParticleBurst(Player.GetParent(), pos, new Color(1.0f, 0.5f, 0.0f),
            20, 0.5f, 60.0f, 150.0f, new Vector3(0, 80, 0), 1.5f, 3.0f);
        VFXManager.SpawnImpactRing(Player.GetParent(), pos, 40.0f, new Color(1.0f, 0.5f, 0.0f), 3.0f);
    }

    private void _PerformMineAttack()
    {
        if (!_CanPayHealthCost())
            return;

        _BeginAttack();

        Vector2 placePos = Player!.GlobalPosition + new Vector2(
            (float)GD.RandRange(-20, 20),
            (float)GD.RandRange(10, 30));

        var mine = new Mine
        {
            Damage = Player.CurrentDamage * 2.0f,
            BlastRadius = 90.0f,
            ArmTime = 0.6f,
            Lifetime = 15.0f,
            StunOnHit = Player.StunDuration > 0,
            ClusterCount = 0,
            Shooter = Player
        };
        mine.GlobalPosition = placePos;
        Player.GetParent().AddChild(mine);

        Player.Vfx?.ShowAttackEffect(placePos, AttackType.Mine);
    }

    public void ApplyWeaponEffect(WeaponCard weapon)
    {
        if (weapon.Category == WeaponCategory.Weapon)
        {
            var kept = new Array<WeaponCard>();
            foreach (var e in Player!.ActiveEffects)
            {
                if (e.Category != WeaponCategory.Weapon)
                    kept.Add(e);
            }
            Player.ActiveEffects = kept;
        }
        Player!.ActiveEffects.Add(weapon);
        RecalculateStats();
        _ShowApplyFeedback("+" + weapon.CardName, weapon.CardName, _GetRarityColor(weapon.Rarity));
    }

    public void ApplyUpgradeCard(UpgradeCard upgrade)
    {
        WeaponCard? target = upgrade.TargetRef;
        if (target == null)
            return;

        target.DamageModifier += upgrade.DamageDelta;
        target.AttackSpeedModifier += upgrade.AttackSpeedDelta;
        target.HealthModifier += upgrade.HealthDelta;
        target.SpeedModifier += upgrade.SpeedDelta;
        target.CriticalBonus += upgrade.CritDelta;
        target.LifeStealBonus += upgrade.LifeStealDelta;
        target.FreezeBonus += upgrade.FreezeDelta;
        target.PoisonBonus += upgrade.PoisonDelta;

        foreach (System.Collections.Generic.KeyValuePair<Variant, Variant> kvp in upgrade.EffectUpgrades)
        {
            int key = (int)kvp.Key;
            Variant vk = kvp.Key;
            int delta = kvp.Value.AsInt32();
            Godot.Collections.Dictionary levelsDict;

            if (target.MeleeEffectLevels.ContainsKey(vk))
                levelsDict = target.MeleeEffectLevels;
            else if (target.RangedEffectLevels.ContainsKey(vk))
                levelsDict = target.RangedEffectLevels;
            else if (target.MagicEffectLevels.ContainsKey(vk))
                levelsDict = target.MagicEffectLevels;
            else
            {
                switch (target.WeaponType)
                {
                    case WeaponType.Melee:
                        levelsDict = target.MeleeEffectLevels;
                        break;
                    case WeaponType.Ranged:
                        levelsDict = target.RangedEffectLevels;
                        break;
                    default:
                        levelsDict = target.MagicEffectLevels;
                        break;
                }
            }
            int existing = 0;
            if (levelsDict.TryGetValue(vk, out var prev))
                existing = prev.AsInt32();
            levelsDict[vk] = existing + delta;
        }

        switch (upgrade.TradeoffKind)
        {
            case UpgradeCard.TradeoffType.HealthCost:
                target.HealthCost += (int)upgrade.TradeoffValue;
                break;
            case UpgradeCard.TradeoffType.Speed:
                target.SpeedModifier += upgrade.TradeoffValue;
                break;
            case UpgradeCard.TradeoffType.HealthMax:
                target.HealthModifier += upgrade.TradeoffValue;
                break;
        }

        target.UpgradeCount += 1;
        if (upgrade.UpgradeDirection_ != UpgradeCard.UpgradeDirection.None)
        {
            Variant dk = Variant.CreateFrom((int)upgrade.UpgradeDirection_);
            int prev2 = 0;
            if (target.UpgradeHistory.TryGetValue(dk, out var prevVal))
                prev2 = prevVal.AsInt32();
            target.UpgradeHistory[dk] = prev2 + 1;
        }

        target.EvolveName();
        RecalculateStats();
        GameServices.Audio.PlayUpgrade();
        _ShowApplyFeedback("强化!" + target.CardName, target.CardName, new Color(0.3f, 1.0f, 0.5f));
    }

    public void RecalculateStats()
    {
        Player!.MaxHealth = Player.BaseMaxHealth;
        Player.CurrentSpeed = Player.BaseSpeed;
        Player.DamageBonus = 0.0f;
        Player.MultiShotCount = 1;
        Player.PiercingCount = 0;
        Player.LifeStealPercent = 0.0f;
        Player.CriticalChance = 0.0f;
        Player.FreezeDuration = 0.0f;
        Player.PoisonDamage = 0.0f;
        Player.BounceCount = 0;
        Player.HealthCostPerAttack = 0;
        Player.AttackSpeedMultiplier = 1.0f;
        Player.WeaponAttackSpeedModifier = 1.0f;
        Player.WeaponCooldown = 0.0f;
        Player.WeaponRangeModifier = 1.0f;
        Player.SplashRadius = 0.0f;
        Player.MeleeTargetCount = 1;
        Player.KillExplosionDamage = 0;
        Player.KillShieldAmount = 0;
        Player.KnockbackForce = 0.0f;
        Player.TrajectoryDamageRatio = 0.0f;
        Player.ReturningProjectiles = false;
        Player.ChainCount = 0;
        Player.CharmDuration = 0.0f;
        Player.PushForce = 0.0f;
        Player.StunDuration = 0.0f;
        Player.DotDamage = 0.0f;

        WeaponCard? currentWeapon = null;
        foreach (var e in Player.ActiveEffects)
        {
            if (e.Category == WeaponCategory.Weapon)
            {
                currentWeapon = e;
                break;
            }
        }

        if (currentWeapon != null)
        {
            switch (currentWeapon.WeaponType)
            {
                case WeaponType.Melee:
                    Player.CurrentAttackType = AttackType.Melee;
                    break;
                case WeaponType.Ranged:
                    Player.CurrentAttackType = AttackType.Ranged;
                    break;
                case WeaponType.Magic:
                    Player.CurrentAttackType = AttackType.Magic;
                    break;
                case WeaponType.Mine:
                    Player.CurrentAttackType = AttackType.Mine;
                    break;
            }
            Player.TargetPriority = (int)currentWeapon.TargetPriority;
        }
        else
        {
            Player.CurrentAttackType = AttackType.Melee;
        }
        Player.Vfx?.SwitchWeaponSprites(Player.CurrentAttackType);

        foreach (var effect in Player.ActiveEffects)
            _ApplyEffectValues(effect);

        Player.CurrentDamage = Player.BaseDamage * Math.Max(Core.GameConfig.MinDamageMult, 1.0f + Player.DamageBonus);
        float totalSpeed = Math.Max(0.1f, Player.WeaponAttackSpeedModifier * Player.AttackSpeedMultiplier);
        Player.AttackCooldown = Math.Max(Core.GameConfig.MinAttackCooldown,
            Core.GameConfig.BaseAttackCooldown / totalSpeed);
        Player.AttackCooldown = Math.Max(Player.AttackCooldown, Player.WeaponCooldown);
        _attackTimer.WaitTime = Player.AttackCooldown;
        _attackTimer.Stop();
        CanAttack = true;
        _UpdateAttackRange(Player.WeaponRangeModifier);
        Player.CurrentHealth = Math.Clamp(Player.CurrentHealth, 0, Player.MaxHealth);
    }

    private void _ApplyEffectValues(WeaponCard weapon)
    {
        if (weapon.DamageModifier != 1.0f)
            Player!.DamageBonus += (weapon.DamageModifier - 1.0f);

        Player!.CurrentSpeed += weapon.SpeedModifier;

        if (weapon.HealthModifier != 0)
        {
            Player.MaxHealth += (int)weapon.HealthModifier;
            Player.CurrentHealth += (int)weapon.HealthModifier;
            Player.CurrentHealth = Math.Clamp(Player.CurrentHealth, 0, Player.MaxHealth);
            Player.FireHealthChanged(Player.CurrentHealth);
        }

        if (weapon.CriticalBonus > 0)
            Player.CriticalChance += weapon.CriticalBonus;
        if (weapon.LifeStealBonus > 0)
            Player.LifeStealPercent += weapon.LifeStealBonus;
        if (weapon.FreezeBonus > 0)
            Player.FreezeDuration = Math.Max(Player.FreezeDuration, weapon.FreezeBonus);
        if (weapon.PoisonBonus > 0)
            Player.PoisonDamage = Math.Max(Player.PoisonDamage, weapon.PoisonBonus);
        if (weapon.GrantsAura)
        {
            if (Player.Aura!.Active)
                Player.UpgradeAura();
            else
                Player.ActivateAura();
        }

        if (weapon.Category == WeaponCategory.Weapon)
        {
            Player.WeaponAttackSpeedModifier = weapon.AttackSpeedModifier;
            Player.WeaponCooldown = weapon.Cooldown;
            Player.WeaponRangeModifier = weapon.RangeModifier;
            if (weapon.HealthCost > 0)
                Player.HealthCostPerAttack = weapon.HealthCost;
        }
        else if (weapon.AttackSpeedModifier != 1.0f)
        {
            Player.AttackSpeedMultiplier *= weapon.AttackSpeedModifier;
        }

        if (Player.CurrentAttackType == AttackType.Melee && weapon.WeaponType == WeaponType.Melee)
        {
            foreach (var effect in weapon.MeleeEffects)
            {
                int lv = 1;
                if (weapon.MeleeEffectLevels.TryGetValue(effect, out var prev2))
                    lv = prev2.AsInt32();
                _ApplyMeleeEffect((int)effect, lv);
            }
        }

        if (Player.CurrentAttackType == AttackType.Ranged && weapon.WeaponType == WeaponType.Ranged)
        {
            foreach (var effect in weapon.RangedEffects)
            {
                int lv = 1;
                if (weapon.RangedEffectLevels.TryGetValue(effect, out var prev2))
                    lv = prev2.AsInt32();
                _ApplyRangedEffect((int)effect, lv);
            }
        }

        if (Player.CurrentAttackType == AttackType.Magic && weapon.WeaponType == WeaponType.Magic)
        {
            foreach (var effect in weapon.MagicEffects)
            {
                int lv = 1;
                if (weapon.MagicEffectLevels.TryGetValue(effect, out var prev2))
                    lv = prev2.AsInt32();
                _ApplyMagicEffect((int)effect, lv);
            }
        }
    }

    private void _ApplyMeleeEffect(int effect, int level)
    {
        var p = Player!;
        switch (effect)
        {
            case (int)MeleeEffect.Splash:
                p.SplashRadius = 30.0f + level * 10.0f;
                break;
            case (int)MeleeEffect.MultiTarget:
                p.MeleeTargetCount = Math.Max(p.MeleeTargetCount, 1 + level);
                break;
            case (int)MeleeEffect.KillExplosion:
                p.KillExplosionDamage += level * 50;
                break;
            case (int)MeleeEffect.KillShield:
                p.KillShieldAmount += level * 20;
                break;
            case (int)MeleeEffect.Knockback:
                p.KnockbackForce += level * 50;
                break;
        }
    }

    private void _ApplyRangedEffect(int effect, int level)
    {
        var p = Player!;
        switch (effect)
        {
            case (int)RangedEffect.MultiShot:
                p.MultiShotCount = Math.Max(p.MultiShotCount, 1 + level);
                break;
            case (int)RangedEffect.TrajectoryDamage:
                p.TrajectoryDamageRatio = Math.Max(p.TrajectoryDamageRatio, level * 0.3f);
                break;
            case (int)RangedEffect.Returning:
                p.ReturningProjectiles = true;
                p.BounceCount = Math.Max(p.BounceCount, level);
                break;
            case (int)RangedEffect.Piercing:
                p.PiercingCount += level;
                break;
        }
    }

    private void _ApplyMagicEffect(int effect, int level)
    {
        var p = Player!;
        switch (effect)
        {
            case (int)MagicEffect.Charm:
                p.CharmDuration = Math.Max(p.CharmDuration, level * 5.0f);
                break;
            case (int)MagicEffect.Push:
                p.PushForce += level * 80;
                break;
            case (int)MagicEffect.Stun:
                p.StunDuration = Math.Max(p.StunDuration, level * 1.5f);
                break;
            case (int)MagicEffect.Chain:
                p.ChainCount = Math.Max(p.ChainCount, level);
                break;
            case (int)MagicEffect.Dot:
                p.DotDamage += level * 5;
                break;
        }
    }

    private void _UpdateAttackRange(float rangeMod)
    {
        if (_attackArea == null)
            return;

        if (_attackArea.GetChild(0) is CollisionShape2D col && col.Shape is CircleShape2D circleShape)
            circleShape.Radius = Core.GameConfig.BaseAttackRange * Math.Max(Core.GameConfig.MinRangeMult, rangeMod);
    }

    private bool _CanPayHealthCost()
    {
        if (Player!.HealthCostPerAttack <= 0 || Player.CurrentHealth >= Player.HealthCostPerAttack)
            return true;

        VFXManager.SpawnFloatingLabel(Player.GetParent(), Player.GlobalPosition, "生命不足", Colors.OrangeRed, 16);
        GameServices.UI.UpdatePlayerStatus(Player);
        return false;
    }

    private void _PayHealthCost()
    {
        if (Player!.HealthCostPerAttack > 0)
            Player.TakeDamage(Player.HealthCostPerAttack);
    }

    public void ApplyLifesteal(float damage)
    {
        if (Player!.LifeStealPercent <= 0 || damage <= 0)
            return;

        int healAmt = (int)(damage * Player.LifeStealPercent / 100.0f);
        if (healAmt > 0)
            Player.Heal(healAmt);
    }

    private void _BeginAttack(bool notifyUi = false)
    {
        CanAttack = false;
        _attackTimer.Start();
        _PayHealthCost();
        if (notifyUi)
            _NotifyUiStatus();
    }

    public void RegisterDamageDealt(Zombie zombie, float damage)
    {
        ApplyLifesteal(damage);
        if (zombie != null && zombie.CurrentHealth <= 0 && !zombie.HasMeta("player_kill_processed"))
        {
            zombie.SetMeta("player_kill_processed", true);
            _OnEnemyKilled(zombie.GlobalPosition, damage);
        }
    }

    private void _ShowApplyFeedback(string labelText, string effectName, Color color)
    {
        Player!.Vfx?.ShowUpgradeEffect();
        Player!.RaiseEffectApplied(effectName);
        VFXManager.SpawnFloatingLabel(Player.GetParent(), Player.GlobalPosition, labelText, color);
        _NotifyUiStatus();
    }

    private Color _GetRarityColor(int rarity)
    {
        return rarity switch
        {
            2 => new Color(0.4f, 0.7f, 1.0f),
            3 => new Color(0.8f, 0.4f, 1.0f),
            4 => new Color(1.0f, 0.8f, 0.2f),
            _ => Colors.Gold
        };
    }

    private void _NotifyUiStatus()
    {
        GameServices.UI.UpdatePlayerStatus(Player!);
    }
}
