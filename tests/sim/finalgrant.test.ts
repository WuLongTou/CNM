import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

describe("R48 终局成就补授", () => {
  it("第 5 妖王与玩家死亡同 tick，妖王猎手仍授", () => {
    const sim = new GameSim(481, memoryStorage(), "sword", 0, [], "boss_rush");
    sim.exec({ action: "start" });
    sim.player.hp = 1; sim.player.invincible = 0;
    const inner = sim as unknown as {
      onEnemyKilled(e: { id: number; boss: boolean }): void;
      onPlayerDied(): void;
    };
    for (let i = 0; i < 4; i++) inner.onEnemyKilled({ id: 100 + i, boss: true });
    // 第 5 妖王击杀后立即死亡（同一结算周期，无后续 tick）——finalizeRun 补授
    inner.onEnemyKilled({ id: 200, boss: true });
    inner.onPlayerDied();
    expect(sim.achievements.unlocked.has("boss_hunter")).toBe(true);
  });
});
