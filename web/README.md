# 斩妖录（Web 版）

血色涂鸦风除妖生存 roguelike：扮演除妖师，在持续逼近的妖潮中存活。
TypeScript + PixiJS v8 实现的完整重写版。玩法数值对齐 C# 旧版数据表，
**主题为「斩妖」设定**：玩家是除妖师，敌人统称妖物。仓库内不含任何旧引擎工程文件。

## 架构

```
src/
  sim/      纯逻辑层（零 DOM/渲染依赖）：状态机、除妖师、妖物 AI、武器、伤害、刷怪、
            难度、天气、成就、结算。固定 60Hz 步长 + 种子化 PRNG → 同 seed 同输入同结果，
            vitest 直接跑。
  render/   PixiJS v8 渲染层：只读 sim 状态绘制（地面/实体/弹道/相机/帧动画）。
  ui/       DOM UI 层：HUD、欢迎、暂停、帮助、装备、合成、结算等（手账血色风）。
  app/      装配：主循环（固定步长累加器）、键盘输入、debug API、音频。
  data/     数值表（妖物三维/武器表/天气表/配方，源自 C# 版数据）。
tests/
  sim/      vitest 逻辑回归。
  e2e/      Playwright E2E（完整对局闭环）。
tools/      资产预处理：从 web/assets-src 生成 public/assets（剥底/切帧/atlas），
            源文件名全部中性（enemy_*）。
```

## 调试 / AI 测试 API

页面加载后暴露 `window.__CNM__`（JSON 指令协议，与 Godot 旧版 cmd-port 同构）：

```js
__CNM__.exec({ id: 1, action: "status" })   // status/game_state/start/input/choose/
__CNM__.getEvents()                          //   force_combat/spawn/give/teleport/quit
__CNM__.sim                                  // GameSim 实例（深度断言用）
```

`input` 指令与真实键盘叠加、持续有效；模态打开时 sim 自动清空游戏输入（模态独占输入硬约束）。

## 命令

```bash
npm run assets:sync   # 从 assets-src 生成 public/assets（加资产后需重跑）
npm run dev           # Vite dev server @ 127.0.0.1:5173
npm run build         # tsc 类型检查 + vite build
npm test              # vitest 逻辑回归
npm run e2e           # Playwright E2E（自动起 dev server）
```

## 里程碑

- [x] M0 骨架与测试闭环
- [x] M1 核心战斗垂直切片：移动/攻击/连击、妖物追击+分离力、精英词缀、Boss（每 5 阶）、
      世界刷怪维持 [6,18]、45s 威胁阶、GameOver 结算存档（localStorage）
- [x] M1.5 主题重构：定名《斩妖录》，敌人统一妖物设定
- [x] M2 武器系统：24 件武器（近战/远程/魔法/地雷）× 全机制（散射/穿透/追踪/链式/
      毒/冰缓/暴击/溅射/血代价）、升级卡 3 次进阶 6 次终极、宝箱、装备面板（C）
- [x] M3 世界：障碍碰撞与采集（木/石/纤维/浆果）、合成 6 配方（I）、天气 4 型
      （雨/雾/血月数值修正）、成就 7 项跨局持久、祭坛融合（Enter/Delete）、
      Boss 三阶段（66%/33% 加速+召唤蚀群）、永久升级（金币购买，跨局生效）
- [x] M4 UI：帮助手账（H，操作/妖物图鉴/机制）、暂停+音量设置（Esc）、小地图、
      通知条、模态独占输入（打开即冻结游戏）
- [x] M5 打击感与音频：hitstop（暴击 45/击杀 70/妖王 220ms）、震屏、伤害数字、
      WebAudio（BGM + 全套音效，音量持久化）

## 硬约束（沿用项目 AGENTS.md）

模态页面打开时游戏快捷键必须 100% 失效；UI 快捷键绝不复用已绑定的游戏键
（WASD/Space/Shift/Tab/H/C/反引号）。

## 待办（美术）

现有妖物贴图为占位素材（视觉与斩妖主题的最终形象有差距），后续按主题重绘
spritesheet，文件名沿用 enemy_* 放入 web/assets-src/sprites/enemies/sheets/ 即可。
