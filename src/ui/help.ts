// 帮助面板（H 键）：操作说明 / 妖物图鉴 / 武器图鉴 / 成就 / 机制。模态暂停。
// H 为 toggle_help 已绑定游戏键——打开期间游戏输入由 sim 门控 100% 失效（项目硬约束）。

import type { GameSim } from "../sim/game";
import type { WeaponDef } from "../sim/weapons";
import { ENEMY_NAMES, ENEMY_STATS } from "../sim/config";

/** R195 图鉴缩略图：妖物 walk sheet 条带（与 view.ts ENEMY_ATLAS 同源命名）。 */
const ENEMY_SHEET: Record<number, string> = {
  0: "enemy_basic_walk", 1: "enemy_fast_walk", 2: "enemy_tank_walk",
  3: "enemy_exploder_walk", 4: "enemy_invisible_walk", 5: "enemy_spitter_walk",
  6: "enemy_healer_walk", 7: "enemy_summoner_walk", 8: "enemy_archer_walk",
  9: "enemy_ship_walk", 10: "enemy_mech_walk", 11: "enemy_knight_walk",
  12: "enemy_dragon_walk", 13: "enemy_swarm_walk",
};
import { CODEX_GOLD_THRESHOLD } from "../sim/game";
import { WEAPON_DATA, FUSION_RECIPES, ASPECTS, ASCEND_AT } from "../sim/weapons";
import { masteryTier } from "../sim/mastery";;
import { ACHIEVEMENTS, ACHIEVEMENT_GROUPS, achievementProgress, HONOR_ACHIEVEMENT_NAMES } from "../sim/achievements";
import { AFFIXES, AFFIX_IDS } from "../sim/affixes";
import { BOSSES, BOSS_BARRAGE_INFO } from "../sim/bosses";
import { FOE_MECHS } from "../sim/foemechs";
import { PACTS, pactLabel } from "../sim/pacts";
import { shouldUseTouch } from "./touch";
import { dailyTheme } from "../sim/daily";

export class HelpPanel {
  /** R149 手柄 pad nav 容器（面板根）。 */
  readonly el: HTMLElement;
  private page = 0;
  private bodyEl!: HTMLElement;
  visible = false;

  private pages = ["操作", "战绩", "统计", "妖物图鉴", "词缀图鉴", "妖王图鉴", "武器图鉴", "成就", "机制"];

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
    if (shouldUseTouch()) {
      const hint = this.el.querySelector<HTMLElement>(".hint");
      if (hint) hint.textContent = "点击空白处关闭 · 点上方页签切换";
    }
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

  /** R167 深链：打开面板并切到指定页（按名查找防索引漂移；未知名回操作页）。 */
  showPage(name: string): void {
    const idx = this.pages.indexOf(name);
    this.page = idx >= 0 ? idx : 0;
    this.toggle(true);
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
    switch (this.pages[this.page]) {
      case "操作": this.renderControls(); break;
    case "战绩": this.renderRunHistory(); break;
    case "统计": this.renderLifetimeStats(); break;
      case "妖物图鉴": this.renderEnemyCodex(); break;
      case "词缀图鉴": this.renderAffixCodex(); break;
      case "妖王图鉴": this.renderBossCodex(); break;
      case "武器图鉴": this.renderWeaponCodex(); break;
      case "成就": this.renderAchievements(); break;
      case "机制": this.renderMechanics(); break;
    }
  }

  private renderControls(): void {
    // 触屏设备展示触屏操作；桌面展示键位
    const rows = shouldUseTouch()
      ? [
        ["移动", "左侧摇杆拖动"],
        ["攻击", "击钮（按住连击）"],
        ["冲刺", "冲钮"],
        ["装备 / 合成 / 手账 / 暂停", "左上按钮排"],
      ]
      : [
        ["移动", "WASD / 方向键"],
        ["攻击", "空格（按住连击，全部武器齐射）"],
        ["冲刺", "Shift"],
        ["手柄", "左摇杆/十字键移动 · A 攻击 · RB/RT 冲刺 · X 装备 · Y 合成 · LB/RB 帮助翻页 · Start 暂停"],
        ["香火龛", "定期浮现 · 走近以金币换机缘（1/2/3 购买 · Esc 离开）"],
        ["装备 / 合成", "C / I"],
        ["帮助 / 暂停", "H / Esc"],
        ["静音", "M（再按恢复）"],
      ];
    this.bodyEl.innerHTML = `
      <table class="help-table">
        ${rows.map(([k, v]) => `<tr><td>${k}</td><td>${v}</td></tr>`).join("")}
        <tr><td>方向攻击</td><td>近战朝移动方向挥砍，锥内妖物自动吸附；站桩时自动瞄向最后朝向</td></tr>
        <tr><td>开局职业</td><td>欢迎页三选一：剑修（伤害+15%，开局即开放）/ 弓手（攻速+15%、冲刺冷却-20%，累计击杀50解锁）/ 术士（范围+20%、拾取+50%，讨伐妖王解锁），各带专属起手武器</td></tr>
        <tr><td>采集</td><td>近战攻击范围内的树、岩、灌木</td></tr>
        <tr><td>冲刺斩</td><td>冲刺中攻击：伤害 1.5×，近战范围扩大，弓+1 矢、法术+1 弹</td></tr>
        <tr><td>完美闪避</td><td>冲刺中受击则闪避：时缓 0.9s + 3s 内伤害 1.3×</td></tr>
        <tr><td>升级重抽 / 跳过</td><td>符箓三选一可 R 重抽（每局 2 次）或 X 跳过（回复 15 生命）；触屏点面板按钮</td></tr>
        <tr><td>升级放逐</td><td>卡上「放逐」按钮或 4 / 5 / 6 键：移除该武器×词条组合（本局不再出现，每局 1 次），并重补一张</td></tr>
      </table>`;
  }

  /** 妖物图鉴（R15 计数 / R27 金边 / R33 汇总行 / R58 机制列）：三维 + 机制应对 + 讨伐数。 */
  private renderEnemyCodex(): void {
    const codex = this.sim.exec({ action: "codex" }).data as {
      run: Record<number, number>; total: Record<number, number>;
    };
    const ids = Object.keys(ENEMY_NAMES).map(Number);
    const goldCount = ids.filter((id) => (codex.total[id] ?? 0) >= CODEX_GOLD_THRESHOLD).length;
    const seenCount = ids.filter((id) => (codex.total[id] ?? 0) > 0).length;
    const totalKills = ids.reduce((n, id) => n + (codex.total[id] ?? 0), 0);
    this.bodyEl.innerHTML = `<p class="sub">点亮 ${seenCount}/${ids.length} · 镀金 ${goldCount}/${ids.length}（单类满 ${CODEX_GOLD_THRESHOLD} 杀） · 总讨伐 ${totalKills}</p>
    <table class="help-table">
      ${ids.map((id) => {
        const s = ENEMY_STATS[id];
        const run = codex.run[id] ?? 0;
        const total = codex.total[id] ?? 0;
        const gold = total >= CODEX_GOLD_THRESHOLD;
        const m = FOE_MECHS[id];
        const sheet = ENEMY_SHEET[id];
        const thumb = sheet
          ? `<br><img class="codex-thumb" loading="lazy" src="assets/sprites/enemies/${sheet}.png" alt="" draggable="false" onerror="this.remove()">`
          : "";
        return `<tr${gold ? ` class="codex-gold"` : ""}><td>${ENEMY_NAMES[id]}${thumb}${gold ? `<i class="w-star">★</i>` : ""}</td>` +
          `<td>生命 ${s.hp} · 伤害 ${s.dmg} · 速度 ${s.speed}<br>` +
          `<span class="w-tags">${m.mech}</span><br><span class="w-fuse">应对：${m.counter}</span></td>` +
          `<td>本局 ${run} · 累计 ${total}</td></tr>`;
      }).join("")}
    </table>`;
  }

  /** 武器机制字段 → 中文标签（图鉴可查阅性）。 */
  private weaponTags(d: WeaponDef): string {
    const t: string[] = [];
    if (d.dmg) t.push(`伤×${d.dmg}`);
    if (d.asp) t.push(`速×${d.asp}`);
    if (d.rng) t.push(`程×${d.rng}`);
    if (d.spread) t.push(`散射×${d.spread}`);
    if (d.pierce) t.push(`穿透×${d.pierce}`);
    if (d.homing) t.push("追踪");
    if (d.chain) t.push(`链式×${d.chain}`);
    if (d.splash) t.push(`溅射${d.splash}`);
    if (d.psn) t.push(`中毒${d.psn}%`);
    if (d.freeze) t.push(`冰缓${d.freeze}%`);
    if (d.crit) t.push(`暴击+${d.crit}%`);
    if (d.multi && d.multi > 1) t.push(`多目标×${d.multi}`);
    return t.join(" · ");
  }

  /** 词缀图鉴（R56）：七词缀的徽标形状 / 行为 / 应对，全部从 AFFIXES 表派生。 */
  private renderAffixCodex(): void {
    const rows = AFFIX_IDS.map((id) => {
      const a = AFFIXES[id];
      return `<tr><td>${a.name}</td>` +
        `<td>徽标：${a.badge}<br><span class="w-tags">${a.effect}</span><br>` +
        `<span class="w-fuse">应对：${a.counter}</span></td></tr>`;
    });
    this.bodyEl.innerHTML = `<p class="sub">精英妖物头顶徽标一览（共 ${AFFIX_IDS.length} 种）</p>` +
      `<table class="help-table">${rows.join("")}</table>`;
  }

  /** 妖王图鉴（R57）：五王轮换顺序 + 原生机制 + 应对，全部从 BOSSES 表派生。 */
  private renderBossCodex(): void {
    const rows = BOSSES.map((b, i) => {
      return `<tr><td>${i + 1}. ${b.name}王</td>` +
        `<td>${b.mech}<br><span class="w-fuse">应对：${b.counter}</span></td></tr>`;
    });
    const bar = BOSS_BARRAGE_INFO;
    this.bodyEl.innerHTML =
      `<p class="sub">妖王按轮换次序登场（每 5 阶一只，第 13 阶终幕）· 通用弹幕：每 ${bar.cd}s 放 ${bar.count[0]}~${bar.count[2]} 发环形弹</p>` +
      `<table class="help-table">${rows.join("")}</table>`;
  }

  /** 武器藏珍（R30 图鉴迷雾 / R35 终极标记）：拥有过点亮，达过终极挂徽。 */
  private renderWeaponCodex(): void {
    const TYPE_NAMES: Record<WeaponDef["type"], string> = { Melee: "近战", Ranged: "远程", Magic: "魔法", Mine: "地雷" };
    const order: Array<WeaponDef["type"]> = ["Melee", "Ranged", "Magic", "Mine"];
    const owned = new Set((this.sim.exec({ action: "armory" }).data as { owned: string[] }).owned);
    // R196 生涯击杀（R195 数据消费端）：每把已持有武器显示累计击杀
    const wkills = (this.sim.exec({ action: "status" }).data as { weapon_kills?: Record<string, number> }).weapon_kills ?? {};
    const ult = new Set((this.sim.exec({ action: "ultimates" }).data as { reached: string[] }).reached);
    const fusionable = new Set(FUSION_RECIPES.flatMap((r) => [r.a, r.b]));
    const rows = order.flatMap((type) =>
      Object.entries(WEAPON_DATA)
        .filter(([, d]) => d.type === type)
        .sort((a, b) => a[1].tier - b[1].tier)
        .map(([id, d]) => {
          if (!owned.has(id)) {
            return `<tr class="armory-unknown"><td>？？？</td><td>[${TYPE_NAMES[d.type]}] 未曾持有</td></tr>`;
          }
          const stars = "★".repeat(d.rarity);
          const fuse = fusionable.has(d.name) ? " · 可祭炼" : "";
          const ulti = ult.has(id) ? `<span class="w-ult">终极</span>` : "";
          const wk = wkills[id] ?? 0;
          const wkTag = owned.has(id) && wk > 0 ? `<span class="w-kill">${masteryTier(wk).title} · ${wk} 杀</span>` : "";
          const tags = this.weaponTags(d);
          // R109 形态入鉴：形态名 + 描述 + 解锁条件（进阶）
          const a = ASPECTS[id];
          const aspText = a ? `<br><span class="w-fuse">形态贰·${a.name}（升 ${ASCEND_AT} 次解锁）：${a.desc}</span>` : "";
          return `<tr><td>${d.name}${ulti}${wkTag}<i class="w-star">${stars}</i></td>` +
            `<td>[${TYPE_NAMES[d.type]}] ${d.desc}${tags ? `<br><span class="w-tags">${tags}</span>` : ""}${fuse ? `<span class="w-fuse">${fuse}</span>` : ""}${aspText}</td></tr>`;
        }));
    this.bodyEl.innerHTML = `<table class="help-table">${rows.join("")}</table>`;
  }

  /** 成就页（R51 分组）：按四条线分组渲染，每组标注该组进度。 */
  /** R102 本地战绩：最近 10 局（分数/击杀/用时/角色难度/模式/种子），新的在前。 */
  private renderRunHistory(): void {
    const d = this.sim.status() as unknown as {
      run_history?: Array<{ score: number; kills: number; survivalTime: number; round: number; roleName: string; difficultyName: string; mode: string; seed: number; date: string; cleared: boolean; achievements?: string[] }>;
    };
    const list = d.run_history ?? [];
    if (list.length === 0) {
      this.bodyEl.innerHTML = `<p class="sub">暂无战绩——完成一局后这里会留下记录。</p>`;
      return;
    }
    const rows = list.map((r) =>
      `<tr><td>${r.score}</td><td>${r.kills}</td><td>${r.survivalTime}s</td>` +
      `<td>${r.roleName}/${r.difficultyName}${r.mode ? ` · ${r.mode}` : ""}${r.cleared ? " · 通关" : ""}${r.achievements?.length ? ` · 🏆${r.achievements.length}` : ""}</td>` +
      `<td>${r.seed}</td><td>${r.date}</td></tr>`).join("");
    this.bodyEl.innerHTML =
      `<p class="sub">最近 ${list.length} 局（本机）</p>` +
      `<table class="help-table"><tr><th>分数</th><th>击杀</th><th>用时</th><th>职业/难度</th><th>种子</th><th>日期</th></tr>${rows}</table>`;
  }

  /** R160 生涯统计全览：LifetimeStats + 成就完成度（一页通）。 */
  private renderLifetimeStats(): void {
    const d = this.sim.exec({ action: "meta" }).data as {
      highScore: number; totalKills: number; games: number; clears: number;
      stats?: { timeAlive: number; coinsEarned: number; maxKills: number; maxCombo: number; totalElites: number; totalBosses: number; gamesByRole: Record<string, number> };
      ach_total?: number; ach_unlocked?: number;
      daily_streak?: number; codex_seen?: number; codex_gold?: number;
      daily_history?: Array<{ date: string; score: number }>;
    };
    const st = d.stats ?? { timeAlive: 0, coinsEarned: 0, maxKills: 0, maxCombo: 0, totalElites: 0, totalBosses: 0, gamesByRole: {} };
    const h = Math.floor(st.timeAlive / 3600);
    const m = Math.floor((st.timeAlive % 3600) / 60);
    const fav = Object.entries(st.gamesByRole ?? {}).sort((a, b) => b[1] - a[1])[0];
    const favName = fav ? fav[0] : "—";
    const pct = d.ach_total ? Math.round((d.ach_unlocked ?? 0) / d.ach_total * 100) : 0;
    this.bodyEl.innerHTML =
      `<table class="help-table">
        <tr><td>总游戏时长</td><td>${h > 0 ? `${h} 时 ${m} 分` : `${m} 分`}</td></tr>
        <tr><td>累计局数</td><td>${d.games} 局（通关 ${d.clears}/9）</td></tr>
        <tr><td>最高分</td><td>${d.highScore}</td></tr>
        <tr><td>单局最高击杀</td><td>${st.maxKills}</td></tr>
        <tr><td>生涯最高连击</td><td>${st.maxCombo}</td></tr>
        <tr><td>累计精英 / 妖王</td><td>${st.totalElites} / ${st.totalBosses}</td></tr>
        <tr><td>累计获得香火</td><td>${st.coinsEarned}</td></tr>
        <tr><td>每日挑战连胜</td><td>🔥 ${d.daily_streak ?? 0} 天</td></tr>
        <tr><td>每日周历</td><td>${(() => {
          const hist = d.daily_history ?? [];
          const cells: string[] = [];
          for (let i = 6; i >= 0; i--) {
            const dt = new Date(); dt.setDate(dt.getDate() - i);
            const key = `${dt.getFullYear()}-${String(dt.getMonth() + 1).padStart(2, "0")}-${String(dt.getDate()).padStart(2, "0")}`;
            const rec = hist.find((h) => h.date === key);
            cells.push(`${i === 0 ? "今" : i + "前"}:${rec ? rec.score : "—"}`);
          }
          return cells.join(" · ");
        })()}</td></tr>
        <tr><td>妖物图鉴</td><td>点亮 ${d.codex_seen ?? 0}/14 · 镀金 ${d.codex_gold ?? 0}/14</td></tr>
        <tr><td>最爱职业</td><td>${favName}</td></tr>
        <tr><td>成就完成度</td><td>${d.ach_unlocked ?? 0}/${d.ach_total ?? 0}（${pct}%）<span class="ach-bar" aria-hidden="true"><i style="width:${pct}%"></i></span></td></tr>
      </table>`;
  }

  private renderAchievements(): void {
    const unlocked = this.sim.achievements.unlocked;
    const total = ACHIEVEMENTS.filter((a) => unlocked.has(a.id)).length;
    const sections = ACHIEVEMENT_GROUPS.map((g) => {
      const list = ACHIEVEMENTS.filter((a) => a.group === g);
      const got = list.filter((a) => unlocked.has(a.id)).length;
      const rows = list.map((a) => {
        const done = unlocked.has(a.id);
        // R81 进度显示：阈值型未达成成就展示 cur/target（retention 钩子）
        const prog = done ? null : achievementProgress(a, this.sim.achievementState());
        const pct = prog ? Math.min(100, Math.round((prog.cur / prog.target) * 100)) : 0;
        // R87 进度条视觉化：纸风 mini 条（血红填充 + 铅笔灰底），文字保留
        const cur = prog ? Math.floor(prog.cur) : 0; // 存活秒为浮点累加：展示取整
        const progText = prog ? `（${cur >= prog.target ? prog.target : cur}/${prog.target}）` : "";
        const bar = prog
          ? `<span class="ach-bar" aria-hidden="true"><i style="width:${pct}%"></i></span>`
          : "";
        // R121 荣誉金字：与结算屏同源（HONOR_ACHIEVEMENT_NAMES 单一事实源）
        const nameCell = HONOR_ACHIEVEMENT_NAMES.has(a.name)
          ? `<span class="ach-honor">${a.name}</span>`
          : a.name;
        return `<tr class="${done ? "ach-done" : ""}"><td>${nameCell}</td>` +
          `<td>${a.desc}${progText}${bar} — ${done ? "✓ 已达成" : "未达成"}</td></tr>`;
      }).join("");
      return `<p class="sub ach-group">${g} ${got}/${list.length}</p><table class="help-table">${rows}</table>`;
    });
    // R18 成就进度：已达成 / 总数（总数取表长，不硬编码）
    this.bodyEl.innerHTML = `<p class="sub">已达成 ${total} / ${ACHIEVEMENTS.length}</p>${sections.join("")}`;
  }

  private renderMechanics(): void {
    // R32 融合图鉴：实际炼成过的配方种数（页签内插值展示）
    const fused = new Set((this.sim.exec({ action: "fusions" }).data as { fused: string[] }).fused);
    const fusedCount = FUSION_RECIPES.filter((r) => fused.has(r.result)).length;
    // R40 一周主题表（周日→周六，标注今天）
    const week = ([0, 1, 2, 3, 4, 5, 6] as const).map((day) => {
      const t = dailyTheme(new Date(2026, 8, 27 + day));
      return `${["日", "一", "二", "三", "四", "五", "六"][day]}·${t.label}`;
    }).join(" / ");
    // R41 本局立誓状态（誓名连乘倍率；无誓显示未立誓）
    const oath = [...this.sim.pacts]
      .map((id) => pactLabel(PACTS[id].name, this.sim.pactTier(id)))
      .join("·");
    const oathInfo = oath ? `（本局：${oath}，结算×${this.sim.pactScoreMult().toFixed(2)}）` : "（本局未立誓）";
    this.bodyEl.innerHTML = `
      <table class="help-table">
        <tr><td>方向攻击</td><td>近战朝移动方向挥砍（±55° 扇形），锥内妖物自动吸附瞄准；出手带突进位移——朝哪走朝哪砍</td></tr>
        <tr><td>武器齐射</td><td>全部装备武器同时出手（各自独立冷却）——武器栏即火力网</td></tr>
        <tr><td>连击</td><td>命中积累连击，每层伤害 +5%（上限 +50%），停手 2 秒衰减</td></tr>
        <tr><td>冲刺斩</td><td>冲刺窗口中攻击：全武器伤害 1.5×，近战范围 1.35× 且多打一个目标</td></tr>
        <tr><td>完美闪避</td><td>冲刺中被妖物攻击/弹道命中则免伤，妖物时缓 0.9 秒，自身 3 秒内伤害 1.3×</td></tr>
        <tr><td>受击反应</td><td>妖物被打会硬直后退、蓄力被打断——连续攻击可以压制出手；妖王免疫硬直</td></tr>
        <tr><td>攻击预警</td><td>妖物近战有 0.35 秒蓄力前摇（后倾+头顶！），走出范围即打空；脚下灰圈=你的攻击范围，圈内妖物脚下有血红标线</td></tr>
        <tr><td>妖王弹幕</td><td>妖王周期向四周放环形弹，阶段越高弹越密——用冲刺贴弹闪避触发妖时</td></tr>
        <tr><td>分幕与终局</td><td>每 150 秒推进一幕（共 4 幕），妖潮密度逐幕翻倍、精英潮渐起；第 13 阶终幕妖王降临，讨伐成功即通关（可继续无尽）</td></tr>
        <tr><td>威胁阶</td><td>每 45 秒推进一阶，妖物变强（伤害/血量乘数 = 1 + 阶数 × 8%，13 阶时 ×2.04）；每 5 阶出妖王（五王轮换，详见「妖王图鉴」页）</td></tr>
        <tr><td>讨伐难度</td><td>欢迎页三档：平妖 / 伏魔（通关解锁，妖物血伤+25%、金币+25%）/ 镇狱（通关伏魔解锁，+60%/+60%）；威势阶越高，香火龛武器稀有度越高</td></tr>
        <tr><td>精英词缀</td><td>七种词缀各带头顶徽标，详见「词缀图鉴」页；精英必掉宝箱（高稀有武器+符箓，血红大箱）</td></tr>
        <tr><td>符箓词条</td><td>三选一除数值成长外还有质变词条：多重（多目标/多发）、连锁、穿透、淬毒、冰缓、会心、溅射——build 由你定义</td></tr>
        <tr><td>武器进阶</td><td>同武器升级 3 次进阶、6 次终极</td></tr>
        <tr><td>战利品</td><td>符箓（普通 3.5%/精英 30%/妖王 3 枚）· 精英宝箱（高稀有武器+符箓）· 回春丹（普通 2%/精英 10%/妖王 1 枚，走近回复 12 生命）</td></tr>
        <tr><td>妖物机制</td><td>十四种妖物各带原生机制与应对，详见「妖物图鉴」页；妖王（每 5 阶一只）详见「妖王图鉴」页</td></tr>
        <tr><td>血誓契约</td><td>欢迎页立誓加难换结算加成（可叠加）：血战（妖血+25%，结算+25%）/ 人海（密度+30%，结算+30%）/ 脆体（上限-25%，结算+50%）——三重全立并通关有成就${oathInfo}</td></tr>
        <tr><td>祭坛融合</td><td>携带两件武器靠近祭坛，可按展示配方祭炼为新武器${fusedCount > 0 ? `（已炼成 ${fusedCount} 种，集齐全配方有成就）` : ""}</td></tr>
        <tr><td>妖潮异变</td><td>开局 100 秒后每 130 秒突发事件轮转：妖群合围（环形快攻群）/ 精锐现身（近点精英）/ 宝藏显形（两侧宝箱）/ 宝妖游荡（追杀爆精英箱，15 秒后逃走）/ 庙会开市（25 秒内香火龛半价），横幅预告</td></tr>
        <tr><td>永久升级</td><td>结算金币在陨落界面购买跨局成长：体魄（生命）/ 杀伐（伤害）/ 疾风（移速）/ 聚灵（拾取）/ 点金（金币）/ 回春（回复），价格 50×（等级+1）</td></tr>
        <tr><td>天气</td><td>雨减速、雾降能见、血月妖潮狂暴且精英频出</td></tr>
        <tr><td>每日主题</td><td>${week}——当日种子开局天气偏向当天主题（欢迎页一键挑战今日）</td></tr>
      </table>`;
  }
}
