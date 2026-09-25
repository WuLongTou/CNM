import { describe, expect, it } from "vitest";
import { GameSim, type Enemy } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

function readySim(seed = 201) {
  const sim = new GameSim(seed, memoryStorage());
  sim.exec({ action: "start" });
  return sim;
}

function slay(sim: GameSim, opts: { elite?: boolean; boss?: boolean }): void {
  sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1, elite: !!opts.elite, boss: !!opts.boss } });
  const e = sim.enemies[sim.enemies.length - 1];
  (sim as unknown as { onEnemyKilled(e: Enemy): void }).onEnemyKilled(e);
}

function placeOrb(sim: GameSim) {
  sim.player.x = 500; sim.player.y = 500;
  sim.healOrbs.push({ id: 9801, x: 510, y: 500 });
}

describe("R20 回春丹", () => {
  it("走近回复 12 生命，不超上限", () => {
    const sim = readySim();
    sim.player.hp = 100;
    placeOrb(sim);
    sim.tick();
    expect(sim.healOrbs.length).toBe(0);
    expect(sim.player.hp).toBe(112);
    const ev = sim.drainEvents().find((e) => e.event === "heal_picked");
    expect(ev?.data).toMatchObject({ heal: 12 });
  });

  it("回复量按缺口封顶；满血不拾取（留地上不浪费）", () => {
    const sim = readySim();
    sim.player.hp = sim.player.maxHp - 5;
    placeOrb(sim);
    sim.tick();
    expect(sim.player.hp).toBe(sim.player.maxHp);
    const sim2 = readySim(202);
    sim2.player.hp = sim2.player.maxHp;
    sim2.player.x = 500; sim2.player.y = 500;
    sim2.healOrbs.push({ id: 9802, x: 510, y: 500 });
    sim2.tick();
    expect(sim2.healOrbs.length).toBe(1);
  });

  it("普通击杀低概率掉落（300 杀必见）", () => {
    const sim = readySim();
    for (let i = 0; i < 300; i++) slay(sim, {});
    expect(sim.healOrbs.length).toBeGreaterThanOrEqual(1);
  });

  it("精英掉率更高（40 杀必见）；妖王必掉 1 枚", () => {
    const sim = readySim(203);
    for (let i = 0; i < 40; i++) slay(sim, { elite: true });
    expect(sim.healOrbs.length).toBeGreaterThanOrEqual(1);
    const sim2 = readySim(204);
    const before = sim2.healOrbs.length;
    slay(sim2, { boss: true });
    expect(sim2.healOrbs.length).toBe(before + 1);
  });

  it("阵亡不拾取", () => {
    const sim = readySim();
    sim.player.hp = 50;
    placeOrb(sim);
    (sim as unknown as { onPlayerDied(): void }).onPlayerDied();
    sim.tick();
    expect(sim.healOrbs.length).toBe(1);
  });
});
