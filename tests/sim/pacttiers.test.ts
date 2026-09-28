// R70 血誓分级（Hades 热量式）：档位设置/钳制 / 分数与生命乘数 / 兼容路径 /
// oathbound 口径 / 文案标签。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { CFG } from "../../src/sim/config";
import { PACTS, PACT_MAX_TIER, pactLabel } from "../../src/sim/pacts";

function readySim(seed = 700) {
  const sim = new GameSim(seed, memoryStorage());
  return sim; // 血誓须在 start 前立（WeaponSelection 守卫），按需 setPactTiers 后再 start
}

describe("R70 血誓分级", () => {
  it("数据表：三誓各三档，一档与 R25 旧值一致", () => {
    for (const id of ["blood", "horde", "frail"] as const) {
      expect(PACTS[id].tiers).toHaveLength(PACT_MAX_TIER);
    }
    expect(PACTS.blood.tiers[0]).toMatchObject({ scoreMult: 1.25, hpMult: 1.25 });
    expect(PACTS.horde.tiers[0]).toMatchObject({ scoreMult: 1.3, spawnBonus: 0.3 });
    expect(PACTS.frail.tiers[0]).toMatchObject({ scoreMult: 1.5, frailMult: 0.75 });
  });

  it("set_pacts tiers 路径：档位生效、越档钳制、0 撤誓（开局前）", () => {
    const sim = readySim();
    expect((sim.exec({ action: "set_pacts", args: { tiers: { frail: 3 } } }).data as { applied: boolean }).applied).toBe(true);
    expect(sim.pactTier("frail")).toBe(3);
    expect((sim.exec({ action: "set_pacts", args: { tiers: { frail: 9 } } }).data as { applied: boolean }).applied).toBe(true);
    expect(sim.pactTier("frail")).toBe(PACT_MAX_TIER); // 越档钳到顶
    sim.exec({ action: "set_pacts", args: { tiers: { frail: 0 } } });
    expect(sim.pactTier("frail")).toBe(0); // 撤誓
  });

  it("结算乘数按档位连乘（脆体三档 2.2）", () => {
    const sim = readySim();
    sim.setPactTiers({ frail: 3 });
    expect(sim.pactScoreMult()).toBeCloseTo(2.2, 10);
  });

  it("脆体三档：生命上限 -45%", () => {
    const sim = new GameSim(701, memoryStorage(), "sword", 0, { frail: 3 });
    sim.exec({ action: "start" });
    expect(sim.player.maxHp).toBe(Math.floor(CFG.Player.MaxHealth * 0.55));
  });

  it("血战三档：妖物生命 ceil(基线 ×1.7)", () => {
    const base = new GameSim(702, memoryStorage());
    base.exec({ action: "start" });
    base.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1 } });
    const hpBase = base.enemies[base.enemies.length - 1]!.hp; // 28（25×1.12 上取整）

    const sim = new GameSim(702, memoryStorage(), "sword", 0, { blood: 3 });
    sim.exec({ action: "start" });
    sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1 } });
    const hpB = sim.enemies[sim.enemies.length - 1]!.hp;
    // 合成公式：ceil(stats.hp × foeMult × blood) = ceil(25 × 1.096 × 1.7)
    expect(hpB).toBe(47);
    expect(hpBase).toBe(28); // 无誓基线（25×1.096 上取整）
  });

  it("人海档位：spawnBonus 随档上升（数据口径）", () => {
    expect(PACTS.horde.tiers.map((t) => t.spawnBonus)).toEqual([0.3, 0.5, 0.75]);
  });

  it("setPacts ids 兼容路径：立誓即一级（R25 语义）", () => {
    const sim = readySim();
    expect(sim.setPacts(["blood", "frail"]).applied).toBe(true);
    expect(sim.pactTier("blood")).toBe(1);
    expect(sim.pactTier("frail")).toBe(1);
    expect(sim.pactScoreMult()).toBeCloseTo(1.25 * 1.5, 10);
  });

  it("开局后拒绝改誓（WeaponSelection 守卫既有口径）", () => {
    const sim = readySim();
    sim.setPactTiers({ blood: 2 });
    sim.exec({ action: "start" });
    expect(sim.setPactTiers({ horde: 3 }).applied).toBe(false);
    expect(sim.pactTier("horde")).toBe(0);
  });

  it("快照携带 pact_tiers；pacts 仍为已立誓 id 数组（兼容）", () => {
    const sim = readySim();
    sim.setPactTiers({ blood: 2 });
    const snap = sim.snapshot();
    expect(snap.pact_tiers).toEqual({ blood: 2 });
    expect(snap.pacts).toEqual(["blood"]);
  });

  it("文案标签：一档纯名，二/三档罗马数字", () => {
    expect(pactLabel("血战", 1)).toBe("血战");
    expect(pactLabel("血战", 2)).toBe("血战II");
    expect(pactLabel("脆体", 3)).toBe("脆体III");
  });
});