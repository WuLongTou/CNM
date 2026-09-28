// R73 每日变异：日期派生确定性 / 仅当日种子生效 / 注入式效果验证 / 池覆盖。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { CFG } from "../../src/sim/config";
import { dailyMutator, dailySeed, DAILY_MUTATORS } from "../../src/sim/daily";

function readySim(seed: number, mutatorId?: string) {
  const sim = new GameSim(seed, memoryStorage(), "sword", 0, {}, "normal", mutatorId);
  sim.exec({ action: "start" });
  return sim;
}

function spawnOne(sim: GameSim) {
  sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1 } });
  return sim.enemies[sim.enemies.length - 1]!;
}

/** 在 9 月里找指定 id 的变异日种子（30 天五变异轮全覆，必存在）。 */
function seedOf(id: string): number {
  for (let day = 1; day <= 30; day++) {
    const d = new Date(2026, 8, day);
    if (dailyMutator(d).id === id) return dailySeed(d);
  }
  throw new Error("mutator not found: " + id);
}

describe("R73 每日变异", () => {
  it("派生：同日稳定、池内、30 天覆盖全部五变异", () => {
    const m = dailyMutator();
    expect(DAILY_MUTATORS).toContain(m);
    expect(dailyMutator()).toBe(m); // 同日重复调用同一对象
    const ids = new Set<string>();
    for (let day = 1; day <= 30; day++) ids.add(dailyMutator(new Date(2026, 8, day)).id);
    expect([...ids].sort()).toEqual(DAILY_MUTATORS.map((x) => x.id).sort());
  });

  it("仅当日种子生效：每日局回报变异 id，普通局为 null", () => {
    const daily = readySim(dailySeed());
    expect(daily.status().mutator).toBe(dailyMutator().id);
    const normal = readySim(999);
    expect(normal.status().mutator).toBeNull();
  });

  it("财神附身：击杀金币 ×2（注入式）", () => {
    const sim = readySim(999, "gold");
    const e = spawnOne(sim);
    (sim as unknown as { dealDamage(e: unknown, d: number): void }).dealDamage(e, 9999);
    expect(sim.coins).toBe(CFG.Shop.KillGold * 2);
  });

  it("妖行如风：妖物移速 ×1.15（注入式）", () => {
    const base = readySim(999);
    const sb = spawnOne(base).speed;
    const sim = readySim(998, "swift");
    const s2 = spawnOne(sim).speed;
    expect(s2).toBeCloseTo(sb * 1.15, 6);
  });

  it("妖坚如铁：妖物生命 +30%（先难度取整再变异取整，注入式）", () => {
    const base = readySim(999);
    const hb = spawnOne(base).hp; // 28（25×1.096 上取整）
    const sim = readySim(997, "tough");
    expect(spawnOne(sim).hp).toBe(Math.ceil(hb * 1.3));
  });

  it("妖潮汹涌：装满至普通上限后仍可再刷（维持上限 +6 行为断言）", () => {
    const capOf = (s: GameSim) => (s as unknown as { actCap(): { max: number } }).actCap().max;
    const me = (s: GameSim) => {
      (s as unknown as { spawnCd: number }).spawnCd = 0; // 清冷却：隔离首调早退
      (s as unknown as { maintainEnemies(dt: number): void }).maintainEnemies(1 / 60);
    };
    const base = readySim(999);
    for (let i = 0; i < capOf(base); i++) spawnOne(base);
    const dense = readySim(seedOf("dense"), "dense");
    for (let i = 0; i < capOf(base); i++) spawnOne(dense); // 填到普通上限（dense 实际 +6）
    me(base);
    me(dense);
    expect(base.enemies.length).toBe(capOf(base)); // 普通日满员不刷
    expect(dense.enemies.length).toBe(capOf(base) + 1); // 庙会日上限 +6：满员仍刷一只
  });

  it("精锐频出：固定种子下精英数显著多于普通日（注入式）", () => {
    const countElites = (seed: number, mutatorId?: string) => {
      const sim = new GameSim(seed, memoryStorage(), "sword", 0, {}, "normal", mutatorId);
      sim.exec({ action: "start" });
      sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 60 } });
      return sim.enemies.filter((e) => e.elite).length;
    };
    const baseElites = countElites(999);
    const dailyElites = countElites(999, "elite");
    expect(dailyElites).toBeGreaterThan(baseElites + 5);
  });
});
