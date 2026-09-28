// R96 新融合配方×2：霜牙/血玉匕获得祭坛替代获取路径（配方 6→8）。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { FUSION_RECIPES, WEAPON_DATA, makeWeapon } from "../../src/sim/weapons";

describe("R96 新融合配方", () => {
  it("配方 8 条；新增两条结果为 霜牙/血玉匕", () => {
    expect(FUSION_RECIPES).toHaveLength(8);
    expect(FUSION_RECIPES.some((r) => r.result === "shuangya" && r.a === "淬毒短刀")).toBe(true);
    expect(FUSION_RECIPES.some((r) => r.result === "dujian" && r.b === "魅火法杖")).toBe(true);
  });

  it("a/b/result 名都在武器表中（配方表与武器表一致）", () => {
    for (const r of FUSION_RECIPES) {
      expect(WEAPON_DATA[r.result]).toBeTruthy();
      // a/b 是武器名：名字反查 defId
      expect(Object.values(WEAPON_DATA).some((d) => d.name === r.a)).toBe(true);
      expect(Object.values(WEAPON_DATA).some((d) => d.name === r.b)).toBe(true);
    }
  });

  it("祭坛路径：持 淬毒短刀+冰霜法杖 开出的报价为 霜牙", () => {
    const sim = new GameSim(960, memoryStorage());
    sim.exec({ action: "start" });
    sim.giveWeapon("duandao");
    sim.giveWeapon("bingshuangfazhang");
    const offer = (sim as unknown as { makeFusionOffer(): { resultId: string } | null }).makeFusionOffer();
    expect(offer).not.toBeNull();
    expect(offer!.resultId).toBe("shuangya");
  });

  it("resolveFusion accept：消耗两件换 霜牙", () => {
    const sim = new GameSim(961, memoryStorage());
    sim.exec({ action: "start" });
    sim.player.weapons = [makeWeapon("duandao"), makeWeapon("bingshuangfazhang")];
    // makeFusionOffer 只生成报价；fusionOffer 置位是祭坛贴近侧的职责——测试直赋
    const offer = (sim as unknown as { makeFusionOffer(): { resultId: string } | null }).makeFusionOffer()!;
    (sim as unknown as { fusionOffer: unknown }).fusionOffer = offer;
    sim.resolveFusion(true);
    const names = sim.player.weapons.map((w) => w.name);
    expect(names).toContain("霜牙");
    expect(names).not.toContain("淬毒短刀");
    expect(names).not.toContain("冰霜法杖");
  });
});