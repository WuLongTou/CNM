import { describe, expect, it } from "vitest";
import { GameSim, type Enemy } from "../../src/sim/game";
import { ACHIEVEMENTS } from "../../src/sim/achievements";
import { memoryStorage } from "../../src/sim/save";

function readySim(seed = 241) {
  const sim = new GameSim(seed, memoryStorage());
  sim.exec({ action: "start" });
  return sim;
}

/** 连打沙包 enemy n 次，收集事件名序列。 */
function punch(sim: GameSim, n: number): string[] {
  sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1 } });
  const e = sim.enemies[sim.enemies.length - 1];
  e.hp = 1e9; e.maxHp = 1e9;
  e.speed = 0; e.damage = 0;
  const got: string[] = [];
  sim.onEvent((ev) => {
    if (ev.event === "combo_milestone") got.push(`m${ev.data.count}`);
    if (ev.event === "combo_century") got.push("century");
  });
  const inner = sim as unknown as {
    hitEnemy(e: Enemy, r: { dmg: number; crit: boolean }, ux: number, uy: number, fx: object): void;
  };
  for (let i = 0; i < n; i++) inner.hitEnemy(e, { dmg: 1, crit: false }, 1, 0, {});
  return got;
}

describe("R24 处决播报", () => {
  it("里程碑只在 3/10/25/50/100 触发", () => {
    const sim = readySim();
    const got = punch(sim, 101);
    expect(got).toEqual(["m3", "m10", "m25", "m50", "century"]);
  });

  it("百连斩成就随 tick 授予", () => {
    const sim = readySim();
    punch(sim, 100);
    sim.tick();
    expect(sim.achievements.unlocked.has("combo_100")).toBe(true);
    expect(ACHIEVEMENTS.length).toBe(36); // R47 妖王猎手
  });

  it("精英首杀播报一次；妖王不算", () => {
    const sim = readySim();
    const evs: string[] = [];
    sim.onEvent((e) => { if (e.event === "first_elite") evs.push("first"); });
    sim.exec({ action: "spawn", args: { type: 10, near: "player", count: 1, boss: true } });
    const b = sim.enemies[sim.enemies.length - 1];
    (sim as unknown as { onEnemyKilled(e: Enemy): void }).onEnemyKilled(b);
    expect(evs).toEqual([]);
    sim.exec({ action: "spawn", args: { type: 0, near: "player", count: 1, elite: true } });
    const e1 = sim.enemies[sim.enemies.length - 1];
    (sim as unknown as { onEnemyKilled(e: Enemy): void }).onEnemyKilled(e1);
    sim.exec({ action: "spawn", args: { type: 1, near: "player", count: 1, elite: true } });
    const e2 = sim.enemies[sim.enemies.length - 1];
    (sim as unknown as { onEnemyKilled(e: Enemy): void }).onEnemyKilled(e2);
    expect(evs).toEqual(["first"]);
  });
});
