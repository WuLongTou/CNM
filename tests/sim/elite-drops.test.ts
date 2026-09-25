import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { WEAPON_DATA } from "../../src/sim/weapons";
import { memoryStorage } from "../../src/sim/save";
import type { Enemy } from "../../src/sim/game";

function killSim(seed = 61) {
  const sim = new GameSim(seed, memoryStorage());
  sim.exec({ action: "start" });
  return sim;
}

/** 定点击杀（私有 onEnemyKilled 直调；progression.test.ts 既有先例）。 */
function slay(sim: GameSim, opts: { elite?: boolean; boss?: boolean }): Enemy {
  sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1, elite: !!opts.elite, boss: !!opts.boss } });
  const e = sim.enemies[sim.enemies.length - 1];
  e.x = 600; e.y = 600;
  (sim as unknown as { onEnemyKilled(e: Enemy): void }).onEnemyKilled(e);
  return e;
}

describe("R8 精英掉宝", () => {
  it("精英击杀必掉精英宝箱（落点≈击杀点）", () => {
    const sim = killSim();
    const before = sim.chests.length;
    slay(sim, { elite: true });
    expect(sim.chests.length).toBe(before + 1);
    const c = sim.chests[sim.chests.length - 1];
    expect(c.elite).toBe(true);
    expect(c.opened).toBe(false);
    expect(Math.hypot(c.x - 600, c.y - 600)).toBeLessThan(30);
  });

  it("普通击杀不掉箱，妖王击杀不掉箱（保持符箓×3）", () => {
    const sim = killSim();
    const before = sim.chests.length;
    slay(sim, {});
    expect(sim.chests.length).toBe(before);
    const picksBefore = sim.pickups.length;
    slay(sim, { boss: true });
    expect(sim.chests.length).toBe(before);
    expect(sim.pickups.length).toBe(picksBefore + 3);
  });

  it("开精英箱：稀有度≥2 武器 + 奖励符箓（就地吸取弹报价） + 精英事件", () => {
    const sim = killSim();
    slay(sim, { elite: true });
    const c = sim.chests[sim.chests.length - 1];
    sim.pickups.length = 0; // 清掉击杀掷骰的符箓，只看开箱奖励
    const ownedBefore = sim.player.weapons.length;
    sim.exec({ action: "teleport", args: { x: c.x, y: c.y } });
    sim.tick();
    expect(c.opened).toBe(true);
    // 奖励符箓落在箱旁 24px，玩家贴箱必在拾取半径内 → 同 tick 吸取并弹出三选一
    expect(sim.upgradeOffers).toHaveLength(3);
    const gained = sim.player.weapons.filter((w) => w.defId !== "tiejian");
    expect(sim.player.weapons.length).toBe(ownedBefore + 1);
    expect(WEAPON_DATA[gained[0].defId].rarity).toBeGreaterThanOrEqual(2);
    const ev = sim.drainEvents().find((e) => e.event === "chest_opened");
    expect(ev?.data).toMatchObject({ elite: true });
    expect(typeof (ev?.data as { weapon?: unknown }).weapon).toBe("string");
  });

  it("远距开箱：奖励符箓留在箱旁待拾取（不自动弹报价）", () => {
    const sim = killSim();
    slay(sim, { elite: true });
    const c = sim.chests[sim.chests.length - 1];
    sim.pickups.length = 0;
    // 站箱体 49px（开箱半径 50 内、符箓拾取半径 42 外）
    sim.exec({ action: "teleport", args: { x: c.x - 49, y: c.y } });
    sim.tick();
    expect(c.opened).toBe(true);
    expect(sim.pickups.length).toBe(1);
    expect(sim.upgradeOffers).toBeNull();
    const u = sim.pickups[0];
    expect(Math.hypot(u.x - c.x, u.y - c.y)).toBeLessThanOrEqual(30);
  });

  it("普通箱保持 elite:false", () => {
    const sim = killSim();
    expect(sim.chests.length).toBeGreaterThan(0);
    for (const c of sim.chests) expect(c.elite).toBe(false);
  });

  it("掉箱先清已开箱，软上限 12", () => {
    const sim = killSim();
    const inner = sim as unknown as { dropChest(x: number, y: number, elite: boolean): void };
    for (let i = 0; i < 15; i++) inner.dropChest(100 + i, 100, i % 2 === 0);
    expect(sim.chests.length).toBeLessThanOrEqual(12);
    expect(sim.chests.every((c) => !c.opened)).toBe(true);
    // 已开箱被清理：开一只再掉，保持规模不膨胀
    sim.chests[0].opened = true;
    inner.dropChest(200, 200, true);
    expect(sim.chests.length).toBeLessThanOrEqual(12);
    expect(sim.chests.some((c) => c.opened)).toBe(false);
  });

  it("快照 chests 携带 elite 标记", () => {
    const sim = killSim();
    slay(sim, { elite: true });
    const snap = sim.snapshot() as { chests: Array<{ x: number; y: number; elite: boolean }> };
    expect(snap.chests.length).toBeGreaterThan(0);
    expect(snap.chests.some((c) => c.elite)).toBe(true);
    expect(snap.chests.some((c) => !c.elite)).toBe(true);
  });
});
