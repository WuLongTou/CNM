import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

function rushSim(seed = 491) {
  const sim = new GameSim(seed, memoryStorage(), "sword", 0, [], "boss_rush");
  sim.exec({ action: "start" });
  return sim;
}

type Killer = { onEnemyKilled(e: { id: number; boss: boolean }): void };

describe("R49 连战阶梯", () => {
  it("乘数：0/3/6 妖王 → ×1 / ×1.15 / ×1.30", () => {
    const sim = rushSim();
    const inner = sim as unknown as Killer;
    expect(sim.rushEscalationMult()).toBe(1);
    for (let i = 0; i < 3; i++) inner.onEnemyKilled({ id: 100 + i, boss: true });
    expect(sim.rushEscalationMult()).toBeCloseTo(1.15, 6);
    for (let i = 0; i < 3; i++) inner.onEnemyKilled({ id: 200 + i, boss: true });
    expect(sim.rushEscalationMult()).toBeCloseTo(1.3, 6);
  });

  it("跨档播报：仅第 3/6 只触发 rush_escalate", () => {
    const sim = rushSim();
    const got: Array<{ tier: number; mult: number }> = [];
    sim.onEvent((e) => { if (e.event === "rush_escalate") got.push(e.data as { tier: number; mult: number }); });
    const inner = sim as unknown as Killer;
    for (let i = 0; i < 6; i++) inner.onEnemyKilled({ id: 300 + i, boss: true });
    expect(got.map((g) => g.tier)).toEqual([1, 2]);
    expect(got[0].mult).toBeCloseTo(1.15, 6);
    expect(got[1].mult).toBeCloseTo(1.3, 6);
  });

  it("狂化后的妖王血伤实增（×1.15 生效）", () => {
    const buffed = rushSim(492);
    const inner = buffed as unknown as Killer;
    for (let i = 0; i < 3; i++) inner.onEnemyKilled({ id: 400 + i, boss: true });
    buffed.enemies.length = 0;
    buffed.spawnEnemy({ type: 10, boss: true, near: "player" });
    const boss = buffed.enemies[buffed.enemies.length - 1];

    const plain = rushSim(492);
    plain.enemies.length = 0;
    plain.spawnEnemy({ type: 10, boss: true, near: "player" });
    const base = plain.enemies[plain.enemies.length - 1];

    expect(boss.maxHp).toBe(Math.ceil(base.maxHp * 1.15));
    expect(boss.damage).toBe(Math.ceil(base.damage * 1.15));
    expect(boss.speed).toBe(base.speed); // 速度不动，走位公平
  });

  it("普通模式妖王不享受阶梯", () => {
    const sim = new GameSim(493, memoryStorage());
    sim.exec({ action: "start" });
    const inner = sim as unknown as Killer;
    for (let i = 0; i < 6; i++) inner.onEnemyKilled({ id: 500 + i, boss: true });
    expect(sim.rushEscalationMult()).toBe(1); // 非连战恒 1
    sim.enemies.length = 0;
    sim.spawnEnemy({ type: 10, boss: true, near: "player" });
    const boss = sim.enemies[sim.enemies.length - 1];
    const ref = new GameSim(493, memoryStorage());
    ref.exec({ action: "start" });
    ref.enemies.length = 0;
    ref.spawnEnemy({ type: 10, boss: true, near: "player" });
    expect(boss.maxHp).toBe(ref.enemies[ref.enemies.length - 1].maxHp);
  });
});
