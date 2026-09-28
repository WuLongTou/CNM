// R118 越阶奖励入香火钱复盘口径（runGoldEarned 与击杀入账同轨）。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

describe("R118 香火账本一致性", () => {
  it("越阶奖励计入 runGoldEarned（+16 与 coins 同步）", () => {
    const sim = new GameSim(1180, memoryStorage());
    sim.exec({ action: "start" });
    const coins0 = sim.coins;
    forceTier(sim);
    sim.tick(0.05);
    expect(sim.coins).toBe(coins0 + 16); // 2 阶 +8×2
    expect((sim as unknown as { runGoldEarned: number }).runGoldEarned).toBe(16);
  });
});

function forceTier(sim: GameSim) {
  (sim as unknown as { roundTimer: number }).roundTimer = 1e9;
}