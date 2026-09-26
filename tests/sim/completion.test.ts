import { describe, expect, it } from "vitest";
import { clearsTotal, GameSim, recordClear } from "../../src/sim/game";
import { ACHIEVEMENTS } from "../../src/sim/achievements";
import { defaultSave, memoryStorage, type SaveData } from "../../src/sim/save";

function winWith(save: ReturnType<typeof memoryStorage>, seed: number, role: string, diff: number) {
  const sim = new GameSim(seed, save, role, diff);
  (sim as unknown as { onVictory(): number }).onVictory();
  return sim;
}

describe("R21 制霸矩阵", () => {
  it("recordClear 合并去重排序，缺字段按空处理", () => {
    expect(recordClear({ sword: [0] }, "sword", 1)).toEqual({ sword: [0, 1] });
    expect(recordClear({ sword: [0, 1] }, "sword", 1)).toEqual({ sword: [0, 1] });
    expect(recordClear(undefined, "mage", 0)).toEqual({ mage: [0] });
    expect(clearsTotal({ sword: [0, 1], mage: [0] })).toBe(3);
    expect(clearsTotal(undefined)).toBe(0);
  });

  it("通关记录职业×难度；死亡不记", () => {
    const save = memoryStorage();
    winWith(save, 211, "sword", 0);
    expect(save.data.clears).toEqual({ sword: [0] });
    const sim = new GameSim(212, save);
    sim.exec({ action: "start" });
    sim.player.hp = 1; sim.player.invincible = 0;
    sim.exec({ action: "spawn", args: { type: "basic", near: { x: sim.player.x + 10, y: sim.player.y }, count: 1 } });
    sim.enemies[0].damage = 9999;
    for (let i = 0; i < 600 && sim.state !== "GameOver"; i++) sim.tick();
    expect(sim.state).toBe("GameOver");
    expect(save.data.clears).toEqual({ sword: [0] });
  });

  it("三英聚义：第三职业通关即达成并广播", () => {
    const save = memoryStorage();
    save.save({ ...defaultSave(), achievements: ["slayer_50", "boss_slayer"], clears: { sword: [0], archer: [0] } });
    const events: string[] = [];
    const sim = new GameSim(211, save, "mage", 0);
    sim.onEvent((e) => events.push(e.event));
    (sim as unknown as { onVictory(): number }).onVictory();
    expect(save.data.clears).toEqual({ sword: [0], archer: [0], mage: [0] });
    expect(sim.achievements.unlocked.has("three_heroes")).toBe(true);
    expect(events).toContain("achievement");
  });

  it("九界平定：集齐 9 格即达成", () => {
    const save = memoryStorage();
    save.save({
      ...defaultSave(),
      achievements: ["slayer_50", "boss_slayer"],
      maxDifficulty: 2,
      clears: { sword: [0, 1, 2], archer: [0, 1, 2], mage: [0, 1] },
    });
    const sim = new GameSim(211, save, "mage", 2);
    (sim as unknown as { onVictory(): number }).onVictory();
    expect(save.data.clears.mage).toEqual([0, 1, 2]);
    expect(sim.achievements.unlocked.has("nine_realms")).toBe(true);
    expect(sim.achievements.unlocked.has("three_heroes")).toBe(true);
  });

  it("重复通关同组合不重复计数", () => {
    const save = memoryStorage();
    winWith(save, 211, "sword", 0);
    winWith(save, 212, "sword", 0);
    expect(save.data.clears).toEqual({ sword: [0] });
  });

  it("老存档兼容：缺 clears 照常通关并补齐", () => {
    const legacy = { ...defaultSave(), clears: undefined as unknown as Record<string, number[]> };
    const save = memoryStorage();
    save.save(legacy as SaveData);
    const sim = new GameSim(211, save);
    (sim as unknown as { onVictory(): number }).onVictory();
    expect(save.data.clears).toEqual({ sword: [0] });
  });

  it("meta 动作返回制霸总数；成就表 20 项", () => {
    const save = memoryStorage();
    save.save({ ...defaultSave(), clears: { sword: [0, 1], mage: [2] } });
    const sim = new GameSim(211, save);
    const meta = sim.exec({ action: "meta" }).data as { clears: number };
    expect(meta.clears).toBe(3);
    expect(ACHIEVEMENTS.length).toBe(20);
  });
});
