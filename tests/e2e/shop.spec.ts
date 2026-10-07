// R212 香火龛 e2e：真实点击/键盘路径（shoplock.spec 只驱动锁定与重抽的协议层）。
// 顺带守护本轮修掉的两个真实 UI 缺陷：
//   ① 卡片缺 position:relative，4 个锁定图标叠在面板角落——玩家锁不了 1/2/3 号槽；
//   ② 锁定态金色样式用了不存在的 [textContent="🔒"] 选择器，永不生效。
// 另守护 R202 一龛多购契约：购后余货逐笔上浮可续购、购罄才散龛。

import { expect, test } from "@playwright/test";

type SlotSnapshot = { label: string; price: number; kind: string; locked: boolean | undefined };

/** 开局并在脚下开一座香火龛。 */
async function openShop(page: import("@playwright/test").Page, coins = 99999) {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  await page.evaluate(() => window.__CNM__.exec({ action: "start" }));
  await page.evaluate((c) => {
    const sim = window.__CNM__.sim;
    sim.coins = c;
    const st = sim as unknown as { shrines: Array<{ id: number; x: number; y: number }>; shopLatch: boolean; nextId: number };
    st.shrines = [{ id: st.nextId++, x: sim.player.x, y: sim.player.y }];
    st.shopLatch = false;
  }, coins);
  await page.waitForSelector("#shop-cards .upgrade-card");
  await page.waitForTimeout(500); // 等出场动画（card-pop 0.28s + 延迟）落定
}

const slots = (page: import("@playwright/test").Page) =>
  page.evaluate(() =>
    (window.__CNM__.sim.shopOffer ?? []).map((s) => ({ label: s.label, price: s.price, kind: s.kind, locked: s.locked })),
  ) as Promise<SlotSnapshot[]>;

const coins = (page: import("@playwright/test").Page) => page.evaluate(() => window.__CNM__.sim.coins);

test("锁定图标挂在自己的卡上（几何回归：此前 4 个叠在面板角落）", async ({ page }) => {
  await openShop(page);
  const inside = await page.evaluate(() => {
    const cards = [...document.querySelectorAll(".upgrade-card")] as HTMLElement[];
    const locks = [...document.querySelectorAll(".uc-lock")] as HTMLElement[];
    return locks.map((l) => {
      const lb = l.getBoundingClientRect();
      const cx = lb.x + lb.width / 2, cy = lb.y + lb.height / 2;
      return cards.findIndex((c) => {
        const cb = c.getBoundingClientRect();
        return cx >= cb.x && cx <= cb.right && cy >= cb.y && cy <= cb.bottom;
      });
    });
  });
  expect(inside.length).toBeGreaterThanOrEqual(3);
  // 第 i 个锁必须落在第 i 张卡内（此前全是 -1）
  inside.forEach((cardIdx, i) => expect(cardIdx, `lock#${i}`).toBe(i));
});

test("点击第 3 张卡的锁：只锁定该槽，且挂上锁定态类名", async ({ page }) => {
  await openShop(page);
  const before = await slots(page);
  const coinsBefore = await coins(page);

  const lock = page.locator('.uc-lock[data-lock="2"]');
  await lock.click();

  const after = await slots(page);
  expect(after[2].locked).toBe(true);
  expect(after.filter((s) => s.locked)).toHaveLength(1); // 只锁了 1 个槽（此前会锁错槽）
  expect(await coins(page)).toBe(coinsBefore); // 锁按钮不触发购买
  expect(after.length).toBe(before.length);
  // 锁定态可视（R212 前该样式永不生效）
  await expect(page.locator('.uc-lock[data-lock="2"]')).toHaveClass(/locked/);
  await expect(page.locator('.uc-lock[data-lock="2"]')).toHaveText("🔒");
});

test("点击卡片购买：按价扣香火、该槽撤下、余货涨价（R202 连购）", async ({ page }) => {
  await openShop(page);
  const before = await slots(page);
  const coinsBefore = await coins(page);
  expect(before.length).toBeGreaterThanOrEqual(3);
  const minBefore = Math.min(...before.map((s) => s.price));

  await page.locator('.upgrade-card[data-idx="0"]').click();

  expect(await coins(page)).toBe(coinsBefore - before[0].price);
  await expect(page.locator("#shop-gold")).toHaveText(`香火钱 ${coinsBefore - before[0].price}`);
  const after = await slots(page);
  expect(after.length).toBe(before.length - 1); // 已购槽撤下，余货保留
  expect(Math.min(...after.map((s) => s.price))).toBeGreaterThan(minBefore); // 余货逐笔上浮
  expect(await page.evaluate(() => window.__CNM__.sim.paused)).toBe(true);
});

test("购罄才散龛：连购清空后报价消失、面板关闭、龛体撤走", async ({ page }) => {
  await openShop(page);
  const total = (await slots(page)).length;
  for (let i = 0; i < total; i++) {
    await page.locator(".upgrade-card[data-idx]").first().click();
  }
  await expect(page.locator("#shop-cards .upgrade-card")).toHaveCount(0);
  const st = await page.evaluate(() => ({
    offer: window.__CNM__.sim.shopOffer,
    shrines: window.__CNM__.sim.shrines.length,
    paused: window.__CNM__.sim.paused,
  }));
  expect(st.offer).toBeNull();
  expect(st.shrines).toBe(0); // 购罄龛散
  expect(st.paused).toBe(false);
});

test("香火不足：卡面标注 + 点击不扣费不撤槽（拒绝路径）", async ({ page }) => {
  await openShop(page, 0);
  const card = page.locator('.upgrade-card[data-idx="0"]');
  await expect(card).toContainText("香火不足");
  const before = await slots(page);

  await card.click();

  expect(await coins(page)).toBe(0);
  expect(await slots(page)).toEqual(before);
  await expect(page.locator("#notify-root")).toContainText("香火不足");
});

test("数字键购买与 Esc 离开：按价扣费、报价收起而龛体保留", async ({ page }) => {
  await openShop(page);
  const before = await slots(page);
  const coinsBefore = await coins(page);

  await page.keyboard.press("Digit1");
  expect(await coins(page)).toBe(coinsBefore - before[0].price);

  await page.keyboard.press("Escape");
  await expect(page.locator("#shop-cards .upgrade-card")).toHaveCount(0);
  const st = await page.evaluate(() => ({
    offer: window.__CNM__.sim.shopOffer,
    shrines: window.__CNM__.sim.shrines.length,
    paused: window.__CNM__.sim.paused,
  }));
  expect(st.offer).toBeNull(); // closeShop 清报价（不消费龛）
  expect(st.shrines).toBe(1); // 龛体保留，走开再回可重开
  expect(st.paused).toBe(false);
});
