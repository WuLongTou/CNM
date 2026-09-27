import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { dailyKey, dailySeed } from "../../src/sim/daily";
import { memoryStorage } from "../../src/sim/save";

function dailyRun(save: ReturnType<typeof memoryStorage>, score: number, events: string[] = []) {
  const sim = new GameSim(dailySeed(), save);
  sim.onEvent((e) => events.push(e.event));
  sim.score = score;
  sim.kills = 1;
  (sim as unknown as { onVictory(): number }).onVictory();
  return sim;
}

describe("R40 今日最佳刷新反馈", () => {
  it("破纪录发 daily_record；首跑不播；未破不播", () => {
    const save = memoryStorage();
    const ev1: string[] = [];
    dailyRun(save, 800, ev1); // 首跑：老榜为空 → 不播
    expect(save.data.dailyBest).toEqual({ date: dailyKey(), score: 800 });
    expect(ev1).not.toContain("daily_record");

    const ev2: string[] = [];
    dailyRun(save, 500, ev2); // 未破 → 不播
    expect(save.data.dailyBest.score).toBe(800);
    expect(ev2).not.toContain("daily_record");

    const ev3: string[] = [];
    dailyRun(save, 1200, ev3); // 破纪录 → 播
    expect(save.data.dailyBest.score).toBe(1200);
    expect(ev3).toContain("daily_record");
  });
});
