import { describe, expect, it } from "vitest";
import { BOSSES, BOSS_BARRAGE_INFO } from "../../src/sim/bosses";
import { BOSS_BARRAGE, BOSS_ROSTER } from "../../src/sim/game";

describe("R57 妖王图鉴", () => {
  it("五王齐备，字段完整", () => {
    expect(BOSSES.length).toBe(5);
    for (const b of BOSSES) {
      for (const f of [b.name, b.mech, b.counter]) expect(f.length).toBeGreaterThan(0);
    }
    expect(new Set(BOSSES.map((b) => b.name)).size).toBe(5);
  });

  it("BOSS_ROSTER 从表派生（顺序与类型一致）", () => {
    expect(BOSS_ROSTER).toEqual(BOSSES.map((b) => b.type));
  });

  it("弹幕信息与运行时参数一致（防文案漂移）", () => {
    expect(BOSS_BARRAGE_INFO.cd).toBe(BOSS_BARRAGE.cd);
    expect(BOSS_BARRAGE_INFO.first).toBe(BOSS_BARRAGE.first);
    expect(BOSS_BARRAGE_INFO.dmgMult).toBe(BOSS_BARRAGE.dmgMult);
    expect(BOSS_BARRAGE_INFO.speed).toBe(BOSS_BARRAGE.speed);
    expect(BOSS_BARRAGE_INFO.count).toEqual(BOSS_BARRAGE.count);
  });
});
