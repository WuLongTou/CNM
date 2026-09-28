// R88 Boss Rush 分阶用时：每王讨伐用时记录 / 最快讨伐陈列。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

function rushSim(seed = 880) {
  const sim = new GameSim(seed, memoryStorage(), "sword", 0, {}, "boss_rush");
  sim.exec({ action: "start" });
  return sim;
}

function killBoss(sim: GameSim): void {
  sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1, boss: true } });
  const e = sim.enemies[sim.enemies.length - 1]!;
  (sim as unknown as { dealDamage(e: unknown, d: number): void }).dealDamage(e, 999999);
}

describe("R88 Boss Rush 分阶用时", () => {
  it("击杀记录用时：二王两笔正值", () => {
    const sim = rushSim();
    sim.survivalTime = 30;
    killBoss(sim);
    sim.survivalTime = 75;
    killBoss(sim);
    expect(sim.bossRushKillTimes).toHaveLength(2);
    expect(sim.bossRushKillTimes[0]).toBeCloseTo(30, 6);
    expect(sim.bossRushKillTimes[1]).toBeCloseTo(45, 6);
  });

  it("status 回报 rush_fastest（最快讨伐）", () => {
    const sim = rushSim();
    sim.survivalTime = 50;
    killBoss(sim);
    sim.survivalTime = 55;
    killBoss(sim);
    expect(sim.status().rush_fastest).toBe(5);
  });

  it("普通模式不记录（bossRushKillTimes 恒空）", () => {
    const sim = new GameSim(881, memoryStorage());
    sim.exec({ action: "start" });
    sim.survivalTime = 100;
    sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1, boss: true } });
    const e = sim.enemies[sim.enemies.length - 1]!;
    (sim as unknown as { dealDamage(e: unknown, d: number): void }).dealDamage(e, 999999);
    expect(sim.bossRushKillTimes).toHaveLength(0);
  });
});