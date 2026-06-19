namespace ZombieWars.UI;

using System;
using System.Collections.Generic;
using Godot;
using ZombieWars.Core;
using ZombieWars.Weapons;

using Player = ZombieWars.Player.Player;
using static ZombieWars.Core.GameConfig;
using static ZombieWars.Core.GameEnums;

/// <summary>
/// 顶部 HUD 面板：回合/僵尸/天气/分数 + 底部生命、武器、属性、效果图标、音效/帮助按钮。
///
/// 注：Boss 血条不在本组件内（它需要挂在 UIManager 顶层而非 HUD 容器内，由 UIManager 单独管理）。
///
/// 状态由调用方通过 Update* 方法写入；按钮回调与 tooltip 桥接通过 Setup 注入。
/// </summary>
public partial class HudPanel : Panel
{
    // ── Setup-time 注入的回调 ────────────────────────────────
    private Action _onToggleSound = null!;
    private Action _onToggleHelp = null!;
    private Action _onToggleEquipment = null!;
    private Action<string> _showTooltip = null!;
    private Action _hideTooltip = null!;

    // ── Row 1: HP / Round / Score ────────────────────────────
    private ProgressBar _healthBar = null!;
    private Label _healthLabel = null!;
    private Label _roundLabel = null!;
    private Label _zombiesLabel = null!;
    private Label _scoreLabel = null!;
    private RichTextLabel _weatherLabel = null!;

    // ── Row 2: 属性行 ───────────────────────────────────────
    private Label _damageLabel = null!;
    private Label _attackIntervalLabel = null!;
    private Label _critLabel = null!;
    private Label _lifestealLabel = null!;
    private Label _attackTypeLabel = null!;
    private Label _targetPriorityLabel = null!;
    private Label _dashCooldownLabel = null!;
    private Label _speedLabel = null!;

    // ── 效果图标 ─────────────────────────────────────────────
    private HBoxContainer _effectsContainer = null!;
    private readonly Dictionary<string, Button> _effectIcons = new();
    private Button _soundToggleButton = null!;
    private Label _debuffLabel = null!;  // 常驻 debuff 提示：中毒/燃烧等
    private Label _hazardLabel = null!;  // 陷阱（毒沼/燃烧地带）持续伤害提示
    private Label _weaponNameLabel = null!;

    // ── 低血量闪烁 tween ────────────────────────────────────
    private Tween? _lowHealthTween;

    /// <summary>
    /// 注入按钮回调与 tooltip 桥接。必须在 AddChild 之前调用一次。
    /// </summary>
    public void Setup(Action onToggleSound, Action onToggleHelp, Action onToggleEquipment, Action<string> showTooltip, Action hideTooltip)
    {
        _onToggleSound = onToggleSound;
        _onToggleHelp = onToggleHelp;
        _onToggleEquipment = onToggleEquipment;
        _showTooltip = showTooltip;
        _hideTooltip = hideTooltip;
    }

    public override void _Ready()
    {
        // ── 全屏透明容器，承载顶栏 + 底栏 ──────────────────
        // ZIndex 100 让 HUD 永远在所有 UI 之上（包括武器选择面板 FullRect）。
        // 本容器必须透明（无 panel 背景），否则会用深色蒙住整个游戏画面。
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 100;
        // 空背景覆盖 Panel 默认 stylebox，让本容器不渲染任何背景色
        AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

        // ═══ 顶栏：波次 / 剩余 / 天气 / Debuff / 分数 ═══
        var topPanel = new Panel();
        topPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        topPanel.OffsetBottom = 34;
        topPanel.CustomMinimumSize = new Vector2(0, 34);
        topPanel.MouseFilter = MouseFilterEnum.Ignore;
        var topStyle = HandDrawnTheme.MakeDarkPanel(0.90f);
        topStyle.BorderColor = new Color(0.239f, 0.102f, 0.102f, 0.4f);
        topStyle.BorderWidthBottom = 1;
        topPanel.AddThemeStyleboxOverride("panel", topStyle);
        AddChild(topPanel);

        var topMargin = new MarginContainer();
        topMargin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        topMargin.AddThemeConstantOverride("margin_left", UiMarginSmall);
        topMargin.AddThemeConstantOverride("margin_right", UiMarginSmall);
        topMargin.AddThemeConstantOverride("margin_top", UiMarginTiny);
        topMargin.AddThemeConstantOverride("margin_bottom", UiMarginTiny);
        topPanel.AddChild(topMargin);

        var topRow = new HBoxContainer();
        topRow.AddThemeConstantOverride("separation", UiSpacingMedium);
        topRow.Alignment = BoxContainer.AlignmentMode.Center;
        topMargin.AddChild(topRow);

        _roundLabel = new Label { Text = "回合 1" };
        _roundLabel.AddThemeFontSizeOverride("font_size", UiFontBody);
        _roundLabel.AddThemeColorOverride("font_color", HandDrawnTheme.Colors.BloodBright);
        _roundLabel.AddThemeConstantOverride("outline_size", UiOutlineNorm);
        _roundLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.6f));
        topRow.AddChild(_roundLabel);

        _zombiesLabel = new Label { Text = "僵尸 0/0" };
        _zombiesLabel.AddThemeFontSizeOverride("font_size", UiFontSmall);
        _zombiesLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.6f, 0.6f));
        topRow.AddChild(_zombiesLabel);

        // 常驻天气显示：酸雨/暴风时变红警示（Label + BBCode 着色）
        _weatherLabel = new RichTextLabel
        {
            Text = "天气 晴",
            BbcodeEnabled = true,
            ScrollActive = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(0, 0),
        };
        _weatherLabel.AddThemeFontSizeOverride("normal_font_size", UiFontSmall);
        _weatherLabel.AddThemeColorOverride("default_color", new Color(0.8f, 0.8f, 0.5f));
        topRow.AddChild(_weatherLabel);

        // 常驻 debuff 提示：中毒/燃烧等持续掉血状态，避免"莫名其妙掉血"
        _debuffLabel = new Label { Text = "" };
        _debuffLabel.AddThemeFontSizeOverride("font_size", UiFontSmall);
        _debuffLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.3f, 0.9f));  // 紫红色提示
        _debuffLabel.AddThemeConstantOverride("outline_size", UiOutlineNorm);
        _debuffLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.6f));
        topRow.AddChild(_debuffLabel);

        // 陷阱（玩家踩入毒沼/燃烧地带）持续伤害提示
        _hazardLabel = new Label { Text = "" };
        _hazardLabel.AddThemeFontSizeOverride("font_size", UiFontSmall);
        _hazardLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.5f, 0.2f));  // 橙红警示
        _hazardLabel.AddThemeConstantOverride("outline_size", UiOutlineNorm);
        _hazardLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.6f));
        topRow.AddChild(_hazardLabel);

        // 分数靠右
        _scoreLabel = new Label
        {
            Text = "分数: 0",
            HorizontalAlignment = HorizontalAlignment.Right,
            CustomMinimumSize = new Vector2(110, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _scoreLabel.AddThemeFontSizeOverride("font_size", UiFontBody);
        _scoreLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.55f, 0.45f));
        _scoreLabel.AddThemeConstantOverride("outline_size", UiOutlineNorm);
        _scoreLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.6f));
        topRow.AddChild(_scoreLabel);

        // ═══ 底栏：装备图标 / HP条 / 状态 / 按钮 ═══
        var bottomPanel = new Panel();
        bottomPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        bottomPanel.OffsetTop = -82;
        bottomPanel.CustomMinimumSize = new Vector2(0, 82);
        bottomPanel.MouseFilter = MouseFilterEnum.Ignore;
        var bottomStyle = HandDrawnTheme.MakeDarkPanel(0.90f);
        bottomStyle.BorderColor = new Color(0.239f, 0.102f, 0.102f, 0.4f);
        bottomStyle.BorderWidthTop = 1;
        bottomPanel.AddThemeStyleboxOverride("panel", bottomStyle);
        AddChild(bottomPanel);

        var bottomMargin = new MarginContainer();
        bottomMargin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        bottomMargin.AddThemeConstantOverride("margin_left", UiMarginSmall);
        bottomMargin.AddThemeConstantOverride("margin_right", UiMarginSmall);
        bottomMargin.AddThemeConstantOverride("margin_top", UiMarginTiny);
        bottomMargin.AddThemeConstantOverride("margin_bottom", UiMarginTiny);
        bottomPanel.AddChild(bottomMargin);

        var bottomStack = new VBoxContainer();
        bottomStack.AddThemeConstantOverride("separation", UiSpacingTight);
        bottomMargin.AddChild(bottomStack);

        var bottomRow = new HBoxContainer();
        bottomRow.AddThemeConstantOverride("separation", UiSpacingMedium);
        bottomStack.AddChild(bottomRow);

        // 左侧：装备效果图标
        var effectsLabel = _MakeStatLabel("效果", new Color(0.75f, 0.66f, 0.45f), UiFontSmall);
        effectsLabel.TooltipText = "当前武器、装备和临时强化。把鼠标放到图标上可查看具体效果。";
        bottomRow.AddChild(effectsLabel);

        _effectsContainer = new HBoxContainer();
        _effectsContainer.AddThemeConstantOverride("separation", UiSpacingHairline);
        _effectsContainer.CustomMinimumSize = new Vector2(190, 0);
        _effectsContainer.Alignment = BoxContainer.AlignmentMode.Begin;
        bottomRow.AddChild(_effectsContainer);

        // 弹性间隔：把 HP 条推到中间
        var leftSpacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        bottomRow.AddChild(leftSpacer);

        // 中间：HP 条（加大加粗，战斗时一眼看到血量）
        var hpWrapper = new HBoxContainer();
        hpWrapper.AddThemeConstantOverride("separation", UiSpacingTight);
        hpWrapper.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        bottomRow.AddChild(hpWrapper);

        _healthBar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(220, 20),
            ShowPercentage = false
        };
        _healthBar.TooltipText = "生命值。归零会死亡；低血量时血条会闪烁。";
        _healthBar.AddThemeStyleboxOverride("background", HandDrawnTheme.MakeHpBarBg());
        _healthBar.AddThemeStyleboxOverride("fill", HandDrawnTheme.MakeHpBarFill(HandDrawnTheme.Colors.HpHigh));
        hpWrapper.AddChild(_healthBar);

        _healthLabel = new Label { Text = "生命 120/120" };
        _healthLabel.AddThemeFontSizeOverride("font_size", UiFontSmall);
        _healthLabel.AddThemeColorOverride("font_color", new Color(0.94f, 0.91f, 0.91f));
        _healthLabel.AddThemeConstantOverride("outline_size", UiOutlineNorm);
        _healthLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        _healthLabel.VerticalAlignment = VerticalAlignment.Center;
        hpWrapper.AddChild(_healthLabel);

        // 弹性间隔：把按钮推到右边
        var rightSpacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        bottomRow.AddChild(rightSpacer);

        // 闪避冷却（战术信息，保留可见）
        _dashCooldownLabel = _MakeStatLabel("闪避: 就绪", new Color(0.55f, 0.6f, 0.55f), UiFontSmall);
        bottomRow.AddChild(_dashCooldownLabel);

        // 右侧：按钮组（固定大小，不拉伸）
        _soundToggleButton = new Button
        {
            Text = "🔊",
            CustomMinimumSize = new Vector2(28, 24),
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "点击切换音效"
        };
        _soundToggleButton.Pressed += _onToggleSound;
        bottomRow.AddChild(_soundToggleButton);

        // 装备面板按钮（C 键也可打开）
        var equipBtn = new Button
        {
            Text = "装备",
            CustomMinimumSize = new Vector2(48, 24),
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "查看角色装备 (C)"
        };
        equipBtn.AddThemeFontSizeOverride("font_size", UiFontMicro);
        equipBtn.Pressed += _onToggleEquipment;
        bottomRow.AddChild(equipBtn);

        var helpBtn = new Button
        {
            Text = "?",
            CustomMinimumSize = new Vector2(28, 24),
            FocusMode = Control.FocusModeEnum.None
        };
        helpBtn.Pressed += _onToggleHelp;
        bottomRow.AddChild(helpBtn);

        // 第二行：把原本藏起来的战斗关键数值直接展示，避免玩家不知道当前 build 在做什么。
        var statRow = new HBoxContainer();
        statRow.AddThemeConstantOverride("separation", UiSpacingMedium);
        statRow.Alignment = BoxContainer.AlignmentMode.Center;
        bottomStack.AddChild(statRow);

        _weaponNameLabel = _MakeStatLabel("武器: 基础近战", new Color(0.86f, 0.78f, 0.55f), UiFontSmall);
        _attackTypeLabel = _MakeStatLabel("类型: 近战", new Color(0.72f, 0.78f, 0.92f), UiFontSmall);
        _targetPriorityLabel = _MakeStatLabel("目标: 最近", new Color(0.65f, 0.82f, 0.88f), UiFontSmall);
        _damageLabel = _MakeStatLabel("伤害 12/次", new Color(0.92f, 0.70f, 0.52f), UiFontSmall);
        _attackIntervalLabel = _MakeStatLabel("间隔 0.45s", new Color(0.78f, 0.72f, 0.92f), UiFontSmall);
        _critLabel = _MakeStatLabel("暴击 0%", new Color(0.95f, 0.70f, 0.35f), UiFontSmall);
        _lifestealLabel = _MakeStatLabel("吸血 0%", new Color(0.85f, 0.45f, 0.45f), UiFontSmall);
        _speedLabel = _MakeStatLabel("移速 220", new Color(0.62f, 0.82f, 0.58f), UiFontSmall);

        statRow.AddChild(_weaponNameLabel);
        statRow.AddChild(_attackTypeLabel);
        statRow.AddChild(_targetPriorityLabel);
        statRow.AddChild(_damageLabel);
        statRow.AddChild(_attackIntervalLabel);
        statRow.AddChild(_critLabel);
        statRow.AddChild(_lifestealLabel);
        statRow.AddChild(_speedLabel);

        var hudHint = _MakeStatLabel("H 帮助 · C 装备 · Tab 目标", new Color(0.52f, 0.50f, 0.46f), UiFontMicro);
        hudHint.TooltipText = "打开帮助可查看 HUD 每个项目的含义；打开装备可查看每件装备的详细效果。";
        statRow.AddChild(hudHint);
    }

    // ═══ Public API ═══

    public void UpdateHealth(int currentHealth, int maxHealth)
    {
        _healthBar.MaxValue = maxHealth;
        _healthBar.Value = currentHealth;
        float healthPercent = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;

        var hpColor = healthPercent > GameConfig.HealthHighThreshold ? HandDrawnTheme.Colors.HpHigh
            : healthPercent > GameConfig.HealthMedThreshold ? HandDrawnTheme.Colors.HpMed
            : HandDrawnTheme.Colors.HpLow;
        _healthBar.AddThemeStyleboxOverride("fill", HandDrawnTheme.MakeHpBarFill(hpColor));

        _healthLabel.Text = $"生命 {currentHealth}/{maxHealth}";

        if (healthPercent <= 0.3f)
        {
            if (_lowHealthTween == null || !_lowHealthTween.IsRunning())
            {
                _lowHealthTween = CreateTween();
                _lowHealthTween.SetLoops(999999);
                _lowHealthTween.TweenProperty(_healthBar, Control.PropertyName.Modulate.ToString() + ":a", 0.6f, 0.35f);
                _lowHealthTween.TweenProperty(_healthBar, Control.PropertyName.Modulate.ToString() + ":a", 1.0f, 0.35f);
            }
        }
        else
        {
            if (_lowHealthTween != null && _lowHealthTween.IsRunning())
                _lowHealthTween.Kill();
            _healthBar.Modulate = _healthBar.Modulate with { A = 1.0f };
        }
    }

    public void UpdateRound(int roundNumber)
    {
        _roundLabel.Text = $"回合: {roundNumber}";
        _roundLabel.TooltipText = $"当前回合: {roundNumber}\n每回合开始前会抽取强化卡。";
    }

    public void UpdateZombies(int alive, int total)
    {
        _zombiesLabel.Text = $"僵尸: {alive}/{total}";
        _zombiesLabel.TooltipText = $"当前存活: {alive}\n本回合总量: {total}";
    }

    /// <summary>
    /// 更新常驻天气显示。WeatherSystem 在 _OnWeatherTick 末尾每 0.5s 调用一次，
    /// 保证玩家随时能看到当前天气（特别是酸雨持续掉血时不会再"莫名其妙"）。
    /// effectsDesc 已由调用方用 BBCode 着色（正面绿、负面红）。
    /// </summary>
    public void UpdateWeather(string name, Color color, bool isHazard, int damagePerTick, string effectsDesc)
    {
        string icon = isHazard ? "⚠ " : "";
        string dmg = (isHazard && damagePerTick > 0) ? $" [color=#e74c3c]-{damagePerTick}/tick[/color]" : "";
        string effects = !string.IsNullOrEmpty(effectsDesc) ? $" {effectsDesc}" : "";
        string colorHtml = "#" + color.ToHtml(false);
        _weatherLabel.Text = $"[color={colorHtml}]天气: {icon}{name}{dmg}[/color]{effects}";
        _weatherLabel.TooltipText = isHazard
            ? $"当前天气: {name}\n危险天气会持续影响战斗。{(damagePerTick > 0 ? $"\n每次天气结算造成 {damagePerTick} 点伤害。" : "")}\n{effectsDesc}"
            : $"当前天气: {name}\n{effectsDesc}";
    }

    /// <summary>
    /// 更新常驻 debuff 提示：当前活跃的中毒/燃烧等持续掉血状态。
    /// 每次 UpdatePlayerStatus 顺便调用，让玩家知道"为什么会持续掉血"。
    /// </summary>
    public void UpdateDebuffStatus(Player player)
    {
        if (_debuffLabel == null) return;
        var buffSystem = player.BuffSystem;
        if (buffSystem == null)
        {
            _debuffLabel.Text = "";
            return;
        }
        float dps = buffSystem.GetActiveDebuffDamagePerSecond();
        string desc = buffSystem.GetActiveDebuffDescription();
        if (dps > 0 && desc.Length > 0)
        {
            _debuffLabel.Text = $"⚠ {desc} -{dps:F0}/s";
        }
        else
        {
            _debuffLabel.Text = "";
        }
    }

    /// <summary>陷阱持续伤害提示（玩家踩入毒沼/燃烧地带时由 HazardZone 推送，UIManager 定时清除）。</summary>
    public void ShowHazard(string text)
    {
        if (_hazardLabel != null)
            _hazardLabel.Text = text;
    }

    public void UpdateScore(int newScore)
    {
        _scoreLabel.Text = $"分数: {newScore}";
        _scoreLabel.TooltipText = "击杀僵尸、精英和 BOSS 可获得分数。";
    }

    public void UpdateDashCooldown(float remaining)
    {
        if (remaining <= 0.0f)
        {
            _dashCooldownLabel.Text = "闪避: 就绪";
            _dashCooldownLabel.TooltipText = "按 Shift 冲刺闪避，拉开距离或穿过危险区域。";
        }
        else
        {
            string secs = remaining < 2.0f ? remaining.ToString("F1") + "s" : Mathf.FloorToInt(remaining) + "s";
            _dashCooldownLabel.Text = $"闪避: {secs}";
            _dashCooldownLabel.TooltipText = $"闪避冷却中，还需 {secs}。";
        }
    }

    public void UpdateAttackInterval(float interval, float cooldown)
    {
        _attackIntervalLabel.Text = $"间隔 {interval:F2}s";
        string intervalText = $"{interval:F2}s";
        string cooldownText = cooldown > 0 ? $"{cooldown:F2}s" : "就绪";
        _attackIntervalLabel.TooltipText = $"攻击间隔: 每 {intervalText} 可攻击一次\n下次可攻击: {cooldownText}";
    }

    public void UpdatePlayerStatus(Player player)
    {
        string attackTypeText = _GetAttackTypeText(player.CurrentAttackType);
        string weaponName = _GetCurrentWeaponName(player);
        _weaponNameLabel.Text = $"武器: {_Shorten(weaponName, 12)}";
        _attackTypeLabel.Text = $"类型: {attackTypeText}";
        _damageLabel.Text = $"伤害 {Mathf.RoundToInt(player.CurrentDamage)}/次";
        _critLabel.Text = $"暴击 {Mathf.RoundToInt(player.CriticalChance)}%";
        _lifestealLabel.Text = $"吸血 {Mathf.RoundToInt(player.LifeStealPercent)}%";
        _speedLabel.Text = $"移速 {Mathf.RoundToInt(player.CurrentSpeed)}";
        _targetPriorityLabel.Text = player.TargetPriority switch
        {
            0 => "目标: 最近",
            1 => "目标: 最远",
            2 => "目标: 密集",
            _ => "目标: 最近"
        };
        _UpdateStatusTooltips(player);
        UpdateEffects(player);
        UpdateDebuffStatus(player);
    }

    public void UpdateEffects(Player player)
    {
        if (_effectsContainer == null) return;
        foreach (Node child in _effectsContainer.GetChildren())
            child.QueueFree();
        _effectIcons.Clear();

        var activeItems = new List<Godot.Collections.Dictionary>();

        // 当前武器
        foreach (var effect in player.ActiveEffects)
        {
            if (effect.Category == WeaponCategory.Weapon)
            {
                activeItems.Add(new Godot.Collections.Dictionary
                {
                    ["icon"] = effect.Icon,
                    ["name"] = effect.CardName,
                    ["type"] = "weapon",
                    ["color"] = WeaponFormatter.GetRarityColorValue(effect.Rarity),
                    ["tooltip"] = WeaponFormatter.GetEffectTooltip(effect)
                });
                break;
            }
        }

        // 装备
        foreach (var effect in player.ActiveEffects)
        {
            if (effect.Category == WeaponCategory.Equipment)
            {
                activeItems.Add(new Godot.Collections.Dictionary
                {
                    ["icon"] = effect.Icon,
                    ["name"] = effect.CardName,
                    ["type"] = "equipment",
                    ["color"] = WeaponFormatter.GetRarityColorValue(effect.Rarity),
                    ["tooltip"] = WeaponFormatter.GetEffectTooltip(effect)
                });
            }
        }

        if (player.Aura?.Active == true)
        {
            activeItems.Add(new Godot.Collections.Dictionary
            {
                ["icon"] = "❄️",
                ["name"] = "冰霜护盾",
                ["type"] = "buff",
                ["color"] = new Color(0.3f, 0.8f, 1.0f),
                ["tooltip"] = $"【冰霜护盾】\n伤害: {Mathf.RoundToInt(player.Aura.Damage)}\n范围: {Mathf.RoundToInt(player.Aura.Radius)}\n效果: 环绕的冰晶会伤害并减速接触的敌人"
            });
        }
        if (player.CriticalChance > 0)
        {
            activeItems.Add(new Godot.Collections.Dictionary
            {
                ["icon"] = "💥",
                ["name"] = "暴击",
                ["type"] = "stat",
                ["color"] = new Color(1.0f, 0.6f, 0.2f),
                ["tooltip"] = $"【暴击】\n几率: {Mathf.RoundToInt(player.CriticalChance)}%\n伤害: x2.0"
            });
        }
        if (player.LifeStealPercent > 0)
        {
            activeItems.Add(new Godot.Collections.Dictionary
            {
                ["icon"] = "🩸",
                ["name"] = "吸血",
                ["type"] = "stat",
                ["color"] = new Color(0.9f, 0.3f, 0.3f),
                ["tooltip"] = $"【吸血】\n比例: {Mathf.RoundToInt(player.LifeStealPercent)}%\n效果: 造成伤害时回复生命"
            });
        }
        if (player.FreezeDuration > 0)
        {
            activeItems.Add(new Godot.Collections.Dictionary
            {
                ["icon"] = "🧊",
                ["name"] = "冰冻",
                ["type"] = "stat",
                ["color"] = new Color(0.4f, 0.8f, 1.0f),
                ["tooltip"] = $"【冰冻】\n几率: {Mathf.RoundToInt(player.FreezeDuration)}%\n效果: 攻击有几率冻结敌人"
            });
        }
        if (player.PoisonDamage > 0)
        {
            activeItems.Add(new Godot.Collections.Dictionary
            {
                ["icon"] = "☠️",
                ["name"] = "中毒",
                ["type"] = "stat",
                ["color"] = new Color(0.4f, 0.9f, 0.3f),
                ["tooltip"] = $"【中毒】\n几率: {Mathf.RoundToInt(player.PoisonDamage)}%\n效果: 攻击有几率使敌人中毒"
            });
        }
        if (player.ShieldAmount > 0)
        {
            activeItems.Add(new Godot.Collections.Dictionary
            {
                ["icon"] = "🛡️",
                ["name"] = "护盾",
                ["type"] = "buff",
                ["color"] = new Color(0.6f, 0.8f, 1.0f),
                ["tooltip"] = $"【护盾】\n剩余: {player.ShieldAmount}\n效果: 吸收伤害"
            });
        }

        foreach (Godot.Collections.Dictionary item in activeItems)
        {
            var btn = new Button
            {
                Text = (string)item["icon"],
                CustomMinimumSize = new Vector2(28, 24),
                FocusMode = Control.FocusModeEnum.None,
                Flat = true
            };
            btn.AddThemeFontSizeOverride("font_size", UiFontBody);

            var itemColor = (Color)item["color"];
            var (style, hoverStyle, _) = HandDrawnTheme.MakeButtonStyles(
                ColorUtils.Darken(itemColor, 0.6f) with { A = 0.7f },
                ColorUtils.Darken(itemColor, 0.4f),
                itemColor,
                radius: 4,
                shadowSize: 0,
                bottomOnlyBorder: true);
            btn.AddThemeStyleboxOverride("normal", style);
            btn.AddThemeStyleboxOverride("hover", hoverStyle);

            string effectTooltip = (string)item["tooltip"];
            btn.MouseEntered += () => _showTooltip(effectTooltip);
            btn.MouseExited += () => _hideTooltip();

            _effectsContainer.AddChild(btn);
            _effectIcons[(string)item["name"]] = btn;
        }

        if (activeItems.Count == 0)
        {
            var hint = new Label { Text = "暂无强化" };
            hint.AddThemeFontSizeOverride("font_size", UiFontMicro);
            hint.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.6f));
            hint.TooltipText = "选择武器或装备强化后，这里会显示对应图标。";
            _effectsContainer.AddChild(hint);
        }
    }

    // ═══ 私有工具 ═══

    private void _UpdateStatusTooltips(Player player)
    {
        _weaponNameLabel.TooltipText = $"当前武器: {_GetCurrentWeaponName(player)}\n类型: {_GetAttackTypeText(player.CurrentAttackType)}\n选择强化卡可替换武器或增强装备。";
        _damageLabel.TooltipText = $"攻击力: {Mathf.RoundToInt(player.CurrentDamage)}\n基础伤害与倍率已计入";
        _critLabel.TooltipText = $"暴击概率: {Mathf.RoundToInt(player.CriticalChance)}%\n暴击伤害倍率: x{Mathf.Snapped(player.CritMultiplier, 0.1f)}";
        _lifestealLabel.TooltipText = $"吸血比例: {Mathf.RoundToInt(player.LifeStealPercent)}%\n命中后回复对应比例生命";
        _speedLabel.TooltipText = $"移动速度: {Mathf.RoundToInt(player.CurrentSpeed)}\n受装备、升级和减速效果影响";
        string priorityText = player.TargetPriority switch
        {
            1 => "最远",
            2 => "密集",
            _ => "最近"
        };
        _targetPriorityLabel.TooltipText = $"目标优先级: {priorityText}\n按 Tab 切换";
        _attackTypeLabel.TooltipText = WeaponFormatter.BuildAttackEffectsTooltip(player);
    }

    private static Label _MakeStatLabel(string text, Color color, int fontSize = UiFontSmall)
    {
        var lbl = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
        lbl.AddThemeFontSizeOverride("font_size", fontSize);
        lbl.AddThemeColorOverride("font_color", color);
        lbl.AddThemeConstantOverride("outline_size", UiOutlineThin);
        lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.5f));
        return lbl;
    }

    private static string _GetAttackTypeText(AttackType attackType) => attackType switch
    {
        AttackType.Melee => "近战",
        AttackType.Ranged => "远程",
        AttackType.Area => "范围",
        AttackType.Magic => "魔法",
        _ => "近战"
    };

    private static string _GetCurrentWeaponName(Player player)
    {
        foreach (var effect in player.ActiveEffects)
        {
            if (effect.Category == WeaponCategory.Weapon)
                return effect.CardName;
        }

        return player.CurrentAttackType switch
        {
            AttackType.Ranged => "基础远程",
            AttackType.Magic => "基础魔法",
            AttackType.Area => "基础范围",
            _ => "基础近战"
        };
    }

    private static string _Shorten(string text, int maxChars)
    {
        if (text.Length <= maxChars) return text;
        return text[..maxChars] + "…";
    }

    public void SetSoundIcon(bool sfxEnabled)
    {
        if (_soundToggleButton != null)
            _soundToggleButton.Text = sfxEnabled ? "🔊" : "🔇";
    }
}
