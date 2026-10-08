// R23 实机强度基线。R227 重钉：出生环屏外化（椭圆 x720/y460）+ Act1 冷却
// 1.3→1.0 补偿路程时间后，vitest 逐种子实测画像：
// - orbit（纯走位不输出）：4/5 种子 ~35-50s 暴毙；个别种子（s19）雪球存活 720s
//   421 杀——纯走位能活属 power-fantasy（与 R23 时代「orbit 个别 720s 不死」同语义）；
// - seek（走位+输出）：部分种子速死，部分雪球化（s15 十阶 2034 杀）；
// - stick（站桩输出不走位）：4/5 种子 52-192s 被磨死（站桩无走位不可行）；
// - afk：20s 暴毙零击杀。
// pins 语义：每策略「必终结种子」锁终结性；seek s15 锁雪球存在性；afk 锁无挂机收益。
import { describe, expect, it } from "vitest";
import { runBot } from "./bot";

describe("R23 实机强度基线（R227 屏外出生重钉）", () => {
  it("对局必终结（无软锁）：三策略 × 可终结种子", () => {
    const seedsByPolicy: Record<string, number[]> = {
      orbit: [13, 16, 18, 20],
      seek: [13],
      stick: [12, 13, 15, 18],
    };
    for (const policy of ["orbit", "seek", "stick"] as const) {
      for (const seed of seedsByPolicy[policy]!) {
        const r = runBot(seed, policy);
        expect(["Victory", "GameOver"]).toContain(r.state);
      }
    }
  }, 180000);

  it("纯走位不输出有代价：orbit 可终结种子暴毙（妖潮压力有效）", () => {
    for (const seed of [13, 16, 18, 20]) {
      const r = runBot(seed, "orbit");
      expect(r.state).toBe("GameOver");
    }
  }, 180000);

  it("站桩无走位有代价：stick 可终结种子被磨死（走位是必需技能）", () => {
    for (const seed of [12, 13, 15, 18]) {
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

  it("雪球存在性：seek s15 720s 高阶高杀（R227 重钉至 s15）", () => {
    const r = runBot(15, "seek");
    expect(r.state).toBe("Combat"); // 720s 不死（power-fantasy 态）
    expect(r.round).toBeGreaterThanOrEqual(8);
    expect(r.kills).toBeGreaterThanOrEqual(1500);
  }, 120000);
});
