// R94 毒 DoT 武器归因：applyHit 存 psnSrcW，DoT tick 透传 → 淬毒系伤害计入来源武器。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { makeWeapon } from "../../src/sim/weapons";

function poisonSim(seed = 940) {
  const sim = new GameSim(seed, memoryStorage());
  sim.exec({ action: "start" });
  sim.player.weapons = [makeWeapon("duandao")]; // 淬毒短刀 psn 10
  return sim;
}

describe("R94 毒 DoT 武器归因", () => {
  it("毒 tick 计入来源武器 dmgDealt", () => {
    const sim = poisonSim();
    sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1 } });
    const e = sim.enemies[sim.enemies.length - 1]!;
    sim.enemies = [e];
    e.hp = 1e9; e.x = sim.player.x + 24; e.y = sim.player.y;
    const w = sim.player.weapons[0]!;
    const before = w.dmgDealt;
    sim.input = { moveX: 0, moveY: 0, attack: true, dash: false, cycleTarget: false };
    for (let i = 0; i < 240 && e.psnT <= 0; i++) {
      e.x = sim.player.x + 24; e.y = sim.player.y;
      sim.tick(1 / 60);
    }
    sim.input = { moveX: 0, moveY: 0, attack: false, dash: false, cycleTarget: false };
    expect(e.psnT).toBeGreaterThan(0); // 中毒已上
    const ticks = 120; // 2s 毒 tick
    for (let i = 0; i < ticks; i++) sim.tick(1 / 60);
    expect(w.dmgDealt).toBeGreaterThan(before); // 毒伤计入来源武器
  });
});