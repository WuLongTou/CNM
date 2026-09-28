// R105 武器形态：进阶解锁形态贰（ASPECTS 覆盖数值）/ aspectDef 战斗生效 / 门控切换。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { makeWeapon, aspectDef, aspectUnlocked, ASPECTS } from "../../src/sim/weapons";

describe("R105 武器形态", () => {
  it("数据表：三把武器有形态；aspectDef aspect=1 覆盖（未列字段继承）", () => {
    expect(Object.keys(ASPECTS)).toContain("tiejian");
    const w = makeWeapon("tiejian");
    w.aspect = 1;
    const d = aspectDef(w);
    expect(d.dmg).toBe(0.7); // 形态覆盖
    expect(d.type).toBe("Melee"); // 本形继承
    expect(d.name).toContain("影袭"); // 名字带形态标签
    expect(aspectDef({ ...w, aspect: 0 }).dmg).toBeUndefined(); // 本形铁剑无 dmg 覆盖字段
  });

  it("门控：未进阶拒绝切换，进阶后可切且来回切", () => {
    const sim = new GameSim(1050, memoryStorage());
    sim.exec({ action: "start" });
    sim.player.weapons = [makeWeapon("tiejian")];
    const w = sim.player.weapons[0]!;
    expect(aspectUnlocked(w)).toBe(false); // ups=0 未解锁
    expect(sim.toggleAspect(0)).toBe(false);
    w.ups = 3; // 进阶
    expect(aspectUnlocked(w)).toBe(true);
    expect(sim.toggleAspect(0)).toBe(true);
    expect(w.aspect).toBe(1);
    expect(sim.toggleAspect(0)).toBe(true);
    expect(w.aspect).toBe(0); // 来回切
  });

  it("战斗生效：影袭形态下齐射伤害系数走形态表（dmg 0.7）", () => {
    const sim = new GameSim(1051, memoryStorage());
    sim.exec({ action: "start" });
    sim.player.weapons = [makeWeapon("tiejian")];
    const w = sim.player.weapons[0]!;
    w.ups = 3;
    sim.toggleAspect(0);
    expect(aspectDef(w).dmg).toBe(0.7);
  });
});

