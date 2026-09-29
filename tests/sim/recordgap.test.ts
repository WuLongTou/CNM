// R166 未破纪录差距：recordGap 字段 + debrief 提示（新纪录时不显）。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

describe("R166 未破纪录差距", () => {
  it("低分局：recordGap = 差值；破分局：0", () => {
    const store = memoryStorage(); // 两局共用同一存储（模拟同一玩家）
    const sim = new GameSim(1660, store);
    sim.exec({ action: "start" });
    (sim as unknown as { score: number }).score = 10;
    (sim as unknown as { finalizeRun(m: number, c: null): number }).finalizeRun(1, null);
    const s1 = sim as unknown as { newRecord: boolean; recordGap: number };
    expect(s1.newRecord).toBe(true); // 首局即纪录
    expect(s1.recordGap).toBe(0);

    const sim2 = new GameSim(1661, store);
    sim2.exec({ action: "start" });
    (sim2 as unknown as { score: number }).score = 5;
    (sim2 as unknown as { finalizeRun(m: number, c: null): number }).finalizeRun(1, null);
    const s2 = sim2 as unknown as { newRecord: boolean; recordGap: number };
    expect(s2.newRecord).toBe(false);
    expect(s2.recordGap).toBe(5); // 10 - 5
  });
});