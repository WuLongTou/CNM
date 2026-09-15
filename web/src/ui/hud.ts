// DOM UI 层：HUD、欢迎画面、GameOver 结算。
// 手账/血色涂鸦风（对齐 HandDrawnTheme：纸面板 + 暗红描边 + 深红墨字）。
// UI 快捷键约束：本层只用鼠标按钮，不占用任何游戏按键。

import type { GameSim } from "../sim/game";

export class Hud {
  private root: HTMLElement;
  private hpFill!: HTMLElement;
  private hpText!: HTMLElement;
  private roundText!: HTMLElement;
  private scoreText!: HTMLElement;
  private killsText!: HTMLElement;
  private timeText!: HTMLElement;

  constructor(private sim: GameSim, private onStart: () => void, private onRestart: () => void) {
    this.root = document.createElement("div");
    this.root.id = "ui-root";
    document.getElementById("app")!.appendChild(this.root);
    this.buildHud();
    this.buildWelcome();
    this.buildGameOver();
  }

  private buildHud(): void {
    const hud = document.createElement("div");
    hud.id = "hud";
    hud.innerHTML = `
      <div class="hp-wrap"><div class="hp-bar"><div class="hp-fill" id="hp-fill"></div></div>
        <span id="hp-text"></span></div>
      <div class="stats">
        <span id="round"></span><span id="score"></span>
        <span id="kills"></span><span id="time"></span>
      </div>`;
    this.root.appendChild(hud);
    this.hpFill = hud.querySelector("#hp-fill")!;
    this.hpText = hud.querySelector("#hp-text")!;
    this.roundText = hud.querySelector("#round")!;
    this.scoreText = hud.querySelector("#score")!;
    this.killsText = hud.querySelector("#kills")!;
    this.timeText = hud.querySelector("#time")!;
  }

  private welcome!: HTMLElement;
  private gameover!: HTMLElement;
  private gameoverStats!: HTMLElement;

  private buildWelcome(): void {
    this.welcome = document.createElement("div");
    this.welcome.className = "overlay";
    this.welcome.innerHTML = `
      <div class="panel">
        <h1>僵尸大战</h1>
        <p class="sub">血色涂鸦 · 持续探索生存</p>
        <button id="btn-start" type="button">开始游戏</button>
        <p class="hint">WASD 移动 · 空格 攻击 · Shift 冲刺</p>
      </div>`;
    this.root.appendChild(this.welcome);
    this.welcome.querySelector("#btn-start")!.addEventListener("click", () => this.onStart());
  }

  private buildGameOver(): void {
    this.gameover = document.createElement("div");
    this.gameover.className = "overlay hidden";
    this.gameover.innerHTML = `
      <div class="panel">
        <h1>你死了</h1>
        <p id="gameover-stats" class="sub"></p>
        <button id="btn-restart" type="button">再来一局</button>
      </div>`;
    this.root.appendChild(this.gameover);
    this.gameoverStats = this.gameover.querySelector("#gameover-stats")!;
    this.gameover.querySelector("#btn-restart")!.addEventListener("click", () => this.onRestart());
  }

  /** 重开一局时替换绑定的 sim 实例。 */
  resimulate(sim: GameSim): void {
    this.sim = sim;
  }

  /** 每帧同步 sim → DOM。 */
  update(): void {
    const s = this.sim;
    const ratio = Math.max(0, s.player.hp / s.player.maxHp);
    this.hpFill.style.width = `${ratio * 100}%`;
    this.hpText.textContent = `${Math.ceil(s.player.hp)} / ${s.player.maxHp}`;
    this.roundText.textContent = `威胁阶 ${s.round}`;
    this.scoreText.textContent = `分数 ${s.score}`;
    this.killsText.textContent = `击杀 ${s.kills}`;
    this.timeText.textContent = `存活 ${s.survivalTime.toFixed(0)}s`;
    this.welcome.classList.toggle("hidden", s.state !== "WeaponSelection");
    const wasOver = this.gameover.classList.contains("hidden");
    this.gameover.classList.toggle("hidden", s.state !== "GameOver");
    if (!wasOver && s.state === "GameOver") {
      const coins = Math.floor(s.score / 50) + s.kills;
      this.gameoverStats.textContent =
        `分数 ${s.score} · 击杀 ${s.kills} · 存活 ${s.survivalTime.toFixed(0)}s · 金币 +${coins}`;
    }
  }
}
