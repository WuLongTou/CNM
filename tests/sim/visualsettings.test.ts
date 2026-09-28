// R61 表现设置偏好：读写往返 / 损坏回退 / 缺字段补默认。
// 模块在 src/app/settings.ts（纯数据 + 持久化策略，零 DOM 依赖），
// 按项目惯例放 tests/sim 下跑 vitest（存储注入内存版，不碰真 localStorage）。

import { describe, expect, it } from "vitest";
import {
  VISUAL_SETTINGS_KEY,
  defaultVisualSettings,
  loadVisualSettings,
  saveVisualSettings,
  type KVStorage,
} from "../../src/app/settings";

function memStorage(initial: Record<string, string> = {}): KVStorage & { store: Record<string, string> } {
  const store: Record<string, string> = { ...initial };
  return {
    store,
    getItem: (k) => (k in store ? store[k]! : null),
    setItem: (k, v) => { store[k] = v; },
  };
}

describe("VisualSettings 偏好", () => {
  it("默认全开（表现不打折，无障碍按需关闭）", () => {
    expect(defaultVisualSettings()).toEqual({ shake: true, damageNumbers: true, hitstop: true });
  });

  it("无存储 / 空 key 时返回默认", () => {
    expect(loadVisualSettings(memStorage())).toEqual(defaultVisualSettings());
  });

  it("损坏 JSON 回退默认且不抛异常", () => {
    const s = memStorage({ [VISUAL_SETTINGS_KEY]: "{{{not-json" });
    expect(loadVisualSettings(s)).toEqual(defaultVisualSettings());
  });

  it("部分字段缺失时缺省回默认、已有值保留", () => {
    const s = memStorage({ [VISUAL_SETTINGS_KEY]: JSON.stringify({ shake: false }) });
    expect(loadVisualSettings(s)).toEqual({ shake: false, damageNumbers: true, hitstop: true });
  });

  it("字段类型不对（如字符串）按缺省处理", () => {
    const s = memStorage({ [VISUAL_SETTINGS_KEY]: JSON.stringify({ hitstop: "yes" }) });
    expect(loadVisualSettings(s).hitstop).toBe(true);
  });

  it("保存后读取往返一致（关→存→读保持关）", () => {
    const s = memStorage();
    const v = { shake: false, damageNumbers: false, hitstop: false };
    saveVisualSettings(s, v);
    expect(loadVisualSettings(s)).toEqual(v);
    expect(JSON.parse(s.store[VISUAL_SETTINGS_KEY]!)).toEqual(v);
  });

  it("多次写覆盖旧值（最后一次生效）", () => {
    const s = memStorage();
    saveVisualSettings(s, { shake: false, damageNumbers: true, hitstop: true });
    saveVisualSettings(s, { shake: true, damageNumbers: false, hitstop: true });
    expect(loadVisualSettings(s)).toEqual({ shake: true, damageNumbers: false, hitstop: true });
  });
});
