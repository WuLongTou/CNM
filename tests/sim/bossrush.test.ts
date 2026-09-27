import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

describe("R44 Boss Rush", () => {
  it("开战即出第一只妖王；普通模式不出", () => {
    const rush = new GameSim(441, memoryStorage(), "sword", 0, [], "boss_rush");
    rush.exec({ action: "start" });
    expect(rush.enemies.some((e) => e.boss)).toBe(true);
    const normal = new GameSim(441, memoryStorage());
    normal.exec({ action: "start" });
    expect(normal.enemies.some((e) => e.boss)).toBe(false);
  });

  it("无基础刷怪压力（maintainEnemies 跳过）", () => {
    const rush = new GameSim(441, memoryStorage(), "sword", 0, [], "boss_rush");
    rush.exec({ action: "start" });
    rush.enemies.length = 0; // 清掉首妖王，验证无补充刷怪
    for (let i = 0; i < 300; i++) rush.tick();
    expect(rush.enemies.filter((e) => !e.boss).length).toBe(0);
  });

  it("每阶一只妖王（goto_round 推进后第二只轮换登场）", () => {
    const rush = new GameSim(441, memoryStorage(), "sword", 0, [], "boss_rush");
    rush.exec({ action: "start" });
    const firstType = rush.enemies.find((e) => e.boss)!.type;
    const r = rush.exec({ action: "goto_round", args: { round: 2 } });
    expect(r.data).toMatchObject({ queued: true });
    rush.tick();
    const bosses = rush.enemies.filter((e) => e.boss);
    expect(bosses.length).toBeGreaterThanOrEqual(2);
    expect(bosses.some((e) => e.type !== firstType)).toBe(true);
  });

  it("终幕提前至第 6 阶（goto_round 6 触发终幕妖王）", () => {
    const rush = new GameSim(441, memoryStorage(), "sword", 0, [], "boss_rush");
    rush.exec({ action: "start" });
    const r = rush.exec({ action: "goto_round", args: { round: 6 } });
    expect(r.data).toMatchObject({ queued: true });
    rush.tick();
    expect(rush.enemies.some((e) => e.boss && e.final)).toBe(true);
  });

  it("快照带 boss_rush 标识", () => {
    const rush = new GameSim(441, memoryStorage(), "sword", 0, [], "boss_rush");
    expect(rush.status()).toMatchObject({ boss_rush: true });
    const normal = new GameSim(442, memoryStorage());
    expect(normal.status()).toMatchObject({ boss_rush: false });
  });
});
