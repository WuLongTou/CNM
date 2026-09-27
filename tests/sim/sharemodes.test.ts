import { describe, expect, it } from "vitest";
import { shareText } from "../../src/ui/hud";

describe("R52 分享文案带模式", () => {
  const base = {
    score: 100, kills: 10, survivalTime: 60, round: 3,
    roleName: "剑修", difficultyName: "平妖", seed: 20260927, cleared: false,
  };

  it("无模式/无血誓/无档位时文案与旧格式一致", () => {
    expect(shareText(base)).toBe(
      "【斩妖录】陨落 · 分数100 · 击杀10 · 存活60s · 威胁阶3 · 剑修/平妖难度 · 种子20260927",
    );
  });

  it("Boss 连战带档位", () => {
    const t = shareText({ ...base, mode: "Boss 连战", rushTier: 2 });
    expect(t).toContain("【斩妖录】陨落 · Boss 连战");
    expect(t).toContain("平妖难度 · 狂化2档");
  });

  it("每日妖潮标识", () => {
    expect(shareText({ ...base, mode: "每日妖潮" })).toContain("陨落 · 每日妖潮");
  });

  it("血誓誓名入文案", () => {
    const t = shareText({ ...base, pacts: "血战·脆体" });
    expect(t).toContain("平妖难度 · 血誓血战·脆体");
  });

  it("档位 0 不显示", () => {
    const t = shareText({ ...base, mode: "Boss 连战", rushTier: 0 });
    expect(t).not.toContain("狂化");
  });
});
