// R131 秘密职业平衡观察（手动分析脚本，非 CI 断言）：三职业 × 12 种子对比。

import { describe, it } from "vitest";
import { runBot } from "./bot";

describe("R131 秘密职业平衡观察", () => {
  it("三职业 × 12 种子：到达阶/时长/击杀/分 对比", { timeout: 120000 }, () => {
    const rows: string[] = [];
    for (const role of ["sword", "hermit", "wusheng"] as const) {
      const ach = role === "hermit" ? ["flawless"] : role === "wusheng" ? ["nine_realms"] : [];
      const agg = { rounds: 0, time: 0, kills: 0, score: 0, n: 0, best: 0 };
      for (let seed = 1408; seed < 1420; seed++) {
        const r = runBot(seed, "orbit", 60 * 720, { role, achievements: ach });
        agg.rounds += r.round; agg.time += r.time; agg.kills += r.kills;
        agg.score += r.score; agg.n++;
        agg.best = Math.max(agg.best, r.round);
      }
      rows.push(`── ${role}（12 局）：平均阶 ${(agg.rounds / agg.n).toFixed(1)}（最好 ${agg.best}）· ` +
        `平均时长 ${(agg.time / agg.n).toFixed(0)}s · 平均击杀 ${(agg.kills / agg.n).toFixed(0)} · ` +
        `平均分 ${(agg.score / agg.n).toFixed(0)}`);
    }
    console.log(rows.join("\n"));
  });
});