// R114 教练补全：形态解锁 / 香火龛首见 两条系统发现型提示。

import { describe, expect, it } from "vitest";
import { coachTrigger, TRANSIENT_TIPS, type CoachSnapshot } from "../../src/ui/coach";
import { makeWeapon } from "../../src/sim/weapons";

const base: CoachSnapshot = { isFirstRun: false, kills: 0, pickups: 0, offersOpen: false, hpRatio: 1 };

describe("R114 教练补全", () => {
  it("aspect_unlocked：有武器达进阶且有形态表才触发", () => {
    const w = makeWeapon("tiejian");
    w.ups = 1; // 未进阶
    const s1: CoachSnapshot = { ...base, aspectReady: false };
    expect(coachTrigger(s1, new Set())).toBeNull();
    const s2: CoachSnapshot = { ...base, aspectReady: true };
    expect(coachTrigger(s2, new Set())).toBe("aspect_unlocked");
  });

  it("shrine_first：场上浮起香火龛触发", () => {
    const s: CoachSnapshot = { ...base, shrineSeen: true };
    expect(coachTrigger(s, new Set())).toBe("shrine_first");
    const s0: CoachSnapshot = { ...base, shrineSeen: false };
    expect(coachTrigger(s0, new Set())).toBeNull();
  });

  it("两条都是 transient（8s 消退，不做事件追踪）", () => {
    expect(TRANSIENT_TIPS.has("aspect_unlocked")).toBe(true);
    expect(TRANSIENT_TIPS.has("shrine_first")).toBe(true);
  });

  it("优先级：boss > arsenal_full > aspect_unlocked > shrine_first", () => {
    const all: CoachSnapshot = { ...base, bossActive: true, weaponCount: 6, aspectReady: true, shrineSeen: true };
    expect(coachTrigger(all, new Set())).toBe("boss");
    const noBoss: CoachSnapshot = { ...all, bossActive: false };
    expect(coachTrigger(noBoss, new Set())).toBe("arsenal_full");
    const noArsenal: CoachSnapshot = { ...noBoss, weaponCount: 2 };
    expect(coachTrigger(noArsenal, new Set())).toBe("aspect_unlocked");
  });
});