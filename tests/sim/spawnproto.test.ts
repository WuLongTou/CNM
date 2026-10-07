// R207 spawn 协议类型解析健壮化：中文名解析（"瘴妖"→5）/ 未知名称与越界数值
// 显式报错（ok:false，不再静默退化成权重随机游魂）/ random 与 basic 别名保持。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

function makeSim(seed = 20700) {
  const sim = new GameSim(seed, memoryStorage());
  sim.exec({ action: "start" });
  return sim;
}

describe("R207 spawn 协议类型解析", () => {
  it("中文名解析：spawn \"瘴妖\" 生成 type 5（此前静默退化成游魂）", () => {
    const sim = makeSim();
    sim.exec({ action: "spawn", args: { type: "瘴妖", count: 1, near: "player" } });
    const e = sim.enemies[sim.enemies.length - 1];
    expect(e.type).toBe(5);
  });

  it("全部 14 个中文名均可解析", () => {
    const sim = makeSim(20701);
    sim.exec({ action: "spawn", args: { type: "游魂", count: 1, near: "player" } });
    sim.exec({ action: "spawn", args: { type: "蛟", count: 1, near: "player" } });
    sim.exec({ action: "spawn", args: { type: "蚀群", count: 1, near: "player" } });
    const types = sim.enemies.map((e) => e.type);
    expect(types).toContain(0);
    expect(types).toContain(12);
    expect(types).toContain(13);
  });

  it("未知名称：exec 返回 ok:false 且不生成实体", () => {
    const sim = makeSim(20702);
    const r = sim.exec({ action: "spawn", args: { type: "不存在的妖", count: 1 } });
    expect(r.ok).toBe(false);
    expect(String(r.error)).toContain("未知妖物类型");
    expect(sim.enemies.length).toBe(0);
  });

  it("越界数值：显式报错不生成坏实体", () => {
    const sim = makeSim(20703);
    const r = sim.exec({ action: "spawn", args: { type: 99, count: 1 } });
    expect(r.ok).toBe(false);
    expect(sim.enemies.length).toBe(0);
  });

  it("random 与 basic 别名保持原语义", () => {
    const sim = makeSim(20704);
    sim.exec({ action: "spawn", args: { type: "random", count: 1, near: "player" } });
    sim.exec({ action: "spawn", args: { type: "basic", count: 1, near: "player" } });
    expect(sim.enemies.length).toBe(2);
    expect(sim.enemies[1]!.type).toBe(0);
  });
});
