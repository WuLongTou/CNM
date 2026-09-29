// R185 妖王凝固：rush 开局刷妖王即触发 BossSpawnMs，随 scale 消退不卡死。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { HITSTOP } from "../../src/sim/hitstop";

describe("R185 妖王凝固层次", () => {
  it("rush 开局刷妖王：hitStop 立即触发 BossSpawnMs", () => {
    const sim = new GameSim(1850, memoryStorage(), "sword", 0, {}, "boss_rush");
    sim.exec({ action: "start" });
    expect(sim.hitStop.remainingMs).toBe(HITSTOP.BossSpawnMs); // 180ms
  });

  it("凝固随 scale 消退（不卡死）且后续阶再触发", () => {
    const sim = new GameSim(1851, memoryStorage(), "sword", 0, {}, "boss_rush");
    sim.exec({ action: "start" });
    for (let i = 0; i < 400 && sim.hitStop.remainingMs > 0; i++) sim.hitStop.scale(1000 / 60);
    expect(sim.hitStop.remainingMs).toBe(0); // 首次凝固消退
    sim.exec({ action: "goto_round", args: { round: 2 } });
    for (let i = 0; i < 200; i++) sim.tick(1 / 60);
    expect(sim.hitStop.remainingMs).toBe(HITSTOP.BossSpawnMs); // 2 阶妖王再凝固
  });
});