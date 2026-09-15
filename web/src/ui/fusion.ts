// 融合确认面板（祭坛触发）：Enter 接受 / Delete 或 Esc 拒绝——均为未绑定的 UI 键。
// 打开时 sim 已由 sim 侧置 paused，游戏输入 100% 失效。

import type { GameSim } from "../sim/game";

export class FusionPanel {
  private el: HTMLElement;
  private infoEl!: HTMLElement;
  visible = false;

  constructor(private sim: GameSim) {
    this.el = document.createElement("div");
    this.el.className = "overlay hidden";
    this.el.innerHTML = `
      <div class="panel">
        <h2>祭坛低语</h2>
        <p id="fusion-info" class="sub"></p>
        <div class="fusion-btns">
          <button id="btn-accept" type="button">融合（Enter）</button>
          <button id="btn-reject" type="button">拒绝（Delete）</button>
        </div>
      </div>`;
    document.getElementById("ui-root")!.appendChild(this.el);
    this.infoEl = this.el.querySelector("#fusion-info")!;
    this.el.querySelector("#btn-accept")!.addEventListener("click", () => this.answer(true));
    this.el.querySelector("#btn-reject")!.addEventListener("click", () => this.answer(false));
  }

  show(): void {
    const offer = this.sim.fusionOffer;
    if (!offer) return;
    this.visible = true;
    this.infoEl.textContent = `以「${offer.a}」与「${offer.b}」祭炼，可得「${offer.resultName}」`;
    this.el.classList.remove("hidden");
  }

  private answer(accept: boolean): void {
    this.sim.exec({ action: "choose", args: { target: "fusion", choice: accept ? "accept" : "reject" } });
    this.visible = false;
    this.el.classList.add("hidden");
  }

  /** 按键路由（main 层调用）：仅面板可见时消费。 */
  onKey(e: KeyboardEvent): boolean {
    if (!this.visible) return false;
    if (e.code === "Enter") { this.answer(true); return true; }
    if (e.code === "Delete" || e.code === "Escape") { this.answer(false); return true; }
    return false;
  }
}
