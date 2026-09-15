// 通知条：成就/合成/开箱/进阶等事件的非阻断提示，2.5s 自动淡出。

export class Notify {
  private root: HTMLElement;

  constructor() {
    this.root = document.createElement("div");
    this.root.id = "notify-root";
    document.getElementById("ui-root")!.appendChild(this.root);
  }

  push(text: string): void {
    const n = document.createElement("div");
    n.className = "notify";
    n.textContent = text;
    this.root.appendChild(n);
    requestAnimationFrame(() => n.classList.add("show"));
    setTimeout(() => {
      n.classList.remove("show");
      setTimeout(() => n.remove(), 400);
    }, 2500);
    // 最多同屏 4 条
    while (this.root.children.length > 4) this.root.firstChild?.remove();
  }
}
