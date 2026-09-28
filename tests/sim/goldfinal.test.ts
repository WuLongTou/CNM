// R139 香火账本终检：bot 全程恒等式 earned − spent ≡ coins（含越阶奖励/财神倍乘）。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { dailySeed } from "../../src/sim/daily";

describe("R139 香火账本终检", () => {
  it("bot 全程：earned − spent ≡ coins（普通种子 ×3）", () => {
    for (const seed of [1390, 1391, 1392]) {
      const sim = new GameSim(seed, memoryStorage());
      sim.exec({ action: "start" });
      for (let i = 0; i < 60 * 240; i++) {
        if (sim.state !== "Combat") break;
        if (sim.upgradeOffers) { sim.resolveUpgrade(0); continue; }
        if (sim.fusionOffer) { sim.resolveFusion(false); continue; }
        sim.tick();
      }
      const s = sim as unknown as { runGoldEarned: number; runGoldSpent: number; coins: number };
      expect(s.runGoldEarned - s.runGoldSpent).toBe(s.coins);
    }
  });

  it("每日种子（财神日含 ×2）：恒等式同样成立", () => {
    const sim = new GameSim(dailySeed(), memoryStorage());
    sim.exec({ action: "start" });
    for (let i = 0; i < 60 * 120; i++) {
      if (sim.state !== "Combat") break;
      sim.tick();
    }
    const s = sim as unknown as { runGoldEarned: number; runGoldSpent: number; coins: number };
    expect(s.runGoldEarned - s.runGoldSpent).toBe(s.coins);
  });
});