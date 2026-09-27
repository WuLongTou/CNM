import { describe, expect, it } from "vitest";
import { weatherEffectTip, type WeatherState } from "../../src/sim/weather";

function w(type: WeatherState["type"], eliteBonus = 0): WeatherState {
  return { type, intensity: "Light", enemySpeedMult: 1, playerSpeedMult: 1, eliteBonus };
}

describe("R54 天气影响提示", () => {
  it("四类天气皆有专属文案", () => {
    const tips = [w("Clear"), w("Rain"), w("Fog", 0.02), w("BloodMoon", 0.1)].map(weatherEffectTip);
    for (const t of tips) expect(t.length).toBeGreaterThan(0);
    expect(new Set(tips).size).toBe(4); // 四类互不重复
  });

  it("文案与数值表一致（雨 15%/8%，血月 15%，精英率百分比派生）", () => {
    expect(weatherEffectTip(w("Rain"))).toContain("15%");
    expect(weatherEffectTip(w("Rain"))).toContain("8%");
    expect(weatherEffectTip(w("BloodMoon", 0.1))).toContain("15%");
    expect(weatherEffectTip(w("BloodMoon", 0.1))).toContain("+10%");
    expect(weatherEffectTip(w("Fog", 0.02))).toContain("+2%");
  });

  it("精英率按入参派生（Moderate 翻倍后文案同步）", () => {
    expect(weatherEffectTip(w("BloodMoon", 0.15))).toContain("+15%");
  });
});
