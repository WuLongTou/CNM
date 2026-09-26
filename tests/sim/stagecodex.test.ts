import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { defaultSave, memoryStorage, type SaveData } from "../../src/sim/save";

describe("R35 终极图鉴", () => {
  it("达终极入库；未达不入", () => {
    const sim = new GameSim(351, memoryStorage());
    sim.exec({ action: "start" });
    // 铁剑需升 6 次到终极（3 进阶 + 3），先推到进阶确认不入，再推到终极
    for (let i = 0; i < 3; i++) sim.applyUpgrade(0, "dmg");
    expect((sim.exec({ action: "ultimates" }).data as { reached: string[] }).reached).toEqual([]);
    for (let i = 0; i < 3; i++) sim.applyUpgrade(0, "dmg");
    expect((sim.exec({ action: "ultimates" }).data as { reached: string[] }).reached).toEqual(["tiejian"]);
    // 重复升级不重复记
    sim.applyUpgrade(0, "dmg");
    expect((sim.exec({ action: "ultimates" }).data as { reached: string[] }).reached).toEqual(["tiejian"]);
  });

  it("死亡结算也入库；老存档缺字段兼容", () => {
    const legacy = { ...defaultSave(), ultimates: undefined as unknown as string[] };
    const save = memoryStorage();
    save.save(legacy as SaveData);
    const sim = new GameSim(351, save);
    sim.exec({ action: "start" });
    for (let i = 0; i < 6; i++) sim.applyUpgrade(0, "dmg");
    sim.player.hp = 1; sim.player.invincible = 0;
    sim.exec({ action: "spawn", args: { type: "basic", near: { x: sim.player.x + 10, y: sim.player.y }, count: 1 } });
    sim.enemies[0].damage = 9999;
    for (let i = 0; i < 600 && sim.state !== "GameOver"; i++) sim.tick();
    expect(sim.state).toBe("GameOver");
    expect(save.data.ultimates).toEqual(["tiejian"]);
  });

  it("快照同步 ultimateCount", () => {
    const save = memoryStorage();
    save.save({ ...defaultSave(), ultimates: ["tiejian", "zhanfu"] });
    const sim = new GameSim(351, save);
    sim.exec({ action: "start" });
    expect(sim.status()).toMatchObject({ ultimateCount: 2 });
  });
});
