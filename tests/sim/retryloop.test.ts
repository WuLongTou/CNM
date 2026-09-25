import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { CFG } from "../../src/sim/config";
import { memoryStorage } from "../../src/sim/save";

describe("R18 重开循环（可测口径）", () => {
  it("幕次：随存活推进，封顶第 4 幕，快照同步", () => {
    const sim = new GameSim(181, memoryStorage());
    sim.exec({ action: "start" });
    expect(sim.act).toBe(0);
    expect(sim.status()).toMatchObject({ act: 0 });
    sim.survivalTime = CFG.Acts.ActTime + 1;
    expect(sim.act).toBe(1);
    sim.survivalTime = CFG.Acts.ActTime * 3 - 1;
    expect(sim.act).toBe(2);
    sim.survivalTime = CFG.Acts.ActTime * 10;
    expect(sim.act).toBe(3);
    expect(sim.status()).toMatchObject({ act: 3 });
  });
});
