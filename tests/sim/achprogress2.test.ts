// R82 复合型成就进度：curator（藏珍）/fusion_master（祭炼）/mutator_all（历劫）。

import { describe, expect, it } from "vitest";
import { memoryStorage } from "../../src/sim/save";
import { GameSim } from "../../src/sim/game";
import { ACHIEVEMENTS, achievementProgress } from "../../src/sim/achievements";

const byId = (id: string) => ACHIEVEMENTS.find((x) => x.id === id)!;
const state = (over: Record<string, unknown>) =>
  ({ ...(new GameSim(1, memoryStorage()).achievementState()), ...over });

describe("R82 复合型成就进度", () => {
  it("三复合成就带 progress，通关直授类不带", () => {
    expect(byId("curator").progress).toBeTruthy();
    expect(byId("fusion_master").progress).toBeTruthy();
    expect(byId("mutator_all").progress).toBeTruthy();
    expect(byId("pacify").progress).toBeUndefined();
    expect(byId("oathbound").progress).toBeUndefined();
  });

  it("curator：cur=拥有数 target=总数（总数 0 时 target 0）", () => {
    const p = achievementProgress(byId("curator"), state({ armoryCount: 3, armoryTotal: 24 }));
    expect(p).toEqual({ cur: 3, target: 24 });
    const empty = achievementProgress(byId("curator"), state({ armoryCount: 0, armoryTotal: 0 }));
    expect(empty).toEqual({ cur: 0, target: 0 });
  });

  it("fusion_master：cur/target 随配方表", () => {
    const p = achievementProgress(byId("fusion_master"), state({ fusedCount: 2, fusedTotal: 6 }));
    expect(p).toEqual({ cur: 2, target: 6 });
  });

  it("mutator_all：键数/5", () => {
    const p = achievementProgress(byId("mutator_all"), state({ mutatorClears: { elite: 1, gold: 1 } }));
    expect(p).toEqual({ cur: 2, target: 5 });
  });

  it("进度与判定一致：满 progress 即 check 通过", () => {
    const s = state({ armoryCount: 24, armoryTotal: 24 });
    const a = byId("curator");
    const p = a.progress!(s);
    expect(p.cur >= p.target).toBe(a.check!(s));
  });
});