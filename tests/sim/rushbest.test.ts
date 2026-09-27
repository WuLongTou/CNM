import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

function rushRun(save: ReturnType<typeof memoryStorage>, score: number, events: string[] = []) {
  const sim = new GameSim(451, save, "sword", 0, [], "boss_rush");
  sim.onEvent((e) => events.push(e.event));
  sim.score = score;
  sim.kills = 2;
  (sim as unknown as { onVictory(): number }).onVictory();
  return sim;
}

describe("R45 连战最佳", () => {
  it("rush 模式计最佳并破纪录播报；普通模式不碰", () => {
    const save = memoryStorage();
    const ev1: string[] = [];
    rushRun(save, 800, ev1); // 首跑不播
    expect(save.data.rushBest).toBe(800);
    expect(ev1).not.toContain("rush_record");

    const ev2: string[] = [];
    rushRun(save, 1200, ev2); // 破纪录播
    expect(save.data.rushBest).toBe(1200);
    expect(ev2).toContain("rush_record");

    // 普通模式高分不碰连战榜
    const normal = new GameSim(451, save);
    normal.score = 99999; normal.kills = 1;
    (normal as unknown as { onVictory(): number }).onVictory();
    expect(save.data.rushBest).toBe(1200);
  });

  it("meta 暴露 rushBest", () => {
    const save = memoryStorage();
    save.save({ ...memoryStorage().data, rushBest: 777 });
    const sim = new GameSim(451, save);
    expect(sim.exec({ action: "meta" }).data).toMatchObject({ rushBest: 777 });
  });
});
