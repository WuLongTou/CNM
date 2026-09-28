// R64 生涯统计：陨落结算累计 / 无尽二次结算按局口径防重 / 存档归一化迁移 /
// 欢迎页陈列行格式化。formatter 在 src/ui/hud.ts（纯函数），其余在 sim/save+game。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage, normalizeSave, defaultStats } from "../../src/sim/save";
import { formatLifetime, lifetimeLine } from "../../src/ui/hud";

/** 把玩家打到 1 血并钉一只游魂贴脸，tick 到 GameOver（走真实 onPlayerDied 结算）。 */
function dieOnce(sim: GameSim): void {
  sim.exec({ action: "spawn", args: { type: "游魂", count: 1, near: "player" } });
  sim.player.hp = 1;
  const e = sim.enemies[sim.enemies.length - 1]!;
  e.x = sim.player.x;
  e.y = sim.player.y;
  for (let i = 0; i < 900 && sim.state !== "GameOver"; i++) sim.tick(1 / 60);
  expect(sim.state).toBe("GameOver");
}

describe("R64 生涯统计累计", () => {
  it("陨落结算：七项指标按局口径入档", () => {
    const store = memoryStorage();
    const sim = new GameSim(64, store);
    sim.exec({ action: "start" });
    sim.survivalTime = 120;
    sim.maxCombo = 42;
    sim.eliteKills = 3;
    sim.bossKills = 2;
    dieOnce(sim);
    const s = store.data.stats;
    expect(s.timeAlive).toBeGreaterThanOrEqual(120); // tick 循环内 survivalTime 继续推进
    expect(s.timeAlive).toBeLessThan(130);
    expect(s.maxCombo).toBe(42);
    expect(s.maxKills).toBe(0); // 挂机被杀，零击杀
    expect(s.totalElites).toBe(3);
    expect(s.totalBosses).toBe(2);
    expect(s.gamesByRole["sword"]).toBe(1);
    expect(s.coinsEarned).toBeGreaterThanOrEqual(0);
  });

  it("Victory→无尽→陨落二次结算：按局口径只计一次（金币按实发累加）", () => {
    const store = memoryStorage();
    const sim = new GameSim(65, store);
    sim.exec({ action: "start" });
    sim.survivalTime = 100;
    dieOnce(sim);
    const first = { ...store.data.stats };
    // 模拟通关后无尽再陨落：胜利结算 → continue → 再死
    sim.state = "Victory";
    expect((sim.exec({ action: "continue" }).data as { continued: boolean }).continued).toBe(true);
    // 真实无尽流中通关时玩家存活；测试直改过死亡标记需一并复原
    sim.player.dead = false;
    sim.survivalTime = 200; // 若无守卫将双计为 300
    dieOnce(sim);
    const second = store.data.stats;
    expect(second.timeAlive).toBe(first.timeAlive); // 防重：不随二次结算膨胀
    expect(second.gamesByRole["sword"]).toBe(1);
    expect(second.maxCombo).toBe(first.maxCombo);
    expect(second.coinsEarned).toBeGreaterThanOrEqual(first.coinsEarned); // 金币实发累加
    expect(store.data.games).toBe(2); // 既有口径：二次结算 games 各计一次（不属本 Round 改动面）
  });
});

describe("normalizeSave 存档归一化", () => {
  it("老存档缺 stats → 全默认", () => {
    const old = { highScore: 100, games: 2 } as Record<string, unknown>;
    const d = normalizeSave(old as never);
    expect(d.stats).toEqual(defaultStats());
    expect(d.highScore).toBe(100);
  });

  it("半新存档 stats 缺字段 → 逐项回退默认、已有值保留", () => {
    const partial = { stats: { timeAlive: 30, coinsEarned: 7 } } as never;
    const d = normalizeSave(partial);
    expect(d.stats.timeAlive).toBe(30);
    expect(d.stats.coinsEarned).toBe(7);
    expect(d.stats.maxCombo).toBe(0);
    expect(d.stats.gamesByRole).toEqual({});
  });

  it("null/空入参 → 全默认存档", () => {
    expect(normalizeSave(null)).toEqual(normalizeSave({}));
  });
});

describe("生涯陈列格式化", () => {
  it("时长：<1h 显示分钟，≥1h 显示一位小数小时", () => {
    expect(formatLifetime(45)).toBe("1 分钟");
    expect(formatLifetime(300)).toBe("5 分钟");
    expect(formatLifetime(3600)).toBe("1.0 小时");
    expect(formatLifetime(5400)).toBe("1.5 小时");
  });

  it("生涯行含全部七项与最爱职业", () => {
    const line = lifetimeLine({
      timeAlive: 5400, coinsEarned: 1234, gamesByRole: { sword: 3, archer: 5 },
      maxKills: 800, maxCombo: 120, totalElites: 66, totalBosses: 7,
    });
    expect(line).toContain("存活 1.5 小时");
    expect(line).toContain("金币 1234");
    expect(line).toContain("最高连击 120");
    expect(line).toContain("单局击杀 800");
    expect(line).toContain("精英 66");
    expect(line).toContain("妖王 7");
    expect(line).toContain("最爱 弓手（5 局）"); // 取局数最多者
  });

  it("零对局口径无最爱段（不显示 — 占位）", () => {
    const line = lifetimeLine(defaultStats());
    expect(line).not.toContain("最爱");
    expect(line).toContain("存活 1 分钟"); // 0s 归一到 1 分钟下限（避免"0 分钟"怪异）
  });
});
