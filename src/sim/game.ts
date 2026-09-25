// 游戏主模拟（sim 层核心）：纯逻辑、零 DOM/渲染依赖。
// 由 app 层以固定 60Hz 步长驱动 tick()；vitest 可直接实例化跑逻辑回归。
// 战斗与武器规则对齐 Godot 版 GameManager/Player/PlayerCombat/Enemy/WeaponFactory。

import { Rng } from "./rng";
import {
  CFG, ENEMY_NAMES, ENEMY_SCORE, ENEMY_STATS, SPAWN_TABLE, EnemyType,
} from "./config";
import { defaultSave, type SaveData, type SaveStorage } from "./save";
import {
  WEAPON_DATA, stageMult, stageTag, UPGRADE_POOL, UPGRADE_STEP, validStats, bonusOf, makeWeapon,
  FUSION_RECIPES,
  type AttackType, type UpgradeStat, type WeaponInstance,
} from "./weapons";
import { Inventory, type Material } from "./inventory";
import { RECIPES } from "./recipes";
import { WeatherSystem, type WeatherType } from "./weather";
import { ACHIEVEMENTS, AchievementSystem } from "./achievements";
import { DEFAULT_DIFFICULTY, DIFFICULTIES, getDifficulty, type DifficultyDef, type DifficultyId } from "./difficulty";
import { World } from "./world";
import { HitStop, HITSTOP } from "./hitstop";
import { DEFAULT_ROLE, getRole, ROLES, type RoleDef } from "./roles";

export type GameState = "WeaponSelection" | "Combat" | "Victory" | "GameOver";

export interface InputState {
  moveX: number;
  moveY: number;
  attack: boolean;
  /** 单帧脉冲：tick 消费后自动清零（对齐 PlayerInput 的 QueueExternalDash）。 */
  dash: boolean;
  cycleTarget: boolean;
}

export interface Enemy {
  id: number;
  type: EnemyType;
  x: number; y: number;
  hp: number; maxHp: number;
  damage: number; speed: number;
  elite: boolean; boss: boolean;
  /** 终幕妖王（最终 Boss，击杀通关）。 */
  final: boolean;
  affix: string;
  /** Boss 阶段（0-2，血量 66%/33% 切换）。 */
  bossPhase: number;
  shieldHp: number;
  attackCd: number;
  hitFlash: number;
  facing: number;
  /** 中毒（dps / 剩余秒）。 */
  psnDps: number; psnT: number; psnTick: number;
  /** 冰缓剩余秒（速度 ×0.4）。 */
  frzT: number;
  /** 隐煞：当前隐身中 / 隐身-显形周期计时秒。 */
  stealth: boolean; stealthT: number;
  /** 愈妖治疗冷却秒。 */
  healCd: number;
  /** 唤灵召唤冷却秒。 */
  summonCd: number;
  /** 浮灯：横向漂浮摆动相位。 */
  floatPhase: number;
  /** 甲妖：举盾剩余秒 / 举盾冷却秒。 */
  blockT: number; blockCd: number;
  /** 疾影词缀：闪现冷却秒（R17）。 */
  blinkCd: number;
  /** 蛟：冲撞冷却 / 蓄力剩余 / 冲刺剩余 / 锁定方向 / 本次冲刺是否已命中。 */
  chargeCd: number; chargeWind: number; chargeDash: number;
  chargeVx: number; chargeVy: number; chargeHit: boolean;
  /** 妖王：环形弹幕冷却秒。 */
  barrageCd: number;
  /** 妖王：已放出的弹幕轮次（起始角按黄金角轮转 → 连续轮呈螺旋漂移）。 */
  barrageVolley: number;
  /** 近战攻击前摇剩余秒（>0 = 蓄力定身，可读预警）。 */
  attackWind: number;
  /** 出手动作剩余秒（扑击/后坐/脉冲，表现层驱动）。 */
  strikeT: number;
  /** 击退速度向量（px/s，指数摩擦衰减；表现"被打得后退"而非传送）。 */
  vx: number; vy: number;
  /** 受击硬直剩余秒（>0 时 AI 挂起；精英×0.4，妖王免疫）。 */
  staggerT: number;
  /** 最近一次被击方向（死亡碎片沿此方向飞溅）。 */
  lastHitUx: number; lastHitUy: number;
  /** 宝妖（R14 Diablo 式 loot goblin）：只逃不打，倒计时结束带宝消失。 */
  goblin: boolean;
  /** 宝妖逃逸倒计时秒。 */
  goblinT: number;
}

/** 敌方弹道（瘴妖毒弹 / 弩妖箭矢）：直线飞行，命中玩家造成伤害。 */
export interface EnemyProjectile {
  id: number;
  x: number; y: number;
  vx: number; vy: number;
  dmg: number;
  life: number;
  /** 毒弹标记（表现层着色）。 */
  psn: boolean;
  /** 死因来源文案（R16；发射时按妖物/弹种写定）。 */
  src: string;
}

export interface Player {
  x: number; y: number;
  hp: number; maxHp: number;
  speed: number;
  baseDamage: number;
  attackCd: number;
  dashTimer: number; dashCd: number;
  invincible: number;
  combo: number; comboTimer: number;
  facing: number;
  /** 瞄准方向 rad（移动输入持续更新；攻击沿此方向扇形判定）。 */
  aimAng: number;
  /** 攻击突进剩余秒与速度向量（出手位移，重量感）。 */
  lungeT: number; lungeVx: number; lungeVy: number;
  moving: boolean;
  dead: boolean;
  weapons: WeaponInstance[];
}

/** 由武器栏聚合出的战斗属性。R2 起"全部装备武器同时出手"，本面板取
 *  最后获得的主武器聚合值，供 HUD/攻击范围圈等显示用途；真实出手参数
 *  在 fireWeapon 中按每把武器独立计算。 */
export interface PlayerStats {
  dmgMult: number;
  aspdMult: number;
  rangeMult: number;
  crit: number;
  hpCost: number;
  multi: number;
  splash: number;
  psn: number;
  freeze: number;
  chain: number;
  pierce: number;
  spread: number;
  homing: boolean;
}

export interface Projectile {
  id: number;
  x: number; y: number;
  vx: number; vy: number;
  dmg: number;
  life: number;
  pierce: number;
  hitIds: Set<number>;
  homing: boolean;
  splash: number;
  psn: number;
  freeze: number;
  chain: number;
}

export interface Mine {
  id: number;
  x: number; y: number;
  dmg: number; splash: number; psn: number;
  arm: number;
}

export interface Chest {
  id: number;
  x: number; y: number;
  opened: boolean;
  /** 精英宝箱（R8）：精英击杀掉落，开出稀有度≥2 武器 + 1 枚符箓。 */
  elite: boolean;
}

/** 升级符箓拾取物：击杀掉落，走近触发升级三选一。 */
export interface UpgradePickup {
  id: number;
  x: number; y: number;
}

/** 回春丹拾取物（R20 Diablo 血球 / VS 炸鸡位）：击杀掉落，走近即时回复生命。 */
export interface HealOrb {
  id: number;
  x: number; y: number;
}

/** 升级三选一报价项（对 WeaponInstance 应用一次升级）。 */
export interface UpgradeOffer {
  weaponIdx: number;
  weaponName: string;
  stat: UpgradeStat;
  statName: string;
}

export interface SimEvent {
  event: string;
  data: Record<string, unknown>;
}

export interface ExecResult {
  id: number;
  ok: boolean;
  data?: Record<string, unknown>;
  error?: string;
}

export interface SpawnOpts {
  count?: number;
  type?: "random" | "basic" | number;
  near?: "player" | "random" | { x: number; y: number };
  elite?: boolean;
  boss?: boolean;
}

const FIXED_DT = 1 / 60;
const MINE_TRIGGER_RANGE = 40;
const CHEST_OPEN_RANGE = 50;
const PICKUP_RANGE = 42;
const MAX_WEAPONS = 6;
/** 两角的有符号最短差（-π..π），扇形判定/追踪转向共用。 */
function angDiff(a: number, b: number): number {
  let d = a - b;
  while (d > Math.PI) d -= Math.PI * 2;
  while (d < -Math.PI) d += Math.PI * 2;
  return d;
}
/** 升级符箓掉落概率（普通/精英/妖王；妖王固定掉 3 枚）。 */
const UPGRADE_DROP = { normal: 0.035, elite: 0.3, boss: 1 } as const;
/** 回春丹掉落（R20）：普通 2% / 精英 10% / 妖王必掉 1 枚；每枚回复量不超上限。 */
const HEAL_DROP = { normal: 0.02, elite: 0.1 } as const;
const HEAL_AMOUNT = 12;
/** 图鉴合并（R15）：跨局累计 + 本局分类型；老存档缺 codex 按空处理。 */
export function mergeCodex(prev: Record<number, number> | undefined, run: Record<number, number>): Record<number, number> {
  const next: Record<number, number> = { ...(prev ?? {}) };
  for (const [k, v] of Object.entries(run)) next[Number(k)] = (next[Number(k)] ?? 0) + v;
  return next;
}
/** 升级词条中文名（报价卡展示用）。 */
export const UPGRADE_STAT_NAMES: Record<UpgradeStat, string> = {
  dmg: "伤害", asp: "攻速", rng: "范围",
  multi: "多重", chain: "连锁", pierce: "穿透",
  psn: "淬毒", freeze: "冰缓", crit: "会心", splash: "溅射",
};
/** 永久升级商店（R5，6 维；对标 Vampire Survivors 跨局成长面板）。
 *  step = 每级增量（hp 为固定生命值，其余为比例/速率）；价格统一 PRICE_BASE×(lv+1)。 */
export type MetaKind = "hp" | "dmg" | "spd" | "mag" | "greed" | "regen";
export type MetaField = "metaHp" | "metaDmg" | "metaSpd" | "metaMag" | "metaGreed" | "metaRegen";
export interface MetaDef { field: MetaField; name: string; desc: string; step: number; }
export const META_SHOP: Record<MetaKind, MetaDef> = {
  hp: { field: "metaHp", name: "体魄", desc: "生命 +20", step: 20 },
  dmg: { field: "metaDmg", name: "杀伐", desc: "伤害 +10%", step: 0.1 },
  spd: { field: "metaSpd", name: "疾风", desc: "移速 +4%", step: 0.04 },
  mag: { field: "metaMag", name: "聚灵", desc: "拾取 +12%", step: 0.12 },
  greed: { field: "metaGreed", name: "点金", desc: "金币 +10%", step: 0.1 },
  regen: { field: "metaRegen", name: "回春", desc: "回复 +0.4/秒", step: 0.4 },
};
export const META_PRICE_BASE = 50;
/** 永久升级价格：50 金币 ×（现等级+1）。 */
export function metaPrice(level: number): number {
  return META_PRICE_BASE * (level + 1);
}
/** 远程妖物（射程内站桩射击，超出继续追击）。 */
const ENEMY_RANGED: Partial<Record<EnemyType, { range: number; cd: number; dmgMult: number; psn?: boolean }>> = {
  [EnemyType.Spitter]: { range: 300, cd: 2.2, dmgMult: 0.8, psn: true },
  [EnemyType.Archer]: { range: 340, cd: 2.0, dmgMult: 0.9 },
};
/** 爆魈自爆触发距离 / 波及半径 / 伤害倍数。 */
const EXPLODER = { trigger: 58, blast: 90, dmgMult: 1.5 };
/** 隐煞隐身/显形周期与强制显形距离。 */
const INVISIBLE = { stealthSec: 2.4, visibleSec: 3.0, revealDist: 110 };
/** 愈妖治疗：半径 / 单次回复比例 / 冷却。 */
const HEALER = { range: 160, ratio: 0.03, cd: 2.5 };
/** 唤灵召唤：冷却 / 每次 2 只 / 全场蚀群上限。 */
const SUMMONER = { cd: 6, count: 2, swarmCap: 12 };
/** 铁傀装甲：常驻受伤减免。 */
const MECH_ARMOR = 0.75;
/** 甲妖举盾：持续 / 冷却（举盾期间完全免伤）。 */
const KNIGHT = { blockSec: 1.2, cd: 5 };
/** 疾影词缀（R17）：闪现周期 / 落点距玩家 / 开局宽限（先露面再闪）。 */
const BLINK = { cd: 4, range: 150, grace: 2.5 };
/** 蛟冲撞：冷却 / 蓄力 / 冲刺时长 / 冲刺速度倍数 / 接触伤害倍数。 */
const DRAGON = { cd: 5, windup: 0.5, dash: 0.45, speedMult: 6, dmgMult: 1.5 };
/** 宝妖（R14）：血量 / 逃逸速度 / 逃逸时限秒 / 击杀符箓数。速度超玩家基础移速（追得上要切角+冲刺）。 */
const GOBLIN = { hp: 30, fleeSpeed: 250, escapeSec: 15, pickupCount: 2 };
/** 妖王环形弹幕：周期向四周放径向弹，阶段越高弹越密（M15）。 */
export const BOSS_BARRAGE = { cd: 4.5, first: 3, dmgMult: 0.7, speed: 280, count: [9, 11, 13] };
/** 妖王种族轮换（M19）：按出场次序循环，各带原生机制与外观——
 *  游魂王(基础) → 石魅王(厚重) → 铁傀王(护甲-25%) → 甲妖王(周期举盾) → 蛟王(蓄力冲撞)。 */
export const BOSS_ROSTER: EnemyType[] = [
  EnemyType.Basic, EnemyType.Tank, EnemyType.Mech, EnemyType.Knight, EnemyType.Dragon,
];
/** 浮灯漂浮：摆动频率（rad/s）/ 垂直于追击方向的摆动速度幅值。 */
const SHIP_FLOAT = { rate: 2.2, sway: 90 };
const MAX_MINES = 6;
const CHAIN_RANGE = 150;        // ChainSearchRange
const POISON_DURATION = 3;      // PoisonDurationDefault
const FREEZE_DURATION = 0.8;    // FreezeDurationDefault

export class GameSim {
  state: GameState = "WeaponSelection";
  round = 1;
  score = 0;
  kills = 0;
  survivalTime = 0;
  /** 模态面板暂停（装备/帮助等打开时由 UI 层置位；tick 冻结）。 */
  paused = false;
  readonly seed: number;
  readonly world = { w: CFG.World.Width, h: CFG.World.Height };
  player: Player;
  enemies: Enemy[] = [];
  projectiles: Projectile[] = [];
  enemyProjectiles: EnemyProjectile[] = [];
  mines: Mine[] = [];
  chests: Chest[] = [];
  inventory = new Inventory();
  terrain: World;
  weather: WeatherSystem;
  achievements: AchievementSystem;
  bossKills = 0;
  /** 本局宝妖讨伐数（R14；成就口径）。 */
  goblinKills = 0;
  /** 本局分类型击杀（R15 图鉴；类型 id → 本局击杀，结算时并入存档 codex）。 */
  killCounts: Record<number, number> = {};
  /** 本局死因（R16；致死伤害来源文案，未阵亡为 null）。 */
  deathCause: string | null = null;
  /** 本局精英讨伐数（R9 战绩复盘；妖王单计 bossKills）。 */
  eliteKills = 0;
  /** 本局最高连击（R9 战绩复盘；命中即刷新，衰减不回落）。 */
  maxCombo = 0;
  /** 祭坛融合报价（打开时模态暂停，choose 指令应答；resultId 保证展示=所得）。 */
  fusionOffer: { a: string; b: string; resultName: string; resultId: string } | null = null;
  /** 场上升级符箓拾取物（走近拾取）。 */
  pickups: UpgradePickup[] = [];
  /** 场上回春丹拾取物（R20；走近即时回复，不开模态）。 */
  healOrbs: HealOrb[] = [];
  /** 升级三选一报价（打开时模态暂停，choose/resolveUpgrade 应答）。 */
  upgradeOffers: UpgradeOffer[] | null = null;
  /** 本局剩余重抽次数（R4；RerollsPerRun 起步，重抽一次消耗一次）。 */
  rerollsLeft = CFG.Upgrade.RerollsPerRun;
  /** 本局剩余放逐次数（R7；BanishesPerRun 起步）。 */
  banishesLeft = CFG.Upgrade.BanishesPerRun;
  /** 本局已放逐组合（R7；键为“武器定义 id:词条”，换武器/位置不影响）。 */
  bannedCombos = new Set<string>();
  /** 上一次天气类型（切换检测，初始 Clear 与 WeatherSystem 一致）。 */
  private lastWeatherType: WeatherType = "Clear";
  /** 命中停顿（打击感）。 */
  hitStop = new HitStop();
  /** 完美闪避子弹时间剩余时长（>0 时敌人/敌弹按 DodgeSlowFactor 慢放）。 */
  slowmoTimer = 0;
  /** 闪避后增伤窗口剩余时长（rollDamage 乘 FocusDmgMult）。 */
  focusTimer = 0;
  /** 伤害数字钩子（表现层订阅；世界坐标 + 是否暴击）。 */
  onDamage: Array<(x: number, y: number, dmg: number, crit: boolean) => void> = [];
  /** 攻击钩子（音频/表现层订阅；主动武器类型 + 本次攻击方向 rad）。 */
  onAttack: Array<(type: AttackType, ang: number) => void> = [];
  /** 击杀钩子。 */
  onKill: Array<(type: EnemyType, elite: boolean, boss: boolean) => void> = [];
  /** 玩家受击钩子。 */
  onPlayerHurt: Array<(hp: number) => void> = [];
  /** 冲刺斩钩子（表现层订阅；触发位置）。 */
  onDashStrike: Array<(x: number, y: number) => void> = [];
  /** 完美闪避钩子（表现层订阅；触发位置）。 */
  onPerfectDodge: Array<(x: number, y: number) => void> = [];
  input: InputState = { moveX: 0, moveY: 0, attack: false, dash: false, cycleTarget: false };
  /** 聚合战斗属性（武器变化时重算）。 */
  stats: PlayerStats;

  private rng: Rng;
  private save: SaveStorage;
  private nextId = 1;
  private spawnCd = 0;
  roundTimer = 0;
  /** 下次妖潮异变倒计时（R13；FirstAt 起步，触发后按 Interval 续杯）。 */
  eventTimer: number = CFG.Events.FirstAt;
  /** 已触发异变次数（轮转序号）。 */
  eventCount = 0;
  private bossSpawnedForRound = 0;
  /** 已出场的妖王总数（BOSS_ROSTER 轮换索引）。 */
  private bossSpawnCount = 0;
  /** 终幕妖王是否已降临（每局一次）。 */
  private finalBossSpawned = false;
  private events: SimEvent[] = [];
  private listeners: Array<(e: SimEvent) => void> = [];
  /** 存档中的跨局累计击杀基线（本局 kills 之外的存量）。 */
  private baseTotalKills = 0;
  /** R5 永久升级快照（构造期自存档读取；拾取/回复挂点用；点金结算时直读存档）。 */
  private metaMag = 0;
  private metaRegen = 0;
  /** 当前职业（R3 开局职业；欢迎页可换，start 后锁定）。 */
  role: RoleDef;
  /** 当前难度（R10；欢迎页可换，start 后锁定）。 */
  difficulty: DifficultyDef;
  /** 存档中的最高通关难度（R10 解锁判定用；-1 = 从未通关）。 */
  private maxDifficulty = -1;
  /** 是否首局（R11 新手教练用；存档局数为 0 即首局，中途 quit 不计局也算首局，无害）。 */
  isFirstRun = true;

  constructor(
    seed = 1,
    save: SaveStorage = { load: () => null, save: () => {} },
    roleId: string = DEFAULT_ROLE,
    difficultyId: number = DEFAULT_DIFFICULTY,
  ) {
    this.seed = seed;
    this.rng = new Rng(seed);
    this.save = save;
    this.role = getRole(roleId);
    this.difficulty = getDifficulty(difficultyId);
    this.maxDifficulty = save.load()?.maxDifficulty ?? -1;
    const c = CFG.Player;
    const prev = save.load();
    // 永久升级：血 +20/级、伤害 +10%/级（对齐 SaveManager 数值）；
    // R5：移速 +4%/级、拾取 +12%/级、金币 +10%/级、回复 +0.4/s/级
    const maxHp = c.MaxHealth + (prev?.metaHp ?? 0) * 20;
    const baseDamage = c.BaseDamage * (1 + (prev?.metaDmg ?? 0) * 0.1);
    const speed = c.BaseSpeed * (1 + (prev?.metaSpd ?? 0) * META_SHOP.spd.step);
    this.metaMag = prev?.metaMag ?? 0;
    this.metaRegen = prev?.metaRegen ?? 0;
    // 跨局累计击杀基线（成就「累计击杀」用；死亡结算时存档累加）
    this.baseTotalKills = prev?.totalKills ?? 0;
    this.isFirstRun = (prev?.games ?? 0) === 0;
    this.player = {
      x: this.world.w / 2, y: this.world.h / 2,
      hp: maxHp, maxHp,
      speed, baseDamage,
      attackCd: 0, dashTimer: 0, dashCd: 0, invincible: 0,
      combo: 0, comboTimer: 0, facing: 1, moving: false, dead: false,
      aimAng: 0, lungeT: 0, lungeVx: 0, lungeVy: 0,
      weapons: [],
    };
    this.terrain = new World(this.rng, this.world, () => this.nextId++);
    this.weather = new WeatherSystem(this.rng);
    this.achievements = new AchievementSystem(prev?.achievements ?? []);
    this.achievements.onUnlock.push((a) => {
      this.emit("achievement", { id: a.id, name: a.name, desc: a.desc });
      this.persistAchievements();
      // R6：被该成就解锁的职业即刻广播（欢迎页下局可选；UI 弹解锁横幅）
      for (const r of Object.values(ROLES)) {
        if (r.unlockAchievement === a.id) {
          this.emit("role_unlocked", { id: r.id, name: r.name });
        }
      }
    });
    // R6：构造指定职业若未解锁直接抛错（协议层返回 ok:false，不静默回退）
    if (!this.isRoleUnlocked(this.role.id)) {
      throw new Error("职业未解锁: " + this.role.name);
    }
    // R10：构造指定难度若未解锁直接抛错
    if (!this.isDifficultyUnlocked(this.difficulty.id)) {
      throw new Error("难度未解锁: " + this.difficulty.name);
    }
    this.stats = this.recalc();
    // 开局武器：职业起手装备（R3；构造期静默入列，不弹获得通知）
    this.player.weapons.push(makeWeapon(this.role.startWeapon));
    this.stats = this.recalc();
    this.scatterChests(3);
  }

  private persistAchievements(): void {
    const prev = this.save.load() ?? defaultSave();
    this.save.save({ ...prev, achievements: [...this.achievements.unlocked] });
  }

  onEvent(cb: (e: SimEvent) => void): void {
    this.listeners.push(cb);
  }

  private emit(event: string, data: Record<string, unknown>): void {
    const e = { event, data };
    this.events.push(e);
    for (const cb of this.listeners) cb(e);
  }

  drainEvents(): SimEvent[] {
    return this.events.splice(0);
  }

  // ── 武器系统 ────────────────────────────────────────────

  get activeWeapon(): WeaponInstance | null {
    const w = this.player.weapons;
    return w.length > 0 ? w[w.length - 1] : null;
  }

  get attackType(): AttackType {
    return this.activeWeapon?.type ?? "Melee";
  }

  /** 聚合主武器（最后获得）→ 面板属性；非主武器只提供 10% 数值支援。 */
  private recalc(): PlayerStats {
    const s: PlayerStats = {
      dmgMult: 1, aspdMult: 1, rangeMult: 1,
      crit: 0, hpCost: 0, multi: 1, splash: 0,
      psn: 0, freeze: 0, chain: 0, pierce: 0, spread: 0, homing: false,
    };
    const ws = this.player.weapons;
    ws.forEach((w, i) => {
      const def = WEAPON_DATA[w.defId];
      const mult = stageMult(w);
      const effDmg = (def.dmg ?? 1) * mult * (1 + bonusOf(w, "dmg"));
      const effAsp = (def.asp ?? 1) * (1 + bonusOf(w, "asp"));
      const effRng = (def.rng ?? 1) * (1 + bonusOf(w, "rng"));
      if (i < ws.length - 1) {
        // 次要武器：小幅支援乘数（齐射时代保留一点携带价值）
        s.dmgMult *= 1 + (effDmg - 1) * 0.1;
        s.aspdMult *= 1 + (effAsp - 1) * 0.1;
      } else {
        s.dmgMult *= effDmg;
        s.aspdMult *= effAsp;
        s.rangeMult *= effRng;
        s.crit = (def.crit ?? 0) + bonusOf(w, "crit");
        s.hpCost += Math.abs(def.hp ?? 0);
        s.multi = Math.max(1, (def.multi ?? 1) + bonusOf(w, "multi"));
        s.splash = (def.splash ?? 0) + bonusOf(w, "splash");
        s.psn = (def.psn ?? 0) + bonusOf(w, "psn");
        s.freeze = (def.freeze ?? 0) + bonusOf(w, "freeze");
        s.chain = (def.chain ?? 0) + bonusOf(w, "chain");
        s.pierce = (def.pierce ?? 0) + bonusOf(w, "pierce");
        s.spread = Math.max(1, (def.multi ?? 1) + bonusOf(w, "multi"));
        s.homing = def.homing ?? false;
      }
    });
    // 职业被动（R3）：面板与 HUD 范围圈显示同步；真实出手数值在 rollDamage/fireWeapon 同乘
    s.dmgMult *= this.role.dmgMult;
    s.aspdMult *= this.role.aspdMult;
    s.rangeMult *= this.role.rangeMult;
    return s;
  }

  /**
   * 获得武器（宝箱/give）：按稀有度加权随机；武器栏满 6 件时替换升级次数最少的非主动武器。
   * 对齐 GrantChestReward 的"随机武器 + 立即生效"。
   */
  giveWeapon(defId?: string): WeaponInstance {
    const id = defId ?? this.rollWeaponDef();
    const def = WEAPON_DATA[id];
    if (!def) throw new Error("未知武器: " + id);
    // 重复获得：不生成重复件，改为既有武器精炼 +1 级（宝箱不开空，保留获得反馈）
    const owned = this.player.weapons.find((w) => w.defId === id);
    if (owned) {
      owned.ups++;
      this.stats = this.recalc();
      this.emit("weapon_gained", { name: def.name, type: def.type });
      return owned;
    }
    const w = makeWeapon(id);
    const ws = this.player.weapons;
    if (ws.length >= MAX_WEAPONS) {
      let victim = 0;
      for (let i = 1; i < ws.length - 1; i++) if (ws[i].ups < ws[victim].ups) victim = i;
      ws.splice(victim, 1);
    }
    ws.push(w);
    this.stats = this.recalc();
    this.emit("weapon_gained", { name: def.name, type: def.type });
    return w;
  }

  private rollWeaponDef(): string {
    const ids = Object.keys(WEAPON_DATA);
    const entries = ids.map((id) => [id, [10, 6, 3, 1][WEAPON_DATA[id].rarity - 1]] as const);
    return this.rng.weighted(entries);
  }

  /** 精英宝箱摇奖（R8）：限定稀有度≥2，同稀有度权重，保证高价值目标时刻。 */
  private rollEliteWeaponDef(): string {
    const ids = Object.keys(WEAPON_DATA).filter((id) => WEAPON_DATA[id].rarity >= 2);
    const entries = ids.map((id) => [id, [6, 3, 1][WEAPON_DATA[id].rarity - 2]] as const);
    return this.rng.weighted(entries);
  }

  /** 职业是否已解锁（R6）：无条件职业恒开放，否则看成就达成。 */
  isRoleUnlocked(id: string): boolean {
    const r = getRole(id);
    return r.unlockAchievement === null || this.achievements.unlocked.has(r.unlockAchievement);
  }

  /** 职业解锁条件文案（R6；已解锁/无条件返回 null）。单事实源：取自成就定义。 */
  roleUnlockHint(id: string): { name: string; desc: string } | null {
    const r = getRole(id);
    if (r.unlockAchievement === null) return null;
    const a = ACHIEVEMENTS.find((x) => x.id === r.unlockAchievement);
    return a ? { name: a.name, desc: a.desc } : null;
  }

  /** 难度是否已开放（R10）：无条件难度恒开放，否则看最高通关。 */
  isDifficultyUnlocked(id: number): boolean {
    const d = getDifficulty(id);
    return d.unlockBy === null || d.unlockBy <= this.maxDifficulty;
  }

  /** 切换难度（R10）：仅开局前（欢迎页）允许；未解锁拒绝。 */
  setDifficulty(id: number): Record<string, unknown> {
    if (this.state !== "WeaponSelection") {
      return { applied: false, note: "仅开局前可选难度", difficulty: this.difficulty.id };
    }
    const next = getDifficulty(id);
    if (!this.isDifficultyUnlocked(id)) {
      return { applied: false, note: `难度未解锁：${next.name}`, difficulty: this.difficulty.id };
    }
    this.difficulty = next;
    this.emit("difficulty_changed", { id: next.id, name: next.name });
    return { applied: true, difficulty: next.id, name: next.name };
  }

  /** 切换职业（R3）：仅开局前（欢迎页）允许；起手武器跟随职业重置。R6 起未解锁拒绝。 */
  setRole(id: string): Record<string, unknown> {
    if (this.state !== "WeaponSelection") {
      return { applied: false, note: "仅开局前可选职业", role: this.role.id };
    }
    const next = getRole(id);
    if (!this.isRoleUnlocked(id)) {
      return { applied: false, note: `职业未解锁：${next.name}`, role: this.role.id };
    }
    this.role = next;
    this.player.weapons = [makeWeapon(this.role.startWeapon)];
    this.stats = this.recalc();
    this.emit("role_changed", { id: this.role.id, name: this.role.name });
    return { applied: true, role: this.role.id, weapon: WEAPON_DATA[this.role.startWeapon].name };
  }

  /** 应用升级卡：随机/指定词条 +1 级，跨阈值触发进阶/终极事件。 */
  applyUpgrade(index: number, stat?: UpgradeStat): Record<string, unknown> {
    const w = this.player.weapons[index];
    if (!w) throw new Error("武器不存在: " + index);
    const before = stageTag(w);
    const st = stat ?? this.rng.pick(UPGRADE_POOL);
    w.ups++;
    w.bonus[st] = bonusOf(w, st) + UPGRADE_STEP[st];
    this.stats = this.recalc();
    const after = stageTag(w);
    if (after !== before) {
      this.emit(after === "ultimate" ? "weapon_ultimate" : "weapon_ascended", { name: w.name, stage: after });
    }
    return { name: w.name, ups: w.ups, stat: st, stage: after };
  }

  // ── 升级符箓（击杀掉落 → 走近拾取 → 三选一） ─────────────

  /** 拾取半径（R20 抽取复用：符箓与回春丹同口径，吃职业/聚灵加成）。 */
  private pickRadius(): number {
    // R5 聚灵：拾取半径随永久等级放大
    return PICKUP_RANGE * this.role.pickMult * (1 + this.metaMag * META_SHOP.mag.step);
  }

  private updatePickups(): void {
    // 报价/融合模态期间不拾取，站在符箓上等待自然排队；
    // 徒手时不拾取（无武器可升级，符箓留在地上不浪费）
    if (this.upgradeOffers || this.fusionOffer || this.player.weapons.length === 0) return;
    const p = this.player;
    if (p.dead) return;
    const pickR = this.pickRadius();
    for (let i = this.pickups.length - 1; i >= 0; i--) {
      const u = this.pickups[i];
      if ((u.x - p.x) ** 2 + (u.y - p.y) ** 2 <= pickR * pickR) {
        this.pickups.splice(i, 1);
        this.openUpgradeOffers();
        return;
      }
    }
  }

  /** 回春丹拾取（R20）：走近即时回复，不开模态（模态中也能吃，无冲突）。 */
  private updateHealOrbs(): void {
    const p = this.player;
    if (p.dead || p.hp >= p.maxHp) return;
    const pickR = this.pickRadius();
    for (let i = this.healOrbs.length - 1; i >= 0; i--) {
      const o = this.healOrbs[i];
      if ((o.x - p.x) ** 2 + (o.y - p.y) ** 2 <= pickR * pickR) {
        this.healOrbs.splice(i, 1);
        const healed = Math.min(HEAL_AMOUNT, p.maxHp - p.hp);
        p.hp += healed;
        this.emit("heal_picked", { heal: healed, hp: p.hp });
      }
    }
  }

  /** 单抽一张报价（R7 抽取：跳过本次报价内已出组合与本局已放逐组合）。 */
  private drawOffer(exclude: Set<string>): UpgradeOffer | null {
    const ws = this.player.weapons;
    if (ws.length === 0) return null;
    for (let t = 0; t < 30; t++) {
      const weaponIdx = this.rng.int(0, ws.length - 1);
      const w = ws[weaponIdx];
      const stats = validStats(w.type);
      const stat = stats[this.rng.int(0, stats.length - 1)];
      const key = `${weaponIdx}:${stat}`;
      if (exclude.has(key)) continue;
      if (this.bannedCombos.has(`${w.defId}:${stat}`)) continue;
      return { weaponIdx, weaponName: w.name, stat, statName: UPGRADE_STAT_NAMES[stat] };
    }
    return null;
  }

  /** 生成三选一报价组合（武器×有效词条去重；行为词条可改变攻击方式——R2 质变成长）。
   *  返回 null 表示无有效组合（武器池空或全被放逐）。 */
  private makeUpgradeOffers(): UpgradeOffer[] | null {
    const ws = this.player.weapons;
    if (ws.length === 0) return null;
    const pool: UpgradeOffer[] = [];
    const seen = new Set<string>();
    let guard = 0;
    const totalCombos = ws.reduce((n, w) => n + validStats(w.type).length, 0);
    while (pool.length < Math.min(3, totalCombos) && guard++ < 60) {
      const o = this.drawOffer(seen);
      if (!o) break;
      seen.add(`${o.weaponIdx}:${o.stat}`);
      pool.push(o);
    }
    return pool.length === 0 ? null : pool;
  }

  private openUpgradeOffers(): void {
    const pool = this.makeUpgradeOffers();
    if (!pool) return;
    this.upgradeOffers = pool;
    this.paused = true;
    this.emit("upgrade_offer", { offers: pool });
  }

  /** 应答升级报价：应用所选词条并解除暂停；index 无效时保持报价打开。 */
  resolveUpgrade(index: number): Record<string, unknown> {
    if (!this.upgradeOffers) return { handled: false };
    const offer = this.upgradeOffers[index];
    if (!offer) return { handled: false, note: "无效选项", offers: this.upgradeOffers };
    this.upgradeOffers = null;
    this.paused = false;
    const r = this.applyUpgrade(offer.weaponIdx, offer.stat);
    this.emit("upgrade_applied", { name: r.name, stat: offer.stat, stage: r.stage });
    return { handled: true, ...r };
  }

  /** 重抽三选一（R4）：消耗一次重抽并重新随机报价；次数用尽或无报价时拒绝。 */
  rerollUpgrade(): Record<string, unknown> {
    if (!this.upgradeOffers) return { handled: false, note: "无进行中的报价" };
    if (this.rerollsLeft <= 0) return { handled: false, note: "重抽已用完", rerollsLeft: 0 };
    this.rerollsLeft--;
    const pool = this.makeUpgradeOffers();
    if (!pool) {
      // 理论不可达（报价开着说明武器池非空）；防御性回滚
      this.rerollsLeft++;
      return { handled: false, note: "无可抽组合" };
    }
    this.upgradeOffers = pool;
    this.emit("upgrade_offer", { offers: pool, rerolled: true });
    return { handled: true, rerollsLeft: this.rerollsLeft, offers: pool };
  }

  /** 放逐三选一中的一张（R7）：该“武器×词条”组合本局不再出现，并重补一张报价；
   *  池空无可补时槽位消失（报价空则直接关闭）。每局限放逐 BanishesPerRun 次。 */
  banishUpgrade(index: number): Record<string, unknown> {
    if (!this.upgradeOffers) return { handled: false, note: "无进行中的报价" };
    if (this.banishesLeft <= 0) return { handled: false, note: "放逐已用完", banishesLeft: 0 };
    const offer = this.upgradeOffers[index];
    if (!offer) return { handled: false, note: "无效选项", offers: this.upgradeOffers };
    const w = this.player.weapons[offer.weaponIdx];
    if (!w) return { handled: false, note: "武器已不在栏中" };
    this.banishesLeft--;
    const bannedKey = `${w.defId}:${offer.stat}`;
    this.bannedCombos.add(bannedKey);
    const exclude = new Set(this.upgradeOffers.map((o) => `${o.weaponIdx}:${o.stat}`));
    const rep = this.drawOffer(exclude);
    if (rep) this.upgradeOffers[index] = rep;
    else this.upgradeOffers.splice(index, 1);
    if (this.upgradeOffers.length === 0) {
      // 防御性关闭（理论仅池空时可达）：等价于无可选升级
      this.upgradeOffers = null;
      this.paused = false;
    } else {
      this.emit("upgrade_offer", {
        offers: this.upgradeOffers, banished: true,
        name: offer.weaponName, stat: offer.stat, statName: offer.statName,
      });
    }
    return { handled: true, banishesLeft: this.banishesLeft, banned: bannedKey, offers: this.upgradeOffers };
  }

  /** 跳过三选一（R4）：放弃本次升级，立即回复 SkipHeal 生命（不超上限）——残血时的真实取舍。 */
  skipUpgrade(): Record<string, unknown> {
    if (!this.upgradeOffers) return { handled: false, note: "无进行中的报价" };
    this.upgradeOffers = null;
    this.paused = false;
    const before = this.player.hp;
    const healed = Math.round(Math.min(CFG.Upgrade.SkipHeal, this.player.maxHp - before));
    this.player.hp = before + healed;
    this.emit("upgrade_skipped", { heal: healed, hp: this.player.hp });
    return { handled: true, healed, hp: this.player.hp };
  }

  // ── 宝箱 ────────────────────────────────────────────────

  private scatterChests(n: number): void {
    for (let i = 0; i < n && this.chests.length < 8; i++) {
      const p = this.resolveSpawnPos("random");
      this.chests.push({ id: this.nextId++, x: p.x, y: p.y, opened: false, elite: false });
    }
  }

  /** 掉宝箱（R8）：先清理已开箱（无存在意义，兼控数组规模），软上限 12 防极端刷屏。 */
  private dropChest(x: number, y: number, elite: boolean): void {
    this.chests = this.chests.filter((c) => !c.opened);
    if (this.chests.length >= 12) return;
    const wx = Math.min(this.world.w - 22, Math.max(22, x));
    const wy = Math.min(this.world.h - 22, Math.max(22, y));
    this.chests.push({ id: this.nextId++, x: wx, y: wy, opened: false, elite });
  }

  private updateChests(): void {
    const p = this.player;
    if (p.dead) return;
    for (const c of this.chests) {
      if (!c.opened && (c.x - p.x) ** 2 + (c.y - p.y) ** 2 <= CHEST_OPEN_RANGE ** 2) {
        c.opened = true;
        // R8 精英箱：高稀有武器 + 1 枚符箓（高价值目标时刻的双重奖励）
        const w = c.elite ? this.giveWeapon(this.rollEliteWeaponDef()) : this.giveWeapon();
        if (c.elite) {
          this.pickups.push({ id: this.nextId++, x: c.x + 24, y: c.y });
        }
        this.emit("chest_opened", { pos: { x: c.x, y: c.y }, weapon: w.name, elite: c.elite });
      }
    }
    if (this.chests.every((c) => c.opened)) this.scatterChests(2);
  }

  // ── 主循环 ──────────────────────────────────────────────

  /** 固定步长推进（app 层累加器保证 60Hz；测试可直接连续调用）。 */
  tick(dt: number = FIXED_DT): void {
    if (this.paused || this.state !== "Combat") {
      // 硬约束（项目 AGENTS.md）：非战斗/暂停态 100% 清空输入，模态绝不漏按键。
      this.input = { moveX: 0, moveY: 0, attack: false, dash: false, cycleTarget: false };
      return;
    }
    this.survivalTime += dt;
    this.roundTimer += dt;
    if (this.roundTimer >= CFG.Difficulty.RampInterval) {
      this.roundTimer -= CFG.Difficulty.RampInterval;
      this.advanceRound();
    }
    this.updatePlayer(dt);
    this.updateProjectiles(dt);
    // 完美闪避子弹时间：敌人与敌弹慢放，玩家保持原速（闪避后反击窗口）
    if (this.slowmoTimer > 0) {
      this.slowmoTimer = Math.max(0, this.slowmoTimer - dt);
      const edt = dt * CFG.Combat.DodgeSlowFactor;
      this.updateEnemyProjectiles(edt);
      this.updateEnemies(edt);
    } else {
      this.updateEnemyProjectiles(dt);
      this.updateEnemies(dt);
    }
    this.updateMines(dt);
    this.updateChests();
    this.updatePickups();
    this.updateHealOrbs();
    this.updateAltars();
    this.maintainEnemies(dt);
    this.updateEvents(dt);
    this.weather.update(dt);
    // 天气切换事件（HUD 标签/通知/表现层共用）
    if (this.weather.state.type !== this.lastWeatherType) {
      this.lastWeatherType = this.weather.state.type;
      this.emit("weather_changed", { type: this.weather.state.type, intensity: this.weather.state.intensity });
    }
    this.achievements.update({
      kills: this.kills, survivalTime: this.survivalTime,
      bossKills: this.bossKills, totalKills: this.baseTotalKills + this.kills,
      // R12 系统成就口径（限额类用“已用”= 起步 - 剩余）
      eliteKills: this.eliteKills, maxCombo: this.maxCombo,
      weaponCount: this.player.weapons.length,
      rerollsUsed: CFG.Upgrade.RerollsPerRun - this.rerollsLeft,
      banishesUsed: CFG.Upgrade.BanishesPerRun - this.banishesLeft,
      goblinKills: this.goblinKills,
    });
  }

  /** 靠近祭坛触发融合报价（每座一次性；模态打开即暂停）。 */
  private updateAltars(): void {
    const p = this.player;
    if (p.dead || this.fusionOffer || p.weapons.length < 1) return;
    for (const altar of this.terrain.altars) {
      if (altar.used) continue;
      if ((altar.x - p.x) ** 2 + (altar.y - p.y) ** 2 <= 60 ** 2) {
        const offer = this.makeFusionOffer();
        if (!offer) { altar.used = true; continue; }
        altar.used = true;
        this.fusionOffer = offer;
        this.paused = true;
        this.emit("fusion_offer", offer);
        return;
      }
    }
  }

  /** 两件持有武器可组成的融合报价（匹配 FUSION_RECIPES；否则任取两件随机产物）。
   *  resultId 与 resultName 同时携带——展示结果与实际所得严格一致。 */
  private makeFusionOffer(): { a: string; b: string; resultName: string; resultId: string } | null {
    const ws = this.player.weapons;
    if (ws.length < 2) return null;
    for (const r of FUSION_RECIPES) {
      const hasA = ws.some((w) => w.name === r.a);
      const hasB = ws.some((w) => w.name === r.b);
      if (hasA && hasB) {
        return { a: r.a, b: r.b, resultName: WEAPON_DATA[r.result].name, resultId: r.result };
      }
    }
    const a = ws[ws.length - 2].name, b = ws[ws.length - 1].name;
    const resultId = this.rollWeaponDef();
    return { a, b, resultName: WEAPON_DATA[resultId].name, resultId };
  }

  /** 应答融合：accept 消耗两件换展示的新武器；reject 丢弃报价。 */
  resolveFusion(accept: boolean): Record<string, unknown> {
    if (!this.fusionOffer) return { handled: false };
    const offer = this.fusionOffer;
    this.fusionOffer = null;
    this.paused = false;
    if (!accept) return { handled: true, accepted: false };
    // 消耗 a/b 各一件（从后往前删避免索引位移）
    for (const name of [offer.a, offer.b]) {
      const ws = this.player.weapons;
      for (let i = ws.length - 1; i >= 0; i--) {
        if (ws[i].name === name) { ws.splice(i, 1); break; }
      }
    }
    const w = this.giveWeapon(offer.resultId);
    this.emit("fusion_done", { result: w.name });
    return { handled: true, accepted: true, result: w.name };
  }

  /** 合成：材料 → 武器/属性效果（对齐 Recipes）。 */
  craft(recipeId: string): Record<string, unknown> {
    const r = RECIPES.find((x) => x.id === recipeId);
    if (!r) throw new Error("未知配方: " + recipeId);
    for (const [m, n] of Object.entries(r.cost) as Array<[Material, number]>) {
      if (this.inventory.counts[m] < n) return { crafted: false, reason: `材料不足：${m}` };
    }
    for (const [m, n] of Object.entries(r.cost) as Array<[Material, number]>) {
      this.inventory.take(m, n);
    }
    if (r.weapon) {
      this.giveWeapon(r.weapon);
    } else if (r.effect === "maxhp") {
      this.player.maxHp += r.amount ?? 0;
      this.player.hp += r.amount ?? 0;
    } else if (r.effect === "heal") {
      this.player.hp = Math.min(this.player.maxHp, this.player.hp + (r.amount ?? 0));
    }
    this.emit("crafted", { name: r.name });
    return { crafted: true, name: r.name };
  }

  /** 购买永久升级（GameOver 结算界面，R5 起 6 维）：价格 50 金币 ×（现等级+1）。 */
  buyMeta(kind: MetaKind): Record<string, unknown> {
    const def = META_SHOP[kind] ?? META_SHOP.hp;
    const prev = this.save.load() ?? defaultSave();
    const level = prev[def.field] ?? 0;
    const price = metaPrice(level);
    if (prev.coins < price) return { bought: false, reason: "金币不足", kind, level, price };
    const next: SaveData = { ...prev, coins: prev.coins - price };
    next[def.field] = level + 1;
    this.save.save(next);
    return { bought: true, kind, level: level + 1, coins: next.coins, price: metaPrice(level + 1) };
  }

  /** 妖潮异变调度（R13 三事件；R14 起四事件轮转：围猎/精锐/宝藏/宝妖），紧张与释放交替。 */
  private updateEvents(dt: number): void {
    this.eventTimer -= dt;
    if (this.eventTimer > 0) return;
    this.eventTimer += CFG.Events.Interval;
    const kinds = ["surge", "hunt", "cache", "goblin"] as const;
    const kind = kinds[this.eventCount % kinds.length];
    this.eventCount++;
    if (kind === "surge") this.eventSurge();
    else if (kind === "hunt") this.eventHunt();
    else if (kind === "cache") this.eventCache();
    else this.eventGoblin();
  }

  /** 宝妖游荡（R14 Diablo 式 loot goblin）：复用魅影外形，只逃不打；
   *  讨伐爆精英箱 + 符箓，15 秒后带宝消失。直调 createEnemy 突破幕上限。 */
  private eventGoblin(): void {
    const p = this.player;
    const ang = this.rng.range(0, Math.PI * 2);
    const x = Math.min(this.world.w - 22, Math.max(22, p.x + Math.cos(ang) * 380));
    const y = Math.min(this.world.h - 22, Math.max(22, p.y + Math.sin(ang) * 380));
    const g = this.createEnemy(EnemyType.Fast, x, y, {});
    g.goblin = true;
    g.goblinT = GOBLIN.escapeSec;
    g.hp = g.maxHp = GOBLIN.hp;
    g.speed = GOBLIN.fleeSpeed;
    g.damage = 0;
    g.affix = "none";
    this.emit("map_event", { kind: "goblin", count: 1 });
  }

  /** 围猎：玩家为圆心环形刷快攻群（数量随幕数爬升的压迫感）。
   *  直调 createEnemy 突破幕上限——潮就是要形成压力（同精英潮/妖王待遇）。 */
  private eventSurge(): void {
    const p = this.player;
    const n = CFG.Events.SurgeBase + this.act * 2;
    const type = this.act >= 2 ? EnemyType.Swarm : EnemyType.Fast;
    for (let i = 0; i < n; i++) {
      const ang = (i / n) * Math.PI * 2;
      const x = Math.min(this.world.w - 22, Math.max(22, p.x + Math.cos(ang) * CFG.Events.SurgeRadius));
      const y = Math.min(this.world.h - 22, Math.max(22, p.y + Math.sin(ang) * CFG.Events.SurgeRadius));
      this.createEnemy(type, x, y, {});
    }
    this.emit("map_event", { kind: "surge", count: n });
  }

  /** 精锐现身：近点刷一只精英（按需投放的 R8 时刻）。 */
  private eventHunt(): void {
    this.spawnEnemy({ type: "random", near: "player", elite: true });
    this.emit("map_event", { kind: "hunt", count: 1 });
  }

  /** 宝藏显形：玩家两侧各显一只普通宝箱（释放时刻，走过去拿）。 */
  private eventCache(): void {
    const p = this.player;
    for (let i = 0; i < CFG.Events.CacheCount; i++) {
      const dx = i === 0 ? -220 : 220;
      this.dropChest(p.x + dx, p.y, false);
    }
    this.emit("map_event", { kind: "cache", count: CFG.Events.CacheCount });
  }

  private advanceRound(): void {
    this.round++;
    this.emit("threat_tier", { tier: this.round });
    // 终幕妖王：round 达到 FinalBossRound 时降临（击杀通关）
    if (this.round >= CFG.Acts.FinalBossRound && !this.finalBossSpawned) {
      this.finalBossSpawned = true;
      this.spawnFinalBoss();
      this.scatterChests(1);
      return;
    }
    if (this.round % CFG.Enemy.BossInterval === 0 && this.bossSpawnedForRound < this.round) {
      this.bossSpawnedForRound = this.round;
      this.spawnEnemy({ type: BOSS_ROSTER[this.bossSpawnCount % BOSS_ROSTER.length], boss: true, near: "player" });
      this.bossSpawnCount++;
    }
    // 精英潮：幕压力的分段跳变（R2 分幕节奏）
    const wave = CFG.Acts.EliteWaveCount[this.act];
    if (wave > 0) {
      this.spawnEnemy({ count: wave, type: "random", near: "player", elite: true });
      this.emit("elite_wave", { count: wave });
    }
    this.scatterChests(1);
  }

  /** 终幕妖王：轮换种族 × 强化倍率，带 final 标记（击杀 → Victory）。 */
  private spawnFinalBoss(): void {
    const type = BOSS_ROSTER[this.bossSpawnCount % BOSS_ROSTER.length];
    this.bossSpawnCount++;
    const pos = this.resolveSpawnPos("player");
    const e = this.createEnemy(type, pos.x, pos.y, { boss: true });
    e.final = true;
    e.hp = Math.ceil(e.hp * 1.6);
    e.maxHp = e.hp;
    e.damage = Math.ceil(e.damage * 1.3);
    this.emit("final_boss", { x: e.x, y: e.y });
  }

  /** 难度倍率，对齐 GameManager._GetRoundMult：1 + round * 0.04 * 2 * factor。 */
  roundMult(factor = 1): number {
    return 1 + this.round * CFG.Difficulty.RoundMultiplierBase * 2 * factor;
  }

  /** 距下一威胁阶秒数（R9 威胁预告；HUD 倒计时用）。 */
  get secsToNextTier(): number {
    return Math.max(0, CFG.Difficulty.RampInterval - this.roundTimer);
  }

  private updatePlayer(dt: number): void {
    const p = this.player;
    const c = CFG.Player;
    if (p.dead) return;
    // R5 回春：永久回复按秒累加（不超上限；暂停/非战斗态 tick 不驱动，天然门控）
    if (this.metaRegen > 0 && p.hp < p.maxHp) {
      p.hp = Math.min(p.maxHp, p.hp + this.metaRegen * META_SHOP.regen.step * dt);
    }

    if (this.input.dash && p.dashCd <= 0) {
      p.dashTimer = c.DashDuration;
      p.dashCd = c.DashCooldown * this.role.dashCdMult;
      this.emit("player_dash", { x: p.x, y: p.y });
    }
    this.input.dash = false;
    this.input.cycleTarget = false;

    let vx = this.input.moveX;
    let vy = this.input.moveY;
    const len = Math.hypot(vx, vy);
    if (len > 1) { vx /= len; vy /= len; }
    // 移动方向即瞄准方向（方向性近战的输入语义：朝哪走朝哪砍）
    if (len > 0.01) p.aimAng = Math.atan2(vy, vx);
    const mult = p.dashTimer > 0 ? c.DashSpeedMult : 1;
    const w = this.weather.state.playerSpeedMult;
    // 攻击突进：出手位移叠加在移动上（地形分离照常约束）
    if (p.lungeT > 0) p.lungeT = Math.max(0, p.lungeT - dt);
    else { p.lungeVx = 0; p.lungeVy = 0; }
    p.x += (vx * p.speed * mult * w + p.lungeVx) * dt;
    p.y += (vy * p.speed * mult * w + p.lungeVy) * dt;
    const sep = this.terrain.separate(p.x, p.y, c.CollisionSize / 2);
    p.x = Math.min(this.world.w - 22, Math.max(22, sep.x));
    p.y = Math.min(this.world.h - 22, Math.max(22, sep.y));
    p.moving = len > 0.01;
    if (vx !== 0) p.facing = vx > 0 ? 1 : -1;

    p.dashTimer = Math.max(0, p.dashTimer - dt);
    p.dashCd = Math.max(0, p.dashCd - dt);
    p.invincible = Math.max(0, p.invincible - dt);
    p.attackCd = Math.max(0, p.attackCd - dt);
    this.focusTimer = Math.max(0, this.focusTimer - dt);

    if (p.comboTimer > 0) {
      p.comboTimer -= dt;
      if (p.comboTimer <= 0) p.combo = 0;
    }

    // R2 齐射：每把武器独立冷却，按住攻击全部同时出手（build 的爽感来源）
    let minCd = Infinity;
    for (const w of p.weapons) {
      w.cd = Math.max(0, w.cd - dt);
      if (this.input.attack && w.cd <= 0) this.fireWeapon(w);
      minCd = Math.min(minCd, w.cd);
    }
    p.attackCd = minCd === Infinity ? 0 : minCd;
  }

  /** 完美闪避：冲刺窗口内被攻击判定命中 → 免伤 + 子弹时间 + 增伤窗口。
   *  返回 true 表示本次伤害已被闪避（调用方跳过掉血）。 */
  private tryPerfectDodge(): boolean {
    const p = this.player;
    if (p.dead || p.dashTimer <= 0) return false;
    this.slowmoTimer = CFG.Combat.DodgeSlowTime;
    this.focusTimer = CFG.Combat.FocusTime;
    this.emit("perfect_dodge", { x: p.x, y: p.y });
    for (const cb of this.onPerfectDodge) cb(p.x, p.y);
    return true;
  }

  /** 妖物名（含阶位前缀；R16 死因陈列用）：妖王×王 / 精英前缀 / 普通直称。 */
  private foeName(e: Enemy): string {
    const base = ENEMY_NAMES[e.type] ?? "妖物";
    if (e.boss) return `${base}王`;
    if (e.elite) return `精英${base}`;
    return base;
  }

  /** 对玩家结算一次伤害（完美闪避/无敌帧统一判定）。返回 true = 实际掉血。
   *  R16：致死时记录死因（结算屏“死于×”）。 */
  private hurtPlayer(dmg: number, cause = "妖潮"): boolean {
    const p = this.player;
    if (p.dead || this.tryPerfectDodge() || p.invincible > 0) return false;
    p.hp -= dmg;
    p.invincible = CFG.Player.InvincibleDuration;
    for (const cb of this.onPlayerHurt) cb(p.hp);
    if (p.hp <= 0) {
      this.deathCause = cause;
      this.onPlayerDied();
    }
    return true;
  }

  /** 单武器出手（R2 齐射）：全部装备武器按各自冷却独立出手；挥空也进冷却；
   *  血代价不足则该武器不出手。冲刺中攻击 = 冲刺斩：伤害 1.5× + 类型强化。
   *  R1 方向性近战：近战方向 = 瞄准方向（吸附锥内自动瞄最近妖物），
   *  扇形判定 + 出手突进；远程/魔法保持自动索敌。 */
  private fireWeapon(w: WeaponInstance): void {
    const p = this.player;
    const def = WEAPON_DATA[w.defId];
    const mult = stageMult(w);
    const effDmg = (def.dmg ?? 1) * mult * (1 + bonusOf(w, "dmg"));
    const effAsp = (def.asp ?? 1) * (1 + bonusOf(w, "asp"));
    const effRng = (def.rng ?? 1) * (1 + bonusOf(w, "rng"));
    const hpCost = Math.abs(def.hp ?? 0);
    if (p.hp <= hpCost) return; // _CanPayHealthCost：不能一击自杀
    w.cd = CFG.Player.BaseAttackCooldown / Math.max(0.2, effAsp * this.role.aspdMult);
    const critC = (def.crit ?? 0) + bonusOf(w, "crit");
    const dashing = p.dashTimer > 0;
    const strike = dashing ? CFG.Combat.DashStrikeDmgMult : 1;
    const range = CFG.Player.BaseAttackRange * effRng * this.role.rangeMult
      * (dashing ? CFG.Combat.DashStrikeRangeMult : 1);
    const multi = Math.max(1, (def.multi ?? 1) + bonusOf(w, "multi"));
    const fxW = {
      splash: (def.splash ?? 0) + bonusOf(w, "splash"),
      psn: (def.psn ?? 0) + bonusOf(w, "psn"),
      freeze: (def.freeze ?? 0) + bonusOf(w, "freeze"),
      chain: (def.chain ?? 0) + bonusOf(w, "chain"),
      pierce: (def.pierce ?? 0) + bonusOf(w, "pierce"),
      homing: (def.homing ?? false) || w.type === "Magic",
    };
    // 攻击方向：瞄准方向为基准；吸附锥（±AimCone）内最近妖物自动修正——走砍/站桩都能命中
    let ang = p.aimAng;
    const aimTarget = this.nearestEnemies(p.x, p.y, 400, 1)[0];
    if (aimTarget && Math.abs(angDiff(Math.atan2(aimTarget.y - p.y, aimTarget.x - p.x), ang)) <= CFG.Combat.AimCone) {
      ang = Math.atan2(aimTarget.y - p.y, aimTarget.x - p.x);
    }
    p.facing = Math.cos(ang) >= 0 ? 1 : -1;
    for (const cb of this.onAttack) cb(w.type, ang);
    if (dashing) for (const cb of this.onDashStrike) cb(p.x, p.y);

    if (w.type === "Melee") {
      // 出手突进：向攻击方向的位移脉冲（重量感，叠加在移动上）
      p.lungeT = CFG.Combat.LungeTime;
      p.lungeVx = Math.cos(ang) * CFG.Combat.LungeSpeed;
      p.lungeVy = Math.sin(ang) * CFG.Combat.LungeSpeed;
      // 扇形判定：range 内且方向偏差 ≤ SlashHalfAngle，按距离取 multi 个
      const half = CFG.Combat.SlashHalfAngle;
      const targets = this.enemies
        .filter((e) => {
          if (e.stealth) return false;
          const dx = e.x - p.x, dy = e.y - p.y;
          if (dx * dx + dy * dy > range * range) return false;
          return Math.abs(angDiff(Math.atan2(dy, dx), ang)) <= half;
        })
        .sort((a, b) => ((a.x - p.x) ** 2 + (a.y - p.y) ** 2) - ((b.x - p.x) ** 2 + (b.y - p.y) ** 2))
        .slice(0, multi + (dashing ? 1 : 0));
      for (const e of targets) {
        const r = this.rollDamage(effDmg, critC);
        if (dashing) r.dmg *= strike;
        const d = Math.hypot(e.x - p.x, e.y - p.y) || 1;
        this.hitEnemy(e, r, (e.x - p.x) / d, (e.y - p.y) / d, fxW);
      }
      this.harvestInRange(range);
    } else if (w.type === "Ranged") {
      const target = this.nearestEnemies(p.x, p.y, 600 * effRng * this.role.rangeMult, 1)[0];
      const baseAng = target ? Math.atan2(target.y - p.y, target.x - p.x) : ang;
      const n = Math.max(1, (def.spread ?? 1) + bonusOf(w, "multi")) + (dashing ? 1 : 0);
      for (let i = 0; i < n; i++) {
        const a2 = baseAng + (i - (n - 1) / 2) * 0.21; // ±12° 散射
        this.fireProjectile(a2, 500, range * 1.2, effDmg, critC, strike, fxW);
      }
    } else if (w.type === "Magic") {
      const target = this.nearestEnemies(p.x, p.y, 600 * effRng * this.role.rangeMult, 1)[0];
      const angM = target ? Math.atan2(target.y - p.y, target.x - p.x) : ang;
      this.fireProjectile(angM, 300, 5, effDmg, critC, strike, fxW);
      if (dashing) this.fireProjectile(angM + 0.35, 300, 5, effDmg, critC, strike, fxW);
    } else {
      // Mine：原地布雷（FIFO 上限）；冲刺斩溅射 1.5×
      if (this.mines.length >= MAX_MINES) this.mines.shift();
      this.mines.push({
        id: this.nextId++, x: p.x, y: p.y,
        dmg: this.rollDamage(effDmg, critC).dmg * strike,
        splash: Math.max(60, fxW.splash) * (dashing ? strike : 1),
        psn: fxW.psn,
        arm: 0.4,
      });
    }
    if (hpCost > 0) p.hp = Math.max(1, p.hp - hpCost);
  }

  /** 近战顺带采集范围内的资源节点（对齐 _HarvestNearbyResources）。 */
  private harvestInRange(range: number): void {
    const p = this.player;
    for (const o of this.terrain.obstacles) {
      if (o.dead || o.hp <= 0) continue;
      if ((o.x - p.x) ** 2 + (o.y - p.y) ** 2 > range * range) continue;
      o.hp--;
      if (o.hp <= 0) {
        o.dead = true;
        const drops: Partial<Record<Material, number>> =
          o.kind === "tree" ? { wood: 2 }
          : o.kind === "rock" ? { stone: 2 }
          : { fiber: 1, berry: 2 };
        for (const [m, n] of Object.entries(drops) as Array<[Material, number]>) {
          this.inventory.add(m, n);
        }
        this.emit("harvested", { kind: o.kind });
      }
    }
  }

  /** 伤害结算：基础伤害 × 武器倍率 × 连击 × 集中窗口，暴击独立判定。 */
  private rollDamage(dmgMult: number, critChance = 0): { dmg: number; crit: boolean } {
    const p = this.player;
    let dmg = p.baseDamage * dmgMult * this.role.dmgMult;
    // 连击：每层 +5% 上限 +50%
    const comboBonus = Math.min(p.combo * CFG.Combo.DamageBonusPerLevel, CFG.Combo.MaxBonus);
    dmg *= 1 + comboBonus;
    // 完美闪避后的集中窗口：伤害 1.3×
    if (this.focusTimer > 0) dmg *= CFG.Combat.FocusDmgMult;
    const crit = this.rng.chance(critChance / 100);
    if (crit) dmg *= CFG.Player.CritMultiplier;
    return { dmg, crit };
  }

  private fireProjectile(
    ang: number, speed: number, life: number,
    dmgMult: number, critChance: number, strike: number,
    fx: { splash: number; psn: number; freeze: number; chain: number; pierce: number; homing: boolean },
  ): void {
    const r = this.rollDamage(dmgMult, critChance);
    this.projectiles.push({
      id: this.nextId++,
      x: this.player.x, y: this.player.y,
      vx: Math.cos(ang) * speed, vy: Math.sin(ang) * speed,
      dmg: r.dmg * strike,
      life, pierce: fx.pierce, hitIds: new Set(),
      homing: fx.homing,
      splash: fx.splash, psn: fx.psn, freeze: fx.freeze, chain: fx.chain,
    });
  }

  private hitEnemy(
    e: Enemy, r: { dmg: number; crit: boolean }, ux = 0, uy = 0,
    fx: { splash: number; psn: number; freeze: number } = { splash: 0, psn: 0, freeze: 0 },
  ): void {
    const p = this.player;
    this.hitStop.trigger(HITSTOP.HitMs);
    p.combo++;
    p.comboTimer = CFG.Combo.DecayTime;
    if (p.combo > this.maxCombo) this.maxCombo = p.combo;
    // 连击里程碑音效节点（表现层订阅；3 连提示音，避免每层都响）
    if (p.combo === 3) this.emit("combo_milestone", { count: 3 });
    this.notifyDamage(e.x, e.y, r.dmg, r.crit);
    if (r.crit) this.hitStop.trigger(HITSTOP.CritMs);
    this.applyHit(e.x, e.y, e, r.dmg, fx, ux, uy);
  }

  /** 伤害数字回调（暴击给更大字号）。 */
  private notifyDamage(x: number, y: number, dmg: number, crit: boolean): void {
    for (const cb of this.onDamage) cb(x, y, Math.round(dmg), crit);
  }

  private updateProjectiles(dt: number): void {
    const alive: Projectile[] = [];
    outer: for (const pr of this.projectiles) {
      pr.life -= dt;
      if (pr.life <= 0) continue;
      // 追踪：向最近妖物转向（对齐 MissileHomingStrength）
      if (pr.homing) {
        const t = this.nearestEnemies(pr.x, pr.y, 300, 1)[0];
        if (t) {
          const want = Math.atan2(t.y - pr.y, t.x - pr.x);
          const cur = Math.atan2(pr.vy, pr.vx);
          const diff = angDiff(want, cur);
          const turn = Math.min(Math.abs(diff), 6 * dt) * Math.sign(diff);
          const sp = Math.hypot(pr.vx, pr.vy);
          pr.vx = Math.cos(cur + turn) * sp;
          pr.vy = Math.sin(cur + turn) * sp;
        }
      }
      pr.x += pr.vx * dt;
      pr.y += pr.vy * dt;

      for (const e of this.enemies) {
        if (e.stealth) continue; // 隐身中不可被弹道命中
        if (pr.hitIds.has(e.id)) continue;
        if ((e.x - pr.x) ** 2 + (e.y - pr.y) ** 2 <= 24 ** 2) {
          pr.hitIds.add(e.id);
          const sp = Math.hypot(pr.vx, pr.vy) || 1;
          this.applyHit(pr.x, pr.y, e, pr.dmg,
            { splash: pr.splash, psn: pr.psn, freeze: pr.freeze }, pr.vx / sp, pr.vy / sp);
          if (pr.chain > 0) this.chainFrom(e, pr.dmg * 0.8, pr.chain, pr.psn, pr.freeze, new Set([e.id]));
          if (pr.pierce > 0) pr.pierce--;
          else continue outer;
        }
      }
      alive.push(pr);
    }
    this.projectiles = alive;
  }

  /** 链式弹射：从命中目标逐个弹向附近其他妖物，伤害衰减 0.8（ChainDamageDecay）。 */
  private chainFrom(src: Enemy, dmg: number, chains: number, psn: number, freeze: number, visited: Set<number>): void {
    let cur = src;
    let d = dmg;
    while (chains-- > 0) {
      const next = this.nearestEnemies(cur.x, cur.y, CHAIN_RANGE, 1, visited)[0];
      if (!next) break;
      visited.add(next.id);
      this.applyHit(next.x, next.y, next, d, { splash: 0, psn, freeze });
      d *= 0.8;
      cur = next;
    }
  }

  private updateMines(dt: number): void {
    const alive: Mine[] = [];
    for (const m of this.mines) {
      m.arm -= dt;
      if (m.arm > 0) { alive.push(m); continue; }
      const trigger = this.enemies.find(
        (e) => (e.x - m.x) ** 2 + (e.y - m.y) ** 2 <= MINE_TRIGGER_RANGE ** 2);
      if (trigger) {
        this.applyHit(m.x, m.y, trigger, m.dmg, { splash: m.splash, psn: m.psn, freeze: 0 });
      } else {
        alive.push(m);
      }
    }
    this.mines = alive;
  }

  private nearestEnemies(x: number, y: number, range: number, n: number, exclude?: Set<number>): Enemy[] {
    const r2 = range * range;
    return this.enemies
      // 隐身中的隐煞不可被玩家武器索敌（近身显形后恢复）
      .filter((e) => !e.stealth && !exclude?.has(e.id) && (e.x - x) ** 2 + (e.y - y) ** 2 <= r2)
      .sort((a, b) => (a.x - x) ** 2 + (a.y - y) ** 2 - ((b.x - x) ** 2 + (b.y - y) ** 2))
      .slice(0, Math.max(1, n));
  }

  /** 命中结算：击退冲击 + 伤害 + 异常。ux/uy 为命中冲击方向（弹道取速度方向，
   *  近战取玩家→妖物方向）；缺省时按命中点→目标方向兜底。 */
  private applyHit(
    x: number, y: number, e: Enemy, dmg: number,
    fx: { splash: number; psn: number; freeze: number },
    ux?: number, uy?: number,
  ): void {
    // 甲妖举盾：完全免疫伤害与异常状态
    if (e.type === EnemyType.Knight && e.blockT > 0) return;
    let ix = ux, iy = uy;
    if (ix === undefined || iy === undefined) {
      const dx = e.x - x, dy = e.y - y;
      const dd = Math.hypot(dx, dy) || 1;
      ix = dx / dd; iy = dy / dd;
    }
    if (ix !== 0 || iy !== 0) this.impact(e, ix, iy);
    this.hitStop.trigger(HITSTOP.HitMs);
    this.dealDamage(e, dmg);
    if (fx.psn > 0 && this.rng.chance(fx.psn / 100)) {
      e.psnDps = 3 + dmg * 0.1;
      e.psnT = POISON_DURATION;
      e.psnTick = 0;
    }
    if (fx.freeze > 0 && this.rng.chance(fx.freeze / 100)) {
      e.frzT = FREEZE_DURATION;
    }
    if (fx.splash > 0) {
      for (const o of this.nearestEnemies(x, y, fx.splash, 99)) {
        if (o.id !== e.id) this.dealDamage(o, dmg * 0.5); // SplashDamageMult
      }
    }
  }

  /** 命中冲击：速度制击退（指数摩擦，总位移 ≈ HitKnockback）+ 受击硬直。
   *  硬直打断攻击蓄力与蛟冲撞（妖王免疫硬直），被打的妖物有真实"反应"。 */
  private impact(e: Enemy, ux: number, uy: number): void {
    const kb = CFG.Player.HitKnockback * (e.boss ? 0.15 : e.elite ? 0.4 : 1);
    e.vx += ux * kb * CFG.Combat.KnockbackDecay;
    e.vy += uy * kb * CFG.Combat.KnockbackDecay;
    e.lastHitUx = ux; e.lastHitUy = uy;
    if (!e.boss) {
      e.staggerT = Math.max(e.staggerT, CFG.Enemy.StaggerTime * (e.elite ? 0.4 : 1));
      if (e.attackWind > 0) e.attackWind = 0;          // 打断近战蓄力
      if (e.chargeWind > 0 || e.chargeDash > 0) {      // 打断蛟冲撞
        e.chargeWind = 0; e.chargeDash = 0; e.chargeHit = false;
      }
    }
  }

  private dealDamage(e: Enemy, damage: number): void {
    // 甲妖举盾：完全免伤（applyHit 侧还负责免异常状态）
    if (e.type === EnemyType.Knight && e.blockT > 0) return;
    // 铁傀装甲：常驻受伤减免（不吃暴击例外，所有来源一致）
    if (e.type === EnemyType.Mech) damage *= MECH_ARMOR;
    if (e.shieldHp > 0) {
      const absorbed = Math.min(e.shieldHp, damage);
      e.shieldHp -= absorbed;
      damage -= absorbed;
    }
    e.hp -= damage;
    e.hitFlash = 0.16;
    if (e.hp <= 0) this.onEnemyKilled(e);
  }

  private onEnemyKilled(e: Enemy): void {
    this.enemies = this.enemies.filter((o) => o.id !== e.id);
    this.kills++;
    if (e.boss) this.bossKills++;
    else if (e.elite) this.eliteKills++;
    this.killCounts[e.type] = (this.killCounts[e.type] ?? 0) + 1;
    // R17 分裂：死亡时爆出 2 只满血蚀群（分裂体为普通怪，不连锁；妖王不分裂）
    if (e.affix === "split" && !e.boss) {
      for (let i = 0; i < 2; i++) {
        this.spawnEnemy({ type: 13, near: { x: e.x + this.rng.range(-40, 40), y: e.y + this.rng.range(-40, 40) } });
      }
    }
    // 击杀凝固（对齐 HitStop*Ms）：普通 70 / 精英 110 / 妖王 220
    this.hitStop.trigger(e.boss ? HITSTOP.BossKillMs : e.elite ? HITSTOP.EliteKillMs : HITSTOP.KillMs);
    for (const cb of this.onKill) cb(e.type, e.elite, e.boss);
    // explosive 词缀：死亡自爆（逼玩家杀完就走位，勿贴脸输出）
    if (e.affix === "explosive") {
      const p = this.player;
      if (!p.dead && Math.hypot(p.x - e.x, p.y - e.y) <= EXPLODER.blast * 0.8) {
        this.hurtPlayer(Math.ceil(e.damage * 0.8), `${this.foeName(e)}自爆`);
      }
      this.emit("enemy_explode", { x: e.x, y: e.y });
    }
    let pts = ENEMY_SCORE[e.type] ?? 10;
    if (e.elite) pts = Math.floor(pts * CFG.Enemy.EliteScoreMult);
    if (e.boss) pts = Math.floor(pts * CFG.Enemy.BossScoreMult);
    this.score += pts;
    // R8 精英掉宝：非妖王精英必掉精英宝箱（高价值目标时刻；妖王保持符箓×3）
    if (e.elite && !e.boss) this.dropChest(e.x, e.y, true);
    // R14 宝妖讨伐：必爆精英箱 + 符箓（追杀的 jackpot）
    if (e.goblin) {
      this.goblinKills++;
      this.dropChest(e.x, e.y, true);
      for (let i = 0; i < GOBLIN.pickupCount; i++) {
        this.pickups.push({
          id: this.nextId++,
          x: e.x + this.rng.range(-30, 30),
          y: e.y + this.rng.range(-30, 30),
        });
      }
      this.emit("goblin_slayed", { x: e.x, y: e.y });
    }
    // 升级符箓掉落：普通低概率 / 精英高概率 / 妖王必掉 3 枚
    const rate = e.boss ? UPGRADE_DROP.boss : e.elite ? UPGRADE_DROP.elite : UPGRADE_DROP.normal;
    if (this.rng.chance(rate)) {
      const n = e.boss ? 3 : 1;
      for (let i = 0; i < n; i++) {
        this.pickups.push({
          id: this.nextId++,
          x: e.x + this.rng.range(-30, 30),
          y: e.y + this.rng.range(-30, 30),
        });
      }
    }
    // R20 回春丹掉落：妖王必掉 1 枚，精英/普通按概率（局内即时续航，不开模态）
    if (e.boss || this.rng.chance(e.elite ? HEAL_DROP.elite : HEAL_DROP.normal)) {
      this.healOrbs.push({
        id: this.nextId++,
        x: e.x + this.rng.range(-30, 30),
        y: e.y + this.rng.range(-30, 30),
      });
    }
    // 终幕妖王陨落 → 通关结算（R2 胜利条件）
    if (e.final) this.onVictory();
  }

  /** 通关结算：金币 1.5× 加成，进入 Victory 态（可继续无尽）。返回金币数。
   *  R10：通关刷新最高通关难度；首次开出下一档即广播解锁。
   *  R12：通关三成就直接 grant（终局后 tick 停摆，走 tick 判定够不着）。 */
  private onVictory(): number {
    this.state = "Victory";
    const prevMax = (this.save.load() ?? defaultSave()).maxDifficulty ?? -1;
    const coins = this.finalizeRun(1.5, this.difficulty.id);
    this.emit("victory", { coins });
    this.achievements.grant("pacify");
    if (this.difficulty.id >= 1) this.achievements.grant("demon_queller");
    if (this.difficulty.id >= 2) this.achievements.grant("warden");
    const next = DIFFICULTIES[(this.difficulty.id + 1) as DifficultyId];
    if (next && next.unlockBy !== null && next.unlockBy > prevMax) {
      this.emit("difficulty_unlocked", { id: next.id, name: next.name });
    }
    return coins;
  }

  /** 局末结算存档（死亡/通关共用）：高分/累计击杀/局数/金币/成就；R5 点金放大金币；
   *  R10 难度放大金币；通关（cleared 非空）刷新最高通关难度，死亡不刷新。 */
  private finalizeRun(coinsMult: number, cleared: DifficultyId | null = null): number {
    const prev = this.save.load() ?? defaultSave();
    const greed = 1 + (prev.metaGreed ?? 0) * META_SHOP.greed.step;
    const coins = Math.floor((this.score / 50 + this.kills) * coinsMult * greed * this.difficulty.coinMult);
    this.save.save({
      highScore: Math.max(prev.highScore, this.score),
      totalKills: prev.totalKills + this.kills,
      games: prev.games + 1,
      coins: prev.coins + coins,
      metaHp: prev.metaHp,
      metaDmg: prev.metaDmg,
      metaSpd: prev.metaSpd ?? 0,
      metaMag: prev.metaMag ?? 0,
      metaGreed: prev.metaGreed ?? 0,
      metaRegen: prev.metaRegen ?? 0,
      maxDifficulty: cleared !== null
        ? Math.max(prev.maxDifficulty ?? -1, cleared)
        : (prev.maxDifficulty ?? -1),
      codex: mergeCodex(prev.codex, this.killCounts),
      achievements: [...this.achievements.unlocked],
    });
    return coins;
  }

  private updateEnemies(dt: number): void {
    const p = this.player;
    for (const e of this.enemies) {
      e.hitFlash = Math.max(0, e.hitFlash - dt);
      e.attackCd = Math.max(0, e.attackCd - dt);
      e.strikeT = Math.max(0, e.strikeT - dt);

      // 毒：1s 一跳
      if (e.psnT > 0) {
        e.psnT -= dt;
        e.psnTick += dt;
        if (e.psnTick >= 1) {
          e.psnTick -= 1;
          this.dealDamage(e, e.psnDps);
          if (e.hp <= 0) continue;
        }
      }
      e.frzT = Math.max(0, e.frzT - dt);

      // 击退位移：速度积分 + 指数摩擦（"被打得后退"有过程，非传送）
      if (e.vx !== 0 || e.vy !== 0) {
        e.x += e.vx * dt;
        e.y += e.vy * dt;
        const dec = Math.exp(-CFG.Combat.KnockbackDecay * dt);
        e.vx *= dec; e.vy *= dec;
        if (e.vx * e.vx + e.vy * e.vy < 25) { e.vx = 0; e.vy = 0; }
        e.x = Math.min(this.world.w - 22, Math.max(22, e.x));
        e.y = Math.min(this.world.h - 22, Math.max(22, e.y));
        if (e.type !== EnemyType.Ship) {
          const sep = this.terrain.separate(e.x, e.y, CFG.Enemy.CollisionSize / 2);
          e.x = sep.x; e.y = sep.y;
        }
      }
      // 受击硬直：AI 挂起（击退位移/通用计时照常结算）——压制走位的反馈窗口
      if (e.staggerT > 0) {
        e.staggerT -= dt;
        continue;
      }

      const weatherMult = this.weather.state.enemySpeedMult;
      let speedMult = (e.frzT > 0 ? 0.4 : 1) * weatherMult; // FreezeSlowMult × 天气
      // frenzy 词缀：低血狂暴（40% 血以下速度 ×1.5）
      if (e.affix === "frenzy" && e.hp < e.maxHp * 0.4) speedMult *= 1.5;

      if (e.affix === "regen" && e.hp < e.maxHp) {
        e.hp = Math.min(e.maxHp, e.hp + e.maxHp * 0.02 * dt);
      }

      // Boss 三阶段：66%/33% 血量切换加速并召唤蚀群（对齐 _HandleBossPhases）
      if (e.boss && e.bossPhase < 2 && e.hp <= e.maxHp * (0.66 - 0.33 * e.bossPhase)) {
        e.bossPhase++;
        e.speed *= 1.2;
        for (let i = 0; i < 2; i++) {
          this.spawnEnemy({ type: 13, near: { x: e.x + this.rng.range(-60, 60), y: e.y + this.rng.range(-60, 60) } });
        }
        this.emit("boss_phase", { phase: e.bossPhase + 1 });
      }

      // 妖王环形弹幕：周期向四周放径向弹，压制走位（完美闪避的考题）。
      // 起始角按黄金角逐轮旋转（连续轮呈螺旋漂移）；狂暴 3 阶追加一圈慢速内环。
      if (e.boss && !e.stealth) {
        e.barrageCd -= dt;
        if (e.barrageCd <= 0) {
          e.barrageCd = BOSS_BARRAGE.cd;
          e.strikeT = 0.14; // 弹幕蓄能脉冲
          const n = BOSS_BARRAGE.count[e.bossPhase];
          const off = e.barrageVolley * 2.39996; // 黄金角 ≈ 137.5°
          e.barrageVolley++;
          for (let i = 0; i < n; i++) {
            this.fireEnemyShot(e, BOSS_BARRAGE.dmgMult, false, off + (i / n) * Math.PI * 2, BOSS_BARRAGE.speed);
          }
          if (e.bossPhase === 2) {
            for (let i = 0; i < n; i++) {
              this.fireEnemyShot(e, BOSS_BARRAGE.dmgMult, false, off + ((i + 0.5) / n) * Math.PI * 2, BOSS_BARRAGE.speed * 0.7);
            }
          }
          this.emit("boss_barrage", { x: e.x, y: e.y, count: e.bossPhase === 2 ? n * 2 : n });
        }
      }

      if (p.dead) continue;
      const dx = p.x - e.x;
      const dy = p.y - e.y;
      const dist = Math.hypot(dx, dy) || 1;

      // 隐煞：隐身/显形周期循环，玩家近身强制显形
      if (e.type === EnemyType.Invisible) {
        e.stealthT -= dt;
        if (e.stealth) {
          if (e.stealthT <= 0 || dist <= INVISIBLE.revealDist) {
            e.stealth = false;
            e.stealthT = INVISIBLE.visibleSec;
          }
        } else if (e.stealthT <= 0 && dist > INVISIBLE.revealDist) {
          e.stealth = true;
          e.stealthT = INVISIBLE.stealthSec;
        }
      }

      // 爆魈：贴近即自爆（自灭不计击杀分、不掉落）
      if (e.type === EnemyType.Exploder && dist <= EXPLODER.trigger) {
        this.explodeEnemy(e);
        continue;
      }

      // 蛟：周期蓄力→锁定方向高速冲撞（蓄力/冲刺期间跳过常规移动与攻击）
      if (e.type === EnemyType.Dragon && this.updateDragonCharge(e, dt, dist)) continue;

      // 甲妖：周期举盾（免伤状态机，不阻断移动）
      if (e.type === EnemyType.Knight) this.updateKnightBlock(e, dt);

      // 疾影词缀（R17）：周期闪现至玩家近点（反风筝；落点在近战范围外，给反应时间）
      if (e.affix === "blink" && !e.goblin) {
        e.blinkCd -= dt;
        if (e.blinkCd <= 0) {
          e.blinkCd = BLINK.cd;
          const ang = Math.atan2(p.y - e.y, p.x - e.x);
          e.x = p.x - Math.cos(ang) * BLINK.range;
          e.y = p.y - Math.sin(ang) * BLINK.range;
          e.x = Math.min(this.world.w - 22, Math.max(22, e.x));
          e.y = Math.min(this.world.h - 22, Math.max(22, e.y));
          const sep = this.terrain.separate(e.x, e.y, CFG.Enemy.CollisionSize / 2);
          e.x = sep.x; e.y = sep.y;
          this.emit("enemy_blink", { x: e.x, y: e.y });
        }
      }

      // 宝妖（R14）：只逃不打；倒计时结束带宝消失（击退/硬直照常生效，打中能减速）
      if (e.goblin) {
        e.goblinT -= dt;
        if (e.goblinT <= 0) {
          this.enemies = this.enemies.filter((o) => o.id !== e.id);
          this.emit("goblin_escape", { x: e.x, y: e.y });
          continue;
        }
        const flee = GOBLIN.fleeSpeed * speedMult;
        e.floatPhase += dt * 3;
        const sw = Math.sin(e.floatPhase) * 40;
        let gx = (-dx / dist) * flee + (-dy / dist) * sw;
        let gy = (-dy / dist) * flee + (dx / dist) * sw;
        for (const o of this.enemies) {
          if (o.id === e.id) continue;
          const ox = e.x - o.x, oy = e.y - o.y;
          const od = Math.hypot(ox, oy);
          if (od > 0.01 && od < CFG.Enemy.SeparationRange) {
            gx += (ox / od) * CFG.Enemy.SeparationForce;
            gy += (oy / od) * CFG.Enemy.SeparationForce;
          }
        }
        e.x += gx * dt;
        e.y += gy * dt;
        e.x = Math.min(this.world.w - 22, Math.max(22, e.x));
        e.y = Math.min(this.world.h - 22, Math.max(22, e.y));
        const sep = this.terrain.separate(e.x, e.y, CFG.Enemy.CollisionSize / 2);
        e.x = sep.x; e.y = sep.y;
        e.facing = dx >= 0 ? -1 : 1; // 脸朝逃跑方向
        continue;
      }

      // 远程妖物（瘴妖/弩妖）：射程内站桩射击，超出射程继续追击
      const ranged = ENEMY_RANGED[e.type];
      let vx = 0, vy = 0;
      if (!ranged || dist > ranged.range) {
        vx = (dx / dist) * e.speed * speedMult;
        vy = (dy / dist) * e.speed * speedMult;
      }
      // 浮灯：叠加垂直于追击方向的蛇形摆动（飘忽轨迹）
      if (e.type === EnemyType.Ship) {
        e.floatPhase += dt * SHIP_FLOAT.rate;
        const sway = Math.sin(e.floatPhase) * SHIP_FLOAT.sway;
        vx += (-dy / dist) * sway;
        vy += (dx / dist) * sway;
      }
      for (const o of this.enemies) {
        if (o.id === e.id) continue;
        const ox = e.x - o.x, oy = e.y - o.y;
        const od = Math.hypot(ox, oy);
        if (od > 0.01 && od < CFG.Enemy.SeparationRange) {
          vx += (ox / od) * CFG.Enemy.SeparationForce;
          vy += (oy / od) * CFG.Enemy.SeparationForce;
        }
      }
      // 蓄力前摇定身（攻击承诺，给玩家可读的出手窗口）
      if (e.attackWind <= 0) {
        e.x += vx * dt;
        e.y += vy * dt;
      }
      // 浮灯漂浮于障碍之上，不受地形挤压
      if (e.type !== EnemyType.Ship) {
        const sep = this.terrain.separate(e.x, e.y, CFG.Enemy.CollisionSize / 2);
        e.x = sep.x; e.y = sep.y;
      }
      e.facing = dx >= 0 ? 1 : -1;

      if (ranged && dist <= ranged.range && e.attackCd <= 0) {
        e.attackCd = ranged.cd;
        e.strikeT = 0.12; // 开火后坐
        this.fireEnemyShot(e, ranged.dmgMult, ranged.psn ?? false);
        continue;
      }

      if (e.type === EnemyType.Healer) this.updateHealer(e, dt);
      if (e.type === EnemyType.Summoner) this.updateSummoner(e, dt);

      // 通用近战两段式（M20 可读性）：进范围先蓄力 0.35s（定身前摇，头顶"！"
      // 预警），蓄完仍在范围（×1.15）才判定命中——走位可躲，冲刺可完美闪避
      if (e.attackWind > 0) {
        e.attackWind -= dt;
        if (e.attackWind <= 0) {
          e.attackCd = CFG.Enemy.AttackCooldown;
          e.strikeT = 0.16; // 扑击动作（扑空也播——出手必有动作语言）

          if (dist <= CFG.Enemy.AttackRange * CFG.Enemy.WindupHitRangeMult) {
            this.hurtPlayer(e.damage, this.foeName(e));
          }
        }
      } else if (!e.stealth && dist <= CFG.Enemy.AttackRange && e.attackCd <= 0) {
        e.attackWind = CFG.Enemy.AttackWindup;
      }
    }
  }

  /** 爆魈自爆：波及半径内玩家受 1.5× 伤害，自身消亡（无击杀分/掉落）。 */
  private explodeEnemy(e: Enemy): void {
    this.enemies = this.enemies.filter((o) => o.id !== e.id);
    const p = this.player;
    if (!p.dead && Math.hypot(p.x - e.x, p.y - e.y) <= EXPLODER.blast) {
      this.hurtPlayer(Math.ceil(e.damage * EXPLODER.dmgMult), `${this.foeName(e)}自爆`);
    }
    this.emit("enemy_explode", { x: e.x, y: e.y });
  }

  /** 敌方弹道：直线飞向玩家当前位置（不追踪）。 */
  private fireEnemyShot(e: Enemy, dmgMult: number, psn: boolean, ang?: number, speed = 340): void {
    const p = this.player;
    let vx: number, vy: number;
    if (ang !== undefined) {
      vx = Math.cos(ang) * speed; vy = Math.sin(ang) * speed;
    } else {
      const dx = p.x - e.x, dy = p.y - e.y;
      const d = Math.hypot(dx, dy) || 1;
      vx = (dx / d) * speed; vy = (dy / d) * speed;
    }
    // R16 死因来源：毒弹 / 妖王弹幕 / 远程箭矢（发射时按妖物写定）
    const foe = this.foeName(e);
    this.enemyProjectiles.push({
      id: this.nextId++,
      x: e.x, y: e.y,
      vx, vy,
      dmg: Math.max(1, Math.ceil(e.damage * dmgMult)),
      life: 3,
      psn,
      src: psn ? `${foe}毒弹` : e.boss ? `${foe}弹幕` : `${foe}箭矢`,
    });
    this.emit("enemy_shot", { x: e.x, y: e.y });
  }

  private updateEnemyProjectiles(dt: number): void {
    const p = this.player;
    const keep: EnemyProjectile[] = [];
    for (const pr of this.enemyProjectiles) {
      pr.x += pr.vx * dt;
      pr.y += pr.vy * dt;
      pr.life -= dt;
      if (pr.life <= 0) continue;
      if (pr.x < 0 || pr.x > this.world.w || pr.y < 0 || pr.y > this.world.h) continue;
      if (!p.dead &&
          Math.hypot(pr.x - p.x, pr.y - p.y) <= CFG.Player.CollisionSize / 2 + 6) {
        this.hurtPlayer(pr.dmg, pr.src); // 闪避/无敌帧在 hurtPlayer 统一判定
        continue;
      }      keep.push(pr);
    }
    this.enemyProjectiles = keep;
  }

  /** 愈妖：周期治疗半径内受伤同伴（含 Boss），无目标时短重试。 */
  private updateHealer(e: Enemy, dt: number): void {
    e.healCd -= dt;
    if (e.healCd > 0) return;
    let healed = false;
    for (const o of this.enemies) {
      if (o.id === e.id || o.hp >= o.maxHp) continue;
      if (Math.hypot(o.x - e.x, o.y - e.y) <= HEALER.range) {
        o.hp = Math.min(o.maxHp, o.hp + o.maxHp * HEALER.ratio);
        healed = true;
      }
    }
    e.healCd = healed ? HEALER.cd : 0.5;
    if (healed) this.emit("enemy_heal", { x: e.x, y: e.y });
  }

  /** 唤灵：周期召唤蚀群，全场蚀群达上限则只走冷却。 */
  private updateSummoner(e: Enemy, dt: number): void {
    e.summonCd -= dt;
    if (e.summonCd > 0) return;
    e.summonCd = SUMMONER.cd;
    if (this.enemies.filter((o) => o.type === EnemyType.Swarm).length >= SUMMONER.swarmCap) return;
    for (let i = 0; i < SUMMONER.count; i++) {
      this.spawnEnemy({
        type: EnemyType.Swarm,
        near: { x: e.x + this.rng.range(-60, 60), y: e.y + this.rng.range(-60, 60) },
      });
    }
    this.emit("enemy_summon", { x: e.x, y: e.y });
  }

  /**
   * 蛟冲撞状态机：中远距离周期蓄力（原地红光提示）→ 锁定玩家方向高速冲刺 →
   * 冲刺中接触玩家造成 1.5× 伤害（每次冲刺至多一次）。返回 true 表示本帧已处理。
   */
  private updateDragonCharge(e: Enemy, dt: number, dist: number): boolean {
    const p = this.player;
    if (e.chargeDash > 0) {
      e.x += e.chargeVx * dt;
      e.y += e.chargeVy * dt;
      e.chargeDash -= dt;
      e.facing = e.chargeVx >= 0 ? 1 : -1;
      if (!e.chargeHit && !p.dead &&
          Math.hypot(p.x - e.x, p.y - e.y) <= CFG.Enemy.CollisionSize / 2 + CFG.Player.CollisionSize / 2) {
        e.chargeHit = true;
        this.hurtPlayer(Math.ceil(e.damage * DRAGON.dmgMult), `${this.foeName(e)}冲撞`);
      }
      if (e.chargeDash <= 0) e.chargeHit = false;
      return true;
    }
    if (e.chargeWind > 0) {
      e.chargeWind -= dt;
      if (e.chargeWind <= 0) {
        const speed = e.speed * DRAGON.speedMult;
        e.chargeVx = ((p.x - e.x) / dist) * speed;
        e.chargeVy = ((p.y - e.y) / dist) * speed;
        e.chargeDash = DRAGON.dash;
        this.emit("enemy_charge", { x: e.x, y: e.y, phase: "dash" });
      }
      return true;
    }
    e.chargeCd -= dt;
    if (e.chargeCd <= 0 && dist > 140 && dist < 640) {
      e.chargeWind = DRAGON.windup;
      e.chargeCd = DRAGON.cd;
      this.emit("enemy_charge", { x: e.x, y: e.y, phase: "windup" });
    }
    return false;
  }

  /** 甲妖举盾：周期进入免伤格挡（dealDamage 侧免疫），举盾期间照常移动。 */
  private updateKnightBlock(e: Enemy, dt: number): void {
    if (e.blockT > 0) {
      e.blockT -= dt;
      return;
    }
    e.blockCd -= dt;
    if (e.blockCd <= 0) {
      e.blockT = KNIGHT.blockSec;
      e.blockCd = KNIGHT.cd;
      this.emit("enemy_block", { x: e.x, y: e.y });
    }
  }

  private onPlayerDied(): void {
    const p = this.player;
    p.hp = 0;
    p.dead = true;
    this.state = "GameOver";
    this.finalizeRun(1);
    this.emit("game_over", this.snapshot());
  }

  /** 当前幕（0-3，R2 分幕结构：密度/刷怪速率/精英潮按幕爬升）。 */
  get act(): number {
    return Math.min(3, Math.floor(this.survivalTime / CFG.Acts.ActTime));
  }

  private actCap(): { max: number; min: number; cd: number } {
    const a = this.act;
    return {
      max: CFG.Acts.MaxEnemies[a],
      min: CFG.Acts.MinEnemies[a],
      cd: CFG.Acts.SpawnCooldown[a],
    };
  }

  /** 怪潮供给：底噪（min 以下快速补）+ 持续供给（向 max 爬升的压迫感）。 */
  private maintainEnemies(dt: number): void {
    this.spawnCd = Math.max(0, this.spawnCd - dt);
    const cap = this.actCap();
    if (this.enemies.length >= cap.max) return;
    if (this.spawnCd > 0) return;
    this.spawnCd = this.enemies.length < cap.min ? 0.15 : cap.cd;
    this.spawnEnemy({ type: "random", near: "player" });
  }

  /** 刷怪：距玩家 ≥420px 环形随机（对齐 EnemySpawnMinDistFromPlayer）。 */
  spawnEnemy(opts: SpawnOpts = {}): number {
    const count = Math.min(Math.max(opts.count ?? 1, 1), 50);
    let spawned = 0;
    for (let i = 0; i < count; i++) {
      // 妖王/精英潮不受幕上限约束（潮就是要形成压力）
      if (this.enemies.length >= this.actCap().max && !opts.boss && !opts.elite) break;
      const type = this.resolveType(opts.type);
      const pos = this.resolveSpawnPos(opts.near ?? "player");
      this.createEnemy(type, pos.x, pos.y, opts);
      spawned++;
    }
    return spawned;
  }

  private resolveType(t: SpawnOpts["type"]): EnemyType {
    if (t === "basic") return 0;
    if (typeof t === "number") return t as EnemyType;
    const entry = SPAWN_TABLE.find((e) => this.round <= e.maxRound) ?? SPAWN_TABLE[SPAWN_TABLE.length - 1];
    const weights = Object.entries(entry.weights).map(([k, w]) => [Number(k), w] as const);
    return this.rng.weighted(weights) as EnemyType;
  }

  private resolveSpawnPos(near: SpawnOpts["near"]): { x: number; y: number } {
    if (near && near !== "player" && near !== "random") {
      return {
        x: Math.min(this.world.w - 22, Math.max(22, near.x)),
        y: Math.min(this.world.h - 22, Math.max(22, near.y)),
      };
    }
    if (near === "random") {
      return { x: this.rng.range(22, this.world.w - 22), y: this.rng.range(22, this.world.h - 22) };
    }
    const p = this.player;
    for (let attempt = 0; attempt < 16; attempt++) {
      const ang = this.rng.range(0, Math.PI * 2);
      const r = this.rng.range(CFG.Spawning.MinDistFromPlayer, CFG.Spawning.MinDistFromPlayer + 180);
      const x = Math.min(this.world.w - 22, Math.max(22, p.x + Math.cos(ang) * r));
      const y = Math.min(this.world.h - 22, Math.max(22, p.y + Math.sin(ang) * r));
      if (Math.hypot(x - p.x, y - p.y) >= CFG.Spawning.MinDistFromPlayer * 0.9) return { x, y };
    }
    return { x: 22, y: 22 };
  }

  private createEnemy(type: EnemyType, x: number, y: number, opts: SpawnOpts): Enemy {
    const stats = ENEMY_STATS[type];
    const mult = this.roundMult(CFG.Difficulty.SpawnScaleFactor);
    // R10 难度：妖物血伤同乘（速度不动，保证走位公平）
    const foe = this.difficulty.foeMult;
    let hp = Math.ceil(stats.hp * mult * foe);
    let damage = Math.ceil(stats.dmg * mult * foe);
    let speed = stats.speed;
    let elite = opts.elite ?? false;
    let boss = opts.boss ?? false;

    if (!elite && !boss) {
      elite = this.rng.chance(Math.min(
        CFG.Enemy.EliteChanceBase + CFG.Enemy.EliteChancePerRound * (this.round - 1)
          + this.weather.state.eliteBonus,
        CFG.Enemy.EliteChanceMax,
      ));
    }
    if (elite) {
      hp = Math.ceil(hp * CFG.Enemy.EliteStatMult);
      damage = Math.ceil(damage * CFG.Enemy.EliteStatMult);
      speed *= CFG.Enemy.EliteSpeedMult;
    }
    if (boss) {
      hp = Math.ceil(hp * CFG.Enemy.BossStatMult * 10);
      damage = Math.ceil(damage * CFG.Enemy.BossStatMult);
      speed *= CFG.Enemy.BossSpeedMult;
      this.emit("boss_spawn", {});
    }

    const e: Enemy = {
      id: this.nextId++,
      type,
      x, y,
      hp, maxHp: hp,
      damage, speed,
      elite, boss,
      final: false,
      affix: elite ? this.pickAffix() : "none",
      bossPhase: 0,
      shieldHp: 0,
      attackCd: this.rng.range(0.2, 0.8),
      hitFlash: 0,
      facing: 1,
      psnDps: 0, psnT: 0, psnTick: 0, frzT: 0,
      // 隐煞开局即隐身潜行，其余类型该字段无语义
      stealth: type === EnemyType.Invisible,
      stealthT: type === EnemyType.Invisible ? INVISIBLE.stealthSec : 0,
      healCd: HEALER.cd * 0.5,
      summonCd: SUMMONER.cd * 0.5,
      floatPhase: this.rng.range(0, Math.PI * 2),
      blockT: 0,
      blockCd: KNIGHT.cd * 0.6,
      blinkCd: BLINK.grace,
      chargeCd: DRAGON.cd * 0.6,
      chargeWind: 0, chargeDash: 0,
      chargeVx: 0, chargeVy: 0, chargeHit: false,
      barrageCd: boss ? BOSS_BARRAGE.first : 0,
      barrageVolley: 0,
      attackWind: 0,
      strikeT: 0,
      vx: 0, vy: 0,
      staggerT: 0,
      lastHitUx: 1, lastHitUy: 0,
      goblin: false, goblinT: 0,
    };
    if (e.affix === "swift") e.speed = speed * 1.5; // EliteSwiftMult
    if (e.affix === "shield") e.shieldHp = 25;
    this.enemies.push(e);
    return e;
  }

  /** 精英词缀池（R17 起 7 种：分裂/疾影新增）。 */
  private pickAffix(): string {
    return this.rng.pick(["swift", "regen", "shield", "explosive", "frenzy", "split", "blink"] as const);
  }

  // ── 调试/AI 指令（JSON 协议，与 Godot 旧版 cmd-port 同构） ──

  exec(cmd: { id?: number; action: string; args?: Record<string, unknown> }): ExecResult {
    const id = cmd.id ?? -1;
    try {
      const data = this.dispatch(cmd.action, cmd.args ?? {});
      return { id, ok: true, data };
    } catch (e) {
      return { id, ok: false, error: e instanceof Error ? e.message : String(e) };
    }
  }

  private dispatch(action: string, args: Record<string, unknown>): Record<string, unknown> {
    switch (action) {
      case "status": return this.status();
      case "game_state": return this.snapshot();
      case "start": return this.start();
      case "input": return this.applyInput(args);
      case "choose": {
        // target:"fusion"：accept=Enter / reject=Delete|Esc（对齐 FusionOfferPanel 快捷键语义）
        if (args.target === "fusion") {
          return this.resolveFusion(args.choice === "accept");
        }
        // target:"upgrade"：index=三选一序号（对齐 UpgradePanel 1/2/3 快捷键）；
        // reroll=true 重抽一次（R4）；skip=true 跳过并回复生命（R4，对齐 R/X 快捷键）；
        // banish=序号则放逐该卡（R7，对齐 4/5/6 快捷键）
        if (args.target === "upgrade") {
          if (args.reroll === true) return this.rerollUpgrade();
          if (args.skip === true) return this.skipUpgrade();
          if (typeof args.banish === "number") return this.banishUpgrade(args.banish);
          return this.resolveUpgrade(typeof args.index === "number" ? args.index : -1);
        }
        return { handled: false, note: "未知 target" };
      }
      case "force_combat": {
        if (this.state === "GameOver") return { combat: false };
        this.state = "Combat";
        return { combat: this.state === "Combat" };
      }
      case "spawn": {
        const opts: SpawnOpts = {
          count: typeof args.count === "number" ? args.count : 1,
          type: (args.type as SpawnOpts["type"]) ?? "random",
          near: (args.near as SpawnOpts["near"]) ?? "player",
          elite: args.elite === true,
          boss: args.boss === true,
        };
        return { spawned: this.spawnEnemy(opts) };
      }
      case "give": {
        const w = this.giveWeapon(typeof args.weapon === "string" ? args.weapon : undefined);
        return { given: 1, weapon: w.name, type: w.type };
      }
      case "upgrade": {
        const idx = typeof args.index === "number" ? args.index : this.player.weapons.length - 1;
        const stat = typeof args.stat === "string" ? (args.stat as UpgradeStat) : undefined;
        return this.applyUpgrade(idx, stat);
      }
      case "craft": return this.craft(typeof args.recipe === "string" ? args.recipe : "");
      case "offer_fusion": {
        // 调试用：强制触发一次融合报价（正常流程由祭坛靠近触发）
        const offer = this.makeFusionOffer();
        if (!offer) return { offered: false, note: "武器不足两件" };
        this.fusionOffer = offer;
        this.paused = true;
        this.emit("fusion_offer", offer);
        return { offered: true, ...offer };
      }
      case "buy_meta": {
        // 未知 kind 回退 hp（保持旧调用兼容）
        const k = typeof args.kind === "string" && args.kind in META_SHOP
          ? (args.kind as MetaKind) : "hp";
        return this.buyMeta(k);
      }
      case "meta": {
        const d = this.save.load() ?? defaultSave();
        const kinds = Object.keys(META_SHOP) as MetaKind[];
        return {
          coins: d.coins, metaHp: d.metaHp, metaDmg: d.metaDmg,
          highScore: d.highScore, totalKills: d.totalKills, games: d.games,
          // R5 商店列表（UI 按钮与自动化测试共用；顶层旧字段保留兼容）
          shop: kinds.map((k) => {
            const def = META_SHOP[k];
            const level = d[def.field] ?? 0;
            return { kind: k, name: def.name, desc: def.desc, level, price: metaPrice(level) };
          }),
        };
      }
      case "inventory": return { counts: { ...this.inventory.counts } };
      case "codex": {
        // R15 图鉴：本局分类型 + 跨局累计（含本局实时合并，老存档缺字段按空处理）
        const prev = this.save.load() ?? defaultSave();
        return { run: { ...this.killCounts }, total: mergeCodex(prev.codex, this.killCounts) };
      }
      case "roles": {
        return {
          current: this.role.id,
          list: Object.values(ROLES).map((r) => ({
            id: r.id, name: r.name, desc: r.desc, passive: r.passive,
            weapon: WEAPON_DATA[r.startWeapon].name,
            unlocked: this.isRoleUnlocked(r.id),
            unlock: this.roleUnlockHint(r.id),
          })),
        };
      }
      case "set_role": return this.setRole(typeof args.role === "string" ? args.role : "");
      case "difficulties": {
        return {
          current: this.difficulty.id,
          list: Object.values(DIFFICULTIES).map((d) => ({
            id: d.id, name: d.name, desc: d.desc,
            unlocked: this.isDifficultyUnlocked(d.id),
            unlockBy: d.unlockBy,
          })),
        };
      }
      case "set_difficulty": {
        const id = typeof args.difficulty === "number" ? args.difficulty : -1;
        return this.setDifficulty(id);
      }
      case "weapons": return {
        active: this.attackType,
        list: this.player.weapons.map((w, i) => ({ index: i, ...w, stage: stageTag(w) })),
      };
      case "teleport": {
        const p = this.player;
        const x = typeof args.x === "number" ? args.x : p.x;
        const y = typeof args.y === "number" ? args.y : p.y;
        p.x = Math.min(this.world.w - 22, Math.max(22, x));
        p.y = Math.min(this.world.h - 22, Math.max(22, y));
        return { teleported: true, pos: { x: p.x, y: p.y } };
      }
      case "continue": {
        // 通关后继续无尽讨伐
        if (this.state !== "Victory") return { continued: false };
        this.state = "Combat";
        return { continued: true };
      }
      case "goto_round": {
        // 调试/E2E：排队跳到指定威胁阶（下一次 tick 触发 advanceRound 的完整逻辑）
        const r = typeof args.round === "number" ? Math.max(1, Math.floor(args.round)) : 1;
        if (r <= this.round) return { round: this.round, queued: false };
        this.round = r - 1;
        this.roundTimer = CFG.Difficulty.RampInterval;
        this.survivalTime = Math.max(this.survivalTime, (r - 1) * CFG.Difficulty.RampInterval);
        return { round: this.round, queued: true };
      }
      case "quit": return { quitting: true };
      default: throw new Error("未知动作: " + action);
    }
  }

  start(): Record<string, unknown> {
    if (this.state === "WeaponSelection") this.state = "Combat";
    return { started: true };
  }

  private applyInput(args: Record<string, unknown>): Record<string, unknown> {
    const i = this.input;
    if (typeof args.move_x === "number") i.moveX = Math.min(1, Math.max(-1, args.move_x));
    if (typeof args.move_y === "number") i.moveY = Math.min(1, Math.max(-1, args.move_y));
    if (typeof args.attack === "boolean") i.attack = args.attack;
    if (args.dash === true) i.dash = true;
    if (args.cycle_target === true) i.cycleTarget = true;
    return { applied: this.state === "Combat" && !this.paused, move: { x: i.moveX, y: i.moveY }, attack: i.attack };
  }

  // ── 观测快照 ────────────────────────────────────────────

  status(): Record<string, unknown> {
    return {
      state: this.state,
      round: this.round,
      score: this.score,
      kills: this.kills,
      enemies_alive: this.enemies.length,
      welcome_visible: this.state === "WeaponSelection",
      player_hp: this.player.hp,
      player_max_hp: this.player.maxHp,
      player_pos: { x: this.player.x, y: this.player.y },
      weapon: this.activeWeapon?.name ?? null,
      role: this.role.id,
      role_name: this.role.name,
      rerolls_left: this.rerollsLeft,
      banishes_left: this.banishesLeft,
      difficulty: this.difficulty.id,
      difficulty_name: this.difficulty.name,
      first_run: this.isFirstRun,
      elite_kills: this.eliteKills,
      max_combo: this.maxCombo,
      secs_to_next_tier: Math.ceil(this.secsToNextTier),
      death_cause: this.deathCause,
      next_event_in: Math.ceil(this.eventTimer),
      act: this.act,
    };
  }

  /** 全量观测快照（GSI 风格），妖物按距离从近到远最多 30 只。 */
  snapshot(): Record<string, unknown> {
    const p = this.player;
    const sorted = [...this.enemies].sort((a, b) =>
      (a.x - p.x) ** 2 + (a.y - p.y) ** 2 - ((b.x - p.x) ** 2 + (b.y - p.y) ** 2));
    return {
      ...this.status(),
      survival_time: this.survivalTime,
      paused: this.paused,
      player: {
        hp: p.hp, max_hp: p.maxHp,
        pos: { x: p.x, y: p.y },
        combo: p.combo,
        can_attack: p.attackCd <= 0,
        dead: p.dead,
        weapons: p.weapons.map((w) => ({ name: w.name, type: w.type, ups: w.ups, stage: stageTag(w) })),
      },
      enemies: {
        total: this.enemies.length,
        list: sorted.slice(0, 30).map((e) => ({
          id: e.id, type: ENEMY_NAMES[e.type] ?? String(e.type), type_id: e.type,
          hp: Math.ceil(e.hp), max_hp: e.maxHp,
          pos: { x: e.x, y: e.y },
          elite: e.elite, boss: e.boss, goblin: e.goblin, affix: e.affix,
        })),
      },
      chests: this.chests.filter((c) => !c.opened).map((c) => ({ x: c.x, y: c.y, elite: c.elite })),
      weather: {
        type: this.weather.state.type,
        intensity: this.weather.state.intensity,
      },
      inventory: { ...this.inventory.counts },
      altars: this.terrain.altars.filter((a) => !a.used).map((a) => ({ x: a.x, y: a.y })),
      boss_phase: this.enemies.find((e) => e.boss)?.bossPhase ?? null,
    };
  }
}
