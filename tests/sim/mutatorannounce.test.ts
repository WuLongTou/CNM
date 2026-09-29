// R153 当日变异开局公示：mutator_start 事件 emit 验证（UI 路由为标准 notify 模式）。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { dailySeed } from "../../src/sim/daily";

describe("R153 当日变异开局公示", () => {
  it("每日局开局：emit mutator_start（name/desc）", () => {
    const sim = new GameSim(dailySeed(), memoryStorage());
    const evs: Array<{ name: string; desc: string }> = [];
    sim.onEvent((e) => { if (e.event === "mutator_start") evs.push(e.data as unknown as { name: string; desc: string }); });
    sim.exec({ action: "start" });
    expect(evs).toHaveLength(1);
    expect(evs[0]!.name.length).toBeGreaterThan(0);
    expect(evs[0]!.desc.length).toBeGreaterThan(0);
  });

  it("非每日局：不 emit", () => {
    const sim = new GameSim(1530, memoryStorage());
    const evs: unknown[] = [];
    sim.onEvent((e) => { if (e.event === "mutator_start") evs.push(e.data); });
    sim.exec({ action: "start" });
    expect(evs).toHaveLength(0);
  });

  it("Combat 态重复调用 start：状态机挡住不重复 emit", () => {
    const sim = new GameSim(dailySeed(), memoryStorage());
    let count = 0;
    sim.onEvent((e) => { if (e.event === "mutator_start") count++; });
    sim.exec({ action: "start" });
    sim.exec({ action: "start" }); // 状态机已 Combat：start 分支不进
    expect(count).toBe(1);
  });
});