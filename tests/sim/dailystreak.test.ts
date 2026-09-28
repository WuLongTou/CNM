// R99/R113 每日挑战连胜：参与即算（开局计）——陨落不断连胜；跨天断裂重置 / 同日不重复计 / 七日成就。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { ACHIEVEMENTS } from "../../src/sim/achievements";
import { dailySeed, nextStreakCount } from "../../src/sim/daily";

function dailyRun(seed = dailySeed()) {
  const sim = new GameSim(seed, memoryStorage());
  sim.exec({ action: "start" });
  return sim;
}

describe("R99 每日挑战连胜", () => {
  it("当日种子局开局：连胜 +1（参与即算）", () => {
    const sim = dailyRun();
    expect(sim.dailyStreakCount).toBe(1);
  });

  it("陨落不断连胜：开局已计，不因未通关回滚", () => {
    const sim = dailyRun();
    (sim as unknown as { finalizeRun(m: number, c: null): number }).finalizeRun(1, null); // 陨落结算
    expect(sim.dailyStreakCount).toBe(1); // 不回滚
  });

  it("非当日局：不计连胜", () => {
    const sim = new GameSim(999, memoryStorage());
    sim.exec({ action: "start" });
    expect(sim.dailyStreakCount).toBe(0);
  });

  it("七日成就 goal：dailyStreakCount ≥ 7", () => {
    const a = ACHIEVEMENTS.find((x) => x.id === "daily_streak_7")!;
    expect(a.goal).toEqual({ stat: "dailyStreakCount", target: 7 });
  });
});

describe("R99 nextStreakCount 纯函数", () => {
  it("同日：不重复计", () => {
    expect(nextStreakCount("2026-09-28", "2026-09-28", 3)).toBe(3);
  });
  it("昨天：+1", () => {
    expect(nextStreakCount("2026-09-28", "2026-09-29", 3)).toBe(4);
  });
  it("断裂：重置 1", () => {
    expect(nextStreakCount("2026-09-20", "2026-09-29", 3)).toBe(1);
  });
  it("首挑战：1", () => {
    expect(nextStreakCount("", "2026-09-29", 0)).toBe(1);
  });
});