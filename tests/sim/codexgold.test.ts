import { describe, expect, it } from "vitest";
import { CODEX_GOLD_THRESHOLD, countGold, GameSim, type Enemy } from "../../src/sim/game";
import { ACHIEVEMENTS } from "../../src/sim/achievements";
import { defaultSave, memoryStorage } from "../../src/sim/save";

function fullCodex(n: number): Record<number, number> {
  const c: Record<number, number> = {};
  for (let i = 0; i < 14; i++) c[i] = n;
  return c;
}

describe("R27 图鉴金边", () => {
  it("countGold：阈值合并计数", () => {
    expect(CODEX_GOLD_THRESHOLD).toBe(100);
    expect(countGold({}, {})).toBe(0);
    expect(countGold({ 0: 100 }, {})).toBe(1);
    expect(countGold({ 0: 99 }, {})).toBe(0);
    expect(countGold({ 0: 99 }, { 0: 1 })).toBe(1);
    expect(countGold({ 0: 50, 1: 100, 2: 7 }, { 0: 60, 2: 1 })).toBe(2);
    expect(countGold(undefined, { 5: 200 })).toBe(1);
  });

  it("十四金即授博物学家；差一种不授", () => {
    const save = memoryStorage();
    save.save({ ...defaultSave(), codex: fullCodex(100) });
    const sim = new GameSim(271, save);
    sim.exec({ action: "start" });
    sim.tick();
    expect(sim.achievements.unlocked.has("naturalist")).toBe(true);
    const save2 = memoryStorage();
    save2.save({ ...defaultSave(), codex: { ...fullCodex(100), 13: 99 } });
    const sim2 = new GameSim(271, save2);
    sim2.exec({ action: "start" });
    sim2.tick();
    expect(sim2.achievements.unlocked.has("naturalist")).toBe(false);
    expect(ACHIEVEMENTS.length).toBe(33); // R47 妖王猎手
  });

  it("本局击杀补足最后一块拼图", () => {
    const save = memoryStorage();
    save.save({ ...defaultSave(), codex: { ...fullCodex(100), 0: 99 } });
    const sim = new GameSim(271, save);
    sim.exec({ action: "start" });
    sim.tick();
    expect(sim.achievements.unlocked.has("naturalist")).toBe(false);
    sim.exec({ action: "spawn", args: { type: 0, near: "player", count: 1 } });
    const e = sim.enemies[sim.enemies.length - 1];
    (sim as unknown as { onEnemyKilled(e: Enemy): void }).onEnemyKilled(e);
    sim.tick();
    expect(sim.achievements.unlocked.has("naturalist")).toBe(true);
  });
});
