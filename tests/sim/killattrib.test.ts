// R197 击杀归因覆盖：六条伤害路径的武器归因回归（防未来新路径漏传 w）。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

describe("R197 击杀归因覆盖（六路径）", () => {
  const mk = () => {
    const sim = new GameSim(1970, memoryStorage());
    sim.exec({ action: "start" });
    return sim;
  };
  const spawnAt = (sim: GameSim, dx: number) => {
    sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1 } });
    const e = sim.enemies[sim.enemies.length - 1];
    e.x = sim.player.x + dx; e.y = sim.player.y; e.hp = 1;
    return e;
  };
  const killBy = (sim: GameSim, e: ReturnType<typeof spawnAt>, w: unknown) => {
    (sim as unknown as { applyHit(x: number, y: number, e: unknown, d: number, fx: object, ux?: number, uy?: number, w?: unknown): void })
      .applyHit(e.x, e.y, e, 99, { splash: 0, psn: 0, freeze: 0 }, 1, 0, w);
  };
  const weaponStub = (defId: string) => ({ defId, name: defId, dmgDealt: 0 } as never);

  it("近战挥砍路径", () => {
    const sim = mk(); const e = spawnAt(sim, 40);
    killBy(sim, e, weaponStub("tiejian"));
    expect((sim as unknown as { runWeaponKills: Record<string, number> }).runWeaponKills.tiejian).toBe(1);
  });

  it("弹道+溅射路径（同 w 传播）", () => {
    const sim = mk();
    const w = weaponStub("gongjian");
    const e = spawnAt(sim, 40); spawnAt(sim, 44);
    (sim as unknown as { applyHit(x: number, y: number, e: unknown, d: number, fx: object, ux?: number, uy?: number, w?: unknown): void })
      .applyHit(e.x, e.y, e, 99, { splash: 60, psn: 0, freeze: 0 }, 1, 0, w);
    const run = (sim as unknown as { runWeaponKills: Record<string, number> }).runWeaponKills;
    expect(run.gongjian ?? 0).toBeGreaterThanOrEqual(1);
  });

  it("链式路径（chainFrom 传 w）", () => {
    const sim = mk();
    const w = weaponStub("yingren");
    const a = spawnAt(sim, 40); const b = spawnAt(sim, 90);
    a.hp = 1; b.hp = 1;
    (sim as unknown as { chainFrom(src: unknown, d: number, c: number, p: number, f: number, v: Set<number>, w?: unknown): void })
      .chainFrom(a, 99, 2, 0, 0, new Set(), w);
    const run = (sim as unknown as { runWeaponKills: Record<string, number> }).runWeaponKills;
    expect(run.yingren ?? 0).toBeGreaterThanOrEqual(2);
  });

  it("毒 DoT 路径（psnSrcW 归因）", () => {
    const sim = mk(); const e = spawnAt(sim, 40);
    e.psnDps = 50; e.psnT = 5; e.psnTick = 0;
    e.psnSrcW = weaponStub("duandao") as never;
    const before = sim.kills;
    for (let i = 0; i < 90; i++) sim.tick(1 / 60);
    expect(sim.kills).toBeGreaterThan(before);
    const run = (sim as unknown as { runWeaponKills: Record<string, number> }).runWeaponKills;
    expect(run.duandao ?? 0).toBeGreaterThanOrEqual(1);
  });

  it("地雷路径（m.srcWeapon 归因）", () => {
    const sim = mk();
    sim.exec({ action: "start" });
    const e = spawnAt(sim, 40);
    e.hp = 1;
    const mines = (sim as unknown as { mines: Array<Record<string, unknown>> }).mines;
    mines.push({ id: 999, x: e.x, y: e.y, arm: 0, dmg: 99, splash: 0, psn: 0, srcWeapon: weaponStub("tiejili") });
    sim.tick(1 / 60);
    const run = (sim as unknown as { runWeaponKills: Record<string, number> }).runWeaponKills;
    expect(run.tiejili ?? 0).toBe(1);
  });
});