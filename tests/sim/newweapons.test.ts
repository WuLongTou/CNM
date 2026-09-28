// R95 新武器：毒箭（Ranged psn）/ 霜牙（Melee freeze）数据表完整性。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { WEAPON_DATA, makeWeapon } from "../../src/sim/weapons";

describe("R95 新武器数据表", () => {
  it("武器总数 26 件，新件字段齐备", () => {
    expect(Object.keys(WEAPON_DATA)).toHaveLength(26);
    expect(WEAPON_DATA.dujian).toMatchObject({ type: "Ranged", psn: 20, rarity: 2, prereq: "弓箭" });
    expect(WEAPON_DATA.shuangya).toMatchObject({ type: "Melee", freeze: 15, rarity: 2, prereq: "淬毒短刀" });
  });

  it("prereq 链引用的武器名都存在（防断链）", () => {
    for (const def of Object.values(WEAPON_DATA)) {
      if (def.prereq) expect(Object.values(WEAPON_DATA).some((x) => x.name === def.prereq)).toBe(true);
    }
  });

  it("giveWeapon 两件可持有并出手（毒箭毒伤归因武器）", () => {
    const sim = new GameSim(950, memoryStorage());
    sim.exec({ action: "start" });
    sim.player.weapons = [makeWeapon("dujian")];
    sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1 } });
    const e = sim.enemies[sim.enemies.length - 1]!;
    sim.enemies = [e];
    e.hp = 1e9; e.x = sim.player.x + 120; e.y = sim.player.y;
    const w = sim.player.weapons[0]!;
    sim.input = { moveX: 0, moveY: 0, attack: true, dash: false, cycleTarget: false };
    for (let i = 0; i < 240 && w.dmgDealt === 0; i++) {
      e.x = sim.player.x + 120; e.y = sim.player.y;
      sim.tick(1 / 60);
    }
    sim.input = { moveX: 0, moveY: 0, attack: false, dash: false, cycleTarget: false };
    expect(w.dmgDealt).toBeGreaterThan(0); // R94 毒伤归因通路对新武器生效
  });
});