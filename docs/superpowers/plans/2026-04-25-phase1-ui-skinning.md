# 手绘血色 UI 皮肤化 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将游戏所有 UI 元素改造为手绘血色涂鸦风格（暗色战场 + 淡红信纸便签）

**Architecture:** 创建 `HandDrawnTheme` 静态工具类，集中管理所有手绘风格常量和工厂方法。UIManager/GameOverUI/ScreenEffects 调用 HandDrawnTheme 替换现有硬编码样式。

**Tech Stack:** Godot 4.6 + C#, StyleBoxFlat, Label 主题覆盖

---

## 文件结构

| 操作 | 文件 | 职责 |
|------|------|------|
| 创建 | `src/ui/HandDrawnTheme.cs` | 手绘风格常量 + 工厂方法 |
| 修改 | `src/core/GameConfig.cs:218-234` | 替换 UI 色彩常量为血色主题 |
| 修改 | `src/ui/UIManager.cs:136-352` | HUD 面板 → 便签风格 |
| 修改 | `src/ui/UIManager.cs:364-579` | 武器选择 → 便签卡牌 |
| 修改 | `src/ui/UIManager.cs:581-666` | Tooltip + 暂停 → 便签风格 |
| 修改 | `src/ui/UIManager.cs:668-875` | 回合横幅 + 控制面板 + 欢迎面板 |
| 修改 | `src/ui/UIManager.cs:1096-1285` | 武器选择显示（稀有度颜色） |
| 修改 | `src/ui/UIManager.cs:1477-1527` | 强化卡渲染 |
| 修改 | `src/ui/UIManager.cs:1879-2001` | 回合总结 |
| 修改 | `src/ui/UIManager.cs:2057-2098` | 血条颜色更新 |
| 修改 | `src/ui/UIManager.cs:1214-1276` | 卡牌悬停样式 |
| 修改 | `src/ui/GameOverUI.cs:20-198` | Game Over → 血色信纸 |
| 修改 | `src/ui/ScreenEffects.cs` | 特效颜色 → 血色主题 |
| 修改 | `src/ui/WeaponFormatter.cs:26-48` | 稀有度颜色 → 血色主题 |

---

## Task 1: 创建 HandDrawnTheme 工具类

**Files:**
- 创建: `src/ui/HandDrawnTheme.cs`

- [x] **Step 1: 创建 HandDrawnTheme.cs**

```csharp
using Godot;
using System;

namespace ZombieWars.UI;

public static class HandDrawnTheme
{
    // ═══════ 战场底色 ═══════
    public static readonly Color BattlefieldBg = new(0.078f, 0.039f, 0.039f);
    public static readonly Color BattlefieldMid = new(0.239f, 0.102f, 0.102f);
    public static readonly Color BattlefieldAccent = new(0.753f, 0.224f, 0.169f);
    public static readonly Color BattlefieldBright = new(0.906f, 0.298f, 0.235f);

    // ═══════ 便签纸色 ═══════
    public static readonly Color NoteBg = new(0.941f, 0.910f, 0.910f);
    public static readonly Color NoteBgHover = new(0.96f, 0.94f, 0.94f);
    public static readonly Color NoteMid = new(0.878f, 0.816f, 0.816f);
    public static readonly Color NoteBorder = new(0.545f, 0.227f, 0.227f);
    public static readonly Color NoteText = new(0.290f, 0.102f, 0.102f);
    public static readonly Color NoteTextDim = new(0.353f, 0.227f, 0.227f);

    // ═══════ 强调色 ═══════
    public static readonly Color BloodRed = new(0.753f, 0.224f, 0.169f);
    public static readonly Color BloodBright = new(0.906f, 0.298f, 0.235f);
    public static readonly Color InkBlack = new(0.09f, 0.04f, 0.04f);
    public static readonly Color PencilGray = new(0.35f, 0.28f, 0.28f);

    // ═══════ 稀有度 ═══════
    public static readonly Color RarityCommon = new(0.4f, 0.35f, 0.35f);
    public static readonly Color RarityRare = BloodRed;
    public static readonly Color RarityEpic = new(0.953f, 0.612f, 0.071f);
    public static readonly Color RarityCommonBg = new(0.25f, 0.2f, 0.2f, 0.95f);
    public static readonly Color RarityRareBg = new(0.2f, 0.1f, 0.1f, 0.95f);
    public static readonly Color RarityEpicBg = new(0.25f, 0.2f, 0.1f, 0.95f);

    // ═══════ 强化卡 ═══════
    public static readonly Color UpgradeBg = new(0.2f, 0.15f, 0.15f, 0.95f);
    public static readonly Color UpgradeBorder = new(0.6f, 0.35f, 0.35f);
    public static readonly Color UpgradeTitle = new(0.85f, 0.65f, 0.65f);

    // ═══════ HP 颜色 ═══════
    public static readonly Color HpHigh = new(0.65f, 0.35f, 0.35f);
    public static readonly Color HpMed = new(0.75f, 0.45f, 0.25f);
    public static readonly Color HpLow = BloodRed;

    public static readonly Color NotePaperShadow = new(0, 0, 0, 0.45f);

    // ═══════ 工厂方法 ═══════

    public static StyleBoxFlat MakeNotePanel(
        float rotationDeg = 0f,
        float borderWidth = 1.5f,
        bool dashed = false,
        float borderRadius = 2f,
        float shadowSize = 4f)
    {
        var style = new StyleBoxFlat
        {
            BgColor = NoteBg,
            BorderColor = NoteBorder,
            BorderWidthTop = borderWidth,
            BorderWidthBottom = borderWidth,
            BorderWidthLeft = borderWidth,
            BorderWidthRight = borderWidth,
            CornerRadiusTopLeft = (int)borderRadius,
            CornerRadiusTopRight = (int)borderRadius,
            CornerRadiusBottomLeft = (int)borderRadius,
            CornerRadiusBottomRight = (int)borderRadius,
            ShadowColor = NotePaperShadow,
            ShadowSize = shadowSize,
            ShadowOffset = new Vector2(2, 2),
            AntiAliasing = false
        };

        if (dashed)
        {
            style.BorderColor = new Color(NoteBorder, 0.7f);
        }

        return style;
    }

    public static StyleBoxFlat MakeCardPanel(Color bgColor, Color borderColor,
        float borderWidth = 1.5f, float borderRadius = 3f)
    {
        return new StyleBoxFlat
        {
            BgColor = bgColor,
            BorderColor = borderColor,
            BorderWidthTop = borderWidth,
            BorderWidthBottom = borderWidth,
            BorderWidthLeft = borderWidth,
            BorderWidthRight = borderWidth,
            CornerRadiusTopLeft = (int)borderRadius,
            CornerRadiusTopRight = (int)borderRadius,
            CornerRadiusBottomLeft = (int)borderRadius,
            CornerRadiusBottomRight = (int)borderRadius,
            ShadowColor = NotePaperShadow,
            ShadowSize = 5,
            ShadowOffset = new Vector2(3, 3),
            AntiAliasing = false
        };
    }

    public static StyleBoxFlat MakeDarkPanel(float alpha = 0.92f)
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(BattlefieldBg, alpha),
            BorderColor = new Color(BattlefieldMid, 0.4f),
            BorderWidthBottom = 1,
            AntiAliasing = false
        };
    }

    public static StyleBoxFlat MakeHpBarBg()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.04f, 0.04f),
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
            AntiAliasing = false
        };
    }

    public static StyleBoxFlat MakeHpBarFill(Color color)
    {
        return new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
            AntiAliasing = false
        };
    }

    public static float RandomRotation(float minDeg = -0.8f, float maxDeg = 0.8f)
    {
        return (float)(new Random().NextDouble() * (maxDeg - minDeg) + minDeg);
    }
}
```

- [x] **Step 2: 编译验证**

Run: `dotnet build`
Expected: 0 errors, 0 warnings

- [x] **Step 3: 提交**

```bash
git add src/ui/HandDrawnTheme.cs
git commit -m "feat(ui): 添加 HandDrawnTheme 手绘血色风格工具类"
```

---

## Task 2: 更新 GameConfig 色彩常量

**Files:**
- 修改: `src/core/GameConfig.cs:218-234`

- [x] **Step 1: 替换 GameConfig 中的 UI 色彩常量**

在 `GameConfig.cs` 的 UI 常量区域，将 HP 颜色替换为血色主题：

找到 (约行 232-234):
```csharp
public static readonly Color HpColorHigh = new("388c38");
public static readonly Color HpColorMed = new("a69926");
public static readonly Color HpColorLow = new("b3261f");
```

替换为:
```csharp
public static readonly Color HpColorHigh = new Color(0.65f, 0.35f, 0.35f);
public static readonly Color HpColorMed = new Color(0.75f, 0.45f, 0.25f);
public static readonly Color HpColorLow = new Color(0.753f, 0.224f, 0.169f);
```

- [x] **Step 2: 编译验证**

Run: `dotnet build`
Expected: 0 errors

- [x] **Step 3: 提交**

```bash
git add src/core/GameConfig.cs
git commit -m "feat(ui): 更新 GameConfig 血条颜色为血色主题"
```

---

## Task 3: 改造 HUD 面板

**Files:**
- 修改: `src/ui/UIManager.cs:136-352`
- 修改: `src/ui/UIManager.cs:2057-2098` (血条颜色更新)

- [x] **Step 1: 在 UIManager.cs 顶部添加 using**

找到:
```csharp
using static ZombieWars.Core.GameEnums;
```

在之后添加:
```csharp
using static ZombieWars.UI.HandDrawnTheme;
```

- [x] **Step 2: 改造 `_CreateHudPanel()` 中的 HUD 面板背景**

找到 (约行 138-150):
```csharp
var hudStyle = new StyleBoxFlat
{
    BgColor = new Color(0.04f, 0.04f, 0.06f, 0.90f),
    BorderColor = new Color(0.25f, 0.18f, 0.12f, 0.4f),
    BorderWidthBottom = 1,
    AntiAliasing = false
};
```

替换为:
```csharp
var hudStyle = MakeDarkPanel(0.90f);
hudStyle.BorderColor = new Color(0.239f, 0.102f, 0.102f, 0.4f);
hudStyle.BorderWidthBottom = 1;
```

- [x] **Step 3: 改造血条样式**

找到 `_healthBar` 的样式设置 (约行 176-205 中的 StyleBoxFlat 创建):

将 `new Color(0.12f, 0.03f, 0.03f)` (背景) 和 `new Color(0.65f, 0.18f, 0.14f)` (填充) 替换为:

```csharp
_healthBar.AddThemeStyleboxOverride("background", MakeHpBarBg());
_healthBar.AddThemeStyleboxOverride("fill", MakeHpBarFill(HpHigh));
```

- [x] **Step 4: 改造 HP 标签颜色**

找到 `_healthLabel` 的颜色设置，替换为:
```csharp
_healthLabel.AddThemeColorOverride("font_color", NoteBg);
_healthLabel.AddThemeColorOverride("font_shadow_color", InkBlack);
```

- [x] **Step 5: 改造回合标签颜色**

找到 `_roundLabel` 的颜色 (金色)，替换为:
```csharp
_roundLabel.AddThemeColorOverride("font_color", BloodBright);
```

- [x] **Step 6: 改造分数标签颜色**

找到 `_scoreLabel` 的颜色 (亮金)，替换为:
```csharp
_scoreLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.55f, 0.45f));
```

- [x] **Step 7: 改造僵尸计数标签颜色**

找到 `_zombiesLabel` 的颜色，替换为:
```csharp
_zombiesLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.6f, 0.6f));
```

- [x] **Step 8: 改造 Boss 血条样式**

找到 Boss 血条的背景和填充样式，替换为:
```csharp
_bossHealthBar.AddThemeStyleboxOverride("background", MakeHpBarBg());
_bossHealthBar.AddThemeStyleboxOverride("fill", MakeHpBarFill(BloodBright));
```

Boss 血条背景色: `new Color(BattlefieldBg, 0.9f)`
Boss 血条边框: `NoteBorder`

- [x] **Step 9: 改造 `_MakeStatLabel()` 属性颜色**

将所有属性标签的字体颜色替换为手绘风格色:
- 攻击: `new Color(0.8f, 0.5f, 0.4f)`
- 暴击: `new Color(0.75f, 0.45f, 0.55f)`
- 吸血: `new Color(0.7f, 0.35f, 0.35f)`
- 速度: `new Color(0.65f, 0.55f, 0.55f)`
- 间隔: `new Color(0.75f, 0.65f, 0.45f)`
- 类型: `new Color(0.6f, 0.55f, 0.6f)`
- 优先: `new Color(0.55f, 0.55f, 0.5f)`
- 闪避: `new Color(0.55f, 0.6f, 0.55f)`

- [x] **Step 10: 改造 `UpdateHealthDisplay()` 血条颜色**

找到 (约行 2057-2098):
```csharp
_healthBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = ... });
```

替换颜色逻辑:
```csharp
var hpColor = ratio > HealthHighThreshold ? HpHigh
            : ratio > HealthMedThreshold ? HpMed
            : HpLow;
_healthBar.AddThemeStyleboxOverride("fill", MakeHpBarFill(hpColor));
```

同时将低血量闪烁色改为 `BloodBright`。

- [x] **Step 11: 编译验证**

Run: `dotnet build`
Expected: 0 errors

- [x] **Step 12: 提交**

```bash
git add src/ui/UIManager.cs
git commit -m "feat(ui): HUD 面板改造为手绘血色便签风格"
```

---

## Task 4: 改造武器选择面板

**Files:**
- 修改: `src/ui/UIManager.cs:364-579` (面板创建)
- 修改: `src/ui/UIManager.cs:1096-1285` (显示逻辑 + 稀有度颜色)
- 修改: `src/ui/UIManager.cs:1477-1527` (强化卡渲染)

- [x] **Step 1: 改造武器选择面板背景**

找到 `_weaponSelectionPanel` 的背景样式 (约行 366-378):
```csharp
new StyleBoxFlat { BgColor = new Color(0.02f, 0.02f, 0.08f, 0.92f) }
```

替换为:
```csharp
var panelStyle = MakeDarkPanel(0.92f);
panelStyle.BgColor = new Color(BattlefieldBg, 0.95f);
```

- [x] **Step 2: 改造标题标签**

找到标题 "⚔️ 选择本回合强化 ⚔️" 的颜色，替换为:
```csharp
titleLabel.AddThemeColorOverride("font_color", BloodBright);
```

- [x] **Step 3: 改造武器卡牌底板**

找到 3 张武器卡牌 (Button) 的创建逻辑 (约行 428-556)。

将卡牌的默认样式从:
```csharp
new StyleBoxFlat { BgColor = new Color(0.15f, 0.15f, 0.2f, 0.95f), BorderColor = ... }
```

替换为使用 `MakeCardPanel()`:
```csharp
button.AddThemeStyleboxOverride("normal", MakeCardPanel(NoteBg, NoteBorder));
button.AddThemeStyleboxOverride("hover", MakeCardPanel(NoteBgHover, BloodBright));
button.AddThemeStyleboxOverride("pressed", MakeCardPanel(NoteMid, BloodRed));
```

- [x] **Step 4: 改造卡牌内文字颜色**

在卡牌内部：
- Category 标签: `PencilGray`
- Title: `NoteText`
- Type: `NoteTextDim`
- Desc: `NoteTextDim`
- Hint: `PencilGray`

- [x] **Step 5: 改造稀有度颜色映射**

找到 `ShowWeaponSelection()` 中的稀有度样式 (约行 1214-1246):

| 稀有度 | 新底色 | 新边框色 | 新标题色 |
|--------|--------|---------|---------|
| 1 普通 | `RarityCommonBg` | `RarityCommon` | `NoteText` |
| 2 稀有 | `RarityRareBg` | `RarityRare` | `BloodBright` |
| 3 史诗 | `RarityEpicBg` | `RarityEpic` | `RarityEpic` |
| 4 传说 | 保持现有金色 | 保持现有金色 | 保持现有金色 |

- [x] **Step 6: 改造卡牌悬停样式**

找到悬停样式 (约行 1263-1276)，将颜色调整为:
```csharp
var hoverBg = new Color(baseBg.R + 0.08f, baseBg.G + 0.08f, baseBg.B + 0.08f, baseBg.A);
var hoverBorder = new Color(border.R + 0.1f, border.G + 0.1f, border.B + 0.1f, border.A);
```

- [x] **Step 7: 改造 Skip 按钮**

找到 `_skipButton` 的样式，改为手写便签风格:
```csharp
_skipButton.AddThemeStyleboxOverride("normal", MakeNotePanel(borderRadius: 3f, shadowSize: 3f));
_skipButton.AddThemeStyleboxOverride("hover", MakeNotePanel(borderRadius: 3f, shadowSize: 3f));
```

文字颜色: `NoteText`

- [x] **Step 8: 改造强化卡渲染**

找到 `_RenderUpgradeCard()` (约行 1477-1527):

将绿色主题改为血色主题:
```csharp
var bgStyle = MakeCardPanel(UpgradeBg, UpgradeBorder);
titleColor = UpgradeTitle;
```

- [x] **Step 9: 编译验证**

Run: `dotnet build`
Expected: 0 errors

- [x] **Step 10: 提交**

```bash
git add src/ui/UIManager.cs
git commit -m "feat(ui): 武器选择面板改造为手绘便签卡牌风格"
```

---

## Task 5: 改造 Tooltip 和暂停面板

**Files:**
- 修改: `src/ui/UIManager.cs:581-666`

- [x] **Step 1: 改造 Tooltip 面板**

找到 `_tooltipPanel` 的样式 (约行 583-604):

将深蓝底 + 蓝边框:
```csharp
BgColor = new Color(0.05f, 0.05f, 0.12f, 0.96f),
BorderColor = new Color(0.35f, 0.55f, 0.95f, 0.9f),
```

替换为血色便签:
```csharp
var tooltipStyle = MakeNotePanel(shadowSize: 6f);
tooltipStyle.BgColor = new Color(NoteBg, 0.98f);
tooltipStyle.ShadowSize = 8;
tooltipStyle.ShadowOffset = new Vector2(3, 3);
```

- [x] **Step 2: 改造 Tooltip 文字颜色**

将 `_tooltipLabel` 的默认颜色改为 `NoteText`。

- [x] **Step 3: 改造暂停面板**

找到 `_pausePanel` 的样式 (约行 642-652):

```csharp
BgColor = new Color(0, 0, 0, 0.6f)
```

替换为:
```csharp
BgColor = new Color(BattlefieldBg, 0.7f)
```

将 `_pauseLabel` 文字和样式改为便签风格：
- 文字: `"⏸ 暂停中\n按 ESC 继续"`
- 颜色: `NoteBg` (浅色文字在暗背景上)
- 添加便签底板: `MakeNotePanel()` 居中显示

- [x] **Step 4: 编译验证**

Run: `dotnet build`
Expected: 0 errors

- [x] **Step 5: 提交**

```bash
git add src/ui/UIManager.cs
git commit -m "feat(ui): Tooltip 和暂停面板改造为手绘便签风格"
```

---

## Task 6: 改造回合横幅、控制面板、欢迎面板

**Files:**
- 修改: `src/ui/UIManager.cs:668-875`

- [x] **Step 1: 改造回合横幅 `_roundBanner`**

找到横幅标签 (约行 670-691):

颜色从金色改为 `BloodBright`，描边改为 `InkBlack`:
```csharp
_roundBanner.AddThemeColorOverride("font_color", BloodBright);
_roundBanner.AddThemeColorOverride("font_shadow_color", InkBlack);
```

- [x] **Step 2: 改造控制面板 `_controlsPanel`**

找到控制面板背景 (约行 696-718):

将深蓝底 + 蓝边框改为便签风格:
```csharp
_controlsPanel.AddThemeStyleboxOverride("panel", MakeNotePanel(borderRadius: 5f, shadowSize: 6f));
```

标题 "🎮 操作指南" 颜色改为 `NoteText`。

- [x] **Step 3: 改造信息卡片 `_CreateInfoCard()`**

找到 (约行 760-800):

将暗紫背景改为:
```csharp
var cardStyle = new StyleBoxFlat
{
    BgColor = new Color(NoteBg, 0.85f),
    BorderColor = NoteBorder,
    BorderWidthLeft = 2,
    CornerRadiusTopLeft = 3,
    CornerRadiusTopRight = 3,
    CornerRadiusBottomLeft = 3,
    CornerRadiusBottomRight = 3,
    AntiAliasing = false
};
```

文字颜色改为 `NoteText`。

- [x] **Step 4: 改造欢迎面板 `_welcomePanel`**

找到欢迎面板 (约行 802-875):

背景: 保持暗色，加入红色调 `new Color(BattlefieldBg, 0.85f)`

标题 "🧟 僵尸大战 🧟" 颜色: `BloodBright`

副标题和介绍文字颜色: `new Color(0.75f, 0.6f, 0.6f)`

"开始游戏" 按钮样式:
```csharp
var startStyle = MakeNotePanel(borderRadius: 4f, shadowSize: 4f);
startStyle.BgColor = new Color(NoteBg, 0.95f);
// hover 略亮
var startHover = MakeNotePanel(borderRadius: 4f, shadowSize: 4f);
startHover.BgColor = NoteBgHover;
```

文字颜色: `NoteText`

- [x] **Step 5: 编译验证**

Run: `dotnet build`
Expected: 0 errors

- [x] **Step 6: 提交**

```bash
git add src/ui/UIManager.cs
git commit -m "feat(ui): 横幅、控制面板、欢迎面板改造为手绘便签风格"
```

---

## Task 7: 改造回合总结面板

**Files:**
- 修改: `src/ui/UIManager.cs:1879-2001`

- [x] **Step 1: 改造回合总结背景**

找到背景 Panel (约行 1885-1895):

替换为血色半透明: `new Color(BattlefieldBg, 0.6f)`

- [x] **Step 2: 改造总结卡片**

找到卡片 Panel (约行 1904-1924):

```csharp
var cardStyle = MakeNotePanel(borderRadius: 8f, shadowSize: 8f);
cardStyle.ShadowOffset = new Vector2(4, 4);
```

移除金色边框，改用 `NoteBorder`。

- [x] **Step 3: 改造总结文字颜色**

标题 "第 X 回合完成": `BloodBright`
击杀数(橙): `new Color(0.8f, 0.5f, 0.4f)`
累计击杀(红): `BloodRed`
得分(金): `new Color(0.85f, 0.6f, 0.45f)`

- [x] **Step 4: 编译验证**

Run: `dotnet build`
Expected: 0 errors

- [x] **Step 5: 提交**

```bash
git add src/ui/UIManager.cs
git commit -m "feat(ui): 回合总结面板改造为手绘便签风格"
```

---

## Task 8: 改造 Game Over 界面

**Files:**
- 修改: `src/ui/GameOverUI.cs:20-198`

- [x] **Step 1: 在 GameOverUI.cs 顶部添加 using**

```csharp
using static ZombieWars.UI.HandDrawnTheme;
```

- [x] **Step 2: 改造背景**

找到黑色背景 ColorRect (约行 26-32):
```csharp
Color = new Color(0, 0, 0, 0.75f)
```

替换为:
```csharp
Color = new Color(BattlefieldBg, 0.85f)
```

红色色调 ColorRect (约行 34-40):
```csharp
Color = new Color(0.3f, 0.02f, 0.02f, 0.25f)
```

替换为:
```csharp
Color = new Color(0.3f, 0.05f, 0.02f, 0.3f)
```

- [x] **Step 3: 改造内容面板**

找到内容面板 Panel (约行 47-69):

```csharp
var panelStyle = MakeNotePanel(borderRadius: 6f, shadowSize: 10f);
panelStyle.BgColor = new Color(NoteBg, 0.96f);
panelStyle.BorderColor = NoteBorder;
panelStyle.BorderWidthTop = 2;
panelStyle.BorderWidthBottom = 2;
panelStyle.BorderWidthLeft = 2;
panelStyle.BorderWidthRight = 2;
panelStyle.ShadowOffset = new Vector2(4, 4);
```

- [x] **Step 4: 改造 "YOU DIED" 标题**

颜色从深红改为 `BloodRed`，描边 `InkBlack`。

- [x] **Step 5: 改造统计行颜色**

标签颜色: `NoteTextDim`
值颜色: 使用血色变体
- 存活波数: `BloodBright`
- 消灭敌人: `NoteText`
- 最终得分: `new Color(0.85f, 0.6f, 0.45f)`
- 新纪录: `RarityEpic` (金色)

- [x] **Step 6: 改造成就徽章**

背景: `new Color(NoteBg, 0.8f)`
边框: `NoteBorder`
文字: `NoteText`

- [x] **Step 7: 改造按钮**

"重新开始" 按钮:
```csharp
var restartStyle = MakeNotePanel(borderRadius: 4f, shadowSize: 3f);
restartStyle.BgColor = new Color(0.25f, 0.12f, 0.12f, 0.9f);
var restartHover = MakeNotePanel(borderRadius: 4f, shadowSize: 3f);
restartHover.BgColor = new Color(0.35f, 0.18f, 0.18f, 0.9f);
```

"退出游戏" 按钮: 类似但更暗。

文字颜色: `NoteBg`

- [x] **Step 8: 编译验证**

Run: `dotnet build`
Expected: 0 errors

- [x] **Step 9: 提交**

```bash
git add src/ui/GameOverUI.cs
git commit -m "feat(ui): Game Over 界面改造为血色信纸风格"
```

---

## Task 9: 改造 ScreenEffects 特效颜色

**Files:**
- 修改: `src/ui/ScreenEffects.cs`

- [x] **Step 1: 在 ScreenEffects.cs 顶部添加 using**

```csharp
using static ZombieWars.UI.HandDrawnTheme;
```

- [x] **Step 2: 改造受伤闪烁颜色**

找到 `FlashDamage()` (约行 111-117):

```csharp
var color = new Color(0.8f, 0.1f, 0.1f, 0.25f);
```

替换为:
```csharp
var color = new Color(BloodBright, 0.2f);
```

- [x] **Step 3: 改造攻击闪光颜色**

找到 `FlashAttackColor()` — 确保传入的颜色与血色主题一致。

- [x] **Step 4: 改造低血量暗角**

找到 `_CreateLowHpVignette()` (约行 121-144):

确保暗角颜色为血红色调。

- [x] **Step 5: 改造连击显示颜色**

找到 `_UpdateComboDisplay()` (约行 302-354):

将 4 级连击颜色替换为血色渐变:
```csharp
var comboColors = new[]
{
    NoteBg,                                              // 白
    new Color(0.95f, 0.75f, 0.65f),                     // 浅红
    new Color(0.9f, 0.5f, 0.4f),                        // 中红
    BloodBright                                          // 亮红
};
```

- [x] **Step 6: 改造伤害数字颜色**

找到 `ShowDamageNumber()` (约行 185-288):

将各级伤害颜色改为血色:
- 普通: `NoteText`
- 中等: `new Color(0.8f, 0.45f, 0.35f)`
- 高: `BloodRed`
- 暴击: `BloodBright`

- [x] **Step 7: 改造 Boss 氛围效果**

找到 `TriggerBossAtmosphere()` (约行 401-415):

将红色叠加色从 `new Color(0.5f, 0.05f, 0.05f, 0.2f)` 调整为 `new Color(BloodRed, 0.25f)`。

- [x] **Step 8: 编译验证**

Run: `dotnet build`
Expected: 0 errors

- [x] **Step 9: 提交**

```bash
git add src/ui/ScreenEffects.cs
git commit -m "feat(ui): 屏幕特效颜色改造为血色主题"
```

---

## Task 10: 改造 WeaponFormatter 稀有度颜色

**Files:**
- 修改: `src/ui/WeaponFormatter.cs:26-48`

- [x] **Step 1: 改造 `GetRarityColor()` 和 `GetRarityColorValue()`**

找到 (约行 26-48):

将 BBCode 颜色和 Color 值替换为血色主题:
```csharp
public static string GetRarityColor(int rarity) => rarity switch
{
    2 => "#c0392b",
    3 => "#f39c12",
    4 => "#ffcc40",
    _ => "#9a8a8a"
};

public static Color GetRarityColorValue(int rarity) => rarity switch
{
    2 => HandDrawnTheme.RarityRare,
    3 => HandDrawnTheme.RarityEpic,
    4 => new Color(1f, 0.8f, 0.25f),
    _ => new Color(0.6f, 0.54f, 0.54f)
};
```

- [x] **Step 2: 编译验证**

Run: `dotnet build`
Expected: 0 errors

- [x] **Step 3: 提交**

```bash
git add src/ui/WeaponFormatter.cs
git commit -m "feat(ui): 武器稀有度颜色改造为血色主题"
```

---

## Task 11: 最终验证与清理

- [x] **Step 1: 全量编译**

Run: `dotnet build`
Expected: 0 errors, 0 warnings

- [x] **Step 2: Godot 脚本检查**

Run: `/Applications/Godot.app/Contents/MacOS/Godot --headless --check-only`
Expected: 无错误

- [x] **Step 3: 检查无残留调试代码**

Run: `grep -n "GD.Print\|LogUtils.Print" src/ui/*.cs`
Expected: 无输出（UI 文件中不应有调试 print）

- [x] **Step 4: 推送到远程**

```bash
git push origin main
```
