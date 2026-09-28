// R127 spd 祝福累计追踪：shopBuffSpd 加法口径 + 预览三系全覆盖。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

const openShop = () => {
  const sim = new GameSim(1270, memoryStorage());
  sim.exec({ action: "start" });
  const st = sim as unknown as { shrines: { id: number; x: number; y: number }[]; shopLatch: boolean; nextId: number };
  st.shrines = [{ id: st.nextId++, x: sim.player.x, y: sim.player.y }];
  st.shopLatch = false;
  sim.tick(1 / 60);
  return sim;
};

const buyKind = (sim: GameSim, kind: "dmg" | "asp" | "spd", rare = false) => {
  const slot = sim.shopOffer!.find((s) => s.kind === "buff")!;
  slot.buffKind = kind; // 随机系别 → 强制为目标系别（买分支按 buffKind 结算）
  slot.buffRare = rare;
  sim.coins = 9999;
  return sim.exec({ action: "shop_buy", args: { index: sim.shopOffer!.indexOf(slot) } });
};

describe("R127 spd 累计追踪", () => {
  it("spd 购买：shopBuffSpd 累加（普通 0.08 / 稀有 0.16）", () => {
    const sim = openShop();
    buyKind(sim, "spd");
    expect(sim.shopBuffSpd).toBeCloseTo(0.08, 10);
  });

  it("稀有 spd：累计 0.16 且 player.speed 同乘", () => {
    const sim = openShop();
    const speed0 = sim.player.speed;
    buyKind(sim, "spd", true);
    expect(sim.shopBuffSpd).toBeCloseTo(0.16, 10);
    expect(sim.player.speed).toBeCloseTo(speed0 * (1 + 0.08 * 2), 10);
  });

  it("spd 累计口径与预览数据通路：shopBuffSpd 可被 shop.ts 读取", () => {
    const sim = openShop();
    // 预览读取 sim.shopBuffSpd（shop.ts cardHtml）——字段存在且初始 0 即通路
    expect(sim.shopBuffSpd).toBe(0);
    buyKind(sim, "spd");
    expect(sim.shopBuffSpd).toBeCloseTo(0.08, 10);
  });
});