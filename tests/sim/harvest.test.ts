// R211 采集反馈：事件载荷（坐标 + 掉落明细）与库存入账（UI 飘字据此定位报数）。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

type Ev = { event: string; data: Record<string, unknown> };

describe("R211 采集事件", () => {
  const setup = () => {
    const sim = new GameSim(2110, memoryStorage());
    sim.exec({ action: "start" });
    sim.terrain.obstacles.length = 0; // 清掉随机地形，保证确定性
    return sim;
  };
  const put = (sim: GameSim, kind: "tree" | "rock" | "bush", dx: number, hp = 1) => {
    sim.terrain.obstacles.push({
      id: 9001, x: sim.player.x + dx, y: sim.player.y, r: 20, kind, hp, dead: false,
    });
  };
  const harvest = (sim: GameSim, range: number) =>
    (sim as unknown as { harvestInRange(r: number): void }).harvestInRange(range);

  it("砍倒树：事件带坐标与掉落明细，木材入账", () => {
    const sim = setup();
    const events: Ev[] = [];
    sim.onEvent((e) => events.push(e as unknown as Ev));
    put(sim, "tree", 30);

    harvest(sim, 100);

    const ev = events.find((x) => x.event === "harvested");
    expect(ev).toBeTruthy();
    expect(ev!.data.kind).toBe("tree");
    expect(typeof ev!.data.x).toBe("number");
    expect(typeof ev!.data.y).toBe("number");
    expect(ev!.data.drops).toEqual({ wood: 2 });
    expect(sim.inventory.counts.wood).toBe(2);
  });

  it("灌木给纤维 1 + 浆果 2；范围外的节点不动", () => {
    const sim = setup();
    put(sim, "bush", 30);
    put(sim, "rock", 500);

    harvest(sim, 100);

    expect(sim.inventory.counts.fiber).toBe(1);
    expect(sim.inventory.counts.berry).toBe(2);
    expect(sim.inventory.counts.stone).toBe(0);
  });
});
