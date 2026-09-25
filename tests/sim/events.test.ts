import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { CFG, EnemyType } from "../../src/sim/config";
import { memoryStorage } from "../../src/sim/save";

function readySim(seed = 121) {
  const sim = new GameSim(seed, memoryStorage());
  sim.exec({ action: "start" });
  return sim;
}

/** 预填怪海压住底噪供给，使触发 tick 内只有事件刷怪（差分精确）。 */
function saturate(sim: GameSim) {
  sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 30 } });
}

/** 把事件计时拨到触发边缘（私有字段直写；跑满 100s 等价但太慢）。 */
function arm(sim: GameSim) {
  (sim as unknown as { eventTimer: number }).eventTimer = 0.01;
}

function lastEvent(sim: GameSim) {
  const found = sim.drainEvents().filter((e) => e.event === "map_event");
  return found[found.length - 1];
}

describe("R13 妖潮异变", () => {
  it("首发 100s，触发前静默", () => {
    const sim = readySim();
    expect(sim.eventTimer).toBe(CFG.Events.FirstAt);
    expect(sim.eventCount).toBe(0);
    for (let i = 0; i < 60; i++) sim.tick();
    expect(sim.eventCount).toBe(0);
    expect(sim.drainEvents().some((e) => e.event === "map_event")).toBe(false);
    expect(sim.status()).toMatchObject({ next_event_in: expect.any(Number) });
  });

  it("首发围猎：环形 6 只快攻群（首幕）", () => {
    const sim = readySim();
    saturate(sim);
    const fastBefore = sim.enemies.filter((e) => e.type === EnemyType.Fast).length;
    arm(sim);
    sim.tick();
    expect(sim.eventCount).toBe(1);
    expect(lastEvent(sim)?.data).toMatchObject({ kind: "surge", count: 6 });
    const fresh = sim.enemies.filter((e) => e.type === EnemyType.Fast);
    expect(fresh.length - fastBefore).toBe(6);
    // 环形落点：距玩家约 500（世界钳制内）
    for (const e of fresh.slice(-6)) {
      expect(Math.hypot(e.x - sim.player.x, e.y - sim.player.y)).toBeLessThanOrEqual(560);
    }
    // 间隔续杯 130s
    expect(sim.eventTimer).toBeGreaterThan(129);
    expect(sim.eventTimer).toBeLessThanOrEqual(130);
  });

  it("次发精锐：近点 1 精英", () => {
    const sim = readySim();
    saturate(sim);
    arm(sim); sim.tick(); // surge
    sim.drainEvents();
    const eliteBefore = sim.enemies.filter((e) => e.elite).length;
    arm(sim); sim.tick(); // hunt
    expect(sim.eventCount).toBe(2);
    expect(lastEvent(sim)?.data).toMatchObject({ kind: "hunt", count: 1 });
    expect(sim.enemies.filter((e) => e.elite).length - eliteBefore).toBe(1);
  });

  it("三发宝藏：玩家两侧 2 普通箱", () => {
    const sim = readySim();
    arm(sim); sim.tick(); // surge
    arm(sim); sim.tick(); // hunt
    sim.drainEvents();
    const closedBefore = sim.chests.filter((c) => !c.opened).length;
    arm(sim); sim.tick(); // cache
    expect(sim.eventCount).toBe(3);
    expect(lastEvent(sim)?.data).toMatchObject({ kind: "cache", count: 2 });
    const closed = sim.chests.filter((c) => !c.opened);
    expect(closed.length - closedBefore).toBe(2);
    expect(closed.slice(-2).every((c) => !c.elite)).toBe(true);
  });

  it("五发回到围猎（R14 起四事件轮转循环）", () => {
    const sim = readySim();
    saturate(sim);
    const kinds: string[] = [];
    for (let i = 0; i < 5; i++) {
      arm(sim); sim.tick();
      kinds.push((lastEvent(sim)?.data as { kind: string }).kind);
    }
    expect(kinds).toEqual(["surge", "hunt", "cache", "goblin", "surge"]);
  });
});
