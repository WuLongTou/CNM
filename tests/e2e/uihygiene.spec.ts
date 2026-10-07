// R206 实机走查修复回归：选人页隐藏 HUD / 模态不跨局驻留 / 结算复盘不泄漏 span 标签。

import { test, expect } from "@playwright/test";

test("R206 选人页隐藏 HUD，开局后显示", async ({ page }) => {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  await page.waitForSelector("#hud", { state: "attached" }); // 隐藏态用 attached 等
  await page.waitForFunction(() => document.getElementById("hud")!.classList.contains("hidden"));
  await page.click("#btn-start");
  await page.waitForFunction(() => window.__CNM__.exec({ action: "status" }).data?.state === "Combat");
  await page.waitForFunction(() => !document.getElementById("hud")!.classList.contains("hidden"));
});

test("R206 欢迎页开手账后开局：模态不跨局驻留", async ({ page }) => {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  await page.keyboard.press("h"); // 欢迎页开手账（合法：开局前可读）
  await page.waitForFunction(
    () => [...document.querySelectorAll(".overlay:not(.hidden)")].some((o) => o.textContent?.includes("手账")),
  );
  await page.click("#btn-start");
  await page.waitForFunction(() => window.__CNM__.exec({ action: "status" }).data?.state === "Combat");
  await page.waitForFunction(
    () => ![...document.querySelectorAll(".overlay:not(.hidden)")].some((o) => o.textContent?.includes("手账")),
  );
});

test("R206 结算复盘不泄漏 span 标签且含下一目标", async ({ page }) => {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  await page.click("#btn-start");
  await page.waitForFunction(() => window.__CNM__.exec({ action: "status" }).data?.state === "Combat");
  await page.evaluate(() => {
    const s = window.__CNM__.sim;
    s.player.hp = 1;
    s.exec({ action: "spawn", args: { type: "游魂", count: 3, near: "player" } });
  });
  await page.waitForFunction(() => window.__CNM__.exec({ action: "status" }).data?.state === "GameOver", null, { timeout: 30000 });
  // R206 面板延迟 0.7s 弹出并填充——等文本非空再断言
  await page.waitForFunction(() => (document.getElementById("gameover-debrief")?.textContent ?? "").length > 0, null, { timeout: 10000 });
  const text = await page.evaluate(() => document.getElementById("gameover-debrief")!.textContent ?? "");
  expect(text).not.toContain("<span");
  expect(text).toContain("下一目标");
});
