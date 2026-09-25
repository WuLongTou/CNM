import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { DIFFICULTIES } from "../../src/sim/difficulty";
import { defaultSave, memoryStorage } from "../../src/sim/save";

function saveWith(maxDifficulty: number) {
  const save = memoryStorage();
  save.save({ ...defaultSave(), maxDifficulty });
  return save;
}

/** 同 seed 刷一批妖物，取其中非精英的血量（排除 2% 随机精英干扰）。 */
function plainHp(sim: GameSim): number[] {
  sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 10 } });
  return sim.enemies.filter((e) => !e.elite && !e.boss).map((e) => e.maxHp);
}

/** 杀死玩家走完死亡结算。 */
function killPlayer(sim: GameSim) {
  sim.player.hp = 1;
  sim.player.invincible = 0;
  sim.exec({ action: "spawn", args: { type: "basic", near: { x: sim.player.x + 10, y: sim.player.y }, count: 1 } });
  sim.enemies[0].damage = 9999;
  for (let i = 0; i < 600 && sim.state !== "GameOver"; i++) sim.tick();
  expect(sim.state).toBe("GameOver");
}

describe("R10 讨伐难度", () => {
  it("难度表三档，乘数与解锁链齐备", () => {
    expect(Object.values(DIFFICULTIES).map((d) => d.id)).toEqual([0, 1, 2]);
    expect(DIFFICULTIES[0]).toMatchObject({ foeMult: 1, coinMult: 1, unlockBy: null });
    expect(DIFFICULTIES[1]).toMatchObject({ foeMult: 1.25, coinMult: 1.25, unlockBy: 0 });
    expect(DIFFICULTIES[2]).toMatchObject({ foeMult: 1.6, coinMult: 1.6, unlockBy: 1 });
  });

  it("默认平妖；妖物血量按难度同乘（速度不动）", () => {
    // 首阶倍率 1.096：游魂 25 血 → 平妖 ceil(27.4)=28 / 伏魔 35 / 镇狱 44
    const base = new GameSim(81, saveWith(1));
    const hb = plainHp(base);
    expect(hb.length).toBeGreaterThan(0);
    for (const hp of hb) expect(hp).toBe(28);
    const hard = new GameSim(81, saveWith(1), "sword", 1);
    for (const hp of plainHp(hard)) expect(hp).toBe(35);
    const hell = new GameSim(81, saveWith(1), "sword", 2);
    const hh = plainHp(hell);
    expect(hh.length).toBeGreaterThan(0);
    for (const hp of hh) expect(hp).toBe(44);
    expect(hell.enemies[0].speed).toBe(base.enemies[0].speed);
  });

  it("锁定难度：构造抛错，setDifficulty 拒绝（未知 id 仍抛错）", () => {
    expect(() => new GameSim(81, saveWith(-1), "sword", 1)).toThrow("难度未解锁");
    expect(() => new GameSim(81, saveWith(0), "sword", 2)).toThrow("难度未解锁");
    const sim = new GameSim(81, saveWith(-1));
    expect(sim.isDifficultyUnlocked(0)).toBe(true);
    expect(sim.isDifficultyUnlocked(1)).toBe(false);
    const r = sim.setDifficulty(1) as { applied: boolean };
    expect(r.applied).toBe(false);
    expect(sim.difficulty.id).toBe(0);
    expect(() => sim.setDifficulty(9)).toThrow();
    expect(sim.exec({ action: "set_difficulty", args: { difficulty: 9 } }).ok).toBe(false);
  });

  it("setDifficulty 仅开局前可换", () => {
    const sim = new GameSim(81, saveWith(0));
    expect(sim.setDifficulty(1)).toMatchObject({ applied: true, difficulty: 1 });
    sim.exec({ action: "start" });
    expect(sim.setDifficulty(0)).toMatchObject({ applied: false });
    expect(sim.difficulty.id).toBe(1);
  });

  it("通关刷新最高难度并广播解锁（镇狱通关无下一档）", () => {
    const save = saveWith(-1);
    const sim = new GameSim(81, save);
    sim.score = 500; sim.kills = 10;
    const events: string[] = [];
    sim.onEvent((e) => events.push(e.event));
    (sim as unknown as { onVictory(): number }).onVictory();
    expect(save.data.maxDifficulty).toBe(0);
    expect(events).toContain("difficulty_unlocked");
    const unlock = sim.drainEvents().find((e) => e.event === "difficulty_unlocked");
    expect(unlock?.data).toMatchObject({ id: 1, name: "伏魔" });
    // 金币：floor((500/50 + 10) × 1.5 × 1.25?) 平妖 coinMult=1 → 30
    expect(save.data.coins).toBe(30);
    // 伏魔通关解锁镇狱，金币吃 1.25
    const save2 = saveWith(0);
    const sim2 = new GameSim(81, save2, "sword", 1);
    sim2.score = 500; sim2.kills = 10;
    (sim2 as unknown as { onVictory(): number }).onVictory();
    expect(save2.data.maxDifficulty).toBe(1);
    expect(save2.data.coins).toBe(37);
    const unlock2 = sim2.drainEvents().find((e) => e.event === "difficulty_unlocked");
    expect(unlock2?.data).toMatchObject({ id: 2 });
    // 镇狱通关：无下一档可广播，纪录照记
    const save3 = saveWith(1);
    const sim3 = new GameSim(81, save3, "sword", 2);
    const ev3: string[] = [];
    sim3.onEvent((e) => ev3.push(e.event));
    (sim3 as unknown as { onVictory(): number }).onVictory();
    expect(save3.data.maxDifficulty).toBe(2);
    expect(ev3).not.toContain("difficulty_unlocked");
  });

  it("死亡不刷新最高难度", () => {
    const save = saveWith(0);
    const sim = new GameSim(81, save, "sword", 1);
    sim.exec({ action: "start" });
    sim.score = 500; sim.kills = 10;
    killPlayer(sim);
    expect(save.data.maxDifficulty).toBe(0);
    // 死亡结算金币同样吃难度加成：floor(20 × 1 × 1.25) = 25
    expect(save.data.coins).toBe(25);
  });

  it("老存档兼容：缺 maxDifficulty 仅平妖开放", () => {
    const legacy = { ...defaultSave(), maxDifficulty: undefined as unknown as number };
    const save = memoryStorage();
    save.save(legacy);
    const sim = new GameSim(81, save);
    expect(sim.isDifficultyUnlocked(0)).toBe(true);
    expect(sim.isDifficultyUnlocked(1)).toBe(false);
  });

  it("difficulties 协议：列表携带开放态；set_difficulty 路由生效", () => {
    const sim = new GameSim(81, saveWith(0));
    const list = sim.exec({ action: "difficulties" }).data as {
      current: number; list: Array<{ id: number; unlocked: boolean }>;
    };
    expect(list.current).toBe(0);
    expect(list.list.map((d) => [d.id, d.unlocked])).toEqual([[0, true], [1, true], [2, false]]);
    const set = sim.exec({ action: "set_difficulty", args: { difficulty: 1 } });
    expect(set.ok).toBe(true);
    expect(sim.difficulty.id).toBe(1);
    expect(sim.status()).toMatchObject({ difficulty: 1, difficulty_name: "伏魔" });
  });
});
