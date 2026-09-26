// 暂停菜单（Esc）+ 设置（音量）。模态暂停；Esc 优先级低于融合/帮助面板（main 层路由）。
// R37：打开时聚合五条收藏线进度（制霸/藏珍/终极/融合/图鉴金边），目标一目了然。

import type { GameSim } from "../sim/game";
import { CODEX_GOLD_THRESHOLD } from "../sim/game";
import { FUSION_RECIPES, WEAPON_DATA } from "../sim/weapons";
import { ENEMY_NAMES } from "../sim/config";
import type { AudioManager } from "../app/audio";

export class PauseMenu {
  private el: HTMLElement;
  visible = false;
  /** 装配层接线：走正常 restart()（保留血誓/种子/战绩），替代旧整页刷新（R36）。 */
  onRestart: (() => void) | null = null;

  constructor(private sim: GameSim, private audio: AudioManager) {
    this.el = document.createElement("div");
    this.el.className = "overlay hidden";
    this.el.innerHTML = `
      <div class="panel">
        <h2>暂停</h2>
        <p class="sub" id="pause-stats"></p>
        <p class="sub" id="pause-collection"></p>
        <label class="vol-row">音量
          <input id="vol" type="range" min="0" max="100" step="5" />
          <span id="vol-num"></span>
        </label>
        <button id="btn-resume" type="button">继续（Esc）</button>
        <button id="btn-pause-restart" type="button">重新开始</button>
      </div>`;
    document.getElementById("ui-root")!.appendChild(this.el);
    const vol = this.el.querySelector("#vol") as HTMLInputElement;
    const num = this.el.querySelector("#vol-num") as HTMLElement;
    vol.value = String(Math.round(this.audio.volume * 100));
    num.textContent = vol.value;
    vol.addEventListener("input", () => {
      this.audio.setVolume(Number(vol.value) / 100);
      num.textContent = vol.value;
    });
    this.el.querySelector("#btn-resume")!.addEventListener("click", () => this.toggle(false));
    this.el.querySelector("#btn-pause-restart")!.addEventListener("click", () => {
      this.toggle(false);
      // R36：整页 reload 会丢血誓/自定义种子并绕过 resimulate 链，改走装配层 restart()
      this.onRestart?.();
    });
    this.el.addEventListener("click", (e) => { if (e.target === this.el) this.toggle(false); });
  }

  toggle(force?: boolean): void {
    this.visible = force ?? !this.visible;
    this.el.classList.toggle("hidden", !this.visible);
    this.sim.paused = this.visible;
    // R29 暂停战绩：打开时刷新本局比分/击杀/存活/目标（随时知道战况）
    if (this.visible) {
      const s = this.sim;
      const el = this.el.querySelector<HTMLElement>("#pause-stats");
      if (el) {
        el.textContent = `分数 ${s.score} · 击杀 ${s.kills} · 存活 ${s.survivalTime.toFixed(0)}s` +
          ` · 威胁阶 ${s.round} · ${s.role.name}/${s.difficulty.name}`;
      }
      this.refreshCollection();
    }
  }

  /** 收藏进度行（R37）：聚合五条收集线，目标一目了然。 */
  private refreshCollection(): void {
    const el = this.el.querySelector<HTMLElement>("#pause-collection");
    if (!el) return;
    const s = this.sim;
    const clears = (s.exec({ action: "meta" }).data as { clears: number }).clears;
    const armory = (s.exec({ action: "armory" }).data as { owned: string[] }).owned.length;
    const armoryTotal = Object.keys(WEAPON_DATA).length;
    const ult = (s.exec({ action: "ultimates" }).data as { reached: string[] }).reached.length;
    const fused = (s.exec({ action: "fusions" }).data as { fused: string[] }).fused.length;
    const codex = s.exec({ action: "codex" }).data as { total: Record<number, number> };
    const ids = Object.keys(ENEMY_NAMES).map(Number);
    const gold = ids.filter((id) => (codex.total[id] ?? 0) >= CODEX_GOLD_THRESHOLD).length;
    el.textContent = `收藏 · 制霸 ${clears}/9 · 藏珍 ${armory}/${armoryTotal} · 终极 ${ult}` +
      ` · 祭炼 ${fused}/${FUSION_RECIPES.length} · 金边 ${gold}/${ids.length}`;
  }

  /** restart 后换绑新 sim：旧局连同其暂停态一起作废（新 sim 未暂停），面板复位隐藏。 */
  resimulate(sim: GameSim): void {
    this.sim = sim;
    this.visible = false;
    this.el.classList.add("hidden");
  }
}
