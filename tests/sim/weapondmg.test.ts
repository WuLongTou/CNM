// R83 武器伤害统计：近战/弹道/地雷归因、毒 DoT 只计总计、装备面板占比。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { makeWeapon } from "../../src/sim/weapons";

function meleeSim(seed = 830) {
  const sim = new GameSim(seed, memoryStorage());
  sim.exec({ action: "start" });
  sim.player.weapons = [makeWeapon("tiejian")];
  return sim;
}

function swingUntil(sim: GameSim, times: number): void {
  sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1 } });
  const e = sim.enemies[sim.enemies.length - 1]!;
  sim.enemies = [e];
  e.hp = 1e9; e.x = sim.player.x + 24; e.y = sim.player.y;
  const w = sim.player.weapons[0]!;
  const before = w.dmgDealt;
  sim.input = { moveX: 0, moveY: 0, attack: true, dash: false, cycleTarget: false };
  for (let i = 0; i < 120 && w.dmgDealt - before < times; i++) {
    e.x = sim.player.x + 24; e.y = sim.player.y;
    sim.tick(1 / 60);
  }
  sim.input = { moveX: 0, moveY: 0, attack: false, dash: false, cycleTarget: false };
}

describe("R83 武器伤害统计", () => {
  it("近战归因：铁剑挥刀累计 dmgDealt，总计同步", () => {
    const sim = meleeSim();
    swingUntil(sim, 3);
    const w = sim.player.weapons[0]!;
    expect(w.dmgDealt).toBeGreaterThan(0);
    expect(sim.dmgDealtTotal).toBeGreaterThanOrEqual(w.dmgDealt);
  });

  it("弹道归因：弓箭命中计入来源武器", () => {
    const sim = new GameSim(831, memoryStorage());
    sim.exec({ action: "start" });
    sim.player.weapons = [makeWeapon("gongjian")];
    const w = sim.player.weapons[0]!;
    sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1 } });
    const e = sim.enemies[sim.enemies.length - 1]!;
    sim.enemies = [e];
    e.hp = 1e9; e.x = sim.player.x + 120; e.y = sim.player.y;
    sim.input = { moveX: 0, moveY: 0, attack: true, dash: false, cycleTarget: false };
    for (let i = 0; i < 240 && w.dmgDealt === 0; i++) {
      e.x = sim.player.x + 120; e.y = sim.player.y;
      sim.tick(1 / 60);
    }
    sim.input = { moveX: 0, moveY: 0, attack: false, dash: false, cycleTarget: false };
    expect(w.dmgDealt).toBeGreaterThan(0);
  });

  it("举盾甲妖免伤不计数（dealDamage 早退）", () => {
    const sim = meleeSim();
    sim.exec({ action: "spawn", args: { type: 11, near: "player", count: 1 } }); // Knight（举盾甲妖）
    const e = sim.enemies[sim.enemies.length - 1]!;
    sim.enemies = [e];
    (e as unknown as { blockT: number }).blockT = 999; // 常驻举盾
    e.x = sim.player.x + 24; e.y = sim.player.y;
    const w = sim.player.weapons[0]!;
    const before = w.dmgDealt;
    sim.input = { moveX: 0, moveY: 0, attack: true, dash: false, cycleTarget: false };
    for (let i = 0; i < 120; i++) {
      e.x = sim.player.x + 24; e.y = sim.player.y;
      sim.tick(1 / 60);
    }
    sim.input = { moveX: 0, moveY: 0, attack: false, dash: false, cycleTarget: false };
    expect(w.dmgDealt).toBe(before); // 免伤不计
  });

  it("status 回报 dmg_dealt_total", () => {
    const sim = meleeSim();
    swingUntil(sim, 2);
    expect(sim.status().dmg_dealt_total).toBeGreaterThan(0);
  });
});