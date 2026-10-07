// R208 存档导出/导入 e2e：欢迎页备份按钮的真实 UI 接线（sim 层往返已有 saveswap 覆盖）。
// 覆盖：①导出下载（文件名/内容/提示）②有效导入（走真实 filechooser，状态与提示）
// ③非法文件拒绝（提示 + 状态不被破坏）。

/// <reference types="node" />
// 说明：tsconfig 的 types 仅含 vite/client，故本文件用三斜线指令显式引入 @types/node
//（仅评测/测试文件需要 Buffer 构造上传文件负载）。
import { test, expect } from "@playwright/test";

/** 进入欢迎页（不开始对局——备份按钮只在欢迎页）。 */
async function openWelcome(page: import("@playwright/test").Page) {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  await page.waitForSelector("#btn-export-save");
}

test("导出存档：下载 JSON + 文件名规范 + 内容与当前进度一致", async ({ page }) => {
  await openWelcome(page);
  // 预置一个可识别的进度
  await page.evaluate(() => window.__CNM__.sim.importSave({ highScore: 1234, totalKills: 567 }));
  const before = await page.evaluate(() => window.__CNM__.sim.exportSave().highScore);

  const [download] = await Promise.all([
    page.waitForEvent("download"),
    page.click("#btn-export-save"),
  ]);

  // 文件名：zhanyaolu-save-YYYY-MM-DD.json
  expect(download.suggestedFilename()).toMatch(/^zhanyaolu-save-\d{4}-\d{2}-\d{2}\.json$/);

  const stream = await download.createReadStream();
  let text = "";
  for await (const chunk of stream) text += chunk.toString("utf8");
  const parsed = JSON.parse(text) as { highScore: number; totalKills: number };
  expect(parsed.highScore).toBe(before);
  expect(parsed.totalKills).toBe(567);

  await expect(page.locator("#backup-hint")).toHaveText("存档已下载");
});

test("导入存档：真实 filechooser 覆写进度 + 提示", async ({ page }) => {
  await openWelcome(page);
  const payload = JSON.stringify({ highScore: 4321, totalKills: 777 });

  const [chooser] = await Promise.all([
    page.waitForEvent("filechooser"),
    page.click("#btn-import-save"),
  ]);
  await chooser.setFiles({ name: "backup.json", mimeType: "application/json", buffer: Buffer.from(payload) });

  await expect(page.locator("#backup-hint")).toHaveText("存档已导入");
  const after = await page.evaluate(() => {
    const s = window.__CNM__.sim.exportSave();
    return { highScore: s.highScore, totalKills: s.totalKills };
  });
  expect(after).toEqual({ highScore: 4321, totalKills: 777 });
});

test("导入非法文件：提示失败且原进度不被破坏", async ({ page }) => {
  await openWelcome(page);
  await page.evaluate(() => window.__CNM__.sim.importSave({ highScore: 888 }));

  const [chooser] = await Promise.all([
    page.waitForEvent("filechooser"),
    page.click("#btn-import-save"),
  ]);
  await chooser.setFiles({ name: "broken.json", mimeType: "application/json", buffer: Buffer.from("这不是 JSON") });

  await expect(page.locator("#backup-hint")).toHaveText("导入失败：文件不是有效存档");
  const kept = await page.evaluate(() => window.__CNM__.sim.exportSave().highScore);
  expect(kept).toBe(888);
});
