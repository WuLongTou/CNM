import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    // 仅 sim 层逻辑回归；tests/e2e 归 Playwright（npm run e2e）
    include: ["tests/sim/**/*.test.ts"],
  },
});
