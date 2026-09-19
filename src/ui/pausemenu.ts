// 暂停菜单（Esc）+ 设置（音量）。模态暂停；Esc 优先级低于融合/帮助面板（main 层路由）。

import type { GameSim } from "../sim/game";
import type { AudioManager } from "../app/audio";

export class PauseMenu {
  private el: HTMLElement;
  visible = false;

  constructor(private sim: GameSim, private audio: AudioManager) {
    this.el = document.createElement("div");
    this.el.className = "overlay hidden";
    this.el.innerHTML = `
      <div class="panel">
        <h2>暂停</h2>
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
      location.reload();
    });
    this.el.addEventListener("click", (e) => { if (e.target === this.el) this.toggle(false); });
  }

  toggle(force?: boolean): void {
    this.visible = force ?? !this.visible;
    this.el.classList.toggle("hidden", !this.visible);
    this.sim.paused = this.visible;
  }

  /** restart 后换绑新 sim：旧局连同其暂停态一起作废（新 sim 未暂停），面板复位隐藏。 */
  resimulate(sim: GameSim): void {
    this.sim = sim;
    this.visible = false;
    this.el.classList.add("hidden");
  }
}
