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

    // 碰撞层（物理位掩码）—— 全局统一约定，避免散落魔法数字
    // Layer 1 = 玩家, Layer 2 = 僵尸, Layer 3 = 固体障碍物, Layer 4 = 可破坏障碍
    public const uint CollisionLayerPlayer = 0b0001;
    public const uint CollisionLayerZombie = 0b0010;
    public const uint CollisionLayerObstacleSolid = 0b0100;
    public const uint CollisionLayerObstacleDestructible = 0b1000;
    public const uint CollisionMaskPlayer = CollisionLayerZombie | CollisionLayerObstacleSolid | CollisionLayerObstacleDestructible;
    public const uint CollisionMaskZombie = CollisionLayerObstacleSolid | CollisionLayerObstacleDestructible;

    // ═══════════════════════════════════════════════
    // Map System（多地图 + 障碍物 + 导航）
    // ═══════════════════════════════════════════════

    // 每隔多少回合切换一张新地图（0 表示仅每局开始切换一次）
    public const int MapRerollRounds = 3;
    // 障碍物避开中心出生安全区半径（占比）
    public const float MapSpawnSafeRadiusX = 0.12f;
    public const float MapSpawnSafeRadiusY = 0.12f;
    // 可破坏障碍物
    public const int DestructibleObstacleHealth = 40;
    public const float DestructibleObstacleShakeOnHit = 2.0f;
    // 陷阱/危险区域
    public const float HazardDamagePerSec = 6.0f;
    public const float HazardSlowMult = 0.55f;
    // 低矮装饰障碍减速
    public const float LowObstacleSlowMult = 0.6f;
    // 导航
    public const float NavAgentPathDesiredDist = 8.0f;
    public const float NavAgentTargetDesiredDist = 12.0f;
    public const float NavAgentRadius = 16.0f;

    // ═══════════════════════════════════════════════
    // Camera System（镜头动感：平滑跟随 + 前瞻 + 震动 + zoom punch + 冲刺）
    // ═══════════════════════════════════════════════

    public static readonly Godot.Vector2 CameraBaseZoom = new(1.05f, 0.85f); // X 轻微聚焦，Y 压缩做斜俯视
    public const float CameraSmoothing = 6.5f;        // 跟随平滑系数（指数阻尼，越大越贴）
    public const float CameraLookaheadFactor = 0.20f; // 速度 → 前瞻距离系数
    public const float CameraLookaheadMax = 90f;      // 前瞻距离上限（像素）
    public const float CameraShakeDecay = 16.0f;      // 震动衰减速率
    public const float CameraShakeToZoom = 0.003f;    // 震动强度 → 相对 zoom punch 转换
    public const float CameraZoomPunchDecay = 9.0f;   // zoom punch 衰减速率
    public const float CameraDashZoomOut = 0.08f;     // 冲刺时相对 zoom out（保持斜视比例）

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
    public const float WalkAnimSpeed = 0.06f; // Faster for smoother animation (was 0.1f)
    public const float IdleBreathSpeed = 2.0f; // Breathing animation cycle

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
    public static readonly Godot.Vector2 ZombieShadowOffset = new(6.0f, 14.0f);
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
    // HitStop（命中停顿 / 打击感凝固）
    // ═══════════════════════════════════════════════

    public const float HitStopFrozenScale = 0.03f;  // 冻结期 TimeScale（近0，留一丝喘息避免完全硬切）
    public const int HitStopCritMs = 45;            // 暴击命中
    public const int HitStopEliteKillMs = 110;      // 精英怪击杀
    public const int HitStopKillMs = 70;            // 普通击杀
    public const int HitStopBossSpawnMs = 180;      // Boss 出场（配合演出）
    public const int HitStopBossPhaseMs = 100;      // Boss 阶段切换（战斗中，比出场短）
    public const int HitStopBossKillMs = 220;       // Boss 击杀（慢动作）

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

    // UI 间距（VBox / HBox separation）
    public const int UiSpacingHairline = 3;
    public const int UiSpacingTight = 6;
    public const int UiSpacingNormal = 8;
    public const int UiSpacingMedium = 12;
    public const int UiSpacingWide = 16;
    public const int UiSpacingHuge = 25;
    public const int UiSpacingSection = 30;

    // UI 边距（Panel.margin_*_）
    public const int UiMarginTiny = 4;
    public const int UiMarginSmall = 10;
    public const int UiMarginMedium = 15;
    public const int UiMarginLarge = 24;
    public const int UiMarginXLarge = 16;     // tooltip / 卡片水平 padding
    public const int UiMarginHeroCard = 28;    // 回合结算 hero card 水平
    public const int UiMarginHeroPad = 24;     // 回合结算 hero card 垂直
    public const int UiMarginPanel = 30;       // 大型 panel 外边距
    public const int UiMarginPanelInner = 25;  // 大型 panel 内边距
    public const int UiMarginHuge = 50;

    // UI 字号
    public const int UiFontMicro = 11;
    public const int UiFontSmall = 12;
    public const int UiFontBody = 14;
    public const int UiFontEmph = 16;
    public const int UiFontTitle = 22;
    public const int UiFontHuge = 36;

    // UI 描边
    public const int UiOutlineThin = 1;
    public const int UiOutlineNorm = 2;
    public const int UiOutlineBold = 3;
    public const int UiOutlineHero = 4;

    // Health thresholds
    public const float HealthHighThreshold = 0.6f;
    public const float HealthMedThreshold = 0.3f;
    public const float HealthLowThreshold = 0.25f;

    // Health bar colors
    public static readonly Godot.Color HpColorHigh = new(0.65f, 0.35f, 0.35f);
    public static readonly Godot.Color HpColorMed = new(0.75f, 0.45f, 0.25f);
    public static readonly Godot.Color HpColorLow = new(0.753f, 0.224f, 0.169f);

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

    // ═══════════════════════════════════════════════
    // 数学系统 — 波形共振
    // ═══════════════════════════════════════════════

    public const float WaveBaseAmplitude = 200.0f;
    public const float WaveAmplitudeGrowth = 15.0f;
    public const float WaveBaseFrequency = 0.5f;
    public const float WaveFrequencyGrowth = 0.05f;
    public const int WaveMaxHarmonics = 5;
    public const float WaveResonanceWidth = 0.15f;
    public const float WaveResonanceMaxMultiplier = 2.0f;
    public const float WaveSpawnVisualRange = 120.0f;

    // ═══════════════════════════════════════════════
    // 数学系统 — 向量场
    // ═══════════════════════════════════════════════

    public const int FieldMaxActiveNodes = 3;
    public const float FieldNodeDuration = 15.0f;
    public const float FieldAttractorStrength = 150.0f;
    public const float FieldRepulsorStrength = 200.0f;
    public const float FieldVortexStrength = 120.0f;
    public const float FieldSoftening = 10.0f;
    public const float FieldMaxForce = 300.0f;
    public const float FieldPreviewRange = 150.0f;

    // ═══════════════════════════════════════════════
    // 数学系统 — 分形
    // ═══════════════════════════════════════════════

    public const int FractalMaxDepth = 3;
    public const float FractalSpeedMultiplier = 1.3f;
    public const int FractalDefaultBranching = 2;

    // ═══════════════════════════════════════════════
    // 数学系统 — 博弈论策略
    // ═══════════════════════════════════════════════

    public const float StrategyMaxAdaptation = 0.5f;

    // ═══════════════════════════════════════════════
    // 数学系统 — 特性开关（static readonly 避免 const 死代码警告）
    // ═══════════════════════════════════════════════

    public static readonly bool MathSystemsEnabled = true;
}
