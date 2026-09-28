// R75 Boss Rush 经济适配：龛节奏（45s 首龛/60s 补龛）与妖王金币 30。
// 纯妖王局无小怪金币供给——量化依据（R75 bot 度量，stick 5 种子均值 28s 死于
// 首王、零金币；人类首杀约 45-60s → 45s 首龛 + 30/王 = 首龛可买调息、6 王 180 金全槽可达）。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { CFG } from "../../src/sim/config";

function readyRush(seed = 750) {
  const sim = new GameSim(seed, memoryStorage(), "sword", 0, {}, "boss_rush");
  sim.exec({ action: "start" });
  return sim;
}

describe("R75 Boss Rush 经济适配", () => {
  it("龛节奏：Boss Rush 45s 首龛（普通模式 75s 不受影响）", () => {
    const rush = readyRush();
    rush.player.maxHp = rush.player.hp = 1e9; // 挂机不死者：隔离玩家生存对龛计时的干扰
    for (let i = 0; i < 46 * 60; i++) rush.tick(1 / 60);
    // 妖王在场（boss rush 第 1 阶）：龛仍会浮现
    expect(rush.shrines.length).toBe(1);
    const normal = new GameSim(751, memoryStorage());
    normal.exec({ action: "start" });
    normal.player.maxHp = normal.player.hp = 1e9;
    for (let i = 0; i < 46 * 60; i++) normal.tick(1 / 60);
    expect(normal.shrines.length).toBe(0); // 普通模式 75s 首龛不受影响
  });

  it("妖王金币：Boss Rush 30（普通模式 20 不变）", () => {
    const rush = readyRush(752);
    rush.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1, boss: true } });
    const e = rush.enemies[rush.enemies.length - 1]!;
    (rush as unknown as { dealDamage(e: unknown, d: number): void }).dealDamage(e, 99999);
    expect(rush.coins).toBe(CFG.Shop.BossGoldRush);
    const normal = new GameSim(752, memoryStorage());
    normal.exec({ action: "start" });
    normal.exec({ action: "spawn", args: { type: "basic", near: "player", count: 1, boss: true } });
    const e2 = normal.enemies[normal.enemies.length - 1]!;
    (normal as unknown as { dealDamage(e: unknown, d: number): void }).dealDamage(e2, 99999);
    expect(normal.coins).toBe(CFG.Shop.BossGold);
  });
});