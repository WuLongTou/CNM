# 僵尸大战 (CNM)

Godot 4.7 + C# 游戏项目（僵尸大战）。

> 通用约束（Git 规范、Commit 格式、验证流程、工具链偏好、Godot 路径等）以全局 `~/.config/opencode/AGENTS.md` 为准，本文件只做项目特有补充，不得放宽或违背全局约束。

## 硬约束
- **Git 额外约束**：在全局 Git 规范之上，本项目**额外禁止 `git cherry-pick`**（保持线性历史）。
- **临时文件目录**：本项目临时文件 / 截图 / 中间输出统一写入 `.local/`（已 gitignore）。
- **模态/选择/UI 页面快捷键必须独占输入**（硬约束，2026-06-11 立）：
  - 卡片选择、强化选择、暂停、Help、GameOver 等任何"模态/选择/UI"页面打开时，
    **游戏操作的快捷键（WASD/方向键移动、空格攻击、Shift 冲刺、Tab 切目标等）必须 100% 失效**，
    绝不允许"在卡片页面按 A 还能让角色移动"这种误操作。
  - **绝不复用"常用操作"按键做 UI 快捷键**（硬约束，2026-06-11 立）：
    任何 input action 已绑定的键（如 move=WASD、attack=Space、dash=Shift、cycle_target=Tab、
    toggle_help=H）都不可用于 UI 快捷键，否则即出现"按 S 跳过时角色向下走"这种双重绑定 bug。
    UI 快捷键只能用未绑定的键（如 Enter、Delete、BackQuote、斜杠等）或纯鼠标操作。
  - 实现要求：
    1. `UIManager` 拦截 1/2/3/Enter/Delete 等卡片快捷键并 `SetInputAsHandled` 之外，**`PlayerInput` 也必须
       在 `GameManager.CurrentState != Combat` 时直接清空所有输入状态并 return**——单靠
       SetInputAsHandled 拦不住 `Input.IsActionPressed` 这种全局查询。
    2. 任何新增的模态/选择/UI 页面必须接入 `GameManager.CurrentState` 状态机，
       或在自身 `_Ready`/`Toggle` 时显式禁用 PlayerInput。
    3. 修改 `project.godot` 里的 input action 绑定前，必须先 grep 检查目标键是否已绑定其他 action。
  - 错误示范：把"跳过强化"绑到 `S/Enter`，而 `move_down` 已绑 S 键——
    玩家在卡片页按 S 会同时触发"跳过"和"向下走"。正确做法：UI 快捷键用未绑定的键（如 `Enter`、`Delete`）。

## 任务分级
- **L0**：问答、只读分析、查文档。
- **L1**：影响面小、无架构变化的局部改动；读完上下文后直接实现。
- **L2**：多点改动或多文件改动；先做简短计划，再实现。
- **L3**：新增功能、跨模块改动、架构调整；先设计，再计划，再实现。

## 验证分级
- **开发完成**：完成与改动直接相关的格式化、lint、编译；不得遗留调试代码或临时注释。
- **准备合并**：在"开发完成"基础上，按全局「完成前验证」过完整 format → lint →（如有用例）test → build；改动涉及场景/资源时，额外在 Godot 编辑器中验证。

## 构建 & 测试
- `mkdir -p build && /Applications/Godot_mono.app/Contents/MacOS/Godot --headless --export-debug "macOS" "build/CNM-debug.zip"` — 导出调试构建（需先在 Godot 编辑器中配置 `export_presets.cfg` 的 "macOS" 预设）
- `/Applications/Godot_mono.app/Contents/MacOS/Godot --headless --quit-after 1` — 启动场景冒烟检查
- `/Applications/Godot_mono.app/Contents/MacOS/Godot --headless -- --autotest` — 自动回归测试（`--autotest` 是项目自定义参数，由 `Main.cs` 解析，跳过欢迎画面并禁用刷怪）
- `dotnet build` — 编译 C# 脚本

## 约束
- **C# 规范**：遵循 Godot C# 风格，节点引用通过 `[Export]` 或 `GetNode<T>()`。
- **场景结构**：场景树层级在 `scenes/` 中组织，C# 脚本在 `src/` 中组织。
- **资源管理**：美术/音频资源放入 `assets/`，数据文件放入 `data/`。

## 视觉资源（Blender 烘焙管线）

多步骤工作流迁移到 skill：`/blender-bake 输出目录`。详见 `.claude/skills/blender-bake/SKILL.md`。

## 协作入口
- 默认先理解上下文，再做最小必要改动；低风险任务直接实现，高风险任务再升级流程。
- 默认目标是达到"开发完成"或"准备合并"状态；只有在用户明确要求时，才进入合并回主线、删除临时 worktree / 分支等后续动作。
