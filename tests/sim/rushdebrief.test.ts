import { describe, expect, it } from "vitest";
import { Hud } from "../../src/ui/hud";

describe("R60 连战战报头", () => {
  it("普通局不显示", () => {
    expect(Hud.rushHead(false, 0, 0)).toBe("");
    expect(Hud.rushHead(false, 7, 2)).toBe("");
  });

  it("连战显示讨伐数与档位（含 0 档 0 杀）", () => {
    expect(Hud.rushHead(true, 0, 0)).toBe("讨伐妖王 0 只 · 狂化 0 档");
    expect(Hud.rushHead(true, 1, 0)).toBe("讨伐妖王 1 只 · 狂化 0 档");
    expect(Hud.rushHead(true, 5, 1)).toBe("讨伐妖王 5 只 · 狂化 1 档");
  });
});
