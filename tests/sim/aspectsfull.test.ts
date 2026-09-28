// R107 形态第三批：剩余 14 把全铺完（ASPECTS 26/26 全覆盖）。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { WEAPON_DATA, makeWeapon, aspectDef, ASPECTS, aspectUnlocked } from "../../src/sim/weapons";

describe("R107 形态全铺完", () => {
  it("26 把武器全部有形态（表键数=武器数）", () => {
    expect(Object.keys(ASPECTS).length).toBe(Object.keys(WEAPON_DATA).length);
    for (const id of Object.keys(WEAPON_DATA)) {
      expect(ASPECTS[id]).toBeTruthy();
      expect(ASPECTS[id]!.desc.length).toBeGreaterThan(4);
    }
  });

  it("覆盖抽查：血祭 steal 20 / 绝对零度 freeze 40 / 炼狱 psn 50", () => {
    const xy = makeWeapon("xueyubi"); xy.aspect = 1;
    expect(aspectDef(xy).steal).toBe(20);
    const bs = makeWeapon("bingshuangfazhang"); bs.aspect = 1;
    expect(aspectDef(bs).freeze).toBe(40);
    const dy = makeWeapon("diyu_huolei"); dy.aspect = 1;
    expect(aspectDef(dy).psn).toBe(50);
  });

  it("全 26 形态可切：门控通过后 aspect=1 且 aspectDef 名带形态标签", () => {
    for (const id of Object.keys(WEAPON_DATA)) {
      const sim = new GameSim(1070, memoryStorage());
      sim.exec({ action: "start" });
      sim.player.weapons = [makeWeapon(id)];
      sim.player.weapons[0]!.ups = 3;
      expect(sim.toggleAspect(0)).toBe(true);
      const w = sim.player.weapons[0]!;
      expect(w.aspect).toBe(1);
      expect(aspectDef(w).name).not.toBe(WEAPON_DATA[id].name); // 名带形态标签
    }
  });

  it("升级池不受形态影响（validStats 按类型不变）", () => {
    const w = makeWeapon("tiejian");
    w.aspect = 1;
    expect(aspectUnlocked({ ...w, ups: 3 })).toBe(true);
    expect(aspectDef(w).type).toBe("Melee"); // 类型继承（升级池键）
  });
});