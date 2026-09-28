// R84 输出主力行：mainWeaponLine 纯函数（最高伤害武器 + 占比）。

import { describe, expect, it } from "vitest";
import { Hud } from "../../src/ui/hud";

describe("R84 输出主力行", () => {
  it("伤害最高武器 + 占比四舍五入", () => {
    const w = [
      { name: "铁剑", dmgDealt: 300 },
      { name: "弓箭", dmgDealt: 100 },
    ];
    expect(Hud.mainWeaponLine(w, 400)).toBe("输出主力 铁剑 75%");
  });

  it("总额 0 / 空武器 / 全零伤害 → 空串", () => {
    const w = [{ name: "铁剑", dmgDealt: 0 }];
    expect(Hud.mainWeaponLine(w, 0)).toBe("");
    expect(Hud.mainWeaponLine([], 100)).toBe("");
    expect(Hud.mainWeaponLine(w, 100)).toBe("");
  });

  it("并列伤害取排序首个（稳定）", () => {
    const w = [
      { name: "弓箭", dmgDealt: 200 },
      { name: "铁剑", dmgDealt: 200 },
    ];
    expect(Hud.mainWeaponLine(w, 400)).toContain("弓箭 50%");
  });
});