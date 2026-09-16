// 游戏主模拟（sim 层核心）：纯逻辑、零 DOM/渲染依赖。
// 由 app 层以固定 60Hz 步长驱动 tick()；vitest 可直接实例化跑逻辑回归。
// 战斗与武器规则对齐 Godot 版 GameManager/Player/PlayerCombat/Enemy/WeaponFactory。

import { Rng } from "./rng";
import {
  CFG, ENEMY_NAMES, ENEMY_SCORE, ENEMY_STATS, SPAWN_TABLE,
  type EnemyType,
} from "./config";
import { defaultSave, type SaveData, type SaveStorage } from "./save";
import {
  WEAPON_DATA, stageMult, stageTag, UPGRADE_POOL, UPGRADE_STEP,
  FUSION_RECIPES,
  type AttackType, type UpgradeStat, type WeaponInstance,
} from "./weapons";
import { Inventory, type Material } from "./inventory";
import { RECIPES } from "./recipes";
import { WeatherSystem } from "./weather";
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
const MAX_WEAPONS = 6;
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
  mines: Mine[] = [];
  chests: Chest[] = [];
  inventory = new Inventory();
  terrain: World;
  weather: WeatherSystem;
  achievements: AchievementSystem;
  bossKills = 0;
  /** 祭坛融合报价（打开时模态暂停，choose 指令应答）。 */
  fusionOffer: { a: string; b: string; resultName: string } | null = null;
  /** 命中停顿（打击感）。 */
  hitStop = new HitStop();
  /** 伤害数字钩子（表现层订阅；世界坐标 + 是否暴击）。 */
  onDamage: Array<(x: number, y: number, dmg: number, crit: boolean) => void> = [];
  /** 攻击钩子（音频层订阅；主动武器类型）。 */
  onAttack: Array<(type: AttackType) => void> = [];
  /** 击杀钩子。 */
  onKill: Array<(type: EnemyType, elite: boolean, boss: boolean) => void> = [];
  /** 玩家受击钩子。 */
  onPlayerHurt: Array<(hp: number) => void> = [];
  input: InputState = { moveX: 0, moveY: 0, attack: false, dash: false, cycleTarget: false };
  /** 聚合战斗属性（武器变化时重算）。 */
  stats: PlayerStats;

  private rng: Rng;
  private save: SaveStorage;
  private nextId = 1;
  private spawnCd = 0;
  private roundTimer = 0;
  private bossSpawnedForRound = 0;
  private events: SimEvent[] = [];
  private listeners: Array<(e: SimEvent) => void> = [];

  constructor(seed = 1, save: SaveStorage = { load: () => null, save: () => {} }) {
    this.seed = seed;
    this.rng = new Rng(seed);
    this.save = save;
    const c = CFG.Player;
    const prev = save.load();
    // 永久升级：血 +20/级、伤害 +10%/级（对齐 SaveManager 数值）
    const maxHp = c.MaxHealth + (prev?.metaHp ?? 0) * 20;
    const baseDamage = c.BaseDamage * (1 + (prev?.metaDmg ?? 0) * 0.1);
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
    this.updateMines(dt);
    this.updateEnemies(dt);
    this.updateChests();
    this.updateAltars();
    this.maintainEnemies(dt);
    this.weather.update(dt);
    this.achievements.update({ kills: this.kills, survivalTime: this.survivalTime, bossKills: this.bossKills });
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
      this.spawnEnemy({ type: "basic", boss: true, near: "player" });
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

    if (p.comboTimer > 0) {
      p.comboTimer -= dt;
      if (p.comboTimer <= 0) p.combo = 0;
    }

    if (this.input.attack && p.attackCd <= 0) {
      this.performAttack();
    }
  }

  /** 执行一次攻击：按主动武器类型分流；挥空也进冷却；血代价不足则无法攻击。 */
  private performAttack(): void {
    const p = this.player;
    const c = CFG.Player;
    if (p.hp <= this.stats.hpCost) return; // _CanPayHealthCost：不能一击自杀
    p.attackCd = c.BaseAttackCooldown / Math.max(0.1, this.stats.aspdMult);
    const type = this.attackType;
    const range = c.BaseAttackRange * this.stats.rangeMult;
    for (const cb of this.onAttack) cb(type);

    if (type === "Melee") {
      for (const e of this.nearestEnemies(p.x, p.y, range, this.stats.multi)) {
        const r = this.rollDamage();
        this.hitEnemy(e, r);
      }
      this.harvestInRange(range);
    } else if (type === "Ranged") {
      const target = this.nearestEnemies(p.x, p.y, 600 * this.stats.rangeMult, 1)[0];
      const baseAng = target ? Math.atan2(target.y - p.y, target.x - p.x) : (p.facing > 0 ? 0 : Math.PI);
      const n = Math.max(1, this.stats.spread);
      for (let i = 0; i < n; i++) {
        const ang = baseAng + (i - (n - 1) / 2) * 0.21; // ±12° 散射
        this.fireProjectile(ang, 500, range * 1.2);
      }
    } else if (type === "Magic") {
      const target = this.nearestEnemies(p.x, p.y, 600 * this.stats.rangeMult, 1)[0];
      const ang = target ? Math.atan2(target.y - p.y, target.x - p.x) : (p.facing > 0 ? 0 : Math.PI);
      this.fireProjectile(ang, 300, 5);
    } else {
      // Mine：原地布雷（FIFO 上限）
      if (this.mines.length >= MAX_MINES) this.mines.shift();
      this.mines.push({
        id: this.nextId++, x: p.x, y: p.y,
        dmg: this.rollDamage().dmg, splash: Math.max(60, this.stats.splash), psn: this.stats.psn,
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
    const crit = this.rng.chance(this.stats.crit / 100);
    if (crit) dmg *= CFG.Player.CritMultiplier;
    return { dmg, crit };
  }

  private fireProjectile(ang: number, speed: number, life: number): void {
    this.projectiles.push({
      id: this.nextId++,
      x: this.player.x, y: this.player.y,
      vx: Math.cos(ang) * speed, vy: Math.sin(ang) * speed,
      dmg: this.rollDamage().dmg,
      life, pierce: this.stats.pierce, hitIds: new Set(),
      homing: this.stats.homing || this.attackType === "Magic",
      splash: this.stats.splash, psn: this.stats.psn, freeze: this.stats.freeze, chain: this.stats.chain,
    });
  }

  private hitEnemy(e: Enemy, r: { dmg: number; crit: boolean }): void {
    const p = this.player;
    p.combo++;
    p.comboTimer = CFG.Combo.DecayTime;
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
      .filter((e) => !exclude?.has(e.id) && (e.x - x) ** 2 + (e.y - y) ** 2 <= r2)
      .sort((a, b) => (a.x - x) ** 2 + (a.y - y) ** 2 - ((b.x - x) ** 2 + (b.y - y) ** 2))
      .slice(0, Math.max(1, n));
  }

  private applyHit(x: number, y: number, e: Enemy, dmg: number, fx: { splash: number; psn: number; freeze: number }): void {
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

  private dealDamage(e: Enemy, damage: number): void {
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

      if (p.dead) continue;
      const dx = p.x - e.x;
      const dy = p.y - e.y;
      const dist = Math.hypot(dx, dy) || 1;
      let vx = (dx / dist) * e.speed * speedMult;
      let vy = (dy / dist) * e.speed * speedMult;
      for (const o of this.enemies) {
        if (o.id === e.id) continue;
        const ox = e.x - o.x, oy = e.y - o.y;
        const od = Math.hypot(ox, oy);
        if (od > 0.01 && od < CFG.Enemy.SeparationRange) {
          vx += (ox / od) * CFG.Enemy.SeparationForce;
          vy += (oy / od) * CFG.Enemy.SeparationForce;
        }
      }
      e.x += vx * dt;
      e.y += vy * dt;
      const sep = this.terrain.separate(e.x, e.y, CFG.Enemy.CollisionSize / 2);
      e.x = sep.x; e.y = sep.y;
      e.facing = dx >= 0 ? 1 : -1;

      if (dist <= CFG.Enemy.AttackRange && e.attackCd <= 0) {
        e.attackCd = CFG.Enemy.AttackCooldown;
        if (p.invincible <= 0) {
          p.hp -= e.damage;
          p.invincible = CFG.Player.InvincibleDuration;
          for (const cb of this.onPlayerHurt) cb(p.hp);
          if (p.hp <= 0) this.onPlayerDied();
        }
      }
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
        return { coins: d.coins, metaHp: d.metaHp, metaDmg: d.metaDmg };
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
