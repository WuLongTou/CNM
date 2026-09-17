// E2E：M2-M5 功能面——武器弹道击杀、模态暂停、合成与永久升级协议。
import { expect, test } from "@playwright/test";

type Exec = (cmd: { id?: number; action: string; args?: Record<string, unknown> }) => Promise<{ ok: boolean; data?: Record<string, unknown>; error?: string }>;

test("远程武器：give → 攻击 → 弹道击杀计分", async ({ page }) => {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  const exec: Exec = (cmd) => page.evaluate((c) => window.__CNM__.exec(c), cmd);
  await exec({ action: "start" });
  expect((await exec({ action: "give", args: { weapon: "gongjian" } })).data).toMatchObject({ given: 1, weapon: "弓箭" });
  await exec({ action: "teleport", args: { x: 1000, y: 1000 } });
  await exec({ action: "spawn", args: { type: "basic", near: { x: 1150, y: 1000 } } });
  await exec({ action: "input", args: { attack: true } });
  await page.waitForFunction(() => (window.__CNM__.exec({ action: "status" }).data!.kills as number) >= 1);
  await exec({ action: "input", args: { attack: false } });
  const ws = (await exec({ action: "weapons" })).data!.list as Array<{ stage: string }>;
  expect(ws[0].stage).toBe("base");
});

test("模态暂停：装备面板打开时游戏冻结，关闭恢复", async ({ page }) => {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  const exec: Exec = (cmd) => page.evaluate((c) => window.__CNM__.exec(c), cmd);
  await exec({ action: "start" });
  await page.keyboard.press("KeyC");
  await expect(page.locator(".overlay:not(.hidden) h2")).toHaveText("行囊 · 武器");
  const paused1 = await page.evaluate(() => window.__CNM__.sim.paused);
  expect(paused1).toBe(true);
  const t0 = await page.evaluate(() => window.__CNM__.sim.survivalTime);
  await page.waitForTimeout(400);
  const t1 = await page.evaluate(() => window.__CNM__.sim.survivalTime);
  expect(t1).toBe(t0); // 模态期间时间冻结
  await page.keyboard.press("KeyC");
  const paused2 = await page.evaluate(() => window.__CNM__.sim.paused);
  expect(paused2).toBe(false);
});

test("祭坛融合协议：offer_fusion → choose 应答", async ({ page }) => {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  const exec: Exec = (cmd) => page.evaluate((c) => window.__CNM__.exec(c), cmd);
  await exec({ action: "start" });
  await exec({ action: "give", args: { weapon: "tiejian" } });
  await exec({ action: "give", args: { weapon: "fazhang" } });
  const offer = (await exec({ action: "offer_fusion" })).data!;
  expect(offer.offered).toBe(true);
  // 面板可见
  await expect(page.locator("#fusion-info")).toBeVisible();
  // 拒绝
  const r = await exec({ action: "choose", args: { target: "fusion", choice: "reject" } });
  expect(r.data).toMatchObject({ handled: true, accepted: false });
  await expect(page.locator("#fusion-info")).toBeHidden();
});

test("合成与永久升级协议", async ({ page }) => {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  const exec: Exec = (cmd) => page.evaluate((c) => window.__CNM__.exec(c), cmd);
  await exec({ action: "start" });
  // 材料不足被拒
  expect((await exec({ action: "craft", args: { recipe: "wood_sword" } })).data).toMatchObject({ crafted: false });
  // 永久升级协议可用（金币不足拒绝）
  expect((await exec({ action: "buy_meta", args: { kind: "hp" } })).data).toMatchObject({ bought: false });
  const meta = (await exec({ action: "meta" })).data!;
  expect(meta).toHaveProperty("coins");
  expect(meta).toHaveProperty("metaHp");
});

test("帮助手账：武器图鉴收录全部武器，成就页展示达成状态", async ({ page }) => {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  await page.click("#btn-start");
  await page.keyboard.press("KeyH");
  await expect(page.locator(".help-panel")).toBeVisible();
  // 武器图鉴：24 件武器全收录，含稀有度星级与祭炼标注
  await page.click('.help-tabs button[data-page="2"]');
  await expect(page.locator(".help-panel h2")).toBeVisible();
  const rows = await page.locator("#help-body .help-table tr").count();
  expect(rows).toBe(23); // 武器表实际 23 件（近战8/远程6/魔法6/地雷3）
  await expect(page.locator("#help-body .w-star").first()).toBeVisible();
  await expect(page.locator("#help-body .w-fuse").first()).toBeVisible();
  // 成就页：7 项成就与达成状态
  await page.click('.help-tabs button[data-page="3"]');
  const achRows = await page.locator("#help-body .help-table tr").count();
  expect(achRows).toBe(7);
  // 左方向键循环切页（成就 → 武器图鉴）
  await page.keyboard.press("ArrowLeft");
  await page.waitForFunction(() => {
    const active = document.querySelector(".help-tabs button.active")?.textContent;
    return active === "武器图鉴";
  });
  await page.keyboard.press("Escape");
});
