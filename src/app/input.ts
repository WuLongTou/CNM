// 键盘输入采样：维护物理按键状态，每帧合成为 sim 的 InputState。
// 键位：WASD/方向键移动、Space 攻击、Shift 冲刺。
// （R1 起 Tab"切目标"移除——方向攻击的吸附锥已取代手动选目标；
//   InputState.cycleTarget 字段保留仅作协议兼容。）
// 外部调试输入（debug input 指令）与触屏控件状态（虚拟摇杆/按钮）与键盘三者叠加，
// 脉冲类（dash）读后即清。
// 硬约束：非战斗态输入由 sim.tick 门控清零，键盘层无需重复判断。

import type { GameSim, InputState } from "../sim/game";

/** 触屏控件状态源（TouchControls 满足此结构，解耦 ui 层类型）。 */
export interface TouchInputSource {
  readonly moveX: number;
  readonly moveY: number;
  readonly attack: boolean;
  consumeDash(): boolean;
}

export class InputManager {
  private keys = new Set<string>();
  /** 外部注入的持续输入（debug API），与键盘叠加。 */
  private external: Partial<InputState> = {};
  private pulseDash = false;
  private disposers: Array<() => void> = [];

  constructor(private sim: GameSim, private touch: TouchInputSource | null = null) {
    const onDown = (e: KeyboardEvent) => {
      // 阻止方向键/空格/Tab 的页面滚动与焦点切换
      if (["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", " ", "Tab"].includes(e.key)) e.preventDefault();
      this.keys.add(e.code);
      if (e.code === "ShiftLeft" || e.code === "ShiftRight") this.pulseDash = true;
    };
    const onUp = (e: KeyboardEvent) => this.keys.delete(e.code);
    const onBlur = () => this.keys.clear();
    window.addEventListener("keydown", onDown);
    window.addEventListener("keyup", onUp);
    window.addEventListener("blur", onBlur);
    this.disposers.push(
      () => window.removeEventListener("keydown", onDown),
      () => window.removeEventListener("keyup", onUp),
      () => window.removeEventListener("blur", onBlur),
    );
  }

  /** 注入外部输入（对齐 ExternalCommandService input 指令的 args）。 */
  applyExternal(args: Record<string, unknown>): void {
    if (typeof args.move_x === "number") this.external.moveX = Math.min(1, Math.max(-1, args.move_x));
    if (typeof args.move_y === "number") this.external.moveY = Math.min(1, Math.max(-1, args.move_y));
    if (typeof args.attack === "boolean") this.external.attack = args.attack;
    if (args.dash === true) this.pulseDash = true;
  }

  /** 注入触屏状态源（装配层在 UI 面板创建后挂载）。 */
  setTouchSource(touch: TouchInputSource): void {
    this.touch = touch;
  }

  /** 重开一局时替换绑定的 sim 实例。 */
  forceSim(sim: GameSim): void {
    this.sim = sim;
  }

  clearExternal(): void {
    this.external = {};
  }

  /** 每帧合成：键盘 + 触屏 + 外部叠加，写入 sim.input。 */
  sample(): void {
    const k = this.keys;
    let x = 0, y = 0;
    if (k.has("KeyA") || k.has("ArrowLeft")) x -= 1;
    if (k.has("KeyD") || k.has("ArrowRight")) x += 1;
    if (k.has("KeyW") || k.has("ArrowUp")) y -= 1;
    if (k.has("KeyS") || k.has("ArrowDown")) y += 1;
    const touch = this.touch;
    const attack = k.has("Space")
      || (touch?.attack ?? false)
      || (this.external.attack ?? false);
    if (touch?.consumeDash()) this.pulseDash = true;
    this.sim.input = {
      moveX: Math.min(1, Math.max(-1, x + (touch?.moveX ?? 0) + (this.external.moveX ?? 0))),
      moveY: Math.min(1, Math.max(-1, y + (touch?.moveY ?? 0) + (this.external.moveY ?? 0))),
      attack,
      dash: this.pulseDash,
      cycleTarget: false,
    };
    this.pulseDash = false;
  }

  destroy(): void {
    for (const d of this.disposers) d();
  }
}
