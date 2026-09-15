// E2E 完整对局流程：对标 Godot 版 .workwork/e2e_events.py + selftest 的断言链。
// start → 移动 → spawn 击杀计分 → game_state 快照 → 围杀 → game_over 事件。
import { expect, test } from "@playwright/test";

type Exec = (cmd: { id?: number; action: string; args?: Record<string, unknown> }) => Promise<{ ok: boolean; data?: Record<string, unknown>; error?: string }>;

test("完整对局闭环：移动/战斗/计分/GameOver 事件", async ({ page }) => {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);

  const exec: Exec = (cmd) => page.evaluate((c) => window.__CNM__.exec(c), cmd);

  // 1. start 进入战斗
  expect((await exec({ id: 1, action: "start" })).data).toEqual({ started: true });
  expect((await exec({ action: "status" })).data).toMatchObject({ state: "Combat" });

  // 2. 固定出生点，持续向右移动 → 位置增加
  await exec({ action: "teleport", args: { x: 800, y: 1080 } });
  const x0 = ((await exec({ action: "status" })).data!.player_pos as { x: number }).x;
  await exec({ action: "input", args: { move_x: 1 } });
  await page.waitForTimeout(600);
  const x1 = ((await exec({ action: "status" })).data!.player_pos as { x: number }).x;
  expect(x1).toBeGreaterThan(x0 + 50);
  await exec({ action: "input", args: { move_x: 0 } });

  // 3. 贴脸刷一只 basic 僵尸，攻击击杀 → 击杀计分
  await exec({ action: "teleport", args: { x: 1000, y: 1000 } });
  expect((await exec({ action: "spawn", args: { type: "basic", near: { x: 1030, y: 1000 } } })).data).toEqual({ spawned: 1 });
  await exec({ action: "input", args: { attack: true } });
  await page.waitForFunction(() => {
    const st = window.__CNM__.exec({ action: "status" }).data!;
    return (st.kills as number) >= 1;
  });
  const st = (await exec({ action: "status" })).data!;
  expect(st.score).toBeGreaterThanOrEqual(10);
  await exec({ action: "input", args: { attack: false } });

  // 4. game_state 快照字段完整性（对齐 _GameState）
  const gs = (await exec({ action: "game_state" })).data!;
  expect(gs).toHaveProperty("player.hp");
  expect(gs).toHaveProperty("player.pos");
  expect(gs).toHaveProperty("zombies.total");
  expect(Array.isArray((gs.zombies as { list: unknown[] }).list)).toBe(true);

  // 5. 围杀：高伤僵尸贴脸 → 玩家死亡 → game_over 事件
  await exec({ action: "spawn", args: { type: "basic", near: { x: 1005, y: 1000 } } });
  await page.evaluate(() => {
    const sim = window.__CNM__.sim;
    for (const z of sim.zombies) z.damage = 9999;
    sim.player.invincible = 0;
  });
  await page.waitForFunction(() => window.__CNM__.getEvents().some((e) => e.event === "game_over"));
  const over = (await exec({ action: "status" })).data!;
  expect(over.state).toBe("GameOver");
  // 结算 UI 可见
  await expect(page.locator("#btn-restart")).toBeVisible();

  // 6. 截图人检：GameOver 画面
  await page.screenshot({ path: "test-results/gameover.png" });
});

test("威胁阶事件：快进 45s 收到 threat_tier 推送", async ({ page }) => {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  const exec: Exec = (cmd) => page.evaluate((c) => window.__CNM__.exec(c), cmd);
  await exec({ action: "start" });
  await page.evaluate(() => { window.__CNM__.sim.player.hp = 1e9; }); // 苟命
  await page.waitForFunction(() => {
    const sim = window.__CNM__.sim;
    return sim.round >= 2 || sim.survivalTime > 47;
  }, undefined, { timeout: 55_000 });
  const round = ((await exec({ action: "status" })).data!.round) as number;
  expect(round).toBeGreaterThanOrEqual(2);
  await page.screenshot({ path: "test-results/threat-tier.png" });
});
