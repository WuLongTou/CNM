import { describe, expect, it } from "vitest";
import { GameSim, mergeCodex, type Enemy } from "../../src/sim/game";
import { defaultSave, memoryStorage, type SaveData } from "../../src/sim/save";

function slay(sim: GameSim, type: number): void {
  sim.exec({ action: "spawn", args: { type, near: "player", count: 1 } });
  const e = sim.enemies[sim.enemies.length - 1];
  (sim as unknown as { onEnemyKilled(e: Enemy): void }).onEnemyKilled(e);
}

function killPlayer(sim: GameSim) {
  sim.player.hp = 1;
  sim.player.invincible = 0;
  sim.exec({ action: "spawn", args: { type: "basic", near: { x: sim.player.x + 10, y: sim.player.y }, count: 1 } });
  sim.enemies[0].damage = 9999;
  for (let i = 0; i < 600 && sim.state !== "GameOver"; i++) sim.tick();
  expect(sim.state).toBe("GameOver");
}

describe("R15 妖物图鉴讨伐计数", () => {
  it("mergeCodex：累加合并，缺字段按空处理", () => {
    expect(mergeCodex({ 0: 5 }, { 0: 2, 1: 1 })).toEqual({ 0: 7, 1: 1 });
    expect(mergeCodex(undefined, { 2: 3 })).toEqual({ 2: 3 });
    expect(mergeCodex({}, {})).toEqual({});
  });

  it("击杀按类型计数（本局）", () => {
    const sim = new GameSim(141, memoryStorage());
    sim.exec({ action: "start" });
    slay(sim, 0); slay(sim, 0); slay(sim, 1); slay(sim, 12);
    expect(sim.killCounts).toEqual({ 0: 2, 1: 1, 12: 1 });
  });

  it("codex 协议：本局 + 跨局实时合并", () => {
    const save = memoryStorage();
    save.save({ ...defaultSave(), codex: { 0: 10 } });
    const sim = new GameSim(141, save);
    sim.exec({ action: "start" });
    slay(sim, 0); slay(sim, 1);
    const codex = sim.exec({ action: "codex" }).data as {
      run: Record<number, number>; total: Record<number, number>;
    };
    expect(codex.run).toEqual({ 0: 1, 1: 1 });
    expect(codex.total).toEqual({ 0: 11, 1: 1 });
  });

  it("结算入库：死亡并入存档，跨局累加且不丢其他字段", () => {
    const save = memoryStorage();
    save.save({ ...defaultSave(), coins: 200, metaHp: 2, codex: { 0: 5 } });
    const sim = new GameSim(141, save);
    sim.exec({ action: "buy_meta", args: { kind: "hp" } });
    sim.exec({ action: "start" });
    slay(sim, 0); slay(sim, 2);
    killPlayer(sim);
    expect(save.data.codex).toEqual({ 0: 6, 2: 1 });
    expect(save.data.metaHp).toBe(3);
    // 第二局继续累加
    const sim2 = new GameSim(142, save);
    sim2.exec({ action: "start" });
    slay(sim2, 2);
    killPlayer(sim2);
    expect(save.data.codex).toEqual({ 0: 6, 2: 2 });
  });

  it("老存档兼容：缺 codex 照常用，结算后补齐", () => {
    const legacy = { ...defaultSave(), codex: undefined as unknown as Record<number, number> };
    const save = memoryStorage();
    save.save(legacy as SaveData);
    const sim = new GameSim(141, save);
    sim.exec({ action: "start" });
    slay(sim, 3);
    const codex = sim.exec({ action: "codex" }).data as { total: Record<number, number> };
    expect(codex.total).toEqual({ 3: 1 });
    killPlayer(sim);
    expect(save.data.codex).toEqual({ 3: 1 });
  });
});
