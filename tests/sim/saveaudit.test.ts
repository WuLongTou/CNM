// R170 存档版本兼容审计：各时代 legacy save → normalizeSave 健壮性。

import { describe, expect, it } from "vitest";
import { normalizeSave, defaultSave } from "../../src/sim/save";

/** 审计输入即「老版本缺字段档」——正是被测对象；TS 宽化走 helper。 */
const legacy = (v: object) => v as never;

describe("R170 存档版本兼容审计", () => {
  it("R36 时代档（仅核心四字段）：缺省字段全部落默认", () => {
    const s = normalizeSave(legacy({
      highScore: 5000, totalKills: 800, games: 12, coins: 300,
    }));
    expect(s.highScore).toBe(5000);
    expect(s.metaHp).toBe(0);
    expect(s.maxDifficulty).toBe(-1);
    expect(s.codex).toEqual({});
    expect(s.clears).toEqual({});
    expect(s.dailyBest).toEqual({ date: "", score: 0 });
    expect(s.armory).toEqual([]);
    expect(s.fused).toEqual([]);
    expect(s.ultimates).toEqual([]);
    expect(s.rushBest).toBe(0);
    expect(s.mutatorClears).toEqual({});
    expect(s.dailyStreak).toEqual({ last: "", count: 0 });
    expect(s.runHistory).toEqual([]);
    expect(s.achievements).toEqual([]);
    expect(s.stats).toEqual(defaultSave().stats);
  });

  it("R102 时代档（runHistory 无 achievements 字段）：记录保留不崩", () => {
    const s = normalizeSave(legacy({
      highScore: 100, games: 1,
      runHistory: [{ score: 100, kills: 10, survivalTime: 60, round: 2, roleName: "剑修", difficultyName: "平妖", mode: "", seed: 1, date: "2026-01-01", cleared: false }],
    }));
    expect(s.runHistory).toHaveLength(1);
    expect((s.runHistory as unknown[])[0]).toMatchObject({ score: 100 });
  });

  it("R108 时代档（stats 半缺）：stats 与默认合并", () => {
    const s = normalizeSave(legacy({
      highScore: 1, games: 1, stats: { timeAlive: 600, maxCombo: 33 },
    }));
    expect(s.stats.timeAlive).toBe(600);
    expect(s.stats.maxCombo).toBe(33);
    expect(s.stats.maxKills).toBe(0); // 缺省落默认
    expect(s.stats.totalBosses).toBe(0);
  });

  it("runHistory 超 10 条：截到 10", () => {
    const entries = Array.from({ length: 15 }, (_, i) => ({
      score: i, kills: 0, survivalTime: 0, round: 1, roleName: "x", difficultyName: "平妖", mode: "", seed: i, date: "d", cleared: false,
    }));
    const s = normalizeSave(legacy({ runHistory: entries }));
    expect(s.runHistory).toHaveLength(10);
  });

  it("null/空对象：返回全默认", () => {
    expect(normalizeSave(null)).toEqual(defaultSave());
    expect(normalizeSave({})).toEqual(defaultSave());
  });
});