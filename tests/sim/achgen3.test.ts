// R135 成就谱系第三轮：万妖斩（累计 5000）/雷劫余威（连战 20）/满级宗师（终极通关）。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { ACHIEVEMENTS } from "../../src/sim/achievements";


const victory = (sim: GameSim) =>
  (sim as unknown as { onVictory(): number }).onVictory();

describe("R135 成就谱系第三轮", () => {
  it("表 42 项：两条 goal 型 + 一条 check 型", () => {
    expect(ACHIEVEMENTS).toHaveLength(44); // R205 战功中段 +2
    const sk = ACHIEVEMENTS.find((x) => x.id === "slayer_5000")!;
    expect(sk.goal).toEqual({ stat: "totalKills", target: 5000 });
    const rv = ACHIEVEMENTS.find((x) => x.id === "rush_veteran")!;
    expect(rv.goal).toEqual({ stat: "bossRushKills", target: 20 });
    const um = ACHIEVEMENTS.find((x) => x.id === "ultmaster")!;
    expect(um.check).toBeTruthy();
  });

  it("万妖斩：totalKills 5000 tick 授（跨局累计口径）", () => {
    const save = memoryStorage();
    const s1 = new GameSim(1350, save);
    s1.exec({ action: "start" });
    (s1 as unknown as { baseTotalKills: number }).baseTotalKills = 4990;
    (s1 as unknown as { kills: number }).kills = 10;
    s1.achievements.update((s1 as unknown as { achievementState(): ReturnType<GameSim["achievementState"]> }).achievementState());
    expect(s1.achievements.unlocked.has("slayer_5000")).toBe(true);
  });

  it("雷劫余威：连战累计 20 授（tick 判定）", () => {
    const sim = new GameSim(1351, memoryStorage(), "sword", 0, {}, "boss_rush");
    sim.exec({ action: "start" });
    (sim as unknown as { bossRushKills: number }).bossRushKills = 20;
    sim.achievements.update((sim as unknown as { achievementState(): ReturnType<GameSim["achievementState"]> }).achievementState());
    expect(sim.achievements.unlocked.has("rush_veteran")).toBe(true);
  });

  it("满级宗师：终极武器通关授；非终极不授", () => {
    const s1 = new GameSim(1352, memoryStorage());
    s1.exec({ action: "start" });
    const w1 = s1.player.weapons[0]!;
    w1.ups = 6; // ULTIMATE_AT
    victory(s1);
    expect(s1.achievements.unlocked.has("ultmaster")).toBe(true);

    const s2 = new GameSim(1353, memoryStorage());
    s2.exec({ action: "start" });
    const w2 = s2.player.weapons[0]!;
    w2.ups = 5; // 进阶未终极
    victory(s2);
    expect(s2.achievements.unlocked.has("ultmaster")).toBe(false);
  });
});