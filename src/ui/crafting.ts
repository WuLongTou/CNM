// 合成面板（I 键开关）：材料清单 + 配方合成。模态面板：打开时暂停 sim。
// I 为未绑定的背包键（符合 UI 快捷键约束），面板内纯鼠标操作。

import type { GameSim } from "../sim/game";
import { RECIPES } from "../sim/recipes";

export class CraftingPanel {
  /** R122 手柄 pad nav 容器（面板根）。 */
  readonly el: HTMLElement;
  private listEl!: HTMLElement;
  private invEl!: HTMLElement;
  visible = false;

  constructor(private sim: GameSim) {
    this.el = document.createElement("div");
    this.el.className = "overlay hidden";
    this.el.innerHTML = `
      <div class="panel panel-wide">
        <h2>工作台 · 合成</h2>
        <div id="craft-inv" class="eq-stats"></div>
        <div id="craft-list" class="eq-list"></div>
        <p class="hint">按 I 或点击关闭</p>
      </div>`;
    document.getElementById("ui-root")!.appendChild(this.el);
    this.listEl = this.el.querySelector("#craft-list")!;
    this.invEl = this.el.querySelector("#craft-inv")!;
    this.el.addEventListener("click", (e) => {
      if (e.target === this.el) this.toggle(false);
    });
  }

  resimulate(sim: GameSim): void {
    this.sim = sim;
    this.visible = false;
    this.el.classList.add("hidden");
  }

  toggle(force?: boolean): void {
    this.visible = force ?? !this.visible;
    this.el.classList.toggle("hidden", !this.visible);
    this.sim.paused = this.visible;
    if (this.visible) this.refresh();
  }

  refresh(): void {
    const inv = this.sim.inventory.counts;
    this.invEl.innerHTML =
      `<span>木 ×${inv.wood}</span><span>石 ×${inv.stone}</span>` +
      `<span>纤维 ×${inv.fiber}</span><span>浆果 ×${inv.berry}</span>`;
    this.listEl.innerHTML = RECIPES.map((r) => {
      const cost = Object.entries(r.cost).map(([m, n]) => `${m}×${n}`).join(" ");
      return `<div class="eq-item">
        <b>${r.name}</b><i>${r.desc}</i>
        <em>${cost}</em>
        <button data-recipe="${r.id}" type="button">合成</button></div>`;
    }).join("");
    this.listEl.querySelectorAll("button[data-recipe]").forEach((btn) => {
      btn.addEventListener("click", () => {
        this.sim.exec({ action: "craft", args: { recipe: (btn as HTMLElement).dataset.recipe } });
        this.refresh();
      });
    });
  }
}
