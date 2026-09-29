// R174 每日挑战周历：dailyHistory 推入/同日取最佳/非每日局不记/cap 28。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage, normalizeSave } from "../../src/sim/save";
import { dailySeed } from "../../src/sim/daily";

const finalizeDaily = (score: number, seed = dailySeed()) => {
  const sim = new GameSim(seed, memoryStorage());
  sim.exec({ action: "start" });
  (sim as unknown as { score: number }).score = score;
  (sim as unknown as { finalizeRun(m: number, c: null): number }).finalizeRun(1, null);
  return sim;
};

describe("R174 每日挑战周历", () => {
  it("每日局终局：dailyHistory 推入当日记录", () => {
    const sim = finalizeDaily(1234);
    const d = (sim as unknown as { save: { load(): { dailyHistory: { date: string; score: number }[] } } }).save.load()!;
    expect(d.dailyHistory).toHaveLength(1);
    expect(d.dailyHistory[0]!.score).toBe(1234);
  });

  it("同日重复终局：保留最佳分（不重复推入）", () => {
    const sim = new GameSim(dailySeed(), memoryStorage());
    sim.exec({ action: "start" });
    (sim as unknown as { score: number }).score = 500;
    (sim as unknown as { finalizeRun(m: number, c: null): number }).finalizeRun(1, null);
    // 重开同日再打更高分
    sim.exec({ action: "start" });
    (sim as unknown as { score: number }).score = 800;
    (sim as unknown as { finalizeRun(m: number, c: null): number }).finalizeRun(1, null);
    const d = (sim as unknown as { save: { load(): { dailyHistory: { date: string; score: number }[] } } }).save.load()!;
    expect(d.dailyHistory).toHaveLength(1);
    expect(d.dailyHistory[0]!.score).toBe(800); // 取最佳
  });

  it("非每日局：不推入", () => {
    const sim = new GameSim(1740, memoryStorage());
    sim.exec({ action: "start" });
    (sim as unknown as { score: number }).score = 999;
    (sim as unknown as { finalizeRun(m: number, c: null): number }).finalizeRun(1, null);
    const d = (sim as unknown as { save: { load(): { dailyHistory: { date: string; score: number }[] } } }).save.load()!;
    expect(d.dailyHistory).toHaveLength(0);
  });

  it("normalize：cap 28 + 老存档缺字段默认 []", () => {
    const entries = Array.from({ length: 30 }, (_, i) => ({ date: `2026-01-${String(i + 1).padStart(2, "0")}`, score: i }));
    const s = normalizeSave({ dailyHistory: entries });
    expect(s.dailyHistory).toHaveLength(28);
    expect(normalizeSave({}).dailyHistory).toEqual([]);
  });
});