// 手柄菜单导航（R76）：模态面板内的焦点环——dpad/摇杆移动、A 确认、B 返回。
// 通用实现：容器内可见的 button/[role=button] 即焦点序列（线性循环），
// 高频三面板（升级/商店/暂停）接入；隐藏面板内元素自动不可聚焦。

import type { MenuEdges } from "../app/gamepad";

const FOCUS_SEL = "button:not([disabled]), [role='button']:not([disabled])";
export const FOCUS_CLASS = "pad-focus";

export class PadMenuNav {
  private container: HTMLElement | null = null;
  private idx = 0;

  /** 每帧同步容器（内容重渲染后焦点序列自动重建；idx 越界自动收敛）。 */
  sync(container: HTMLElement | null): void {
    if (this.container !== container) {
      this.clearPaint();
      this.container = container;
      this.idx = 0;
    }
    if (!this.container) return;
    const list = this.focusables();
    if (this.idx >= list.length) this.idx = list.length > 0 ? 0 : -1;
    this.paint(list);
  }

  detach(): void {
    this.clearPaint();
    this.container = null;
    this.idx = -1;
  }

  /** 处理菜单边沿：方向移动焦点（循环），A 点击焦点。返回是否有焦点可交互。 */
  handle(e: MenuEdges): boolean {
    if (!this.container) return false;
    const list = this.focusables();
    if (list.length === 0) return false;
    if (this.idx < 0) this.idx = 0;
    const n = list.length;
    if (e.up || e.left) this.idx = (this.idx - 1 + n) % n;
    else if (e.down || e.right) this.idx = (this.idx + 1) % n;
    this.paint(list);
    if (e.confirm) list[this.idx]?.click();
    return true;
  }

  private focusables(): HTMLElement[] {
    if (!this.container) return [];
    return [...this.container.querySelectorAll<HTMLElement>(FOCUS_SEL)].filter(
      (el) => el.offsetParent !== null, // 隐藏祖先内不可聚焦
    );
  }

  private paint(list: HTMLElement[]): void {
    for (const el of list) el.classList.remove(FOCUS_CLASS);
    list[this.idx]?.classList.add(FOCUS_CLASS);
  }

  private clearPaint(): void {
    if (!this.container) return;
    for (const el of this.container.querySelectorAll("." + FOCUS_CLASS)) {
      el.classList.remove(FOCUS_CLASS);
    }
  }
}
