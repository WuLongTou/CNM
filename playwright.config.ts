import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "tests/e2e",
  timeout: 60_000,
  retries: 0,
  workers: 1, // 串行：帧率等计时敏感断言对 CPU 负载噪声敏感
  use: {
    baseURL: "http://127.0.0.1:5173",
    viewport: { width: 1280, height: 720 },
    screenshot: "only-on-failure",
  },
  webServer: {
    // public/assets 为生成产物不入库，起 dev server 前先同步，避免资源缺失导致页面以晦涩错误挂掉
    command: "npm run assets:sync && npm run dev",
    url: "http://127.0.0.1:5173",
    reuseExistingServer: true,
    timeout: 30_000,
  },
});
