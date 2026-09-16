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

  test("挂载摇杆/攻击/冲刺与 UI 按钮排", async ({ page }) => {
    await page.goto("/");
    await page.waitForFunction(() => window.__CNM__?.ready === true);
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
