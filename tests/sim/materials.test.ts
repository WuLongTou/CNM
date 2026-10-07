// R210 材料中文名与合成拒绝原因：单一事实源（防面板中英混排、新增材料漏名）。

import { describe, expect, it } from "vitest";
import { MATERIAL_NAMES, type Material } from "../../src/sim/inventory";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { RECIPES } from "../../src/sim/recipes";

const ALL: Material[] = ["wood", "stone", "fiber", "berry"];

describe("R210 材料名与合成原因", () => {
  it("MATERIAL_NAMES 覆盖全部材料且为中文", () => {
    for (const m of ALL) {
      expect(MATERIAL_NAMES[m], m).toBeTruthy();
      expect(MATERIAL_NAMES[m], m).toMatch(/[\u4e00-\u9fa5]/);
    }
    expect(Object.keys(MATERIAL_NAMES).sort()).toEqual([...ALL].sort());
  });

  it("全部配方成本的材料都有中文名", () => {
    for (const r of RECIPES) {
      for (const m of Object.keys(r.cost) as Material[]) {
        expect(MATERIAL_NAMES[m], `${r.id}/${m}`).toBeTruthy();
      }
    }
  });

  it("材料不足的 reason 用中文名且不泄露英文 id", () => {
    const sim = new GameSim(2100, memoryStorage());
    sim.exec({ action: "start" });
    const res = sim.craft("wood_sword") as { crafted: boolean; reason?: string };
    expect(res.crafted).toBe(false);
    expect(res.reason).toBe("材料不足：木材");
    expect(res.reason).not.toMatch(/wood|stone|fiber|berry/);
  });
});
