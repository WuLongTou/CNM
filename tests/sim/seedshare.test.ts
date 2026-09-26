import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { parseSeed, shareText } from "../../src/ui/hud";
import { memoryStorage } from "../../src/sim/save";

describe("R22 种子与战绩分享", () => {
  it("status 暴露构造种子", () => {
    expect(new GameSim(777, memoryStorage()).status()).toMatchObject({ seed: 777 });
    expect(new GameSim(12345, memoryStorage()).status()).toMatchObject({ seed: 12345 });
  });

  it("parseSeed：有效数字通过，无效回 null", () => {
    expect(parseSeed("123")).toBe(123);
    expect(parseSeed("  007  ")).toBe(7);
    expect(parseSeed("999999999")).toBe(999999999);
    expect(parseSeed("")).toBeNull();
    expect(parseSeed("   ")).toBeNull();
    expect(parseSeed("abc")).toBeNull();
    expect(parseSeed("0")).toBeNull();
    expect(parseSeed("-5")).toBeNull();
    expect(parseSeed("12.5")).toBeNull();
    expect(parseSeed("1000000000")).toBeNull();
    expect(parseSeed("12a")).toBeNull();
  });

  it("shareText：单行速通格式，含种子与通关标记", () => {
    expect(shareText({
      score: 1234, kills: 56, survivalTime: 321.6, round: 7,
      roleName: "剑修", difficultyName: "伏魔", seed: 777, cleared: false,
    })).toBe("【斩妖录】陨落 · 分数1234 · 击杀56 · 存活322s · 威胁阶7 · 剑修/伏魔难度 · 种子777");
    expect(shareText({
      score: 1, kills: 1, survivalTime: 1, round: 13,
      roleName: "术士", difficultyName: "镇狱", seed: 42, cleared: true,
    })).toContain("通关");
  });

  it("同种子同输入同结果（种子可复现整局）", () => {
    const run = (seed: number) => {
      const sim = new GameSim(seed, memoryStorage());
      sim.exec({ action: "start" });
      sim.input.attack = true;
      for (let i = 0; i < 300; i++) sim.tick();
      return {
        x: sim.player.x, y: sim.player.y, hp: sim.player.hp,
        kills: sim.kills, score: sim.score, foes: sim.enemies.length,
      };
    };
    expect(run(777)).toEqual(run(777));
    expect(run(777)).not.toEqual(run(778));
  });
});
