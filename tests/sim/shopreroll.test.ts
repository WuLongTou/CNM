// R78 香火龛刷新报价：扣费重 roll / 龛体保留 / 通胀联动 / 余额拒绝。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { CFG } from "../../src/sim/config";

function openShop(seed = 780) {
  const sim = new GameSim(seed, memoryStorage());
  sim.exec({ action: "start" });
  const s = sim as unknown as { nextId: number; shopLatch: boolean; shrines: { id: number; x: number; y: number }[] };
  s.shrines = [{ id: s.nextId++, x: sim.player.x, y: sim.player.y }];
  s.shopLatch = false;
  sim.tick(1 / 60);
  return sim;
}

describe("R78 香火龛刷新报价", () => {
  it("刷新：扣费重 roll，龛体保留、报价仍在（R 键/按钮同一路径）", () => {
    const sim = openShop();
    sim.coins = 500;
    const before = JSON.stringify(sim.shopOffer);
    const r = sim.rerollShop();
    expect(r.handled).toBe(true);
    expect(sim.coins).toBe(500 - sim.shopRerollCost());
    expect(sim.shopOffer).not.toBeNull(); // 龛体与报价保留
    expect(sim.paused).toBe(true); // 仍在模态
    expect(JSON.stringify(sim.shopOffer)).not.toBe(before); // 重 roll 出新报价
  });

  it("刷新成本随通胀：购买后重刷变贵", () => {
    const sim = openShop();
    sim.coins = 9999;
    const cost0 = sim.shopRerollCost();
    expect(cost0).toBe(CFG.Shop.PriceReroll);
    sim.buyShop(sim.shopOffer!.findIndex((s) => s.kind === "heal"));
    expect(sim.shopRerollCost()).toBe(Math.floor(CFG.Shop.PriceReroll * CFG.Shop.EscalateMult));
  });

  it("余额不足：拒绝且报价保持", () => {
    const sim = openShop();
    sim.coins = 5;
    const r = sim.rerollShop();
    expect(r.handled).toBe(false);
    expect(sim.shopOffer).not.toBeNull();
    expect(sim.coins).toBe(5);
  });

  it("exec 路径：shop_reroll 透传（关闭后拒绝）", () => {
    const sim = openShop();
    sim.closeShop();
    const r = sim.exec({ action: "shop_reroll" });
    expect((r.data as { handled: boolean }).handled).toBe(false);
  });
});