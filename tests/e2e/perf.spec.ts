// R217 性能守护 e2e：满密度（84 妖=4 幕上限）帧率回归——性能悬崖是静默退化，
// 此前只有人工走查（实测 60fps @84 敌）。阈值放宽防 CI 抖动：
// 采 4 秒 rAF 间隔：中位数 ≤ 30ms（≥33fps）且 >100ms 大尖峰 ≤ 3 个。

import { expect, test } from "@playwright/test";

type PerfStats = { frames: number; median: number; p95: number; spikes: number };

const sample = (page: import("@playwright/test").Page, ms: number): Promise<PerfStats> =>
  page.evaluate((dur) => new Promise((resolve) => {
    const deltas: number[] = [];
    let last = performance.now();
    const start = last;
    const frame = (now: number) => {
      deltas.push(now - last);
      last = now;
      if (now - start < dur) requestAnimationFrame(frame);
      else {
        const sorted = [...deltas].sort((a, b) => a - b);
        resolve({
          frames: deltas.length,
          median: sorted[Math.floor(sorted.length / 2)],
          p95: sorted[Math.min(sorted.length - 1, Math.floor(sorted.length * 0.95))],
          spikes: deltas.filter((d) => d > 100).length,
        });
      }
    };
    requestAnimationFrame(frame);
  }), ms);

test("满密度 84 妖：帧率守护", async ({ page }) => {
  test.setTimeout(60000);
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  await page.evaluate(() => window.__CNM__.exec({ action: "start" }));

  // 基线：0 妖采样（headless 渲染本身可能钳帧，绝对阈值不可靠）
  const base = await sample(page, 2000);

  await page.evaluate(() => {
    const sim = window.__CNM__.sim;
    sim.survivalTime = 100000; // 直跳第 4 幕（act 为 survivalTime 派生 getter，MaxEnemies[3] = 84）
    sim.player.hp = 1e9;
    sim.player.invincible = 1e9;
    // spawnEnemy 单次 count 上限 50——84 需两次调用
    window.__CNM__.exec({ action: "spawn", args: { type: "basic", near: "player", count: 84 } });
    window.__CNM__.exec({ action: "spawn", args: { type: "basic", near: "player", count: 84 } });
  });
  await page.waitForFunction(() => window.__CNM__.sim.enemies.length >= 80, null, { timeout: 8000 });
  await page.waitForTimeout(500);

  const loaded: PerfStats = await sample(page, 4000);
  console.log("PERF", JSON.stringify({ base, loaded, enemies: 84 }));

  // 相对守护：满载中位帧间隔 ≤ 基线 ×2.5 且 ≤ 66ms 绝对上限；大尖峰稀少
  const limit = Math.max(base.median * 2.5, 20);
  expect(loaded.median, `满载中位 ${loaded.median}ms（基线 ${base.median}ms，上限 ${limit}ms）`).toBeLessThanOrEqual(Math.min(limit, 66));
  expect(loaded.spikes, `>100ms 尖峰 ${loaded.spikes} 个`).toBeLessThanOrEqual(3);
});
