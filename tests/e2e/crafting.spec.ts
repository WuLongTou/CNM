// R210 合成面板 e2e：中文化、缺料可视化与合成反馈。
// 此前该面板只有协议级覆盖（craft 被拒），DOM 层存在两个真实缺陷：
// 成本行显示英文 raw id（同屏中英混排）、材料不足时按钮仍可点且点了毫无反应。

import { expect, test } from "@playwright/test";

type Exec = (cmd: { action: string; args?: Record<string, unknown> }) => Promise<{ ok: boolean; data?: Record<string, unknown> }>;

async function openCraft(page: import("@playwright/test").Page, materials?: Record<string, number>) {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  const exec: Exec = (cmd) => page.evaluate((c) => window.__CNM__.exec(c), cmd);
  await exec({ action: "start" });
  if (materials) {
    await page.evaluate((m) => {
      const inv = window.__CNM__.sim.inventory.counts as unknown as Record<string, number>;
      for (const [k, v] of Object.entries(m)) inv[k] = v;
    }, materials);
  }
  await page.keyboard.press("KeyI");
  await expect(page.locator("#craft-list")).toBeVisible();
  return exec;
}

test("I 键开关 + 模态暂停 + 面板期间游戏键失效", async ({ page }) => {
  await openCraft(page);
  expect(await page.evaluate(() => window.__CNM__.sim.paused)).toBe(true);

  await page.keyboard.press("Space");
  await page.keyboard.down("KeyD");
  await page.waitForTimeout(250);
  const t0 = await page.evaluate(() => window.__CNM__.sim.survivalTime);
  await page.waitForTimeout(200);
  const t1 = await page.evaluate(() => window.__CNM__.sim.survivalTime);
  expect(t1).toBe(t0); // 时间冻结
  expect(await page.evaluate(() => window.__CNM__.sim.paused)).toBe(true);
  await page.keyboard.up("KeyD");

  await page.keyboard.press("KeyI");
  await expect(page.locator("#craft-list")).toBeHidden();
  expect(await page.evaluate(() => window.__CNM__.sim.paused)).toBe(false);
});

test("材料与配方成本全中文，不泄露英文 raw id", async ({ page }) => {
  await openCraft(page);
  const inv = await page.locator("#craft-inv").innerText();
  for (const label of ["木材", "石料", "纤维", "浆果"]) {
    expect(inv, label).toContain(label);
  }
  const list = await page.locator("#craft-list").innerText();
  expect(list).not.toMatch(/wood|stone|fiber|berry/);
});

test("缺料：按钮禁用并写明「材料不足」，成本项标红", async ({ page }) => {
  await openCraft(page); // 开局 0 材料
  const first = page.locator("#craft-list button[data-recipe]").first();
  await expect(first).toBeDisabled();
  await expect(first).toHaveText("材料不足");
  expect(await page.locator("#craft-list .lack").count()).toBeGreaterThan(0);
});

test("材料充足：按钮可用，点击后合成入囊并扣料、面板内回报", async ({ page }) => {
  await openCraft(page, { wood: 5, stone: 5 });
  // 战力口径：新武器 +1 件、重复武器 +1 精炼，两者都恰好 +1 级（绕开职业初始武器差异）
  const power = () =>
    page.evaluate(() =>
      window.__CNM__.sim.player.weapons.reduce((n, w) => n + 1 + w.ups, 0),
    );
  const before = await power();

  const btn = page.locator('button[data-recipe="stone_axe"]');
  await expect(btn).toBeEnabled();
  await expect(btn).toHaveText("合成");
  await btn.click();

  await expect(page.locator("#craft-msg")).toHaveText("已合成：石斧");
  expect(await power()).toBe(before + 1);
  const inv = await page.evaluate(() => ({
    wood: window.__CNM__.sim.inventory.counts.wood,
    stone: window.__CNM__.sim.inventory.counts.stone,
  }));
  expect(inv).toEqual({ wood: 3, stone: 2 }); // stone_axe 消耗 木2 石3
});
