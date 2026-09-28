// R62 手柄输入映射：死区重标定 / 十字键 / 按键映射 / 脉冲边沿 / 多手柄取先。
// 模块在 src/app/gamepad.ts（结构化最小接口 + 注入轮询，零 DOM 依赖），
// 按项目惯例放 tests/sim 下跑 vitest，注入假手柄快照。

import { describe, expect, it } from "vitest";
import {
  GAMEPAD_DEADZONE,
  GamepadInput,
  GP_BTN,
  stickWithDeadzone,
  type GamepadSnapshot,
} from "../../src/app/gamepad";

/** 造一个标准布局假手柄：axes 两轴 + 17 键（下标集合可按下）。 */
function fakePad(opts: {
  axes?: number[];
  down?: number[];
  connected?: boolean;
} = {}): GamepadSnapshot {
  const down = new Set(opts.down ?? []);
  return {
    connected: opts.connected ?? true,
    axes: opts.axes ?? [0, 0],
    buttons: Array.from({ length: 17 }, (_, i) => ({ pressed: down.has(i) })),
  };
}

function pollOf(pads: (GamepadSnapshot | null)[] | undefined) {
  return new GamepadInput(() => pads).poll();
}

describe("stickWithDeadzone 摇杆死区", () => {
  it("死区内（含正中心）归零", () => {
    expect(stickWithDeadzone(0, 0)).toEqual({ x: 0, y: 0 });
    expect(stickWithDeadzone(GAMEPAD_DEADZONE, 0)).toEqual({ x: 0, y: 0 });
    expect(stickWithDeadzone(0.2, 0)).toEqual({ x: 0, y: 0 });
  });

  it("满偏保持原值", () => {
    expect(stickWithDeadzone(1, 0).x).toBeCloseTo(1);
    expect(stickWithDeadzone(0, -1).y).toBeCloseTo(-1);
  });

  it("死区外线性重标定（0.8 → (0.8-0.25)/0.75）", () => {
    expect(stickWithDeadzone(0.8, 0).x).toBeCloseTo(0.55 / 0.75, 5);
    expect(stickWithDeadzone(-0.5, 0).x).toBeCloseTo(-0.25 / 0.75, 5);
  });

  it("方向保持：对角输入 x/y 等比缩放", () => {
    const r = stickWithDeadzone(0.7, 0.7);
    expect(r.x).toBeCloseTo(r.y, 8);
    expect(r.x).toBeGreaterThan(0);
  });
});

describe("GamepadInput 轮询", () => {
  it("无手柄（undefined / 全 null）按未连接处理，读数全零", () => {
    for (const pads of [undefined, [null, null]]) {
      const r = pollOf(pads);
      expect(r).toEqual({ connected: false, moveX: 0, moveY: 0, attack: false, dash: false, start: false });
    }
  });

  it("getPads 抛错不外泄，按未连接处理", () => {
    const r = new GamepadInput(() => { throw new Error("no api"); }).poll();
    expect(r.connected).toBe(false);
  });

  it("左摇杆读数过死区映射到 moveX/moveY（纯轴向）", () => {
    expect(pollOf([fakePad({ axes: [0.8, 0] })])).toMatchObject({ connected: true, moveX: expect.closeTo(0.7333, 3), moveY: 0 });
    expect(pollOf([fakePad({ axes: [0, -1] })])).toMatchObject({ moveX: 0, moveY: -1 });
    expect(pollOf([fakePad({ axes: [0.1, 0.1] })])).toMatchObject({ moveX: 0, moveY: 0 });
  });

  it("斜轴超单位圆（模长>1）整体归一化，方向保持", () => {
    const r = pollOf([fakePad({ axes: [0.8, -1] })]);
    const len = Math.hypot(0.8, 1); // k = 1/len：向量缩回单位圆
    expect(r.moveX).toBeCloseTo(0.8 / len, 10);
    expect(r.moveY).toBeCloseTo(-1 / len, 10);
  });

  it("十字键移动（±1，与摇杆叠加后钳制）", () => {
    expect(pollOf([fakePad({ down: [GP_BTN.DRight] })])).toMatchObject({ moveX: 1, moveY: 0 });
    expect(pollOf([fakePad({ down: [GP_BTN.DUp] })])).toMatchObject({ moveX: 0, moveY: -1 });
    expect(pollOf([fakePad({ axes: [0.5, 0], down: [GP_BTN.DLeft] })]).moveX).toBeLessThan(0);
  });

  it("A 按住攻击（持续读数，非脉冲）", () => {
    const gi = new GamepadInput(() => [fakePad({ down: [GP_BTN.A] })]);
    expect(gi.poll().attack).toBe(true);
    expect(gi.poll().attack).toBe(true); // 按住第二帧仍为真
  });

  it("RB/RT 冲刺为脉冲：按下首帧触发，按住不重复", () => {
    for (const btn of [GP_BTN.RB, GP_BTN.RT]) {
      let pads = [fakePad({ down: [btn] })];
      const gi = new GamepadInput(() => pads);
      expect(gi.poll().dash).toBe(true);
      expect(gi.poll().dash).toBe(false); // 按住第二帧不触发
      pads = [fakePad()]; // 松开
      gi.poll();
      pads = [fakePad({ down: [btn] })]; // 再按
      expect(gi.poll().dash).toBe(true);
    }
  });

  it("Start 同为边沿触发（main 层接暂停）", () => {
    let pads: (GamepadSnapshot | null)[] = [fakePad({ down: [GP_BTN.Start] })];
    const gi = new GamepadInput(() => pads);
    expect(gi.poll().start).toBe(true);
    expect(gi.poll().start).toBe(false);
    pads = [fakePad()];
    gi.poll();
    pads = [fakePad({ down: [GP_BTN.Start] })];
    expect(gi.poll().start).toBe(true);
  });

  it("断开重连：边沿状态复位，重连瞬间按住的键视为新按下（游戏侧冲刺冷却兜底）", () => {
    let pads: (GamepadSnapshot | null)[] = [fakePad({ down: [GP_BTN.RT] })];
    const gi = new GamepadInput(() => pads);
    expect(gi.poll().dash).toBe(true);
    pads = [null]; // 拔出
    expect(gi.poll().connected).toBe(false);
    pads = [fakePad()]; // 重连（未按键）：无脉冲
    expect(gi.poll().dash).toBe(false);
    pads = [fakePad({ down: [GP_BTN.RT] })]; // 重按：正常触发
    expect(gi.poll().dash).toBe(true);
  });

  it("多手柄取第一个已连接者", () => {
    const r = pollOf([null, fakePad({ down: [GP_BTN.DLeft] }), fakePad({ down: [GP_BTN.DRight] })]);
    expect(r.moveX).toBe(-1);
  });
});
