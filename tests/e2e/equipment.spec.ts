// R193 装备面板 e2e：C 键开关/卡片渲染/形态按钮（事件委托）。

import { test, expect } from "@playwright/test";

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

test("形态按钮点击：切换生效、面板即时刷新、聚合数值随形态变化", async ({ page }) => {
  await openEquip(page);
  // 开局铁剑进阶到形态门槛（ups>=3），重开面板触发刷新
  await page.evaluate(() => {
    const w = window.__CNM__.sim.player.weapons.find((x) => x.defId === "tiejian")!;
    w.ups = 3;
  });
  await page.keyboard.press("KeyC");
  await page.keyboard.press("KeyC");
  const btn = page.locator(".eq-aspect").first();
  await expect(btn).toBeVisible();
  await expect(btn).toContainText("本形");
  const aspBefore = await page.evaluate(() => window.__CNM__.sim.stats.aspdMult);

  // 点击必须真的生效——R210 前每帧重建 DOM 会让 mousedown/mouseup 之间元素被换掉而落空
  await btn.click();
  await expect
    .poll(() => page.evaluate(() => window.__CNM__.sim.player.weapons.some((w) => w.aspect === 1)))
    .toBe(true);
  // 面板自身操作后即时刷新：按钮文案切成形态名（影袭）
  await expect(page.locator(".eq-aspect").first()).toContainText("影袭");
  // 聚合数值立刻重算（影袭 攻速 ×1.5）——R210 补的 recalc：此前切形态后 stats 要等
  // 下一次别的 recalc 才更新，面板显示与挥砍弧半径都滞后
  const aspAfter = await page.evaluate(() => window.__CNM__.sim.stats.aspdMult);
  expect(aspAfter).toBeCloseTo(aspBefore * 1.5, 5);
});

test("模态暂停：装备面板打开时游戏冻结", async ({ page }) => {
  await openEquip(page);
  expect(await page.evaluate(() => window.__CNM__.sim.paused)).toBe(true);
  await page.keyboard.press("KeyC");
  await page.waitForTimeout(150);
  expect(await page.evaluate(() => window.__CNM__.sim.paused)).toBe(false);
});