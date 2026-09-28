// 装备面板（C 键开关）：显示武器栏与聚合属性。模态面板：打开时暂停 sim。
// 快捷键约束：仅用 C（toggle_equipment 游戏键）开合，面板内纯鼠标操作。

import type { GameSim } from "../sim/game";
import { stageTag, aspectUnlocked, ASPECTS } from "../sim/weapons";

export class EquipmentPanel {
  private el: HTMLElement;
  private listEl!: HTMLElement;
  private statsEl!: HTMLElement;
  visible = false;

  constructor(private sim: GameSim) {
    this.el = document.createElement("div");
    this.el.className = "overlay hidden";
    this.el.innerHTML = `
      <div class="panel panel-wide">
        <h2>行囊 · 武器</h2>
        <div id="eq-stats" class="eq-stats"></div>
        <div id="eq-list" class="eq-list"></div>
        <p class="hint">按 C 或点击关闭</p>
      </div>`;
    document.getElementById("ui-root")!.appendChild(this.el);
    this.listEl = this.el.querySelector("#eq-list")!;
    // R105 形态切换：事件委托（卡片列表重绘不重复绑定）
    this.listEl.addEventListener("click", (e) => {
      const btn = (e.target as HTMLElement).closest<HTMLElement>(".eq-aspect");
      if (!btn) return;
      const idx = Number(btn.dataset.idx);
      this.sim.toggleAspect(idx);
      this.refresh();
    });
    this.statsEl = this.el.querySelector("#eq-stats")!;
    this.el.addEventListener("click", () => this.toggle(false));
  }

  resimulate(sim: GameSim): void {
    this.sim = sim;
    this.visible = false;
    this.el.classList.add("hidden");
  }

  toggle(force?: boolean): void {
    this.visible = force ?? !this.visible;
    this.el.classList.toggle("hidden", !this.visible);
    this.sim.paused = this.visible; // 模态独占：打开即暂停战斗
    if (this.visible) this.refresh();
  }

  refresh(): void {
    const s = this.sim;
    const st = s.stats;
    this.statsEl.innerHTML = `
      <span>伤害 ×${st.dmgMult.toFixed(2)}</span>
      <span>攻速 ×${st.aspdMult.toFixed(2)}</span>
      <span>范围 ×${st.rangeMult.toFixed(2)}</span>
      <span>暴击 ${st.crit.toFixed(0)}%</span>
      ${st.psn > 0 ? `<span>中毒 ${st.psn.toFixed(0)}%</span>` : ""}
      ${st.freeze > 0 ? `<span>冰缓 ${st.freeze.toFixed(0)}%</span>` : ""}
      ${st.chain > 0 ? `<span>链式 ${st.chain}</span>` : ""}`;
    this.listEl.innerHTML = s.player.weapons.map((w, i) => {
      const stage = stageTag(w);
      const stageLabel = stage === "ultimate" ? "【终极】" : stage === "ascended" ? "【进阶】" : "";
      const active = i === s.player.weapons.length - 1;
      // R83 战斗统计：本武器累计伤害与占比（总额 0 时省略）
      const share = s.dmgDealtTotal > 0
        ? `<em>伤害 ${Math.round(w.dmgDealt)}（${Math.round((w.dmgDealt / s.dmgDealtTotal) * 100)}%）</em>`
        : "";
      // R105 形态切换：进阶解锁且有形态表的武器显示按钮（事件委托 bind 一次）
      const aspectBtn = aspectUnlocked(w) && ASPECTS[w.defId]
        ? `<button type="button" class="eq-aspect" data-idx="${i}">形态：${w.aspect === 1 ? ASPECTS[w.defId].name : "本形"}</button>`
        : "";
      return `<div class="eq-item ${active ? "active" : ""}">
        <b>${w.name}</b>${stageLabel}<i>${w.type}${active ? " · 当前" : ""}</i>
        <em>升 ${w.ups} 次</em>${aspectBtn}${share}</div>`;
    }).join("") || `<div class="eq-item"><i>空手（徒手近战）</i></div>`;
  }
}
