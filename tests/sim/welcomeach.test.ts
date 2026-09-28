// R140 欢迎页成就完成度：meta 口径 ach_total/unlocked/last。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

describe("R140 欢迎页成就完成度", () => {
  it("meta：ach_total 42 / ach_unlocked 随授予增长 / ach_last 最近 id", () => {
    const sim = new GameSim(1400, memoryStorage());
    sim.exec({ action: "start" });
    const d0 = sim.exec({ action: "meta" }).data as unknown as { ach_total: number; ach_unlocked: number; ach_last: string | null };
    expect(d0.ach_total).toBe(42);
    expect(d0.ach_unlocked).toBe(0);
    expect(d0.ach_last).toBeNull();
    sim.achievements.grant("first_blood");
    sim.achievements.grant("boss_slayer");
    const d1 = sim.exec({ action: "meta" }).data as unknown as { ach_unlocked: number; ach_last: string };
    expect(d1.ach_unlocked).toBe(2);
    expect(d1.ach_last).toBe("boss_slayer"); // Set 插入序=最近
  });
});