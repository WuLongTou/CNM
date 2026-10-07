// R213 模态面板命中审计：系统巡检「点不中」类缺陷。
// 本类缺陷近期已两次咬人：R210（每帧重建 DOM，真实点击在 mousedown/mouseup 之间
// 元素被换掉）、R212（锁定图标绝对定位失去定位基准，4 个叠在面板角落）。
// 判据：模态内每个可见交互元素的**中心点**必须命中自身或其后代（不被遮挡），
// 且中心点落在面板框内（不溢出）。

import { test, expect } from "@playwright/test";

type Hit = { label: string; w: number; h: number; hitSelf: boolean; insidePanel: boolean };

async function audit(page: import("@playwright/test").Page): Promise<{ name: string; items: Hit[] }> {
  return page.evaluate(() => {
    const overlays = [...document.querySelectorAll(".overlay:not(.hidden)")] as HTMLElement[];
    const root = overlays.find((o) => o.querySelector("button, [role=button]"))!;
    const name = (root.querySelector("h2")?.textContent ?? "?").trim();
    const panel = (root.querySelector(".panel") as HTMLElement | null) ?? root;
    const pb = panel.getBoundingClientRect();
    const sel = "button, [role=button], a[href], input:not([type=hidden]), select";
    const items = ([...root.querySelectorAll(sel)] as HTMLElement[]).filter((el) => {
      const r = el.getBoundingClientRect();
      return r.width > 0 && r.height > 0;
    });
    return {
      name,
      items: items.map((el) => {
        const b = el.getBoundingClientRect();
        const cx = b.x + b.width / 2, cy = b.y + b.height / 2;
        const hit = document.elementFromPoint(cx, cy);
        return {
          label: ((el.textContent ?? "").trim().slice(0, 16) || el.id || el.className).replace(/\s+/g, " "),
          w: Math.round(b.width),
          h: Math.round(b.height),
          hitSelf: !!hit && (hit === el || el.contains(hit)),
          insidePanel: cx >= pb.x - 1 && cx <= pb.right + 1 && cy >= pb.y - 1 && cy <= pb.bottom + 1,
        };
      }),
    };
  });
}

function expectHealthy(a: { name: string; items: Hit[] }) {
  expect(a.items.length, `${a.name}：应有可交互元素`).toBeGreaterThan(0);
  for (const it of a.items) {
    expect(it.w, `[${a.name}] ${it.label}：宽度`).toBeGreaterThan(0);
    expect(it.h, `[${a.name}] ${it.label}：高度`).toBeGreaterThan(0);
    expect(it.hitSelf, `[${a.name}] ${it.label}：中心点被遮挡`).toBe(true);
    expect(it.insidePanel, `[${a.name}] ${it.label}：溢出面板`).toBe(true);
  }
}

async function boot(page: import("@playwright/test").Page) {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  await page.evaluate(() => window.__CNM__.exec({ action: "start" }));
}

test("审计：手账（H）", async ({ page }) => {
  await boot(page);
  await page.keyboard.press("KeyH");
  await page.waitForSelector(".overlay:not(.hidden) h2");
  await page.waitForTimeout(250);
  expectHealthy(await audit(page));
});

test("审计：行囊装备（C）", async ({ page }) => {
  await boot(page);
  // 剑修开局武器进阶到形态门槛：面板里才有可交互元素（形态按钮）可审
  await page.evaluate(() => {
    const w = window.__CNM__.sim.player.weapons.find((x) => x.defId === "tiejian")!;
    w.ups = 3;
  });
  await page.keyboard.press("KeyC");
  await page.waitForSelector(".eq-aspect");
  await page.waitForTimeout(250);
  expectHealthy(await audit(page));
});

test("审计：工作台合成（I）", async ({ page }) => {
  await boot(page);
  await page.keyboard.press("KeyI");
  await page.waitForSelector("#craft-list button[data-recipe]");
  await page.waitForTimeout(250);
  expectHealthy(await audit(page));
});

test("审计：暂停菜单（Esc）", async ({ page }) => {
  await boot(page);
  await page.keyboard.press("Escape");
  await page.waitForSelector(".overlay:not(.hidden) h2");
  await page.waitForTimeout(250);
  expectHealthy(await audit(page));
});

test("审计：升级三选一（符箓）", async ({ page }) => {
  await boot(page);
  await page.evaluate(() => {
    const s = window.__CNM__.sim;
    s.player.hp = 1e9;
    s.pickups.push({ id: 9001, x: s.player.x, y: s.player.y });
  });
  await page.waitForFunction(() => window.__CNM__.sim.upgradeOffers !== null);
  await page.waitForSelector(".upgrade-cards .upgrade-card");
  await page.waitForTimeout(400); // 等 card-pop 出场动画
  expectHealthy(await audit(page));
});

test("审计：祭坛融合", async ({ page }) => {
  await boot(page);
  await page.evaluate(() => {
    window.__CNM__.exec({ action: "give", args: { weapon: "tiejian" } });
    window.__CNM__.exec({ action: "give", args: { weapon: "fazhang" } });
    window.__CNM__.exec({ action: "offer_fusion" });
  });
  await page.waitForSelector("#fusion-info", { state: "visible" });
  await page.waitForTimeout(250);
  expectHealthy(await audit(page));
});

test("审计：香火龛", async ({ page }) => {
  await boot(page);
  await page.evaluate(() => {
    const sim = window.__CNM__.sim;
    sim.coins = 9999;
    const st = sim as unknown as { shrines: Array<{ id: number; x: number; y: number }>; shopLatch: boolean; nextId: number };
    st.shrines = [{ id: st.nextId++, x: sim.player.x, y: sim.player.y }];
    st.shopLatch = false;
  });
  await page.waitForSelector("#shop-cards .upgrade-card");
  await page.waitForTimeout(500);
  expectHealthy(await audit(page));
});

test("审计：结算面板（GameOver）", async ({ page }) => {
  await boot(page);
  // 借鉴 uihygiene 的成熟触发：残血 + 塞游魂围殴（直接置 dead 不会走死亡结算）
  await page.evaluate(() => {
    const s = window.__CNM__.sim;
    s.player.hp = 1;
    s.exec({ action: "spawn", args: { type: "游魂", count: 3, near: "player" } });
  });
  await page.waitForFunction(() => window.__CNM__.exec({ action: "status" }).data?.state === "GameOver", null, { timeout: 20000 });
  await page.waitForTimeout(900); // 面板延迟 0.7s 弹出
  expectHealthy(await audit(page));
});
