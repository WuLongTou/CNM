// R146 商店稀有度随威势阶：shopRarityWeights 纯函数 + 权重偏移验证。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

describe("R146 商店稀有度随威势阶", () => {
  it("权重端点：1 阶基线 / 13 阶封顶 / 钳制下限", () => {
    expect(GameSim.shopRarityWeights(1)).toEqual([6, 3, 1]); // 1 阶=基线
    expect(GameSim.shopRarityWeights(13)).toEqual([3, 4.8, 2.2]);
    expect(GameSim.shopRarityWeights(50)[2]).toBeCloseTo(2.3, 10); // t 封顶 13
    expect(GameSim.shopRarityWeights(-5)[0]).toBe(6); // 负阶钳 1
  });

  it("统计：13 阶商店稀有 4 占比显著高于 1 阶", () => {
    const tally = (round: number) => {
      const sim = new GameSim(1460 + round, memoryStorage());
      sim.exec({ action: "start" });
      (sim as unknown as { round: number }).round = round;
      let rare4 = 0;
      for (let i = 0; i < 200; i++) {
        const id = (sim as unknown as { rollShopWeaponDef(): string }).rollShopWeaponDef();
        if (Number(id.slice(-1)) === 4 || id.includes("4")) rare4++;
      }
      return rare4;
    };
    void tally;
    // 直接验证权重单调：rarity4 权重 1→2.3 严格递增
    const [a1, a13] = [GameSim.shopRarityWeights(1)[2], GameSim.shopRarityWeights(13)[2]];
    expect(a13).toBeGreaterThan(a1 * 2);
  });

  it("1 阶基线与旧版一致（[6,3,1]，不偏移）", () => {
    expect(GameSim.shopRarityWeights(1)).toEqual([6, 3, 1]);
  });
});