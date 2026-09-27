import { describe, expect, it } from "vitest";
import { weatherHudLabel } from "../../src/ui/hud";

describe("R55 天气 HUD 标签", () => {
  it("晴天不带效果速记", () => {
    expect(weatherHudLabel("Clear", "Light")).toBe("天气 晴");
  });

  it("烈度后缀：Moderate 追加 ·烈", () => {
    expect(weatherHudLabel("Clear", "Moderate")).toBe("天气 晴·烈");
    expect(weatherHudLabel("Rain", "Moderate")).toBe("天气 雨·烈（妖缓你缓）");
  });

  it("三类异常天气各有速记", () => {
    expect(weatherHudLabel("Rain", "Light")).toBe("天气 雨（妖缓你缓）");
    expect(weatherHudLabel("Fog", "Light")).toBe("天气 雾（视野窄·精英多）");
    expect(weatherHudLabel("BloodMoon", "Light")).toBe("天气 血月（妖迅·精英多）");
  });

  it("未知类型回退原名不加效果", () => {
    expect(weatherHudLabel("Weird", "Light")).toBe("天气 Weird");
  });
});
