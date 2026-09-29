// R154 战绩历史成就快照：runHistory 推入 achievements 名单 + 战绩 tab 列。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";


describe("R154 战绩历史成就快照", () => {
  it("终局推入：achievements 名单入记录", () => {
    const sim = new GameSim(1540, memoryStorage());
    sim.exec({ action: "start" });
    sim.achievements.grant("first_blood");
    sim.achievements.grant("slayer_50");
    (sim as unknown as { finalizeRun(m: number, c: null): number }).finalizeRun(1, null);
    const d = sim.status() as unknown as { run_history: Array<{ achievements: string[] }> };
    expect(d.run_history[0]!.achievements).toEqual(["初试锋芒", "除妖行者"]);
  });

  it("无成就局：空数组", () => {
    const sim = new GameSim(1541, memoryStorage());
    sim.exec({ action: "start" });
    (sim as unknown as { finalizeRun(m: number, c: null): number }).finalizeRun(1, null);
    const d = sim.status() as unknown as { run_history: Array<{ achievements: string[] }> };
    expect(d.run_history[0]!.achievements).toEqual([]);
  });
});