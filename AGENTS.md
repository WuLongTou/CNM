# 斩妖录 (CNM) — Web 版

简笔画速写本风除妖生存 roguelike。TypeScript + PixiJS v8 实现（仅本 web 版）。

通用约束（Git 规范、Commit 格式、验证流程、工具链偏好等）以全局 `~/AGENTS.md` 为准，
本文件只做项目特有补充，不得放宽或违背全局约束。

## 按需加载的 skill（.agents/skills/）

| skill | 何时加载 |
|---|---|
| cnm-auto-test | 用 window.__CNM__ 协议做自动化测试/状态探查/跳阶时 |
| sprite-sheet-ai | 让生图 AI（Gemini 等）产出多帧动画/图标网格等游戏贴图时 |


## 硬约束
- Git 额外约束：在全局 Git 规范之上，本项目额外禁止 `git cherry-pick`（保持线性历史）。
- 包管理器：npm（`package-lock.json` 为唯一权威锁文件；禁止引入 pnpm，仓库内不得出现 `pnpm-lock.yaml` / `pnpm-workspace.yaml`——误执行 `pnpm install` 生成属残留，直接删除勿提交；判据：`ls pnpm-lock.yaml pnpm-workspace.yaml` 报不存在）。
- 模态/选择/UI 页面快捷键必须独占输入（硬约束）：
  - 装备/合成/帮助/暂停/融合/GameOver/Victory 等任何模态页面打开时，游戏操作快捷键
    （WASD/方向键移动、空格攻击、Shift 冲刺）必须 100% 失效。
    实现机制：`GameSim.paused` 置位 + `GameSim.tick` 门控清空全部输入状态。
  - 绝不复用已绑定的游戏按键做 UI 快捷键（游戏键绑定：WASD/方向键、Space、Shift；
    UI 键：H、C、I、Esc、反引号、Tab（帮助切页）、Enter、Delete）。
    新增 UI 快捷键只用未绑定键或纯鼠标。
- 主题约束：项目为《斩妖录》妖物设定；代码、文案、资产名只使用该设定的命名，不得混入其它/历史主题命名；
  敌人资产源文件统一中性命名（enemy_*）。
- 美术风格约束：全局简笔画速写本风格——米白
  纸面、填充色块 + 炭棕描边（马克笔手绘感）、血红点缀。调色与布局约定见
  README「美术」章节；玩家/妖物源图为透明 PNG，sync-assets 对其跳过剥底。
  资产来源两条路：程序化生成器 tools/gen-sketch.mjs（玩家/妖物基准造型），
  或 AI 生图资产（Gemini 等）——后者必须先经 .workwork 后处理（去底/归一化）
  达到管线格式再入 assets-src，风格规范与体检标准同上，不得直接手贴外部素材。

## 构建 & 测试（项目根目录执行）

- `npm run dev` — Vite dev server @ 127.0.0.1:5173
- `npm run build` — tsc 类型检查 + vite build
- `npm test` — vitest 逻辑回归（sim 层，秒级）
- `npm run e2e` — Playwright E2E（自动起 dev server）
- `npm run assets:sync` — 从 assets-src 生成 public/assets（剥底/切帧/atlas）

## CI 门禁与本地验证分工

重活（依赖安装、资产同步、全量单测、构建、preview 冒烟）一律交给 GitHub Actions，本地只跑秒级检查。
完整流程见全局 skill `github-ci-flow`：开发分支推送 → `gh pr create` → `gh pr checks --watch`
全绿 → `gh pr merge --rebase --delete-branch` → 本地 `git pull --ff-only` 并清理 worktree。

- 本地（提交前，秒级）：`npx tsc --noEmit`（类型检查，实测约 0.8s）
- CI 覆盖（`.github/workflows/ci.yml`，job `Unit Tests & Build (node <22|24>)`，node 22/24 双档矩阵）：`npm ci` → `npm run assets:sync` → `npm test`（vitest）→ `npm run build`（tsc + vite）→ preview 冒烟（起 `vite preview` 断言 HTTP 200 + HTML）→ 上传 `dist/` 产物（仅 node 24 档）；push main 与 PR 均触发
- CI 覆盖不到、必须本地/真机验证的：`npm run e2e`（Playwright，断言对帧率计时敏感，共享 runner 噪声会放大偶发失败，故不进 CI，改核心玩法/渲染后本地手跑）
- 发版/发布门禁不变：无项目专属本地门禁

## AI 自动测试入口（详见 skill cnm-auto-test）

页面 window.__CNM__ 指令协议（exec/getEvents/sim）见 skill cnm-auto-test。

## 协作入口
- 任务分级：低风险（trivial）直接实现；高风险（非 trivial）按全局「方案选择」对比 2–3 种方案。
- 游戏数值改动需与 `src/sim/config.ts`、`src/sim/weapons.ts` 数据表对照。
- 战斗/游戏性核心：方向性近战判定、击退/硬直、多武器齐射、质变词条、
  分幕刷怪（CFG.Acts）均在 `src/sim/game.ts` + `src/sim/weapons.ts` + `src/sim/config.ts`；
  改动手感参数前先读 `tests/sim/combat.test.ts` 的既有语义断言。