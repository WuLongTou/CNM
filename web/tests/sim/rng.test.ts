import { describe, expect, it } from "vitest";
import { Rng } from "../../src/sim/rng";

describe("Rng（种子化 PRNG）", () => {
  it("同 seed 产生相同序列（确定性）", () => {
    const a = new Rng(42);
    const b = new Rng(42);
    const seqA = Array.from({ length: 100 }, () => a.next());
    const seqB = Array.from({ length: 100 }, () => b.next());
    expect(seqA).toEqual(seqB);
  });

  it("不同 seed 产生不同序列", () => {
    const a = new Rng(1);
    const b = new Rng(2);
    expect(Array.from({ length: 50 }, () => a.next()))
      .not.toEqual(Array.from({ length: 50 }, () => b.next()));
  });

  it("next() 落在 [0,1)", () => {
    const r = new Rng(7);
    for (let i = 0; i < 1000; i++) {
      const v = r.next();
      expect(v).toBeGreaterThanOrEqual(0);
      expect(v).toBeLessThan(1);
    }
  });

  it("int() 含两端且在范围内", () => {
    const r = new Rng(9);
    for (let i = 0; i < 500; i++) {
      const v = r.int(3, 7);
      expect(v).toBeGreaterThanOrEqual(3);
      expect(v).toBeLessThanOrEqual(7);
      expect(Number.isInteger(v)).toBe(true);
    }
  });

  it("weighted() 只返回表内值且分布合理", () => {
    const r = new Rng(11);
    const counts = new Map<string, number>();
    for (let i = 0; i < 3000; i++) {
      const v = r.weighted([["a", 0.75], ["b", 0.25]] as const);
      counts.set(v, (counts.get(v) ?? 0) + 1);
    }
    const a = counts.get("a")!;
    const b = counts.get("b")!;
    expect(a + b).toBe(3000);
    // 允许统计波动：a 应显著多于 b
    expect(a).toBeGreaterThan(b * 2);
  });
});
