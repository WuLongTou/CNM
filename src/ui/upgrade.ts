// 升级三选一面板（拾取符箓触发）：三张词条卡，点击或数字键 1/2/3 选择；
// R4 新增重抽（R，每局限量）与跳过（X，回复生命）。打开时 sim 已置 paused，
// 游戏输入 100% 失效；报价必须三选一或跳过（不可取消，避免囤积），
// 数字键与 R/X 均为未绑定的 UI 键（符合 UI 快捷键硬约束）。

import type { GameSim, UpgradeOffer } from "../sim/game";
import { CFG } from "../sim/config";

export class UpgradePanel {
  private el: HTMLElement;
  private cardsEl!: HTMLElement;
  private actionsEl!: HTMLElement;
  visible = false;

  constructor(private sim: GameSim) {
    this.el = document.createElement("div");
    this.el.className = "overlay hidden";
    this.el.innerHTML = `
      <div class="panel panel-wide">
        <h2>符箓 · 顿悟</h2>
        <p class="sub">拾得升级符箓，择一道铭刻（1 / 2 / 3 · R 重抽 · X 跳过）</p>
        <div id="upgrade-cards" class="upgrade-cards"></div>
        <div id="upgrade-actions" class="upgrade-actions"></div>
      </div>`;
    document.getElementById("ui-root")!.appendChild(this.el);
    this.cardsEl = this.el.querySelector("#upgrade-cards")!;
    this.actionsEl = this.el.querySelector("#upgrade-actions")!;
  }

  /** 重开一局时替换绑定的 sim 实例（新实例无残留报价，重抽次数随新局重置）。 */
  resimulate(sim: GameSim): void {
    this.sim = sim;
    this.hide();
  }

  show(): void {
    const offers = this.sim.upgradeOffers;
    if (!offers) return;
    this.visible = true;
    this.cardsEl.innerHTML = offers.map((o, i) => this.cardHtml(o, i)).join("");
    this.cardsEl.querySelectorAll("button[data-idx]").forEach((b) =>
      b.addEventListener("click", () => this.choose(Number((b as HTMLElement).dataset.idx))));
    this.renderActions();
    this.el.classList.remove("hidden");
  }

  /** 重抽/跳过按钮排（R4）：每次 show 重建（重抽次数实时变化）。 */
  private renderActions(): void {
    const rr = this.sim.rerollsLeft;
    this.actionsEl.innerHTML = `
      <button type="button" class="upgrade-action" data-act="reroll"${rr <= 0 ? " disabled" : ""}>
        重抽 · 剩 ${rr}<i>R</i>
      </button>
      <button type="button" class="upgrade-action" data-act="skip">
        跳过 · 回复 ${CFG.Upgrade.SkipHeal} 生命<i>X</i>
      </button>`;
    this.actionsEl.querySelectorAll("button[data-act]").forEach((b) => {
      b.addEventListener("click", () => {
        const act = (b as HTMLElement).dataset.act;
        if (act === "reroll") this.reroll();
        else if (act === "skip") this.skip();
      });
    });
  }

  private cardHtml(o: UpgradeOffer, i: number): string {
    return `<button type="button" class="upgrade-card" data-idx="${i}">
      <span class="uc-key">${i + 1}</span>
      <b>${o.weaponName}</b>
      <span class="uc-stat">${o.statName} +</span>
    </button>`;
  }

  private choose(index: number): void {
    // resolveUpgrade 内部 emit upgrade_applied（通知条/音效由装配层事件路由统一处理）
    this.sim.exec({ action: "choose", args: { target: "upgrade", index } });
    this.hide();
  }

  /** 重抽：消耗一次限额并重随机报价；成功后重渲染（按钮剩余数同步）。 */
  private reroll(): void {
    if (this.sim.rerollsLeft <= 0) return;
    const r = this.sim.exec({ action: "choose", args: { target: "upgrade", reroll: true } });
    const data = r.data as { handled?: boolean } | undefined;
    if (r.ok && data?.handled) this.show();
  }

  /** 跳过：放弃本次升级换即时回复（skipUpgrade 内部 emit upgrade_skipped）。 */
  private skip(): void {
    this.sim.exec({ action: "choose", args: { target: "upgrade", skip: true } });
    this.hide();
  }

  private hide(): void {
    this.visible = false;
    this.cardsEl.innerHTML = "";
    this.actionsEl.innerHTML = "";
    this.el.classList.add("hidden");
  }

  /** 每帧同步：协议侧（choose 指令）应答后隐藏面板。 */
  update(): void {
    if (this.visible && !this.sim.upgradeOffers) this.hide();
  }

  /** 按键路由（main 层调用）：仅面板可见时消费；数字键 1/2/3 选择，R 重抽，X 跳过。 */
  onKey(e: KeyboardEvent): boolean {
    if (!this.visible) return false;
    if (e.code === "Digit1" || e.code === "Numpad1") { this.choose(0); return true; }
    if (e.code === "Digit2" || e.code === "Numpad2") { this.choose(1); return true; }
    if (e.code === "Digit3" || e.code === "Numpad3") { this.choose(2); return true; }
    if (e.code === "KeyR") { this.reroll(); return true; }
    if (e.code === "KeyX") { this.skip(); return true; }
    return true; // 面板打开时吞掉其余按键，防止游戏/UI 热键漏进暂停态
  }
}
