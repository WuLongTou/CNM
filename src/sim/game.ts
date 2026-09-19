// 游戏主模拟（sim 层核心）：纯逻辑、零 DOM/渲染依赖。
// 由 app 层以固定 60Hz 步长驱动 tick()；vitest 可直接实例化跑逻辑回归。
// 战斗与武器规则对齐 Godot 版 GameManager/Player/PlayerCombat/Enemy/WeaponFactory。

import { Rng } from "./rng";
import {
  CFG, ENEMY_NAMES, ENEMY_SCORE, ENEMY_STATS, SPAWN_TABLE, EnemyType,
} from "./config";
import { defaultSave, type SaveData, type SaveStorage } from "./save";
import {
  WEAPON_DATA, stageMult, stageTag, UPGRADE_POOL, UPGRADE_STEP,
  FUSION_RECIPES,
  type AttackType, type UpgradeStat, type WeaponInstance,
} from "./weapons";
import { Inventory, type Material } from "./inventory";
import { RECIPES } from "./recipes";
import { WeatherSystem, type WeatherType } from "./weather";
import { AchievementSystem } from "./achievements";
import { World } from "./world";
import { HitStop, HITSTOP } from "./hitstop";

export type GameState = "WeaponSelection" | "Combat" | "GameOver";

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
  /** 蛟：冲撞冷却 / 蓄力剩余 / 冲刺剩余 / 锁定方向 / 本次冲刺是否已命中。 */
  chargeCd: number; chargeWind: number; chargeDash: number;
  chargeVx: number; chargeVy: number; chargeHit: boolean;
  /** 妖王：环形弹幕冷却秒。 */
  barrageCd: number;
  /** 妖王：已放出的弹幕轮次（起始角按黄金角轮转 → 连续轮呈螺旋漂移）。 */
  barrageVolley: number;
  /** 近战攻击前摇剩余秒（>0 = 蓄力定身，可读预警）。 */
  attackWind: number;
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
  moving: boolean;
  dead: boolean;
  weapons: WeaponInstance[];
}

/** 由武器栏聚合出的战斗属性（RecalculateStats 语义）。 */
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
}

/** 升级符箓拾取物：击杀掉落，走近触发升级三选一。 */
export interface UpgradePickup {
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
/** 升级符箓掉落概率（普通/精英/妖王；妖王固定掉 3 枚）。 */
const UPGRADE_DROP = { normal: 0.035, elite: 0.3, boss: 1 } as const;
/** 升级词条中文名（报价卡展示用）。 */
export const UPGRADE_STAT_NAMES: Record<UpgradeStat, string> = { dmg: "伤害", asp: "攻速", rng: "范围" };
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
/** 蛟冲撞：冷却 / 蓄力 / 冲刺时长 / 冲刺速度倍数 / 接触伤害倍数。 */
const DRAGON = { cd: 5, windup: 0.5, dash: 0.45, speedMult: 6, dmgMult: 1.5 };
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
  /** 祭坛融合报价（打开时模态暂停，choose 指令应答）。 */
  fusionOffer: { a: string; b: string; resultName: string } | null = null;
  /** 场上升级符箓拾取物（走近拾取）。 */
  pickups: UpgradePickup[] = [];
  /** 升级三选一报价（打开时模态暂停，choose/resolveUpgrade 应答）。 */
  upgradeOffers: UpgradeOffer[] | null = null;
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
  /** 攻击钩子（音频层订阅；主动武器类型）。 */
  onAttack: Array<(type: AttackType) => void> = [];
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
  private bossSpawnedForRound = 0;
  /** 已出场的妖王总数（BOSS_ROSTER 轮换索引）。 */
  private bossSpawnCount = 0;
  private events: SimEvent[] = [];
  private listeners: Array<(e: SimEvent) => void> = [];
  /** 存档中的跨局累计击杀基线（本局 kills 之外的存量）。 */
  private baseTotalKills = 0;

  constructor(seed = 1, save: SaveStorage = { load: () => null, save: () => {} }) {
    this.seed = seed;
    this.rng = new Rng(seed);
    this.save = save;
    const c = CFG.Player;
    const prev = save.load();
    // 永久升级：血 +20/级、伤害 +10%/级（对齐 SaveManager 数值）
    const maxHp = c.MaxHealth + (prev?.metaHp ?? 0) * 20;
    const baseDamage = c.BaseDamage * (1 + (prev?.metaDmg ?? 0) * 0.1);
    // 跨局累计击杀基线（成就「累计击杀」用；死亡结算时存档累加）
    this.baseTotalKills = prev?.totalKills ?? 0;
    this.player = {
      x: this.world.w / 2, y: this.world.h / 2,
      hp: maxHp, maxHp,
      speed: c.BaseSpeed, baseDamage,
      attackCd: 0, dashTimer: 0, dashCd: 0, invincible: 0,
      combo: 0, comboTimer: 0, facing: 1, moving: false, dead: false,
      weapons: [],
    };
    this.terrain = new World(this.rng, this.world, () => this.nextId++);
    this.weather = new WeatherSystem(this.rng);
    this.achievements = new AchievementSystem(prev?.achievements ?? []);
    this.achievements.onUnlock.push((a) => {
      this.emit("achievement", { id: a.id, name: a.name, desc: a.desc });
      this.persistAchievements();
    });
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

  /** 聚合武器栏 → 战斗属性（对齐 RecalculateStats：主动武器给全部特性，其余给被动乘数）。 */
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
      if (i < ws.length - 1) {
        // 被动携带：各乘数小幅叠加（EffectCatalog 叠加语义的简化）
        s.dmgMult *= 1 + ((def.dmg ?? 1) * mult - 1) * 0.1;
        s.aspdMult *= 1 + ((def.asp ?? 1) * mult - 1) * 0.1;
      } else {
        s.dmgMult *= (def.dmg ?? 1) * mult;
        s.aspdMult *= (def.asp ?? 1) * mult;
        s.rangeMult *= (def.rng ?? 1) * mult;
        s.crit += def.crit ?? 0;
        s.hpCost += Math.abs(def.hp ?? 0);
        s.multi = Math.max(s.multi, def.multi ?? 1);
        s.splash = Math.max(s.splash, def.splash ?? 0);
        s.psn = Math.max(s.psn, def.psn ?? 0);
        s.freeze = Math.max(s.freeze, def.freeze ?? 0);
        s.chain = Math.max(s.chain, def.chain ?? 0);
        s.pierce = Math.max(s.pierce, def.pierce ?? 0);
        s.spread = Math.max(s.spread, def.spread ?? 0);
        s.homing = def.homing ?? false;
      }
      s.dmgMult *= 1 + w.bonus.dmg;
      s.aspdMult *= 1 + w.bonus.asp;
      s.rangeMult *= 1 + w.bonus.rng;
    });
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
    const w: WeaponInstance = { defId: id, name: def.name, type: def.type, ups: 0, bonus: { dmg: 0, asp: 0, rng: 0 } };
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

  /** 应用升级卡：随机/指定词条 +1 级，跨阈值触发进阶/终极事件。 */
  applyUpgrade(index: number, stat?: UpgradeStat): Record<string, unknown> {
    const w = this.player.weapons[index];
    if (!w) throw new Error("武器不存在: " + index);
    const before = stageTag(w);
    const st = stat ?? this.rng.pick(UPGRADE_POOL);
    w.ups++;
    w.bonus[st] += UPGRADE_STEP[st];
    this.stats = this.recalc();
    const after = stageTag(w);
    if (after !== before) {
      this.emit(after === "ultimate" ? "weapon_ultimate" : "weapon_ascended", { name: w.name, stage: after });
    }
    return { name: w.name, ups: w.ups, stat: st, stage: after };
  }

  // ── 升级符箓（击杀掉落 → 走近拾取 → 三选一） ─────────────

  private updatePickups(): void {
    // 报价/融合模态期间不拾取，站在符箓上等待自然排队；
    // 徒手时不拾取（无武器可升级，符箓留在地上不浪费）
    if (this.upgradeOffers || this.fusionOffer || this.player.weapons.length === 0) return;
    const p = this.player;
    if (p.dead) return;
    for (let i = this.pickups.length - 1; i >= 0; i--) {
      const u = this.pickups[i];
      if ((u.x - p.x) ** 2 + (u.y - p.y) ** 2 <= PICKUP_RANGE ** 2) {
        this.pickups.splice(i, 1);
        this.openUpgradeOffers();
        return;
      }
    }
  }

  /** 生成三选一报价（武器×词条去重；模态暂停，choose target:"upgrade" 应答）。 */
  private openUpgradeOffers(): void {
    const ws = this.player.weapons;
    if (ws.length === 0) return;
    const pool: UpgradeOffer[] = [];
    const seen = new Set<string>();
    let guard = 0;
    while (pool.length < Math.min(3, ws.length * UPGRADE_POOL.length) && guard++ < 60) {
      const weaponIdx = this.rng.int(0, ws.length - 1);
      const stat = this.rng.pick(UPGRADE_POOL);
      const key = `${weaponIdx}:${stat}`;
      if (seen.has(key)) continue;
      seen.add(key);
      pool.push({ weaponIdx, weaponName: ws[weaponIdx].name, stat, statName: UPGRADE_STAT_NAMES[stat] });
    }
    if (pool.length === 0) return;
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

  // ── 宝箱 ────────────────────────────────────────────────

  private scatterChests(n: number): void {
    for (let i = 0; i < n && this.chests.length < 8; i++) {
      const p = this.resolveSpawnPos("random");
      this.chests.push({ id: this.nextId++, x: p.x, y: p.y, opened: false });
    }
  }

  private updateChests(): void {
    const p = this.player;
    if (p.dead) return;
    for (const c of this.chests) {
      if (!c.opened && (c.x - p.x) ** 2 + (c.y - p.y) ** 2 <= CHEST_OPEN_RANGE ** 2) {
        c.opened = true;
        const w = this.giveWeapon();
        this.emit("chest_opened", { pos: { x: c.x, y: c.y }, weapon: w.name });
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
    this.updateAltars();
    this.maintainEnemies(dt);
    this.weather.update(dt);
    // 天气切换事件（HUD 标签/通知/表现层共用）
    if (this.weather.state.type !== this.lastWeatherType) {
      this.lastWeatherType = this.weather.state.type;
      this.emit("weather_changed", { type: this.weather.state.type, intensity: this.weather.state.intensity });
    }
    this.achievements.update({
      kills: this.kills, survivalTime: this.survivalTime,
      bossKills: this.bossKills, totalKills: this.baseTotalKills + this.kills,
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

  /** 两件持有武器可组成的融合报价（匹配 FUSION_RECIPES；否则任取两件合成占位）。 */
  private makeFusionOffer(): { a: string; b: string; resultName: string } | null {
    const ws = this.player.weapons;
    if (ws.length < 2) return null;
    for (const r of FUSION_RECIPES) {
      const hasA = ws.some((w) => w.name === r.a);
      const hasB = ws.some((w) => w.name === r.b);
      if (hasA && hasB) {
        return { a: r.a, b: r.b, resultName: WEAPON_DATA[r.result].name };
      }
    }
    const a = ws[ws.length - 2].name, b = ws[ws.length - 1].name;
    return { a, b, resultName: WEAPON_DATA[this.rollWeaponDef()].name };
  }

  /** 应答融合：accept 消耗两件换新武器；reject 丢弃报价。 */
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
    const w = this.giveWeapon();
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

  /** 购买永久升级（GameOver 结算界面）：血/伤各一级，价格 50 金币 × 现等级。 */
  buyMeta(kind: "hp" | "dmg"): Record<string, unknown> {
    const prev = this.save.load() ?? defaultSave();
    const level = kind === "hp" ? prev.metaHp : prev.metaDmg;
    const price = 50 * (level + 1);
    if (prev.coins < price) return { bought: false, reason: "金币不足" };
    const next: SaveData = {
      ...prev,
      coins: prev.coins - price,
      metaHp: prev.metaHp + (kind === "hp" ? 1 : 0),
      metaDmg: prev.metaDmg + (kind === "dmg" ? 1 : 0),
    };
    this.save.save(next);
    return { bought: true, kind, level: level + 1, coins: next.coins };
  }

  private advanceRound(): void {
    this.round++;
    this.emit("threat_tier", { tier: this.round });
    if (this.round % CFG.Enemy.BossInterval === 0 && this.bossSpawnedForRound < this.round) {
      this.bossSpawnedForRound = this.round;
      this.spawnEnemy({ type: BOSS_ROSTER[this.bossSpawnCount % BOSS_ROSTER.length], boss: true, near: "player" });
      this.bossSpawnCount++;
    }
    this.scatterChests(1);
  }

  /** 难度倍率，对齐 GameManager._GetRoundMult：1 + round * 0.04 * 2 * factor。 */
  roundMult(factor = 1): number {
    return 1 + this.round * CFG.Difficulty.RoundMultiplierBase * 2 * factor;
  }

  private updatePlayer(dt: number): void {
    const p = this.player;
    const c = CFG.Player;
    if (p.dead) return;

    if (this.input.dash && p.dashCd <= 0) {
      p.dashTimer = c.DashDuration;
      p.dashCd = c.DashCooldown;
      this.emit("player_dash", { x: p.x, y: p.y });
    }
    this.input.dash = false;
    this.input.cycleTarget = false;

    let vx = this.input.moveX;
    let vy = this.input.moveY;
    const len = Math.hypot(vx, vy);
    if (len > 1) { vx /= len; vy /= len; }
    const mult = p.dashTimer > 0 ? c.DashSpeedMult : 1;
    const w = this.weather.state.playerSpeedMult;
    p.x += vx * p.speed * mult * w * dt;
    p.y += vy * p.speed * mult * w * dt;
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

    if (this.input.attack && p.attackCd <= 0) {
      this.performAttack();
    }
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

  /** 执行一次攻击：按主动武器类型分流；挥空也进冷却；血代价不足则无法攻击。
   *  冲刺中攻击 = 冲刺斩：伤害 1.5×，按武器类型附加强化（范围/矢数/弹数/溅射）。 */
  private performAttack(): void {
    const p = this.player;
    const c = CFG.Player;
    if (p.hp <= this.stats.hpCost) return; // _CanPayHealthCost：不能一击自杀
    p.attackCd = c.BaseAttackCooldown / Math.max(0.1, this.stats.aspdMult);
    const type = this.attackType;
    const dashing = p.dashTimer > 0;
    const strike = dashing ? CFG.Combat.DashStrikeDmgMult : 1;
    const range = c.BaseAttackRange * this.stats.rangeMult * (dashing ? CFG.Combat.DashStrikeRangeMult : 1);
    for (const cb of this.onAttack) cb(type);
    if (dashing) for (const cb of this.onDashStrike) cb(p.x, p.y);

    if (type === "Melee") {
      const targets = this.nearestEnemies(p.x, p.y, range, this.stats.multi + (dashing ? 1 : 0));
      for (const e of targets) {
        const r = this.rollDamage();
        if (dashing) r.dmg *= strike;
        const d = Math.hypot(e.x - p.x, e.y - p.y) || 1;
        this.hitEnemy(e, r, (e.x - p.x) / d, (e.y - p.y) / d);
      }
      this.harvestInRange(range);
    } else if (type === "Ranged") {
      const target = this.nearestEnemies(p.x, p.y, 600 * this.stats.rangeMult, 1)[0];
      const baseAng = target ? Math.atan2(target.y - p.y, target.x - p.x) : (p.facing > 0 ? 0 : Math.PI);
      const n = Math.max(1, this.stats.spread) + (dashing ? 1 : 0);
      for (let i = 0; i < n; i++) {
        const ang = baseAng + (i - (n - 1) / 2) * 0.21; // ±12° 散射
        this.fireProjectile(ang, 500, range * 1.2, strike);
      }
    } else if (type === "Magic") {
      const target = this.nearestEnemies(p.x, p.y, 600 * this.stats.rangeMult, 1)[0];
      const ang = target ? Math.atan2(target.y - p.y, target.x - p.x) : (p.facing > 0 ? 0 : Math.PI);
      this.fireProjectile(ang, 300, 5, strike);
      if (dashing) this.fireProjectile(ang + 0.35, 300, 5, strike);
    } else {
      // Mine：原地布雷（FIFO 上限）；冲刺斩溅射 1.5×
      if (this.mines.length >= MAX_MINES) this.mines.shift();
      this.mines.push({
        id: this.nextId++, x: p.x, y: p.y,
        dmg: this.rollDamage().dmg * strike,
        splash: Math.max(60, this.stats.splash) * (dashing ? strike : 1),
        psn: this.stats.psn,
        arm: 0.4,
      });
    }
    if (this.stats.hpCost > 0) p.hp = Math.max(1, p.hp - this.stats.hpCost);
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

  private rollDamage(): { dmg: number; crit: boolean } {
    const p = this.player;
    let dmg = p.baseDamage * this.stats.dmgMult;
    // 连击：每层 +5% 上限 +50%
    const comboBonus = Math.min(p.combo * CFG.Combo.DamageBonusPerLevel, CFG.Combo.MaxBonus);
    dmg *= 1 + comboBonus;
    // 完美闪避后的集中窗口：伤害 1.3×
    if (this.focusTimer > 0) dmg *= CFG.Combat.FocusDmgMult;
    const crit = this.rng.chance(this.stats.crit / 100);
    if (crit) dmg *= CFG.Player.CritMultiplier;
    return { dmg, crit };
  }

  private fireProjectile(ang: number, speed: number, life: number, dmgMult = 1): void {
    this.projectiles.push({
      id: this.nextId++,
      x: this.player.x, y: this.player.y,
      vx: Math.cos(ang) * speed, vy: Math.sin(ang) * speed,
      dmg: this.rollDamage().dmg * dmgMult,
      life, pierce: this.stats.pierce, hitIds: new Set(),
      homing: this.stats.homing || this.attackType === "Magic",
      splash: this.stats.splash, psn: this.stats.psn, freeze: this.stats.freeze, chain: this.stats.chain,
    });
  }

  private hitEnemy(e: Enemy, r: { dmg: number; crit: boolean }, ux = 0, uy = 0): void {
    const p = this.player;
    if (ux !== 0 || uy !== 0) this.knockback(e, ux, uy);
    this.hitStop.trigger(HITSTOP.HitMs);
    p.combo++;
    p.comboTimer = CFG.Combo.DecayTime;
    // 连击里程碑音效节点（表现层订阅；3 连提示音，避免每层都响）
    if (p.combo === 3) this.emit("combo_milestone", { count: 3 });
    this.notifyDamage(e.x, e.y, r.dmg, r.crit);
    if (r.crit) this.hitStop.trigger(HITSTOP.CritMs);
    this.applyHit(e.x, e.y, e, r.dmg, { splash: this.stats.splash, psn: this.stats.psn, freeze: this.stats.freeze });
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
          let diff = want - cur;
          while (diff > Math.PI) diff -= Math.PI * 2;
          while (diff < -Math.PI) diff += Math.PI * 2;
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
          this.applyHit(pr.x, pr.y, e, pr.dmg, { splash: pr.splash, psn: pr.psn, freeze: pr.freeze });
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

  private applyHit(x: number, y: number, e: Enemy, dmg: number, fx: { splash: number; psn: number; freeze: number }): void {
    // 甲妖举盾：完全免疫伤害与异常状态
    if (e.type === EnemyType.Knight && e.blockT > 0) return;
    const dx = e.x - x, dy = e.y - y;
    const dd = Math.hypot(dx, dy) || 1;
    this.knockback(e, dx / dd, dy / dd);
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

  /** 命中击退：沿指定方向推开（Boss×0.15 / 精英×0.4），地形分离防嵌墙。 */
  private knockback(e: Enemy, ux: number, uy: number, mult = 1): void {
    const kb = CFG.Player.HitKnockback * mult * (e.boss ? 0.15 : e.elite ? 0.4 : 1);
    const nx = Math.min(this.world.w - 22, Math.max(22, e.x + ux * kb));
    const ny = Math.min(this.world.h - 22, Math.max(22, e.y + uy * kb));
    if (e.type !== EnemyType.Ship) {
      const sep = this.terrain.separate(nx, ny, CFG.Enemy.CollisionSize / 2);
      e.x = sep.x; e.y = sep.y;
    } else {
      e.x = nx; e.y = ny; // 浮灯无视地形
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
    // 击杀凝固（对齐 HitStop*Ms）：普通 70 / 精英 110 / 妖王 220
    this.hitStop.trigger(e.boss ? HITSTOP.BossKillMs : e.elite ? HITSTOP.EliteKillMs : HITSTOP.KillMs);
    for (const cb of this.onKill) cb(e.type, e.elite, e.boss);
    let pts = ENEMY_SCORE[e.type] ?? 10;
    if (e.elite) pts = Math.floor(pts * CFG.Enemy.EliteScoreMult);
    if (e.boss) pts = Math.floor(pts * CFG.Enemy.BossScoreMult);
    this.score += pts;
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
  }

  private updateEnemies(dt: number): void {
    const p = this.player;
    for (const e of this.enemies) {
      e.hitFlash = Math.max(0, e.hitFlash - dt);
      e.attackCd = Math.max(0, e.attackCd - dt);

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
      const weatherMult = this.weather.state.enemySpeedMult;
      const speedMult = (e.frzT > 0 ? 0.4 : 1) * weatherMult; // FreezeSlowMult × 天气

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
          if (dist <= CFG.Enemy.AttackRange * CFG.Enemy.WindupHitRangeMult) {
            if (!this.tryPerfectDodge() && p.invincible <= 0) {
              p.hp -= e.damage;
              p.invincible = CFG.Player.InvincibleDuration;
              for (const cb of this.onPlayerHurt) cb(p.hp);
              if (p.hp <= 0) this.onPlayerDied();
            }
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
      if (!this.tryPerfectDodge()) {
        if (p.invincible <= 0) {
          p.hp -= Math.ceil(e.damage * EXPLODER.dmgMult);
          p.invincible = CFG.Player.InvincibleDuration;
          for (const cb of this.onPlayerHurt) cb(p.hp);
          if (p.hp <= 0) this.onPlayerDied();
        }
      }
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
    this.enemyProjectiles.push({
      id: this.nextId++,
      x: e.x, y: e.y,
      vx, vy,
      dmg: Math.max(1, Math.ceil(e.damage * dmgMult)),
      life: 3,
      psn,
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
        if (this.tryPerfectDodge()) continue; // 子弹时间中擦身而过，弹不解体
        if (p.invincible <= 0) {
          p.hp -= pr.dmg;
          p.invincible = CFG.Player.InvincibleDuration;
          for (const cb of this.onPlayerHurt) cb(p.hp);
          if (p.hp <= 0) this.onPlayerDied();
        }
        continue;
      }
      keep.push(pr);
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
        if (this.tryPerfectDodge()) return true; // 闪避冲撞：不受伤，蛟继续冲
        if (p.invincible <= 0) {
          p.hp -= Math.ceil(e.damage * DRAGON.dmgMult);
          p.invincible = CFG.Player.InvincibleDuration;
          for (const cb of this.onPlayerHurt) cb(p.hp);
          if (p.hp <= 0) this.onPlayerDied();
        }
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
    const prev = this.save.load() ?? defaultSave();
    const next: SaveData = {
      highScore: Math.max(prev.highScore, this.score),
      totalKills: prev.totalKills + this.kills,
      games: prev.games + 1,
      coins: prev.coins + Math.floor(this.score / 50) + this.kills,
      metaHp: prev.metaHp,
      metaDmg: prev.metaDmg,
      achievements: [...this.achievements.unlocked],
    };
    this.save.save(next);
    this.emit("game_over", this.snapshot());
  }

  /** 持续探索刷怪：世界妖物数维持 [6,18]，冷却 1.5s。 */
  private maintainEnemies(dt: number): void {
    this.spawnCd = Math.max(0, this.spawnCd - dt);
    if (this.enemies.length >= CFG.Spawning.WorldMinEnemies) return;
    if (this.enemies.length >= CFG.Spawning.WorldMaxEnemies) return;
    if (this.spawnCd > 0) return;
    this.spawnCd = CFG.Spawning.SpawnCooldown;
    this.spawnEnemy({ type: "random", near: "player" });
  }

  /** 刷怪：距玩家 ≥420px 环形随机（对齐 EnemySpawnMinDistFromPlayer）。 */
  spawnEnemy(opts: SpawnOpts = {}): number {
    const count = Math.min(Math.max(opts.count ?? 1, 1), 50);
    let spawned = 0;
    for (let i = 0; i < count; i++) {
      if (this.enemies.length >= CFG.Spawning.WorldMaxEnemies && !opts.boss) break;
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
    let hp = Math.ceil(stats.hp * mult);
    let damage = Math.ceil(stats.dmg * mult);
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
      chargeCd: DRAGON.cd * 0.6,
      chargeWind: 0, chargeDash: 0,
      chargeVx: 0, chargeVy: 0, chargeHit: false,
      barrageCd: boss ? BOSS_BARRAGE.first : 0,
      barrageVolley: 0,
      attackWind: 0,
    };
    if (e.affix === "swift") e.speed = speed * 1.5; // EliteSwiftMult
    if (e.affix === "shield") e.shieldHp = 25;
    this.enemies.push(e);
    return e;
  }

  private pickAffix(): string {
    return this.rng.pick(["swift", "regen", "shield", "explosive", "frenzy"] as const);
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
        // target:"upgrade"：index=三选一序号（对齐 UpgradePanel 1/2/3 快捷键）
        if (args.target === "upgrade") {
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
      case "buy_meta": return this.buyMeta(args.kind === "dmg" ? "dmg" : "hp");
      case "meta": {
        const d = this.save.load() ?? defaultSave();
        return {
          coins: d.coins, metaHp: d.metaHp, metaDmg: d.metaDmg,
          highScore: d.highScore, totalKills: d.totalKills, games: d.games,
        };
      }
      case "inventory": return { counts: { ...this.inventory.counts } };
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
          elite: e.elite, boss: e.boss,
        })),
      },
      chests: this.chests.filter((c) => !c.opened).map((c) => ({ x: c.x, y: c.y })),
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
