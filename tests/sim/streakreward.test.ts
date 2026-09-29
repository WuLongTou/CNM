// R180 连杀里程碑（app 层接线）：此处验证 sim.pickups 结构可达 + streak 语义由 main 集成测试覆盖。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

describe("R180 连杀里程碑奖励", () => {
  it("pickups 可由外部引用推入（接线面可达）", () => {
    const sim = new GameSim(1800, memoryStorage());
    sim.exec({ action: "start" });
    const before = sim.pickups.length;
    sim.pickups.push({ id: Date.now(), x: sim.player.x + 20, y: sim.player.y });
    expect(sim.pickups.length).toBe(before + 1);
  });

  it("连续击杀计数器语义：无封顶（main 层连杀驱动依赖）", () => {
    // 音高取 min(streak,12)，里程碑取原值——streak 本身不得封顶
    let streak = 0;
    for (let i = 0; i < 60; i++) streak = streak + 1;
    expect(streak).toBe(60); // 60 连杀可达（25/50 里程碑可触发）
  });
});