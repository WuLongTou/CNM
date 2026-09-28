// R99 每日挑战连胜：跨天断裂重置 / 同日不重复计 / 七日成就。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { ACHIEVEMENTS } from "../../src/sim/achievements";
import { dailySeed, nextStreakCount } from "../../src/sim/daily";

function dailyRun(seed = dailySeed()) {
  const sim = new GameSim(seed, memoryStorage());
  sim.exec({ action: "start" });
  (sim as unknown as { onVictory(): number }).onVictory();
  return sim;
}

describe("R99 每日挑战连胜", () => {
  it("当日种子局终局：连胜 +1", () => {
    const sim = dailyRun();
    expect(sim.dailyStreakCount).toBe(1);
  });

  it("非当日局：不计连胜", () => {
    const sim = new GameSim(999, memoryStorage());
    sim.exec({ action: "start" });
    sim.exec({ action: "set_pacts", args: { tiers: { blood: 1 } } });
    (sim as unknown as { onVictory(): number }).onVictory();
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