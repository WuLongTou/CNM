import { describe, expect, it } from "vitest";
import { GameSim, type Enemy } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

function readySim(seed = 131) {
  const sim = new GameSim(seed, memoryStorage());
  sim.exec({ action: "start" });
  return sim;
}

/** 直达第四发事件（宝妖），返回宝妖实体。 */
function triggerGoblin(sim: GameSim): Enemy {
  sim.eventCount = 3;
  sim.eventTimer = 0.01;
  sim.tick();
  const g = sim.enemies.find((e) => e.goblin);
  expect(g).toBeTruthy();
  return g!;
}

describe("R14 宝妖游荡", () => {
  it("第四发为宝妖：属性配装正确（30 血/250 速/0 伤/15 秒）", () => {
    const sim = readySim();
    const g = triggerGoblin(sim);
    expect(g.hp).toBe(30);
    expect(g.maxHp).toBe(30);
    expect(g.speed).toBe(250);
    expect(g.damage).toBe(0);
    expect(g.affix).toBe("none");
    expect(g.goblinT).toBe(15);
    const ev = sim.drainEvents().find((e) => e.event === "map_event");
    expect(ev?.data).toMatchObject({ kind: "goblin", count: 1 });
  });

  it("只逃不打：远离玩家，贴脸 2 秒无蓄力无伤害", () => {
    const sim = readySim();
    const g = triggerGoblin(sim);
    sim.player.x = 500; sim.player.y = 500;
    g.x = 600; g.y = 600;
    const d0 = Math.hypot(100, 100);
    sim.input.attack = false;
    for (let i = 0; i < 60; i++) sim.tick();
    expect(Math.hypot(g.x - 500, g.y - 500)).toBeGreaterThan(d0 + 130);
    // 贴脸：无敌帧隔绝杂兵，只验宝妖不出手
    g.x = sim.player.x + 30; g.y = sim.player.y;
    sim.player.invincible = 9999;
    for (let i = 0; i < 120; i++) sim.tick();
    expect(g.attackWind).toBe(0);
    expect(sim.player.hp).toBe(sim.player.maxHp);
  });

  it("讨伐：爆精英箱 + 2 符箓 + 事件 + 成就", () => {
    const sim = readySim();
    const g = triggerGoblin(sim);
    const chestsBefore = sim.chests.length;
    sim.pickups.length = 0;
    (sim as unknown as { onEnemyKilled(e: Enemy): void }).onEnemyKilled(g);
    expect(sim.chests.length).toBe(chestsBefore + 1);
    const c = sim.chests[sim.chests.length - 1];
    expect(c.elite).toBe(true);
    expect(sim.pickups.length).toBeGreaterThanOrEqual(2);
    expect(sim.goblinKills).toBe(1);
    const slayed = sim.drainEvents().find((e) => e.event === "goblin_slayed");
    expect(slayed).toBeTruthy();
    sim.tick();
    expect(sim.achievements.unlocked.has("goblin_slayer")).toBe(true);
  });

  it("逃逸：倒计时结束带宝消失并广播", () => {
    const sim = readySim();
    const g = triggerGoblin(sim);
    g.goblinT = 0.05;
    for (let i = 0; i < 5; i++) sim.tick();
    expect(sim.enemies.some((e) => e.id === g.id)).toBe(false);
    const ev = sim.drainEvents().find((e) => e.event === "goblin_escape");
    expect(ev).toBeTruthy();
  });

  it("事件刷怪突破幕上限（怪海中照样现身）", () => {
    const sim = readySim();
    sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 30 } });
    const before = sim.enemies.length;
    sim.eventCount = 3;
    sim.eventTimer = 0.01;
    sim.tick();
    expect(sim.enemies.length).toBeGreaterThan(before);
    expect(sim.enemies.some((e) => e.goblin)).toBe(true);
  });
});
