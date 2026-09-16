# 斩妖录 (CNM) — Web 版

血色涂鸦除妖生存 roguelike。TypeScript + PixiJS v8 实现（旧引擎工程已整体移除，
仅存本 web 版；旧实现可从 git 历史早期提交恢复）。

通用约束（Git 规范、Commit 格式、验证流程、工具链偏好等）以全局 AGENTS.md 为准，
本文件只做项目特有补充，不得放宽或违背全局约束。

## 硬约束
- Git 额外约束：在全局 Git 规范之上，本项目额外禁止 `git cherry-pick`（保持线性历史）。
- 临时文件目录：AI agent 工作期间产生的所有中间文件统一写入 `.workwork/`（已 gitignore）。
- 模态/选择/UI 页面快捷键必须独占输入（硬约束，自 Godot 版平移）：
  - 装备/合成/帮助/暂停/融合/GameOver 等任何模态页面打开时，游戏操作快捷键
    （WASD/方向键移动、空格攻击、Shift 冲刺、Tab 切目标等）必须 100% 失效。
    实现机制：`GameSim.paused` 置位 + `GameSim.tick` 门控清空全部输入状态。
  - 绝不复用已绑定的游戏按键做 UI 快捷键（已绑定：WASD/方向键、Space、Shift、Tab、
    H、C、I、反引号、Esc）。UI 快捷键只用未绑定键（Enter、Delete 等）或纯鼠标。
- 主题约束：项目为《斩妖录》妖物设定，已全面清除旧主题字样。新增代码、文案、
  资产名不得引入旧主题相关命名（旧怪名称等）；敌人资产源文件统一中性命名（enemy_*）。

## 构建 & 测试（项目根目录执行）

- `npm run dev` — Vite dev server @ 127.0.0.1:5173
- `npm run build` — tsc 类型检查 + vite build
- `npm test` — vitest 逻辑回归（sim 层，秒级）
- `npm run e2e` — Playwright E2E（自动起 dev server）
- `npm run assets:sync` — 从 assets-src 生成 public/assets（剥底/切帧/atlas）

## AI 自动测试入口

页面加载后暴露 `window.__CNM__`（JSON 指令协议，与旧版 cmd-port 同构）：
`exec({id, action, args})` 支持 status/game_state/start/input/choose/force_combat/
spawn/give/upgrade/weapons/craft/offer_fusion/buy_meta/meta/inventory/teleport/quit；
`getEvents()` 拉取事件推送；`sim` 直接暴露 GameSim 实例。

## 协作入口
- 默认先理解上下文，再做最小必要改动；低风险任务直接实现，高风险任务再升级流程。
- 游戏数值改动需与 `src/sim/config.ts`、`src/sim/weapons.ts` 数据表对照。
