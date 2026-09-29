// R169 放弃本局 e2e（evaluate 直驱——点击层竞态绕开，逻辑断言等价）。

import { test, expect } from "@playwright/test";

test("暂停菜单放弃本局：回选人页且再开局正常", async ({ page }) => {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  await page.click("#btn-start");
  await page.waitForFunction(() => window.__CNM__.exec({ action: "status" }).data?.state === "Combat");

  // Esc 开暂停菜单（桌面上下文无触屏按钮排）
  await page.keyboard.press("Escape");
  await page.waitForSelector("#btn-pause-abandon");
  await page.click("#btn-pause-abandon"); // 第一段：武装确认
  await expect(page.locator("#btn-pause-abandon")).toHaveText("确认放弃？（再点一次）");
  await page.click("#btn-pause-abandon"); // 第二段：确认放弃

  // 回选人页：state + 欢迎层可见
  await page.waitForFunction(() => window.__CNM__.sim.state === "WeaponSelection");
  await page.waitForTimeout(300);
  const welcomeVisible = await page.evaluate(() => {
    const s = window.__CNM__.sim;
    const welcome = document.getElementById("save-stats")?.closest(".overlay");
    return { state: s.state, welcome: welcome ? !welcome.classList.contains("hidden") : false };
  });
  expect(welcomeVisible.state).toBe("WeaponSelection");
  expect(welcomeVisible.welcome).toBe(true);

  // 再开局：正常进 Combat（放弃局不影响后续开局）
  await page.click("#btn-start");
  await page.waitForFunction(() => window.__CNM__.exec({ action: "status" }).data?.state === "Combat");
  const hist = await page.evaluate(() => {
    const st = window.__CNM__.exec({ action: "status" }).data as { run_history: unknown[] };
    return st.run_history.length;
  });
  expect(hist).toBe(0); // 放弃局不入战绩
});