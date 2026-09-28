import { describe, it, expect } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

describe("R136 诊断：终局路径", () => {
  it("goto_round 13 → advanceRound → final boss", () => {
    const sim = new GameSim(1360, memoryStorage());
    sim.exec({ action: "start" });
    const r = sim.exec({ action: "goto_round", args: { round: 13 } });
    console.log("goto:", JSON.stringify(r.data));
    for (let i = 0; i < 10 && sim.state === "Combat"; i++) sim.tick();
    console.log("round:", sim.round, "enemies:", sim.enemies.map((e) => ({ final: e.final, boss: e.boss, type: e.type })));
    expect(sim.round).toBe(13);
  });
});