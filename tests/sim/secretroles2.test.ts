// R124 秘密职业扩展：隐士起手形态特权 + 武圣（九界平定解锁全维 +10%）。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage, defaultSave } from "../../src/sim/save";
import { aspectDef } from "../../src/sim/weapons";
import { ROLES } from "../../src/sim/roles";

const withSave = (achievements: string[]) => {
  const save = memoryStorage();
  save.save({ ...defaultSave(), achievements });
  return save;
};

describe("R124 秘密职业扩展", () => {
  it("武圣：数据定义（九界平定解锁/全维 1.1/起手镰刀）", () => {
    expect(ROLES.wusheng.unlockAchievement).toBe("nine_realms");
    expect(ROLES.wusheng.dmgMult).toBe(1.1);
    expect(ROLES.wusheng.startWeapon).toBe("sishenliandao");
  });

  it("隐士特权：开局雷杖 aspect=1（引雷形态，无需进阶）", () => {
    const sim = new GameSim(1240, withSave(["flawless"]), "hermit");
    sim.exec({ action: "start" });
    const sw = sim.player.weapons[0]!;
    expect(sw.defId).toBe("leidianfazhang");
    expect(sw.aspect).toBe(1);
    expect(aspectDef(sw).chain).toBe(1); // 引雷链 +1 生效
  });

  it("非隐士同武器不受特权影响（术士起手法杖无形态）", () => {
    const sim = new GameSim(1241, withSave(["flawless", "boss_slayer"]), "mage");
    sim.exec({ action: "start" });
    expect(sim.player.weapons[0]!.aspect).toBe(0);
  });

  it("门控：武圣未解锁不可构造；预置九界平定后可", () => {
    expect(() => new GameSim(1242, memoryStorage(), "wusheng")).toThrow("职业未解锁");
    const sim = new GameSim(1242, withSave(["nine_realms"]), "wusheng");
    sim.exec({ action: "start" });
    expect(sim.role.id).toBe("wusheng");
  });
});