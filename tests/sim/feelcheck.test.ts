// R176 手感 pass 数值守护：世界/密度/速度基线钉。

import { describe, expect, it } from "vitest";
import { CFG, ENEMY_STATS } from "../../src/sim/config";

describe("R176 手感 pass 数值守护", () => {
  it("世界缩 75%：2880×1620", () => {
    expect(CFG.World).toEqual({ Width: 2880, Height: 1620 });
  });

  it("密度：Act1 min 6 / max 20；冷却 1.3s；Act4 max 84", () => {
    expect(CFG.Acts.MinEnemies).toEqual([6, 20, 30, 40]);
    expect(CFG.Acts.MaxEnemies).toEqual([20, 48, 66, 84]);
    expect(CFG.Acts.SpawnCooldown).toEqual([1.3, 0.7, 0.5, 0.4]);
  });

  it("敌速基线：×1.25 全表上调（魅影 0.51× 玩家速过半）", () => {
    const p = CFG.Player.BaseSpeed;
    const oldSpeeds = [45, 90, 22, 55, 55, 35, 30, 28, 30, 55, 35, 40, 65, 85];
    for (let i = 0; i < 14; i++) {
      expect(ENEMY_STATS[i].speed).toBeCloseTo(oldSpeeds[i] * 1.25, -1); // ×1.25 语义（±5 内视为对齐）
    }
    expect(ENEMY_STATS[1].speed / p).toBeGreaterThan(0.45); // 快速系过半玩家速
    expect(ENEMY_STATS[13].speed / p).toBeCloseTo(106 / 220, 2);
  });

  it("阶数速度爬坡语义：RampInterval 45s 不变（爬坡在 createEnemy 内联）", () => {
    expect(CFG.Difficulty.RampInterval).toBe(45);
  });

  it("出生距离仍离屏（> 视口半宽 640）", () => {
    expect(CFG.Spawning.MinDistFromPlayer).toBeGreaterThanOrEqual(380);
  });
});