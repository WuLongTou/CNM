import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { ACHIEVEMENTS } from "../../src/sim/achievements";
import { WEAPON_DATA } from "../../src/sim/weapons";
import { memoryStorage } from "../../src/sim/save";

const IDS = Object.keys(WEAPON_DATA);

describe("R38 终极收藏家", () => {
  it("5 把不同武器达终极即授勋；4 把不授", () => {
    const sim = new GameSim(381, memoryStorage());
    sim.exec({ action: "start" });
    sim.enemies.length = 0;
    sim.terrain.altars.length = 0;
    // 铁剑升满 → 第 1 把终极；此后每次新加一把并各自升满
    for (let i = 0; i < 6; i++) sim.applyUpgrade(0, "dmg");
    const ultimateOf = (idx: number) => { for (let i = 0; i < 6; i++) sim.applyUpgrade(idx, "dmg"); };
    for (let k = 0; k < 3; k++) {
      sim.exec({ action: "give", args: { weapon: IDS[10 + k] } });
      ultimateOf(sim.player.weapons.length - 1);
    }
    sim.tick();
    expect(sim.achievements.unlocked.has("ultimate_hoarder")).toBe(false);
    // 第 5 把
    sim.exec({ action: "give", args: { weapon: IDS[13] } });
    ultimateOf(sim.player.weapons.length - 1);
    sim.tick();
    expect(sim.achievements.unlocked.has("ultimate_hoarder")).toBe(true);
    expect(ACHIEVEMENTS.length).toBe(39); // R47 妖王猎手
  });
});
