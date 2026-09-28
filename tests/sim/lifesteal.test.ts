// R77 血玉匕（命中吸血）：吸血挂 applyHit（举盾免疫不可吸血）/ 回复不超上限 /
// 非吸血武器零回复。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { makeWeapon } from "../../src/sim/weapons";

function daggerSim(seed = 770) {
  const sim = new GameSim(seed, memoryStorage());
  sim.exec({ action: "start" });
  sim.player.weapons = [makeWeapon("xueyubi")]; // 换装单武器：隔离吸血来源
  return sim;
}

describe("R77 血玉匕吸血", () => {
  it("命中吸血：每击 +10（不超上限）", () => {
    const sim = daggerSim();
    sim.player.hp = 50;
    sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1 } });
    const e = sim.enemies[sim.enemies.length - 1]!;
    sim.enemies = [e];
    e.hp = 1e9; e.x = sim.player.x + 24; e.y = sim.player.y;
    const hpBefore = sim.player.hp;
    const dmg: number[] = [];
    sim.onDamage.push((_x, _y, d) => dmg.push(d));
    sim.input = { moveX: 0, moveY: 0, attack: true, dash: false, cycleTarget: false };
    for (let i = 0; i < 120 && dmg.length === 0; i++) {
      e.x = sim.player.x + 24; e.y = sim.player.y; // 每 tick 重钉前方（分离力）
      sim.tick(1 / 60);
    }
    sim.input = { moveX: 0, moveY: 0, attack: false, dash: false, cycleTarget: false };
    expect(dmg.length).toBeGreaterThan(0);
    expect(sim.player.hp).toBe(Math.min(120, hpBefore + 10 * dmg.length));
  });

  it("满血不超上限", () => {
    const sim = daggerSim();
    sim.player.hp = sim.player.maxHp;
    sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1 } });
    const e = sim.enemies[sim.enemies.length - 1]!;
    sim.enemies = [e];
    e.hp = 1e9; e.x = sim.player.x + 24; e.y = sim.player.y;
    sim.input = { moveX: 0, moveY: 0, attack: true, dash: false, cycleTarget: false };
    for (let i = 0; i < 120; i++) {
      e.x = sim.player.x + 24; e.y = sim.player.y;
      sim.tick(1 / 60);
    }
    sim.input = { moveX: 0, moveY: 0, attack: false, dash: false, cycleTarget: false };
    expect(sim.player.hp).toBe(sim.player.maxHp);
  });

  it("非吸血武器（铁剑）：零回复", () => {
    const sim = new GameSim(771, memoryStorage());
    sim.exec({ action: "start" });
    sim.player.hp = 50;
    sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1 } });
    const e = sim.enemies[sim.enemies.length - 1]!;
    sim.enemies = [e];
    e.hp = 1e9; e.x = sim.player.x + 24; e.y = sim.player.y;
    sim.input = { moveX: 0, moveY: 0, attack: true, dash: false, cycleTarget: false };
    for (let i = 0; i < 120; i++) {
      e.x = sim.player.x + 24; e.y = sim.player.y;
      sim.tick(1 / 60);
    }
    sim.input = { moveX: 0, moveY: 0, attack: false, dash: false, cycleTarget: false };
    expect(sim.player.hp).toBe(50);
  });
});