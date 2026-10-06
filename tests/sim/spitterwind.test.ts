// R204 远程读题（预警不延时，瘴妖/弩妖）：射程内且攻击冷却进入 0.35s 预警
// 窗口时置 aimWarned（渲染层画血红蓄力环），开火即清、每轮恰一次——出手
// 时机分毫不变（零平衡），只把题面提前 0.35s 亮给玩家。
// 背景记录：延迟出手方案（远程 M20 两段式）实测翻「妖潮磨血有效」基线钉
// （雪球回路放大任何磨血减免），故只做信息层。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

function makeSim(seed = 20400) {
  const sim = new GameSim(seed, memoryStorage());
  sim.exec({ action: "start" });
  return sim;
}

/** 造一只远程妖（不动 sim.enemies 数组，由调用方决定保留谁）。 */
function spawnRanged(sim: GameSim, type: number, dist: number, cd: number) {
  sim.exec({ action: "spawn", args: { type, count: 1, near: "player" } });
  const e = sim.enemies[sim.enemies.length - 1]!;
  e.x = sim.player.x + dist;
  e.y = sim.player.y;
  (e as unknown as { attackCd: number }).attackCd = cd;
  return e;
}

/** 清场后钉一只远程妖在指定距离（唯一妖物）。 */
function pinRanged(sim: GameSim, type: number, dist: number, cd: number) {
  const e = spawnRanged(sim, type, dist, cd);
  sim.enemies = [e];
  return e;
}

describe("R204 远程读题（预警不延时）", () => {
  it("瘴妖：冷却进入 0.35s 窗口置 aimWarned，开火即清", () => {
    const sim = makeSim();
    const e = pinRanged(sim, 5, 200, 0.36); // 射程 300；1 tick 后 cd=0.343 进入窗口
    sim.tick();
    expect(e.aimWarned).toBe(true); // 亮题
    expect(sim.enemyProjectiles.length).toBe(0); // 还没开火
    let warnedTicks = 0;
    for (let i = 0; i < 60 && sim.enemyProjectiles.length === 0; i++) {
      if (e.aimWarned) warnedTicks++;
      sim.tick();
    }
    expect(sim.enemyProjectiles.length).toBe(1); // 开火
    expect(e.aimWarned).toBe(false); // 开火即清
    expect(warnedTicks).toBeGreaterThanOrEqual(15); // 读题窗口 ≥0.25s
  });

  it("每轮恰一次：第二轮重新亮题（边沿触发不重复）", () => {
    const sim = makeSim(20401);
    const e = pinRanged(sim, 5, 200, 0.3);
    sim.tick();
    expect(e.aimWarned).toBe(true);
    for (let i = 0; i < 60 && sim.enemyProjectiles.length === 0; i++) sim.tick();
    expect(sim.enemyProjectiles.length).toBe(1);
    expect(e.aimWarned).toBe(false);
    // 第二轮：cd 重注后再次走 置位→开火→清 完整周期
    (e as unknown as { attackCd: number }).attackCd = 0.3;
    let rising = 0;
    let prevWarn = false;
    for (let i = 0; i < 120 && sim.enemyProjectiles.length < 2; i++) {
      sim.tick();
      if (e.aimWarned && !prevWarn) rising++;
      prevWarn = e.aimWarned ?? false;
    }
    expect(sim.enemyProjectiles.length).toBe(2);
    expect(rising).toBe(1); // 第二轮恰一次置位（边沿触发）
  });

  it("弩妖同款读题；射程外不亮题也不开火", () => {
    const sim = makeSim(20402);
    const inRange = spawnRanged(sim, 8, 250, 0.3); // 弩妖射程 340
    const outRange = spawnRanged(sim, 8, 420, 0.1); // 射程外
    sim.enemies = [inRange, outRange];
    sim.tick();
    expect(inRange.aimWarned).toBe(true);
    expect(outRange.aimWarned ?? false).toBe(false);
    for (let i = 0; i < 30; i++) sim.tick();
    expect(sim.enemyProjectiles.length).toBe(1); // 只有射程内那轮开火
  });
});
