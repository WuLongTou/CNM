import { describe, expect, it } from "vitest";
import { FOE_MECHS } from "../../src/sim/foemechs";
import { ENEMY_NAMES } from "../../src/sim/config";

describe("R58 妖物机制表", () => {
  it("14 种妖物皆有机制与应对", () => {
    const ids = Object.keys(ENEMY_NAMES).map(Number);
    expect(ids.length).toBe(14);
    for (const id of ids) {
      const m = FOE_MECHS[id];
      expect(m, `缺机制: ${ENEMY_NAMES[id]}`).toBeTruthy();
      expect(m.mech.length).toBeGreaterThan(0);
      expect(m.counter.length).toBeGreaterThan(0);
    }
  });

  it("机制文案互不重复（每只妖物可辨）", () => {
    const mechs = Object.values(FOE_MECHS).map((m) => m.mech);
    expect(new Set(mechs).size).toBe(mechs.length);
  });
});
