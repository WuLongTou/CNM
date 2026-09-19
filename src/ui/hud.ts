// DOM UI 层：HUD、欢迎画面、GameOver 结算。
// 简笔画速写本风（纸面板 + 炭描边 + 血红点缀，对齐 gen-sketch 风格规范）。
// UI 快捷键约束：本层只用鼠标按钮，不占用任何游戏按键。

import type { GameSim } from "../sim/game";

export class Hud {
  private root: HTMLElement;
  private hpFill!: HTMLElement;
  private hpText!: HTMLElement;
  private roundText!: HTMLElement;
  private weatherText!: HTMLElement;
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
        <span id="round"></span><span id="weather"></span><span id="score"></span>
        <span id="kills"></span><span id="time"></span>
        <span id="weapon"></span>
      </div>`;
    this.root.appendChild(hud);
    this.hpFill = hud.querySelector("#hp-fill")!;
    this.hpText = hud.querySelector("#hp-text")!;
    this.roundText = hud.querySelector("#round")!;
    this.weatherText = hud.querySelector("#weather")!;
    this.scoreText = hud.querySelector("#score")!;
    this.killsText = hud.querySelector("#kills")!;
    this.timeText = hud.querySelector("#time")!;
    this.weaponText = hud.querySelector("#weapon")!;
  }

  private welcome!: HTMLElement;
  private gameover!: HTMLElement;
  private gameoverStats!: HTMLElement;
  /** GameOver 面板延迟弹出计时（进 GameOver 起算；让死亡消散先播完）。 */
  private gameOverAt = 0;

  private buildWelcome(): void {
    this.welcome = document.createElement("div");
    this.welcome.className = "overlay";
    this.welcome.innerHTML = `
      <div class="panel">
        <img class="title-sketch" src="assets/sprites/ui/title_sketch.png" alt="" draggable="false">
        <h1>斩妖录</h1>
        <p class="sub">简笔画速写本 · 除妖生存</p>
        <p id="save-stats" class="sub"></p>
        <button id="btn-start" type="button">开始游戏</button>
        <p class="hint">WASD 移动 · 空格 攻击 · Shift 冲刺</p>
      </div>`;
    this.root.appendChild(this.welcome);
    this.welcome.querySelector("#btn-start")!.addEventListener("click", () => this.onStart());
    this.refreshSaveStats();
  }

  /** 欢迎页历史战绩（最高分/总击杀/局数），零局时显示引言。 */
  private refreshSaveStats(): void {
    const el = this.welcome.querySelector<HTMLElement>("#save-stats");
    if (!el) return;
    const d = this.sim.exec({ action: "meta" }).data as { highScore: number; totalKills: number; games: number };
    el.textContent = d.games > 0
      ? `最高分 ${d.highScore} · 总击杀 ${d.totalKills} · 已历 ${d.games} 局`
      : "初入山林 · 祝武运昌隆";
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

  /** 重开一局时替换绑定的 sim 实例（新局存档可能已更新战绩）。 */
  resimulate(sim: GameSim): void {
    this.sim = sim;
    this.refreshSaveStats();
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
    const w = s.weather.state;
    const WNAMES: Record<string, string> = { Clear: "晴", Rain: "雨", Fog: "雾", BloodMoon: "血月" };
    this.weatherText.textContent =
      w.type === "Clear" ? "天气 晴" : `天气 ${WNAMES[w.type]}${w.intensity === "Moderate" ? "·烈" : ""}`;
    this.scoreText.textContent = `分数 ${s.score}`;
    this.killsText.textContent = `击杀 ${s.kills}`;
    this.timeText.textContent = `存活 ${s.survivalTime.toFixed(0)}s`;
    this.weaponText.textContent = s.activeWeapon ? `武器 ${s.activeWeapon.name}` : "徒手";
    this.welcome.classList.toggle("hidden", s.state !== "WeaponSelection");
    // GameOver 面板延迟 0.7s 弹出：先看玩家阵亡消散，再进结算
    if (s.state === "GameOver" && this.gameOverAt === 0) this.gameOverAt = performance.now();
    if (s.state !== "GameOver") this.gameOverAt = 0;
    const overReady = s.state === "GameOver" && performance.now() - this.gameOverAt > 700;
    const wasOver = this.gameover.classList.contains("hidden");
    this.gameover.classList.toggle("hidden", !overReady);
    if (!wasOver && overReady) {
      const coins = Math.floor(s.score / 50) + s.kills;
      this.gameoverStats.textContent =
        `分数 ${s.score} · 击杀 ${s.kills} · 存活 ${s.survivalTime.toFixed(0)}s · 威胁阶 ${s.round} · 金币 +${coins}`;
      this.refreshMeta();
    }
  }
}
