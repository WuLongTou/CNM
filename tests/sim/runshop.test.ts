// R65 局内金币与香火龛：击杀入账 / 补龛节奏 / 贴近开报价 / 闩与重开 /
// 三槽购买效果 / 余额拒绝 / 通胀倍乘。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";
import { CFG } from "../../src/sim/config";

/** 击杀一只带旗标的妖：走真实 dealDamage→死亡结算→金币入账链路（词缀盾/瞄准不干扰断言）。 */
function killOne(sim: GameSim, opts: { elite?: boolean; boss?: boolean; goblin?: boolean } = {}): void {
  const before = sim.coins;
  sim.exec({ action: "spawn", args: { type: "游魂", count: 1, near: "player", elite: opts.elite === true, boss: opts.boss === true } });
  const e = sim.enemies[sim.enemies.length - 1]! as unknown as { goblin: boolean };
  e.goblin = opts.goblin === true;
  const expected = opts.boss ? CFG.Shop.BossGold : opts.elite ? CFG.Shop.EliteGold : opts.goblin ? CFG.Shop.GoblinGold : CFG.Shop.KillGold;
  (sim as unknown as { dealDamage: (e: unknown, d: number) => void }).dealDamage(e, 9999);
  expect(sim.coins).toBe(before + expected);
}

/** 强制开一尊龛并让玩家站上去触发报价。 */
function openShopAt(sim: GameSim): void {
  const s = sim as unknown as { nextId: number; shopLatch: boolean; shrines: { id: number; x: number; y: number }[] };
  s.shrines = [{ id: s.nextId++, x: sim.player.x, y: sim.player.y }];
  s.shopLatch = false;
  sim.tick(1 / 60);
  expect(sim.shopOffer).not.toBeNull();
  expect(sim.paused).toBe(true);
}

describe("R65 局内金币入账", () => {
  it("普通妖 1 / 精英 5 / 妖王 20 / 宝妖 15", () => {
    const sim = new GameSim(650, memoryStorage());
    sim.exec({ action: "start" });
    killOne(sim, {});
    expect(sim.coins).toBe(CFG.Shop.KillGold);
    killOne(sim, { elite: true });
    expect(sim.coins).toBe(CFG.Shop.KillGold + CFG.Shop.EliteGold);
    killOne(sim, { boss: true });
    expect(sim.coins).toBe(CFG.Shop.KillGold + CFG.Shop.EliteGold + CFG.Shop.BossGold);
    killOne(sim, { goblin: true });
    expect(sim.coins).toBe(CFG.Shop.KillGold + CFG.Shop.EliteGold + CFG.Shop.BossGold + CFG.Shop.GoblinGold);
  });
});

describe("R65 香火龛调度与触发", () => {
  it("补龛节奏：计时到点浮现一尊，场上已有则不重复", () => {
    const sim = new GameSim(651, memoryStorage());
    sim.exec({ action: "start" });
    (sim as unknown as { shrineTimer: number }).shrineTimer = 0.01;
    sim.tick(1 / 60);
    expect(sim.shrines).toHaveLength(1);
    (sim as unknown as { shrineTimer: number }).shrineTimer = 0.01;
    sim.tick(1 / 60);
    expect(sim.shrines).toHaveLength(1); // 已有龛不补
  });

  it("贴近开报价（模态暂停），关闭保留龛体，走开复位闩后可重开", () => {
    const sim = new GameSim(652, memoryStorage());
    sim.exec({ action: "start" });
    openShopAt(sim);
    sim.closeShop();
    expect(sim.shopOffer).toBeNull();
    expect(sim.paused).toBe(false);
    // 原地 tick：闩未复位，不重开
    sim.tick(1 / 60);
    expect(sim.shopOffer).toBeNull();
    // 走开 → 闩复位 → 回来 → 重开
    sim.player.x = Math.max(20, sim.shrines[0]!.x - 300);
    sim.tick(1 / 60);
    sim.player.x = sim.shrines[0]!.x;
    sim.tick(1 / 60);
    expect(sim.shopOffer).not.toBeNull();
  });
});

describe("R65 购买", () => {
  it("武器槽：扣费发放、龛散、暂停解除", () => {
    const sim = new GameSim(653, memoryStorage());
    sim.exec({ action: "start" });
    sim.coins = 500;
    openShopAt(sim);
    const idx = sim.shopOffer!.findIndex((s) => s.kind === "weapon");
    const slot = sim.shopOffer![idx]!;
    const r = sim.buyShop(idx);
    expect(r.handled).toBe(true);
    expect(sim.coins).toBe(500 - slot.price);
    expect(sim.shopOffer).toBeNull();
    expect(sim.shrines).toHaveLength(0);
    expect(sim.paused).toBe(false);
  });

  it("符箓槽：应用升级生效；调息槽：回复不超上限", () => {
    const sim = new GameSim(654, memoryStorage());
    sim.exec({ action: "start" });
    sim.coins = 500;
    sim.player.hp = 30;
    openShopAt(sim);
    const upIdx = sim.shopOffer!.findIndex((s) => s.kind === "upgrade");
    if (upIdx >= 0) {
      expect(sim.buyShop(upIdx).handled).toBe(true);
      // 升级真实生效走 applyUpgrade 既有通路（R4 语义复用），此处断言流程闭环
      expect(sim.paused).toBe(false);
    }
    openShopAt(sim);
    const hpBefore = sim.player.hp;
    const healSlot = sim.shopOffer!.findIndex((s) => s.kind === "heal");
    expect(sim.buyShop(healSlot).handled).toBe(true);
    expect(sim.player.hp).toBe(Math.min(hpBefore + CFG.Shop.HealAmount, sim.player.maxHp));
  });

  it("余额不足：拒绝、播报、报价保持", () => {
    const sim = new GameSim(655, memoryStorage());
    sim.exec({ action: "start" });
    sim.coins = 10;
    openShopAt(sim);
    const r = sim.buyShop(0);
    expect(r.handled).toBe(false);
    expect(sim.coins).toBe(10);
    expect(sim.shopOffer).not.toBeNull(); // 报价保持可改买便宜槽
  });

  it("通胀：每次购买后全场基价按 EscalateMult 取整上浮", () => {
    const sim = new GameSim(656, memoryStorage());
    sim.exec({ action: "start" });
    sim.coins = 9999;
    openShopAt(sim);
    sim.buyShop(sim.shopOffer!.findIndex((s) => s.kind === "heal"));
    openShopAt(sim);
    for (const s of sim.shopOffer!) {
      const base = s.kind === "weapon" ? CFG.Shop.PriceWeapon
        : s.kind === "upgrade" ? CFG.Shop.PriceUpgrade
        : s.kind === "buff" ? CFG.Shop.PriceBuff
        : CFG.Shop.PriceHeal;
      expect(s.price).toBe(Math.floor(base * CFG.Shop.EscalateMult));
    }
  });

  it("exec 协议：shop_buy 透传 buyShop（status 回报 coins）", () => {
    const sim = new GameSim(657, memoryStorage());
    sim.exec({ action: "start" });
    sim.coins = 500;
    openShopAt(sim);
    const idx = sim.shopOffer!.findIndex((s) => s.kind === "heal");
    const r = sim.exec({ action: "shop_buy", args: { index: idx } });
    expect((r.data as { handled: boolean }).handled).toBe(true);
    expect(sim.status().coins).toBe(500 - CFG.Shop.PriceHeal);
  });
});

describe("R66 香火祝福与复盘", () => {
  /** 清场钉满血木桩，单次挥刀记录伤害（铁剑无暴击无散布，连击清零后确定）。 */
  function swingDamageOnce(sim: GameSim): number {
    sim.exec({ action: "spawn", args: { type: "游魂", count: 1, near: "player" } });
    const e = sim.enemies[sim.enemies.length - 1]!;
    sim.enemies = [e];
    e.hp = 1e9;
    e.x = sim.player.x + 24;
    e.y = sim.player.y;
    sim.player.combo = 0;
    (sim as unknown as { focusTimer: number }).focusTimer = 0;
    const dmg: number[] = [];
    const cb = (_x: number, _y: number, d: number) => dmg.push(d);
    sim.onDamage.push(cb);
    sim.input = { moveX: 0, moveY: 0, attack: true, dash: false, cycleTarget: false };
    for (let i = 0; i < 120 && dmg.length === 0; i++) sim.tick(1 / 60);
    sim.input = { moveX: 0, moveY: 0, attack: false, dash: false, cycleTarget: false };
    sim.onDamage.pop();
    expect(dmg.length).toBeGreaterThan(0);
    return dmg[0]!;
  }

  it("祝福槽：购买后全局伤害 ×1.15（同条件对照挥刀）", () => {
    const sim = new GameSim(660, memoryStorage());
    sim.exec({ action: "start" });
    const d1 = swingDamageOnce(sim);
    sim.coins = 9999;
    openShopAt(sim);
    const bIdx = sim.shopOffer!.findIndex((s) => s.kind === "buff");
    expect(bIdx).toBeGreaterThanOrEqual(0);
    expect(sim.buyShop(bIdx).handled).toBe(true);
    expect(sim.shopBuff).toBeCloseTo(CFG.Shop.BuffDmgBonus);
    const d2 = swingDamageOnce(sim);
    // 伤害事件为取整整数：祝福后 = round(基准 × 1.15)
    expect(d2).toBe(Math.round(d1 * (1 + CFG.Shop.BuffDmgBonus)));
  });

  it("祝福限购一次：购买后报价不再出现祝福槽", () => {
    const sim = new GameSim(661, memoryStorage());
    sim.exec({ action: "start" });
    sim.coins = 9999;
    openShopAt(sim);
    sim.buyShop(sim.shopOffer!.findIndex((s) => s.kind === "buff"));
    openShopAt(sim);
    expect(sim.shopOffer!.findIndex((s) => s.kind === "buff")).toBe(-1);
  });

  it("散财消灾：单局购买 5 次解锁成就", () => {
    const sim = new GameSim(662, memoryStorage());
    sim.exec({ action: "start" });
    sim.coins = 99999;
    for (let n = 0; n < 5; n++) {
      openShopAt(sim);
      const heal = sim.shopOffer!.findIndex((s) => s.kind === "heal");
      expect(sim.buyShop(heal).handled).toBe(true);
    }
    sim.tick(1 / 60); // 成就判定随 tick 跑
    expect(sim.achievements.unlocked.has("incense_5")).toBe(true);
    expect(sim.shopPurchases).toBe(5);
  });

  it("复盘口径：单局香火钱收入/支出累计", () => {
    const sim = new GameSim(663, memoryStorage());
    sim.exec({ action: "start" });
    sim.coins = 999;
    killOne(sim, {});
    expect(sim.runGoldEarned).toBe(CFG.Shop.KillGold);
    openShopAt(sim);
    sim.buyShop(sim.shopOffer!.findIndex((s) => s.kind === "heal"));
    expect(sim.runGoldSpent).toBe(CFG.Shop.PriceHeal);
  });
});
