// 手柄输入（R62）：Gamepad API 第四输入源（键盘/触屏/调试指令之外）。
// 映射（W3C 标准布局）：左摇杆/十字键移动、A 攻击（按住）、RB/RT 冲刺、Start 暂停。
// 采样即轮询（业内惯例）：每帧读 getGamepads()，不依赖连接事件；
// 连接/断开事件只用于 UI 播报（main 层挂）。
// 轮询器注入 getter + 结构化最小接口，纯逻辑可单测（vitest 无 DOM/手柄依赖）。

/** 手柄快照的最小结构（DOM Gamepad 天然满足，测试可注入假对象）。 */
export interface GamepadSnapshot {
  readonly connected: boolean;
  readonly axes: readonly number[];
  readonly buttons: readonly { readonly pressed: boolean }[];
}

/** 一帧手柄读数（已做死区/边沿处理）。 */
export interface GamepadRead {
  readonly connected: boolean;
  readonly moveX: number;
  readonly moveY: number;
  readonly attack: boolean;
  /** 冲刺脉冲：本帧新按下（按住不重复）。 */
  readonly dash: boolean;
  /** Start 本帧新按下（main 层接暂停切换）。 */
  readonly start: boolean;
}

/** 左摇杆径向死区（0-1）：低于此半径视为回中，防摇杆漂移误移动。 */
export const GAMEPAD_DEADZONE = 0.25;

/** 标准布局按钮下标（W3C Gamepad "standard" mapping）。 */
export const GP_BTN = { A: 0, RB: 5, RT: 7, Start: 9, DUp: 12, DDown: 13, DLeft: 14, DRight: 15 } as const;

function pressed(pad: GamepadSnapshot, i: number): boolean {
  return pad.buttons[i]?.pressed === true;
}

/** 摇杆径向死区 + 重标定：死区内归零，死区外线性放大回满程（方向保持）。 */
export function stickWithDeadzone(x: number, y: number): { x: number; y: number } {
  const len = Math.hypot(x, y);
  if (len <= GAMEPAD_DEADZONE) return { x: 0, y: 0 };
  const k = Math.min(1, (len - GAMEPAD_DEADZONE) / (1 - GAMEPAD_DEADZONE)) / len;
  return { x: x * k, y: y * k };
}

const NO_PAD: GamepadRead = { connected: false, moveX: 0, moveY: 0, attack: false, dash: false, start: false };

export class GamepadInput {
  private prevDash = false;
  private prevStart = false;

  constructor(private getPads: () => readonly (GamepadSnapshot | null)[] | undefined) {}

  poll(): GamepadRead {
    let pad: GamepadSnapshot | null | undefined;
    try {
      pad = this.getPads()?.find((p) => p?.connected) ?? null;
    } catch {
      // 怪异环境（getGamepads 抛错）按未连接处理，绝不影响键盘/触屏输入
      this.prevDash = this.prevStart = false;
      return NO_PAD;
    }
    if (!pad) {
      this.prevDash = this.prevStart = false;
      return NO_PAD;
    }
    const stick = stickWithDeadzone(pad.axes[0] ?? 0, pad.axes[1] ?? 0);
    let x = stick.x;
    let y = stick.y;
    if (pressed(pad, GP_BTN.DLeft)) x -= 1;
    if (pressed(pad, GP_BTN.DRight)) x += 1;
    if (pressed(pad, GP_BTN.DUp)) y -= 1;
    if (pressed(pad, GP_BTN.DDown)) y += 1;
    const dash = pressed(pad, GP_BTN.RB) || pressed(pad, GP_BTN.RT);
    const start = pressed(pad, GP_BTN.Start);
    const read: GamepadRead = {
      connected: true,
      moveX: Math.min(1, Math.max(-1, x)),
      moveY: Math.min(1, Math.max(-1, y)),
      attack: pressed(pad, GP_BTN.A),
      dash: dash && !this.prevDash, // 边沿：按住不重复触发冲刺
      start: start && !this.prevStart,
    };
    this.prevDash = dash;
    this.prevStart = start;
    return read;
  }
}
