// R110 无伤通关成就「铁壁」：hurtPlayer 受伤累计 + onVictory 直授。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { ACHIEVEMENTS } from "../../src/sim/achievements";

const hurt = (sim: GameSim, dmg: number) =>
  (sim as unknown as { hurtPlayer(d: number): boolean }).hurtPlayer(dmg);

describe("R110 无伤通关「铁壁」", () => {
  it("受伤累计 damageTakenRun；无敌帧/完美闪避不计数（hurtPlayer 早退）", () => {
    const sim = new GameSim(1100, memoryStorage());
    sim.exec({ action: "start" });
    hurt(sim, 10);
    (sim.player as unknown as { invincible: number }).invincible = 0; // 重置无敌帧再吃第二刀
    hurt(sim, 5);
    expect(sim.damageTakenRun).toBe(15);
  });

  it("无伤通关：onVictory 直授 铁壁", () => {
    const sim = new GameSim(1101, memoryStorage());
    sim.exec({ action: "start" });
    (sim as unknown as { onVictory(): number }).onVictory();
    expect(sim.achievements.unlocked.has("flawless")).toBe(true);
  });

  it("受过伤通关：不授铁壁", () => {
    const sim = new GameSim(1102, memoryStorage());
    sim.exec({ action: "start" });
    hurt(sim, 10);
    (sim as unknown as { onVictory(): number }).onVictory();
    expect(sim.achievements.unlocked.has("flawless")).toBe(false);
  });

  it("成就表 39 项：铁壁 check 型（终局直授）", () => {
    expect(ACHIEVEMENTS).toHaveLength(39);
    const a = ACHIEVEMENTS.find((x) => x.id === "flawless")!;
    expect(a.check).toBeTruthy();
    expect(a.goal).toBeUndefined();
  });
});