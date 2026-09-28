// R137 秘密职业与荣誉金字 e2e：欢迎页五卡（秘密 2 卡暗金+锁定）、成就页金字。

import { test, expect } from "@playwright/test";

test.describe("R137 秘密职业与荣誉金字", () => {
  test("欢迎页五张职业卡：隐士/武圣暗金锁定卡（未解锁态）", async ({ page }) => {
    await page.goto("/");
    await page.waitForFunction(() => window.__CNM__?.ready === true);
    const cards = page.locator(".role-card");
    await expect(cards).toHaveCount(5);
    await expect(page.locator(".role-card-secret")).toHaveCount(2);
    await expect(page.locator('.role-card[data-role="hermit"]')).toContainText("隐士");
    await expect(page.locator('.role-card[data-role="hermit"]')).toContainText("🔒");
    await expect(page.locator('.role-card[data-role="wusheng"]')).toContainText("武圣");
  });

  test("M 键静音：通知反馈且静音标志持久化", async ({ page }) => {
    await page.goto("/");
    await page.waitForFunction(() => window.__CNM__?.ready === true);
    await page.keyboard.press("KeyM");
    await expect(page.locator(".notify").last()).toContainText("已静音");
    const muted = await page.evaluate(() => localStorage.getItem("zhanyaolu_muted"));
    expect(muted).toBe("1");
    await page.keyboard.press("KeyM");
    await expect(page.locator(".notify").last()).toContainText("声音已恢复");
  });
});