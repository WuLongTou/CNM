// R198 精通称号：五档阈值边界 + 纯函数。

import { describe, expect, it } from "vitest";
import { masteryTier } from "../../src/sim/mastery";

describe("R198 精通称号", () => {
  it("五档边界", () => {
    expect(masteryTier(0)).toEqual({ level: 1, title: "初识" });
    expect(masteryTier(49).title).toBe("初识");
    expect(masteryTier(50)).toEqual({ level: 2, title: "入门" });
    expect(masteryTier(199).title).toBe("入门");
    expect(masteryTier(200)).toEqual({ level: 3, title: "熟练" });
    expect(masteryTier(500)).toEqual({ level: 4, title: "精通" });
    expect(masteryTier(1000)).toEqual({ level: 5, title: "宗师" });
    expect(masteryTier(9999).title).toBe("宗师");
  });
});