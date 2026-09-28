// R63 手柄震动：效果表 / 开关门控 / 击杀节流 / 执行器容错。
// 模块在 src/app/rumble.ts（结构化执行器 + 注入时钟，零 DOM 依赖）。

import { describe, expect, it } from "vitest";
import {
  buildRumble,
  RUMBLE_FX,
  RUMBLE_KILL_THROTTLE_MS,
  RumbleManager,
  type RumbleActuator,
} from "../../src/app/rumble";

/** 记录调用并可控拒绝的假执行器。 */
function fakeActuator(reject = false): RumbleActuator & { calls: Array<{ type: string; params: unknown }> } {
  const calls: Array<{ type: string; params: unknown }> = [];
  return {
    calls,
    playEffect(type, params) {
      calls.push({ type, params });
      return reject ? Promise.reject(new Error("not supported")) : Promise.resolve(true);
    },
  };
}

describe("buildRumble 效果表", () => {
  it("四类效果齐备且幅度在 0-1", () => {
    for (const [kind, fx] of Object.entries(RUMBLE_FX)) {
      expect(fx.duration).toBeGreaterThan(0);
      expect(fx.strongMagnitude).toBeGreaterThanOrEqual(0);
      expect(fx.strongMagnitude).toBeLessThanOrEqual(1);
      expect(fx.weakMagnitude).toBeGreaterThanOrEqual(0);
      expect(fx.weakMagnitude).toBeLessThanOrEqual(1);
      expect(buildRumble(kind as keyof typeof RUMBLE_FX)).toEqual(fx);
    }
  });

  it("返回表项拷贝：改结果不污染效果表", () => {
    const fx = buildRumble("hurt");
    fx.strongMagnitude = 0;
    expect(RUMBLE_FX.hurt.strongMagnitude).toBe(1.0);
  });

  it("完美闪避为倒置签名（弱马达强于强马达）", () => {
    expect(RUMBLE_FX.dodge.weakMagnitude).toBeGreaterThan(RUMBLE_FX.dodge.strongMagnitude);
    expect(RUMBLE_FX.hurt.strongMagnitude).toBeGreaterThan(RUMBLE_FX.hurt.weakMagnitude);
  });
});

describe("RumbleManager", () => {
  it("开关关闭时不触发", () => {
    const act = fakeActuator();
    const m = new RumbleManager(() => act, () => false);
    m.play("hurt");
    expect(act.calls).toHaveLength(0);
  });

  it("触发时以 dual-rumble 播放表内参数", () => {
    const act = fakeActuator();
    const m = new RumbleManager(() => act, () => true);
    m.play("hurt");
    expect(act.calls).toEqual([{ type: "dual-rumble", params: RUMBLE_FX.hurt }]);
  });

  it("击杀节流：间隔内只触发一次，超过阈值恢复", () => {
    const act = fakeActuator();
    let t = 0;
    const m = new RumbleManager(() => act, () => true, () => t);
    m.play("kill");
    m.play("kill"); // 同刻第二杀 → 节流
    t = RUMBLE_KILL_THROTTLE_MS - 1;
    m.play("kill"); // 阈值内 → 节流
    t = RUMBLE_KILL_THROTTLE_MS;
    m.play("kill"); // 达到阈值 → 触发
    expect(act.calls).toHaveLength(2);
  });

  it("非击杀类型不受节流（受击连击每次都震）", () => {
    const act = fakeActuator();
    let t = 0;
    const m = new RumbleManager(() => act, () => true, () => t);
    m.play("hurt");
    m.play("hurt");
    expect(act.calls).toHaveLength(2);
  });

  it("无手柄/无执行器不抛错", () => {
    const m = new RumbleManager(() => null, () => true);
    expect(() => m.play("boss")).not.toThrow();
  });

  it("playEffect 拒绝被吞掉（旧设备不支持，不产生未处理拒绝）", async () => {
    const act = fakeActuator(true);
    const m = new RumbleManager(() => act, () => true);
    m.play("dodge");
    await new Promise((r) => setTimeout(r, 0)); // 让拒绝落到 .catch
    expect(act.calls).toHaveLength(1); // 调用发出，错误被吞
  });
});
