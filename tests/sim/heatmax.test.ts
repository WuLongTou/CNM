// R71 炼狱修罗（三誓满档通关）：终局直授语义复用 oathbound 通路（onVictory 探针）。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { PACT_MAX_TIER, PACT_IDS } from "../../src/sim/pacts";

function maxHeatSim(seed = 710) {
  const tiers: Partial<Record<string, number>> = {};
  for (const id of PACT_IDS) tiers[id] = PACT_MAX_TIER;
  const sim = new GameSim(seed, memoryStorage(), "sword", 0, tiers);
  sim.exec({ action: "start" });
  return sim;
}

describe("R71 炼狱修罗（三誓满档通关）", () => {
  it("三誓满档通关：直授 heat_max", () => {
    const sim = maxHeatSim();
    (sim as unknown as { onVictory(): number }).onVictory();
    expect(sim.achievements.unlocked.has("heat_max")).toBe(true);
    expect(sim.achievements.unlocked.has("oathbound")).toBe(true); // 满档含立誓
  });

  it("未满档通关：只授 oathbound 不授 heat_max", () => {
    const sim = new GameSim(711, memoryStorage(), "sword", 0, { blood: 2, horde: 3, frail: 3 });
    sim.exec({ action: "start" });
    (sim as unknown as { onVictory(): number }).onVictory();
    expect(sim.achievements.unlocked.has("oathbound")).toBe(true);
    expect(sim.achievements.unlocked.has("heat_max")).toBe(false);
  });

  it("陨落不授（通关限定）", () => {
    const sim = maxHeatSim();
    sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1 } });
    sim.player.hp = 1;
    const e = sim.enemies[sim.enemies.length - 1]!;
    e.x = sim.player.x;
    e.y = sim.player.y;
    for (let i = 0; i < 300 && sim.state !== "GameOver"; i++) sim.tick(1 / 60);
    expect(sim.state).toBe("GameOver");
    expect(sim.achievements.unlocked.has("heat_max")).toBe(false);
  });
});