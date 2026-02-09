class_name GameConfig
extends RefCounted
## 全局游戏配置 — 所有可调参数的唯一真实来源
## 消除全项目的魔法数字，集中管理平衡性参数

# ═══════════════════════════════════════════════
# 玩家属性
# ═══════════════════════════════════════════════
const PLAYER_MAX_HEALTH: int = 120
const PLAYER_BASE_DAMAGE: float = 12.0
const PLAYER_BASE_SPEED: float = 220.0
const PLAYER_COLLISION_SIZE = Vector2(32, 32)

# 冲刺
const DASH_SPEED_MULT: float = 3.5
const DASH_DURATION: float = 0.25
const DASH_COOLDOWN: float = 0.8
const DASH_TRAIL_INTERVAL: float = 0.04

# 无敌帧
const INVINCIBLE_DURATION: float = 0.5

# 暴击
const CRIT_MULTIPLIER: float = 2.5

# 攻击
const BASE_ATTACK_COOLDOWN: float = 0.45
const BASE_ATTACK_RANGE: float = 70.0
const RANGED_ATTACK_RANGE: float = 600.0
const MAGIC_ATTACK_RANGE: float = 600.0
const AREA_DAMAGE_MULT: float = 0.7
const SPLASH_DAMAGE_MULT: float = 0.5
const MIN_ATTACK_COOLDOWN: float = 0.12
const MIN_DAMAGE_MULT: float = 0.2
const MIN_RANGE_MULT: float = 0.6
const BULLET_VELOCITY: float = 500.0

# 击杀奖励
const KILL_EXPLOSION_RADIUS: float = 100.0
const MAX_SHIELD: int = 50

# 碰撞
const REPULSION_DISTANCE: float = 30.0
const REPULSION_FORCE: float = 200.0

# 画面边界
const BOUNDS_PADDING: float = 22.0
const TOP_UI_MARGIN: float = 56.0

# 动画
const WALK_ANIM_SPEED: float = 0.15

# ═══════════════════════════════════════════════
# 光环系统
# ═══════════════════════════════════════════════
const AURA_TICK_INTERVAL: float = 0.12
const AURA_ORB_COUNT: int = 3
const AURA_ORB_RADIUS: float = 8.0
const AURA_BASE_DAMAGE: float = 5.0
const AURA_BASE_RADIUS: float = 80.0
const AURA_HIT_COOLDOWN: float = 0.4
const AURA_ROTATION_SPEED: float = 2.0
const AURA_DETECT_RADIUS_MULT: float = 3.0
const AURA_DETECT_EXTRA: float = 18.0

# ═══════════════════════════════════════════════
# 僵尸属性
# ═══════════════════════════════════════════════
const ZOMBIE_DEFAULT_ATTACK_RANGE: float = 40.0
const ZOMBIE_COLLISION_SIZE = Vector2(24, 24)
const ZOMBIE_ATTACK_COOLDOWN: float = 1.0
const ZOMBIE_POISON_TICK: float = 1.0
const ZOMBIE_INVISIBLE_REVEAL_MIN: float = 2.0
const ZOMBIE_INVISIBLE_REVEAL_MAX: float = 4.0
const ZOMBIE_ELITE_SPEED_MULT: float = 1.05
const ZOMBIE_ELITE_STAT_MULT: float = 1.25

# 特殊僵尸
const SPITTER_COOLDOWN: float = 3.0
const SPITTER_RANGE: float = 200.0
const SPITTER_POISON_DPS: float = 5.0
const SPITTER_POISON_DURATION: float = 3.0
const HEALER_COOLDOWN: float = 2.5
const HEALER_RANGE: float = 120.0
const HEALER_AMOUNT: int = 8
const EXPLODER_RANGE: float = 100.0
const EXPLODER_DAMAGE_MULT: float = 2.0
const CHARM_SEARCH_RANGE: float = 600.0

# 冰冻效果
const FREEZE_SLOW_MULT: float = 0.4
const FREEZE_DURATION_DEFAULT: float = 0.8
const POISON_DURATION_DEFAULT: float = 3.0

# 僵尸死亡粒子数
const ZOMBIE_DEATH_PARTICLES_DEFAULT: int = 25
const ZOMBIE_DEATH_PARTICLES_TANK: int = 40
const ZOMBIE_DEATH_PARTICLES_EXPLODER: int = 50

# 僵尸视觉
const ZOMBIE_SHADOW_SIZE = Vector2(28, 10)
const ZOMBIE_SHADOW_COLOR = Color(0, 0, 0, 0.5)
const ZOMBIE_SHADOW_OFFSET = Vector2(0, 14)
const ZOMBIE_ELITE_RING_COLOR = Color(1.0, 0.85, 0.2, 0.75)
const ZOMBIE_ELITE_SCALE = Vector2(1.15, 1.15)
const ZOMBIE_STATUS_FONT_SIZE: int = 10
const ZOMBIE_STATUS_HP_OFFSET = Vector2(-15, -45)
const ZOMBIE_STATUS_ATK_OFFSET = Vector2(-15, -35)
const ZOMBIE_DAMAGE_POPUP_SIZE: int = 14

# 僵尸动画
const ZOMBIE_HIT_FLASH_IN: float = 0.04
const ZOMBIE_HIT_FLASH_OUT: float = 0.12
const ZOMBIE_KNOCKBACK_DIST: float = 3.0
const ZOMBIE_CORPSE_FADE: float = 0.5
const ZOMBIE_BLOOD_FADE: float = 8.0

# 魅惑抗性
const CHARM_RESIST_TANK: float = 0.4
const CHARM_RESIST_FAST: float = 0.2
const CHARM_MIN_DURATION: float = 1.0

# 回合缩放
const SPITTER_MIN_COOLDOWN: float = 1.5
const SPITTER_COOLDOWN_DECAY: float = 0.03
const ROUND_SPEED_SCALE_FACTOR: float = 0.6

# ═══════════════════════════════════════════════
# 回合管理
# ═══════════════════════════════════════════════
const SPAWN_MARGIN: float = 30.0
const SPAWN_TOP_MARGIN_FALLBACK: float = 110.0
const SPAWN_OFFSET_RANGE: float = 20.0
const ROUND_TRANSITION_DELAY: float = 2.0
const EDGE_INDICATOR_INTERVAL: float = 0.5
const EDGE_INDICATOR_MIN_COUNT: int = 2
const ELITE_SCORE_MULT: float = 1.5

# 僵尸类型标量数据 — 用于不需要 Color 的场景（日志、计分等）
# 复杂嵌套数据（含 Color）由各使用者自行持有，避免 GDScript const 解析限制

# ═══════════════════════════════════════════════
# 音效参数
# ═══════════════════════════════════════════════
const AUDIO_SAMPLE_RATE: int = 44100
const MUSIC_VOLUME_DB: float = -10.0
const SFX_VOLUME_DB: float = -5.0

# ═══════════════════════════════════════════════
# 投射物
# ═══════════════════════════════════════════════
const PROJECTILE_LIFETIME: float = 3.0
const PROJECTILE_BOUNDS_PADDING: float = 8.0
const TRAJECTORY_DETECT_RADIUS: float = 30.0

# ═══════════════════════════════════════════════
# 魔法飞弹
# ═══════════════════════════════════════════════
const MISSILE_SPEED: float = 300.0
const MISSILE_HOMING_STRENGTH: float = 5.0
const MISSILE_LIFETIME: float = 5.0
const CHAIN_DAMAGE_DECAY: float = 0.8
const CHAIN_SEARCH_RANGE: float = 150.0
const MISSILE_POISON_DURATION: float = 5.0

# ═══════════════════════════════════════════════
# UI
# ═══════════════════════════════════════════════
const HUD_HEIGHT: float = 52.0
const COMBO_DECAY_TIME: float = 2.0
const SHAKE_DECAY: float = 8.0
const TOOLTIP_MAX_WIDTH: float = 360.0
const TOOLTIP_OFFSET = Vector2(15, 15)

# 血量阈值
const HEALTH_HIGH_THRESHOLD: float = 0.6
const HEALTH_MED_THRESHOLD: float = 0.3
const HEALTH_LOW_THRESHOLD: float = 0.25

# 血条颜色
const HP_COLOR_HIGH = Color(0.22, 0.55, 0.22)
const HP_COLOR_MED = Color(0.65, 0.60, 0.15)
const HP_COLOR_LOW = Color(0.70, 0.15, 0.12)

# ═══════════════════════════════════════════════
# 视觉效果
# ═══════════════════════════════════════════════
const VFX_IMPACT_RING_SEGMENTS: int = 16
const VFX_RING_EXPAND_SCALE = Vector2(2.5, 2.5)
const VFX_RING_EXPAND_DURATION: float = 0.3
const VFX_HIT_PARTICLE_COUNT: int = 5
const VFX_HIT_LIFETIME: float = 0.3
const VFX_POPUP_FLOAT_DISTANCE: float = 40.0
const VFX_POPUP_DURATION: float = 0.8
