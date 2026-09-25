// 新手教练（R11）：首局情境提示条——R3–R10 堆出的系统复杂度需要开局引导。
// 决策函数 coachTrigger 为纯逻辑（可单测）；Coach 类为薄 DOM 包装。
// 触发（仅首局，按优先级）：首杀前=移动攻击；场上有符箓/报价开=拾取铭刻；
// 血量<60%=冲刺闪避。消退：事件驱动（报价/完美闪避）+ 条件自然不满足（轮询）。
// 提示条为非模态（pointer-events 继承 root 的 none），绝不拦截输入。

import type { GameSim, SimEvent } from "../sim/game";
import { shouldUseTouch } from "./touch";

export type CoachTipId = "move_attack" | "pickup" | "dash";

export interface CoachSnapshot {
  isFirstRun: boolean;
  kills: number;
  pickups: number;
  offersOpen: boolean;
  hpRatio: number;
}

/** 当前应展示的提示（按优先级取首个未完成且条件成立者；无则 null）。 */
export function coachTrigger(s: CoachSnapshot, done: Set<CoachTipId>): CoachTipId | null {
  if (!s.isFirstRun) return null;
  if (!done.has("move_attack") && s.kills === 0) return "move_attack";
  if (!done.has("pickup") && (s.pickups > 0 || s.offersOpen)) return "pickup";
  if (!done.has("dash") && s.hpRatio < 0.6) return "dash";
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
};

export class Coach {
  private el: HTMLElement;
  private done = new Set<CoachTipId>();
  private current: CoachTipId | null = null;

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
    }, this.done);
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
}
