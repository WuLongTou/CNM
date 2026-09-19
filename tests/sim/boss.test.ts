// M15 妖王战：环形弹幕（周期/阶段加密/死后停止）与出场事件回归。
import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { BOSS_BARRAGE } from "../../src/sim/game";

describe("妖王环形弹幕", () => {
  function bossSim() {
    const sim = new GameSim(42, memoryStorage());
    sim.exec({ action: "start" });
    const events: string[] = [];
    sim.onEvent((e) => events.push(e.event));
    sim.exec({ action: "spawn", args: { type: 0, boss: true, near: "player" } });
    const boss = sim.enemies[sim.enemies.length - 1];
    boss.hp = 1e9; // 不死靶王
    return { sim, events, boss };
  }

  it("出场推送 boss_spawn，周期放出环形弹幕", () => {
    const { sim, events, boss } = bossSim();
    expect(events).toContain("boss_spawn");
    // 快进到首次弹幕（first=3s）之后
    for (let i = 0; i < 260 && !events.includes("boss_barrage"); i++) sim.tick();
    expect(events).toContain("boss_barrage");
    // 弹幕数量 = 阶段 0 配置，弹体真实存在
    const e0 = events.filter((x) => x === "enemy_shot").length;
    expect(e0).toBeGreaterThanOrEqual(BOSS_BARRAGE.count[0]);
    // 弹幕冷却重置
    expect(boss.barrageCd).toBeGreaterThan(0);
  });

  it("阶段 2 双层环：弹数翻倍至 count[2]×2", () => {
    const { sim, events, boss } = bossSim();
    boss.bossPhase = 2;
    boss.barrageCd = 0.01;
    const before = events.filter((x) => x === "enemy_shot").length;
    sim.tick();
    const fired = events.filter((x) => x === "enemy_shot").length - before;
    expect(fired).toBe(BOSS_BARRAGE.count[2] * 2);
  });

  it("弹幕起始角按黄金角逐轮旋转（螺旋漂移，确定性）", () => {
    const { sim, boss } = bossSim();
    const angles: number[] = [];
    // 通过两次弹幕的弹体角度差验证轮转：截获弹道速度方向
    boss.barrageCd = 0.01;
    sim.tick();
    const first = sim.enemyProjectiles.slice(-BOSS_BARRAGE.count[0]);
    angles.push(...first.map((p) => Math.atan2(p.vy, p.vx)));
    boss.barrageCd = 0.01;
    sim.tick();
    const second = sim.enemyProjectiles.slice(-BOSS_BARRAGE.count[0]);
    angles.push(...second.map((p) => Math.atan2(p.vy, p.vx)));
    // 两轮同序号弹的角度差应 ≈ 黄金角（mod 2π）
    const d = Math.abs(angles[BOSS_BARRAGE.count[0]] - angles[0]) % (Math.PI * 2);
    const golden = 2.39996 % (Math.PI * 2);
    expect(Math.min(d, Math.PI * 2 - d)).toBeCloseTo(Math.min(golden, Math.PI * 2 - golden), 1);
  });

  it("妖王死亡后不再放弹幕", () => {
    const { sim, events, boss } = bossSim();
    sim.exec({ action: "give", args: { weapon: "jinfu" } });
    sim.player.baseDamage = 1e7;
    boss.hp = 100;
    // 贴脸攻击秒杀
    boss.x = sim.player.x + 30;
    boss.y = sim.player.y;
    sim.input = { moveX: 0, moveY: 0, attack: true, dash: false, cycleTarget: false };
    sim.tick();
    const dead = !sim.enemies.some((e) => e.boss);
    expect(dead).toBe(true);
    const before = events.filter((x) => x === "enemy_shot").length;
    for (let i = 0; i < 320; i++) sim.tick();
    const after = events.filter((x) => x === "enemy_shot").length - before;
    expect(after).toBe(0); // 无妖王 → 无弹幕（其余敌种 3s 内可能射箭，世界仅此怪时为 0）
  });
});
