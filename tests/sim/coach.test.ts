import { describe, expect, it } from "vitest";
import { COACH_TEXTS, coachTrigger, type CoachSnapshot, type CoachTipId } from "../../src/ui/coach";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

function snap(over: Partial<CoachSnapshot> = {}): CoachSnapshot {
  return { isFirstRun: true, kills: 0, pickups: 0, offersOpen: false, hpRatio: 1, ...over };
}

describe("R11 新手教练决策", () => {
  it("非首局恒不提示", () => {
    expect(coachTrigger(snap({ isFirstRun: false }), new Set())).toBeNull();
    expect(coachTrigger(snap({ isFirstRun: false, kills: 0, pickups: 3, hpRatio: 0.2 }), new Set())).toBeNull();
  });

  it("优先级：首杀前学攻杀 → 有符箓学拾取 → 低血学闪避", () => {
    const done = new Set<CoachTipId>();
    expect(coachTrigger(snap(), done)).toBe("move_attack");
    expect(coachTrigger(snap({ kills: 5 }), done)).toBeNull();
    expect(coachTrigger(snap({ kills: 5, pickups: 1 }), done)).toBe("pickup");
    expect(coachTrigger(snap({ kills: 5, offersOpen: true }), done)).toBe("pickup");
    expect(coachTrigger(snap({ kills: 5, hpRatio: 0.5 }), done)).toBe("dash");
  });

  it("已完成项不再出现", () => {
    const done = new Set<CoachTipId>(["move_attack", "pickup", "dash"]);
    expect(coachTrigger(snap({ pickups: 2, hpRatio: 0.1 }), done)).toBeNull();
    const half = new Set<CoachTipId>(["move_attack"]);
    expect(coachTrigger(snap({ hpRatio: 0.5 }), half)).toBe("dash");
  });

  it("闪避血线边界：0.6 不触发，低于才触发", () => {
    const done = new Set<CoachTipId>(["move_attack"]);
    expect(coachTrigger(snap({ kills: 3, hpRatio: 0.6 }), done)).toBeNull();
    expect(coachTrigger(snap({ kills: 3, hpRatio: 0.59 }), done)).toBe("dash");
  });

  it("三条文案键鼠/触屏齐备且有区分", () => {
    const ids: CoachTipId[] = ["move_attack", "pickup", "dash"];
    for (const id of ids) {
      expect(COACH_TEXTS[id].desktop.length).toBeGreaterThan(0);
      expect(COACH_TEXTS[id].touch.length).toBeGreaterThan(0);
    }
    expect(COACH_TEXTS.move_attack.desktop).toContain("WASD");
    expect(COACH_TEXTS.move_attack.touch).toContain("摇杆");
    expect(COACH_TEXTS.dash.desktop).toContain("Shift");
  });

  it("sim 首局标记：新存档 true，有对局记录 false，快照同步", async () => {
    const { defaultSave } = await import("../../src/sim/save");
    expect(new GameSim(91, memoryStorage()).isFirstRun).toBe(true);
    const save = memoryStorage();
    save.save({ ...defaultSave(), games: 2 });
    const vet = new GameSim(91, save);
    expect(vet.isFirstRun).toBe(false);
    expect(vet.status()).toMatchObject({ first_run: false });
    expect(new GameSim(91, memoryStorage()).status()).toMatchObject({ first_run: true });
  });
});
