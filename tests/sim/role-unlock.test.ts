import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { defaultSave, memoryStorage } from "../../src/sim/save";

function freshSave() {
  const save = memoryStorage();
  return save;
}

describe("R6 职业解锁", () => {
  it("新鲜存档：仅剑修开放，弓手/术士锁定", () => {
    const sim = new GameSim(41, freshSave());
    expect(sim.isRoleUnlocked("sword")).toBe(true);
    expect(sim.isRoleUnlocked("archer")).toBe(false);
    expect(sim.isRoleUnlocked("mage")).toBe(false);
    expect(sim.roleUnlockHint("sword")).toBeNull();
    expect(sim.roleUnlockHint("archer")).toMatchObject({ name: "除妖行者" });
    expect(sim.roleUnlockHint("mage")).toMatchObject({ name: "斩妖大将" });
  });

  it("锁定职业：构造抛错，setRole 拒绝（未知 id 仍抛错）", () => {
    expect(() => new GameSim(41, freshSave(), "archer")).toThrow("职业未解锁");
    expect(() => new GameSim(41, freshSave(), "mage")).toThrow("职业未解锁");
    const sim = new GameSim(41, freshSave());
    const r = sim.setRole("archer") as { applied: boolean; note: string };
    expect(r.applied).toBe(false);
    expect(r.note).toContain("职业未解锁");
    expect(sim.role.id).toBe("sword");
    expect(() => sim.setRole("ninja")).toThrow();
    // 协议层：locked → ok:true + applied:false（拒绝非异常）；未知 → ok:false
    expect(sim.exec({ action: "set_role", args: { role: "mage" } }).data)
      .toMatchObject({ applied: false });
    expect(sim.exec({ action: "set_role", args: { role: "ninja" } }).ok).toBe(false);
  });

  it("成就解锁职业：slayer_50 开弓手，boss_slayer 开术士", () => {
    const save = memoryStorage();
    save.save({ ...defaultSave(), achievements: ["slayer_50"] });
    const sim = new GameSim(41, save);
    expect(sim.isRoleUnlocked("archer")).toBe(true);
    expect(sim.isRoleUnlocked("mage")).toBe(false);
    expect(sim.setRole("archer")).toMatchObject({ applied: true });
    expect(sim.player.weapons[0].defId).toBe("gongjian");
    save.save({ ...save.data, achievements: ["slayer_50", "boss_slayer"] });
    const sim2 = new GameSim(41, save);
    expect(sim2.isRoleUnlocked("mage")).toBe(true);
  });

  it("老存档回填：累计击杀达标首 tick 即解锁并广播 role_unlocked", () => {
    const save = memoryStorage();
    save.save({ ...defaultSave(), totalKills: 60 });
    const sim = new GameSim(41, save);
    expect(sim.isRoleUnlocked("archer")).toBe(false);
    const events: string[] = [];
    sim.onEvent((e) => events.push(e.event));
    sim.exec({ action: "start" });
    sim.tick();
    expect(sim.achievements.unlocked.has("slayer_50")).toBe(true);
    expect(sim.isRoleUnlocked("archer")).toBe(true);
    expect(events).toContain("role_unlocked");
    const unlock = sim.drainEvents().find((e) => e.event === "role_unlocked");
    expect(unlock?.data).toMatchObject({ id: "archer", name: "弓手" });
  });

  it("讨伐妖王解锁术士（bossKills 达成即广播，持久化后下局可选）", () => {
    const save = freshSave();
    const sim = new GameSim(41, save);
    sim.exec({ action: "start" });
    sim.bossKills = 1;
    const events: string[] = [];
    sim.onEvent((e) => events.push(e.event));
    sim.tick();
    expect(sim.achievements.unlocked.has("boss_slayer")).toBe(true);
    expect(sim.isRoleUnlocked("mage")).toBe(true);
    expect(events).toContain("role_unlocked");
    const sim2 = new GameSim(42, save);
    expect(sim2.setRole("mage")).toMatchObject({ applied: true, role: "mage" });
    expect(sim2.player.weapons[0].defId).toBe("fazhang");
  });

  it("roles 协议：列表携带 unlocked 与解锁条件", () => {
    const sim = new GameSim(41, freshSave());
    const roles = sim.exec({ action: "roles" }).data as {
      current: string;
      list: Array<{
        id: string; unlocked: boolean;
        unlock: { name: string; desc: string } | null;
      }>;
    };
    expect(roles.list).toHaveLength(4); // R123 +隐士
    const byId = Object.fromEntries(roles.list.map((r) => [r.id, r]));
    expect(byId.sword).toMatchObject({ unlocked: true, unlock: null });
    expect(byId.archer.unlocked).toBe(false);
    expect(byId.archer.unlock).toMatchObject({ name: "除妖行者", desc: "累计击杀 50" });
    expect(byId.mage.unlock).toMatchObject({ name: "斩妖大将", desc: "击杀一只妖王" });
  });
});
