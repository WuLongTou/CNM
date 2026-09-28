// R67 音量分离：老单键迁移拆分 / 钳制。纯函数在 src/app/audio.ts（无 DOM 依赖）。

import { describe, expect, it } from "vitest";
import { clampVolume, legacyVolumeSplit } from "../../src/app/audio";

describe("clampVolume 钳制", () => {
  it("越界收口 0-1，范围内原值", () => {
    expect(clampVolume(-0.5)).toBe(0);
    expect(clampVolume(1.5)).toBe(1);
    expect(clampVolume(0.35)).toBe(0.35);
  });

  it("非有限值（NaN/Infinity/NaN 字符串）守卫回 0", () => {
    expect(clampVolume(Number.NaN)).toBe(0);
    expect(clampVolume(Number.POSITIVE_INFINITY)).toBe(0); // 非有限值一律静音（防脏档）
    expect(clampVolume(Number("abc"))).toBe(0);
  });
});

describe("legacyVolumeSplit 老键迁移", () => {
  it("无旧档返回默认（音乐 0.28 / 音效 0.7，等价旧默认听感）", () => {
    expect(legacyVolumeSplit(null)).toEqual({ music: 0.28, sfx: 0.7 });
    expect(legacyVolumeSplit("")).toEqual({ music: 0.28, sfx: 0.7 });
  });

  it("旧主音量拆分：音效取原值、音乐取 ×0.4（听感不变）", () => {
    const r = legacyVolumeSplit("0.7");
    expect(r.sfx).toBe(0.7);
    expect(r.music).toBeCloseTo(0.28, 12); // 0.7×0.4 浮点
    expect(legacyVolumeSplit("1")).toEqual({ music: 0.4, sfx: 1 });
  });

  it("旧值越界/非数字先钳制再拆分", () => {
    expect(legacyVolumeSplit("2")).toEqual({ music: 0.4, sfx: 1 });
    expect(legacyVolumeSplit("abc")).toEqual({ music: 0, sfx: 0 });
  });
});
