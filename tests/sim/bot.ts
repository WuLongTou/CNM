// Bot 对局框架（R23 实机强度基线）：三种固定策略 + 零输入，全部确定性（无 Math.random）。
// 用途：度量生存曲线、锁定防挂机/终局门禁等基线语义；不是人类水平标杆——
// bot 不会垫步躲前摇、不会定时 perfect dodge、不挑目标，故只能证伪明显退化，不能证平衡完美。

import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

export type BotPolicy = "orbit" | "seek" | "stick" | "afk";

export interface BotResult {
  seed: number; ticks: number; time: number; state: string;
  kills: number; score: number; round: number; act: number;
  maxCombo: number; eliteKills: number; weapon: string; cause: string | null;
  coins: number; bosses: number; // R75 Boss Rush 经济曲线度量
}

export interface BotOptions {
  /** Boss Rush 模式（R75）：纯妖王连战，经济曲线度量用。 */
  bossRush?: boolean;
  /** R131 职业覆盖（默认剑修）；秘密职业需预解锁成就。 */
  role?: "sword" | "archer" | "mage" | "hermit" | "wusheng";
  /** R131 预解锁成就 id（秘密职业构造门控用）。 */
  achievements?: string[];
}

/** 跑一整局（终局或 tick 上限即停；报价无脑选首项、融合一律拒绝，保证不卡模态）。 */
export function runBot(seed: number, policy: BotPolicy, maxTicks = 60 * 720, opts: BotOptions = {}): BotResult {
  const save = memoryStorage();
  if (opts.achievements?.length) {
    save.save({ ...save.load()!, achievements: opts.achievements });
  }
  const sim = new GameSim(seed, save, opts.role ?? "sword", 0, {}, opts.bossRush ? "boss_rush" : "normal");
  sim.exec({ action: "start" });
  const cx = sim.world.w / 2, cy = sim.world.h / 2;
  let tick = 0;
  for (; tick < maxTicks; tick++) {
    if (sim.state !== "Combat") break;
    if (sim.upgradeOffers) { sim.resolveUpgrade(0); continue; }
    if (sim.fusionOffer) { sim.resolveFusion(false); continue; }
    if (policy !== "afk") drive(sim, policy, cx, cy);
    sim.tick();
  }
  const s = sim.status() as unknown as {
    kills: number; score: number; round: number; act: number;
    max_combo: number; elite_kills: number; weapon: string; death_cause: string | null;
    coins: number; boss_kills: number;
  };
  return {
    seed, ticks: tick, time: Math.floor(tick / 60), state: sim.state,
    kills: s.kills, score: s.score, round: s.round, act: s.act,
    maxCombo: s.max_combo, eliteKills: s.elite_kills, weapon: s.weapon, cause: s.death_cause,
    coins: s.coins, bosses: s.boss_kills,
  };
}

/** 策略位移 + 常开攻击 + 贴脸冲刺（三种走位，攻击逻辑相同）。 */
function drive(sim: GameSim, policy: Exclude<BotPolicy, "afk">, cx: number, cy: number): void {
  const p = sim.player;
  let nx = Infinity, ex = 0, ey = 0, near = 0, gx = 0, gy = 0;
  for (const e of sim.enemies) {
    const dx = e.x - p.x, dy = e.y - p.y;
    const d = Math.hypot(dx, dy);
    if (d < nx) { nx = d; ex = dx; ey = dy; }
    if (d < 130) { near++; gx += dx; gy += dy; }
  }
  const d = nx || 1;
  let mx = 0, my = 0;
  if (policy === "orbit") {
    // 纯环绕：420 码外绕地图中心轨道，420 内贴脸——近战锥永远打不中追兵（已知局限）
    if (nx < 420) { mx = ex / d; my = ey / d; if (nx < 60) { mx = -mx * 0.5; my = -my * 0.5; } }
    else {
      const px = p.x - cx, py = p.y - cy;
      const r = Math.hypot(px, py) || 1;
      const R = 500;
      const radial = (R - r) / R;
      mx = (-py / r) * 0.9 + (px / r) * radial * 0.5;
      my = (px / r) * 0.9 + (py / r) * radial * 0.5;
    }
  } else if (policy === "seek") {
    // 无脑冲脸：模拟纯新手，预期 30 秒内暴毙
    mx = ex / d; my = ey / d;
  } else {
    // 贴身缠斗：黏住最近目标，人多/残血则撤（仍不会垫步躲前摇——近战换血偏难是已知局限）
    if (near >= 5 || (p.hp < p.maxHp * 0.35 && nx < 150)) {
      const g = Math.hypot(gx, gy) || 1;
      mx = -gx / g; my = -gy / g;
    } else if (nx < 45) { mx = -ey / d * 0.6; my = ex / d * 0.6; }
    else { mx = ex / d; my = ey / d; }
  }
  sim.input.moveX = Math.max(-1, Math.min(1, mx));
  sim.input.moveY = Math.max(-1, Math.min(1, my));
  sim.input.attack = true;
  sim.input.dash = nx < (policy === "stick" ? 90 : 110) && p.dashCd <= 0;
}
