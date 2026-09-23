# 斩妖录 (CNM) — Web 版

简笔画速写本风除妖生存 roguelike。TypeScript + PixiJS v8 实现（旧引擎工程已整体
移除，仅存本 web 版；旧实现可从 git 历史早期提交恢复）。

通用约束（Git 规范、Commit 格式、验证流程、工具链偏好等）以全局 `~/AGENTS.md` 为准，
本文件只做项目特有补充，不得放宽或违背全局约束。


## 硬约束
- Git 额外约束：在全局 Git 规范之上，本项目额外禁止 `git cherry-pick`（保持线性历史）。
- 包管理器：npm（`package-lock.json` 为唯一权威锁文件；pnpm-lock.yaml 与 pnpm-workspace.yaml 已删除，勿再引入——git 历史中仍有，仅作考古。本机若因误执行 `pnpm install` 再现 `pnpm-lock.yaml`，属残留，直接删除，勿提交）。
- 模态/选择/UI 页面快捷键必须独占输入（硬约束，自 Godot 版平移）：
  - 装备/合成/帮助/暂停/融合/GameOver/Victory 等任何模态页面打开时，游戏操作快捷键
    （WASD/方向键移动、空格攻击、Shift 冲刺）必须 100% 失效。
    实现机制：`GameSim.paused` 置位 + `GameSim.tick` 门控清空全部输入状态。
  - 绝不复用已绑定的游戏按键做 UI 快捷键（游戏键绑定：WASD/方向键、Space、Shift；
    UI 键：H、C、I、Esc、反引号、Tab（帮助切页）、Enter、Delete）。
    新增 UI 快捷键只用未绑定键或纯鼠标。
- 主题约束：项目为《斩妖录》妖物设定，已全面清除旧主题字样。新增代码、文案、
  资产名不得引入旧主题相关命名（旧怪名称等）；敌人资产源文件统一中性命名（enemy_*）。
- 美术风格约束（M11 起，M22 v2 填充式重绘）：全局简笔画速写本风格——米白
  纸面、填充色块 + 炭棕描边（马克笔手绘感）、血红点缀。角色贴图迭代一律改
  tools/gen-sketch.mjs 参数重新生成，不手贴外部素材；调色与布局约定见
  README「美术」章节；玩家/妖物源图为透明 PNG，sync-assets 对其跳过剥底。

## 构建 & 测试（项目根目录执行）

- `npm run dev` — Vite dev server @ 127.0.0.1:5173
- `npm run build` — tsc 类型检查 + vite build
- `npm test` — vitest 逻辑回归（sim 层，秒级）
- `npm run e2e` — Playwright E2E（自动起 dev server）
- `npm run assets:sync` — 从 assets-src 生成 public/assets（剥底/切帧/atlas）

## AI 自动测试入口（详见 skill cnm-auto-test）

页面 window.__CNM__ 指令协议（exec/getEvents/sim）已移入 skill cnm-auto-test，此处不再重复。

## 协作入口
- 任务分级：低风险（trivial）直接实现；高风险（非 trivial）按全局「方案选择」对比 2–3 种方案。
- 游戏数值改动需与 `src/sim/config.ts`、`src/sim/weapons.ts` 数据表对照。
- 战斗/游戏性核心（R1/R2 起）：方向性近战判定、击退/硬直、多武器齐射、质变词条、
  分幕刷怪（CFG.Acts）均在 `src/sim/game.ts` + `src/sim/weapons.ts` + `src/sim/config.ts`；
  改动手感参数前先读 `tests/sim/combat.test.ts` 的既有语义断言。
