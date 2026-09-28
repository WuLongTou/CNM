import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { ACHIEVEMENTS } from "../../src/sim/achievements";
import { WEAPON_DATA } from "../../src/sim/weapons";
import { defaultSave, memoryStorage, type SaveData } from "../../src/sim/save";

const ALL_WEAPONS = Object.keys(WEAPON_DATA);

describe("R30 武器藏珍", () => {
  it("起手/给予/转职皆入库；协议返回并集", () => {
    const sim = new GameSim(301, memoryStorage());
    sim.exec({ action: "start" });
    sim.giveWeapon("fazhang");
    const armory = sim.exec({ action: "armory" }).data as { owned: string[] };
    expect(armory.owned).toContain("tiejian");
    expect(armory.owned).toContain("fazhang");
  });

  it("死亡结算也入库；老存档缺字段兼容", () => {
    const legacy = { ...defaultSave(), armory: undefined as unknown as string[] };
    const save = memoryStorage();
    save.save(legacy as SaveData);
    const sim = new GameSim(301, save);
    sim.exec({ action: "start" });
    sim.giveWeapon("gongjian");
    sim.player.hp = 1; sim.player.invincible = 0;
    sim.exec({ action: "spawn", args: { type: "basic", near: { x: sim.player.x + 10, y: sim.player.y }, count: 1 } });
    sim.enemies[0].damage = 9999;
    for (let i = 0; i < 600 && sim.state !== "GameOver"; i++) sim.tick();
    expect(sim.state).toBe("GameOver");
    expect(save.data.armory.sort()).toEqual(["gongjian", "tiejian"]);
  });

  it("集齐藏珍授藏珍满架", () => {
    const save = memoryStorage();
    save.save({ ...defaultSave(), armory: ALL_WEAPONS.slice(0, -1) });
    const sim = new GameSim(301, save);
    sim.exec({ action: "start" });
    sim.tick();
    expect(sim.achievements.unlocked.has("curator")).toBe(false);
    sim.giveWeapon(ALL_WEAPONS[ALL_WEAPONS.length - 1]);
    sim.tick();
    expect(sim.achievements.unlocked.has("curator")).toBe(true);
    expect(ACHIEVEMENTS.length).toBe(29); // R47 妖王猎手
  });
});
