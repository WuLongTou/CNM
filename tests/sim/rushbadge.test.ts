import { describe, expect, it } from "vitest";
import { rushTierLabel } from "../../src/ui/hud";

describe("R50 连战档位徽标", () => {
  it("非连战恒空", () => {
    expect(rushTierLabel(false, 9, 1.45)).toBe("");
  });

  it("0 档（未满 3 杀）不显示", () => {
    expect(rushTierLabel(true, 0, 1)).toBe("");
    expect(rushTierLabel(true, 2, 1)).toBe("");
  });

  it("1/2/5 档标签与倍率一致", () => {
    expect(rushTierLabel(true, 3, 1.15)).toBe("狂化 1 档 ×1.15");
    expect(rushTierLabel(true, 6, 1.3)).toBe("狂化 2 档 ×1.30");
    expect(rushTierLabel(true, 15, 1.75)).toBe("狂化 5 档 ×1.75");
  });

  it("档内（4/5 杀）维持同一档", () => {
    expect(rushTierLabel(true, 4, 1.15)).toBe("狂化 1 档 ×1.15");
    expect(rushTierLabel(true, 5, 1.15)).toBe("狂化 1 档 ×1.15");
  });
});
