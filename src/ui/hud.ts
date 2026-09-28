// DOM UI 层：HUD、欢迎画面、GameOver 结算。
// 简笔画速写本风（纸面板 + 炭描边 + 血红点缀，对齐 gen-sketch 风格规范）。
// UI 快捷键约束：本层只用鼠标按钮，不占用任何游戏按键。

import type { GameSim } from "../sim/game";
import { DIFFICULTIES } from "../sim/difficulty";
import { PACTS, PACT_IDS, PACT_MAX_TIER, PACT_ROMAN, type PactId } from "../sim/pacts";
import { dailyKey, dailySeed, dailyTheme, dailyMutator } from "../sim/daily";
import { ROLES } from "../sim/roles";
import { WEAPON_DATA } from "../sim/weapons";
import type { LifetimeStats } from "../sim/save";
import { pactLabel } from "../sim/pacts";

/** R64 生涯时长：<1 小时显示 N 分钟，否则 N.N 小时。 */
export function formatLifetime(seconds: number): string {
  if (seconds < 3600) return `${Math.max(1, Math.floor(seconds / 60))} 分钟`;
  return `${(seconds / 3600).toFixed(1)} 小时`;
}

/** R64 欢迎页生涯陈列行（纯函数，vitest 直测）。 */
export function lifetimeLine(s: LifetimeStats): string {
  let fav = "";
  let favN = 0;
  for (const [id, n] of Object.entries(s.gamesByRole)) {
    if (n > favN) {
      favN = n;
      fav = ROLES[id as keyof typeof ROLES]?.name ?? id;
    }
  }
  return `生涯 · 存活 ${formatLifetime(s.timeAlive)} · 金币 ${s.coinsEarned} · 最高连击 ${s.maxCombo}`
    + ` · 单局击杀 ${s.maxKills} · 精英 ${s.totalElites} · 妖王 ${s.totalBosses}`
    + (favN > 0 ? ` · 最爱 ${fav}（${favN} 局）` : "");
}

/** 永久升级商店条目（meta 协议 shop 列表项）。 */
interface MetaShopItem { kind: string; name: string; desc: string; level: number; price: number; }

/** 濒危红边不透明度（R16 纯函数可单测）：血量 ≥35% 无显示，越低越红，空血拉满。 */
export function dangerOpacity(hpRatio: number): number {
  if (hpRatio >= 0.35) return 0;
  return Math.min(1, (0.35 - Math.max(0, hpRatio)) / 0.35);
}

/** 天气 HUD 标签（R55 纯函数可单测）：晴/无效果时简洁，异常天气附效果速记。 */
export function weatherHudLabel(type: string, intensity: string): string {
  const WNAMES: Record<string, string> = { Clear: "晴", Rain: "雨", Fog: "雾", BloodMoon: "血月" };
  const name = WNAMES[type] ?? type;
  const lv = intensity === "Moderate" ? "·烈" : "";
  // 效果速记（与 R54 播报同源语义，HUD 极简版）
  const effect: Record<string, string> = {
    Rain: "妖缓你缓",
    Fog: "视野窄·精英多",
    BloodMoon: "妖迅·精英多",
  };
  const eff = effect[type];
  return eff ? `天气 ${name}${lv}（${eff}）` : `天气 ${name}${lv}`;
}

/** 本局成就陈列行（R53 纯函数可单测）：无则空串，有则“本局成就 N 项：名 / 名”。 */
export function runAchievementLine(names: string[]): string {
  if (names.length === 0) return "";
  return `本局成就 ${names.length} 项：${names.join(" / ")}`;
}

/** 连战档位标签（R50 纯函数可单测）：0 档不显示，≥1 档显示“狂化 N 档 ×倍率”。 */
export function rushTierLabel(bossRush: boolean, killed: number, mult: number): string {
  if (!bossRush) return "";
  const tier = Math.floor(killed / 3);
  if (tier <= 0) return "";
  return `狂化 ${tier} 档 ×${mult.toFixed(2)}`;
}

/** 自定义种子解析（R22 纯函数可单测）：1~999999999 整数有效，否则 null（调用方回退随机）。 */
export function parseSeed(raw: string): number | null {
  const t = raw.trim();
  if (!/^\d{1,9}$/.test(t)) return null;
  const n = Number(t);
  return n >= 1 && n <= 999999999 ? n : null;
}

export interface ShareSummary {
  score: number; kills: number; survivalTime: number; round: number;
  roleName: string; difficultyName: string; seed: number;
  cleared: boolean;
  /** R52：挑战类型标识（Boss 连战 / 每日妖潮 / 空 = 普通局）。 */
  mode?: string;
  /** R52：血誓誓名串（空 = 未立誓）。 */
  pacts?: string;
  /** R52：连战狂化档位（0 = 未狂化/非连战）。 */
  rushTier?: number;
}

/** 战绩分享文案（R22 纯函数可单测）：单行速通分享格式，含模式/血誓/种子等复现信息。 */
export function shareText(s: ShareSummary): string {
  const mark = s.cleared ? "通关" : "陨落";
  const mode = s.mode ? ` · ${s.mode}` : "";
  const pacts = s.pacts ? ` · 血誓${s.pacts}` : "";
  const tier = s.rushTier && s.rushTier > 0 ? ` · 狂化${s.rushTier}档` : "";
  return `【斩妖录】${mark}${mode} · 分数${s.score} · 击杀${s.kills} · 存活${s.survivalTime.toFixed(0)}s` +
    ` · 威胁阶${s.round} · ${s.roleName}/${s.difficultyName}难度${pacts}${tier} · 种子${s.seed}`;
}

export class Hud {
  private root: HTMLElement;
  private hpFill!: HTMLElement;
  private hpText!: HTMLElement;
  private roundText!: HTMLElement;
  private difficultyText!: HTMLElement;
  private roleText!: HTMLElement;
  private weatherText!: HTMLElement;
  private scoreText!: HTMLElement;
  private killsText!: HTMLElement;
  private timeText!: HTMLElement;
  private weaponText!: HTMLElement;
  private dashDot!: HTMLElement;
  /** 继续无尽讨伐（Victory 面板按钮）。 */
  onEndless: (() => void) | null = null;
  /** 分享复制结果反馈（R22；装配层接 notify）。 */
  onShareCopy: ((ok: boolean) => void) | null = null;

  constructor(private sim: GameSim, private onStart: () => void, private onRestart: () => void) {
    this.root = document.createElement("div");
    this.root.id = "ui-root";
    document.getElementById("app")!.appendChild(this.root);
    // R16 濒危红边：盖在画布上、UI 之下，不拦截点击（同纸感叠加层级语义）
    const danger = document.createElement("div");
    danger.id = "danger-fx";
    document.getElementById("app")!.appendChild(danger);
    this.dangerEl = danger;
    this.buildHud();
    this.buildWelcome();
    this.buildVictory();
    this.buildGameOver();
  }

  private buildHud(): void {
    const hud = document.createElement("div");
    hud.id = "hud";
    hud.innerHTML = `
      <div class="hp-wrap"><div class="hp-bar"><div class="hp-fill" id="hp-fill"></div></div>
        <span id="hp-text"></span>
        <span id="dash-dot" title="冲刺就绪">⚡</span></div>
      <div class="stats">
        <span id="round"></span><span id="difficulty"></span><span id="role"></span><span id="weather"></span><span id="score"></span>
        <span id="kills"></span><span id="time"></span>
        <span id="weapon"></span>
      </div>`;
    this.root.appendChild(hud);
    this.hpFill = hud.querySelector("#hp-fill")!;
    this.hpText = hud.querySelector("#hp-text")!;
    this.roundText = hud.querySelector("#round")!;
    this.difficultyText = hud.querySelector("#difficulty")!;
    this.roleText = hud.querySelector("#role")!;
    this.weatherText = hud.querySelector("#weather")!;
    this.scoreText = hud.querySelector("#score")!;
    this.killsText = hud.querySelector("#kills")!;
    this.timeText = hud.querySelector("#time")!;
    this.weaponText = hud.querySelector("#weapon")!;
    this.dashDot = hud.querySelector("#dash-dot")!;
  }

  private welcome!: HTMLElement;
  private gameoverStats!: HTMLElement;
  /** 结算屏根（R80 手柄「再来一局」导航容器；buildGameOver 懒构建赋值）。 */
  gameover!: HTMLElement;
  /** 胜利结算屏根（R80 手柄「再来一局」导航容器；buildVictory 懒构建赋值）。 */
  victory!: HTMLElement;
  private victoryStats!: HTMLElement;
  private victoryDebrief!: HTMLElement;
  private victoryCoins!: HTMLElement;
  private victoryAchievements!: HTMLElement;
  private gameoverDebrief!: HTMLElement;
  private gameoverAchievements!: HTMLElement;
  private dangerEl!: HTMLElement;
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
        <div class="role-cards" id="role-cards"></div>
        <div class="diff-row" id="diff-row"></div>
        <div class="pact-row" id="pact-row"></div>
        <p id="save-stats" class="sub"></p>
        <p id="save-life" class="sub"></p>
        <p class="sub seed-row">种子 <span id="seed-now"></span> · 自定 <input id="seed-input" type="text" inputmode="numeric" placeholder="留空随机" />（填入则下局用该种子，可复现）</p>
        <p class="sub daily-row">今日妖潮 · 种子 <span id="daily-seed"></span><span id="daily-best"></span> <button id="btn-daily" type="button">挑战今日</button> <button id="btn-boss-rush" type="button">Boss 连战</button><span id="rush-best"></span></p>
        <button id="btn-start" type="button">开始游戏</button>
        <p class="hint">WASD 移动 · 空格 攻击 · Shift 冲刺</p>
      </div>`;
    this.root.appendChild(this.welcome);
    this.welcome.querySelector("#btn-start")!.addEventListener("click", () => this.onStart());
    this.buildRoleCards();
    this.buildDiffRow();
    this.buildPactRow();
    this.refreshSaveStats();
    this.refreshSeedRow();
    this.refreshDailyRow();
    this.welcome.querySelector("#btn-daily")!.addEventListener("click", () => {
      // R26 一键挑战今日：填入当日种子后走正常开局流程（职业/难度/血誓选择照常）
      const input = this.welcome.querySelector<HTMLInputElement>("#seed-input");
      if (input) input.value = String(dailySeed());
      this.pendingBossRush = false;
      this.onStart();
    });
    this.welcome.querySelector("#btn-boss-rush")!.addEventListener("click", () => {
      // R44 Boss 连战：一键进入妖王轮战（职业/难度/血誓照常，血誓加难结算更肥）
      this.pendingBossRush = true;
      this.onStart();
    });
  }

  /** Boss Rush 开关（R44；欢迎页一键，走正常 onStart 由装配层读此标记）。 */
  pendingBossRush = false;

  /** 今日妖潮行（R26）：当日种子 + 同日最佳（跨天自动换榜）。 */
  private refreshDailyRow(): void {
    const seedEl = this.welcome.querySelector<HTMLElement>("#daily-seed");
    const bestEl = this.welcome.querySelector<HTMLElement>("#daily-best");
    const rushEl = this.welcome.querySelector<HTMLElement>("#rush-best");
    if (!seedEl || !bestEl || !rushEl) return;
    const theme = dailyTheme();
    seedEl.textContent = String(dailySeed());
    const d = this.sim.exec({ action: "meta" }).data as {
      dailyBest: { date: string; score: number };
      rushBest: number;
      mutatorClears?: Record<string, number>;
    };
    // R73 每日变异：同日同变异（欢迎页一眼可辨今天怎么打）；R74 历劫进度 x/5
    const mut = dailyMutator();
    const done = Object.keys(d.mutatorClears ?? {}).length;
    bestEl.textContent =
      ` · ${theme.label} · 变异${mut.name} · 历劫 ${done}/5` + (d.dailyBest?.date === dailyKey() ? ` · 今日最佳 ${d.dailyBest.score}` : "");
    rushEl.textContent = d.rushBest > 0 ? ` · 连战最佳 ${d.rushBest}` : "";
  }

  /** 当前种子展示（R22；重开/新局后刷新）。 */
  private refreshSeedRow(): void {
    const el = this.welcome.querySelector<HTMLElement>("#seed-now");
    if (el) el.textContent = String(this.sim.seed);
  }

  /** 待用自定义种子（R22；输入有效则返回，否则 null 由装配层回退随机）。 */
  pendingSeed(): number | null {
    const input = this.welcome.querySelector<HTMLInputElement>("#seed-input");
    if (!input) return null;
    return parseSeed(input.value);
  }

  /** 欢迎页难度选择（R10）：三档小按钮，纯鼠标；未解锁置灰并印条件。 */
  private buildDiffRow(): void {
    const wrap = this.welcome.querySelector<HTMLElement>("#diff-row")!;
    wrap.innerHTML = "";
    for (const d of Object.values(DIFFICULTIES)) {
      const b = document.createElement("button");
      b.type = "button";
      b.dataset.diff = String(d.id);
      const open = this.sim.isDifficultyUnlocked(d.id);
      b.className = open ? "diff-btn" : "diff-btn locked";
      const need = d.unlockBy === null ? null : DIFFICULTIES[d.unlockBy];
      b.innerHTML = `<b>${d.name}${open ? "" : " 🔒"}</b><i>${open ? d.desc : `通关${need!.name}解锁`}</i>`;
      b.addEventListener("click", () => {
        if (!this.sim.isDifficultyUnlocked(d.id)) return;
        this.sim.setDifficulty(d.id);
        this.refreshDiffRow();
      });
      wrap.appendChild(b);
    }
    this.refreshDiffRow();
  }

  /** 欢迎页血誓分级（R70 热量式）：三誓芯片，点击升档（0→1→2→3→0 循环），无锁全开放。 */
  private buildPactRow(): void {
    const wrap = this.welcome.querySelector<HTMLElement>("#pact-row")!;
    wrap.innerHTML = "";
    for (const id of PACT_IDS) {
      const d = PACTS[id];
      const b = document.createElement("button");
      b.type = "button";
      b.dataset.pact = id;
      b.className = "pact-btn";
      b.innerHTML = `<b>${d.name}</b><i></i>`;
      b.addEventListener("click", () => {
        const next = (this.sim.pactTier(id) + 1) % (PACT_MAX_TIER + 1);
        this.sim.setPactTiers(next > 0 ? { [id]: next } : { [id]: 0 });
        this.refreshPactRow();
      });
      wrap.appendChild(b);
    }
    this.refreshPactRow();
  }

  /** 按当前 sim 血誓档位重绘卡片（R70：名 + 罗马档徽 + 本档效果；未立誓灰显）。 */
  private refreshPactRow(): void {
    this.welcome.querySelectorAll<HTMLElement>(".pact-btn").forEach((x) => {
      const id = x.dataset.pact as PactId;
      const tier = this.sim.pactTier(id);
      const d = PACTS[id];
      x.classList.toggle("selected", tier >= 1);
      x.querySelector("b")!.textContent = tier >= 1 ? `${d.name}${PACT_ROMAN[tier]}` : d.name;
      x.querySelector("i")!.textContent = tier >= 1 ? d.tiers[tier - 1].desc : "点击立誓 · 再点升档";
    });
  }

  /** 按当前 sim 难度高亮选中钮。 */
  private refreshDiffRow(): void {
    const cur = String(this.sim.difficulty.id);
    this.welcome.querySelectorAll<HTMLElement>(".diff-btn").forEach((x) =>
      x.classList.toggle("selected", x.dataset.diff === cur));
  }

  /** 欢迎页职业选择卡（R3 纯鼠标选卡；R6 未解锁置灰并明示条件，点击不切换）。 */
  private buildRoleCards(): void {
    const wrap = this.welcome.querySelector<HTMLElement>("#role-cards")!;
    wrap.innerHTML = "";
    for (const r of Object.values(ROLES)) {
      const card = document.createElement("button");
      card.type = "button";
      card.dataset.role = r.id;
      const open = this.sim.isRoleUnlocked(r.id);
      card.className = open ? "role-card" : "role-card locked";
      const hint = open ? null : this.sim.roleUnlockHint(r.id);
      card.innerHTML = `
        <b>${r.name}${open ? "" : " 🔒"}</b>
        <span class="rc-weapon">起手 ${WEAPON_DATA[r.startWeapon].name}</span>
        <span class="rc-passive">${r.passive}</span>
        <span class="rc-desc">${hint ? `${hint.name}：${hint.desc}` : r.desc}</span>`;
      card.addEventListener("click", () => {
        if (!this.sim.isRoleUnlocked(r.id)) return;
        this.sim.setRole(r.id);
        this.refreshRoleCards();
      });
      wrap.appendChild(card);
    }
    this.refreshRoleCards();
  }

  /** 按当前 sim 职业高亮选中卡。 */
  private refreshRoleCards(): void {
    const cur = this.sim.role.id;
    this.welcome.querySelectorAll<HTMLElement>(".role-card").forEach((c) =>
      c.classList.toggle("selected", c.dataset.role === cur));
  }

  /** 欢迎页历史战绩（最高分/总击杀/局数 + R64 生涯行），零局时显示引言。 */
  private refreshSaveStats(): void {
    const el = this.welcome.querySelector<HTMLElement>("#save-stats");
    if (!el) return;
    const life = this.welcome.querySelector<HTMLElement>("#save-life");
    const d = this.sim.exec({ action: "meta" }).data as {
      highScore: number; totalKills: number; games: number; clears: number;
      stats?: LifetimeStats;
    };
    el.textContent = d.games > 0
      ? `最高分 ${d.highScore} · 总击杀 ${d.totalKills} · 已历 ${d.games} 局 · 制霸 ${d.clears}/9`
      : "初入山林 · 祝武运昌隆";
    if (life) life.textContent = d.games > 0 && d.stats ? lifetimeLine(d.stats) : "";
  }

  private buildVictory(): void {
    this.victory = document.createElement("div");
    this.victory.className = "overlay hidden";
    this.victory.innerHTML = `
      <div class="panel">
        <h1>妖潮平定</h1>
        <p class="sub">终幕妖王授首 · 山林复归安宁</p>
        <p id="victory-stats" class="sub"></p>
        <p id="victory-debrief" class="sub"></p>
        <p id="victory-coins" class="sub"></p>
        <p id="victory-achievements" class="sub ach-line"></p>
        <button id="btn-endless" type="button">继续讨伐（无尽）</button>
        <button id="btn-victory-restart" type="button">再来一局</button>
        <button id="btn-victory-share" type="button">复制战绩</button>
        <p class="hint">按 R 再来一局</p>
      </div>`;
    this.root.appendChild(this.victory);
    this.victoryStats = this.victory.querySelector("#victory-stats")!;
    this.victoryDebrief = this.victory.querySelector("#victory-debrief")!;
    this.victoryCoins = this.victory.querySelector("#victory-coins")!;
    this.victoryAchievements = this.victory.querySelector("#victory-achievements")!;
    this.victory.querySelector("#btn-endless")!.addEventListener("click", () => this.onEndless?.());
    this.victory.querySelector("#btn-victory-restart")!.addEventListener("click", () => this.onRestart());
    this.victory.querySelector("#btn-victory-share")!.addEventListener("click", () => this.shareRun(true));
  }

  private refreshVictory(): void {
    const s = this.sim;
    const coins = Math.floor((s.score / 50 + s.kills) * 1.5);
    this.victoryStats.textContent =
      `分数 ${s.score} · 击杀 ${s.kills} · 用时 ${s.survivalTime.toFixed(0)}s · 威胁阶 ${s.round} · ${s.difficulty.name}难度` +
      (s.bossRush ? " · Boss 连战" : s.seed === dailySeed() ? " · 每日妖潮" : "") +
      (s.pactScoreMult() > 1 ? ` · 血誓×${s.pactScoreMult().toFixed(2)}` : "");
    this.victoryDebrief.textContent = this.debriefText(s);
    this.victoryCoins.textContent = `结算金币 ×1.5 → +${coins}`;
    // R53 本局成就陈列（R48 补授后结算已含终局同 tick 的成就）
    this.victoryAchievements.textContent = runAchievementLine(s.runAchievements.map((a) => a.name));
  }

  private buildGameOver(): void {
    this.gameover = document.createElement("div");
    this.gameover.className = "overlay hidden";
    this.gameover.innerHTML = `
      <div class="panel">
        <h1>除妖师陨落</h1>
        <p id="gameover-stats" class="sub"></p>
        <p id="gameover-debrief" class="sub"></p>
        <p id="gameover-achievements" class="sub ach-line"></p>
        <div class="meta-row" id="meta-row"></div>
        <p id="meta-coins" class="sub"></p>
        <button id="btn-restart" type="button">再来一局</button>
        <button id="btn-gameover-share" type="button">复制战绩</button>
        <p class="hint">按 R 再来一局</p>
      </div>`;
    this.root.appendChild(this.gameover);
    this.gameoverStats = this.gameover.querySelector("#gameover-stats")!;
    this.gameoverDebrief = this.gameover.querySelector("#gameover-debrief")!;
    this.gameoverAchievements = this.gameover.querySelector("#gameover-achievements")!;
    this.gameover.querySelector("#btn-restart")!.addEventListener("click", () => this.onRestart());
    this.gameover.querySelector("#btn-gameover-share")!.addEventListener("click", () => this.shareRun(false));
  }

  private fetchMeta(): { coins: number; shop: MetaShopItem[] } {
    return this.sim.exec({ action: "meta" }).data as { coins: number; shop: MetaShopItem[] };
  }

  private metaLabel(item: MetaShopItem): string {
    return `${item.name} ${item.desc}（Lv.${item.level} · ${item.price}金）`;
  }

  /** 永久升级商店（R5，6 维）：面板弹出时建按钮；购买只原位更新文案不换节点
   *  （重建会摘除点击中的按钮并丢焦点）。纯鼠标交互。 */
  private refreshMeta(): void {
    const m = this.fetchMeta();
    const row = this.gameover.querySelector<HTMLElement>("#meta-row")!;
    row.innerHTML = "";
    for (const item of m.shop) {
      const b = document.createElement("button");
      b.type = "button";
      b.dataset.kind = item.kind;
      b.textContent = this.metaLabel(item);
      b.addEventListener("click", () => {
        this.sim.exec({ action: "buy_meta", args: { kind: item.kind } });
        const m2 = this.fetchMeta();
        const updated = m2.shop.find((s) => s.kind === item.kind);
        if (updated) b.textContent = this.metaLabel(updated);
        (this.gameover.querySelector("#meta-coins") as HTMLElement).textContent = `金币 ${m2.coins}`;
      });
      row.appendChild(b);
    }
    (this.gameover.querySelector("#meta-coins") as HTMLElement).textContent = `金币 ${m.coins}`;
  }

  /** 战绩复盘行（R9；对标 VS 结算屏）：精英讨伐/最高连击/收集武器。 */
  /** 连战战报头（R60 纯函数可单测）：非连战空串，连战显示讨伐妖王数与狂化档位。 */
  static rushHead(bossRush: boolean, kills: number, tier: number): string {
    if (!bossRush) return "";
    return `讨伐妖王 ${kills} 只 · 狂化 ${tier} 档`;
  }

  private debriefText(s: GameSim): string {
    // R42 天气回顾：驻留 ≥10s 的天气按时长降序陈列
    const entries = Object.entries(s.weatherTime)
      .filter(([, t]) => t >= 10)
      .sort((a, b) => b[1] - a[1]);
    const WN: Record<string, string> = { Clear: "晴", Rain: "雨", Fog: "雾", BloodMoon: "血月" };
    const weather = entries.length > 0
      ? ` · 历${entries.map(([type, t]) => `${WN[type] ?? type} ${Math.round(t)}s`).join(" / ")}`
      : "";
    // R43 异变回顾：触发过的异变按次数降序陈列
    const EN: Record<string, string> = { surge: "合围", hunt: "精锐", cache: "宝藏", goblin: "宝妖", market: "庙会" };
    const tally = Object.entries(s.eventTally)
      .filter(([, n]) => n > 0)
      .sort((a, b) => b[1] - a[1]);
    const events = tally.length > 0
      ? ` · 异变${tally.map(([k, n]) => `${EN[k] ?? k}×${n}`).join(" / ")}`
      : "";
    // R66 香火钱复盘：本局局内经济收支（有收支才陈列，零花销不占行宽）
    const gold = s.runGoldSpent > 0 || s.runGoldEarned > 0
      ? ` · 香火钱 +${s.runGoldEarned}/-${s.runGoldSpent}`
      : "";
    // R60 连战战报：讨伐妖王数与狂化档位置顶（连战模式的核心战绩）
    const rush = Hud.rushHead(s.bossRush, s.bossRushKills, Math.floor(s.bossRushKills / 3));
    const head = rush ? `${rush} · ` : "";
    return `${head}精英讨伐 ${s.eliteKills} · 最高连击 ${s.maxCombo} · 武器 ${s.player.weapons.length} 件${weather}${events}${gold}`;
  }

  /** 复制本局战绩（R22）：文案含种子可复现；结果经 onShareCopy 反馈通知条。 */
  private shareRun(cleared: boolean): void {
    const s = this.sim;
    // R52：挑战类型/血誓/狂化档位入文案（战队成绩单自解释）
    const mode = s.bossRush ? "Boss 连战" : s.seed === dailySeed() ? "每日妖潮" : "";
    const pacts = Object.entries(s.pactTiers)
      .filter(([, t]) => (t ?? 0) >= 1)
      .sort((a, b) => PACT_IDS.indexOf(a[0] as PactId) - PACT_IDS.indexOf(b[0] as PactId))
      .map(([id, t]) => pactLabel(PACTS[id as PactId].name, t ?? 0))
      .join("·");
    const text = shareText({
      score: s.score, kills: s.kills, survivalTime: s.survivalTime, round: s.round,
      roleName: s.role.name, difficultyName: s.difficulty.name, seed: s.seed, cleared,
      mode, pacts, rushTier: Math.floor(s.bossRushKills / 3),
    });
    try {
      const done = navigator.clipboard?.writeText(text);
      if (done && typeof (done as Promise<void>).then === "function") {
        (done as Promise<void>).then(() => this.onShareCopy?.(true), () => this.onShareCopy?.(false));
      } else {
        this.onShareCopy?.(false);
      }
    } catch {
      this.onShareCopy?.(false);
    }
  }

  /** 重开一局时替换绑定的 sim 实例（新局存档可能已更新战绩/解锁）。 */
  resimulate(sim: GameSim): void {
    this.sim = sim;
    this.refreshSaveStats();
    this.refreshSeedRow();
    this.refreshDailyRow();
    this.buildRoleCards();
    this.buildDiffRow();
    this.buildPactRow();
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
    // R16 濒危红边：血量越低红边越重（只读 sim，不拦截输入）
    this.dangerEl.style.opacity = String(dangerOpacity(ratio));
    this.hpText.textContent = `${Math.ceil(s.player.hp)} / ${s.player.maxHp}`;
    // R9 威胁预告：下一阶倒计时（终幕后无尽讨伐同样进阶，倒计时持续有效）
    // R50 连战档位：连战模式达到 1 档起在威胁阶后追加狂化徽标
    const tierTag = rushTierLabel(s.bossRush, s.bossRushKills, s.rushEscalationMult());
    this.roundText.textContent = `威胁阶 ${s.round} · ${Math.ceil(s.secsToNextTier)}s 后进阶` +
      (tierTag ? ` · ${tierTag}` : "");
    // R25 血誓公示：有立誓则难度后追加誓名
    const oath = Object.entries(s.pactTiers)
      .filter(([, t]) => (t ?? 0) >= 1)
      .sort((a, b) => PACT_IDS.indexOf(a[0] as PactId) - PACT_IDS.indexOf(b[0] as PactId))
      .map(([id, t]) => pactLabel(PACTS[id as PactId].name, t ?? 0))
      .join("·");
    this.difficultyText.textContent = oath ? `难度 ${s.difficulty.name}·${oath}` : `难度 ${s.difficulty.name}`;
    this.roleText.textContent = `职业 ${s.role.name}`;
    // R55 天气 HUD：异常天气附效果速记（R54 播报的常驻版）
    const w = s.weather.state;
    this.weatherText.textContent = weatherHudLabel(w.type, w.intensity);
    this.scoreText.textContent = `分数 ${s.score}`;
    this.killsText.textContent = `击杀 ${s.kills}`;
    // R65 局内金币：香火龛流通币（结算金币另计，两轨展示防混淆）
    const coinsEl = this.root.querySelector<HTMLElement>("#coins");
    if (coinsEl) coinsEl.textContent = `金币 ${s.coins}`;
    // R18 幕显示：R2 分幕结构此前 HUD 无感知，此处点名
    this.timeText.textContent = `存活 ${s.survivalTime.toFixed(0)}s · 第${s.act + 1}幕`;
    this.weaponText.textContent = s.activeWeapon
      ? `武器 ${s.activeWeapon.name}${s.player.combo >= 3 ? ` · 连击 ${s.player.combo}` : ""}`
      : "徒手";
    // 冲刺指示：就绪=血红实心；冷却=铅笔灰并降透明度
    const ready = s.player.dashCd <= 0;
    this.dashDot.textContent = ready ? "⚡" : "·";
    this.dashDot.style.color = ready ? "#c0392b" : "#b0a894";
    this.dashDot.style.opacity = ready ? "1" : "0.55";
    this.welcome.classList.toggle("hidden", s.state !== "WeaponSelection");
    // Victory 结算：终幕妖王陨落即弹出
    this.victory.classList.toggle("hidden", s.state !== "Victory");
    if (s.state === "Victory" && this.victoryStats.textContent === "") this.refreshVictory();
    if (s.state !== "Victory") this.victoryStats.textContent = "";
    // GameOver 面板延迟 0.7s 弹出：先看玩家阵亡消散，再进结算
    if (s.state === "GameOver" && this.gameOverAt === 0) this.gameOverAt = performance.now();
    if (s.state !== "GameOver") this.gameOverAt = 0;
    const overReady = s.state === "GameOver" && performance.now() - this.gameOverAt > 700;
    const wasOver = this.gameover.classList.contains("hidden");
    this.gameover.classList.toggle("hidden", !overReady);
    // R5 修正：只在 hidden→visible 的过渡帧触发一次（旧条件 !wasOver 在面板
    // 显示后每帧为真，导致 refreshMeta 每帧重建按钮、点击永远落空）
    if (wasOver && overReady) {
      const coins = Math.floor(s.score / 50) + s.kills;
      this.gameoverStats.textContent =
        `分数 ${s.score} · 击杀 ${s.kills} · 存活 ${s.survivalTime.toFixed(0)}s · 威胁阶 ${s.round} · ${s.difficulty.name}难度 · 金币 +${coins} · 死于${s.deathCause ?? "妖潮"}` +
        (s.bossRush ? " · Boss 连战" : s.seed === dailySeed() ? " · 每日妖潮" : "") +
        (s.pactScoreMult() > 1 ? ` · 血誓×${s.pactScoreMult().toFixed(2)}` : "");
      this.gameoverDebrief.textContent = this.debriefText(s);
      this.gameoverAchievements.textContent = runAchievementLine(s.runAchievements.map((a) => a.name));
      this.refreshMeta();
    }
  }
}
