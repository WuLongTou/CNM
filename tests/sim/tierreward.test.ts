// R117 越阶奖励：威胁阶进阶补香火（+8×新阶数），风险回报节奏。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

const forceTier = (sim: GameSim) =>
  (sim as unknown as { roundTimer: number }).roundTimer = 1e9; // 立即触发进阶

describe("R117 越阶奖励", () => {
  it("进阶时香火 +8×新阶数（2 阶 +16）", () => {
    const sim = new GameSim(1170, memoryStorage());
    sim.exec({ action: "start" });
    const before = sim.coins;
    const round0 = sim.round;
    forceTier(sim);
    sim.tick(0.05);
    expect(sim.round).toBe(round0 + 1);
    expect(sim.coins).toBe(before + 8 * (round0 + 1));
  });

  it("连续进阶奖励递增（2 阶 +16 → 3 阶 +24）", () => {
    const sim = new GameSim(1171, memoryStorage());
    sim.exec({ action: "start" });
    const before = sim.coins;
    forceTier(sim); sim.tick(0.05);
    forceTier(sim); sim.tick(0.05);
    expect(sim.coins).toBe(before + 16 + 24);
  });
});