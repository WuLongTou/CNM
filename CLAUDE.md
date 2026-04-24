# CLAUDE.md

## 项目概述
Godot 4.6 + C# 游戏项目（僵尸大战）。

## 硬约束
- **Git 安全**：禁止破坏性 git 操作；同步到 main 前必须整理为单功能单 commit，并保持线性历史。合并流程：worktree 内 `git rebase main`（解决冲突）→ 必要时 `git reset --soft main && git commit` 压成一条 → 回到 main 执行 `git merge --ff-only <branch>` → 清理 worktree 和分支。禁止使用 `git cherry-pick` 或产生 merge commit。
- **Commit 规范**：commit message 格式为 `<type>(<scope>): <subject>`，例如 `feat(combat): add zombie AI behavior`。
- **临时文件**：所有临时文件、截图、中间输出写入 `.local/` 目录。

## 任务分级
- **L0**：问答、只读分析、查文档。
- **L1**：影响面小、无架构变化的局部改动；读完上下文后直接实现。
- **L2**：多点改动或多文件改动；先做简短计划，再实现。
- **L3**：新增功能、跨模块改动、架构调整；先设计，再计划，再实现。

## 验证分级
- **开发完成**：完成与改动直接相关的格式化、lint、编译和验证；不得遗留调试代码或临时注释。
- **准备合并**：在"开发完成"的基础上，如改动涉及场景/资源变更，需在 Godot 编辑器中验证。

## 构建 & 测试
- `/Applications/Godot.app/Contents/MacOS/Godot --headless --export-debug "macOS"` — 导出调试构建
- `/Applications/Godot.app/Contents/MacOS/Godot --headless --check-only` — 脚本检查
- `dotnet build` — 编译 C# 脚本

## 约束
- **C# 规范**：遵循 Godot C# 风格，类名 PascalCase，节点引用通过 `[Export]` 或 `GetNode<T>()`。
- **场景结构**：场景树层级在 `scenes/` 中组织，C# 脚本在 `src/` 中组织。
- **资源管理**：美术/音频资源放入 `assets/`，数据文件放入 `data/`。

## 协作入口
- 默认先理解上下文，再做最小必要改动；低风险任务直接实现，高风险任务再升级流程。
- 默认目标是达到"开发完成"或"准备合并"状态；只有在用户明确要求时，才进入合并回主线、删除临时 worktree / 分支等后续动作。
