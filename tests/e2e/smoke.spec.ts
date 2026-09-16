// E2E 冒烟：页面加载、canvas 渲染、debug API 就绪、欢迎画面可见。
import { expect, test } from "@playwright/test";

test("页面加载完成，canvas 与 debug API 就绪", async ({ page }) => {
  await page.goto("/");
  await expect(page).toHaveTitle("斩妖录");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  // boot 文案已被移除，canvas 已挂载
  await expect(page.locator("#boot")).toHaveCount(0);
  // 主画布（PixiJS 挂在 #app 直接子级；另有小地图 canvas 在 #ui-root 内）
  await expect(page.locator("#app > canvas")).toHaveCount(1);
  const status = page.evaluate(() => window.__CNM__.exec({ id: 1, action: "status" }));
  expect(await status).toMatchObject({ id: 1, ok: true, data: { state: "WeaponSelection", round: 1 } });
});

test("帧率上限 60：渲染帧率稳定在 60 而非跑满刷新率", async ({ page }) => {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  // 预热 1s 后采样。上限 62 是本测试重点：maxFPS=60 保证高刷屏不会跑满刷新率；
  // 下限仅验证渲染循环存活（无头环境 rAF 基准可能低至 30fps）。
  await page.waitForTimeout(1000);
  const fps = await page.evaluate(() => window.__CNM__.fps);
  expect(fps).toBeGreaterThan(20);
  expect(fps).toBeLessThanOrEqual(62);
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
