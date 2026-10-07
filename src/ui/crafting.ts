// 合成面板（I 键开关）：材料清单 + 配方合成。模态面板：打开时暂停 sim。
// I 为未绑定的背包键（符合 UI 快捷键约束），面板内纯鼠标操作。

import type { GameSim } from "../sim/game";
import { RECIPES } from "../sim/recipes";
import { MATERIAL_NAMES, type Material } from "../sim/inventory";

export class CraftingPanel {
  /** R122 手柄 pad nav 容器（面板根）。 */
  readonly el: HTMLElement;
  private listEl!: HTMLElement;
  private invEl!: HTMLElement;
  private msgEl!: HTMLElement;
  visible = false;

  constructor(private sim: GameSim) {
    this.el = document.createElement("div");
    this.el.className = "overlay hidden";
    this.el.innerHTML = `
      <div class="panel panel-wide">
        <h2>工作台 · 合成</h2>
        <div id="craft-inv" class="eq-stats"></div>
        <div id="craft-list" class="eq-list"></div>
        <p id="craft-msg" class="hint"></p>
        <p class="hint">按 I 或点击关闭</p>
      </div>`;
    document.getElementById("ui-root")!.appendChild(this.el);
    this.listEl = this.el.querySelector("#craft-list")!;
    this.invEl = this.el.querySelector("#craft-inv")!;
    this.msgEl = this.el.querySelector("#craft-msg")!;
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
    // R210 材料名统一走 MATERIAL_NAMES（此前库存行中文、配方成本行英文 raw id，同屏中英混排）
    this.invEl.innerHTML = (Object.keys(MATERIAL_NAMES) as Material[])
      .map((m) => `<span>${MATERIAL_NAMES[m]} ×${inv[m]}</span>`).join("");
    this.listEl.innerHTML = RECIPES.map((r) => {
      const cost = Object.entries(r.cost) as Array<[Material, number]>;
      // R210 缺料可视化：成本项标红 + 按钮禁用并写明「材料不足」（此前按钮永远可点、
      // 面板又忽略 craft 返回值——点了毫无反应）
      const costText = cost
        .map(([m, n]) => `<span class="${inv[m] < n ? "lack" : ""}">${MATERIAL_NAMES[m]}×${n}</span>`)
        .join(" ");
      const ok = cost.every(([m, n]) => inv[m] >= n);
      return `<div class="eq-item">
        <b>${r.name}</b><i>${r.desc}</i>
        <em>${costText}</em>
        <button data-recipe="${r.id}" type="button"${ok ? "" : " disabled"}>${ok ? "合成" : "材料不足"}</button></div>`;
    }).join("");
    this.listEl.querySelectorAll("button[data-recipe]").forEach((btn) => {
      btn.addEventListener("click", () => {
        const recipe = (btn as HTMLElement).dataset.recipe!;
        const res = this.sim.exec({ action: "craft", args: { recipe } });
        const data = (res.data ?? {}) as { crafted?: boolean; reason?: string; name?: string };
        this.msgEl.textContent = data.crafted ? `已合成：${data.name ?? recipe}` : (data.reason ?? "合成失败");
        this.refresh();
      });
    });
  }
}
