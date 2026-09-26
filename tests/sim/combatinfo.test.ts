import { describe, expect, it } from "vitest";
import { GameSim, type Enemy } from "../../src/sim/game";
import { ACHIEVEMENTS } from "../../src/sim/achievements";
import { memoryStorage } from "../../src/sim/save";

describe("R29 战斗信息补完", () => {
  it("击杀里程碑只在 100/500/1000 触发", () => {
    const sim = new GameSim(291, memoryStorage());
    sim.exec({ action: "start" });
    const got: string[] = [];
    sim.onEvent((e) => {
      if (e.event === "kill_milestone") got.push(`m${e.data.count}`);
      if (e.event === "kill_legend") got.push("legend");
    });
    const inner = sim as unknown as { onEnemyKilled(e: Enemy): void };
    for (let i = 0; i < 1001; i++) {
      sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1 } });
      inner.onEnemyKilled(sim.enemies[sim.enemies.length - 1]);
    }
    expect(got).toEqual(["m100", "m500", "legend"]);
    expect(sim.kills).toBe(1001);
  });

  it("千人斩成就随 tick 授予", () => {
    const sim = new GameSim(291, memoryStorage());
    sim.exec({ action: "start" });
    const inner = sim as unknown as { onEnemyKilled(e: Enemy): void };
    for (let i = 0; i < 1000; i++) {
      sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1 } });
      inner.onEnemyKilled(sim.enemies[sim.enemies.length - 1]);
    }
    sim.tick();
    expect(sim.achievements.unlocked.has("slayer_1000")).toBe(true);
    expect(ACHIEVEMENTS.length).toBe(25); // R30 藏珍满架
  });
});
