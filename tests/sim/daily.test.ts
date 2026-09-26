import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { dailyKey, dailySeed } from "../../src/sim/daily";
import { defaultSave, memoryStorage, type SaveData } from "../../src/sim/save";

function winWith(save: ReturnType<typeof memoryStorage>, score: number) {
  const sim = new GameSim(dailySeed(), save);
  sim.score = score;
  sim.kills = 1;
  (sim as unknown as { onVictory(): number }).onVictory();
  return sim;
}

describe("R26 每日妖潮", () => {
  it("日期键与种子格式固定", () => {
    const d = new Date(2026, 8, 26);
    expect(dailyKey(d)).toBe("2026-09-26");
    expect(dailySeed(d)).toBe(20260926);
    expect(dailyKey(new Date(2026, 0, 5))).toBe("2026-01-05");
    expect(dailySeed(new Date(2026, 0, 5))).toBe(20260105);
  });

  it("当日种子局计入最佳；异种局不碰榜", () => {
    const save = memoryStorage();
    winWith(save, 1000);
    expect(save.data.dailyBest).toEqual({ date: dailyKey(), score: 1000 });
    winWith(save, 500);
    expect(save.data.dailyBest.score).toBe(1000);
    winWith(save, 1500);
    expect(save.data.dailyBest.score).toBe(1500);
    const other = new GameSim(12345, save);
    other.score = 99999; other.kills = 1;
    (other as unknown as { onVictory(): number }).onVictory();
    expect(save.data.dailyBest.score).toBe(1500);
  });

  it("跨天重新开榜；老存档缺字段兼容", () => {
    const save = memoryStorage();
    save.save({ ...defaultSave(), dailyBest: { date: "2000-01-01", score: 9999 } });
    winWith(save, 100);
    expect(save.data.dailyBest).toEqual({ date: dailyKey(), score: 100 });
    const legacy = { ...defaultSave(), dailyBest: undefined as unknown as SaveData["dailyBest"] };
    const save2 = memoryStorage();
    save2.save(legacy as SaveData);
    winWith(save2, 50);
    expect(save2.data.dailyBest).toEqual({ date: dailyKey(), score: 50 });
  });

  it("meta 暴露当日种子与最佳", () => {
    const save = memoryStorage();
    save.save({ ...defaultSave(), dailyBest: { date: dailyKey(), score: 777 } });
    const sim = new GameSim(7, save);
    const meta = sim.exec({ action: "meta" }).data as { dailySeed: number; dailyBest: { date: string; score: number } };
    expect(meta.dailySeed).toBe(dailySeed());
    expect(meta.dailyBest).toEqual({ date: dailyKey(), score: 777 });
  });
});
