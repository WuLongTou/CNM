import { defineConfig } from "vite";

export default defineConfig({
  // 部署根路径可用 VITE_BASE 覆盖（如 /zhanyao/），默认根路径部署
  base: process.env.VITE_BASE ?? "/",
  server: {
    port: 5173,
    strictPort: true,
    host: "127.0.0.1",
  },
  publicDir: "public",
  build: {
    target: "es2022",
    sourcemap: true,
  },
});
