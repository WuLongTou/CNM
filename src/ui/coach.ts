// 新手教练（R11）：首局情境提示条——R3–R10 堆出的系统复杂度需要开局引导。
// 决策函数 coachTrigger 为纯逻辑（可单测）；Coach 类为薄 DOM 包装。
// 触发（仅首局，按优先级）：首杀前=移动攻击；场上有符箓/报价开=拾取铭刻；
// 血量<60%=冲刺闪避。消退：事件驱动（报价/完美闪避）+ 条件自然不满足（轮询）。
// 提示条为非模态（pointer-events 继承 root 的 none），绝不拦截输入。

import type { GameSim, SimEvent } from "../sim/game";
import { shouldUseTouch } from "./touch";

export type CoachTipId =
  | "move_attack" | "pickup" | "dash"
  | "elite_chest" | "fusion" | "boss" | "arsenal_full";

export interface CoachSnapshot {
  isFirstRun: boolean;
  kills: number;
  pickups: number;
  offersOpen: boolean;
  hpRatio: number;
  /** R31 进阶：精英击杀 / 身边有未用祭坛 / 场上有妖王 / 持有武器数（可选，老调用方缺省为 0）。 */
  eliteKills?: number;
  altarNear?: boolean;
  bossActive?: boolean;
  weaponCount?: number;
}

/** 进阶提示集合（R31：展示 8 秒即消退，老手每局最多见四行，不做事件追踪）。 */
export const TRANSIENT_TIPS: ReadonlySet<CoachTipId> = new Set([
  "elite_chest", "fusion", "boss", "arsenal_full",
]);
/** 进阶提示展示时长毫秒。 */
export const TRANSIENT_MS = 8000;

/** 当前应展示的提示（按优先级取首个未完成且条件成立者；无则 null）。
 *  R11 基础三条仅首局；R31 进阶四条不分局数（系统发现型提示）。 */
export function coachTrigger(s: CoachSnapshot, done: Set<CoachTipId>): CoachTipId | null {
  if (s.isFirstRun) {
    if (!done.has("move_attack") && s.kills === 0) return "move_attack";
    if (!done.has("pickup") && (s.pickups > 0 || s.offersOpen)) return "pickup";
    if (!done.has("dash") && s.hpRatio < 0.6) return "dash";
  }
  if (!done.has("elite_chest") && (s.eliteKills ?? 0) > 0) return "elite_chest";
  if (!done.has("fusion") && (s.altarNear ?? false) && (s.weaponCount ?? 0) >= 2) return "fusion";
  if (!done.has("boss") && (s.bossActive ?? false)) return "boss";
  if (!done.has("arsenal_full") && (s.weaponCount ?? 0) >= 5) return "arsenal_full";
  return null;
}

export const COACH_TEXTS: Record<CoachTipId, { desktop: string; touch: string }> = {
  move_attack: {
    desktop: "WASD 移动 · 按住空格攻击（全部武器齐射）",
    touch: "左侧摇杆移动 · 按住击钮攻击",
  },
  pickup: {
    desktop: "走近拾取升级符箓 → 三选一强化（1 / 2 / 3）",
    touch: "走近拾取升级符箓 → 三选一强化",
  },
  dash: {
    desktop: "Shift 冲刺 · 冲刺中受击触发完美闪避（时缓 + 增伤）",
    touch: "冲钮冲刺 · 冲刺中受击触发完美闪避",
  },
  // R31 进阶四条（不分局数，展示 8 秒即消退）
  elite_chest: {
    desktop: "精英必掉血红宝箱（高稀有武器 + 符箓），值得追杀",
    touch: "精英必掉血红宝箱，值得追杀",
  },
  fusion: {
    desktop: "靠近祭坛（紫点）可祭炼两件武器为一（I 打开合成查配方）",
    touch: "靠近祭坛（紫点）可祭炼两件武器为一",
  },
  boss: {
    desktop: "妖王现身！走位躲环形弹幕，打完阶段它才走",
    touch: "妖王现身！走位躲环形弹幕",
  },
  arsenal_full: {
    desktop: "六械齐备： I 打开行囊可替换升级最少的武器",
    touch: "六械齐备：可在行囊替换武器",
  },
};

export class Coach {
  private el: HTMLElement;
  private done = new Set<CoachTipId>();
  private current: CoachTipId | null = null;
  /** 进阶提示限时展示截止（R31；0 = 当前非限时项）。 */
  private transientUntil = 0;

  constructor(private sim: GameSim, private touch = shouldUseTouch()) {
    this.el = document.createElement("div");
    this.el.id = "coach-tip";
    this.el.classList.add("hidden");
    document.getElementById("ui-root")!.appendChild(this.el);
    this.watch(sim);
  }

  /** 重开一局时替换绑定的 sim 实例（新局提示状态清零，首局重新判定）。 */
  resimulate(sim: GameSim): void {
    this.sim = sim;
    this.done.clear();
    this.current = null;
    this.transientUntil = 0;
    this.el.classList.add("hidden");
    this.watch(sim);
  }

  /** 报价/闪避事件 → 对应提示永久消退（本局不再出现）。 */
  private watch(sim: GameSim): void {
    sim.onEvent((e: SimEvent) => {
      if (e.event === "upgrade_offer" || e.event === "upgrade_applied" || e.event === "upgrade_skipped") {
        this.done.add("pickup");
      } else if (e.event === "perfect_dodge") {
        this.done.add("dash");
      }
    });
  }

  /** 每帧同步 sim → 提示条（条件变化或事件消退时切换）。
   *  R19：仅战斗态显示——欢迎/结算/模态下藏条（半透明遮罩下抢戏）。 */
  update(): void {
    const s = this.sim;
    if (s.state !== "Combat") {
      if (this.current !== null) {
        this.current = null;
        this.el.classList.add("hidden");
      }
      return;
    }
    const tip = coachTrigger({
      isFirstRun: s.isFirstRun,
      kills: s.kills,
      pickups: s.pickups.length,
      offersOpen: s.upgradeOffers !== null,
      hpRatio: s.player.hp / s.player.maxHp,
      // R31 进阶上下文
      eliteKills: s.eliteKills,
      altarNear: this.altarNear(s),
      bossActive: s.enemies.some((e) => e.boss),
      weaponCount: s.player.weapons.length,
    }, this.done);
    // R31 进阶提示限时：先于去重判定结算（同一提示持续在场也要到期消退）
    if (tip !== null && TRANSIENT_TIPS.has(tip)) {
      if (this.transientUntil === 0) this.transientUntil = performance.now() + TRANSIENT_MS;
      else if (performance.now() > this.transientUntil) {
        this.done.add(tip);
        this.transientUntil = 0;
        this.current = null;
        this.el.classList.add("hidden");
        return;
      }
    } else {
      this.transientUntil = 0;
    }
    if (tip === this.current) return;
    this.current = tip;
    if (!tip) {
      this.el.classList.add("hidden");
      return;
    }
    const t = COACH_TEXTS[tip];
    this.el.textContent = this.touch ? t.touch : t.desktop;
    this.el.classList.remove("hidden");
  }

  /** 玩家是否站在未用祭坛旁（60px，与 sim 融合触发半径一致）。 */
  private altarNear(s: GameSim): boolean {
    for (const a of s.terrain.altars) {
      if (!a.used && (a.x - s.player.x) ** 2 + (a.y - s.player.y) ** 2 <= 90 * 90) return true;
    }
    return false;
  }
}
