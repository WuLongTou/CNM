// R158 分源账本：goldEarnedKills/goldEarnedTier 字段 + 恒等式（与 R139 同式）。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

describe("R158 分源账本", () => {
  it("击杀入 goldEarnedKills", () => {
    const sim = new GameSim(1580, memoryStorage());
    sim.exec({ action: "start" });
    sim.exec({ action: "spawn", args: { type: "basic", near: { x: sim.player.x + 30, y: sim.player.y }, count: 1 } });
    const e = sim.enemies[0];
    e.hp = 1;
    (sim as unknown as { onEnemyKilled(e: unknown): void }).onEnemyKilled(e);
    expect(sim.goldEarnedKills).toBeGreaterThan(0);
    expect(sim.goldEarnedTier).toBe(0);
  });

  it("越阶入 goldEarnedTier；两源之和 = runGoldEarned", () => {
    const sim = new GameSim(1581, memoryStorage());
    sim.exec({ action: "start" });
    (sim as unknown as { roundTimer: number }).roundTimer = 1e9;
    sim.tick(0.05);
    expect(sim.goldEarnedTier).toBeGreaterThan(0);
    expect(sim.goldEarnedKills + sim.goldEarnedTier).toBe(sim.runGoldEarned);
  });
});