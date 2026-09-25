// 帮助面板（H 键）：操作说明 / 妖物图鉴 / 武器图鉴 / 成就 / 机制。模态暂停。
// H 为 toggle_help 已绑定游戏键——打开期间游戏输入由 sim 门控 100% 失效（项目硬约束）。

import type { GameSim } from "../sim/game";
import type { WeaponDef } from "../sim/weapons";
import { ENEMY_NAMES, ENEMY_STATS } from "../sim/config";
import { WEAPON_DATA, FUSION_RECIPES } from "../sim/weapons";
import { ACHIEVEMENTS } from "../sim/achievements";
import { shouldUseTouch } from "./touch";

export class HelpPanel {
  private el: HTMLElement;
  private page = 0;
  private bodyEl!: HTMLElement;
  visible = false;

  private pages = ["操作", "妖物图鉴", "武器图鉴", "成就", "机制"];

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
      case "妖物图鉴": this.renderEnemyCodex(); break;
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
        ["装备 / 合成", "C / I"],
        ["帮助 / 暂停", "H / Esc"],
      ];
    this.bodyEl.innerHTML = `
      <table class="help-table">
        ${rows.map(([k, v]) => `<tr><td>${k}</td><td>${v}</td></tr>`).join("")}
        <tr><td>方向攻击</td><td>近战朝移动方向挥砍，锥内妖物自动吸附；站桩时自动瞄向最后朝向</td></tr>
        <tr><td>开局职业</td><td>欢迎页三选一：剑修（伤害+15%）/ 弓手（攻速+15%、冲刺冷却-20%）/ 术士（范围+20%、拾取+50%），各带专属起手武器</td></tr>
        <tr><td>采集</td><td>近战攻击范围内的树、岩、灌木</td></tr>
        <tr><td>冲刺斩</td><td>冲刺中攻击：伤害 1.5×，近战范围扩大，弓+1 矢、法术+1 弹</td></tr>
        <tr><td>完美闪避</td><td>冲刺中受击则闪避：时缓 0.9s + 3s 内伤害 1.3×</td></tr>
        <tr><td>升级重抽 / 跳过</td><td>符箓三选一可 R 重抽（每局 2 次）或 X 跳过（回复 15 生命）；触屏点面板按钮</td></tr>
      </table>`;
  }

  private renderEnemyCodex(): void {
    this.bodyEl.innerHTML = `<table class="help-table">
      ${Object.keys(ENEMY_NAMES).map((k) => {
        const s = ENEMY_STATS[Number(k)];
        return `<tr><td>${ENEMY_NAMES[Number(k)]}</td><td>生命 ${s.hp} · 伤害 ${s.dmg} · 速度 ${s.speed}</td></tr>`;
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

  private renderWeaponCodex(): void {
    const TYPE_NAMES: Record<WeaponDef["type"], string> = { Melee: "近战", Ranged: "远程", Magic: "魔法", Mine: "地雷" };
    const order: Array<WeaponDef["type"]> = ["Melee", "Ranged", "Magic", "Mine"];
    const fusionable = new Set(FUSION_RECIPES.flatMap((r) => [r.a, r.b]));
    const rows = order.flatMap((type) =>
      Object.values(WEAPON_DATA)
        .filter((d) => d.type === type)
        .sort((a, b) => a.tier - b.tier)
        .map((d) => {
          const stars = "★".repeat(d.rarity);
          const fuse = fusionable.has(d.name) ? " · 可祭炼" : "";
          const tags = this.weaponTags(d);
          return `<tr><td>${d.name}<i class="w-star">${stars}</i></td>` +
            `<td>[${TYPE_NAMES[d.type]}] ${d.desc}${tags ? `<br><span class="w-tags">${tags}</span>` : ""}${fuse ? `<span class="w-fuse">${fuse}</span>` : ""}</td></tr>`;
        }));
    this.bodyEl.innerHTML = `<table class="help-table">${rows.join("")}</table>`;
  }

  private renderAchievements(): void {
    const unlocked = this.sim.achievements.unlocked;
    const rows = ACHIEVEMENTS.map((a) => {
      const done = unlocked.has(a.id);
      return `<tr class="${done ? "ach-done" : ""}"><td>${a.name}</td>` +
        `<td>${a.desc} — ${done ? "✓ 已达成" : "未达成"}</td></tr>`;
    });
    this.bodyEl.innerHTML = `<table class="help-table">${rows.join("")}</table>`;
  }

  private renderMechanics(): void {
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
        <tr><td>威胁阶</td><td>每 45 秒推进一阶，妖物变强；每 5 阶出妖王（游魂→石魅→铁傀→甲妖→蛟 轮换，各带原生机制）</td></tr>
        <tr><td>精英词缀</td><td>迅捷 / 再生 / 护盾 / 爆裂（死亡自爆，别贴脸击杀）/ 狂乱（低血狂暴）</td></tr>
        <tr><td>符箓词条</td><td>三选一除数值成长外还有质变词条：多重（多目标/多发）、连锁、穿透、淬毒、冰缓、会心、溅射——build 由你定义</td></tr>
        <tr><td>武器进阶</td><td>同武器升级 3 次进阶、6 次终极</td></tr>
        <tr><td>妖物机制</td><td>爆魈自爆 · 隐煞潜行 · 瘴妖毒弹 · 愈妖疗伤 · 唤灵召唤 · 弩妖远程<br>铁傀甲坚（受伤-25%）· 甲妖举盾免伤 · 蛟蓄力冲撞 · 浮灯飘忽无碍</td></tr>
        <tr><td>祭坛融合</td><td>携带两件武器靠近祭坛，可按展示配方祭炼为新武器</td></tr>
        <tr><td>永久升级</td><td>结算金币在陨落界面购买跨局成长：体魄（生命）/ 杀伐（伤害）/ 疾风（移速）/ 聚灵（拾取）/ 点金（金币）/ 回春（回复），价格 50×（等级+1）</td></tr>
        <tr><td>天气</td><td>雨减速、雾降能见、血月妖潮狂暴且精英频出</td></tr>
      </table>`;
  }
}
