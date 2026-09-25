import { describe, expect, it } from "vitest";
import { GameSim, type Enemy } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

function readySim(seed = 161) {
  const sim = new GameSim(seed, memoryStorage());
  sim.exec({ action: "start" });
  return sim;
}

function spawnElite(sim: GameSim, type: number): Enemy {
  sim.exec({ action: "spawn", args: { type, near: "player", count: 1, elite: true } });
  return sim.enemies[sim.enemies.length - 1];
}

describe("R17 精英词缀扩展", () => {
  it("词缀池 7 种全覆盖", () => {
    const sim = readySim();
    const inner = sim as unknown as { pickAffix(): string };
    const seen = new Set<string>();
    for (let i = 0; i < 500; i++) seen.add(inner.pickAffix());
    expect([...seen].sort()).toEqual(["blink", "explosive", "frenzy", "regen", "shield", "split", "swift"]);
  });

  it("分裂：死亡爆 2 只满血蚀群（普通怪，不连锁）", () => {
    const sim = readySim();
    const e = spawnElite(sim, 2);
    e.affix = "split";
    e.x = 600; e.y = 600;
    const before = sim.enemies.length;
    (sim as unknown as { onEnemyKilled(e: Enemy): void }).onEnemyKilled(e);
    const swarm = sim.enemies.filter((o) => o.type === 13);
    expect(sim.enemies.length - before).toBe(1); // -1 击杀 + 2 分裂
    expect(swarm.length).toBe(2);
    for (const s of swarm) {
      expect(s.elite).toBe(false);
      expect(s.affix).toBe("none");
      expect(s.hp).toBe(s.maxHp);
    }
  });

  it("分裂不作用于妖王", () => {
    const sim = readySim();
    sim.exec({ action: "spawn", args: { type: 10, near: "player", count: 1, boss: true, elite: true } });
    const b = sim.enemies[sim.enemies.length - 1];
    b.affix = "split";
    const before = sim.enemies.length;
    (sim as unknown as { onEnemyKilled(e: Enemy): void }).onEnemyKilled(b);
    expect(sim.enemies.length).toBe(before - 1);
  });

  it("疾影：冷却结束闪现至玩家 150px 并续杯", () => {
    const sim = readySim();
    sim.player.x = 500; sim.player.y = 500;
    const e = spawnElite(sim, 1);
    e.affix = "blink";
    e.x = 1200; e.y = 500;
    e.speed = 0; // 排除走位干扰，只看闪现位移
    e.blinkCd = 0.05;
    sim.tick(); sim.tick(); sim.tick(); sim.tick();
    const d = Math.hypot(e.x - 500, e.y - 500);
    expect(d).toBeGreaterThan(140);
    expect(d).toBeLessThan(165);
    expect(e.blinkCd).toBeGreaterThan(3.5);
    const ev = sim.drainEvents().find((e2) => e2.event === "enemy_blink");
    expect(ev).toBeTruthy();
  });

  it("快照敌表携带 affix", () => {
    const sim = readySim();
    spawnElite(sim, 0);
    const snap = sim.snapshot() as { enemies: { list: Array<{ affix: string }> } };
    expect(snap.enemies.list.length).toBeGreaterThan(0);
    for (const o of snap.enemies.list) expect(typeof o.affix).toBe("string");
  });
});
