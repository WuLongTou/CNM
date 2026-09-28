import { describe, expect, it } from "vitest";
import { coachTrigger, TRANSIENT_TIPS, TRANSIENT_MS, COACH_TEXTS, type CoachSnapshot, type CoachTipId } from "../../src/ui/coach";

function snap(over: Partial<CoachSnapshot> = {}): CoachSnapshot {
  return { isFirstRun: false, kills: 5, pickups: 0, offersOpen: false, hpRatio: 1, ...over };
}

describe("R31 进阶教练", () => {
  it("精英首杀触发藏宝提示（不分首局）", () => {
    const done = new Set<CoachTipId>();
    expect(coachTrigger(snap(), done)).toBeNull();
    expect(coachTrigger(snap({ eliteKills: 1 }), done)).toBe("elite_chest");
    expect(coachTrigger(snap({ eliteKills: 1 }), new Set(["elite_chest"]))).toBeNull();
  });

  it("祭坛近且双武器才提示祭炼", () => {
    const done = new Set<CoachTipId>();
    expect(coachTrigger(snap({ altarNear: true, weaponCount: 1 }), done)).toBeNull();
    expect(coachTrigger(snap({ altarNear: true, weaponCount: 2 }), done)).toBe("fusion");
    expect(coachTrigger(snap({ altarNear: false, weaponCount: 3 }), done)).toBeNull();
  });

  it("妖王在场触发；六械齐备触发且优先级居末", () => {
    const done = new Set<CoachTipId>();
    expect(coachTrigger(snap({ bossActive: true }), done)).toBe("boss");
    expect(coachTrigger(snap({ weaponCount: 5 }), done)).toBe("arsenal_full");
    expect(coachTrigger(snap({ weaponCount: 6 }), done)).toBe("arsenal_full");
    expect(coachTrigger(snap({ bossActive: true, weaponCount: 6 }), done)).toBe("boss");
  });

  it("首局前置条件成立时基础三条优先", () => {
    const done = new Set<CoachTipId>();
    expect(coachTrigger(snap({ isFirstRun: true, kills: 0, eliteKills: 1, bossActive: true }), done)).toBe("move_attack");
  });

  it("四条全为限时且时长 8 秒、文案齐备", () => {
    expect(TRANSIENT_TIPS.size).toBe(6); // R114 +2（形态解锁/香火龛首见）
    expect(TRANSIENT_MS).toBe(8000);
    for (const id of TRANSIENT_TIPS) {
      expect(COACH_TEXTS[id].desktop.length).toBeGreaterThan(0);
      expect(COACH_TEXTS[id].touch.length).toBeGreaterThan(0);
    }
  });
});
