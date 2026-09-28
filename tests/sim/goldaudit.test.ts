// R119 香火账本审计：重抽花费补入 runGoldSpent（与购买同轨）。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

describe("R119 香火账本：重抽花费", () => {
  it("重抽一次 spent += 基价×通胀倍乘", () => {
    const sim = new GameSim(1190, memoryStorage());
    sim.exec({ action: "start" });
    // 先贴近龛开出报价（重抽门控：报价打开中才可 reroll）
    const s2 = sim as unknown as { shrines: { id: number; x: number; y: number }[]; shopLatch: boolean; nextId: number };
    s2.shrines = [{ id: s2.nextId++, x: sim.player.x, y: sim.player.y }];
    s2.shopLatch = false;
    sim.tick(1 / 60);
    expect(sim.shopOffer).not.toBeNull();
    sim.coins = 500;
    const spent0 = (sim as unknown as { runGoldSpent: number }).runGoldSpent;
    const r = sim.exec({ action: "shop_reroll" });
    const d = r.data as unknown as { handled: boolean; coins: number };
    expect(d.handled).toBe(true);
    const spent = (sim as unknown as { runGoldSpent: number }).runGoldSpent;
    expect(spent).toBeGreaterThan(spent0); // 花费入账
    expect(500 - d.coins).toBe(spent - spent0); // coins 差值 = spent 差值（同轨）
  });
});