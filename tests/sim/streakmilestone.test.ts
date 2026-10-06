// R201 连杀里程碑阶梯：10/25/50 固定档 + 每满 100 一档（100/200/…）；其余不触发。

import { describe, expect, it } from "vitest";
import { streakMilestone } from "../../src/app/streak";

describe("R201 连杀里程碑阶梯", () => {
  it("固定档 10/25/50 命中", () => {
    expect(streakMilestone(10)).toBe(10);
    expect(streakMilestone(25)).toBe(25);
    expect(streakMilestone(50)).toBe(50);
  });

  it("每满 100 命中（100/200/300）", () => {
    for (const n of [100, 200, 300]) expect(streakMilestone(n)).toBe(n);
  });

  it("其余数值不触发（含 99/101/150 非百整档）", () => {
    for (const n of [1, 3, 5, 9, 11, 24, 26, 49, 51, 99, 101, 150, 199, 250]) {
      expect(streakMilestone(n)).toBeNull();
    }
  });
});
