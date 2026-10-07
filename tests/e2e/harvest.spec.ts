// R211 采集 e2e：近战砍倒身边的树 → 材料入账（端到端，含新增 harvested 反馈链路不抛错）。
// 采到材料也正是合成面板（R210）的前置输入，这条链路此前只有协议级覆盖。

import { test, expect } from "@playwright/test";

test("近战采集：砍倒身边的树后木材入账", async ({ page }) => {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  await page.evaluate(() => window.__CNM__.exec({ action: "start" }));
  // 清随机地形并在身侧放一棵树（确定性）
  await page.evaluate(() => {
    const sim = window.__CNM__.sim;
    sim.terrain.obstacles.length = 0;
    sim.terrain.obstacles.push({
      id: 9001, x: sim.player.x + 30, y: sim.player.y, r: 20, kind: "tree", hp: 1, dead: false,
    });
  });

  await page.evaluate(() => window.__CNM__.exec({ action: "input", args: { attack: true } }));
  await page.waitForFunction(() => window.__CNM__.sim.inventory.counts.wood >= 2, null, { timeout: 5000 });
  await page.evaluate(() => window.__CNM__.exec({ action: "input", args: { attack: false } }));

  const st = await page.evaluate(() => ({
    wood: window.__CNM__.sim.inventory.counts.wood,
    dead: window.__CNM__.sim.terrain.obstacles.every((o) => o.dead),
  }));
  expect(st.wood).toBeGreaterThanOrEqual(2);
  expect(st.dead).toBe(true);
});
