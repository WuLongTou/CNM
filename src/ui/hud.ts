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
  private weaponText!: HTMLElement;

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
        <span id="weapon"></span>
      </div>`;
    this.root.appendChild(hud);
    this.hpFill = hud.querySelector("#hp-fill")!;
    this.hpText = hud.querySelector("#hp-text")!;
    this.roundText = hud.querySelector("#round")!;
    this.scoreText = hud.querySelector("#score")!;
    this.killsText = hud.querySelector("#kills")!;
    this.timeText = hud.querySelector("#time")!;
    this.weaponText = hud.querySelector("#weapon")!;
  }

  private welcome!: HTMLElement;
  private gameover!: HTMLElement;
  private gameoverStats!: HTMLElement;

  private buildWelcome(): void {
    this.welcome = document.createElement("div");
    this.welcome.className = "overlay";
    this.welcome.innerHTML = `
      <div class="panel">
        <h1>斩妖录</h1>
        <p class="sub">血色涂鸦 · 除妖生存</p>
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
        <h1>除妖师陨落</h1>
        <p id="gameover-stats" class="sub"></p>
        <div class="meta-row">
          <button id="btn-meta-hp" type="button"></button>
          <button id="btn-meta-dmg" type="button"></button>
        </div>
        <p id="meta-coins" class="sub"></p>
        <button id="btn-restart" type="button">再来一局</button>
      </div>`;
    this.root.appendChild(this.gameover);
    this.gameoverStats = this.gameover.querySelector("#gameover-stats")!;
    this.gameover.querySelector("#btn-restart")!.addEventListener("click", () => this.onRestart());
    const buy = (kind: "hp" | "dmg") => {
      this.sim.exec({ action: "buy_meta", args: { kind } });
      this.refreshMeta();
    };
    this.gameover.querySelector("#btn-meta-hp")!.addEventListener("click", () => buy("hp"));
    this.gameover.querySelector("#btn-meta-dmg")!.addEventListener("click", () => buy("dmg"));
  }

  private refreshMeta(): void {
    const m = this.sim.exec({ action: "meta" }).data as { coins: number; metaHp: number; metaDmg: number };
    (this.gameover.querySelector("#btn-meta-hp") as HTMLElement).textContent =
      `生命 +20（Lv.${m.metaHp}）`;
    (this.gameover.querySelector("#btn-meta-dmg") as HTMLElement).textContent =
      `伤害 +10%（Lv.${m.metaDmg}）`;
    (this.gameover.querySelector("#meta-coins") as HTMLElement).textContent = `金币 ${m.coins}`;
  }

  /** 重开一局时替换绑定的 sim 实例。 */
  resimulate(sim: GameSim): void {
    this.sim = sim;
  }

  /** 触屏设备上替换欢迎页操作提示（默认文案为键盘键位）。 */
  setHint(text: string): void {
    const el = this.welcome.querySelector<HTMLElement>(".hint");
    if (el) el.textContent = text;
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
    this.weaponText.textContent = s.activeWeapon ? `武器 ${s.activeWeapon.name}` : "徒手";
    this.welcome.classList.toggle("hidden", s.state !== "WeaponSelection");
    const wasOver = this.gameover.classList.contains("hidden");
    this.gameover.classList.toggle("hidden", s.state !== "GameOver");
    if (!wasOver && s.state === "GameOver") {
      const coins = Math.floor(s.score / 50) + s.kills;
      this.gameoverStats.textContent =
        `分数 ${s.score} · 击杀 ${s.kills} · 存活 ${s.survivalTime.toFixed(0)}s · 金币 +${coins}`;
      this.refreshMeta();
    }
  }
}
