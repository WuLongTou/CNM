import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { ACHIEVEMENTS } from "../../src/sim/achievements";
import { memoryStorage } from "../../src/sim/save";

function rushSim(seed: number) {
  const sim = new GameSim(seed, memoryStorage(), "sword", 0, [], "boss_rush");
  sim.exec({ action: "start" });
  return sim;
}

describe("R47 妖王猎手", () => {
  it("连战讨伐 5 妖王即授勋；4 只不授", () => {
    const sim = rushSim(471);
    const inner = sim as unknown as { onEnemyKilled(e: { id: number; boss: boolean }): void };
    for (let i = 0; i < 4; i++) inner.onEnemyKilled({ id: 100 + i, boss: true });
    sim.tick();
    expect(sim.achievements.unlocked.has("boss_hunter")).toBe(false);
    inner.onEnemyKilled({ id: 200, boss: true });
    sim.tick();
    expect(sim.achievements.unlocked.has("boss_hunter")).toBe(true);
    expect(sim.bossRushKills).toBe(5);
    expect(ACHIEVEMENTS.length).toBe(44); // R205 战功中段 +2
  });

  it("普通模式讨伐妖王不计入", () => {
    const sim = new GameSim(472, memoryStorage());
    sim.exec({ action: "start" });
    sim.enemies.length = 0;
    sim.terrain.altars.length = 0;
    const inner = sim as unknown as { onEnemyKilled(e: { id: number; boss: boolean }): void };
    for (let i = 0; i < 6; i++) inner.onEnemyKilled({ id: 300 + i, boss: true });
    sim.tick();
    expect(sim.achievements.unlocked.has("boss_hunter")).toBe(false);
  });
});
