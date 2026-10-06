// R132 三花聚顶：单局集齐三系祝福并通关（buffsBought 公开只读口径）。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { ACHIEVEMENTS } from "../../src/sim/achievements";

const openShop = () => {
  const sim = new GameSim(1320, memoryStorage());
  sim.exec({ action: "start" });
  const st = sim as unknown as { shrines: { id: number; x: number; y: number }[]; shopLatch: boolean; nextId: number };
  st.shrines = [{ id: st.nextId++, x: sim.player.x, y: sim.player.y }];
  st.shopLatch = false;
  sim.tick(1 / 60);
  return sim;
};

const buyKind = (sim: GameSim, kind: "dmg" | "asp" | "spd") => {
  const slot = sim.shopOffer!.find((s) => s.kind === "buff")!;
  slot.buffKind = kind;
  sim.coins = 9999;
  sim.exec({ action: "shop_buy", args: { index: sim.shopOffer!.indexOf(slot) } });
  sim.closeShop(); // R202 连购语义：主动收摊，重置触发闩再贴近重开报价（三连购测试用）
  const st = sim as unknown as { shrines: { id: number; x: number; y: number }[]; shopLatch: boolean };
  st.shrines = [{ id: st.shrines[0]?.id ?? 1, x: sim.player.x, y: sim.player.y }];
  st.shopLatch = false;
  sim.tick(1 / 60);
};

describe("R132 三花聚顶", () => {
  it("成就表 42 项：三花聚顶 check 型", () => {
    expect(ACHIEVEMENTS).toHaveLength(42);
    const a = ACHIEVEMENTS.find((x) => x.id === "three_buffs")!;
    expect(a.check).toBeTruthy();
  });

  it("集齐三系后通关：授三花聚顶", () => {
    const sim = openShop();
    buyKind(sim, "dmg");
    buyKind(sim, "asp");
    buyKind(sim, "spd");
    expect(sim.buffsBought.size).toBe(3);
    (sim as unknown as { onVictory(): number }).onVictory();
    expect(sim.achievements.unlocked.has("three_buffs")).toBe(true);
  });

  it("只集两系通关：不授", () => {
    const sim = openShop();
    buyKind(sim, "dmg");
    buyKind(sim, "asp");
    (sim as unknown as { onVictory(): number }).onVictory();
    expect(sim.achievements.unlocked.has("three_buffs")).toBe(false);
  });
});