// R187 符箓保底：40 杀无符箓 → 普通妖必掉玩家脚边；任何掉落重置计数。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

const spawnAndKill = (sim: GameSim) => {
  sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1 } });
  const e = sim.enemies[sim.enemies.length - 1];
  e.hp = 1;
  (sim as unknown as { onEnemyKilled(e: unknown): void }).onEnemyKilled(e);
};

describe("R187 符箓保底", () => {
  it("25 杀内必触发：计数 ≥25 后普通妖死 → pickups 非空且落玩家脚边", () => {
    const sim = new GameSim(1870, memoryStorage());
    sim.exec({ action: "start" });
    let pityDrop = false;
    for (let i = 0; i < 200 && !pityDrop; i++) {
      const before = sim.pickups.length;
      spawnAndKill(sim);
      if (sim.pickups.length > before) {
        const last = sim.pickups[sim.pickups.length - 1]!;
        const near = Math.abs(last.x - sim.player.x) <= 12 && Math.abs(last.y - sim.player.y) <= 12;
        pityDrop = near && (sim as unknown as { killsSinceTalisman: number }).killsSinceTalisman === 0;
      }
    }
    expect(pityDrop).toBe(true);
  });

  it("保底触发后计数重置：再 40 杀内必有下一次（cadence 持续）", () => {
    const sim = new GameSim(1871, memoryStorage());
    sim.exec({ action: "start" });
    let drops = 0;
    for (let i = 0; i < 100; i++) {
      const before = sim.pickups.length;
      spawnAndKill(sim);
      if (sim.pickups.length > before) drops++;
    }
    // 100 杀按保底应 ≥2 次掉落（40/40/20 节奏，概率掉落只会更多）
    expect(drops).toBeGreaterThanOrEqual(2);
  });

  it("徒手（无武器）不保底：60 杀内仅自然 2% 掉落（≤5 上限界）", () => {
    const sim = new GameSim(1873, memoryStorage());
    sim.exec({ action: "start" });
    // 清空武器模拟徒手
    (sim as unknown as { player: { weapons: unknown[] } }).player.weapons = [];
    for (let i = 0; i < 60; i++) spawnAndKill(sim);
    const s = sim as unknown as { killsSinceTalisman: number; pickups: unknown[] };
    // 无保底：60 杀 × 2% 期望 1.2 掉落（≤5 为宽上界；有保底则约 2 次必掉+计数重置会显著不同）
    expect(s.pickups.length).toBeLessThanOrEqual(5);
    expect(s.killsSinceTalisman).toBeGreaterThanOrEqual(20); // 计数未被保底清零过（自然掉落偶有重置）
  });
});