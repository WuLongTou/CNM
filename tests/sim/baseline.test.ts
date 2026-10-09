// R23 实机强度基线。R228 重钉：簇拥地形（8 簇林带/岩阵/遗迹）改变战斗几何——
// bot 无绕障寻路，会被林带挡住，行为画像再度变化，逐种子 vitest 实测重扫：
// - orbit（纯走位不输出）：s16/s20 暴毙；s13/s18 雪球存活（走位能活属 power-fantasy）；
// - seek（走位+输出）：s13/s15 快速暴毙（地形挡路放大走位失误的代价）；
// - stick（站桩）：s13/s15 暴毙、s12/s18 雪球；
// - afk：20s 暴毙零击杀。
// pins 语义不变：每策略「必终结种子」锁终结性；orbit s13 锁雪球存在性；afk 锁无挂机收益。
import { describe, expect, it } from "vitest";
import { runBot } from "./bot";

describe("R23 实机强度基线（R229 祭坛象限重钉）", () => {
  it("对局必终结（无软锁）：三策略 × 可终结种子", () => {
    const seedsByPolicy: Record<string, number[]> = {
      orbit: [13, 20],
      seek: [11, 13, 15],
      stick: [12, 15],
    };
    for (const policy of ["orbit", "seek", "stick"] as const) {
      for (const seed of seedsByPolicy[policy]!) {
        const r = runBot(seed, policy);
        expect(["Victory", "GameOver"]).toContain(r.state);
      }
    }
  }, 180000);

  it("纯走位不输出有代价：orbit 可终结种子暴毙（妖潮压力有效）", () => {
    for (const seed of [13, 20]) {
      const r = runBot(seed, "orbit");
      expect(r.state).toBe("GameOver");
    }
  }, 180000);

  it("站桩无走位有代价：stick 可终结种子被磨死（走位是必需技能）", () => {
    for (const seed of [12, 15]) {
      const r = runBot(seed, "stick");
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

  it("雪球存在性：orbit s18 720s 高阶高杀（R229 重钉）", () => {
    const r = runBot(18, "orbit");
    expect(r.state).toBe("Combat"); // 720s 不死（power-fantasy 态）
    expect(r.round).toBeGreaterThanOrEqual(6);
    expect(r.kills).toBeGreaterThanOrEqual(600);
  }, 120000);
});
