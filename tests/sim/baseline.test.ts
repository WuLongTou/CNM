// R23 实机强度基线 pins：bot 度量的可回归断言（21 局 spike 结论固化）。
// 证据摘要（平妖/剑修/固定种子）：AFK 17 秒暴毙；无脑冲脸 30 秒内暴毙；
// 纯环绕风筝存活 300 秒+但从未通关（死于妖王弹幕）；贴身缠斗约 3 分钟 换血 出局；
// 雪球（AoE 武器起飞）存在且能进 10 阶。结论：曲线健康，不做数值调优——
// 退化结果皆源于 bot 技能缺限（不垫步/不定时闪避/不挑目标），盲调伤害真人手感。
import { describe, expect, it } from "vitest";
import { runBot } from "./bot";

describe("R23 实机强度基线", () => {
  it("对局必终结（无软锁）：三策略 × 3 种子", () => {
    for (const policy of ["orbit", "seek", "stick"] as const) {
      for (const seed of [11, 12, 13]) {
        const r = runBot(seed, policy);
        expect(["Victory", "GameOver"]).toContain(r.state);
      }
    }
  }, 180000);

  it("风筝永不通关（妖王 DPS 门禁有效）", () => {
    for (const seed of [11, 12, 13, 14, 15]) {
      const r = runBot(seed, "orbit");
      expect(r.state).toBe("GameOver");
    }
  }, 180000);

  it("AFK 零输入 60 秒内暴毙且零击杀（无挂机收益）", () => {
    for (const seed of [11, 12, 13]) {
      const r = runBot(seed, "afk");
      expect(r.state).toBe("GameOver");
      expect(r.time).toBeLessThan(60);
      expect(r.kills).toBe(0);
    }
  }, 60000);

  it("雪球存在性：缠斗 bot seed15 起飞（回归敏感，改战斗必看）", () => {
    const r = runBot(15, "stick");
    expect(r.kills).toBeGreaterThanOrEqual(300);
    expect(r.round).toBeGreaterThanOrEqual(5);
  }, 60000);
});
