// 通知条：成就/合成/开箱/进阶等事件的非阻断提示，2.5s 自动淡出。
// banner()：成就等高光事件的中央纸签（简笔风，弹入-悬停-淡出）。
// R92 纸签队列：同 tick 多成就不再互相顶掉——排队逐张轮播（每张完整展示 2.6s）。

export class Notify {
  private root: HTMLElement;
  private bannerRoot: HTMLElement;
  /** R92 纸签队列 + 展示中标志（多成就同 tick 排队轮播）。 */
  private bannerQueue: Array<{ title: string; sub: string }> = [];
  private bannerShowing = false;

  constructor() {
    this.root = document.createElement("div");
    this.root.id = "notify-root";
    document.getElementById("ui-root")!.appendChild(this.root);
    this.bannerRoot = document.createElement("div");
    this.bannerRoot.id = "banner-root";
    document.getElementById("ui-root")!.appendChild(this.bannerRoot);
  }

  /** 中央纸签：成就达成等高光时刻。R92 队列化——有纸签展示中则排队逐张轮播。 */
  banner(title: string, sub: string): void {
    this.bannerQueue.push({ title, sub });
    if (!this.bannerShowing) this.showNextBanner();
  }

  private showNextBanner(): void {
    const next = this.bannerQueue.shift();
    if (!next) return;
    this.bannerShowing = true;
    const b = document.createElement("div");
    b.className = "sketch-banner";
    b.innerHTML = `<div class="sb-title">${next.title}</div><div class="sb-sub">${next.sub}</div>`;
    this.bannerRoot.replaceChildren(b);
    requestAnimationFrame(() => b.classList.add("show"));
    setTimeout(() => {
      b.classList.remove("show");
      b.classList.add("leave");
      setTimeout(() => {
        b.remove();
        this.bannerShowing = false;
        this.showNextBanner(); // 队列下一张
      }, 450);
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