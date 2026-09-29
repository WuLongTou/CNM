// R143 财神日回溯验证：dailyMutatorId 注入 gold → 越阶奖励 ×2 生效（R138）。
// 注：mutatorId 注入是构造器测试钩子（R73）；真实玩家路径 = 财神日当天跑
// dailySeed（mutator() 自动派生 gold），两者走同一 advanceRound 表达式。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { dailyMutator } from "../../src/sim/daily";

describe("R143 财神日越阶 ×2 验证", () => {
  it("近 30 天内存在财神日（真实玩家路径存在）", () => {
    let goldDays = 0;
    for (let i = 1; i <= 30; i++) {
      const d = new Date();
      d.setDate(d.getDate() - i);
      if (dailyMutator(d).id === "gold") goldDays++;
    }
    expect(goldDays).toBeGreaterThan(0);
  });

  it("注入 gold：越阶奖励 +32（×2 生效）；注入 dense：+16", () => {
    const gains: number[] = [];
    for (const mut of ["gold", "dense"] as const) {
      const sim = new GameSim(1430, memoryStorage(), "sword", 0, {}, "normal", mut);
      sim.exec({ action: "start" });
      const before = sim.coins;
      (sim as unknown as { roundTimer: number }).roundTimer = 1e9;
      sim.tick(0.05);
      gains.push(sim.coins - before); // 1→2 阶：gold +32 / dense +16
    }
    console.log("gains:", JSON.stringify(gains));
    expect(gains[0]).toBe(32);
    expect(gains[1]).toBe(16);
  });
});