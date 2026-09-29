// R195 武器击杀统计：击杀归因/finalize 并入/normalize 迁移/装备显示数据。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage, normalizeSave } from "../../src/sim/save";

describe("R195 武器击杀统计", () => {
  it("击杀归因：挥砍武器击杀计入 runWeaponKills[defId]", () => {
    const sim = new GameSim(1950, memoryStorage());
    sim.exec({ action: "start" });
    // 敌人钉在面前，直接用铁剑（defId=tiejian）挥砍一次击杀
    sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1 } });
    const e = sim.enemies[sim.enemies.length - 1];
    e.x = sim.player.x + 40; e.y = sim.player.y;
    e.hp = 1;
    const w = sim.player.weapons[sim.player.weapons.length - 1];
    sim.onAttack.push(() => {}); // no-op 占位
    // 直调挥砍伤害路径：applyHit 走 w 来源
    (sim as unknown as { applyHit(x: number, y: number, e: unknown, d: number, fx: object, ux?: number, uy?: number, w?: unknown): void })
      .applyHit(e.x, e.y, e, 99, { splash: 0, psn: 0, freeze: 0 }, 1, 0, w);
    const run = (sim as unknown as { runWeaponKills: Record<string, number> }).runWeaponKills;
    expect(run[w.defId] ?? 0).toBe(1);
  });

  it("finalize 并入生涯：runWeaponKills → save.weaponKills 累加", () => {
    const sim = new GameSim(1951, memoryStorage());
    sim.exec({ action: "start" });
    (sim as unknown as { runWeaponKills: Record<string, number> }).runWeaponKills = { tiejian: 7, fazhang: 3 };
    (sim as unknown as { finalizeRun(m: number, c: null): number }).finalizeRun(1, null);
    const st = sim.status() as unknown as { weapon_kills: Record<string, number> };
    expect(st.weapon_kills.tiejian).toBe(7);
    expect(st.weapon_kills.fazhang).toBe(3);
  });

  it("normalize 迁移：老存档缺字段默认 {}", () => {
    const s = normalizeSave({ highScore: 1 });
    expect(s.weaponKills).toEqual({});
  });
});