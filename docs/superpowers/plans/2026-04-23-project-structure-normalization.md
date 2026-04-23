# 项目资源目录正规化实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 `assets/sprites/` 扁平化混乱结构正规化为标准目录，同时修复玩家贴图导入问题

**Architecture:** 
- 玩家贴图从 `processed/{charName}/` 提升到 `player/{charName}/`
- 删除多余的 `processed/` 层级
- 修复贴图导入状态（valid=false）

**Tech Stack:** Godot 4.x, C#, 文件系统重构

---

## 文件变更清单

| 操作 | 源路径 | 目标路径 |
|------|--------|----------|
| 移动 | `assets/sprites/player/processed/warrior/` | `assets/sprites/player/warrior/` |
| 移动 | `assets/sprites/player/processed/ranger/` | `assets/sprites/player/ranger/` |
| 移动 | `assets/sprites/player/processed/mage/` | `assets/sprites/player/mage/` |
| 删除 | `assets/sprites/player/processed/` 及其 .DS_Store |
| 修改 | `src/player/PlayerVfx.cs` — 更新路径 |

---

## 任务列表

### Task 1: 备份并移动玩家贴图目录

**Files:**
- Modify: `src/player/PlayerVfx.cs:143`
- 操作: `assets/sprites/player/processed/` → `assets/sprites/player/`

- [ ] **Step 1: 确认当前目录结构**

```bash
ls -la assets/sprites/player/
# 预期: 只有 processed 目录
```

- [ ] **Step 2: 创建目标目录**

```bash
mkdir -p assets/sprites/player/warrior
mkdir -p assets/sprites/player/ranger
mkdir -p assets/sprites/player/mage
```

- [ ] **Step 3: 移动贴图文件（保留 .import 文件）**

```bash
mv assets/sprites/player/processed/warrior/*.png assets/sprites/player/warrior/
mv assets/sprites/player/processed/warrior/*.import assets/sprites/player/warrior/
mv assets/sprites/player/processed/ranger/*.png assets/sprites/player/ranger/
mv assets/sprites/player/processed/ranger/*.import assets/sprites/player/ranger/
mv assets/sprites/player/processed/mage/*.png assets/sprites/player/mage/
mv assets/sprites/player/processed/mage/*.import assets/sprites/player/mage/
```

- [ ] **Step 4: 删除旧的 processed 目录**

```bash
rm -rf assets/sprites/player/processed/
```

- [ ] **Step 5: 验证新结构**

```bash
ls -la assets/sprites/player/
# 预期: warrior/, ranger/, mage/, 无 processed/
```

---

### Task 2: 更新代码中的路径引用

**Files:**
- Modify: `src/player/PlayerVfx.cs:143`

- [ ] **Step 1: 更新 processedDir 路径**

修改 PlayerVfx.cs 第 143 行：
```csharp
// 修改前
string processedDir = $"res://assets/sprites/player/processed/{charName}";

// 修改后
string processedDir = $"res://assets/sprites/player/{charName}";
```

- [ ] **Step 2: 更新原始帧路径（第 213-238 行）**

```csharp
// 第 213 行
string pngPath = $"res://assets/sprites/player/{charName}_walk_{i}.png";
// 第 214 行
string jpgPath = $"res://assets/sprites/player/{charName}_walk_{i}.jpg";
// 第 225 行
string idlePng = $"res://assets/sprites/player/{charName}_idle.png";
// 第 226 行
string idleJpg = $"res://assets/sprites/player/{charName}_idle.jpg";
// 第 237 行
string pngPath = $"res://assets/sprites/player/{charName}_attack_{i}.png";
// 第 238 行
string jpgPath = $"res://assets/sprites/player/{charName}_attack_{i}.jpg";
// 第 262 行
string atlasPath = $"res://assets/sprites/player/{charName}_atlas.png";
```

- [ ] **Step 3: 编译验证**

```bash
dotnet build
# 预期: 成功，无错误
```

---

### Task 3: 修复贴图导入（删除 .import 缓存让 Godot 重新导入）

**Files:**
- 操作: `assets/sprites/player/warrior/*.import`
- 操作: `assets/sprites/player/ranger/*.import`
- 操作: `assets/sprites/player/mage/*.import`

- [ ] **Step 1: 删除所有 .import 文件**

```bash
find assets/sprites/player -name "*.import" -exec rm {} \;
```

- [ ] **Step 2: 验证 .import 已删除**

```bash
find assets/sprites/player -name "*.import" | wc -l
# 预期: 0
```

- [ ] **Step 3: 列出当前贴图文件**

```bash
ls -la assets/sprites/player/warrior/*.png
# 预期: 18 个 .png 文件，无 .import
```

---

### Task 4: 提交变更

- [ ] **Step 1: 检查 git 状态**

```bash
git status
# 预期: 显示 assets/sprites/player/ 变更
```

- [ ] **Step 2: 添加并提交**

```bash
git add assets/sprites/player/
git add src/player/PlayerVfx.cs
git status
```

- [ ] **Step 3: 创建提交**

```bash
git commit -m "$(cat <<'EOF'
refactor: 正规化玩家贴图目录结构

- 将 player/processed/{charName}/ 移动到 player/{charName}/
- 删除多余的 processed 子目录层级
- 清除 .import 缓存让 Godot 重新导入贴图

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## 验证清单

完成所有任务后，在 Godot 编辑器中验证：

- [ ] 打开 Godot 项目
- [ ] FileSystem 中查看 `res://assets/sprites/player/`
- [ ] 应该显示: warrior/, ranger/, mage/ 三个目录
- [ ] 每个 PNG 文件旁应该有导入图标（不是 valid=false）
- [ ] 运行游戏，玩家应该显示为 96x96 像素角色（不再是火柴人）
- [ ] 切换武器类型（Tab），角色贴图应该随之变化