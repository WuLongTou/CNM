// E2E 触屏适配：触屏上下文（hasTouch）挂载虚拟摇杆/按钮；
// 摇杆拖动与攻击按住反映到 sim.input；UI 按钮排走与键盘热键相同的模态逻辑。
// 桌面上下文（无触屏）不挂载控件。
import { expect, test } from "@playwright/test";

/** 在摇杆底座上派发一串合成 PointerEvent（自动化环境无真实触点）。 */
async function driveJoystick(
  page: import("@playwright/test").Page,
  seq: Array<{ type: string; dx: number; dy: number }>,
): Promise<void> {
  const box = await page.locator("#joy-base").boundingBox();
  if (!box) throw new Error("#joy-base 不存在");
  const cx = box.x + box.width / 2;
  const cy = box.y + box.height / 2;
  for (const s of seq) {
    await page.locator("#joy-base").dispatchEvent(s.type, {
      pointerId: 7, clientX: cx + s.dx, clientY: cy + s.dy,
      bubbles: true, pointerType: "touch", isPrimary: true,
    });
  }
  // R165 稳定等待：派发后等 RAF 采样到 poll（重载下 tick 边界竞态会漏采导致 flake）
  await page.waitForTimeout(120);
}

test.describe("桌面上下文：不挂载触屏控件", () => {
  test("无 hasTouch 时 #touch-root 不存在", async ({ page }) => {
    await page.goto("/");
    await page.waitForFunction(() => window.__CNM__?.ready === true);
    await expect(page.locator("#touch-root")).toHaveCount(0);
  });
});

test.describe("触屏上下文（手机视口）", () => {
  test.use({ hasTouch: true, viewport: { width: 390, height: 844 } });

  test("挂载摇杆/攻击/冲刺与 UI 按钮排（开局后可见）", async ({ page }) => {
    await page.goto("/");
    await page.waitForFunction(() => window.__CNM__?.ready === true);
    // R19：欢迎页隐藏触屏层（首屏干净）
    await expect(page.locator("#touch-root")).toBeHidden();
    await page.click("#btn-start");
    await page.waitForFunction(() => window.__CNM__.exec({ action: "status" }).data?.state === "Combat");
    await expect(page.locator("#joy-base")).toBeVisible();
    await expect(page.locator("#tbtn-attack")).toBeVisible();
    await expect(page.locator("#tbtn-dash")).toBeVisible();
    await expect(page.locator("#touch-ui")).toBeVisible();
  });

  test("摇杆左推驱动 moveX，回中归零", async ({ page }) => {
    await page.goto("/");
    await page.waitForFunction(() => window.__CNM__?.ready === true);
    await page.click("#btn-start");
    await page.waitForFunction(() => window.__CNM__.exec({ action: "status" }).data?.state === "Combat");
    // R165 挂载等待：触屏层异步追加，boundingBox 不自动等待
    await expect(page.locator("#joy-base")).toBeVisible();
    await driveJoystick(page, [
      { type: "pointerdown", dx: 0, dy: 0 },
      { type: "pointermove", dx: -58, dy: 0 },
    ]);
    await page.waitForFunction(() => window.__CNM__.sim.input.moveX < -0.9);
    await driveJoystick(page, [{ type: "pointerup", dx: -58, dy: 0 }]);
    await page.waitForFunction(() =>
      window.__CNM__.sim.input.moveX === 0 && window.__CNM__.sim.input.moveY === 0);
  });

  test("摇杆死区不产生漂移", async ({ page }) => {
    await page.goto("/");
    await page.waitForFunction(() => window.__CNM__?.ready === true);
    await page.click("#btn-start");
    // R165 挂载等待：触屏层异步追加，boundingBox 不自动等待
    await expect(page.locator("#joy-base")).toBeVisible();
    // 轻触（位移 < 18% 半径）输出应保持 0
    await driveJoystick(page, [
      { type: "pointerdown", dx: 0, dy: 0 },
      { type: "pointermove", dx: 8, dy: 8 },
    ]);
    await page.waitForTimeout(120);
    const input = await page.evaluate(() => ({
      x: window.__CNM__.sim.input.moveX, y: window.__CNM__.sim.input.moveY,
    }));
    expect(input.x).toBe(0);
    expect(input.y).toBe(0);
    await driveJoystick(page, [{ type: "pointerup", dx: 8, dy: 8 }]);
  });

  test("攻击按钮按住持续攻击，松开停止", async ({ page }) => {
    await page.goto("/");
    await page.waitForFunction(() => window.__CNM__?.ready === true);
    await page.click("#btn-start");
    await page.waitForFunction(() => window.__CNM__.exec({ action: "status" }).data?.state === "Combat");
    await page.locator("#tbtn-attack").dispatchEvent("pointerdown", {
      pointerId: 7, bubbles: true, pointerType: "touch", isPrimary: true,
    });
    await page.waitForFunction(() => window.__CNM__.sim.input.attack === true);
    await page.locator("#tbtn-attack").dispatchEvent("pointerup", {
      pointerId: 7, bubbles: true, pointerType: "touch", isPrimary: true,
    });
    await page.waitForFunction(() => window.__CNM__.sim.input.attack === false);
  });

  // R19 移动端首屏与面板可用性：欢迎页藏小地图/触屏/教练条；模态打开时藏摇杆与击/冲
  // （留 UI 按钮排解暂停）；职业卡片横排不断字；HUD 状态项不断字；高面板内部可滚。
  test("欢迎页隐藏小地图/触屏层/教练条", async ({ page }) => {
    await page.goto("/");
    await page.waitForFunction(() => window.__CNM__?.ready === true);
    await expect(page.locator("#minimap")).toBeHidden();
    await expect(page.locator("#touch-root")).toBeHidden();
    await expect(page.locator("#coach-tip")).toBeHidden();
  });

  test("模态打开时隐藏摇杆与击/冲，保留 UI 按钮排", async ({ page }) => {
    await page.goto("/");
    await page.waitForFunction(() => window.__CNM__?.ready === true);
    await page.click("#btn-start");
    await page.waitForFunction(() => window.__CNM__.exec({ action: "status" }).data?.state === "Combat");
    await page.click("#tbtn-help");
    await expect(page.locator(".overlay:not(.hidden) .help-panel")).toBeVisible();
    await expect(page.locator("#joy-base")).toBeHidden();
    await expect(page.locator("#touch-btns")).toBeHidden();
    await expect(page.locator("#touch-ui")).toBeVisible();
  });

  test("职业卡片横排不断字，页面无横向溢出", async ({ page }) => {
    await page.goto("/");
    await page.waitForFunction(() => window.__CNM__?.ready === true);
    const noOverflow = await page.evaluate(() =>
      document.documentElement.scrollWidth <= window.innerWidth + 1);
    expect(noOverflow).toBe(true);
    // 逐字竖排的坏味道：卡片被内容撑到 3 倍高以上；阈值 400 留足字体差异余量
    const heights = await page.locator(".role-card").evaluateAll((els) =>
      els.map((el) => el.getBoundingClientRect().height));
    expect(heights.length).toBe(5); // R123/R124 五职业（剑修/弓手/术士/隐士/武圣）
    for (const h of heights) expect(h).toBeLessThan(400);
  });

  test("HUD 状态项不断字，高升级面板内部可滚", async ({ page }) => {
    await page.goto("/");
    await page.waitForFunction(() => window.__CNM__?.ready === true);
    await page.click("#btn-start");
    await page.waitForFunction(() => window.__CNM__.exec({ action: "status" }).data?.state === "Combat");
    const spanHeights = await page.locator(".stats span").evaluateAll((els) =>
      els.map((el) => el.getBoundingClientRect().height));
    for (const h of spanHeights) expect(h).toBeLessThan(30);
    await page.evaluate(() => {
      window.__CNM__.sim.pickups.push({ id: 1, x: window.__CNM__.sim.player.x + 5, y: window.__CNM__.sim.player.y });
    });
    await page.waitForSelector("#upgrade-cards .upgrade-card");
    const scrollable = await page.locator("#upgrade-cards").evaluate((el) => {
      const panel = el.closest(".panel-wide") as HTMLElement;
      const cs = getComputedStyle(panel);
      return cs.maxHeight !== "none" && cs.overflowY === "auto";
    });
    expect(scrollable).toBe(true);
  });

  test("UI 按钮排打开帮助面板，Esc 语义按钮可关闭", async ({ page }) => {
    await page.goto("/");
    await page.waitForFunction(() => window.__CNM__?.ready === true);
    await page.click("#btn-start");
    await page.waitForFunction(() => window.__CNM__.exec({ action: "status" }).data?.state === "Combat");
    await page.click("#tbtn-help");
    await expect(page.locator(".overlay:not(.hidden) .help-panel")).toBeVisible();
    // 暂停按钮 = Esc 语义：有模态时先关闭模态
    await page.click("#tbtn-pause");
    await expect(page.locator(".overlay:not(.hidden) .help-panel")).toBeHidden();
  });
});
