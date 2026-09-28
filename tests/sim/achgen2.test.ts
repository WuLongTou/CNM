// R130 成就谱系第二轮：承伤/香火收入/零消费 三个通关挑战（现有数据直授）。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { ACHIEVEMENTS } from "../../src/sim/achievements";

const hurt = (sim: GameSim, dmg: number) =>
  (sim as unknown as { hurtPlayer(d: number): boolean }).hurtPlayer(dmg);
const victory = (sim: GameSim) =>
  (sim as unknown as { onVictory(): number }).onVictory();

describe("R130 成就谱系第二轮", () => {
  it("表 36 项：三条新成就 check 型", () => {
    expect(ACHIEVEMENTS).toHaveLength(36);
    for (const id of ["bulwark", "windfall", "emptyhands"]) {
      const a = ACHIEVEMENTS.find((x) => x.id === id)!;
      expect(a.check).toBeTruthy();
      expect(a.goal).toBeUndefined();
    }
  });

  it("铜墙铁壁：承伤 ≤200 通关授，>200 不授（铁壁含于其中）", () => {
    const s1 = new GameSim(1300, memoryStorage());
    s1.exec({ action: "start" });
    hurt(s1, 150);
    victory(s1);
    expect(s1.achievements.unlocked.has("bulwark")).toBe(true);
    expect(s1.achievements.unlocked.has("flawless")).toBe(false); // 150>0 铁壁不授

    const s2 = new GameSim(1301, memoryStorage());
    s2.exec({ action: "start" });
    hurt(s2, 300);
    victory(s2);
    expect(s2.achievements.unlocked.has("bulwark")).toBe(false);
  });

  it("香火盈门：earned ≥800 通关授；两手空空：spent=0 通关授", () => {
    const s1 = new GameSim(1302, memoryStorage());
    s1.exec({ action: "start" });
    (s1 as unknown as { runGoldEarned: number }).runGoldEarned = 900;
    (s1 as unknown as { runGoldSpent: number }).runGoldSpent = 0;
    victory(s1);
    expect(s1.achievements.unlocked.has("windfall")).toBe(true);
    expect(s1.achievements.unlocked.has("emptyhands")).toBe(true);

    const s2 = new GameSim(1303, memoryStorage());
    s2.exec({ action: "start" });
    (s2 as unknown as { runGoldEarned: number }).runGoldEarned = 500;
    (s2 as unknown as { runGoldSpent: number }).runGoldSpent = 100;
    victory(s2);
    expect(s2.achievements.unlocked.has("windfall")).toBe(false);
    expect(s2.achievements.unlocked.has("emptyhands")).toBe(false);
  });
});