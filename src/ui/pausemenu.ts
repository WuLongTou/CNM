// 暂停菜单（Esc）+ 设置（音量 + R61 表现三开关：震屏/伤害数字/顿帧）。
// 模态暂停；Esc 优先级低于融合/帮助面板（main 层路由）。
// R37：打开时聚合五条收藏线进度（制霸/藏珍/终极/融合/图鉴金边），目标一目了然。

import type { GameSim } from "../sim/game";
import { CODEX_GOLD_THRESHOLD } from "../sim/game";
import { FUSION_RECIPES, WEAPON_DATA } from "../sim/weapons";
import { ENEMY_NAMES } from "../sim/config";
import type { AudioManager } from "../app/audio";
import type { VisualSettings } from "../app/settings";

export class PauseMenu {
  /** 面板根元素（R76 手柄菜单导航的容器）。 */
  readonly el: HTMLElement;
  visible = false;
  /** 装配层接线：走正常 restart()（保留血誓/种子/战绩），替代旧整页刷新（R36）。 */
  onRestart: (() => void) | null = null;
  /** R168 放弃本局接线（回欢迎页；不入战绩）。 */
  onAbandon: (() => void) | null = null;
  /** R168 两段确认武装态。 */
  private abandonArmed = false;

  constructor(
    private sim: GameSim,
    private audio: AudioManager,
    /** R61 表现设置（app 层持有对象引用，这里原位改写 + 变更回调）。 */
    private settings: VisualSettings,
    /** R61 设置变更回调（app 层负责持久化与 view 同步）。 */
    private onSettingsChange: () => void,
  ) {
    this.el = document.createElement("div");
    this.el.className = "overlay hidden";
    this.el.innerHTML = `
      <div class="panel">
        <h2>暂停</h2>
        <p class="sub" id="pause-stats"></p>
        <p class="sub" id="pause-collection"></p>
        <label class="vol-row">音乐
          <input id="vol-music" type="range" min="0" max="100" step="5" />
          <span id="vol-music-num"></span>
        </label>
        <label class="vol-row">音效
          <input id="vol-sfx" type="range" min="0" max="100" step="5" />
          <span id="vol-sfx-num"></span>
        </label>
        <div class="set-row" role="group" aria-label="表现设置">
          <span class="set-title">表现</span>
          <button id="set-shake" type="button"></button>
          <button id="set-dmg" type="button"></button>
          <button id="set-hit" type="button"></button>
          <button id="set-rumble" type="button"></button>
        </div>
        <button id="btn-resume" type="button">继续（Esc）</button>
        <button id="btn-pause-restart" type="button">重新开始</button>
        <button id="btn-pause-abandon" type="button">放弃本局</button>
      </div>`;
    document.getElementById("ui-root")!.appendChild(this.el);
    // R67 音乐/音效双滑杆：各自独立持久化（audio 层 setMusicVolume/setSfxVolume）
    const wireVol = (inputId: string, numId: string, get: () => number, set: (v: number) => void) => {
      const input = this.el.querySelector<HTMLInputElement>(`#${inputId}`)!;
      const num = this.el.querySelector<HTMLElement>(`#${numId}`)!;
      input.value = String(Math.round(get() * 100));
      num.textContent = input.value;
      input.addEventListener("input", () => {
        set(Number(input.value) / 100);
        num.textContent = input.value;
      });
    };
    wireVol("vol-music", "vol-music-num", () => this.audio.musicVolume, (v) => this.audio.setMusicVolume(v));
    wireVol("vol-sfx", "vol-sfx-num", () => this.audio.sfxVolume, (v) => this.audio.setSfxVolume(v));
    // R61 表现设置三开关：鼠标点按切换（不占用任何键位，符合模态硬约束）；
    // 只改写共享 settings 对象并回调，持久化与 view 同步由 app 层做。
    const wireToggle = (id: string, key: keyof VisualSettings, label: string) => {
      const btn = this.el.querySelector<HTMLButtonElement>(`#${id}`)!;
      const paint = () => {
        const on = this.settings[key];
        btn.textContent = `${label}·${on ? "开" : "关"}`;
        btn.setAttribute("aria-pressed", String(on));
        btn.classList.toggle("off", !on);
      };
      paint();
      btn.addEventListener("click", () => {
        this.settings[key] = !this.settings[key];
        paint();
        this.onSettingsChange();
      });
    };
    wireToggle("set-shake", "shake", "震屏");
    wireToggle("set-dmg", "damageNumbers", "数字");
    wireToggle("set-hit", "hitstop", "顿帧");
    wireToggle("set-rumble", "rumble", "震动");
    this.el.querySelector("#btn-resume")!.addEventListener("click", () => this.toggle(false));
    this.el.querySelector("#btn-pause-restart")!.addEventListener("click", () => {
      this.toggle(false);
      // R36：整页 reload 会丢血誓/自定义种子并绕过 resimulate 链，改走装配层 restart()
      this.onRestart?.();
    });
    // R168 放弃本局：两段确认（防误触丢局）；确认后走装配层放弃（回欢迎页）
    this.el.querySelector("#btn-pause-abandon")!.addEventListener("click", () => {
      const btn = this.el.querySelector<HTMLButtonElement>("#btn-pause-abandon")!;
      if (!this.abandonArmed) {
        this.abandonArmed = true;
        btn.textContent = "确认放弃？（再点一次）";
        return;
      }
      this.abandonArmed = false;
      btn.textContent = "放弃本局";
      this.toggle(false);
      this.onAbandon?.();
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
    this.abandonArmed = false;
    const ab = this.el.querySelector<HTMLButtonElement>("#btn-pause-abandon");
    if (ab) ab.textContent = "放弃本局";
    this.el.classList.add("hidden");
  }
}
