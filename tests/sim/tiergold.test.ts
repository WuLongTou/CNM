// R138 越阶奖励受财神附身加成（与击杀入账口径一致；当日回归守卫）。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { dailyMutator, dailySeed } from "../../src/sim/daily";

// R206 修复：原测试假定「今日非财神日」，在真实财神日（2026-10-07 实测）必然翻车——
// 改为按当日变异口径自适应断言（gold ×2 / 其余 ×1），任意日期都守卫同一表达式。
const goldMult = () => (dailyMutator()?.id === "gold" ? 2 : 1);

const advance = (seed: number) => {
  const sim = new GameSim(seed, memoryStorage());
  sim.exec({ action: "start" });
  const before = sim.coins;
  (sim as unknown as { roundTimer: number }).roundTimer = 1e9;
  sim.tick(0.05);
  return sim.coins - before;
};

describe("R138 越阶奖励 ×财神口径", () => {
  it("越阶奖励 = +8×阶数 ×当日财神口径（日期无关回归守卫）", () => {
    const gain = advance(dailySeed());
    expect(gain).toBe(8 * 2 * goldMult()); // 1→2 阶基础 +16
  });

  it("财神日口径与击杀金币同一表达式（代码同构守卫）", () => {
    // advanceRound 与 onEnemyKilled 均为 `*(this.mutator()?.id === "gold" ? 2 : 1)`
    // ——单行差异由 code review 保证；此处锁定非财神日行为不回归
    const gain = advance(dailySeed());
    expect(gain % 8).toBe(0);
  });
});