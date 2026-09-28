import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { ACHIEVEMENTS } from "../../src/sim/achievements";
import { FUSION_RECIPES } from "../../src/sim/weapons";
import { defaultSave, memoryStorage, type SaveData } from "../../src/sim/save";

const ALL_RESULTS = FUSION_RECIPES.map((r) => r.result);

describe("R32 融合图鉴", () => {
  it("offer_fusion → accept 炼成入库", () => {
    const sim = new GameSim(321, memoryStorage());
    sim.exec({ action: "start" });
    sim.exec({ action: "give", args: { weapon: "tiejian" } });
    sim.exec({ action: "give", args: { weapon: "fazhang" } });
    sim.exec({ action: "offer_fusion", args: {} });
    const r = sim.resolveFusion(true);
    expect(r.accepted).toBe(true);
    const fused = sim.exec({ action: "fusions" }).data as { fused: string[] };
    expect(fused.fused).toContain("yingren");
  });

  it("reject 不入库；藏珍只记拥有一次", () => {
    const sim = new GameSim(321, memoryStorage());
    sim.exec({ action: "start" });
    sim.exec({ action: "give", args: { weapon: "tiejian" } });
    sim.exec({ action: "give", args: { weapon: "fazhang" } });
    sim.exec({ action: "offer_fusion", args: {} });
    sim.resolveFusion(false);
    expect((sim.exec({ action: "fusions" }).data as { fused: string[] }).fused).toEqual([]);
  });

  it("集齐六配方授炼金大师", () => {
    const save = memoryStorage();
    save.save({ ...defaultSave(), fused: ALL_RESULTS.slice(0, -1) });
    const sim = new GameSim(321, save);
    sim.exec({ action: "start" });
    sim.tick();
    expect(sim.achievements.unlocked.has("fusion_master")).toBe(false);
    (sim as unknown as { runFused: Set<string> }).runFused.add(ALL_RESULTS[ALL_RESULTS.length - 1]);
    sim.tick();
    expect(sim.achievements.unlocked.has("fusion_master")).toBe(true);
    expect(ACHIEVEMENTS.length).toBe(31); // R47 妖王猎手
  });

  it("死亡结算也入库；老存档缺字段兼容", () => {
    const legacy = { ...defaultSave(), fused: undefined as unknown as string[] };
    const save = memoryStorage();
    save.save(legacy as SaveData);
    const sim = new GameSim(321, save);
    sim.exec({ action: "start" });
    sim.exec({ action: "give", args: { weapon: "tiejian" } });
    sim.exec({ action: "give", args: { weapon: "fazhang" } });
    sim.exec({ action: "offer_fusion", args: {} });
    sim.resolveFusion(true);
    sim.player.hp = 1; sim.player.invincible = 0;
    sim.exec({ action: "spawn", args: { type: "basic", near: { x: sim.player.x + 10, y: sim.player.y }, count: 1 } });
    sim.enemies[0].damage = 9999;
    for (let i = 0; i < 600 && sim.state !== "GameOver"; i++) sim.tick();
    expect(sim.state).toBe("GameOver");
    expect(save.data.fused).toEqual(["yingren"]);
  });
});
