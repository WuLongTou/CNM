// 全局数值配置 —— M1 子集，数值从 Godot 版 src/core/GameConfig.cs 逐项对齐。
// 后续里程碑（武器表/天气/数学系统）在此文件继续扩充。

export const CFG = {
  // R176 手感 pass 1：世界缩 75%（视口密度 ×1.8——此前 8.3M px² 摊 29 只，
  // 视口 810K px² 只见 3-8 只，「妖潮」无潮感）
  World: { Width: 2880, Height: 1620 },

  Player: {
    MaxHealth: 120,
    BaseDamage: 12.0,
    BaseSpeed: 220.0,
    CollisionSize: 32.0,
    HitKnockback: 30.0,  // 命中击退总位移 px（速度脉冲+指数摩擦；Boss×0.15 精英×0.4）
    DashSpeedMult: 3.5,
    DashDuration: 0.25,
    DashCooldown: 0.8,
    InvincibleDuration: 0.5,
    CritMultiplier: 2.5,
    BaseAttackCooldown: 0.40,
    BaseAttackRange: 92.0,
    MeleeTargetCount: 2,
  },

  Combo: {
    DecayTime: 2.0,
    DamageBonusPerLevel: 0.05,
    MaxBonus: 0.5,
  },

  // R65 局内金币与香火龛：金币为局内流通币（击杀入账），香火龛定期浮现可消费
  Shop: {
    FirstDelaySec: 75, // 首龛浮现时间
    IntervalSec: 90, // 后续补龛间隔（场上无龛才补）
    Range: 46, // 贴近触发半径
    KillGold: 1, // 普通妖金币
    EliteGold: 5, // 精英金币
    BossGold: 20, // 妖王金币
    BossGoldRush: 30, // R75 Boss Rush 妖王金币（6 王 180 金全槽可达）
    RushFirstDelaySec: 45, // R75 Boss Rush 首龛时刻（纯妖王局节奏快一倍）
    RushIntervalSec: 60, // R75 Boss Rush 补龛间隔
    GoblinGold: 15, // 宝妖金币
    PriceWeapon: 80, // 武器槽基价（稀有度≥2）
    PriceUpgrade: 60, // 符箓槽基价
    PriceHeal: 40, // 调息槽基价
    HealAmount: 40, // 调息回复量
    PriceBuff: 120, // 香火祝福槽基价·普通档（R66；R89 稀有档 ×2.5）
    PriceReroll: 20, // R78 刷新报价基价（随通胀倍乘）
    BuffDmgBonus: 0.15, // 香火祝福·普通：本局全局伤害加成（R66，每局一次）
    BuffAspBonus: 0.12, // 香火祝福·普通：本局攻速加成（R72 三系化，各限一次）
    BuffSpdBonus: 0.08, // 香火祝福·普通：本局移速加成（R72 三系化，各限一次）
    BuffRareMult: 2, // R89 稀有档：效果 ×2、价格 ×2.5（约 40% 出率）
    BuffRarePriceMult: 2.5,
    EscalateMult: 1.35, // 每次购买后全场基价倍乘（通胀逼取舍）
  },

  // M14 操作与战斗：冲刺斩 / 完美闪避（妖时）
  Combat: {
    DashStrikeDmgMult: 1.5,      // 冲刺中攻击：全武器伤害倍率
    DashStrikeRangeMult: 1.35,   // 冲刺斩近战范围倍率
    DodgeSlowTime: 0.9,          // 完美闪避子弹时间时长
    DodgeSlowFactor: 0.45,       // 子弹时间内敌人/敌弹时间缩放
    FocusTime: 3.0,              // 闪避后增伤窗口
    FocusDmgMult: 1.3,           // 增伤倍率
    // R1 方向性近战：攻击方向 = 移动方向（自动吸附锥内最近妖物），扇形判定替代 360° 索敌
    SlashHalfAngle: 0.96,        // 近战扇形判定半角 rad（≈55°，与挥砍弧视觉一致）
    AimCone: 1.05,               // 攻击方向吸附锥半角 rad（≈60°，锥内自动瞄向最近妖物）
    LungeSpeed: 460.0,           // 出手突进速度 px/s（攻击带位移的重量感）
    LungeTime: 0.07,             // 突进时长 s（总位移 ≈32px）
    KnockbackDecay: 12.0,        // 击退速度指数摩擦 1/s（总位移 = v0/decay）
  },

  Enemy: {
    CollisionSize: 24.0,
    AttackCooldown: 1.0,
    AttackRange: 70.0,
    AttackWindup: 0.35,       // 近战攻击前摇（定身蓄力，可读预警）
    WindupHitRangeMult: 1.15, // 前摇结束时的命中范围倍率（微宽于起手范围）
    StaggerTime: 0.16,        // 受击硬直 s（精英×0.4，妖王免疫；打断蓄力/冲撞）
    SeparationRange: 26.0,
    SeparationForce: 150.0,
    EliteChanceBase: 0.02,
    EliteChancePerRound: 0.01,
    EliteChanceMax: 0.18,
    EliteStatMult: 1.25,
    EliteSpeedMult: 1.05,
    EliteScoreMult: 1.5,
    BossStatMult: 3.0,
    BossSpeedMult: 0.9,
    BossScoreMult: 5.0,
    BossScale: 1.8,
    BossInterval: 5,
  },

  Spawning: {
    MinDistFromPlayer: 380.0,
  },

  // R2 分幕结构：每幕 ActTime 秒，密度/刷怪速率/精英潮分段爬升；终幕妖王击杀通关
  Acts: {
    ActTime: 150,                          // 每幕时长 s
    MaxEnemies: [24, 48, 66, 84],          // 各幕场上妖物上限（密度的爽感来源）
    MinEnemies: [8, 20, 30, 40],           // 各幕底噪下限
    SpawnCooldown: [1.15, 0.7, 0.5, 0.4],  // 各幕底噪刷怪间隔 s
    DamageMult: [0.6, 0.85, 1.0, 1.1],     // R187 Act1 温和热身：妖物伤害按幕爬升
    EliteWaveCount: [0, 2, 3, 5],          // 各幕每轮(45s)追加精英潮数量
    FinalBossRound: 13,                    // 终幕妖王出现轮（≈585s）
  },

  Difficulty: {
    RampInterval: 45.0,
    RoundMultiplierBase: 0.04, // roundMult = 1 + round * base * 2
    SpawnScaleFactor: 1.2,     // 刷怪数值缩放用的 factor（对齐 C# _GetRoundMult(1.2f)）
  },

  // R13 妖潮异变：周期性突发事件（对标 Hades 房间事件 / VS 怪潮惊喜）——
  // 紧张（围猎/精锐）与释放（宝藏）轮转，保持长局的新鲜感
  Events: {
    FirstAt: 100,   // 首发时刻（秒，Combat 计时）
    Interval: 130,  // 后续间隔（秒）
    SurgeBase: 6,   // 围猎基数（+ 2×幕数）
    SurgeRadius: 500, // 围猎环半径 px
    CacheCount: 2,  // 宝藏显形宝箱数
    MarketDurationSec: 25, // R69 庙会开市时长（香龛半价窗口）
    MarketDiscount: 0.5, // R69 庙会折扣（与通胀倍乘叠乘，开龛时定格）
  },

  // R4/R7 升级 agency：三选一的重抽/跳过/放逐（对标 Vampire Survivors 的
  // Reroll/Skip/Banish）；跳过带治疗补偿——残血时「贪升级还是保命」构成真实决策
  Upgrade: {
    RerollsPerRun: 2,  // 每局重抽次数
    SkipHeal: 15,      // 跳过即时回复生命（不超上限）
    BanishesPerRun: 1, // 每局放逐次数（R7：移除一对“武器×词条”组合，本局不再出现）
  },
} as const;

/** 妖物类型（序号与 C# EnemyType 枚举一致，SpawnTable 用数字索引）。 */
export const EnemyType = {
  Basic: 0,
  Fast: 1,
  Tank: 2,
  Exploder: 3,
  Invisible: 4,
  Spitter: 5,
  Healer: 6,
  Summoner: 7,
  Archer: 8,
  Ship: 9,
  Mech: 10,
  Knight: 11,
  Dragon: 12,
  Swarm: 13,
} as const;
export type EnemyType = (typeof EnemyType)[keyof typeof EnemyType];

export const ENEMY_NAMES: Record<number, string> = {
  0: "游魂", 1: "魅影", 2: "石魅", 3: "爆魈", 4: "隐煞", 5: "瘴妖", 6: "愈妖",
  7: "唤灵", 8: "弩妖", 9: "浮灯", 10: "铁傀", 11: "甲妖", 12: "蛟", 13: "蚀群",
};

/** 14 种妖物三维表（HP/伤害/速度）。R176 手感 pass：速度 ×1.25——
 *  叠加阶数爬坡（1 + 阶×3.5%，13 阶 ×1.455）：首分钟温和、后期妖潮。 */
export const ENEMY_STATS: Record<number, { hp: number; dmg: number; speed: number }> = {
  0: { hp: 25, dmg: 8, speed: 56.0 },
  1: { hp: 15, dmg: 6, speed: 113.0 },
  2: { hp: 100, dmg: 18, speed: 28.0 },
  3: { hp: 20, dmg: 12, speed: 69.0 },
  4: { hp: 30, dmg: 10, speed: 69.0 },
  5: { hp: 35, dmg: 8, speed: 44.0 },
  6: { hp: 45, dmg: 5, speed: 38.0 },
  7: { hp: 40, dmg: 5, speed: 35.0 },
  8: { hp: 20, dmg: 10, speed: 38.0 },
  9: { hp: 40, dmg: 12, speed: 69.0 },
  10: { hp: 80, dmg: 20, speed: 44.0 },
  11: { hp: 55, dmg: 15, speed: 50.0 },
  12: { hp: 60, dmg: 18, speed: 81.0 },
  13: { hp: 12, dmg: 5, speed: 106.0 },
};

/** 击杀分数表，对齐 GameManager.TypeScore。 */
export const ENEMY_SCORE: Record<number, number> = {
  0: 10, 1: 15, 2: 25, 3: 30, 4: 35, 5: 40,
  6: 50, 7: 60, 8: 45, 9: 55, 10: 65, 11: 50, 12: 70, 13: 20,
};

/** 出兵权重表（按回合解锁），对齐 GameManager.SpawnTable。 */
export const SPAWN_TABLE: Array<{ maxRound: number; weights: Record<number, number> }> = [
  { maxRound: 2, weights: { 0: 1.0 } },
  { maxRound: 4, weights: { 0: 0.75, 1: 0.25 } },
  { maxRound: 6, weights: { 0: 0.45, 1: 0.3, 2: 0.15, 3: 0.1 } },
  { maxRound: 10, weights: { 0: 0.25, 1: 0.2, 2: 0.2, 3: 0.15, 4: 0.1, 5: 0.1 } },
  { maxRound: 14, weights: { 0: 0.08, 1: 0.1, 2: 0.18, 3: 0.12, 4: 0.1, 5: 0.1, 6: 0.08, 7: 0.06, 8: 0.08 } },
  { maxRound: 18, weights: { 0: 0.05, 1: 0.08, 2: 0.12, 3: 0.1, 4: 0.08, 5: 0.1, 6: 0.08, 7: 0.07, 8: 0.08, 9: 0.08, 10: 0.05, 11: 0.05 } },
  { maxRound: 9999, weights: { 0: 0.04, 1: 0.06, 2: 0.1, 3: 0.08, 4: 0.07, 5: 0.08, 6: 0.07, 7: 0.06, 8: 0.07, 9: 0.1, 10: 0.07, 11: 0.06, 12: 0.08, 13: 0.06 } },
];
