// R106 形态第二批：8 把武器（旋风斧/血怒/突贯/破城/床弩/龙息/双影/蚀骨）。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { makeWeapon, aspectDef, ASPECTS } from "../../src/sim/weapons";

const IDS = ["zhanfu", "shuangdao", "changqiang", "jinfu", "zhongnu", "sandanqiang", "yingren", "duandao"];

describe("R106 形态第二批", () => {
  it("形态表全铺完（R107 起 26 条）；每条 desc 非空", () => {
    expect(Object.keys(ASPECTS)).toHaveLength(26);
    for (const id of IDS) expect(ASPECTS[id].desc.length).toBeGreaterThan(4);
  });

  it("覆盖语义抽查：旋风斧 splash=100（终值非叠加）、蚀骨 psn=20（翻倍）", () => {
    const zf = makeWeapon("zhanfu"); zf.aspect = 1;
    expect(aspectDef(zf).splash).toBe(100);
    const dd = makeWeapon("duandao"); dd.aspect = 1;
    expect(aspectDef(dd).psn).toBe(20);
  });

  it("龙息 spread 终值 5（基 3 + 2，覆盖语义给终值）", () => {
    const sd = makeWeapon("sandanqiang"); sd.aspect = 1;
    expect(aspectDef(sd).spread).toBe(5);
  });

  it("全部新形态可切：门控通过后 aspect 生效", () => {
    for (const id of IDS) {
      const sim = new GameSim(1060, memoryStorage());
      sim.exec({ action: "start" });
      sim.player.weapons = [makeWeapon(id)];
      sim.player.weapons[0]!.ups = 3;
      expect(sim.toggleAspect(0)).toBe(true);
      expect(sim.player.weapons[0]!.aspect).toBe(1);
    }
  });
});