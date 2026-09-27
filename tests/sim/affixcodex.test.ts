import { describe, expect, it } from "vitest";
import { AFFIXES, AFFIX_IDS } from "../../src/sim/affixes";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

describe("R56 词缀图鉴", () => {
  it("七词缀齐备，字段完整", () => {
    expect(AFFIX_IDS.length).toBe(7);
    for (const id of AFFIX_IDS) {
      const a = AFFIXES[id];
      expect(a.id).toBe(id);
      for (const f of [a.name, a.badge, a.effect, a.counter]) expect(f.length).toBeGreaterThan(0);
    }
  });

  it("名字与徽标互不重复（可读性前提）", () => {
    expect(new Set(AFFIX_IDS.map((i) => AFFIXES[i].name)).size).toBe(7);
    expect(new Set(AFFIX_IDS.map((i) => AFFIXES[i].badge)).size).toBe(7);
  });

  it("抽池与表同源：1000 次随机构建的精英词缀皆在表内且七种都出现", () => {
    const sim = new GameSim(561, memoryStorage());
    sim.exec({ action: "start" });
    const seen = new Set<string>();
    for (let i = 0; i < 1000; i++) {
      sim.exec({ action: "spawn", args: { type: 0, near: "player", count: 1, elite: true } });
      const e = sim.enemies[sim.enemies.length - 1];
      seen.add(e.affix);
      sim.enemies.pop();
    }
    expect([...seen].sort()).toEqual([...AFFIX_IDS].sort());
  });
});
