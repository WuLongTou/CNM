// 触屏控件：左下虚拟摇杆（移动）、右下攻击/冲刺按钮、HUD 下 UI 按钮排。
// 仅在触屏设备（coarse 指针 / maxTouchPoints>0）或 ?touch=1 时挂载，?touch=0 可强制关闭。
// 输出归一化状态供 InputManager 叠加；UI 按钮复用键盘热键同一组动作函数
// （模态互斥逻辑在装配层），模态打开时游戏输入由 sim.tick 门控清零（硬约束沿用）。
// 本层不 import sim/app，保持 ui 层单向依赖。

export interface TouchActions {
  openEquipment(): void;
  openCrafting(): void;
  toggleHelp(): void;
  togglePause(): void;
}

/** 是否按触屏设备处理（允许 URL 参数强制覆盖，供调试与 E2E）。 */
export function shouldUseTouch(): boolean {
  const q = new URLSearchParams(location.search).get("touch");
  if (q === "1") return true;
  if (q === "0") return false;
  return (
    typeof matchMedia === "function" && matchMedia("(pointer: coarse)").matches ||
    navigator.maxTouchPoints > 0 ||
    "ontouchstart" in window
  );
}

/** 真实触点才可 setPointerCapture；合成事件（自动化测试）无活跃指针会抛错，静默降级。 */
function capture(el: HTMLElement, pointerId: number): void {
  try { el.setPointerCapture(pointerId); } catch { /* 降级：move 监听仍在元素上 */ }
}

export class TouchControls {
  /** 归一化移动向量（摇杆），每帧被 InputManager 读取。 */
  moveX = 0;
  moveY = 0;
  /** 攻击按钮按住状态。 */
  attack = false;
  private dashQueued = false;

  constructor(actions: TouchActions) {
    const root = document.createElement("div");
    root.id = "touch-root";
    root.innerHTML = `
      <div id="joy-base"><div id="joy-knob"></div></div>
      <div id="touch-btns">
        <button id="tbtn-dash" type="button">冲</button>
        <button id="tbtn-attack" type="button">击</button>
      </div>
      <div id="touch-ui">
        <button id="tbtn-equip" type="button">装</button>
        <button id="tbtn-craft" type="button">合</button>
        <button id="tbtn-help" type="button">册</button>
        <button id="tbtn-pause" type="button">停</button>
      </div>`;
    document.getElementById("ui-root")!.appendChild(root);
    this.bindJoystick(root);
    this.bindButtons(root, actions);
  }

  /** 冲刺为脉冲输入：读取即消费（与键盘 Shift / debug dash 同语义）。 */
  consumeDash(): boolean {
    const d = this.dashQueued;
    this.dashQueued = false;
    return d;
  }

  private bindJoystick(root: HTMLElement): void {
    const base = root.querySelector<HTMLElement>("#joy-base")!;
    const knob = root.querySelector<HTMLElement>("#joy-knob")!;
    let activeId = -1;

    const apply = (e: PointerEvent): void => {
      const r = base.getBoundingClientRect();
      const cx = r.left + r.width / 2;
      const cy = r.top + r.height / 2;
      const radius = r.width / 2;
      let dx = (e.clientX - cx) / radius;
      let dy = (e.clientY - cy) / radius;
      const len = Math.hypot(dx, dy);
      // 死区 0.18：指尖轻触不产生漂移；超出底座按单位向量截断
      if (len < 0.18) {
        dx = 0; dy = 0;
      } else if (len > 1) {
        dx /= len; dy /= len;
      }
      this.moveX = dx;
      this.moveY = dy;
      const clamp = Math.min(len, 1) * (radius - knob.offsetWidth / 2);
      const ang = Math.atan2(dy, dx);
      knob.style.transform =
        len < 0.18 ? "translate(0,0)"
          : `translate(${Math.cos(ang) * clamp}px, ${Math.sin(ang) * clamp}px)`;
    };

    const reset = (): void => {
      activeId = -1;
      this.moveX = 0;
      this.moveY = 0;
      knob.style.transform = "translate(0,0)";
      base.classList.remove("active");
    };

    base.addEventListener("pointerdown", (e) => {
      if (activeId !== -1) return;
      activeId = e.pointerId;
      capture(base, e.pointerId);
      base.classList.add("active");
      apply(e);
    });
    base.addEventListener("pointermove", (e) => {
      if (e.pointerId === activeId) apply(e);
    });
    base.addEventListener("pointerup", (e) => { if (e.pointerId === activeId) reset(); });
    base.addEventListener("pointercancel", (e) => { if (e.pointerId === activeId) reset(); });
  }

  private bindButtons(root: HTMLElement, actions: TouchActions): void {
    const hold = (sel: string, down: () => void, up?: () => void): void => {
      const el = root.querySelector<HTMLElement>(sel)!;
      el.addEventListener("pointerdown", (e) => {
        e.preventDefault();
        capture(el, e.pointerId);
        el.classList.add("pressed");
        down();
      });
      const release = (): void => { el.classList.remove("pressed"); up?.(); };
      el.addEventListener("pointerup", release);
      el.addEventListener("pointercancel", release);
    };
    // 攻击：按住持续；冲刺：按下即入脉冲队列
    hold("#tbtn-attack", () => { this.attack = true; }, () => { this.attack = false; });
    hold("#tbtn-dash", () => { this.dashQueued = true; });
    // UI 按钮排：与键盘热键（C/I/H/Esc）共用装配层动作
    hold("#tbtn-equip", actions.openEquipment);
    hold("#tbtn-craft", actions.openCrafting);
    hold("#tbtn-help", actions.toggleHelp);
    hold("#tbtn-pause", actions.togglePause);
  }
}
