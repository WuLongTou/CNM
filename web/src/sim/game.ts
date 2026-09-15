// 游戏主模拟（sim 层核心）：纯逻辑、零 DOM/渲染依赖。
// 由 app 层以固定 60Hz 步长驱动 tick()；vitest 可直接实例化跑逻辑回归。
// 规则对齐 Godot 版 GameManager/Player/PlayerCombat/Zombie（M1 切片：基础战斗闭环）。

import { Rng } from "./rng";
import {
  CFG, ZOMBIE_NAMES, ZOMBIE_SCORE, ZOMBIE_STATS, SPAWN_TABLE,
  type ZombieType,
} from "./config";
import { defaultSave, type SaveData, type SaveStorage } from "./save";

export type GameState = "WeaponSelection" | "Combat" | "GameOver";

export interface InputState {
  moveX: number;
  moveY: number;
  attack: boolean;
  /** 单帧脉冲：tick 消费后自动清零（对齐 PlayerInput 的 QueueExternalDash）。 */
  dash: boolean;
  cycleTarget: boolean;
}

export interface EliteAffix {
  kind: "none" | "swift" | "regen" | "shield" | "explosive" | "frenzy";
}

export interface Zombie {
  id: number;
  type: ZombieType;
  x: number; y: number;
  hp: number; maxHp: number;
  damage: number; speed: number;
  elite: boolean; boss: boolean;
  affix: EliteAffix["kind"];
  /** 护盾（shield 词缀），先扣护盾再扣血。 */
  shieldHp: number;
  attackCd: number;
  hitFlash: number;
  facing: number;
}

export interface Player {
  x: number; y: number;
  hp: number; maxHp: number;
  speed: number;
  damage: number;
  attackCd: number;
  dashTimer: number; dashCd: number;
  invincible: number;
  combo: number; comboTimer: number;
  facing: number;
  moving: boolean;
  dead: boolean;
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

export class GameSim {
  state: GameState = "WeaponSelection";
  round = 1;
  score = 0;
  kills = 0;
  /** 本局存活时间（秒）。 */
  survivalTime = 0;
  readonly seed: number;
  readonly world = { w: CFG.World.Width, h: CFG.World.Height };
  player: Player;
  zombies: Zombie[] = [];
  input: InputState = { moveX: 0, moveY: 0, attack: false, dash: false, cycleTarget: false };

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
    this.player = {
      x: this.world.w / 2, y: this.world.h / 2,
      hp: c.MaxHealth, maxHp: c.MaxHealth,
      speed: c.BaseSpeed, damage: c.BaseDamage,
      attackCd: 0, dashTimer: 0, dashCd: 0, invincible: 0,
      combo: 0, comboTimer: 0, facing: 1, moving: false, dead: false,
    };
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

  // ── 主循环 ──────────────────────────────────────────────

  /** 固定步长推进（app 层累加器保证 60Hz；测试可直接连续调用）。 */
  tick(dt: number = FIXED_DT): void {
    if (this.state !== "Combat") {
      // 硬约束（项目 AGENTS.md）：非战斗态 100% 清空输入，模态/欢迎/结算页绝不漏按键。
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
    this.updateZombies(dt);
    this.maintainZombies(dt);
  }

  private advanceRound(): void {
    this.round++;
    this.emit("threat_tier", { tier: this.round });
    // 每 BossInterval 阶出一只 Boss（对齐 GameConfig.BossInterval）
    if (this.round % CFG.Zombie.BossInterval === 0 && this.bossSpawnedForRound < this.round) {
      this.bossSpawnedForRound = this.round;
      this.spawnZombie({ type: "basic", boss: true, near: "player" });
    }
  }

  /** 难度倍率，对齐 GameManager._GetRoundMult：1 + round * 0.04 * 2 * factor。 */
  roundMult(factor = 1): number {
    return 1 + this.round * CFG.Difficulty.RoundMultiplierBase * 2 * factor;
  }

  private updatePlayer(dt: number): void {
    const p = this.player;
    const c = CFG.Player;
    if (p.dead) return;

    // 冲刺：单帧脉冲消费
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
    p.x += vx * p.speed * mult * dt;
    p.y += vy * p.speed * mult * dt;
    p.x = Math.min(this.world.w - 22, Math.max(22, p.x));
    p.y = Math.min(this.world.h - 22, Math.max(22, p.y));
    p.moving = len > 0.01;
    if (vx !== 0) p.facing = vx > 0 ? 1 : -1;

    p.dashTimer = Math.max(0, p.dashTimer - dt);
    p.dashCd = Math.max(0, p.dashCd - dt);
    p.invincible = Math.max(0, p.invincible - dt);
    p.attackCd = Math.max(0, p.attackCd - dt);

    // 连击衰减
    if (p.comboTimer > 0) {
      p.comboTimer -= dt;
      if (p.comboTimer <= 0) p.combo = 0;
    }

    // 近战攻击：范围内最近 1 只；挥空也进冷却（对齐 _PerformMeleeAttack 注释约定）
    if (this.input.attack && p.attackCd <= 0) {
      p.attackCd = c.BaseAttackCooldown;
      const range = c.BaseAttackRange;
      const target = this.nearestZombie(p.x, p.y, range);
      if (target) this.attackZombie(target);
    }
  }

  private nearestZombie(x: number, y: number, range: number): Zombie | null {
    let best: Zombie | null = null;
    let bestD = range * range;
    for (const z of this.zombies) {
      const d = (z.x - x) ** 2 + (z.y - y) ** 2;
      if (d <= bestD) { bestD = d; best = z; }
    }
    return best;
  }

  private attackZombie(z: Zombie): void {
    const p = this.player;
    p.facing = z.x >= p.x ? 1 : -1;
    // 连击：命中 +1 层，每层 +5% 上限 +50%（对齐 Combo 系统与 AttackZombie）
    p.combo++;
    p.comboTimer = CFG.Combo.DecayTime;
    const comboBonus = Math.min((p.combo - 1) * CFG.Combo.DamageBonusPerLevel, CFG.Combo.MaxBonus);
    let damage = p.damage * (1 + comboBonus);
    this.dealDamage(z, damage);
  }

  private dealDamage(z: Zombie, damage: number): void {
    if (z.shieldHp > 0) {
      const absorbed = Math.min(z.shieldHp, damage);
      z.shieldHp -= absorbed;
      damage -= absorbed;
    }
    z.hp -= damage;
    z.hitFlash = 0.16; // ZombieHitFlashIn 0.04 + Out 0.12
    if (z.hp <= 0) this.onZombieKilled(z);
  }

  private onZombieKilled(z: Zombie): void {
    this.zombies = this.zombies.filter((o) => o.id !== z.id);
    this.kills++;
    let pts = ZOMBIE_SCORE[z.type] ?? 10;
    if (z.elite) pts = Math.floor(pts * CFG.Zombie.EliteScoreMult);
    if (z.boss) pts = Math.floor(pts * CFG.Zombie.BossScoreMult);
    this.score += pts;
  }

  private updateZombies(dt: number): void {
    const p = this.player;
    for (const z of this.zombies) {
      z.hitFlash = Math.max(0, z.hitFlash - dt);
      z.attackCd = Math.max(0, z.attackCd - dt);

      // 精英词缀（M1 实现 swift/regen，其余 M2）
      if (z.affix === "regen" && z.hp < z.maxHp) {
        z.hp = Math.min(z.maxHp, z.hp + z.maxHp * 0.02 * dt);
      }

      // 追击玩家
      const dx = p.x - z.x;
      const dy = p.y - z.y;
      const dist = Math.hypot(dx, dy) || 1;
      if (!p.dead) {
        let vx = (dx / dist) * z.speed;
        let vy = (dy / dist) * z.speed;

        // 分离力：避免堆叠成一点，形成包围圈（对齐 SeparationRange/Force）
        for (const o of this.zombies) {
          if (o.id === z.id) continue;
          const ox = z.x - o.x, oy = z.y - o.y;
          const od = Math.hypot(ox, oy);
          if (od > 0.01 && od < CFG.Zombie.SeparationRange) {
            vx += (ox / od) * CFG.Zombie.SeparationForce;
            vy += (oy / od) * CFG.Zombie.SeparationForce;
          }
        }
        z.x += vx * dt;
        z.y += vy * dt;
        z.facing = dx >= 0 ? 1 : -1;
      }

      // 攻击：范围 70、冷却 1s；玩家无敌帧 0.5s 内免疫
      if (!p.dead && dist <= CFG.Zombie.AttackRange && z.attackCd <= 0) {
        z.attackCd = CFG.Zombie.AttackCooldown;
        if (p.invincible <= 0) {
          p.hp -= z.damage;
          p.invincible = CFG.Player.InvincibleDuration;
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
    // 结算（对齐 GameManager._OnPlayerDied）：金币 = score/50 + kills
    const prev = this.save.load() ?? defaultSave();
    const next: SaveData = {
      highScore: Math.max(prev.highScore, this.score),
      totalKills: prev.totalKills + this.kills,
      games: prev.games + 1,
      coins: prev.coins + Math.floor(this.score / 50) + this.kills,
    };
    this.save.save(next);
    this.emit("game_over", this.snapshot());
  }

  /** 持续探索模式刷怪：世界僵尸数维持 [6,18]，冷却 1.5s（对齐 _MaintainZombies）。 */
  private maintainZombies(dt: number): void {
    this.spawnCd = Math.max(0, this.spawnCd - dt);
    if (this.zombies.length >= CFG.Spawning.WorldMinZombies) return;
    if (this.zombies.length >= CFG.Spawning.WorldMaxZombies) return;
    if (this.spawnCd > 0) return;
    this.spawnCd = CFG.Spawning.SpawnCooldown;
    this.spawnZombie({ type: "random", near: "player" });
  }

  /** 刷怪：距玩家 ≥420px 环形随机（对齐 ZombieSpawnMinDistFromPlayer）。 */
  spawnZombie(opts: SpawnOpts = {}): number {
    const count = Math.min(Math.max(opts.count ?? 1, 1), 50);
    let spawned = 0;
    for (let i = 0; i < count; i++) {
      if (this.zombies.length >= CFG.Spawning.WorldMaxZombies && !opts.boss) break;
      const type = this.resolveType(opts.type);
      const pos = this.resolveSpawnPos(opts.near ?? "player");
      this.createZombie(type, pos.x, pos.y, opts);
      spawned++;
    }
    return spawned;
  }

  private resolveType(t: SpawnOpts["type"]): ZombieType {
    if (t === "basic") return 0;
    if (typeof t === "number") return t as ZombieType;
    const entry = SPAWN_TABLE.find((e) => this.round <= e.maxRound) ?? SPAWN_TABLE[SPAWN_TABLE.length - 1];
    const weights = Object.entries(entry.weights).map(([k, w]) => [Number(k), w] as const);
    return this.rng.weighted(weights) as ZombieType;
  }

  private resolveSpawnPos(near: SpawnOpts["near"]): { x: number; y: number } {
    if (near && near !== "player" && near !== "random") {
      return { x: Math.min(this.world.w - 22, Math.max(22, near.x)), y: Math.min(this.world.h - 22, Math.max(22, near.y)) };
    }
    if (near === "random") {
      return { x: this.rng.range(22, this.world.w - 22), y: this.rng.range(22, this.world.h - 22) };
    }
    // 玩家周围 420~600px 环形
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

  private createZombie(type: ZombieType, x: number, y: number, opts: SpawnOpts): Zombie {
    const stats = ZOMBIE_STATS[type];
    const mult = this.roundMult(CFG.Difficulty.SpawnScaleFactor);
    let hp = Math.ceil(stats.hp * mult);
    let damage = Math.ceil(stats.dmg * mult);
    let speed = stats.speed;
    let elite = opts.elite ?? false;
    let boss = opts.boss ?? false;

    if (!elite && !boss) {
      elite = this.rng.chance(Math.min(
        CFG.Zombie.EliteChanceBase + CFG.Zombie.EliteChancePerRound * (this.round - 1),
        CFG.Zombie.EliteChanceMax,
      ));
    }
    if (elite) {
      hp = Math.ceil(hp * CFG.Zombie.EliteStatMult);
      damage = Math.ceil(damage * CFG.Zombie.EliteStatMult);
      speed *= CFG.Zombie.EliteSpeedMult;
    }
    if (boss) {
      hp = Math.ceil(hp * CFG.Zombie.BossStatMult * 10);
      damage = Math.ceil(damage * CFG.Zombie.BossStatMult);
      speed *= CFG.Zombie.BossSpeedMult;
    }

    const z: Zombie = {
      id: this.nextId++,
      type,
      x, y,
      hp, maxHp: hp,
      damage, speed,
      elite, boss,
      affix: elite ? this.pickAffix() : "none",
      shieldHp: 0,
      attackCd: this.rng.range(0.2, 0.8),
      hitFlash: 0,
      facing: 1,
    };
    if (z.affix === "swift") z.speed = speed * 1.5; // EliteSwiftMult
    if (z.affix === "shield") z.shieldHp = 25;
    this.zombies.push(z);
    return z;
  }

  private pickAffix(): EliteAffix["kind"] {
    return this.rng.pick(["swift", "regen", "shield", "explosive", "frenzy"] as const);
  }

  // ── 调试/AI 指令（协议对齐 ExternalCommandService） ─────────

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
      case "choose": return { handled: false, note: "M1 无融合面板（M2 实装）" };
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
        return { spawned: this.spawnZombie(opts) };
      }
      case "give": return { given: 0, note: "武器系统未实装（M2）" };
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
    return { applied: this.state === "Combat", move: { x: i.moveX, y: i.moveY }, attack: i.attack };
  }

  // ── 观测快照（对齐 _Status/_GameState 字段名） ─────────────

  status(): Record<string, unknown> {
    const d: Record<string, unknown> = {
      state: this.state,
      round: this.round,
      score: this.score,
      kills: this.kills,
      zombies_alive: this.zombies.length,
      welcome_visible: this.state === "WeaponSelection",
      player_hp: this.player.hp,
      player_max_hp: this.player.maxHp,
      player_pos: { x: this.player.x, y: this.player.y },
    };
    return d;
  }

  /** 全量观测快照（Dota2 GSI 风格），字段名对齐 _GameState。 */
  snapshot(): Record<string, unknown> {
    const p = this.player;
    const sorted = [...this.zombies].sort((a, b) =>
      (a.x - p.x) ** 2 + (a.y - p.y) ** 2 - ((b.x - p.x) ** 2 + (b.y - p.y) ** 2));
    return {
      ...this.status(),
      survival_time: this.survivalTime,
      paused: false,
      player: {
        hp: p.hp, max_hp: p.maxHp,
        pos: { x: p.x, y: p.y },
        combo: p.combo,
        can_attack: p.attackCd <= 0,
        dead: p.dead,
      },
      zombies: {
        total: this.zombies.length,
        list: sorted.slice(0, 30).map((z) => ({
          id: z.id, type: ZOMBIE_NAMES[z.type] ?? String(z.type), type_id: z.type,
          hp: Math.ceil(z.hp), max_hp: z.maxHp,
          pos: { x: z.x, y: z.y },
          elite: z.elite, boss: z.boss,
        })),
      },
    };
  }
}
