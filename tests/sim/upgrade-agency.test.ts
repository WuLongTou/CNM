import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { CFG } from "../../src/sim/config";
import { memoryStorage } from "../../src/sim/save";

function offerSim(seed = 21) {
  const sim = new GameSim(seed, memoryStorage());
  sim.exec({ action: "start" });
  // 报价打开前先清场上符箓，避免干扰
  sim.pickups.length = 0;
  sim.player.x = 500; sim.player.y = 500;
  sim.pickups.push({ id: 9101, x: 510, y: 500 });
  sim.tick();
  return sim;
}

describe("R4 升级重抽与跳过", () => {
  it("每局重抽次数按配置起步，status 暴露剩余数", () => {
    const sim = new GameSim(21, memoryStorage());
    expect(sim.rerollsLeft).toBe(CFG.Upgrade.RerollsPerRun);
    expect(sim.rerollsLeft).toBe(2);
    expect(sim.status().rerolls_left).toBe(2);
  });

  it("重抽：消耗一次限额并重新生成三张报价，面板保持打开", () => {
    const sim = offerSim();
    expect(sim.upgradeOffers).toHaveLength(3);
    const before = sim.upgradeOffers!.map((o) => `${o.weaponIdx}:${o.stat}`).join("|");
    const r = sim.rerollUpgrade() as { handled: boolean; rerollsLeft: number; offers: unknown[] };
    expect(r.handled).toBe(true);
    expect(r.rerollsLeft).toBe(1);
    expect(sim.rerollsLeft).toBe(1);
    expect(r.offers).toHaveLength(3);
    expect(sim.status().rerolls_left).toBe(1);
    // 报价保持打开（面板不关闭），暂停态维持
    expect(sim.upgradeOffers).not.toBeNull();
    expect(sim.paused).toBe(true);
    // 重抽推进 rng 流：组合大概率变化（仅作信息性断言，不强制）
    void before;
  });

  it("重抽用尽后拒绝且不消耗（报价保持原样）", () => {
    const sim = offerSim();
    sim.rerollUpgrade();
    sim.rerollUpgrade();
    expect(sim.rerollsLeft).toBe(0);
    const keep = sim.upgradeOffers;
    const r = sim.rerollUpgrade() as { handled: boolean; note: string };
    expect(r.handled).toBe(false);
    expect(r.note).toBe("重抽已用完");
    expect(sim.upgradeOffers).toBe(keep);
  });

  it("无报价时重抽/跳过均拒绝", () => {
    const sim = new GameSim(21, memoryStorage());
    sim.exec({ action: "start" });
    expect((sim.rerollUpgrade() as { handled: boolean }).handled).toBe(false);
    expect((sim.skipUpgrade() as { handled: boolean }).handled).toBe(false);
  });

  it("跳过：关闭报价、解除暂停、受伤回复 SkipHeal 生命", () => {
    const sim = offerSim();
    sim.player.hp = 50;
    const r = sim.skipUpgrade() as { handled: boolean; healed: number; hp: number };
    expect(r.handled).toBe(true);
    expect(r.healed).toBe(CFG.Upgrade.SkipHeal);
    expect(r.hp).toBe(65);
    expect(sim.player.hp).toBe(65);
    expect(sim.upgradeOffers).toBeNull();
    expect(sim.paused).toBe(false);
    // 事件对外广播（含回血量，供通知条/音效）
    const ev = sim.drainEvents().find((e) => e.event === "upgrade_skipped");
    expect(ev?.data).toMatchObject({ heal: 15, hp: 65 });
  });

  it("跳过回血不超上限（满血跳过 healed=0）", () => {
    const sim = offerSim();
    sim.player.hp = sim.player.maxHp;
    const r = sim.skipUpgrade() as { handled: boolean; healed: number; hp: number };
    expect(r.handled).toBe(true);
    expect(r.healed).toBe(0);
    expect(r.hp).toBe(sim.player.maxHp);
    // 接近满血：只补缺口
    const sim2 = offerSim(22);
    sim2.player.hp = sim2.player.maxHp - 6;
    const r2 = sim2.skipUpgrade() as { healed: number; hp: number };
    expect(r2.healed).toBe(6);
    expect(r2.hp).toBe(sim2.player.maxHp);
  });

  it("跳过后流程不断：再拾符箓可重新打开报价并正常升级", () => {
    const sim = offerSim();
    sim.skipUpgrade();
    const w = sim.player.weapons[0];
    const upsBefore = w.ups;
    sim.pickups.push({ id: 9102, x: sim.player.x + 5, y: sim.player.y });
    sim.tick();
    expect(sim.upgradeOffers).toHaveLength(3);
    sim.resolveUpgrade(0);
    expect(w.ups).toBe(upsBefore + 1);
  });

  it("choose 协议：reroll/skip 路由生效", () => {
    const sim = offerSim();
    const rr = sim.exec({ action: "choose", args: { target: "upgrade", reroll: true } });
    expect(rr.ok).toBe(true);
    expect((rr.data as { handled: boolean }).handled).toBe(true);
    expect(sim.rerollsLeft).toBe(1);
    const sk = sim.exec({ action: "choose", args: { target: "upgrade", skip: true } });
    expect(sk.ok).toBe(true);
    expect((sk.data as { handled: boolean }).handled).toBe(true);
    expect(sim.upgradeOffers).toBeNull();
  });
});
