import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    // 仅 sim 层逻辑回归；tests/e2e 归 Playwright（npm run e2e）
    include: ["tests/sim/**/*.test.ts"],
    // R143：fakeTimers 需 mock Date（财神日 setSystemTime 验证用）
    fakeTimers: { toFake: ["Date", "setTimeout", "clearTimeout", "setInterval", "clearInterval"] },
  },
});
