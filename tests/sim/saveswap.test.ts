// R86 存档导出/导入：GameSim.exportSave/importSave（归一化兜底 + 往返一致）。

import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

describe("R86 存档导出/导入", () => {
  it("导出返回完整 SaveData 快照（含 R74 变异收集）", () => {
    const sim = new GameSim(860, memoryStorage(), "sword", 0, { blood: 2 }, "normal");
    const d = sim.exportSave();
    expect(d.highScore).toBe(0);
    expect(d.mutatorClears).toEqual({});
    expect(d.stats).toBeDefined();
  });

  it("导入：归一化覆写（export→import 往返一致）", () => {
    const a = new GameSim(861, memoryStorage());
    a.exec({ action: "start" });
    const backup = a.exportSave();
    const b = new GameSim(862, memoryStorage());
    b.importSave(JSON.parse(JSON.stringify(backup)));
    expect(b.exportSave()).toEqual(backup);
  });

  it("导入损坏/部分字段：normalizeSave 兜底回默认", () => {
    const a = new GameSim(863, memoryStorage());
    a.importSave({ highScore: 77 } as never);
    expect(a.exportSave().highScore).toBe(77);
    expect(a.exportSave().mutatorClears).toEqual({});
    expect(() => a.importSave(null as never)).not.toThrow();
    expect(a.exportSave().highScore).toBe(0); // null → 全默认
  });
});