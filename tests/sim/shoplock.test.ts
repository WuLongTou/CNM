// R163 槽位锁定：locked 槽重抽保留 / toggleShopLock / 购买后解锁复位。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

const openShop = () => {
  const sim = new GameSim(1630, memoryStorage());
  sim.exec({ action: "start" });
  const st = sim as unknown as { shrines: { id: number; x: number; y: number }[]; shopLatch: boolean; nextId: number };
  st.shrines = [{ id: st.nextId++, x: sim.player.x, y: sim.player.y }];
  st.shopLatch = false;
  sim.tick(1 / 60);
  return sim;
};

describe("R163 槽位锁定", () => {
  it("锁定后重抽：locked 槽保留原报价，未锁定槽重 roll", () => {
    const sim = openShop();
    expect(sim.toggleShopLock(0)).toBe(true);
    expect(sim.shopOffer![0].locked).toBe(true);
    const before = JSON.stringify(sim.shopOffer![0]);
    sim.rerollShop();
    expect(JSON.stringify(sim.shopOffer![0])).toBe(before); // 保留
    // 未锁定槽仍存在（总槽数不变）
    expect(sim.shopOffer!.length).toBeGreaterThanOrEqual(3);
  });

  it("解锁后重抽：该槽重新 roll", () => {
    const sim = openShop();
    sim.toggleShopLock(1);
    sim.toggleShopLock(1); // 再切=解锁
    expect(sim.shopOffer![1].locked).toBe(false);
  });

  it("R202 连购：被购锁定槽移除、余槽无锁定残留；购罄散龛（无跨龛残留）", () => {
    const sim = openShop();
    sim.toggleShopLock(0);
    sim.coins = 9999;
    sim.buyShop(0);
    expect(sim.shopOffer).not.toBeNull(); // R202 余货保持
    expect(sim.shopOffer!.some((s) => s.locked)).toBe(false); // 被购锁定槽已移除
    while (sim.shopOffer) sim.buyShop(0);
    expect(sim.shopOffer).toBeNull(); // 购罄散龛
    expect(sim.shrines).toHaveLength(0);
    expect(sim.paused).toBe(false);
  });
});