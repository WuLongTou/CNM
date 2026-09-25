import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { ACHIEVEMENTS } from "../../src/sim/achievements";
import { defaultSave, memoryStorage } from "../../src/sim/save";

function offerSim(seed = 111) {
  const sim = new GameSim(seed, memoryStorage());
  sim.exec({ action: "start" });
  sim.pickups.length = 0;
  sim.player.x = 500; sim.player.y = 500;
  sim.pickups.push({ id: 9501, x: 510, y: 500 });
  sim.tick();
  return sim;
}

describe("R12 系统成就", () => {
  it("成就表 18 项，id 唯一", () => {
    expect(ACHIEVEMENTS.length).toBe(18);
    expect(new Set(ACHIEVEMENTS.map((a) => a.id)).size).toBe(18);
  });

  it("变数：重抽一次即达成并广播", () => {
    const sim = offerSim();
    sim.rerollUpgrade();
    sim.resolveUpgrade(0);
    sim.tick();
    expect(sim.achievements.unlocked.has("fate_twist")).toBe(true);
    const ev = sim.drainEvents().find((e) => e.event === "achievement");
    expect(ev?.data).toMatchObject({ id: "fate_twist" });
  });

  it("断舍离：放逐一次即达成", () => {
    const sim = offerSim();
    sim.banishUpgrade(0);
    sim.resolveUpgrade(0);
    sim.tick();
    expect(sim.achievements.unlocked.has("sever")).toBe(true);
  });

  it("猎手/猎魔人：精英讨伐 10/30", () => {
    const sim = new GameSim(111, memoryStorage());
    sim.exec({ action: "start" });
    sim.eliteKills = 10;
    sim.tick();
    expect(sim.achievements.unlocked.has("hunter_10")).toBe(true);
    expect(sim.achievements.unlocked.has("hunter_30")).toBe(false);
    sim.eliteKills = 30;
    sim.tick();
    expect(sim.achievements.unlocked.has("hunter_30")).toBe(true);
  });

  it("连绵不绝/万军取首：最高连击 20/50", () => {
    const sim = new GameSim(111, memoryStorage());
    sim.exec({ action: "start" });
    sim.maxCombo = 20;
    sim.tick();
    expect(sim.achievements.unlocked.has("combo_20")).toBe(true);
    expect(sim.achievements.unlocked.has("combo_50")).toBe(false);
    sim.maxCombo = 50;
    sim.tick();
    expect(sim.achievements.unlocked.has("combo_50")).toBe(true);
  });

  it("六械齐备：集齐 6 件武器", () => {
    const sim = new GameSim(111, memoryStorage());
    sim.exec({ action: "start" });
    for (const id of ["gongjian", "fazhang", "zhanfu", "shuangdao", "duandao"]) {
      sim.giveWeapon(id);
    }
    expect(sim.player.weapons.length).toBe(6);
    sim.tick();
    expect(sim.achievements.unlocked.has("arsenal")).toBe(true);
  });

  it("通关三成就：按难度发放，镇狱无遗漏", () => {
    const mk = (maxDiff: number, diff: number) => {
      const save = memoryStorage();
      save.save({ ...defaultSave(), maxDifficulty: maxDiff });
      const sim = new GameSim(111, save, "sword", diff);
      (sim as unknown as { onVictory(): number }).onVictory();
      return { sim, save };
    };
    const v0 = mk(-1, 0);
    expect(v0.sim.achievements.unlocked.has("pacify")).toBe(true);
    expect(v0.sim.achievements.unlocked.has("demon_queller")).toBe(false);
    const v1 = mk(0, 1);
    expect(v1.sim.achievements.unlocked.has("demon_queller")).toBe(true);
    expect(v1.sim.achievements.unlocked.has("warden")).toBe(false);
    const v2 = mk(1, 2);
    expect(v2.sim.achievements.unlocked.has("warden")).toBe(true);
  });

  it("通关成就不经 tick 误触发；grant 去重", () => {
    const sim = new GameSim(111, memoryStorage());
    sim.exec({ action: "start" });
    for (let i = 0; i < 100; i++) sim.tick();
    expect(sim.achievements.unlocked.has("pacify")).toBe(false);
    expect(sim.achievements.grant("pacify")).toBe(true);
    expect(sim.achievements.grant("pacify")).toBe(false);
    expect(sim.achievements.grant("nope")).toBe(false);
  });

  it("老成就回归：首杀判定不受影响", () => {
    const sim = new GameSim(111, memoryStorage());
    sim.exec({ action: "start" });
    sim.kills = 1;
    sim.tick();
    expect(sim.achievements.unlocked.has("first_blood")).toBe(true);
  });
});
