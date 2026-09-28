import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { CFG } from "../../src/sim/config";
import { DEFAULT_ROLE, ROLES } from "../../src/sim/roles";
import { WEAPON_DATA } from "../../src/sim/weapons";
import { defaultSave, memoryStorage } from "../../src/sim/save";

// R6 起非默认职业需先解锁：本文件 helper 预置两项解锁成就（锁语义由 role-unlock.test.ts 覆盖）
function newSim(seed = 7, roleId?: string) {
  const save = memoryStorage();
  save.save({ ...defaultSave(), achievements: ["slayer_50", "boss_slayer"] });
  return new GameSim(seed, save, roleId);
}

describe("R3 开局职业", () => {
  it("默认职业为剑修（起手铁剑），保持既有测试/玩法基线", () => {
    const sim = newSim();
    expect(DEFAULT_ROLE).toBe("sword");
    expect(sim.role.id).toBe("sword");
    expect(sim.player.weapons).toHaveLength(1);
    expect(sim.player.weapons[0].defId).toBe("tiejian");
  });

  it("三职业起手武器各就位", () => {
    expect(newSim(7, "archer").player.weapons[0].defId).toBe("gongjian");
    expect(newSim(7, "mage").player.weapons[0].defId).toBe("fazhang");
    // 表驱动：ROLES 声明的起手武器必须是已知武器 id
    for (const r of Object.values(ROLES)) expect(WEAPON_DATA[r.startWeapon]).toBeTruthy();
  });

  it("剑修被动：伤害面板 ×1.15，且真实出手伤害同步放大", () => {
    const base = newSim(11, "mage"); // mage dmgMult=1，作基线
    const sword = newSim(11);        // sword dmgMult=1.15
    for (const s of [base, sword]) {
      s.exec({ action: "start" });
      s.exec({ action: "give", args: { weapon: "tiejian" } }); // 重复获得 → 精炼+1，同武器可对比
    }
    expect(sword.stats.dmgMult).toBeCloseTo(base.stats.dmgMult * 1.15, 5);
    // 真实伤害：同位置放静置木桩（damage/speed 置 0），一次近战结算对比
    const place = (sim: GameSim) => {
      sim.player.x = 500; sim.player.y = 500;
      sim.exec({ action: "spawn", args: { type: "basic", near: { x: 540, y: 500 }, count: 1 } });
      const e = sim.enemies[0];
      e.damage = 0; e.speed = 0; // 木桩
      return e;
    };
    const eb = place(base);
    const es = place(sword);
    const hpBeforeB = eb.hp;
    const hpBeforeS = es.hp;
    base.input.attack = true;
    sword.input.attack = true;
    sword.tick();
    base.tick();
    const dmgB = hpBeforeB - eb.hp;
    const dmgS = hpBeforeS - es.hp;
    expect(dmgB).toBeGreaterThan(0);
    expect(dmgS / dmgB).toBeCloseTo(1.15, 1);
  });

  it("弓手被动：面板攻速 ×1.15 + 冲刺冷却 -20%", () => {
    const sim = newSim(11, "archer");
    sim.exec({ action: "start" });
    expect(sim.stats.aspdMult).toBeCloseTo(1.15, 5);
    sim.input.dash = true;
    sim.tick();
    // 触发帧内已扣一帧（dt=1/60），补回后应恰为 0.8×基础冷却
    expect(sim.player.dashCd + 1 / 60).toBeCloseTo(CFG.Player.DashCooldown * 0.8, 6);
  });

  it("术士被动：拾取半径 +50%（基线半径外仍能拾取符箓并弹出三选一）", () => {
    const make = (roleId: string) => {
      const sim = newSim(11, roleId);
      sim.exec({ action: "start" });
      sim.player.x = 500; sim.player.y = 500;
      // 距离 50px：基线半径 42 之外，术士 63 之内
      sim.pickups.push({ id: 9001, x: 550, y: 500 });
      sim.tick();
      return sim.upgradeOffers !== null;
    };
    expect(make("sword")).toBe(false);
    expect(make("mage")).toBe(true);
  });

  it("术士被动：面板范围 ×1.2（范围圈/索敌同步）", () => {
    const sim = newSim(11, "mage");
    sim.exec({ action: "start" });
    expect(sim.stats.rangeMult).toBeCloseTo(1.2, 5);
  });

  it("setRole 仅开局前可换：欢迎页换职业重置起手武器，开战后拒绝", () => {
    const sim = newSim(11);
    expect(sim.setRole("mage").applied).toBe(true);
    expect(sim.role.id).toBe("mage");
    expect(sim.player.weapons[0].defId).toBe("fazhang");
    sim.exec({ action: "start" });
    const r = sim.setRole("sword");
    expect(r.applied).toBe(false);
    expect(sim.role.id).toBe("mage");
    expect(sim.player.weapons[0].defId).toBe("fazhang");
  });

  it("未知职业：构造与 setRole 均抛错（协议层返回 ok:false）", () => {
    expect(() => newSim(7, "ninja")).toThrow();
    const sim = newSim(7);
    expect(() => sim.setRole("ninja")).toThrow();
    const res = sim.exec({ action: "set_role", args: { role: "ninja" } });
    expect(res.ok).toBe(false);
  });

  it("exec 协议：roles 列出三职业含被动文案；set_role 切换生效", () => {
    const sim = newSim(7);
    const roles = sim.exec({ action: "roles" }).data as {
      current: string;
      list: Array<{ id: string; name: string; passive: string; weapon: string }>;
    };
    expect(roles.current).toBe("sword");
    expect(roles.list).toHaveLength(4); // R123 +隐士
    expect(roles.list.map((r) => r.id)).toEqual(["sword", "archer", "mage", "hermit"]);
    for (const r of roles.list) {
      expect(r.passive.length).toBeGreaterThan(0);
      expect(r.weapon.length).toBeGreaterThan(0);
    }
    const set = sim.exec({ action: "set_role", args: { role: "archer" } });
    expect(set.ok).toBe(true);
    expect(sim.role.id).toBe("archer");
    expect(sim.status().role).toBe("archer");
    expect(sim.status().role_name).toBe("弓手");
  });
});
