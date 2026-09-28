// R112 Boss Rush 每日最佳：dailyRushBest 按日轮换（rush + 当日种子门控）。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { dailySeed } from "../../src/sim/daily";

const rushVictory = (seed: number, score: number, save = memoryStorage()) => {
  const sim = new GameSim(seed, save, "sword", 0, {}, "boss_rush");
  sim.exec({ action: "start" });
  sim.score = score;
  (sim as unknown as { finalizeRun(m: number, c: null): number }).finalizeRun(1, null);
  return sim;
};

describe("R112 连战每日最佳", () => {
  it("rush + 当日种子：dailyRushBest 记当日最高", () => {
    rushVictory(dailySeed(), 500);
    const s2 = rushVictory(dailySeed(), 800);
    const d = s2.status() as unknown as { daily_rush_best: number };
    expect(d.daily_rush_best).toBe(800);
  });

  it("同日更差成绩：取最高不降", () => {
    const save = memoryStorage();
    rushVictory(dailySeed(), 800, save);
    const s2 = rushVictory(dailySeed(), 300, save);
    const d = s2.status() as unknown as { daily_rush_best: number };
    expect(d.daily_rush_best).toBe(800);
  });

  it("非当日局不计入 dailyRushBest（普通种子 rush）", () => {
    const save = memoryStorage();
    rushVictory(dailySeed(), 500, save);
    const s2 = rushVictory(999, 1000, save);
    const d = s2.status() as unknown as { daily_rush_best: number };
    expect(d.daily_rush_best).toBe(500); // 999 种子局不写入
  });

  it("meta 回报 daily_rush_best（当日有效口径）", () => {
    const save = memoryStorage();
    rushVictory(dailySeed(), 600, save);
    const d = rushVictory(dailySeed(), 650, save).exec({ action: "meta" }).data as unknown as { daily_rush_best: number };
    expect(d.daily_rush_best).toBe(650);
  });
});