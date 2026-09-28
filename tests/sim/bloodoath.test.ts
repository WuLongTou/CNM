import { describe, expect, it } from "vitest";
import { GameSim, type Enemy } from "../../src/sim/game";
import { ACHIEVEMENTS } from "../../src/sim/achievements";
import { PACTS } from "../../src/sim/pacts";
import { memoryStorage } from "../../src/sim/save";

function readySim(seed = 251, pacts: string[] = []) {
  const sim = new GameSim(seed, memoryStorage(), "sword", 0, pacts);
  sim.exec({ action: "start" });
  return sim;
}

function slay(sim: GameSim): void {
  sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1 } });
  const e = sim.enemies[sim.enemies.length - 1];
  (sim as unknown as { onEnemyKilled(e: Enemy): void }).onEnemyKilled(e);
}

describe("R25 血誓契约", () => {
  it("默认无誓：零影响（血量/分数与基线一致）", () => {
    const sim = readySim();
    sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1 } });
    const e = sim.enemies[sim.enemies.length - 1];
    expect(e.hp).toBe(28); // 1 阶倍率后基线（25×1.12）
    slay(sim);
    expect(sim.score).toBe(10);
    expect(sim.pactScoreMult()).toBe(1);
  });

  it("血战：妖血 ×1.25；分数 ×1.25", () => {
    const sim = readySim(251, ["blood"]);
    sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1 } });
    const e = sim.enemies[sim.enemies.length - 1];
    expect(e.hp).toBe(Math.ceil(28 * 1.25));
    slay(sim);
    expect(sim.score).toBe(Math.floor(10 * 1.25));
  });

  it("脆体：上限 90，满血开局", () => {
    const sim = readySim(251, ["frail"]);
    expect(sim.player.maxHp).toBe(90);
    expect(sim.player.hp).toBe(90);
  });

  it("人海：300 tick 击杀数显著多于无誓", () => {
    const run = (pacts: string[]) => {
      const sim = new GameSim(251, memoryStorage(), "sword", 0, pacts);
      sim.exec({ action: "start" });
      sim.player.hp = 1e9; sim.player.maxHp = 1e9;
      const inner = sim as unknown as { onEnemyKilled(e: Enemy): void };
      for (let i = 0; i < 300; i++) {
        sim.tick();
        for (const e of [...sim.enemies]) inner.onEnemyKilled(e);
      }
      return sim.kills;
    };
    const base = run([]);
    const horde = run(["horde"]);
    expect(horde).toBeGreaterThan(base);
    expect(horde).toBeLessThan(base * 1.8);
  });

  it("三誓叠加连乘；set_pacts 校验", () => {
    const sim = new GameSim(251, memoryStorage());
    expect(sim.pactScoreMult()).toBe(1);
    expect(sim.setPacts(["blood", "nope"]).applied).toBe(false);
    expect(sim.pacts.size).toBe(0);
    expect(sim.setPacts(["blood", "horde", "frail"]).applied).toBe(true);
    expect(sim.pactScoreMult()).toBeCloseTo(1.25 * 1.3 * 1.5, 10);
    expect(sim.player.maxHp).toBe(90);
    const snap = sim.snapshot() as { pacts: string[]; score_mult: number };
    expect(snap.pacts.sort()).toEqual(["blood", "frail", "horde"]);
    expect(snap.score_mult).toBeCloseTo(1.25 * 1.3 * 1.5, 10);
    const via = sim.exec({ action: "set_pacts", args: { pacts: ["blood"] } });
    expect((via.data as { applied: boolean }).applied).toBe(true);
    expect([...sim.pacts]).toEqual(["blood"]);
  });

  it("开局后锁死；三重血誓通关授勋", () => {
    const sim = new GameSim(251, memoryStorage(), "sword", 0, ["blood", "horde", "frail"]);
    sim.exec({ action: "start" });
    expect(sim.setPacts([]).applied).toBe(false);
    (sim as unknown as { onVictory(): number }).onVictory();
    expect(sim.achievements.unlocked.has("oathbound")).toBe(true);
    expect(ACHIEVEMENTS.length).toBe(29); // R47 妖王猎手
  });

  it("PACTS 表自洽：三誓名与文案乘数一致", () => {
    expect(PACTS.blood.desc).toContain("+25%");
    expect(PACTS.horde.desc).toContain("+30%");
    expect(PACTS.frail.desc).toContain("+50%");
  });
});
