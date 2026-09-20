import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { stageTag } from "../../src/sim/weapons";

function newSim(seed = 77) {
  const sim = new GameSim(seed);
  sim.exec({ action: "start" });
  return sim;
}

/** 把一只低血妖物放到玩家攻击范围内。 */
function placeTarget(sim: GameSim, x: number, y: number, hp = 1) {
  sim.spawnEnemy({ type: "basic", near: { x, y } });
  const e = sim.enemies[sim.enemies.length - 1];
  e.hp = hp; e.maxHp = Math.max(hp, e.maxHp);
  e.attackCd = 999;
  e.damage = 0;
  return e;
}

describe("武器装备", () => {
  it("give 指定/随机武器并成为主动武器", () => {
    const sim = newSim();
    const r = sim.exec({ action: "give", args: { weapon: "gongjian" } });
    expect(r.data).toEqual({ given: 1, weapon: "弓箭", type: "Ranged" });
    expect(sim.attackType).toBe("Ranged");
    const list = sim.exec({ action: "weapons" }).data!.list as unknown[];
    expect(list).toHaveLength(2); // 开局铁剑 + 给予的弓箭
  });

  it("武器栏上限 6 件，满时替换升级最少者，主动武器保留", () => {
    const sim = newSim(); // 开局已持铁剑（tiejian），列表不再重复给
    for (const id of ["zhanfu", "shuangdao", "gongjian", "fazhang", "tiejili"]) {
      sim.giveWeapon(id);
    }
    expect(sim.player.weapons).toHaveLength(6);
    sim.giveWeapon("yingren"); // 第 7 件：全为 0 级时替换最早的非主动武器（开局铁剑）
    expect(sim.player.weapons).toHaveLength(6);
    expect(sim.player.weapons.some((w) => w.defId === "tiejian")).toBe(false);
    expect(sim.attackType).toBe("Melee"); // 影刃是近战，主动武器更新
  });
});

describe("弹道武器", () => {
  it("Ranged 攻击发射弹道并击杀目标", () => {
    const sim = newSim();
    sim.giveWeapon("gongjian");
    sim.player.x = 1000; sim.player.y = 1000;
    const e = placeTarget(sim, 1150, 1000);
    sim.input.attack = true;
    sim.tick();
    expect(sim.projectiles.length).toBeGreaterThanOrEqual(1);
    // 弹道飞行至命中（距离 150，弹速 500 → 约 0.3s）
    for (let i = 0; i < 60 && sim.kills === 0; i++) sim.tick();
    expect(sim.kills).toBe(1);
    expect(e.hp).toBeLessThanOrEqual(0);
  });

  it("散弹枪一次发射 3 发", () => {
    const sim = newSim();
    sim.giveWeapon("sandanqiang");
    sim.player.x = 1000; sim.player.y = 1000;
    placeTarget(sim, 1200, 1000);
    sim.input.attack = true;
    sim.tick();
    expect(sim.projectiles).toHaveLength(3);
  });

  it("Magic 追踪弹转向命中", () => {
    const sim = newSim();
    sim.giveWeapon("fazhang");
    sim.player.x = 1000; sim.player.y = 1000;
    // 目标在玩家身后：直飞会错过，追踪必须转向命中
    const e = placeTarget(sim, 950, 1060, 99999);
    sim.player.facing = 1;
    sim.input.attack = true;
    sim.tick();
    expect(sim.projectiles).toHaveLength(1);
    for (let i = 0; i < 240 && e.hp > 99000; i++) sim.tick();
    expect(e.hp).toBeLessThan(99999); // 至少命中一次
  });
});

describe("元素机制", () => {
  it("中毒：命中后周期掉血（淬毒词条堆叠后必验）", () => {
    const sim = newSim();
    sim.giveWeapon("duandao"); // psn 10%
    // R2 质变词条：升级淬毒 ×2 → 10% + 30% = 40%（同时验证 bonus 生效）
    sim.exec({ action: "upgrade", args: { index: 1, stat: "psn" } });
    sim.exec({ action: "upgrade", args: { index: 1, stat: "psn" } });
    sim.player.x = 1000; sim.player.y = 1000;
    const e = placeTarget(sim, 1040, 1000, 999999);
    let poisoned = false;
    for (let i = 0; i < 30 && !poisoned; i++) {
      // 击退会把目标推远：每轮放回攻击位、清武器冷却强制出手
      e.x = sim.player.x + 40; e.y = sim.player.y;
      for (const w of sim.player.weapons) w.cd = 0;
      sim.input.attack = true;
      sim.tick();
      poisoned = e.psnT > 0;
    }
    expect(poisoned).toBe(true);
    e.psnT = 3; e.psnDps = 5; e.psnTick = 0; // 确保已中毒，只验证掉血
    const hpBefore = e.hp;
    for (let i = 0; i < 61; i++) sim.tick(); // 1 秒：至少一跳
    expect(e.hp).toBeLessThan(hpBefore);
  });

  it("冰缓：命中后妖物减速（冰缓词条堆叠后必验）", () => {
    const sim = newSim();
    sim.giveWeapon("bingshuangfazhang"); // freeze 20%
    // 只留法杖：铁剑的 lunge/击退会持续移动发射点与目标，弹道永远追不上（场景干扰）
    sim.player.weapons = sim.player.weapons.filter((w) => w.defId === "bingshuangfazhang");
    // R2 质变词条：升级冰缓 ×2 → 20% + 30% = 50%
    sim.exec({ action: "upgrade", args: { index: 0, stat: "freeze" } });
    sim.exec({ action: "upgrade", args: { index: 0, stat: "freeze" } });
    sim.player.x = 1000; sim.player.y = 1000;
    const e = placeTarget(sim, 1110, 1000, 999999);
    let frozen = false;
    for (let i = 0; i < 120 && !frozen; i++) {
      e.x = 1110; e.y = 1000; // 钉住目标，隔离"弹命中 → 冻结判定"链路
      e.vx = 0; e.vy = 0; e.staggerT = 0;
      for (const w of sim.player.weapons) w.cd = 0;
      sim.input.attack = true;
      sim.tick();
      frozen = e.frzT > 0;
    }
    expect(frozen).toBe(true);
  });

  it("地雷：放置后妖物接近触发爆炸", () => {
    const sim = newSim();
    sim.giveWeapon("tiejili");
    sim.player.x = 1000; sim.player.y = 1000;
    sim.input.attack = true;
    sim.tick();
    expect(sim.mines).toHaveLength(1);
    const e = placeTarget(sim, 1010, 1000, 50);
    e.x = 1005; e.y = 1005;
    const hpBefore = e.hp;
    for (let i = 0; i < 40 && sim.mines.length > 0; i++) sim.tick(); // 布防 0.4s 后触发
    expect(e.hp).toBeLessThan(hpBefore); // 触发爆炸
    expect(sim.mines).toHaveLength(0);
  });
});

describe("升级与进阶", () => {
  it("升级 3 次进阶、6 次终极，并推送事件", () => {
    const sim = newSim();
    const events: string[] = [];
    sim.onEvent((e) => events.push(e.event));
    sim.giveWeapon("tiejian");
    for (let i = 0; i < 3; i++) sim.exec({ action: "upgrade", args: { index: 0, stat: "dmg" } });
    expect(stageTag(sim.player.weapons[0])).toBe("ascended");
    expect(events).toContain("weapon_ascended");
    for (let i = 0; i < 3; i++) sim.exec({ action: "upgrade", args: { index: 0, stat: "dmg" } });
    expect(stageTag(sim.player.weapons[0])).toBe("ultimate");
    expect(events).toContain("weapon_ultimate");
    // 终极伤害乘数生效：1.5→2.2 全修正
    expect(sim.stats.dmgMult).toBeGreaterThan(2);
  });

  it("R2 齐射：铁剑+弓箭+法杖同时出手（近战命中+两路弹）", () => {
    const sim = newSim();
    sim.giveWeapon("gongjian");
    sim.giveWeapon("fazhang");
    sim.player.x = 1000; sim.player.y = 1000;
    placeTarget(sim, 1080, 1000); // 近战范围内（80px，铁剑 lunge 后必中）
    const proj0 = sim.projectiles.length;
    for (const w of sim.player.weapons) w.cd = 0;
    sim.input.attack = true;
    sim.tick();
    expect(sim.projectiles.length).toBeGreaterThanOrEqual(proj0 + 2); // 弓+杖两路弹
  });

  it("R2 多重词条：近战 multi +1 → 一击双目标", () => {
    const sim = newSim();
    sim.exec({ action: "upgrade", args: { index: 0, stat: "multi" } }); // 铁剑 multi 1→2
    spawnTwin(sim, 40, 8);   // 同在扇形内的两只
    spawnTwin(sim, 38, -14);
    const c0 = sim.player.combo;
    sim.input.attack = true;
    sim.tick();
    expect(sim.player.combo - c0).toBe(2); // 一挥双中
  });
});

/** 两只并排靶子（扇形判定内）。 */
function spawnTwin(sim: GameSim, dx: number, dy: number) {
  sim.spawnEnemy({ type: "basic", near: { x: sim.player.x + dx, y: sim.player.y + dy } });
  const e = sim.enemies[sim.enemies.length - 1];
  e.hp = 1e9; e.maxHp = 1e9; e.attackCd = 999; e.damage = 0;
  return e;
}

describe("宝箱", () => {
  it("靠近宝箱自动开启并获得武器", () => {
    const sim = newSim(42);
    const chest = sim.chests[0];
    sim.exec({ action: "teleport", args: { x: chest.x, y: chest.y } });
    const events: string[] = [];
    sim.onEvent((e) => events.push(e.event));
    sim.tick();
    expect(chest.opened).toBe(true);
    expect(sim.player.weapons.length).toBeGreaterThanOrEqual(1);
    expect(events).toContain("chest_opened");
    expect(events).toContain("weapon_gained");
  });
});
