// R196 武器图鉴生涯击杀显示：数据面（渲染为纯 DOM 断言在 e2e 覆盖）。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

describe("R196 武器图鉴生涯击杀", () => {
  it("status.weapon_kills 生涯表存在且 finalize 后可查", () => {
    const sim = new GameSim(1960, memoryStorage());
    sim.exec({ action: "start" });
    (sim as unknown as { runWeaponKills: Record<string, number> }).runWeaponKills = { tiejian: 42 };
    (sim as unknown as { finalizeRun(m: number, c: null): number }).finalizeRun(1, null);
    const st = sim.status() as unknown as { weapon_kills: Record<string, number> };
    expect(st.weapon_kills.tiejian).toBe(42);
  });

  it("重开一局：新实例 runWeaponKills 从零开始（生涯已在档）", () => {
    const fresh = new GameSim(1962, memoryStorage());
    fresh.exec({ action: "start" });
    expect(Object.keys((fresh as unknown as { runWeaponKills: Record<string, number> }).runWeaponKills)).toHaveLength(0);
  });
});