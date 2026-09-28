import { describe, expect, it } from "vitest";
import { ACHIEVEMENTS, ACHIEVEMENT_GROUPS } from "../../src/sim/achievements";

describe("R51 成就分组", () => {
  it("每条成就都有合法分组", () => {
    for (const a of ACHIEVEMENTS) {
      expect(ACHIEVEMENT_GROUPS).toContain(a.group);
    }
  });

  it("四条线皆有成员且总数为 32", () => {
    const counts = ACHIEVEMENT_GROUPS.map((g) => ACHIEVEMENTS.filter((a) => a.group === g).length);
    for (const n of counts) expect(n).toBeGreaterThan(0);
    expect(counts.reduce((a, b) => a + b, 0)).toBe(32);
  });

  it("组内 id 唯一，组名非空", () => {
    expect(new Set(ACHIEVEMENTS.map((a) => a.id)).size).toBe(32);
    for (const g of ACHIEVEMENT_GROUPS) expect(g.length).toBeGreaterThan(0);
  });
});
