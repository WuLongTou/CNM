// 全局数值配置 —— M1 子集，数值从 Godot 版 src/core/GameConfig.cs 逐项对齐。
// 后续里程碑（武器表/天气/数学系统）在此文件继续扩充。

export const CFG = {
  World: { Width: 3840, Height: 2160 },

  Player: {
    MaxHealth: 120,
    BaseDamage: 12.0,
    BaseSpeed: 220.0,
    CollisionSize: 32.0,
    HitKnockback: 26.0,  // 近战/弹道命中击退距离（Boss×0.15 精英×0.4）
    DashSpeedMult: 3.5,
    DashDuration: 0.25,
    DashCooldown: 0.8,
    InvincibleDuration: 0.5,
    CritMultiplier: 2.5,
    BaseAttackCooldown: 0.45,
    BaseAttackRange: 70.0,
    MeleeTargetCount: 1,
  },

  Combo: {
    DecayTime: 2.0,
    DamageBonusPerLevel: 0.05,
    MaxBonus: 0.5,
  },

  // M14 操作与战斗：冲刺斩 / 完美闪避（妖时）
  Combat: {
    DashStrikeDmgMult: 1.5,      // 冲刺中攻击：全武器伤害倍率
    DashStrikeRangeMult: 1.35,   // 冲刺斩近战范围倍率
    DodgeSlowTime: 0.9,          // 完美闪避子弹时间时长
    DodgeSlowFactor: 0.45,       // 子弹时间内敌人/敌弹时间缩放
    FocusTime: 3.0,              // 闪避后增伤窗口
    FocusDmgMult: 1.3,           // 增伤倍率
  },

  Enemy: {
    CollisionSize: 24.0,
    AttackCooldown: 1.0,
    AttackRange: 70.0,
    AttackWindup: 0.35,       // 近战攻击前摇（定身蓄力，可读预警）
    WindupHitRangeMult: 1.15, // 前摇结束时的命中范围倍率（微宽于起手范围）
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
    WorldMinEnemies: 6,
    WorldMaxEnemies: 18,
    SpawnCooldown: 1.5,
    MinDistFromPlayer: 420.0,
  },

  Difficulty: {
    RampInterval: 45.0,
    RoundMultiplierBase: 0.04, // roundMult = 1 + round * base * 2
    SpawnScaleFactor: 1.2,     // 刷怪数值缩放用的 factor（对齐 C# _GetRoundMult(1.2f)）
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

/** 14 种妖物三维表（HP/伤害/速度），对齐 Enemy.TypeStats。 */
export const ENEMY_STATS: Record<number, { hp: number; dmg: number; speed: number }> = {
  0: { hp: 25, dmg: 8, speed: 45.0 },
  1: { hp: 15, dmg: 6, speed: 90.0 },
  2: { hp: 100, dmg: 18, speed: 22.0 },
  3: { hp: 20, dmg: 12, speed: 55.0 },
  4: { hp: 30, dmg: 10, speed: 55.0 },
  5: { hp: 35, dmg: 8, speed: 35.0 },
  6: { hp: 45, dmg: 5, speed: 30.0 },
  7: { hp: 40, dmg: 5, speed: 28.0 },
  8: { hp: 20, dmg: 10, speed: 30.0 },
  9: { hp: 40, dmg: 12, speed: 55.0 },
  10: { hp: 80, dmg: 20, speed: 35.0 },
  11: { hp: 55, dmg: 15, speed: 40.0 },
  12: { hp: 60, dmg: 18, speed: 65.0 },
  13: { hp: 12, dmg: 5, speed: 85.0 },
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
