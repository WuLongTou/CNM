// R89 祝福稀有分层：约 40% 出稀有档（效果 ×2、价格 ×2.5），各系仍限购一次。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { CFG } from "../../src/sim/config";

function openShop(seed = 890) {
  const sim = new GameSim(seed, memoryStorage());
  sim.exec({ action: "start" });
  const s = sim as unknown as { nextId: number; shopLatch: boolean; shrines: { id: number; x: number; y: number }[] };
  s.shrines = [{ id: s.nextId++, x: sim.player.x, y: sim.player.y }];
  s.shopLatch = false;
  sim.tick(1 / 60);
  return sim;
}

const buffSlot = (sim: GameSim) => sim.shopOffer!.find((s) => s.kind === "buff")!;

describe("R89 祝福稀有分层", () => {
  it("价格：普通 120 / 稀有 300（120×2.5 取整）", () => {
    for (let i = 0; i < 40; i++) {
      const sim = openShop(890 + i);
      const b = buffSlot(sim);
      const expected = b.buffRare
        ? Math.floor(CFG.Shop.PriceBuff * CFG.Shop.BuffRarePriceMult)
        : CFG.Shop.PriceBuff;
      expect(b.price).toBe(expected);
    }
  });

  it("稀有档效果 ×2：购买伤害稀有 → shopBuff = 0.30", () => {
    for (let i = 0; i < 40; i++) {
      const sim = openShop(930 + i);
      const b = buffSlot(sim);
      if (b.buffKind !== "dmg" || !b.buffRare) continue;
      sim.coins = 9999;
      expect(sim.buyShop(sim.shopOffer!.indexOf(b)).handled).toBe(true);
      expect(sim.shopBuff).toBeCloseTo(CFG.Shop.BuffDmgBonus * CFG.Shop.BuffRareMult, 10);
      return;
    }
    throw new Error("40 龛未出稀有伤害（概率异常）");
  });

  it("稀有出率约 40%（100 龛大数校验，噪声界 ±15）", () => {
    let rare = 0;
    for (let i = 0; i < 100; i++) {
      const sim = openShop(1000 + i);
      if (buffSlot(sim).buffRare) rare++;
    }
    expect(Math.abs(rare - 40)).toBeLessThanOrEqual(15);
  });
});