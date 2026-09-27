import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

describe("R43 异变回顾", () => {
  it("触发异变按种类计数；快照可读", () => {
    const sim = new GameSim(431, memoryStorage());
    sim.exec({ action: "start" });
    sim.enemies.length = 0;
    sim.terrain.altars.length = 0;
    // 4 次异变 = 轮转一整圈（surge/hunt/cache/goblin 各 1）
    for (let i = 0; i < 4; i++) {
      sim.eventTimer = 0.01;
      sim.tick(); sim.tick();
    }
    expect(sim.eventTally).toEqual({ surge: 1, hunt: 1, cache: 1, goblin: 1 });
    const snap = sim.status() as { event_tally: Record<string, number> };
    expect(snap.event_tally).toEqual({ surge: 1, hunt: 1, cache: 1, goblin: 1 });
  });
});
