import { describe, expect, it } from "vitest";
import { GameSim, type UpgradeOffer } from "../../src/sim/game";
import { CFG } from "../../src/sim/config";
import { memoryStorage } from "../../src/sim/save";

function offerSim(seed = 51) {
  const sim = new GameSim(seed, memoryStorage());
  sim.exec({ action: "start" });
  sim.pickups.length = 0;
  sim.player.x = 500; sim.player.y = 500;
  sim.pickups.push({ id: 9401, x: 510, y: 500 });
  sim.tick();
  return sim;
}

/** 私有发牌直调（穷举验证放逐排除；onEnemyKilled 式私有访问先例见 progression.test.ts）。 */
function dealMany(sim: GameSim, n: number): UpgradeOffer[][] {
  const inner = sim as unknown as { makeUpgradeOffers(): UpgradeOffer[] | null };
  const out: UpgradeOffer[][] = [];
  for (let i = 0; i < n; i++) {
    const pool = inner.makeUpgradeOffers();
    if (pool) out.push(pool);
  }
  return out;
}

function comboKey(sim: GameSim, o: UpgradeOffer): string {
  return sim.player.weapons[o.weaponIdx].defId + ":" + o.stat;
}

describe("R7 升级放逐", () => {
  it("每局放逐 1 次，status 暴露剩余数", () => {
    const sim = new GameSim(51, memoryStorage());
    expect(sim.banishesLeft).toBe(CFG.Upgrade.BanishesPerRun);
    expect(sim.banishesLeft).toBe(1);
    expect(sim.status().banishes_left).toBe(1);
  });

  it("放逐：移除组合记入集合，限额-1，重补一张保持三卡", () => {
    const sim = offerSim();
    expect(sim.upgradeOffers).toHaveLength(3);
    const target = sim.upgradeOffers![0];
    const bannedKey = comboKey(sim, target);
    const r = sim.banishUpgrade(0) as {
      handled: boolean; banishesLeft: number; banned: string; offers: UpgradeOffer[];
    };
    expect(r.handled).toBe(true);
    expect(r.banishesLeft).toBe(0);
    expect(r.banned).toBe(bannedKey);
    expect(sim.bannedCombos.has(bannedKey)).toBe(true);
    expect(sim.upgradeOffers).toHaveLength(3);
    expect(sim.upgradeOffers![0]).not.toBe(target);
    expect(sim.paused).toBe(true);
    const ev = sim.drainEvents().find((e) => e.event === "upgrade_offer"
      && (e.data as { banished?: boolean }).banished);
    expect(ev?.data).toMatchObject({ name: target.weaponName, stat: target.stat });
  });

  it("被放逐组合此后绝不再出现（50 次发牌穷举）", () => {
    const sim = offerSim();
    const target = sim.upgradeOffers![1];
    const bannedKey = comboKey(sim, target);
    sim.banishUpgrade(1);
    const deals = dealMany(sim, 50);
    expect(deals.length).toBeGreaterThan(0);
    for (const pool of deals) {
      for (const o of pool) {
        expect(comboKey(sim, o)).not.toBe(bannedKey);
      }
    }
  });

  it("放逐用尽/无报价/无效序号均拒绝", () => {
    const sim = offerSim();
    sim.banishUpgrade(0);
    const r = sim.banishUpgrade(1) as { handled: boolean; note: string };
    expect(r.handled).toBe(false);
    expect(r.note).toBe("放逐已用完");
    const bad = sim.banishUpgrade(9) as { handled: boolean };
    expect(bad.handled).toBe(false);
    const sim2 = new GameSim(51, memoryStorage());
    sim2.exec({ action: "start" });
    expect((sim2.banishUpgrade(0) as { handled: boolean }).handled).toBe(false);
  });

  it("放逐后仍可正常选择剩余卡升级", () => {
    const sim = offerSim();
    sim.banishUpgrade(2);
    const idx = sim.upgradeOffers![0].weaponIdx;
    const before = sim.player.weapons[idx].ups;
    const r = sim.resolveUpgrade(0);
    expect(r).toMatchObject({ handled: true });
    expect(sim.player.weapons[idx].ups).toBe(before + 1);
  });

  it("choose 协议：banish 路由生效", () => {
    const sim = offerSim();
    const r = sim.exec({ action: "choose", args: { target: "upgrade", banish: 0 } });
    expect(r.ok).toBe(true);
    expect((r.data as { handled: boolean }).handled).toBe(true);
    expect(sim.banishesLeft).toBe(0);
    expect(sim.status().banishes_left).toBe(0);
  });

  it("放逐不消耗重抽次数（两者独立限额）", () => {
    const sim = offerSim();
    sim.banishUpgrade(0);
    expect(sim.rerollsLeft).toBe(CFG.Upgrade.RerollsPerRun);
    sim.rerollUpgrade();
    expect(sim.rerollsLeft).toBe(CFG.Upgrade.RerollsPerRun - 1);
    expect(sim.banishesLeft).toBe(0);
  });
});
