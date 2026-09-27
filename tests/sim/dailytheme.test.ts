import { describe, expect, it } from "vitest";
import { dailyTheme, dailySeed } from "../../src/sim/daily";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

describe("R39 每日主题", () => {
  it("按星期固定派生（2026-09-27 为周日）", () => {
    expect(dailyTheme(new Date(2026, 8, 27)).label).toBe("晴空日");
    expect(dailyTheme(new Date(2026, 8, 25)).label).toBe("血月日"); // 周五
    expect(dailyTheme(new Date(2026, 8, 30)).label).toBe("乱流日"); // 周三无偏向
    expect(dailyTheme(new Date(2026, 8, 30)).favor).toBeNull();
  });

  it("当日种子开局天气偏向主题（长时间统计占比显著更高）", () => {
    const sim = new GameSim(dailySeed(), memoryStorage());
    sim.exec({ action: "start" });
    const favor = dailyTheme().favor;
    if (!favor) return; // 乱流日无偏向，跳过统计
    let hit = 0, n = 4000;
    const w = (sim as unknown as { weather: { state: { type: string }; reroll(): void } }).weather;
    for (let i = 0; i < n; i++) { w.reroll(); if (w.state.type === favor) hit++; }
    expect(hit / n).toBeGreaterThan(0.4);
  });

  it("异种子开局无偏向（占比贴近随机基线 25%）", () => {
    const sim = new GameSim(999, memoryStorage());
    sim.exec({ action: "start" });
    const favor = dailyTheme().favor;
    let hit = 0, n = 4000;
    const w = (sim as unknown as { weather: { state: { type: string }; reroll(): void } }).weather;
    for (let i = 0; i < n; i++) { w.reroll(); if (favor && w.state.type === favor) hit++; }
    expect(hit / n).toBeLessThan(0.4);
  });
});
