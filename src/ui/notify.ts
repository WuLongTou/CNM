// 通知条：成就/合成/开箱/进阶等事件的非阻断提示，2.5s 自动淡出。
// banner()：成就等高光事件的中央纸签（简笔风，弹入-悬停-淡出）。

export class Notify {
  private root: HTMLElement;
  private bannerRoot: HTMLElement;

  constructor() {
    this.root = document.createElement("div");
    this.root.id = "notify-root";
    document.getElementById("ui-root")!.appendChild(this.root);
    this.bannerRoot = document.createElement("div");
    this.bannerRoot.id = "banner-root";
    document.getElementById("ui-root")!.appendChild(this.bannerRoot);
  }

  /** 中央纸签：成就达成等高光时刻（2.6s 后淡出）。 */
  banner(title: string, sub: string): void {
    const b = document.createElement("div");
    b.className = "sketch-banner";
    b.innerHTML = `<div class="sb-title">${title}</div><div class="sb-sub">${sub}</div>`;
    this.bannerRoot.replaceChildren(b); // 高光事件只保留最新一张
    requestAnimationFrame(() => b.classList.add("show"));
    setTimeout(() => {
      b.classList.remove("show");
      b.classList.add("leave");
      setTimeout(() => b.remove(), 450);
    }, 2600);
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
