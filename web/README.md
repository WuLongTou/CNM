# 僵尸大战 Web 版（重构中）

Godot 4.7 + C# 版《僵尸大战》的 TypeScript + Web 技术栈重写。第一动机：AI 自动测试体验
（逻辑回归 vitest 秒级、E2E 用 Playwright、游戏状态可观测）。Godot 版保留在仓库根目录并行对照。

## 架构

```
src/
  sim/      纯逻辑层（零 DOM/渲染依赖）：状态机、玩家、僵尸 AI、伤害、刷怪、难度、结算。
            固定 60Hz 步长 + 种子化 PRNG → 同 seed 同输入同结果，vitest 直接跑。
  render/   PixiJS v8 渲染层：只读 sim 状态绘制（地面/实体/相机/帧动画）。
  ui/       DOM UI 层：HUD、欢迎画面、GameOver（手账血色涂鸦风，对齐 HandDrawnTheme）。
  app/      装配：主循环（固定步长累加器）、键盘输入、debug API。
  data/     （规划）从 C# GameConfig/TypeStats/WeaponFactory 抽出的数值表——当前在 sim/config.ts。
tests/
  sim/      vitest 逻辑回归（对标 Godot 版 AutoTestRunner/EvoTestRunner 的断言）。
  e2e/      Playwright E2E（对标 .workwork 的 selftest/e2e_events.py 流程）。
tools/      资产预处理：从 ../assets 生成 public/assets（剥底/切帧/atlas，规则直译 TextureUtils.cs）。
```

数值规则逐项对齐 C# 版（GameConfig / Zombie.TypeStats / GameManager.SpawnTable 等），改动时两边同步。

## 调试 / AI 测试 API

页面加载后暴露 `window.__CNM__`（协议对齐 Godot 版 ExternalCommandService 的 action 与字段名）：

```js
__CNM__.exec({ id: 1, action: "status" })      // status/game_state/start/input/choose/
__CNM__.getEvents()                             //   force_combat/spawn/give/teleport/quit
__CNM__.sim                                     // GameSim 实例（深度断言用）
```

`input` 指令与真实键盘叠加、持续有效；非战斗态 sim 自动清空输入（模态独占输入硬约束）。
screenshot 由 Playwright 原生 `page.screenshot` 替代。

## 命令

```bash
npm run assets:sync   # 从 ../assets 生成 public/assets（预处理 spritesheet，加资产后需重跑）
npm run dev           # Vite dev server @ 127.0.0.1:5173
npm run build         # tsc 类型检查 + vite build
npm test              # vitest 逻辑回归
npm run e2e           # Playwright E2E（自动起 dev server）
```

## 里程碑

- [x] M0 骨架与测试闭环（worktree `feature/web-rewrite`）
- [x] M1 核心战斗垂直切片：移动/攻击/连击、僵尸追击+分离力、精英词缀、Boss（每 5 阶）、
      世界刷怪维持 [6,18]、45s 威胁阶、GameOver 结算存档（localStorage）
- [ ] M2 武器全套（63 武器表/融合进化/宝箱/合成台）+ 剩余 11 种僵尸行为 + Boss 三阶段
- [ ] M3 世界（地图生成/障碍/交互台/天气/成就/数学系统）
- [ ] M4 UI 全套（23 面板 + 模态独占输入）
- [ ] M5 视觉与性能（VFX/hitstop/震屏/涂鸦细节层复刻）

## 硬约束（从项目 AGENTS.md 平移）

模态/欢迎/结算页面打开时游戏快捷键必须 100% 失效（sim 状态机门控清空输入）；
UI 快捷键绝不复用已绑定的游戏键（WASD/Space/Shift/Tab/H/C/反引号）。
