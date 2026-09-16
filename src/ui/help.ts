// 帮助面板（H 键）：操作说明 / 妖物图鉴 / 机制。模态暂停。
// H 为 toggle_help 已绑定游戏键——打开期间游戏输入由 sim 门控 100% 失效（项目硬约束）。

import type { GameSim } from "../sim/game";
import { ENEMY_NAMES, ENEMY_STATS } from "../sim/config";

export class HelpPanel {
  private el: HTMLElement;
  private page = 0;
  private bodyEl!: HTMLElement;
  visible = false;

  private pages = ["操作", "妖物图鉴", "机制"];

  constructor(private sim: GameSim) {
    this.el = document.createElement("div");
    this.el.className = "overlay hidden";
    this.el.innerHTML = `
      <div class="panel panel-wide help-panel">
        <h2>手账 · 修行笔记</h2>
        <div class="help-tabs">${this.pages.map((p, i) => `<button data-page="${i}" type="button">${p}</button>`).join("")}</div>
        <div id="help-body" class="help-body"></div>
        <p class="hint">按 H 或点击空白处关闭 · Tab 切页（← →）</p>
      </div>`;
    document.getElementById("ui-root")!.appendChild(this.el);
    this.bodyEl = this.el.querySelector("#help-body")!;
    this.el.addEventListener("click", (e) => { if (e.target === this.el) this.toggle(false); });
    this.el.querySelectorAll("button[data-page]").forEach((b) =>
      b.addEventListener("click", () => { this.page = Number((b as HTMLElement).dataset.page); this.refresh(); }));
    this.refresh();
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

  /** 左右方向键切页（面板可见时消费，避免与移动键冲突——面板开着时游戏输入已死）。 */
  onKey(e: KeyboardEvent): boolean {
    if (!this.visible) return false;
    if (e.code === "ArrowLeft") { this.page = (this.page + this.pages.length - 1) % this.pages.length; this.refresh(); return true; }
    if (e.code === "ArrowRight") { this.page = (this.page + 1) % this.pages.length; this.refresh(); return true; }
    return false;
  }

  refresh(): void {
    this.el.querySelectorAll(".help-tabs button").forEach((b, i) =>
      b.classList.toggle("active", i === this.page));
    if (this.page === 0) {
      this.bodyEl.innerHTML = `
        <table class="help-table">
          <tr><td>移动</td><td>WASD / 方向键</td></tr>
          <tr><td>攻击</td><td>空格（按住连击）</td></tr>
          <tr><td>冲刺</td><td>Shift</td></tr>
          <tr><td>装备 / 合成</td><td>C / I</td></tr>
          <tr><td>帮助 / 暂停</td><td>H / Esc</td></tr>
          <tr><td>采集</td><td>近战攻击范围内的树、岩、灌木</td></tr>
        </table>`;
    } else if (this.page === 1) {
      this.bodyEl.innerHTML = `<table class="help-table">
        ${Object.keys(ENEMY_NAMES).map((k) => {
          const s = ENEMY_STATS[Number(k)];
          return `<tr><td>${ENEMY_NAMES[Number(k)]}</td><td>生命 ${s.hp} · 伤害 ${s.dmg} · 速度 ${s.speed}</td></tr>`;
        }).join("")}
      </table>`;
    } else {
      this.bodyEl.innerHTML = `
        <table class="help-table">
          <tr><td>威胁阶</td><td>每 45 秒推进一阶，妖物变强；每 5 阶出妖王</td></tr>
          <tr><td>精英词缀</td><td>迅捷 / 再生 / 护盾 / 爆裂 / 狂乱</td></tr>
          <tr><td>武器进阶</td><td>同一武器升级 3 次进阶、6 次终极</td></tr>
          <tr><td>祭坛融合</td><td>携带两件武器靠近祭坛，可祭炼为新武器</td></tr>
          <tr><td>天气</td><td>雨减速、雾降能见、血月妖潮狂暴且精英频出</td></tr>
          <tr><td>连击</td><td>连续命中叠伤害（每层 +5%，上限 +50%）</td></tr>
        </table>`;
    }
  }
}
