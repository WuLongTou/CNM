// R168 放弃本局：abandon 回欢迎态、不入战绩、暂停态复位。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

describe("R168 放弃本局", () => {
  it("Combat 中 abandon：回 WeaponSelection、不入战绩、局数不涨", () => {
    const sim = new GameSim(1680, memoryStorage());
    sim.exec({ action: "start" });
    (sim as unknown as { score: number }).score = 42;
    const r = sim.exec({ action: "abandon" });
    expect(r.data).toMatchObject({ abandoned: true });
    expect(sim.state).toBe("WeaponSelection");
    expect(sim.paused).toBe(false);
    const st = sim.exec({ action: "status" }).data as { run_history: unknown[] };
    expect(st.run_history).toHaveLength(0); // 放弃局不入战绩
  });

  it("非 Combat 态 abandon：拒绝（abandoned:false）", () => {
    const sim = new GameSim(1681, memoryStorage());
    const r = sim.exec({ action: "abandon" });
    expect(r.data).toMatchObject({ abandoned: false });
  });
});