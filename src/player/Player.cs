namespace ZombieWars.Player;

using Godot;
using Godot.Collections;
using System;
using ZombieWars.Core;
using ZombieWars.Enemy;
using ZombieWars.Vfx;
using ZombieWars.Weapons;
using static ZombieWars.Core.GameEnums;
using ZombieWars.Audio;

/// <summary>
/// 玩家角色 — 精简 Facade，核心逻辑委托给组件
/// combat: PlayerCombat — 攻击、目标选择、投射物、武器效果
/// aura: PlayerAura — 冰霜护盾光环系统
/// vfx: PlayerVfx — 视觉效果、精灵、动画
/// </summary>
public partial class Player : CharacterBody2D
{
    public event Action<int>? HealthChanged;

    public void FireHealthChanged(int currentHealth) => HealthChanged?.Invoke(currentHealth);
    public event Action? Died;
    public event Action<string>? EffectApplied;
    public void RaiseEffectApplied(string effectName) => EffectApplied?.Invoke(effectName);

    [Export]
    public int MaxHealth { get; set; } = Core.GameConfig.PlayerMaxHealth;

    [Export]
    public float BaseDamage { get; set; } = Core.GameConfig.PlayerBaseDamage;

    [Export]
    public float BaseSpeed { get; set; } = Core.GameConfig.PlayerBaseSpeed;

    public int BaseMaxHealth { get; set; }
    public int CurrentHealth { get; set; }
    public float CurrentDamage { get; set; }
    public float CurrentSpeed { get; set; }

    public Array<WeaponCard> ActiveEffects { get; set; } = new();

    public bool CanDash { get; set; } = true;
    public bool IsDashing { get; set; } = false;
    public double DashCooldownRemaining => _dashCooldownTimer?.TimeLeft ?? 0.0;
    public Timer? DashCooldownTimer => _dashCooldownTimer;

    private Timer _dashTimer = null!;
    private Timer _dashCooldownTimer = null!;
    private Vector2 _cachedScreenMin;
    private Vector2 _cachedScreenMax;
    private Vector2 _cachedScreenSize;

    public int TargetPriority { get; set; } = 0;
    public AttackType CurrentAttackType { get; set; } = AttackType.Melee;
    public float AttackCooldown { get; set; } = Core.GameConfig.BaseAttackCooldown;
    public float AttackSpeedMultiplier { get; set; } = 1.0f;
    public float WeaponAttackSpeedModifier { get; set; } = 1.0f;
    public float WeaponCooldown { get; set; } = 0.0f;
    public float WeaponRangeModifier { get; set; } = 1.0f;

    public float SplashRadius { get; set; } = 0.0f;
    public int MeleeTargetCount { get; set; } = 1;
    public int KillExplosionDamage { get; set; } = 0;
    public int KillShieldAmount { get; set; } = 0;
    public float KnockbackForce { get; set; } = 0.0f;

    public float TrajectoryDamageRatio { get; set; } = 0.0f;
    public bool ReturningProjectiles { get; set; } = false;
    public int ChainCount { get; set; } = 0;
    public int BounceCount { get; set; } = 0;
    public int MultiShotCount { get; set; } = 1;
    public int PiercingCount { get; set; } = 0;

    public float CharmDuration { get; set; } = 0.0f;
    public float PushForce { get; set; } = 0.0f;
    public float StunDuration { get; set; } = 0.0f;
    public float DotDamage { get; set; } = 0.0f;

    public float DamageBonus { get; set; } = 0.0f;
    public float LifeStealPercent { get; set; } = 0.0f;
    public float CriticalChance { get; set; } = 0.0f;
    public float FreezeDuration { get; set; } = 0.0f;
    public float PoisonDamage { get; set; } = 0.0f;
    public float CritMultiplier { get; set; } = Core.GameConfig.CritMultiplier;
    public int HealthCostPerAttack { get; set; } = 0;

    public int ShieldAmount { get; set; } = 0;
    public float InvincibleTimer { get; set; } = 0.0f;
    public float InvincibleDuration { get; set; } = Core.GameConfig.InvincibleDuration;

    public float PoisonedTimer { get; set; } = 0.0f;
    public float PoisonedTickTimer { get; set; } = 0.0f;
    public float PoisonedDps { get; set; } = 0.0f;

    public Sprite2D? Sprite { get; set; }
    public Sprite2D? Shadow { get; set; }
    public CollisionShape2D? Collision { get; set; }
    public Label? HealthDisplay { get; set; }
    public Label? AttackTypeDisplay { get; set; }
    public Node2D? MeleeRangeIndicator { get; set; }
    public bool ShowMeleeRange { get; set; } = true;
    public bool IsDead { get; set; } = false;

    public PlayerCombat? Combat { get; set; }
    public PlayerAura? Aura { get; set; }
    public PlayerVfx? Vfx { get; set; }
    public PlayerBuffSystem? BuffSystem { get; set; }

    public override void _Ready()
    {
        BaseMaxHealth = MaxHealth;
        CurrentHealth = MaxHealth;
        CurrentDamage = BaseDamage;
        CurrentSpeed = BaseSpeed;

        Sprite = new Sprite2D();
        AddChild(Sprite);

        Collision = new CollisionShape2D();
        var shape = new RectangleShape2D();
        shape.Size = Core.GameConfig.PlayerCollisionSize;
        Collision.Shape = shape;
        AddChild(Collision);

        // Set collision layers for zombie detection
        CollisionLayer = 0b001; // Layer 1 - zombies detect this
        CollisionMask = 0b010; // Layer 2 - collide with zombies

        Vfx = new PlayerVfx { Name = "VFX" };
        AddChild(Vfx);
        Vfx.Setup(this);

        Combat = new PlayerCombat { Name = "Combat" };
        AddChild(Combat);
        Combat.Setup(this);

        Aura = new PlayerAura { Name = "Aura" };
        AddChild(Aura);
        Aura.Setup(this);

        BuffSystem = new PlayerBuffSystem { Name = "BuffSystem" };
        AddChild(BuffSystem);
        BuffSystem.Setup(this);

        _SetupDash();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDashing)
        {
            Vfx?.SpawnDashTrail((float)delta);
            MoveAndSlide();
            return;
        }

        _HandleMovement();
        Vfx?.UpdateWalkAnimation((float)delta);
        _HandleDashInput();
        _HandleTargetCycle();
        _HandleStatusEffects((float)delta);
        _HandleInvincibility((float)delta);
        Aura?.Handle((float)delta);
        Combat?.HandleManualAttack();
        _HandleCollisionRepulsion();
        MoveAndSlide();
        _ClampToScreen();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            if (keyEvent.Keycode == Key.Tab)
            {
                TargetPriority = (TargetPriority + 1) % 3;
                GameServices.UI.UpdatePlayerStatus(this);
            }
        }
    }

    private void _HandleMovement()
    {
        Vector2 inputVec = Vector2.Zero;
        if (Input.IsActionPressed("move_right")) inputVec.X += 1;
        if (Input.IsActionPressed("move_left"))  inputVec.X -= 1;
        if (Input.IsActionPressed("move_down"))   inputVec.Y += 1;
        if (Input.IsActionPressed("move_up"))     inputVec.Y -= 1;

        Velocity = inputVec != Vector2.Zero ? inputVec.Normalized() * CurrentSpeed : Vector2.Zero;
    }

    private void _ClampToScreen()
    {
        // Use world bounds instead of screen bounds for larger world
        var worldSize = Core.GameConfig.WorldSize;
        var margin = Core.GameConfig.BoundsPadding;

        GlobalPosition = new Vector2(
            Mathf.Clamp(GlobalPosition.X, margin, worldSize.X - margin),
            Mathf.Clamp(GlobalPosition.Y, margin, worldSize.Y - margin)
        );
    }

    private void _HandleCollisionRepulsion()
    {
        foreach (var zombie in Core.ZombieRegistry.All)
        {
            if (!IsInstanceValid(zombie))
                continue;
            float dist = GlobalPosition.DistanceTo(zombie.GlobalPosition);
            if (dist < Core.GameConfig.RepulsionDistance && dist > 0)
            {
                Vector2 dir = (zombie.GlobalPosition - GlobalPosition).Normalized();
                float factor = (Core.GameConfig.RepulsionDistance - dist) / Core.GameConfig.RepulsionDistance;
                Velocity += dir * factor * Core.GameConfig.RepulsionForce * 0.016f;
            }
        }
    }

    private void _SetupDash()
    {
        _dashTimer = new Timer { WaitTime = Core.GameConfig.DashDuration, OneShot = true };
        _dashTimer.Timeout += _OnDashFinished;
        AddChild(_dashTimer);

        _dashCooldownTimer = new Timer { WaitTime = Core.GameConfig.DashCooldown, OneShot = true };
        _dashCooldownTimer.Timeout += () => CanDash = true;
        AddChild(_dashCooldownTimer);
    }

    private void _HandleDashInput()
    {
        if (Input.IsActionJustPressed("dash") && CanDash && Velocity != Vector2.Zero)
        {
            IsDashing = true;
            CanDash = false;
            Vfx?.PlayDashAnim();
            Vfx?.ResetDashTrail();
            Velocity = Velocity.Normalized() * CurrentSpeed * Core.GameConfig.DashSpeedMult;
            if (Collision != null) Collision.Disabled = true;
            _dashTimer.Start();

            if (Sprite != null)
            {
                var tw = CreateTween();
                tw.TweenProperty(Sprite, "modulate", new Color(0.5f, 0.8f, 1.0f, 0.5f), 0.05f);
            }
        }
    }

    private void _OnDashFinished()
    {
        IsDashing = false;
        Velocity = Vector2.Zero;
        if (Collision != null) Collision.Disabled = false;
        if (Sprite != null) Sprite.Modulate = Colors.White;
        _dashCooldownTimer.Start();
    }

    private void _HandleTargetCycle()
    {
        if (Input.IsActionJustPressed("cycle_target"))
        {
            TargetPriority = (TargetPriority + 1) % 3;
            GameServices.UI.UpdatePlayerStatus(this);
        }
    }

    private void _HandleStatusEffects(float delta)
    {
        if (PoisonedTimer > 0)
        {
            PoisonedTimer -= delta;
            PoisonedTickTimer -= delta;
            if (PoisonedTickTimer <= 0)
            {
                PoisonedTickTimer = Core.GameConfig.ZombiePoisonTick;
                int dmg = (int)PoisonedDps;
                if (dmg > 0)
                    TakeDamage(dmg);
            }
            if (PoisonedTimer <= 0 && Sprite != null)
                Sprite.Modulate = Colors.White;
        }
    }

    private void _HandleInvincibility(float delta)
    {
        if (InvincibleTimer > 0)
        {
            InvincibleTimer -= delta;
            if (InvincibleTimer <= 0 && Sprite != null)
                Sprite.Modulate = Colors.White;
        }
    }

    public void ApplyPoison(float damagePerSecond, float duration)
    {
        PoisonedDps = Math.Max(PoisonedDps, damagePerSecond);
        PoisonedTimer = Math.Max(PoisonedTimer, duration);
        PoisonedTickTimer = Math.Min(PoisonedTickTimer, 0.1f);
        if (Sprite != null)
            Sprite.Modulate = new Color(0.7f, 0.3f, 0.8f);
    }

    public void ApplyBuffFromZombie(BuffType buffType, float duration, float value)
    {
        if (BuffSystem == null) return;
        var source = buffType switch
        {
            BuffType.Poison or BuffType.Burn => BuffSource.ZombieSpitter,
            _ => BuffSource.ZombieHealer
        };
        BuffSystem.AddBuff(buffType, source, duration, value);
    }

    public void TakeDamage(int amount, Zombie? attacker = null)
    {
        if (IsDead || InvincibleTimer > 0)
            return;

        if (ShieldAmount > 0)
        {
            int blocked = Math.Min(ShieldAmount, amount);
            ShieldAmount -= blocked;
            amount -= blocked;
            GameServices.UI.ShowDamageNumber(GlobalPosition, -blocked, false);
            VFXManager.SpawnFloatingLabel(GetParent(), GlobalPosition, "护盾吸收", Colors.Cyan);
        }

        if (amount > 0)
        {
            CurrentHealth = Math.Max(0, CurrentHealth - amount);
            HealthChanged?.Invoke(CurrentHealth);
            GameServices.UI.ShowDamageNumber(GlobalPosition, amount, true);
            GameServices.UI.FlashDamage();
            GameServices.UI.ShakeScreen(5.0f);
            InvincibleTimer = InvincibleDuration;
            VFXManager.FlashHurt(this, Sprite);
            Vfx?.PlayHurtAnim();
            SoundManager.Instance?.PlayPlayerHurt();
            Vfx?.UpdateStatusDisplays();

            // Counterattack: thorns-like damage when hit
            _TryCounterattack(attacker);

            if ((float)CurrentHealth / (float)MaxHealth <= Core.GameConfig.HealthLowThreshold)
                VFXManager.SpawnFloatingLabel(GetParent(), GlobalPosition, "危险！", Colors.Red);

            if (CurrentHealth <= 0)
                Die();
        }
    }

    private void _TryCounterattack(Zombie? attacker)
    {
        // Check if player has any thorns/counterattack ability
        // For now, we give a small chance to reflect damage
        float counterChance = 0.15f; // 15% chance to counter
        if (GD.Randf() < counterChance && attacker != null)
        {
            int counterDamage = (int)(CurrentDamage * 0.3f);
            if (counterDamage > 0)
            {
                attacker.TakeDamage(counterDamage);
                GameServices.UI.ShowDamageNumber(attacker.GlobalPosition, counterDamage, false);
                VFXManager.SpawnFloatingLabel(GetParent(), GlobalPosition, "反击!", new Color(1.0f, 0.5f, 0.3f));
            }
        }
    }

    public void Heal(int amount)
    {
        CurrentHealth = Math.Min(MaxHealth, CurrentHealth + amount);
        HealthChanged?.Invoke(CurrentHealth);
    }

    public void ApplyWeaponEffect(object? card)
    {
        if (card is WeaponCard wc)
            Combat?.ApplyWeaponEffect(wc);
    }

    public void ApplyUpgradeCard(object? card)
    {
        if (card is UpgradeCard uc)
            Combat?.ApplyUpgradeCard(uc);
    }

    public void Die()
    {
        if (IsDead)
            return;
        IsDead = true;
        SetPhysicsProcess(false);
        SetProcess(false);
        if (Collision != null) Collision.Disabled = true;
        _dashTimer?.Stop();
        _dashCooldownTimer?.Stop();
        Combat?.AttackTimer.Stop();
        if (Combat != null)
            Combat.CanAttack = true;
        if (HealthDisplay != null) HealthDisplay.Visible = false;
        if (AttackTypeDisplay != null) AttackTypeDisplay.Visible = false;
        if (MeleeRangeIndicator != null) MeleeRangeIndicator.Visible = false;
        if (Aura != null) Aura.Visible = false;

        if (Sprite != null)
        {
            var tw = CreateTween();
            tw.TweenProperty(Sprite, "modulate", new Color(1.0f, 0.2f, 0.2f, 1.0f), 0.15f);
            tw.TweenProperty(Sprite, "modulate", new Color(0.5f, 0.1f, 0.1f, 0.5f), 0.4f);
            tw.Parallel().TweenProperty(Sprite, "scale", new Vector2(0.3f, 0.3f), 0.4f)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
            tw.Parallel().TweenProperty(Sprite, "rotation", (float)GD.RandRange(-1.0, 1.0), 0.4f);
            tw.TweenProperty(Sprite, "modulate:a", 0.0f, 0.3f);
        }

        VFXManager.SpawnParticleBurst(GetParent(), GlobalPosition, new Color(0.8f, 0.15f, 0.1f, 0.9f),
            30, 0.8f, 40.0f, 120.0f, new Vector3(0, 150, 0), 1.5f, 3.5f);

        SoundManager.Instance?.PlayGameOver();

        Died?.Invoke();
    }

    public void ApplyWeaponEffect(WeaponCard weapon)
    {
        Combat?.ApplyWeaponEffect(weapon);
    }

    public void ApplyUpgradeCard(UpgradeCard upgrade)
    {
        Combat?.ApplyUpgradeCard(upgrade);
    }

    public void RegisterDamageDealt(Zombie zombie, float damage)
    {
        Combat?.RegisterDamageDealt(zombie, damage);
    }

    public void ActivateAura()
    {
        Aura?.Activate();
    }

    public void UpgradeAura(float extraDamage = 3.0f, float extraRadius = 15.0f)
    {
        Aura?.Upgrade(extraDamage, extraRadius);
    }

    public void ResetForNewRound()
    {
        var permanent = new Array<WeaponCard>();
        foreach (var e in ActiveEffects)
        {
            if (e.Duration == -1)
                permanent.Add(e);
        }
        ActiveEffects = permanent;
        Combat?.RecalculateStats();
    }
}
