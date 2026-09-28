// R85 手柄右摇杆瞄准（twin-stick）：InputState.aimX/aimY 覆盖移动瞄准。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { makeWeapon } from "../../src/sim/weapons";

function aimedSim() {
  const sim = new GameSim(850, memoryStorage());
  sim.exec({ action: "start" });
  return sim;
}

describe("R85 右摇杆瞄准", () => {
  it("aim 覆盖移动瞄准：向左移动但瞄准右侧", () => {
    const sim = aimedSim();
    sim.input = { moveX: -1, moveY: 0, attack: false, dash: false, cycleTarget: false, aimX: 1, aimY: 0 };
    sim.tick(1 / 60);
    expect(sim.player.aimAng).toBeCloseTo(0, 6); // 瞄准右（覆盖移动向左的 π）
  });

  it("无 aim 时回退移动瞄准", () => {
    const sim = aimedSim();
    sim.input = { moveX: -1, moveY: 0, attack: false, dash: false, cycleTarget: false };
    sim.tick(1 / 60);
    expect(sim.player.aimAng).toBeCloseTo(Math.PI, 6); // 朝移动方向（左）
  });

  it("aim 死区（接近 0 向量）回退移动瞄准", () => {
    const sim = aimedSim();
    sim.input = { moveX: 0, moveY: -1, attack: false, dash: false, cycleTarget: false, aimX: 0, aimY: 0 };
    sim.tick(1 / 60);
    expect(sim.player.aimAng).toBeCloseTo(-Math.PI / 2, 6);
  });

  it("近战朝瞄准方向命中：敌在移动反方向也能打到", () => {
    const sim = aimedSim();
    sim.player.weapons = [makeWeapon("tiejian")]; // 单武器：齐射仅铁剑出手
    sim.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1 } });
    const e = sim.enemies[sim.enemies.length - 1]!;
    sim.enemies = [e];
    e.hp = 100; e.x = sim.player.x + 24; e.y = sim.player.y; // 敌在右
    sim.input = { moveX: -1, moveY: 0, attack: true, dash: false, cycleTarget: false, aimX: 1, aimY: 0 };
    for (let i = 0; i < 90 && e.hp === 100; i++) {
      e.x = sim.player.x + 24; e.y = sim.player.y; // 每 tick 重钉右侧
      sim.tick(1 / 60);
    }
    expect(e.hp).toBeLessThan(100); // 向左移动 + 瞄准右：仍命中右侧敌人
  });
});