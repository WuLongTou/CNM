// R81 成就进度：goal 声明式判定等价 / 进度值 / 复合型 null / 数据表完整性。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import {
  ACHIEVEMENTS,
  achievementProgress,
  type AchievementState,
} from "../../src/sim/achievements";

const byId = (id: string) => {
  const a = ACHIEVEMENTS.find((x) => x.id === id)!;
  expect(a).toBeTruthy();
  return a;
};

const state = (over: Partial<AchievementState> = {}): AchievementState => ({
  kills: 0, survivalTime: 0, bossKills: 0, totalKills: 0, eliteKills: 0,
  maxCombo: 0, weaponCount: 0, rerollsUsed: 0, banishesUsed: 0, goblinKills: 0,
  shopPurchases: 0, codexGold: 0, armoryCount: 0, armoryTotal: 0,
  fusedCount: 0, fusedTotal: 0, ultimateRunCount: 0, bossRushKills: 0,
  dailyStreakCount: 0,
  ...over,
});

describe("R81 成就进度", () => {
  it("数据表：42 项、goal 与 check 恰二选一", () => {
    expect(ACHIEVEMENTS).toHaveLength(44) // R205 战功中段 +2;
    for (const a of ACHIEVEMENTS) {
      expect(!!a.goal !== !!a.check).toBe(true); // 恰一
      if (a.goal) expect(a.goal.target).toBeGreaterThan(0);
    }
  });

  it("阈值型迁移判定等价：update 按 goal 授勋", () => {
    const sim = new GameSim(810, memoryStorage());
    sim.exec({ action: "start" });
    sim.achievements.update(state({ kills: 1 }));
    expect(sim.achievements.unlocked.has("first_blood")).toBe(true);
    sim.achievements.update(state({ kills: 1, totalKills: 49 }));
    expect(sim.achievements.unlocked.has("slayer_50")).toBe(false);
    sim.achievements.update(state({ kills: 1, totalKills: 50 }));
    expect(sim.achievements.unlocked.has("slayer_50")).toBe(true);
  });

  it("achievementProgress：阈值型返回 cur/target", () => {
    const a = byId("slayer_50");
    expect(achievementProgress(a, state({ totalKills: 120 }))).toEqual({ cur: 120, target: 50 });
    expect(achievementProgress(a, state())).toEqual({ cur: 0, target: 50 });
  });

  it("复合型（check）无进度：返回 null", () => {
    const a = byId("oathbound");
    expect(a.goal).toBeUndefined();
    expect(achievementProgress(a, state())).toBeNull();
  });

  it("存活类阈值同样走 goal（60/180/300 三连）", () => {
    const s300 = byId("survive_300");
    expect(achievementProgress(s300, state({ survivalTime: 240 }))).toEqual({ cur: 240, target: 300 });
  });
});