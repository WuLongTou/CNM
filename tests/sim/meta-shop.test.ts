import { describe, expect, it } from "vitest";
import { GameSim, META_SHOP, metaPrice } from "../../src/sim/game";
import { CFG } from "../../src/sim/config";
import { defaultSave, memoryStorage, type SaveData } from "../../src/sim/save";

function richSave(coins = 500) {
  const save = memoryStorage();
  save.save({ ...defaultSave(), coins });
  return save;
}

/** 杀死玩家走完死亡结算（GameOver → finalizeRun）。 */
function killPlayer(sim: GameSim) {
  sim.player.hp = 1;
  sim.player.invincible = 0;
  sim.exec({ action: "spawn", args: { type: "basic", near: { x: sim.player.x + 10, y: sim.player.y }, count: 1 } });
  sim.enemies[0].damage = 9999;
  for (let i = 0; i < 600 && sim.state !== "GameOver"; i++) sim.tick();
  expect(sim.state).toBe("GameOver");
}

describe("R5 永久升级商店（6 维）", () => {
  it("商店表 6 件齐备，价格公式 50×(lv+1)", () => {
    expect(Object.keys(META_SHOP)).toEqual(["hp", "dmg", "spd", "mag", "greed", "regen"]);
    expect(metaPrice(0)).toBe(50);
    expect(metaPrice(3)).toBe(200);
    for (const def of Object.values(META_SHOP)) {
      expect(def.name.length).toBeGreaterThan(0);
      expect(def.desc.length).toBeGreaterThan(0);
    }
  });

  it("默认全 0：移速/生命/伤害取基线，无回复", () => {
    const sim = new GameSim(31, memoryStorage());
    expect(sim.player.speed).toBe(CFG.Player.BaseSpeed);
    expect(sim.player.maxHp).toBe(CFG.Player.MaxHealth);
    expect(sim.player.baseDamage).toBe(CFG.Player.BaseDamage);
    sim.exec({ action: "start" });
    sim.player.hp = 50;
    sim.enemies.length = 0;
    sim.player.invincible = 1e9;
    sim.tick();
    expect(sim.player.hp).toBe(50);
  });

  it("六种购买：扣费升级、价格爬升、金币不足拒绝", () => {
    const save = richSave(1000);
    const sim = new GameSim(31, save);
    for (const kind of Object.keys(META_SHOP) as (keyof typeof META_SHOP)[]) {
      const r = sim.exec({ action: "buy_meta", args: { kind } }).data as {
        bought: boolean; level: number; coins: number;
      };
      expect(r.bought).toBe(true);
      expect(r.level).toBe(1);
    }
    expect(save.data.coins).toBe(1000 - 50 * 6);
    expect(save.data.metaSpd).toBe(1);
    expect(save.data.metaRegen).toBe(1);
    // 二次购买价格翻倍级（50×2）
    const r2 = sim.exec({ action: "buy_meta", args: { kind: "spd" } }).data as {
      bought: boolean; level: number;
    };
    expect(r2.bought).toBe(true);
    expect(r2.level).toBe(2);
    expect(save.data.coins).toBe(1000 - 50 * 6 - 100);
    // 金币不足
    save.save({ ...save.data, coins: 10 });
    const poor = sim.exec({ action: "buy_meta", args: { kind: "regen" } }).data as {
      bought: boolean; reason: string;
    };
    expect(poor.bought).toBe(false);
    expect(poor.reason).toBe("金币不足");
  });

  it("未知 kind 回退 hp（旧调用兼容）", () => {
    const save = richSave(100);
    const sim = new GameSim(31, save);
    const r = sim.exec({ action: "buy_meta", args: { kind: "nope" } }).data as {
      bought: boolean; kind: string; level: number;
    };
    expect(r.bought).toBe(true);
    expect(r.kind).toBe("hp");
    expect(r.level).toBe(1);
  });

  it("疾风：移速 +4%/级（构造期生效）", () => {
    const save = memoryStorage();
    save.save({ ...defaultSave(), metaSpd: 2 });
    const sim = new GameSim(31, save);
    expect(sim.player.speed).toBeCloseTo(CFG.Player.BaseSpeed * 1.08, 6);
  });

  it("聚灵：拾取半径 +12%/级（基线外可拾取符箓）", () => {
    const mk = (mag: number) => {
      const save = memoryStorage();
      save.save({ ...defaultSave(), metaMag: mag });
      const sim = new GameSim(31, save);
      sim.exec({ action: "start" });
      sim.player.x = 500; sim.player.y = 500;
      // 距离 45：基线半径 42 之外，聚灵 1 级 47.04 之内
      sim.pickups.push({ id: 9301, x: 545, y: 500 });
      sim.tick();
      return sim.upgradeOffers !== null;
    };
    expect(mk(0)).toBe(false);
    expect(mk(1)).toBe(true);
  });

  it("回春：+0.4/s/级持续回复（不超上限）", () => {
    const save = memoryStorage();
    save.save({ ...defaultSave(), metaRegen: 1 });
    const sim = new GameSim(31, save);
    sim.exec({ action: "start" });
    sim.player.hp = 50;
    sim.enemies.length = 0;
    sim.player.invincible = 1e9; // 排除受伤干扰，只测回复
    for (let i = 0; i < 120; i++) sim.tick(); // 2 秒
    expect(sim.player.hp).toBeCloseTo(50.8, 1);
    // 封顶：缺口 0.1，30 帧可回 0.2 → 钳在上限
    sim.player.hp = sim.player.maxHp - 0.1;
    for (let i = 0; i < 30; i++) sim.tick();
    expect(sim.player.hp).toBe(sim.player.maxHp);
  });

  it("点金：结算金币 +10%/级；新字段结算后保留（防 finalize 抹零）", () => {
    const save = richSave(100);
    const sim = new GameSim(31, save);
    sim.exec({ action: "buy_meta", args: { kind: "spd" } });
    sim.exec({ action: "buy_meta", args: { kind: "greed" } });
    expect(save.data.metaSpd).toBe(1);
    sim.exec({ action: "start" });
    sim.score = 500;
    sim.kills = 10;
    killPlayer(sim);
    // floor((500/50 + 10) × 1 × 1.1) = 22
    expect(save.data.coins).toBe(100 - 50 - 50 + 22);
    expect(save.data.metaSpd).toBe(1);
    expect(save.data.metaGreed).toBe(1);
    expect(save.data.metaMag).toBe(0);
  });

  it("老存档兼容：缺新字段照常用（默认 0），结算后补齐", () => {
    const legacy = {
      highScore: 0, totalKills: 0, games: 0, coins: 100,
      metaHp: 0, metaDmg: 0, achievements: [],
    } as unknown as SaveData;
    const save = memoryStorage();
    save.save(legacy);
    const sim = new GameSim(31, save);
    expect(sim.player.speed).toBe(CFG.Player.BaseSpeed);
    const r = sim.exec({ action: "buy_meta", args: { kind: "mag" } }).data as { bought: boolean };
    expect(r.bought).toBe(true);
    sim.exec({ action: "start" });
    killPlayer(sim);
    expect(save.data.metaMag).toBe(1);
    expect(save.data.metaRegen).toBe(0);
  });

  it("meta 动作返回 6 件商店列表（含等级/价格，购买后同步）", () => {
    const save = richSave(200);
    const sim = new GameSim(31, save);
    const m1 = sim.exec({ action: "meta" }).data as {
      coins: number; metaHp: number;
      shop: Array<{ kind: string; name: string; desc: string; level: number; price: number }>;
    };
    expect(m1.shop).toHaveLength(6);
    expect(m1.shop[0]).toMatchObject({ kind: "hp", level: 0, price: 50 });
    expect(m1.metaHp).toBe(0); // 顶层旧字段保留兼容
    sim.exec({ action: "buy_meta", args: { kind: "regen" } });
    const m2 = sim.exec({ action: "meta" }).data as typeof m1;
    expect(m2.shop.find((s) => s.kind === "regen")).toMatchObject({ level: 1, price: 100 });
    expect(m2.coins).toBe(150);
  });
});
