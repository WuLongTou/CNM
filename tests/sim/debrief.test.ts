import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { CFG } from "../../src/sim/config";
import { memoryStorage } from "../../src/sim/save";
import type { Enemy } from "../../src/sim/game";

function slay(sim: GameSim, opts: { elite?: boolean; boss?: boolean }): void {
  sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1, elite: !!opts.elite, boss: !!opts.boss } });
  const e = sim.enemies[sim.enemies.length - 1];
  (sim as unknown as { onEnemyKilled(e: Enemy): void }).onEnemyKilled(e);
}

describe("R9 战斗反馈闭环", () => {
  it("初始统计为 0，快照携带复盘字段", () => {
    const sim = new GameSim(71, memoryStorage());
    expect(sim.eliteKills).toBe(0);
    expect(sim.maxCombo).toBe(0);
    expect(sim.status()).toMatchObject({ elite_kills: 0, max_combo: 0 });
  });

  it("精英击杀计入 eliteKills；普通不计；妖王单计 bossKills", () => {
    const sim = new GameSim(71, memoryStorage());
    sim.exec({ action: "start" });
    slay(sim, {});
    expect(sim.eliteKills).toBe(0);
    slay(sim, { elite: true });
    slay(sim, { elite: true });
    expect(sim.eliteKills).toBe(2);
    slay(sim, { boss: true });
    expect(sim.bossKills).toBe(1);
    expect(sim.eliteKills).toBe(2);
    expect(sim.status()).toMatchObject({ elite_kills: 2 });
  });

  it("最高连击：命中刷新、衰减不回落", () => {
    const sim = new GameSim(71, memoryStorage());
    sim.exec({ action: "start" });
    sim.player.x = 500; sim.player.y = 500;
    sim.exec({ action: "spawn", args: { type: 2, near: { x: 540, y: 500 }, count: 1 } });
    const e = sim.enemies[sim.enemies.length - 1];
    e.hp = 9999; e.maxHp = 9999; e.damage = 0; e.speed = 0;
    sim.input.attack = true;
    for (let i = 0; i < 90; i++) {
      if (i % 30 === 0) { e.x = sim.player.x + 40; e.y = sim.player.y; } // 击退拉回木桩
      sim.tick();
    }
    expect(sim.player.combo).toBeGreaterThanOrEqual(3);
    expect(sim.maxCombo).toBeGreaterThanOrEqual(3);
    const peak = sim.maxCombo;
    sim.input.attack = false;
    for (let i = 0; i < 150; i++) sim.tick(); // 2.5s 停手，连击衰减
    expect(sim.player.combo).toBe(0);
    expect(sim.maxCombo).toBe(peak);
    expect(sim.status()).toMatchObject({ max_combo: peak });
  });

  it("进阶倒计时：开局 45s，随 tick 递减，跳阶清零", () => {
    const sim = new GameSim(71, memoryStorage());
    sim.exec({ action: "start" });
    expect(sim.secsToNextTier).toBe(CFG.Difficulty.RampInterval);
    for (let i = 0; i < 60; i++) sim.tick();
    expect(sim.secsToNextTier).toBe(CFG.Difficulty.RampInterval - 1);
    sim.exec({ action: "goto_round", args: { round: 5 } });
    expect(sim.secsToNextTier).toBe(0);
    expect(sim.status()).toMatchObject({ secs_to_next_tier: 0 });
  });

  it("威胁阶推进广播 threat_tier（含阶数）", () => {
    const sim = new GameSim(71, memoryStorage());
    sim.exec({ action: "start" });
    sim.round = 1;
    sim.roundTimer = CFG.Difficulty.RampInterval - 0.01;
    sim.tick();
    expect(sim.round).toBe(2);
    const ev = sim.drainEvents().find((e) => e.event === "threat_tier");
    expect(ev?.data).toMatchObject({ tier: 2 });
  });
});
