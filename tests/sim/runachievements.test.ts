import { describe, expect, it } from "vitest";
import { runAchievementLine } from "../../src/ui/hud";
import { GameSim, type Enemy } from "../../src/sim/game";
import { defaultSave, memoryStorage } from "../../src/sim/save";

describe("R53 本局成就陈列", () => {
  it("陈列行：空串 / 单项 / 多项", () => {
    expect(runAchievementLine([])).toBe("");
    expect(runAchievementLine(["初试锋芒"])).toBe("本局成就 1 项：初试锋芒");
    expect(runAchievementLine(["初试锋芒", "斩妖大将"])).toBe("本局成就 2 项：初试锋芒 / 斩妖大将");
  });

  it("本局达成入列；开局前已解锁的不入列", () => {
    const save = memoryStorage();
    save.save({ ...defaultSave(), achievements: ["first_blood"] });
    const sim = new GameSim(531, save);
    sim.exec({ action: "start" });
    expect(sim.runAchievements).toEqual([]);
    sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1 } });
    const e = sim.enemies[sim.enemies.length - 1];
    (sim as unknown as { onEnemyKilled(e: Enemy): void }).onEnemyKilled(e);
    sim.tick();
    // first_blood 已解锁不入列；但击杀可能触发其它新成就（若触发应有名）
    expect(sim.runAchievements.every((a) => a.id !== "first_blood")).toBe(true);
    const snap = sim.status() as { run_achievements: string[] };
    expect(snap.run_achievements).toEqual(sim.runAchievements.map((a) => a.name));
  });

  it("新档首杀入列并快照同步", () => {
    const sim = new GameSim(532, memoryStorage());
    sim.exec({ action: "start" });
    sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1 } });
    const e = sim.enemies[sim.enemies.length - 1];
    (sim as unknown as { onEnemyKilled(e: Enemy): void }).onEnemyKilled(e);
    sim.tick();
    expect(sim.runAchievements.map((a) => a.id)).toContain("first_blood");
    expect((sim.status() as { run_achievements: string[] }).run_achievements).toContain("初试锋芒");
  });
});
