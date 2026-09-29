// R193 装备面板 e2e：C 键开关/卡片渲染/形态按钮（事件委托）。

import { test, expect } from "@playwright/test";

const Exec = (cmd: unknown) => (cmd: unknown) => cmd;

async function openEquip(page: import("@playwright/test").Page) {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  await page.click("#btn-start");
  await page.waitForFunction(() => window.__CNM__.exec({ action: "status" }).data?.state === "Combat");
  await page.keyboard.press("KeyC");
  await page.waitForSelector(".eq-item");
}

test("C 键开关：打开显示武器卡，Esc/C 关闭", async ({ page }) => {
  await openEquip(page);
  const cards = await page.locator(".eq-item").count();
  expect(cards).toBeGreaterThanOrEqual(1);
  await expect(page.locator(".eq-item").first()).toContainText("铁剑");
  await page.keyboard.press("KeyC");
  // 轮询等待关闭（避免瞬时竞态；paused 翻转是关闭的可靠信号）
  await page.waitForFunction(() => window.__CNM__.sim.paused === false, null, { timeout: 5000 });
  const eqOverlayHidden = await page.evaluate(() =>
    [...document.querySelectorAll(".overlay")].every((o) => o.classList.contains("hidden") || !o.querySelector(".eq-item")),
  );
  expect(eqOverlayHidden).toBe(true);
});

test("形态按钮：未进阶不显示（ups<3 门控）", async ({ page }) => {
  await openEquip(page);
  const aspectBtns = await page.locator(".eq-aspect").count();
  expect(aspectBtns).toBe(0); // 初始武器 ups=0，形态未解锁
});

test("模态暂停：装备面板打开时游戏冻结", async ({ page }) => {
  await openEquip(page);
  expect(await page.evaluate(() => window.__CNM__.sim.paused)).toBe(true);
  await page.keyboard.press("KeyC");
  await page.waitForTimeout(150);
  expect(await page.evaluate(() => window.__CNM__.sim.paused)).toBe(false);
});