// R134 秘密职业专属成就：隐世高人/武德充沛（role.id 判定 onVictory 直授）。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage, defaultSave } from "../../src/sim/save";
import { ACHIEVEMENTS, HONOR_ACHIEVEMENT_NAMES } from "../../src/sim/achievements";

const withSave = (ach: string[]) => {
  const save = memoryStorage();
  save.save({ ...defaultSave(), achievements: ach });
  return save;
};
const victory = (sim: GameSim) =>
  (sim as unknown as { onVictory(): number }).onVictory();

describe("R134 秘密职业专属成就", () => {
<<<<<<< HEAD
  it("表 39 项：hermit_win/wusheng_win check 型且入荣誉集", () => {
    expect(ACHIEVEMENTS).toHaveLength(39);
=======
  it("表 42 项：hermit_win/wusheng_win check 型且入荣誉集", () => {
    expect(ACHIEVEMENTS).toHaveLength(42);
>>>>>>> 5a48984 (新增(游戏性): R135 成就谱系第三轮——万妖斩/雷劫余威/满级宗师)
    for (const id of ["hermit_win", "wusheng_win"]) {
      const a = ACHIEVEMENTS.find((x) => x.id === id)!;
      expect(a.check).toBeTruthy();
      expect(HONOR_ACHIEVEMENT_NAMES.has(a.name)).toBe(true);
    }
  });

  it("隐士通关：授 隐世高人；武圣通关：授 武德充沛", () => {
    const s1 = new GameSim(1340, withSave(["flawless"]), "hermit");
    s1.exec({ action: "start" });
    victory(s1);
    expect(s1.achievements.unlocked.has("hermit_win")).toBe(true);

    const s2 = new GameSim(1341, withSave(["nine_realms"]), "wusheng");
    s2.exec({ action: "start" });
    victory(s2);
    expect(s2.achievements.unlocked.has("wusheng_win")).toBe(true);
  });

  it("普通职业通关：两条都不授", () => {
    const sim = new GameSim(1342, memoryStorage());
    sim.exec({ action: "start" });
    victory(sim);
    expect(sim.achievements.unlocked.has("hermit_win")).toBe(false);
    expect(sim.achievements.unlocked.has("wusheng_win")).toBe(false);
  });
});