// R23 实机强度基线 pins（R176 手感 pass 重钉）。
// R176 调参（世界 ×0.75 / 密度 ×1.5 / 敌速 ×1.25 + 阶爬坡）后行为画像：
// - 坏种子（约 7 成）第 1 阶 20-30s 暴毙（bot 无垫步/闪避，真人约 2-5 分钟）；
// - 好种子（约 3 成）雪球化：seek s14 杀 3177 进 13 阶死于终幕；orbit 个别
//   种子 720s 不死（power-fantasy 态，非软锁——玩家持续蒸发妖潮）。
// pins 语义：终结性用「可终结种子」；雪球存在性用 orbit s14（720s 阶 4+ 杀 700+）。
import { describe, expect, it } from "vitest";
import { runBot } from "./bot";

describe("R23 实机强度基线（R176 重钉）", () => {
  it("对局必终结（无软锁）：三策略 × 可终结种子", () => {
    const seedsByPolicy: Record<string, number[]> = {
      orbit: [11, 12, 13, 15, 18],
      seek: [13, 16, 17, 18, 21],
      stick: [12, 13, 14, 17, 22],
    };
    for (const policy of ["orbit", "seek", "stick"] as const) {
      for (const seed of seedsByPolicy[policy]!) {
        const r = runBot(seed, policy);
        expect(["Victory", "GameOver"]).toContain(r.state);
      }
    }
  }, 180000);

  it("风筝种子全部可终结（妖潮压力有效）", () => {
    for (const seed of [12, 13, 15, 18, 20]) {
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

  it("雪球存在性：orbit s14 720s 阶 4+ 杀 700+（R187 保底后重扫）", () => {
    const r = runBot(14, "orbit");
    expect(r.state).toBe("Combat"); // 720s 不死（power-fantasy 态）
    expect(r.round).toBeGreaterThanOrEqual(4);
    expect(r.kills).toBeGreaterThanOrEqual(700);
  }, 120000);
});