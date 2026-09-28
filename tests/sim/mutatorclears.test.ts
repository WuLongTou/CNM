// R74 每日变异通关收集：仅每日局计入 / 五变异各通一次授「历劫全域」/
// 重复通关不重复计 / 存档归一化迁移。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage, normalizeSave, defaultSave } from "../../src/sim/save";
import { dailySeed } from "../../src/sim/daily";

function dailyVictory(mutatorId: string, store = memoryStorage()): GameSim {
  // 当日种子 + 注入变异 = 今日每日局；终局直授走 onVictory 真实路径。
  // store 可共享：跨局累计读同一位存档（R74 收集语义）。
  const sim = new GameSim(dailySeed(), store, "sword", 0, {}, "normal", mutatorId);
  sim.exec({ action: "start" });
  (sim as unknown as { onVictory(): number }).onVictory();
  return sim;
}

function clearsOf(s: GameSim): Record<string, number> {
  return s.status().mutator_clears as Record<string, number>;
}

describe("R74 每日变异通关收集", () => {
  it("每日局通关：变异计入 mutatorClears", () => {
    const sim = dailyVictory("elite");
    expect(clearsOf(sim).elite).toBe(1);
  });

  it("非每日局通关：变异未生效也不计入", () => {
    const sim = new GameSim(999, memoryStorage(), "sword", 0, {}, "normal", "gold");
    sim.exec({ action: "start" });
    (sim as unknown as { onVictory(): number }).onVictory();
    expect(clearsOf(sim).gold ?? 0).toBe(0);
  });

  it("五变异各通一次：授「历劫全域」", () => {
    const store = memoryStorage();
    for (const id of ["elite", "gold", "swift", "dense", "tough"]) dailyVictory(id, store);
    const last = dailyVictory("elite", store); // 第六局（重复变异）仍可复验
    expect(Object.keys(clearsOf(last)).sort()).toEqual(["dense", "elite", "gold", "swift", "tough"]);
    expect(last.achievements.unlocked.has("mutator_all")).toBe(true);
  });

  it("重复通关同变异：次数累加但键数不变（不提前满足五键）", () => {
    const store = memoryStorage();
    dailyVictory("elite", store);
    dailyVictory("elite", store);
    const sim = new GameSim(dailySeed(), store, "sword", 0, {}, "normal", "elite");
    sim.exec({ action: "start" });
    expect(clearsOf(sim).elite).toBe(2); // 同 id 通关数累加
    expect(Object.keys(clearsOf(sim))).toEqual(["elite"]);
    expect(sim.achievements.unlocked.has("mutator_all")).toBe(false);
  });

  it("normalizeSave：老存档缺 mutatorClears 回退空表", () => {
    const d = normalizeSave({ highScore: 5 } as never);
    expect(d.mutatorClears).toEqual({});
    expect(defaultSave().mutatorClears).toEqual({});
  });
});
