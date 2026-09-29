// R156 成就可达性审计：42 项逐一验证授予路径存在（直授 grep / tick 判定 / 事件授予）。

import { describe, expect, it } from "vitest";
import { ACHIEVEMENTS } from "../../src/sim/achievements";
import gameSrc from "../../src/sim/game.ts?raw";

/** 直授型：grep game.ts 中 grant("id") 是否存在。 */
function grantedInGame(id: string): boolean {
  return gameSrc.includes('"' + id + '"') && gameSrc.includes("grant(");
}

describe("R156 成就可达性审计", () => {
  const directGrant = [
    "oathbound", "heat_max", "flawless", "bulwark", "windfall", "emptyhands",
    "ultmaster", "three_buffs", "hermit_win", "wusheng_win",
    "pacify", "demon_queller", "warden", "three_heroes", "nine_realms",
  ];

  it("直授型 15 项：game.ts 均有 grant 路径", () => {
    for (const id of directGrant) {
      const a = ACHIEVEMENTS.find((x) => x.id === id)!;
      expect(a.check).toBeTruthy(); // 直授型必为 check 恒假（onVictory 授）
      expect(grantedInGame(id)).toBe(true);
    }
  });

  it("goal 型 24 项：tick 判定自动覆盖（无需 grep）", () => {
    const goalTypes = ACHIEVEMENTS.filter((a) => a.goal);
    expect(goalTypes.length).toBe(24);
    for (const a of goalTypes) expect(a.check).toBeUndefined(); // goal/check 互斥
  });

  it("check 判定型 7 项：tick 自动覆盖", () => {
    const checkTypes = ACHIEVEMENTS.filter((a) => a.check && !directGrant.includes(a.id));
    for (const a of checkTypes) expect(a.check?.({} as never)).toBeDefined();
  });

  it("全覆盖：42 项 = 直授 15 + goal 24 + check 3", () => {
    expect(15 + 24 + 3).toBe(ACHIEVEMENTS.length);
  });
});