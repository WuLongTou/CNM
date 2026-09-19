// M14 操作与战斗：冲刺斩 / 完美闪避（子弹时间 + 集中增伤）回归。
import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { CFG } from "../../src/sim/config";

function newSim(seed = 42) {
  const sim = new GameSim(seed, memoryStorage());
  sim.exec({ action: "start" });
  sim.exec({ action: "give", args: { weapon: "tiejian" } });
  return sim;
}

/** 放一只不死靶子在玩家旁并返回。 */
function spawnDummy(sim: GameSim, dx = 40, dy = 0) {
  sim.exec({ action: "spawn", args: { type: 0, near: { x: sim.player.x + dx, y: sim.player.y + dy } } });
  const z = sim.enemies[sim.enemies.length - 1];
  z.hp = 1e9;
  return z;
}

describe("冲刺斩", () => {
  it("冲刺中攻击伤害 1.5×（同种子同靶对照）", () => {
    const dmgOnce = (dash: boolean) => {
      const sim = newSim();
      const z = spawnDummy(sim);
      const hp0 = z.hp;
      if (dash) {
        sim.input = { moveX: 0, moveY: 0, attack: false, dash: true, cycleTarget: false };
        sim.tick(); // 进入冲刺窗口
      }
      sim.input = { moveX: 0, moveY: 0, attack: true, dash: false, cycleTarget: false };
      sim.tick(); // 第一击（冲刺中 → 冲刺斩）
      return hp0 - z.hp;
    };
    const base = dmgOnce(false);
    const strike = dmgOnce(true);
    expect(base).toBeGreaterThan(0);
    expect(strike).toBeCloseTo(base * CFG.Combat.DashStrikeDmgMult, 0);
  });

  it("冲刺斩近战范围 1.35×：横排双靶只有冲刺斩能同时命中（multi=1）", () => {
    const hits = (dash: boolean) => {
      const sim = newSim();
      spawnDummy(sim, 40, 0);
      spawnDummy(sim, -62, 0); // 62px：普通范围 70 内?——用 90 确保超出普通、落在 70×1.35≈94 内
      // 重摆第二只到 90px
      const far = sim.enemies[sim.enemies.length - 1];
      far.x = sim.player.x - 90;
      far.hp = 1e9;
      if (dash) {
        sim.input = { moveX: 0, moveY: 0, attack: false, dash: true, cycleTarget: false };
        sim.tick();
      }
      const c0 = sim.player.combo;
      sim.input = { moveX: 0, moveY: 0, attack: true, dash: false, cycleTarget: false };
      sim.tick();
      return sim.player.combo - c0;
    };
    expect(hits(false)).toBe(1); // 普通攻击只够到近靶
    expect(hits(true)).toBeGreaterThanOrEqual(2); // 冲刺斩扫到远靶
  });
});

describe("完美闪避", () => {
  /** 造一只贴脸敌人并让其在下一 tick 攻击玩家。 */
  function setupThreat(sim: GameSim) {
    const z = spawnDummy(sim, 0, -40);
    sim.player.hp = 120;
    return z;
  }

  it("冲刺中受近战攻击：免伤 + 子弹时间 + 集中窗口", () => {
    const sim = newSim();
    setupThreat(sim);
    sim.input = { moveX: 0, moveY: 0, attack: false, dash: true, cycleTarget: false };
    sim.tick(); // 进入冲刺窗口（0.25s）
    const hp0 = sim.player.hp;
    const events: string[] = [];
    sim.onEvent((e) => events.push(e.event));
    // 快进到敌人攻击冷却就绪并接触判定
    for (let i = 0; i < 30 && events.indexOf("perfect_dodge") < 0; i++) sim.tick();
    expect(events).toContain("perfect_dodge");
    expect(sim.player.hp).toBe(hp0); // 免伤
    expect(sim.slowmoTimer).toBeGreaterThan(0);
    expect(sim.focusTimer).toBeGreaterThan(0);
  });

  it("子弹时间内敌人移动慢放", () => {
    const sim = newSim();
    setupThreat(sim);
    sim.input = { moveX: 0, moveY: 0, attack: false, dash: true, cycleTarget: false };
    sim.tick();
    sim.slowmoTimer = CFG.Combat.DodgeSlowTime; // 强制进入子弹时间
    const z = sim.enemies[sim.enemies.length - 1];
    const x0 = z.x;
    for (let i = 0; i < 30; i++) sim.tick();
    const movedSlow = Math.abs(z.x - x0);
    // 对照：无子弹时间跑同样时长
    sim.slowmoTimer = 0;
    const x1 = z.x;
    for (let i = 0; i < 30; i++) sim.tick();
    const movedNormal = Math.abs(z.x - x1);
    expect(movedSlow).toBeGreaterThan(0);
    expect(movedNormal).toBeGreaterThan(movedSlow * (1 / CFG.Combat.DodgeSlowFactor) * 0.8);
  });

  it("集中窗口内伤害 1.3×，窗口结束恢复", () => {
    const sim = newSim();
    const z = spawnDummy(sim);
    const hitOnce = () => {
      z.x = sim.player.x + 40; z.y = sim.player.y; // 按回位（防分离力推走）
      const hp0 = z.hp;
      sim.input = { moveX: 0, moveY: 0, attack: true, dash: false, cycleTarget: false };
      sim.tick();
      sim.input = { moveX: 0, moveY: 0, attack: false, dash: false, cycleTarget: false };
      for (let i = 0; i < 40; i++) sim.tick(); // 等攻击冷却结束供下次用
      return hp0 - z.hp;
    };
    // 手动进入集中窗口（跳过闪避流程直接验证倍率）；两次都清零连击以对齐基准
    sim.focusTimer = CFG.Combat.FocusTime;
    const focused = hitOnce();
    sim.focusTimer = 0;
    sim.player.combo = 0;
    sim.player.comboTimer = 0;
    const normal = hitOnce();
    expect(focused).toBeCloseTo(normal * CFG.Combat.FocusDmgMult, 0);
  });

  it("非冲刺状态受击正常掉血（回归）", () => {
    const sim = newSim();
    setupThreat(sim);
    sim.player.invincible = 0;
    const hp0 = sim.player.hp;
    // 敌人贴近后攻击
    for (let i = 0; i < 60 && sim.player.hp === hp0; i++) sim.tick();
    expect(sim.player.hp).toBeLessThan(hp0);
    expect(sim.slowmoTimer).toBe(0); // 未触发闪避
  });
});
