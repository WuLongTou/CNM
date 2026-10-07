// R215 升级三选一 e2e：全游戏使用频率最高的模态。protocol 层早有覆盖
// （features/progression），本 spec 补真实 DOM/键盘交互：数字键选择、放逐按钮
// （R7 限额与停止冒泡）、重抽（R4 限额与禁用态）、跳过（X 回复）。

import { expect, test } from "@playwright/test";

type Exec = (cmd: { action: string; args?: Record<string, unknown> }) => Promise<{ ok: boolean; data?: Record<string, unknown> }>;
type Offer = { weaponIdx: number; stat: string; weaponName: string; statName: string };

/** 开局并塞符箓打开三选一面板（progression.spec 的成熟做法）。 */
async function openUpgrade(page: import("@playwright/test").Page) {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  const exec: Exec = (cmd) => page.evaluate((c) => window.__CNM__.exec(c), cmd);
  await exec({ action: "start" });
  await page.evaluate(() => {
    const s = window.__CNM__.sim;
    s.player.hp = 1e9;
    if (s.player.weapons.length === 0) s.exec({ action: "give", args: { weapon: "tiejian" } });
    s.pickups.push({ id: 9001, x: s.player.x, y: s.player.y });
  });
  await page.waitForFunction(() => window.__CNM__.sim.upgradeOffers !== null);
  await page.waitForSelector("#upgrade-cards .upgrade-card");
  return exec;
}

const offers = (page: import("@playwright/test").Page) =>
  page.evaluate(() =>
    (window.__CNM__.sim.upgradeOffers ?? []).map((o) => ({ weaponIdx: o.weaponIdx, stat: o.stat, weaponName: o.weaponName, statName: o.statName })),
  ) as Promise<Offer[]>;

test("数字键选择：词条 +1、面板关闭、铭刻通知", async ({ page }) => {
  const exec = await openUpgrade(page);
  const list = await offers(page);
  const target = list[1];
  const upsBefore = await page.evaluate(
    (idx) => window.__CNM__.sim.player.weapons[idx].ups, target.weaponIdx,
  );

  await page.keyboard.press("Digit2");

  await page.waitForFunction(() => window.__CNM__.sim.upgradeOffers === null);
  await expect(page.locator("#upgrade-cards .upgrade-card")).toHaveCount(0);
  const upsAfter = await page.evaluate(
    (idx) => window.__CNM__.sim.player.weapons[idx].ups, target.weaponIdx,
  );
  expect(upsAfter).toBe(upsBefore + 1);
  await expect(page.locator("#notify-root")).toContainText("符箓铭刻");
  void exec;
});

test("放逐按钮：组合移除并重补卡、限额归零后全部禁用、按键无效", async ({ page }) => {
  await openUpgrade(page);
  const banished = (await offers(page))[0];
  expect(await page.evaluate(() => window.__CNM__.sim.banishesLeft)).toBe(1);

  await page.locator('button[data-banish="0"]').click();

  // 组合移除：报价里不再出现同一对 武器×词条
  const after = await offers(page);
  expect(after.some((o) => o.weaponIdx === banished.weaponIdx && o.stat === banished.stat)).toBe(false);
  expect(await page.evaluate(() => window.__CNM__.sim.banishesLeft)).toBe(0);
  // 限额归零：三张卡的放逐按钮全部禁用
  for (const b of await page.locator("#upgrade-cards .uc-banish").all()) {
    await expect(b).toBeDisabled();
  }
  // 4/5/6 键无效（不再消耗、报价不变）
  const snapshot = await offers(page);
  await page.keyboard.press("Digit4");
  expect(await offers(page)).toEqual(snapshot);
  expect(await page.evaluate(() => window.__CNM__.sim.banishesLeft)).toBe(0);
});

test("重抽：三卡重随机、限额递减、用尽后按钮禁用且 R 无效", async ({ page }) => {
  await openUpgrade(page);
  expect(await page.evaluate(() => window.__CNM__.sim.rerollsLeft)).toBe(2);

  await page.locator('button[data-act="reroll"]').click();
  expect(await page.evaluate(() => window.__CNM__.sim.rerollsLeft)).toBe(1);
  await expect(page.locator('#upgrade-actions button[data-act="reroll"]')).toContainText("剩 1");

  await page.keyboard.press("KeyR");
  expect(await page.evaluate(() => window.__CNM__.sim.rerollsLeft)).toBe(0);
  await expect(page.locator('#upgrade-actions button[data-act="reroll"]')).toBeDisabled();

  await page.keyboard.press("KeyR");
  expect(await page.evaluate(() => window.__CNM__.sim.rerollsLeft)).toBe(0);
  expect(await page.evaluate(() => window.__CNM__.sim.upgradeOffers)).not.toBeNull();
});

test("跳过（X）：面板关闭、回复 15 生命（不超上限）、通知播报", async ({ page }) => {
  await openUpgrade(page);
  await page.evaluate(() => { window.__CNM__.sim.player.hp = 30; });

  await page.keyboard.press("KeyX");

  await page.waitForFunction(() => window.__CNM__.sim.upgradeOffers === null);
  const hp = await page.evaluate(() => window.__CNM__.sim.player.hp);
  expect(hp).toBe(45); // 30 + SkipHeal 15（上限 120 未触顶）
  await expect(page.locator("#notify-root")).toContainText("跳过升级");
});
