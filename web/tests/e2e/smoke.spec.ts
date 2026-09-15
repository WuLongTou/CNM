// E2E 冒烟：页面加载、canvas 渲染、debug API 就绪、欢迎画面可见。
import { expect, test } from "@playwright/test";

test("页面加载完成，canvas 与 debug API 就绪", async ({ page }) => {
  await page.goto("/");
  await expect(page).toHaveTitle("僵尸大战");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  // boot 文案已被移除，canvas 已挂载
  await expect(page.locator("#boot")).toHaveCount(0);
  await expect(page.locator("canvas")).toHaveCount(1);
  const status = page.evaluate(() => window.__CNM__.exec({ id: 1, action: "status" }));
  expect(await status).toMatchObject({ id: 1, ok: true, data: { state: "WeaponSelection", round: 1 } });
});

test("欢迎画面可见，点击开始后进入战斗并出 HUD", async ({ page }) => {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  await expect(page.locator("#btn-start")).toBeVisible();
  await page.click("#btn-start");
  await page.waitForFunction(() => window.__CNM__.exec({ action: "status" }).data?.state === "Combat");
  await expect(page.locator("#hud")).toBeVisible();
  await expect(page.locator("#btn-start")).toBeHidden();
});
