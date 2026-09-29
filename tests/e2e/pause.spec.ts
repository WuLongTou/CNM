// R188 暂停菜单 e2e：音量持久化/表现开关/放弃两段确认/继续恢复。

import { test, expect } from "@playwright/test";

async function openPause(page: import("@playwright/test").Page) {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  await page.click("#btn-start");
  await page.waitForFunction(() => window.__CNM__.exec({ action: "status" }).data?.state === "Combat");
  await page.keyboard.press("Escape");
  await page.waitForSelector("#btn-pause-abandon");
}

test("音量滑杆：调整即持久化，重载后保持", async ({ page }) => {
  await openPause(page);
  await page.locator("#vol-music").fill("60");
  await page.locator("#vol-sfx").fill("45");
  await page.reload();
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  await page.click("#btn-start");
  await page.waitForFunction(() => window.__CNM__.exec({ action: "status" }).data?.state === "Combat");
  await page.keyboard.press("Escape");
  await page.waitForSelector("#vol-music");
  const mv = await page.locator("#vol-music").inputValue();
  const sv = await page.locator("#vol-sfx").inputValue();
  expect(mv).toBe("60");
  expect(sv).toBe("45");
});

test("表现开关：点按切换 + aria-pressed 反馈", async ({ page }) => {
  await openPause(page);
  const btn = page.locator("#set-shake");
  const before = await btn.getAttribute("aria-pressed");
  await btn.click();
  expect(await btn.getAttribute("aria-pressed")).toBe(before === "true" ? "false" : "true");
  await btn.click();
  expect(await btn.getAttribute("aria-pressed")).toBe(before);
});

test("放弃本局：两段确认 → 回选人页", async ({ page }) => {
  await openPause(page);
  await page.click("#btn-pause-abandon");
  await expect(page.locator("#btn-pause-abandon")).toHaveText("确认放弃？（再点一次）");
  await page.click("#btn-pause-abandon");
  await page.waitForFunction(() => window.__CNM__.sim.state === "WeaponSelection");
  const stats = await page.evaluate(() => window.__CNM__.exec({ action: "status" }).data as { run_history: unknown[] });
  expect(stats.run_history).toHaveLength(0);
});

test("继续（Esc）：暂停恢复战斗", async ({ page }) => {
  await openPause(page);
  await page.keyboard.press("Escape");
  await page.waitForFunction(() => window.__CNM__.sim.state === "Combat" && !window.__CNM__.sim.paused);
  const visible = await page.evaluate(
    () => !document.querySelector("#btn-resume")!.closest(".overlay")!.classList.contains("hidden"),
  );
  expect(visible).toBe(false);
});