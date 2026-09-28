// R102 本地战绩历史：finalize 推入 / normalize 迁移 / 最近 10 局上限。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { dailySeed } from "../../src/sim/daily";

describe("R102 本地战绩历史", () => {
  it("终局推入一条记录（字段齐备）", () => {
    const sim = new GameSim(1020, memoryStorage());
    sim.exec({ action: "start" });
    (sim as unknown as { finalizeRun(m: number, c: null): number }).finalizeRun(1, null);
    const d = sim.status() as unknown as { run_history: Array<Record<string, unknown>> };
    expect(d.run_history).toHaveLength(1);
    const r = d.run_history[0]!;
    expect(r.score).toBe(0);
    expect(r.roleName).toBe("剑修");
    expect(r.cleared).toBe(false);
    expect(r.seed).toBe(1020);
  });

  it("最近 10 局上限：11 局只留最近 10（新的在前）", () => {
    const sim = new GameSim(1021, memoryStorage());
    sim.exec({ action: "start" });
    const f = sim as unknown as { finalizeRun(m: number, c: null): number };
    for (let i = 0; i < 11; i++) {
      sim.score = i + 1;
      f.finalizeRun(1, null);
      sim.exec({ action: "start" });
    }
    const d = sim.status() as unknown as { run_history: Array<{ score: number }> };
    expect(d.run_history).toHaveLength(10);
    expect(d.run_history[0]!.score).toBe(11); // 最新在前
  });

  it("每日局 mode 标注 每日妖潮", () => {
    const sim = new GameSim(dailySeed(), memoryStorage());
    sim.exec({ action: "start" });
    (sim as unknown as { finalizeRun(m: number, c: null): number }).finalizeRun(1, null);
    const d = sim.status() as unknown as { run_history: Array<{ mode: string }> };
    expect(d.run_history[0]!.mode).toBe("每日妖潮");
  });
});