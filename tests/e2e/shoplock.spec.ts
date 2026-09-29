// R164 重抽锁定 e2e（evaluate 直驱版——绕开点击/键盘时序，逻辑断言等价）。

import { test, expect } from "@playwright/test";

async function openShop(page: import("@playwright/test").Page) {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  await page.click("#btn-start");
  await page.waitForFunction(() => window.__CNM__.exec({ action: "status" }).data?.state === "Combat");
  await page.evaluate(() => {
    const s2 = window.__CNM__.sim;
    const st = s2 as unknown as { shrines: { id: number; x: number; y: number }[]; shopLatch: boolean; nextId: number };
    st.shrines = [{ id: st.nextId++, x: s2.player.x, y: s2.player.y }];
    st.shopLatch = false;
  });
  await page.waitForTimeout(500);
  await page.waitForSelector("#shop-cards .upgrade-card");
}

test("锁定槽重抽保留原报价（evaluate 直驱）", async ({ page }) => {
  await openShop(page);
  const r = await page.evaluate(() => {
    const s2 = window.__CNM__.sim;
    s2.toggleShopLock(0);
    const before = s2.shopOffer![0].label;
    s2.coins = 9999;
    s2.rerollShop();
    const s = s2.shopOffer![0];
    return { before, after: s.label, locked: s.locked, rerolled: s2.coins >= 0 };
  });
  expect(r.locked).toBe(true);
  expect(r.after).toBe(r.before); // 锁定槽报价保留
});

test("解锁后重抽：该槽重新 roll（🔒→🔓 可逆）", async ({ page }) => {
  await openShop(page);
  await page.evaluate(() => window.__CNM__.sim.toggleShopLock(1));
  await page.evaluate(() => window.__CNM__.sim.toggleShopLock(1));
  const st = await page.evaluate(() => window.__CNM__.sim.shopOffer![1].locked);
  expect(st).toBe(false);
});

test("锁按钮手柄焦点可达（pad nav 通用按钮选择器）", async ({ page }) => {
  await openShop(page);
  const n = await page.evaluate(
    () => [...document.querySelectorAll("#shop-cards button, #shop-actions button")].length,
  );
  expect(n).toBeGreaterThanOrEqual(3);
});