// 升级符箓闭环（击杀掉落 → 拾取 → 三选一 → 应用）与天气切换事件回归。
import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

function newSim(seed = 42) {
  const save = memoryStorage();
  const sim = new GameSim(seed, save);
  sim.exec({ action: "start" });
  sim.exec({ action: "give", args: { weapon: "tiejian" } });
  return sim;
}

describe("升级符箓闭环", () => {
  it("妖王击杀必掉 3 枚符箓", () => {
    const sim = newSim();
    sim.exec({ action: "spawn", args: { type: "basic", boss: true, near: "player" } });
    const boss = sim.enemies.find((e) => e.boss)!;
    (sim as unknown as { onEnemyKilled: (e: typeof boss) => void }).onEnemyKilled(boss);
    expect(sim.pickups.length).toBe(3);
  });

  it("走近拾取触发三选一报价并暂停，武器×词条不重复", () => {
    const sim = newSim();
    sim.pickups.push({ id: 9001, x: sim.player.x, y: sim.player.y });
    const events: string[] = [];
    sim.onEvent((e) => events.push(e.event));
    sim.tick();
    expect(sim.upgradeOffers).not.toBeNull();
    expect(sim.upgradeOffers!.length).toBe(3);
    expect(sim.paused).toBe(true);
    expect(events).toContain("upgrade_offer");
    const keys = sim.upgradeOffers!.map((o) => `${o.weaponIdx}:${o.stat}`);
    expect(new Set(keys).size).toBe(keys.length);
  });

  it("报价打开时 tick 清空游戏输入（模态独占输入硬约束）", () => {
    const sim = newSim();
    sim.pickups.push({ id: 9001, x: sim.player.x, y: sim.player.y });
    sim.tick(); // 拾取 → 报价打开（paused）
    sim.input = { moveX: 1, moveY: 1, attack: true, dash: true, cycleTarget: true };
    sim.tick();
    expect(sim.input).toEqual({ moveX: 0, moveY: 0, attack: false, dash: false, cycleTarget: false });
  });

  it("应答应用词条：ups+1、解除暂停、推送 upgrade_applied；无效序号保持报价", () => {
    const sim = newSim();
    sim.pickups.push({ id: 9001, x: sim.player.x, y: sim.player.y });
    sim.tick();
    const idx = sim.upgradeOffers![0].weaponIdx;
    const before = sim.player.weapons[idx].ups;
    const events: string[] = [];
    sim.onEvent((e) => events.push(e.event));

    const bad = sim.exec({ action: "choose", args: { target: "upgrade", index: 9 } });
    expect(bad.data).toMatchObject({ handled: false });
    expect(sim.upgradeOffers).not.toBeNull(); // 报价保持

    const r = sim.exec({ action: "choose", args: { target: "upgrade", index: 0 } });
    expect(r.data).toMatchObject({ handled: true });
    expect(sim.player.weapons[idx].ups).toBe(before + 1);
    expect(sim.paused).toBe(false);
    expect(sim.upgradeOffers).toBeNull();
    expect(events).toContain("upgrade_applied");
  });

  it("场上无符箓时不产生报价", () => {
    const sim = newSim();
    sim.tick();
    expect(sim.upgradeOffers).toBeNull();
    expect(sim.paused).toBe(false);
  });
});

describe("天气切换事件", () => {
  it("天气类型变化时推送 weather_changed 且 HUD 可读", () => {
    const sim = newSim();
    const events: Array<{ event: string; data: Record<string, unknown> }> = [];
    sim.onEvent((e) => events.push(e));
    sim.weather.state = { ...sim.weather.state, type: "BloodMoon", intensity: "Moderate" };
    sim.tick();
    const ev = events.find((e) => e.event === "weather_changed");
    expect(ev).toBeDefined();
    expect(ev!.data).toMatchObject({ type: "BloodMoon", intensity: "Moderate" });
    // 同类型持续 tick 不重复推送
    sim.tick();
    sim.tick();
    expect(events.filter((e) => e.event === "weather_changed").length).toBe(1);
  });
});

// ── 妖物特殊 AI ──

describe("妖物特殊 AI", () => {
  it("爆魈贴近自爆：对玩家造成 1.5× 伤害并自灭（不计击杀）", () => {
    const sim = newSim();
    const hp0 = sim.player.hp;
    sim.exec({ action: "spawn", args: { type: 3, near: { x: sim.player.x + 40, y: sim.player.y } } });
    const events: string[] = [];
    sim.onEvent((e) => events.push(e.event));
    const kills0 = sim.kills;
    sim.tick();
    expect(sim.player.hp).toBeLessThan(hp0);
    expect(sim.enemies.some((e) => e.type === 3)).toBe(false); // 自爆消亡
    expect(sim.kills).toBe(kills0); // 不计击杀
    expect(events).toContain("enemy_explode");
  });

  it("弩妖射程外追击、射程内站桩射击敌方弹道", () => {
    const sim = newSim();
    sim.player.invincible = 999; // 本测试只验证弹道生成，不验证命中
    sim.exec({ action: "spawn", args: { type: 8, near: { x: sim.player.x + 200, y: sim.player.y } } });
    const archer = sim.enemies.find((e) => e.type === 8)!;
    archer.attackCd = 0;
    const x0 = archer.x;
    sim.tick();
    // 200 < 340 射程：站桩 + 发射
    expect(sim.enemyProjectiles.length).toBe(1);
    expect(archer.x).toBe(x0); // 站桩不移动
    // 射程外继续追击
    archer.x = sim.player.x + 600;
    archer.y = sim.player.y;
    archer.attackCd = 0;
    sim.enemyProjectiles.length = 0;
    sim.tick();
    expect(sim.enemyProjectiles.length).toBe(0);
    expect(Math.abs(archer.x - sim.player.x)).toBeLessThan(600);
  });

  it("瘴妖毒弹带毒标记", () => {
    const sim = newSim();
    sim.player.invincible = 999;
    sim.exec({ action: "spawn", args: { type: 5, near: { x: sim.player.x + 150, y: sim.player.y } } });
    const spitter = sim.enemies.find((e) => e.type === 5)!;
    spitter.attackCd = 0;
    sim.tick();
    expect(sim.enemyProjectiles.length).toBe(1);
    expect(sim.enemyProjectiles[0].psn).toBe(true);
  });

  it("隐煞隐身时不可被玩家索敌命中，周期后显形", () => {
    const sim = newSim();
    sim.exec({ action: "spawn", args: { type: 4, near: "random" } });
    const ghost = sim.enemies.find((e) => e.type === 4)!;
    expect(ghost.stealth).toBe(true);
    // 隐身中：放在玩家攻击范围内也索敌不到
    ghost.x = sim.player.x + 50;
    ghost.y = sim.player.y;
    ghost.stealth = true;
    const near = (sim as unknown as { nearestEnemies: (x: number, y: number, r: number, n: number) => unknown[] })
      .nearestEnemies(sim.player.x, sim.player.y, 100, 10);
    expect(near.some((e) => (e as { id: number }).id === ghost.id)).toBe(false);
    // 显形后可索敌
    ghost.stealth = false;
    const near2 = (sim as unknown as { nearestEnemies: (x: number, y: number, r: number, n: number) => unknown[] })
      .nearestEnemies(sim.player.x, sim.player.y, 100, 10);
    expect(near2.some((e) => (e as { id: number }).id === ghost.id)).toBe(true);
  });

  it("愈妖周期治疗半径内受伤同伴", () => {
    const sim = newSim();
    sim.exec({ action: "spawn", args: { type: 6, near: { x: sim.player.x + 300, y: sim.player.y } } });
    sim.exec({ action: "spawn", args: { type: 2, near: { x: sim.player.x + 380, y: sim.player.y } } });
    const healer = sim.enemies.find((e) => e.type === 6)!;
    const tank = sim.enemies.find((e) => e.type === 2)!;
    tank.hp = tank.maxHp * 0.5; // 打伤同伴
    healer.healCd = 0.001;
    const hp0 = tank.hp;
    sim.tick();
    expect(tank.hp).toBeGreaterThan(hp0);
  });

  it("唤灵周期召唤蚀群", () => {
    const sim = newSim();
    sim.exec({ action: "spawn", args: { type: 7, near: { x: sim.player.x + 300, y: sim.player.y } } });
    const summoner = sim.enemies.find((e) => e.type === 7)!;
    summoner.summonCd = 0.001;
    sim.tick();
    expect(sim.enemies.filter((e) => e.type === 13).length).toBeGreaterThanOrEqual(2);
  });
});

describe("高阶妖物机制", () => {
  it("铁傀常驻减伤 25%", () => {
    const sim = newSim();
    sim.exec({ action: "spawn", args: { type: 10, near: "random" } });
    const mech = sim.enemies.find((e) => e.type === 10)!;
    const hp0 = mech.hp;
    (sim as unknown as { dealDamage: (e: typeof mech, d: number) => void }).dealDamage(mech, 100);
    expect(hp0 - mech.hp).toBeCloseTo(75);
  });

  it("甲妖举盾期间完全免伤，盾歇正常受击", () => {
    const sim = newSim();
    sim.exec({ action: "spawn", args: { type: 11, near: "random" } });
    const knight = sim.enemies.find((e) => e.type === 11)!;
    const dd = (e: typeof knight, d: number) =>
      (sim as unknown as { dealDamage: (e2: typeof knight, d2: number) => void }).dealDamage(e, d);
    knight.blockT = 1;
    const hp0 = knight.hp;
    dd(knight, 100);
    expect(knight.hp).toBe(hp0); // 举盾免伤
    knight.blockT = 0;
    dd(knight, 100);
    expect(knight.hp).toBe(hp0 - 100); // 盾歇受击
  });

  it("蛟周期蓄力后锁定方向冲刺，接触玩家造成 1.5× 伤害", () => {
    const sim = newSim();
    sim.player.hp = 1e9;
    sim.exec({ action: "spawn", args: { type: 12, near: { x: sim.player.x + 300, y: sim.player.y } } });
    const dragon = sim.enemies.find((e) => e.type === 12)!;
    dragon.chargeCd = 0.001;
    sim.tick();
    expect(dragon.chargeWind).toBeGreaterThan(0); // 进入蓄力
    // 等待蓄力结束进入冲刺（蓄力 0.5s = 30 tick）
    for (let i = 0; i < 60 && dragon.chargeDash <= 0; i++) sim.tick();
    expect(dragon.chargeDash).toBeGreaterThan(0);
    // 玩家站到冲刺方向前方 80px 的固定点（冲刺全程约 175px 必然穿过）
    const sp = Math.hypot(dragon.chargeVx, dragon.chargeVy) || 1;
    sim.player.x = dragon.x + (dragon.chargeVx / sp) * 80;
    sim.player.y = dragon.y + (dragon.chargeVy / sp) * 80;
    for (let i = 0; i < 40 && sim.player.hp >= 1e9; i++) sim.tick();
    expect(sim.player.hp).toBeLessThan(1e9); // 冲撞命中
  });

  it("浮灯蛇形漂浮：追击轨迹含垂直摆动分量", () => {
    const sim = newSim();
    // 玩家在正下方：常规追击只在 y 轴移动，摆动应产生 x 偏移
    sim.exec({ action: "spawn", args: { type: 9, near: { x: sim.player.x, y: sim.player.y - 400 } } });
    const ship = sim.enemies.find((e) => e.type === 9)!;
    const x0 = ship.x;
    for (let i = 0; i < 40; i++) sim.tick();
    expect(Math.abs(ship.x - x0)).toBeGreaterThan(4);
  });
});

describe("音频事件与累计成就", () => {
  it("冲刺触发 player_dash 事件，连击 3 触发 combo_milestone", () => {
    const sim = newSim();
    const events: string[] = [];
    sim.onEvent((e) => events.push(e.event));
    // 冲刺
    sim.input = { moveX: 0, moveY: 0, attack: false, dash: true, cycleTarget: false };
    sim.tick();
    expect(events).toContain("player_dash");
    // 连击 3：命中三次（spawn 靶子在前方 40px + 按住攻击；过近会被突进越过）
    sim.exec({ action: "spawn", args: { type: 0, near: { x: sim.player.x + 40, y: sim.player.y } } });
    const z = sim.enemies[sim.enemies.length - 1]; // 刚 spawn 的贴脸靶子（末位）
    z.hp = 1e9; // 不死靶子
    sim.input = { moveX: 0, moveY: 0, attack: true, dash: false, cycleTarget: false };
    for (let i = 0; i < 90 && !events.includes("combo_milestone"); i++) sim.tick();
    expect(events).toContain("combo_milestone");
  });

  it("成就累计击杀按跨局 totalKills 判定", () => {
    const sim = newSim();
    // 存档已有 49 累计击杀：本局首杀即应达成 slayer_50
    const prev = sim.exec({ action: "meta" });
    void prev;
    (sim as unknown as { baseTotalKills: number }).baseTotalKills = 49;
    sim.exec({ action: "spawn", args: { type: 0, near: { x: sim.player.x + 10, y: sim.player.y } } });
    (sim as unknown as { onEnemyKilled: (e: (typeof sim.enemies)[0]) => void }).onEnemyKilled(sim.enemies[0]);
    sim.tick(); // 成就仅在 tick 中刷新
    expect(sim.achievements.unlocked.has("first_blood")).toBe(true);
    expect(sim.achievements.unlocked.has("slayer_50")).toBe(true);
    expect(sim.achievements.unlocked.has("slayer_200")).toBe(false);
  });
});
