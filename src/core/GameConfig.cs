namespace ZombieWars.Core;

/// <summary>
/// Global game configuration — single source of truth for all tunable parameters.
/// Eliminates magic numbers across the entire project and centralizes balance values.
/// </summary>
public static class GameConfig
{
    // ═══════════════════════════════════════════════
    // World Settings
    // ═══════════════════════════════════════════════

    public static readonly Godot.Vector2 WorldSize = new(1920, 1080);

    // ═══════════════════════════════════════════════
    // Player Attributes
    // ═══════════════════════════════════════════════

    public const int PlayerMaxHealth = 120;
    public const float PlayerBaseDamage = 12.0f;
    public const float PlayerBaseSpeed = 220.0f;
    public static readonly Godot.Vector2 PlayerCollisionSize = new(32.0f, 32.0f);

    // Dash
    public const float DashSpeedMult = 3.5f;
    public const float DashDuration = 0.25f;

    // Game flow
    public const float GetReadyDuration = 2.0f; // Delay before weapon selection after round ends
    public const float DashCooldown = 0.8f;
    public const float DashTrailInterval = 0.04f;

    // Invincibility frames
    public const float InvincibleDuration = 0.5f;

    // Critical hit
    public const float CritMultiplier = 2.5f;

    // Attack
    public const float BaseAttackCooldown = 0.45f;
    public const float BaseAttackRange = 70.0f;
    public const float RangedAttackRange = 600.0f;
    public const float MagicAttackRange = 600.0f;
    public const float AreaDamageMult = 0.7f;
    public const float SplashDamageMult = 0.5f;
    public const float MinAttackCooldown = 0.12f;
    public const float MinDamageMult = 0.2f;
    public const float MinRangeMult = 0.6f;
    public const float BulletVelocity = 500.0f;

    // Kill reward
    public const float KillExplosionRadius = 100.0f;
    public const int MaxShield = 50;

    // Collision
    public const float RepulsionDistance = 30.0f;
    public const float RepulsionForce = 200.0f;

    // Zombie attack
    public const float ZombieDefaultAttackRange = 70.0f; // Must be > RepulsionDistance for melee to work
    public const float ZombieRangedAttackRange = 250.0f; // Range for ranged zombie attacks
    public const float ZombieMeleeTouchRange = 50.0f; // Touch damage range

    // Screen bounds
    public const float BoundsPadding = 22.0f;
    public const float TopUiMargin = 56.0f;

    // Animation
    public const float WalkAnimSpeed = 0.1f;

    // ═══════════════════════════════════════════════
    // Aura System
    // ═══════════════════════════════════════════════

    public const float AuraTickInterval = 0.12f;
    public const int AuraOrbCount = 3;
    public const float AuraOrbRadius = 8.0f;
    public const float AuraBaseDamage = 5.0f;
    public const float AuraBaseRadius = 80.0f;
    public const float AuraHitCooldown = 0.4f;
    public const float AuraRotationSpeed = 2.0f;
    public const float AuraDetectRadiusMult = 3.0f;
    public const float AuraDetectExtra = 18.0f;

    // ═══════════════════════════════════════════════
    // Zombie Attributes
    // ═══════════════════════════════════════════════

    public static readonly Godot.Vector2 ZombieCollisionSize = new(24.0f, 24.0f);
    public const float ZombieAttackCooldown = 1.0f;
    public const float ZombiePoisonTick = 1.0f;
    public const float ZombieInvisibleRevealMin = 2.0f;
    public const float ZombieInvisibleRevealMax = 4.0f;
    public const float ZombieEliteSpeedMult = 1.05f;
    public const float ZombieEliteStatMult = 1.25f;

    // Special zombie types
    public const float SpitterCooldown = 3.0f;
    public const float SpitterRange = 200.0f;
    public const float SpitterPoisonDps = 5.0f;
    public const float SpitterPoisonDuration = 3.0f;
    public const float HealerCooldown = 2.5f;
    public const float HealerRange = 120.0f;
    public const int HealerAmount = 8;
    public const float ExploderRange = 100.0f;
    public const float ExploderDamageMult = 2.0f;
    public const float CharmSearchRange = 600.0f;

    // Debuff effects
    public const float FreezeSlowMult = 0.4f;
    public const float FreezeDurationDefault = 0.8f;
    public const float PoisonDurationDefault = 3.0f;

    // Death particles
    public const int ZombieDeathParticlesDefault = 25;
    public const int ZombieDeathParticlesTank = 40;
    public const int ZombieDeathParticlesExploder = 50;

    // Zombie visuals
    public static readonly Godot.Vector2 ZombieShadowSize = new(28.0f, 10.0f);
    public static readonly Godot.Color ZombieShadowColor = new(0.0f, 0.0f, 0.0f, 0.5f);
    public static readonly Godot.Vector2 ZombieShadowOffset = new(0.0f, 14.0f);
    public static readonly Godot.Color ZombieEliteRingColor = new(1.0f, 0.85f, 0.2f, 0.75f);
    public static readonly Godot.Vector2 ZombieEliteScale = new(1.15f, 1.15f);
    public const int ZombieStatusFontSize = 10;
    public static readonly Godot.Vector2 ZombieStatusHpOffset = new(-15.0f, -45.0f);
    public static readonly Godot.Vector2 ZombieStatusAtkOffset = new(-15.0f, -35.0f);
    public const int ZombieDamagePopupSize = 14;

    // Zombie animation
    public const float ZombieHitFlashIn = 0.04f;
    public const float ZombieHitFlashOut = 0.12f;
    public const float ZombieKnockbackDist = 3.0f;
    public const float ZombieCorpseFade = 0.5f;
    public const float ZombieBloodFade = 8.0f;

    // Charm resistance
    public const float CharmResistTank = 0.4f;
    public const float CharmResistFast = 0.2f;
    public const float CharmMinDuration = 1.0f;

    // Summoner zombie
    public const float SummonerCooldown = 5.0f;
    public const float SummonerRange = 100.0f;
    public const int SummonerCountMin = 1;
    public const int SummonerCountMax = 2;

    // Boss system
    public const float BossStatMult = 3.0f;
    public const float BossSpeedMult = 0.7f;
    public const float BossScoreMult = 5.0f;
    public static readonly Godot.Vector2 BossScale = new(1.8f, 1.8f);
    public const int BossInterval = 5;

    // Elite affixes
    public const float EliteRegenPercent = 0.02f;
    public const float EliteSwiftMult = 1.5f;
    public const float EliteExplosiveRange = 120.0f;
    public const float EliteExplosiveMult = 2.0f;

    // Round scaling
    public const float SpitterMinCooldown = 1.5f;
    public const float SpitterCooldownDecay = 0.03f;
    public const float RoundSpeedScaleFactor = 0.6f;

    // ═══════════════════════════════════════════════
    // Round Management
    // ═══════════════════════════════════════════════

    public const float SpawnMargin = 30.0f;
    public const float SpawnTopMarginFallback = 110.0f;
    public const float SpawnOffsetRange = 20.0f;
    public const float RoundTransitionDelay = 2.0f;
    public const float EdgeIndicatorInterval = 0.5f;
    public const int EdgeIndicatorMinCount = 2;
    public const float EliteScoreMult = 1.5f;

    // Elite spawn rate balance (adjusted)
    public const float EliteChanceMax = 0.18f; // Reduced from 0.25f for better balance

    // ═══════════════════════════════════════════════
    // Combo System
    // ═══════════════════════════════════════════════

    public const float ComboDecayTime = 2.0f;
    public const float ComboDamageBonusPerLevel = 0.05f;
    public const float ComboMaxBonus = 0.5f;

    // ═══════════════════════════════════════════════
    // Audio
    // ═══════════════════════════════════════════════

    public const int AudioSampleRate = 44100;
    public const float MusicVolumeDb = -10.0f;
    public const float SfxVolumeDb = -5.0f;

    // ═══════════════════════════════════════════════
    // Projectiles
    // ═══════════════════════════════════════════════

    public const float ProjectileLifetime = 3.0f;
    public const float ProjectileBoundsPadding = 8.0f;
    public const float TrajectoryDetectRadius = 30.0f;

    // ═══════════════════════════════════════════════
    // Magic Missiles
    // ═══════════════════════════════════════════════

    public const float MissileSpeed = 300.0f;
    public const float MissileHomingStrength = 1.0f;
    public const float MissileLifetime = 5.0f;
    public const float ChainDamageDecay = 0.8f;
    public const float ChainSearchRange = 150.0f;
    public const float MissilePoisonDuration = 5.0f;

    // ═══════════════════════════════════════════════
    // UI
    // ═══════════════════════════════════════════════

    public const float HudHeight = 52.0f;
    public const float ShakeDecay = 8.0f;
    public const float TooltipMaxWidth = 360.0f;
    public static readonly Godot.Vector2 TooltipOffset = new(15.0f, 15.0f);

    // Health thresholds
    public const float HealthHighThreshold = 0.6f;
    public const float HealthMedThreshold = 0.3f;
    public const float HealthLowThreshold = 0.25f;

    // Health bar colors
    public static readonly Godot.Color HpColorHigh = new(0.22f, 0.55f, 0.22f);
    public static readonly Godot.Color HpColorMed = new(0.65f, 0.60f, 0.15f);
    public static readonly Godot.Color HpColorLow = new(0.70f, 0.15f, 0.12f);

    // ═══════════════════════════════════════════════
    // Visual Effects (VFX)
    // ═══════════════════════════════════════════════

    public const int VfxImpactRingSegments = 16;
    public static readonly Godot.Vector2 VfxRingExpandScale = new(2.5f, 2.5f);
    public const float VfxRingExpandDuration = 0.3f;
    public const int VfxHitParticleCount = 5;
    public const float VfxHitLifetime = 0.3f;
    public const float VfxPopupFloatDistance = 40.0f;
    public const float VfxPopupDuration = 0.8f;

    // Configurable particle counts for performance tuning
    public const int VfxDeathParticlesDefault = 15;
    public const int VfxDeathParticlesTank = 25;
    public const int VfxDeathParticlesBoss = 35;
    public const int VfxExplosionParticles = 25;

    // Damage number optimization
    public const int VfxMaxDamageNumbers = 15;
    public const float VfxDamageNumberThrottle = 0.05f;
}
