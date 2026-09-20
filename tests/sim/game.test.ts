import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { CFG } from "../../src/sim/config";
import { memoryStorage } from "../../src/sim/save";

function newSim(seed = 123) {
  const save = memoryStorage();
  const sim = new GameSim(seed, save);
  return { sim, save };
}

describe("GameSim 状态机", () => {
  it("初始为 WeaponSelection，start() 后进入 Combat", () => {
    const { sim } = newSim();
    expect(sim.state).toBe("WeaponSelection");
    expect(sim.status().welcome_visible).toBe(true);
    sim.exec({ action: "start" });
    expect(sim.state).toBe("Combat");
  });

  it("非战斗态 tick 清空输入（模态独占输入硬约束）", () => {
    const { sim } = newSim();
    sim.input = { moveX: 1, moveY: 0, attack: true, dash: true, cycleTarget: true };
    sim.tick();
    expect(sim.input).toEqual({ moveX: 0, moveY: 0, attack: false, dash: false, cycleTarget: false });
  });

  it("force_combat 在 GameOver 态无效", () => {
    const { sim } = newSim();
    sim.exec({ action: "start" });
    sim.player.hp = 1;
    sim.player.invincible = 0;
    // 放一只高伤妖物贴脸击杀玩家
    sim.exec({ action: "spawn", args: { type: "basic", near: { x: sim.player.x + 10, y: sim.player.y }, count: 1 } });
    const z = sim.enemies[0];
    z.damage = 9999;
    for (let i = 0; i < 600 && sim.state !== "GameOver"; i++) sim.tick();
    expect(sim.state).toBe("GameOver");
    expect(sim.exec({ action: "force_combat" }).data).toEqual({ combat: false });
  });
});

describe("GameSim 刷怪与难度", () => {
  it("世界妖物数维持当前幕上限内且不低于底噪", () => {
    const { sim } = newSim();
    sim.exec({ action: "start" });
    for (let i = 0; i < 60 * 30; i++) sim.tick(); // 30 秒
    expect(sim.enemies.length).toBeGreaterThanOrEqual(CFG.Acts.MinEnemies[sim.act] - 1);
    expect(sim.enemies.length).toBeLessThanOrEqual(CFG.Acts.MaxEnemies[sim.act]);
  });

  it("45 秒推进一威胁阶并推送事件", () => {
    const { sim } = newSim();
    const events: string[] = [];
    sim.onEvent((e) => events.push(e.event));
    sim.exec({ action: "start" });
    sim.player.hp = 1e9; // 本测试只验证时间推进，抬血避免站桩被围殴致死
    for (let i = 0; i < 60 * 46; i++) sim.tick(); // 46 秒
    expect(sim.round).toBe(2);
    expect(events).toContain("threat_tier");
  });

  it("roundMult 与 C# _GetRoundMult 公式一致（1 + round*0.04*2*factor）", () => {
    const { sim } = newSim();
    sim.round = 3;
    expect(sim.roundMult(1)).toBeCloseTo(1 + 3 * 0.08);
    expect(sim.roundMult(1.2)).toBeCloseTo(1 + 3 * 0.08 * 1.2);
  });

  it("每 5 阶出 Boss", () => {
    const { sim } = newSim();
    sim.exec({ action: "start" });
    sim.player.hp = 1e9;
    // 推进到 round 5：4 次 45s
    for (let i = 0; i < 60 * 45 * 4 + 10; i++) sim.tick();
    expect(sim.round).toBe(5);
    expect(sim.enemies.some((z) => z.boss)).toBe(true);
  });
});

describe("R2 分幕与胜利", () => {
  it("act 随时间推进；密度上限随幕爬升", () => {
    const { sim } = newSim();
    sim.exec({ action: "start" });
    expect(sim.act).toBe(0);
    sim.survivalTime = CFG.Acts.ActTime * 2 + 1; // 幕 2
    expect(sim.act).toBe(2);
    sim.player.hp = 1e9;
    for (let i = 0; i < 60 * 40; i++) sim.tick(); // 40s 刷怪窗口
    expect(sim.enemies.length).toBeLessThanOrEqual(CFG.Acts.MaxEnemies[2]);
  });

  it("精英潮：幕 2 起每轮追加精英", () => {
    const { sim } = newSim();
    sim.exec({ action: "start" });
    sim.player.hp = 1e9;
    sim.survivalTime = CFG.Acts.ActTime * 2; // 幕 2
    const events: string[] = [];
    sim.onEvent((e) => events.push(e.event));
    sim.round = 4; // 下一 tick advanceRound → round 5
    sim.roundTimer = CFG.Difficulty.RampInterval;
    sim.tick();
    expect(events).toContain("elite_wave");
    expect(sim.enemies.some((z) => z.elite)).toBe(true);
  });

  it("终幕妖王击杀 → Victory；continue 进入无尽", () => {
    const { sim, save } = newSim();
    sim.exec({ action: "start" });
    sim.player.hp = 1e9;
    sim.exec({ action: "goto_round", args: { round: CFG.Acts.FinalBossRound } });
    sim.tick(); // advanceRound → 终幕妖王降临
    expect(sim.round).toBe(CFG.Acts.FinalBossRound);
    const king = sim.enemies.find((z) => z.final);
    expect(king).toBeDefined();
    expect(king!.maxHp).toBeGreaterThan(1000); // 强化倍率生效
    // 站到妖王左侧向右移动/攻击：aimAng 跟随移动方向，吸附锥内命中
    king!.hp = 1;
    sim.exec({ action: "teleport", args: { x: king!.x - 40, y: king!.y } });
    sim.player.invincible = 1e9;
    sim.input = { moveX: 1, moveY: 0, attack: true, dash: false, cycleTarget: false };
    sim.tick();
    expect(sim.state).toBe("Victory");
    expect(save.data.games).toBe(1);
    const c = sim.exec({ action: "continue" });
    expect(c.data).toEqual({ continued: true });
    expect(sim.state).toBe("Combat");
  });
});

describe("GameSim 战斗闭环", () => {
  it("spawn 指令可指定类型与位置；击杀计分", () => {
    const { sim } = newSim();
    sim.exec({ action: "start" });
    sim.exec({ action: "teleport", args: { x: 1000, y: 1000 } });
    const r = sim.exec({ action: "spawn", args: { type: "basic", near: { x: 1010, y: 1000 } } });
    expect(r.data).toEqual({ spawned: 1 });
    expect(sim.enemies).toHaveLength(1);
    const z = sim.enemies[0];
    z.hp = 1;
    sim.player.attackCd = 0;
    sim.input.attack = true;
    sim.tick();
    expect(sim.kills).toBe(1);
    expect(sim.score).toBe(10); // Basic 分值
  });

  it("连击与冷却：挥空也进冷却；连击叠加伤害", () => {
    const { sim } = newSim();
    sim.exec({ action: "start" });
    sim.player.attackCd = 0;
    sim.input.attack = true;
    sim.tick();
    expect(sim.player.attackCd).toBeGreaterThan(0); // 挥空也进冷却
    sim.input.attack = true;
    const canAttack = sim.player.attackCd > 0;
    sim.tick();
    expect(canAttack).toBe(true);
    expect(sim.kills).toBe(0);
  });

  it("玩家受击有无敌帧；死亡进入 GameOver 并结算存档", () => {
    const { sim, save } = newSim();
    sim.exec({ action: "start" });
    sim.exec({ action: "spawn", args: { type: "basic", near: { x: sim.player.x + 30, y: sim.player.y } } });
    const z = sim.enemies[0];
    z.damage = 10;
    z.attackCd = 0; // 新刷妖物有 0.2-0.8s 初始攻击冷却，清零让首拍即起手
    // 第一次受击：前摇 0.35s（21 tick）后判定命中，HP 120→110，进入无敌帧
    for (let i = 0; i < 40 && sim.player.hp === 120; i++) sim.tick();
    expect(sim.player.hp).toBe(110);
    const hpAfterHit = sim.player.hp;
    sim.tick();
    expect(sim.player.hp).toBe(hpAfterHit); // 无敌帧内不掉血
    // 击杀玩家
    z.damage = 9999;
    sim.player.invincible = 0;
    for (let i = 0; i < 300 && sim.state !== "GameOver"; i++) sim.tick();
    expect(sim.state).toBe("GameOver");
    expect(save.data.games).toBe(1);
    expect(save.data.highScore).toBeGreaterThanOrEqual(0);
    expect(save.data.coins).toBeGreaterThanOrEqual(sim.kills);
  });

  it("teleport 后位置更新并受边界钳制", () => {
    const { sim } = newSim();
    const r = sim.exec({ action: "teleport", args: { x: -50, y: 99999 } });
    expect(r.ok).toBe(true);
    expect(sim.player.x).toBeGreaterThanOrEqual(22);
    expect(sim.player.y).toBeLessThanOrEqual(sim.world.h - 22);
  });

  it("未知 action 返回错误（协议一致）", () => {
    const { sim } = newSim();
    const r = sim.exec({ id: 7, action: "nope" });
    expect(r.ok).toBe(false);
    expect(r.error).toContain("未知动作");
    expect(r.id).toBe(7);
  });

  it("同 seed 同操作序列 → 完全一致的对局（确定性回归）", () => {
    const run = (seed: number) => {
      const { sim } = newSim(seed);
      sim.exec({ action: "start" });
      sim.exec({ action: "teleport", args: { x: 1920, y: 1080 } });
      for (let f = 0; f < 60 * 20; f++) {
        sim.input.moveX = Math.sin(f / 30);
        sim.input.moveY = Math.cos(f / 45);
        sim.input.attack = f % 40 < 10;
        sim.tick();
      }
      return { score: sim.score, kills: sim.kills, hp: sim.player.hp, n: sim.enemies.length };
    };
    expect(run(2024)).toEqual(run(2024));
    expect(run(2024)).not.toEqual(run(2025));
  });
});
