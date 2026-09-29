// R199 最爱武器：status.weapon_kills 排序取最大（数据面；渲染为 DOM e2e 范畴）。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

describe("R199 最爱武器", () => {
  it("finalize 后 weapon_kills 可查且多武器取最大", () => {
    const sim = new GameSim(1990, memoryStorage());
    sim.exec({ action: "start" });
    (sim as unknown as { runWeaponKills: Record<string, number> }).runWeaponKills = { tiejian: 30, fazhang: 90 };
    (sim as unknown as { finalizeRun(m: number, c: null): number }).finalizeRun(1, null);
    const st = sim.status() as unknown as { weapon_kills: Record<string, number> };
    const top = Object.entries(st.weapon_kills).sort((a, b) => b[1] - a[1])[0];
    expect(top[0]).toBe("fazhang");
    expect(top[1]).toBe(90);
  });
});