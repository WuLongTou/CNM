// 香火龛面板（R65）：贴近香火龛触发，金币选购（武器 / 符箓 / 调息 / 祝福）。
// 点击或数字键 1/2/3 购买（R202 一龛多购：购后余货涨价可续购，购罄龛散、全场基价通胀），
// Esc 关闭（不消费龛，走开再回可重开）。打开时 sim 已置 paused，游戏输入 100% 失效；
// 数字键与 Esc 均为既有 UI 键，不占用游戏键位（符合模态硬约束）。

import type { GameSim, ShopSlot } from "../sim/game";
import { CFG } from "../sim/config";

const KIND_TAG: Record<ShopSlot["kind"], string> = {
  weapon: "请武",
  upgrade: "铭箓",
  heal: "调息",
  buff: "祝福",
};

export class ShopPanel {
  /** 面板根元素（R76 手柄菜单导航的容器）。 */
  readonly el: HTMLElement;
  private cardsEl!: HTMLElement;
  private goldEl!: HTMLElement;
  private actionsEl!: HTMLElement;
  visible = false;

  constructor(private sim: GameSim) {
    this.el = document.createElement("div");
    this.el.className = "overlay hidden";
    this.el.innerHTML = `
      <div class="panel panel-wide">
        <h2>香火龛</h2>
        <p class="sub">以香火钱换机缘（1 / 2 / 3 购买 · 可连购，每笔后本龛余货与全场涨价 · R 刷新 · Esc 离开）</p>
        <p class="sub" id="shop-gold"></p>
        <div id="shop-cards" class="upgrade-cards"></div>
        <div id="shop-actions"></div>
      </div>`;
    document.getElementById("ui-root")!.appendChild(this.el);
    this.cardsEl = this.el.querySelector("#shop-cards")!;
    this.goldEl = this.el.querySelector("#shop-gold")!;
    this.actionsEl = this.el.querySelector("#shop-actions")!;
  }

  /** 重开一局时替换绑定的 sim 实例（新实例无残留报价）。 */
  resimulate(sim: GameSim): void {
    this.sim = sim;
    this.hide();
  }

  show(): void {
    const slots = this.sim.shopOffer;
    if (!slots) return;
    this.visible = true;
    this.render();
    this.el.classList.remove("hidden");
  }

  private render(): void {
    const slots = this.sim.shopOffer ?? [];
    const rerollCost = this.sim.shopRerollCost();
    this.goldEl.textContent = `香火钱 ${this.sim.coins}`;
    this.cardsEl.innerHTML = slots
      .map((s, i) => this.cardHtml(s, i))
      .join("");
    this.cardsEl.querySelectorAll(".upgrade-card[data-idx]").forEach((c) =>
      c.addEventListener("click", () => this.buy(Number((c as HTMLElement).dataset.idx))));
    // R163 锁按钮事件委托（阻止冒泡避免触发整卡购买）
    this.cardsEl.querySelectorAll(".uc-lock[data-lock]").forEach((b) =>
      b.addEventListener("click", (e) => {
        e.stopPropagation();
        this.sim.toggleShopLock(Number((b as HTMLElement).dataset.lock));
        this.render();
      }));
    this.actionsEl.innerHTML = `<button type="button" class="upgrade-action" data-act="reroll"${this.sim.coins < rerollCost ? " disabled" : ""}>刷新报价 · ${rerollCost} 文<i>R</i></button>`;
    this.actionsEl.querySelectorAll("button[data-act]").forEach((b) => {
      b.addEventListener("click", () => { if ((b as HTMLElement).dataset.act === "reroll") this.reroll(); });
    });
  }

  /** 刷新报价（R78/R79）：花金币重 roll 三槽（龛体保留、不触发通胀）。
   *  公开供手柄 Y 键路由。 */
  reroll(): void {
    const r = this.sim.exec({ action: "shop_reroll" });
    const data = r.data as { handled?: boolean } | undefined;
    if (r.ok && data?.handled) this.render();
  }

  private cardHtml(s: ShopSlot, i: number): string {
    const afford = this.sim.coins >= s.price;
    // R90 稀有祝福金字卡面（buffRare 仅祝福槽有）
    const rareCls = s.buffRare ? " shop-card-rare" : "";
    // R126 祝福购买预览：当前累计 → 购后累计（dmg/asp 可精确计算；spd 为乘法叠加不展示）
    let preview = "";
    if (s.kind === "buff" && s.buffKind) {
      const base = s.buffKind === "dmg" ? CFG.Shop.BuffDmgBonus : s.buffKind === "asp" ? CFG.Shop.BuffAspBonus : CFG.Shop.BuffSpdBonus;
      const cur = s.buffKind === "dmg" ? this.sim.shopBuff : s.buffKind === "asp" ? this.sim.shopBuffAsp : this.sim.shopBuffSpd;
      const rareMult = s.buffRare ? 2 : 1;
      const after = cur + base * rareMult;
      preview = `<span class="uc-preview">（当前 ${Math.round(cur * 100)}% → 购后 ${Math.round(after * 100)}%）</span>`;
    }
    // R163 槽位锁定按钮（🔒/🔓）
    const lockBtn = `<button type="button" class="uc-lock" data-lock="${i}">${s.locked ? "🔒" : "🔓"}</button>`;
    return `<div class="upgrade-card shop-card${rareCls}" data-idx="${i}" role="button" tabindex="0"${afford ? "" : ' style="opacity:0.55"'}>
      <span class="uc-key">${i + 1}</span>${lockBtn}
      <b>${s.label}</b>
      <span class="uc-stat">${KIND_TAG[s.kind]} · ${s.price} 文${afford ? "" : "（香火不足）"}</span>
      ${preview}
    </div>`;
  }

  /** 购买：buyShop 内部扣费/发放/通胀并 emit shop_bought（通知/音效走装配层路由）。 */
  private buy(index: number): void {
    const r = this.sim.exec({ action: "shop_buy", args: { index } });
    const data = r.data as { handled?: boolean } | undefined;
    if (r.ok && data?.handled) {
      // R202 一龛多购：龛有余货原地重渲染续购（main 层 shop_offer 事件亦会刷新）；购罄才隐藏
      if (this.sim.shopOffer) this.render();
      else this.hide();
    } else this.render(); // 拒绝（香火不足）：留在报价页，价格与余额实时重渲染
  }

  /** Esc/关闭：closeShop 保留龛体，走开再回可重开（closeAllModals 亦用）。 */
  close(): void {
    if (!this.sim.shopOffer) { this.hide(); return; }
    this.sim.exec({ action: "shop_close" });
    this.hide();
  }

  private hide(): void {
    this.visible = false;
    this.cardsEl.innerHTML = "";
    this.el.classList.add("hidden");
  }

  /** 每帧同步：协议侧（shop_buy/shop_close 指令）应答后隐藏面板。 */
  update(): void {
    if (this.visible && !this.sim.shopOffer) this.hide();
  }

  /** 按键路由（main 层调用）：仅面板可见时消费；1/2/3 购买，Esc 关闭，其余吞掉。 */
  onKey(e: KeyboardEvent): boolean {
    if (!this.visible) return false;
    if (e.code === "Digit1" || e.code === "Numpad1") { this.buy(0); return true; }
    if (e.code === "Digit2" || e.code === "Numpad2") { this.buy(1); return true; }
    if (e.code === "Digit3" || e.code === "Numpad3") { this.buy(2); return true; }
    if (e.code === "Escape") { this.close(); return true; }
    if (e.code === "KeyR") { this.reroll(); return true; } // 手柄 Y 同效（main 层路由）
    return true; // 模态独占：吞掉其余按键（游戏键已由 sim.tick 门控清零）
  }
}
