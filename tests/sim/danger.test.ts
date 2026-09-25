import { describe, expect, it } from "vitest";
import { GameSim, type Enemy } from "../../src/sim/game";
import { dangerOpacity } from "../../src/ui/hud";
import { memoryStorage } from "../../src/sim/save";

function readySim(seed = 151) {
  const sim = new GameSim(seed, memoryStorage());
  sim.exec({ action: "start" });
  return sim;
}

/** 贴脸放一只高伤妖，等它出手致死。 */
function faceKiller(sim: GameSim, type: number, elite = false, boss = false) {
  sim.exec({ action: "spawn", args: { type, near: { x: sim.player.x + 30, y: sim.player.y }, count: 1, elite, boss } });
  const e = sim.enemies[sim.enemies.length - 1];
  e.damage = 9999;
  e.speed = 0;
  return e;
}

function dieTo(sim: GameSim, type: number, elite = false, boss = false) {
  sim.player.hp = 120;
  sim.player.invincible = 0;
  faceKiller(sim, type, elite, boss);
  for (let i = 0; i < 300 && sim.state !== "GameOver"; i++) sim.tick();
  expect(sim.state).toBe("GameOver");
}

describe("R16 生存反馈", () => {
  it("dangerOpacity：满血无显示，越低越红", () => {
    expect(dangerOpacity(1)).toBe(0);
    expect(dangerOpacity(0.5)).toBe(0);
    expect(dangerOpacity(0.35)).toBe(0);
    expect(dangerOpacity(0.2)).toBeCloseTo(0.15 / 0.35, 6);
    expect(dangerOpacity(0)).toBe(1);
    expect(dangerOpacity(-5)).toBe(1);
  });

  it("近战死因直称妖名", () => {
    const sim = readySim();
    dieTo(sim, 11);
    expect(sim.deathCause).toBe("甲妖");
    expect(sim.status()).toMatchObject({ death_cause: "甲妖" });
  });

  it("阶位修饰：精英前缀 / 妖王后缀", () => {
    const sim = readySim();
    dieTo(sim, 0, true);
    expect(sim.deathCause).toBe("精英游魂");
    const sim2 = readySim(152);
    dieTo(sim2, 0, false, true);
    expect(sim2.deathCause).toBe("游魂王");
  });

  it("爆魈自爆死因", () => {
    const sim = readySim();
    sim.player.hp = 5;
    sim.player.invincible = 0;
    sim.exec({ action: "spawn", args: { type: 3, near: { x: sim.player.x + 30, y: sim.player.y }, count: 1 } });
    const e = sim.enemies[sim.enemies.length - 1];
    e.damage = 9999;
    e.speed = 0;
    for (let i = 0; i < 300 && sim.state !== "GameOver"; i++) sim.tick();
    expect(sim.state).toBe("GameOver");
    expect(sim.deathCause).toBe("爆魈自爆");
  });

  it("弹道死因：箭矢 / 毒弹", () => {
    const mk = (type: number, psn: boolean, want: string) => {
      const sim = readySim(type * 100 + 7);
      sim.player.hp = 3;
      sim.player.invincible = 0;
      sim.exec({ action: "spawn", args: { type, near: { x: sim.player.x + 100, y: sim.player.y }, count: 1 } });
      const e = sim.enemies[sim.enemies.length - 1];
      e.speed = 0;
      const ang = Math.atan2(sim.player.y - e.y, sim.player.x - e.x);
      (sim as unknown as { fireEnemyShot(e: Enemy, m: number, p: boolean, a: number, s: number): void })
        .fireEnemyShot(e, 99, psn, ang, 340);
      for (let i = 0; i < 60 && sim.state !== "GameOver"; i++) sim.tick();
      expect(sim.state).toBe("GameOver");
      expect(sim.deathCause).toBe(want);
    };
    mk(8, false, "弩妖箭矢");
    mk(5, true, "瘴妖毒弹");
  });

  it("蛟冲撞死因", () => {
    const sim = readySim();
    sim.player.hp = 5;
    sim.player.invincible = 0;
    sim.exec({ action: "spawn", args: { type: 12, near: { x: sim.player.x + 20, y: sim.player.y }, count: 1 } });
    const e = sim.enemies[sim.enemies.length - 1];
    e.damage = 999;
    e.speed = 0;
    e.chargeDash = 0.45;
    e.chargeVx = 0; e.chargeVy = 0;
    for (let i = 0; i < 120 && sim.state !== "GameOver"; i++) sim.tick();
    expect(sim.state).toBe("GameOver");
    expect(sim.deathCause).toBe("蛟冲撞");
  });

  it("非致死伤害不记死因", () => {
    const sim = readySim();
    sim.player.hp = 100;
    sim.player.invincible = 0;
    faceKiller(sim, 0);
    sim.enemies[sim.enemies.length - 1].damage = 5;
    for (let i = 0; i < 120 && sim.state !== "GameOver"; i++) sim.tick();
    expect(sim.state).toBe("Combat");
    expect(sim.deathCause).toBeNull();
  });
});
