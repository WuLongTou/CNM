// E2E 功能补全闭环：升级符箓（掉落→拾取→三选一→应用）与天气感知（HUD/覆盖层）。
// sim 侧行为已有单测覆盖，此处验证 UI 装配与玩家可见路径。
import { expect, test } from "@playwright/test";

test("拾取符箓弹出三选一，点击卡片应用升级", async ({ page }) => {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  await page.click("#btn-start");
  await page.waitForFunction(() => window.__CNM__.exec({ action: "status" }).data?.state === "Combat");
  // 在玩家脚下塞一枚符箓（掉落 roll 的确定性由单测覆盖）；
  // 给一把武器（真实流程中徒手无法升级）、拉高血量避免站桩被妖潮围殴致死
  await page.evaluate(() => {
    const s = window.__CNM__.sim;
    s.player.hp = 1e9;
    if (s.player.weapons.length === 0) s.exec({ action: "give", args: { weapon: "tiejian" } });
    s.pickups.push({ id: 9001, x: s.player.x, y: s.player.y });
  });
  await page.waitForFunction(() => window.__CNM__.sim.upgradeOffers !== null);
  await expect(page.locator(".upgrade-cards .upgrade-card")).toHaveCount(3);
  const upsBefore = await page.evaluate(() => {
    const s = window.__CNM__.sim;
    return s.player.weapons[s.upgradeOffers![0].weaponIdx].ups;
  });
  await page.locator(".upgrade-cards .upgrade-card").first().click();
  await page.waitForFunction(() => window.__CNM__.sim.upgradeOffers === null);
  const upsAfter = await page.evaluate(() => {
    const s = window.__CNM__.sim;
    const w = s.player.weapons.find((x: { ups: number }) => x.ups > 0);
    return w ? w.ups : 0;
  });
  expect(upsAfter).toBeGreaterThan(upsBefore);
  // 报价应答后游戏解除暂停（paused 已由报价置位）
  await expect(page.locator(".upgrade-cards .upgrade-card")).toHaveCount(0);
});

test("天气变化反映到 HUD 标签", async ({ page }) => {
  await page.goto("/");
  await page.waitForFunction(() => window.__CNM__?.ready === true);
  await page.click("#btn-start");
  await page.waitForFunction(() => window.__CNM__.exec({ action: "status" }).data?.state === "Combat");
  await expect(page.locator("#weather")).toHaveText(/天气 晴/);
  await page.evaluate(() => {
    const s = window.__CNM__.sim;
    s.weather.state = { ...s.weather.state, type: "BloodMoon", intensity: "Moderate" };
  });
  await page.waitForFunction(() => window.__CNM__.sim.weather.state.type === "BloodMoon");
  // tick 推进后（帧循环持续运行）HUD 刷新为血月
  await page.waitForFunction(() => document.querySelector("#weather")?.textContent?.includes("血月"));
});
