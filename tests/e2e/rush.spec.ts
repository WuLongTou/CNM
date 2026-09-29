// R171 Boss 连战 e2e（evaluate 直驱——入口/模式标记/讨伐计数/战报头）。

import { test, expect } from "@playwright/test";

test("Boss 连战：入口开局 → 妖王生成 → 讨伐计数 → 战报头渲染", async ({ page }) => {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  await page.click("#btn-daily-rush");
  await page.waitForFunction(() => window.__CNM__.exec({ action: "status" }).data?.state === "Combat");

  const meta = await page.evaluate(() => {
    const s2 = window.__CNM__.sim;
    return {
      bossRush: s2.bossRush,
      daily: s2.seed === (new Date().getFullYear() * 10000 + (new Date().getMonth() + 1) * 100 + new Date().getDate()),
      bossSpawnCount: (s2 as unknown as { bossSpawnCount: number }).bossSpawnCount,
    };
  });
  expect(meta.bossRush).toBe(true);
  expect(meta.daily).toBe(true);

  // rush 每阶一只妖王（R44）：跳到 2 阶触发 spawnEnemy boss 潮，等妖王出现
  await page.evaluate(() => {
    const s2 = window.__CNM__.sim;
    s2.exec({ action: "goto_round", args: { round: 2 } });
  });
  const bossFound = await page.waitForFunction(() => {
    const s2 = window.__CNM__.sim;
    return s2.enemies.find((e) => (e as unknown as { boss: boolean }).boss) ?? false;
  }, null, { timeout: 20000 });
  expect(bossFound).toBeTruthy();

  // 击杀妖王 → bossRushKills +1
  const kills = await page.evaluate(() => {
    const s2 = window.__CNM__.sim;
    const boss = s2.enemies.find((e) => (e as unknown as { boss: boolean }).boss);
    if (!boss) return -1;
    boss.hp = 1;
    (s2 as unknown as { onEnemyKilled(e: unknown): void }).onEnemyKilled(boss);
    return (s2 as unknown as { bossRushKills: number }).bossRushKills;
  });
  expect(kills).toBeGreaterThanOrEqual(1);

  // 陨落结算：战报头含 连战讨伐（R60 rushHead）
  await page.evaluate(() => {
    const s2 = window.__CNM__.sim;
    s2.player.hp = 1;
    s2.exec({ action: "spawn", args: { type: "basic", near: { x: s2.player.x + 30, y: s2.player.y }, count: 2 } });
  });
  await page.waitForFunction(() => window.__CNM__.sim.state === "GameOver", null, { timeout: 15000 });
  // R5 延迟 0.7s 弹出结算：等 debrief 填充（而非 state 翻转即断言）
  await page.waitForFunction(
    () => (document.getElementById("gameover-debrief")?.textContent ?? "").length > 0,
    null, { timeout: 15000 },
  );
  const debrief = await page.evaluate(() => document.getElementById("gameover-debrief")?.textContent ?? "");
  expect(debrief).toContain("讨伐妖王 1 只"); // R60 rushHead 战报头
  expect(debrief).toContain("狂化");
});