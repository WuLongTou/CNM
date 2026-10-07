// R209 融合面板 e2e：祭坛确认面板的真实 DOM/键盘接线。
// sim 层 resolveFusion 已有 fusionnew/fusioncodex 覆盖，本 spec 补的是面板渲染、
// 按钮与键盘应答、模态输入门控，以及「祭炼功成」反馈。
// 断言一律以报价返回的 a/b/resultName 为准，不硬编码武器名或配方（防配表漂移）。

import { expect, test } from "@playwright/test";

type Exec = (cmd: { action: string; args?: Record<string, unknown> }) => Promise<{ ok: boolean; data?: Record<string, unknown> }>;

type Offer = { offered: boolean; a: string; b: string; resultName: string; resultId: string };

const count = (list: string[], name: string) => list.filter((n) => n === name).length;

const weapons = (page: import("@playwright/test").Page) =>
  page.evaluate(() => window.__CNM__.sim.player.weapons.map((w) => w.name));

/** 开局并强制触发一次祭坛报价（返回报价明细）。 */
async function openFusion(page: import("@playwright/test").Page): Promise<Offer> {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  const exec: Exec = (cmd) => page.evaluate((c) => window.__CNM__.exec(c), cmd);
  await exec({ action: "start" });
  await exec({ action: "give", args: { weapon: "tiejian" } });
  await exec({ action: "give", args: { weapon: "fazhang" } });
  const offer = (await exec({ action: "offer_fusion" })).data as unknown as Offer;
  expect(offer.offered).toBe(true);
  await expect(page.locator("#fusion-info")).toBeVisible();
  return offer;
}

test("报价面板：文案含两件祭品与结果，且模态暂停", async ({ page }) => {
  const offer = await openFusion(page);
  const info = page.locator("#fusion-info");
  await expect(info).toContainText(offer.a);
  await expect(info).toContainText(offer.b);
  await expect(info).toContainText(offer.resultName);
  expect(await page.evaluate(() => window.__CNM__.sim.paused)).toBe(true);
});

test("接受按钮：各消耗一件、结果入囊、面板关闭", async ({ page }) => {
  const offer = await openFusion(page);
  const before = await weapons(page);
  expect(count(before, offer.a)).toBeGreaterThan(0);
  expect(count(before, offer.b)).toBeGreaterThan(0);

  await page.click("#btn-accept");
  await expect(page.locator("#fusion-info")).toBeHidden();

  const after = await weapons(page);
  expect(count(after, offer.resultName)).toBe(count(before, offer.resultName) + 1);
  expect(count(after, offer.a)).toBe(count(before, offer.a) - 1);
  expect(count(after, offer.b)).toBe(count(before, offer.b) - 1);
  expect(await page.evaluate(() => window.__CNM__.sim.fusionOffer)).toBeNull();
  expect(await page.evaluate(() => window.__CNM__.sim.paused)).toBe(false);
});

test("Enter 键接受：面板关闭 +「祭炼功成」纸签 + 结果入囊", async ({ page }) => {
  const offer = await openFusion(page);
  await page.keyboard.press("Enter");
  await expect(page.locator("#fusion-info")).toBeHidden();
  await expect(page.locator("#banner-root .sb-title")).toHaveText("祭炼功成");
  await expect(page.locator("#banner-root .sb-sub")).toContainText(offer.resultName);
  expect(await weapons(page)).toContain(offer.resultName);
});

test("Delete 键拒绝：武器原样保留、面板关闭、解冻", async ({ page }) => {
  const offer = await openFusion(page);
  const before = await weapons(page);

  await page.keyboard.press("Delete");
  await expect(page.locator("#fusion-info")).toBeHidden();

  const after = await weapons(page);
  expect(after).toEqual(before);
  expect(count(after, offer.resultName)).toBe(count(before, offer.resultName));
  expect(await page.evaluate(() => window.__CNM__.sim.fusionOffer)).toBeNull();
  expect(await page.evaluate(() => window.__CNM__.sim.paused)).toBe(false);
});

test("模态门控：面板期间游戏键不驱动对局，Esc 拒绝后恢复", async ({ page }) => {
  await openFusion(page);
  await page.keyboard.press("Space");
  await page.keyboard.down("KeyD");
  await page.waitForTimeout(300);
  // 时间冻结 + 仍处暂停（游戏输入 100% 失效）
  const t0 = await page.evaluate(() => window.__CNM__.sim.survivalTime);
  await page.waitForTimeout(200);
  const t1 = await page.evaluate(() => window.__CNM__.sim.survivalTime);
  expect(t1).toBe(t0);
  expect(await page.evaluate(() => window.__CNM__.sim.paused)).toBe(true);
  await page.keyboard.up("KeyD");

  // Esc 走 fusion.onKey（先于暂停菜单），仅拒绝本报价
  await page.keyboard.press("Escape");
  await expect(page.locator("#fusion-info")).toBeHidden();
  expect(await page.evaluate(() => window.__CNM__.sim.paused)).toBe(false);
});
