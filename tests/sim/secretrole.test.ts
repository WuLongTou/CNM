// R123 秘密职业「隐士」：铁壁解锁的玻璃大炮（无伤大师的孤注一掷）。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage, defaultSave } from "../../src/sim/save";
import { ROLES } from "../../src/sim/roles";

describe("R123 秘密职业 隐士", () => {
  it("数据：hermit 定义齐备，解锁条件=铁壁", () => {
    expect(ROLES.hermit).toBeTruthy();
    expect(ROLES.hermit.unlockAchievement).toBe("flawless");
    expect(ROLES.hermit.startWeapon).toBe("leidianfazhang");
    expect(ROLES.hermit.dmgMult).toBe(1.3);
  });

  it("未解锁：isRoleUnlocked=false；存档含铁壁后解锁并可直接构造", () => {
    const sim = new GameSim(1230, memoryStorage());
    sim.exec({ action: "start" });
    expect(sim.isRoleUnlocked("hermit")).toBe(false);
    // 存档预置铁壁 → 新实例（选人阶段）解锁并可直接以 hermit 构造
    const save = memoryStorage();
    save.save({ ...defaultSave(), achievements: ["flawless"] });
    const sim2 = new GameSim(1230, save);
    expect(sim2.isRoleUnlocked("hermit")).toBe(true);
    sim2.setRole("hermit");
    expect(sim2.role.id).toBe("hermit");
    expect(sim2.role.startWeapon).toBe("leidianfazhang");
  });

  it("被动生效：隐士局 dmgMult 1.3（预置铁壁存档直构）", () => {
    const save = memoryStorage();
    save.save({ ...defaultSave(), achievements: ["flawless"] });
    const sim = new GameSim(1231, save, "hermit");
    sim.exec({ action: "start" });
    expect(sim.role.dmgMult).toBe(1.3);
    expect(sim.role.startWeapon).toBe("leidianfazhang");
  });
});