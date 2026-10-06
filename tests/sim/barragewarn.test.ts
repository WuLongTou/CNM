// R203 妖王弹幕预警：发射前 ~0.45s 发 boss_barrage_windup（每轮恰一次、
// 预警与发射交替成对；弹幕节奏不变——纯可读性，零平衡变化）。

import { describe, expect, it } from "vitest";
import { GameSim, BOSS_BARRAGE } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

function makeSim(seed = 20300) {
  const sim = new GameSim(seed, memoryStorage());
  sim.exec({ action: "start" });
  return sim;
}

/** 拉出一只孤妖王（拉开距离避免接触伤害/击杀干扰），回传事件时间线。 */
function bossTimeline(sim: GameSim, maxTicks: number, need: number) {
  sim.exec({ action: "spawn", args: { type: "游魂", count: 1, near: "player", boss: true } });
  const boss = sim.enemies[sim.enemies.length - 1]!;
  sim.enemies = [boss];
  boss.x = sim.player.x + 300;
  boss.y = sim.player.y;
  const evs: { ev: string; t: number }[] = [];
  sim.onEvent((e) => {
    if (e.event === "boss_barrage_windup" || e.event === "boss_barrage") {
      evs.push({ ev: e.event, t: sim.survivalTime });
    }
  });
  (boss as unknown as { barrageCd: number }).barrageCd = 1.0; // 快进到首弹幕窗口
  for (let i = 0; i < maxTicks && evs.length < need; i++) sim.tick();
  return evs;
}

describe("R203 妖王弹幕预警", () => {
  it("预警-发射成对；间隔落在 (0, windup+1/30] 窗口", () => {
    const sim = makeSim();
    const evs = bossTimeline(sim, 60 * 8, 4);
    const winds = evs.filter((e) => e.ev === "boss_barrage_windup");
    const fires = evs.filter((e) => e.ev === "boss_barrage");
    expect(winds.length).toBe(fires.length);
    expect(fires.length).toBeGreaterThanOrEqual(2);
    for (let i = 0; i < fires.length; i++) {
      const gap = fires[i]!.t - winds[i]!.t;
      expect(gap).toBeGreaterThan(0);
      expect(gap).toBeLessThanOrEqual(BOSS_BARRAGE.windup + 1 / 30);
    }
  });

  it("弹幕发射节奏不变：相邻发射间隔 = cd（±1 tick 量化）", () => {
    const sim = makeSim(20301);
    const evs = bossTimeline(sim, 60 * 10, 4);
    const fires = evs.filter((e) => e.ev === "boss_barrage").map((e) => e.t);
    expect(fires.length).toBeGreaterThanOrEqual(2);
    for (let i = 1; i < fires.length; i++) {
      expect(Math.abs(fires[i]! - fires[i - 1]! - BOSS_BARRAGE.cd)).toBeLessThanOrEqual(2 / 60);
    }
  });

  it("每轮恰一次预警（不重复播报）", () => {
    const sim = makeSim(20302);
    const evs = bossTimeline(sim, 60 * 10, 6);
    for (let i = 0; i < evs.length - 1; i++) {
      expect(evs[i]!.ev).not.toBe(evs[i + 1]!.ev); // 严格交替：windup→barrage→windup…
    }
  });
});
