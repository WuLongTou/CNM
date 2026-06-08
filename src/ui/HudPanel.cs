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
/// 顶部 HUD 面板：HP 条 + 回合/僵尸/分数 + 属性行（攻击/间隔/暴击/吸血/类型/目标/闪避/速度）
/// + 效果图标 + 音效/帮助按钮。
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
    private Action<string> _showTooltip = null!;
    private Action _hideTooltip = null!;

    // ── Row 1: HP / Round / Score ────────────────────────────
    private ProgressBar _healthBar = null!;
    private Label _healthLabel = null!;
    private Label _roundLabel = null!;
    private Label _zombiesLabel = null!;
    private Label _scoreLabel = null!;

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

    // ── 低血量闪烁 tween ────────────────────────────────────
    private Tween? _lowHealthTween;

    /// <summary>
    /// 注入按钮回调与 tooltip 桥接。必须在 AddChild 之前调用一次。
    /// </summary>
    public void Setup(Action onToggleSound, Action onToggleHelp, Action<string> showTooltip, Action hideTooltip)
    {
        _onToggleSound = onToggleSound;
        _onToggleHelp = onToggleHelp;
        _showTooltip = showTooltip;
        _hideTooltip = hideTooltip;
    }

    public override void _Ready()
    {
        // ── 顶部 HUD 容器 ──────────────────────────────────
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        CustomMinimumSize = new Vector2(0, 48);
        OffsetBottom = 52;

        var hudStyle = HandDrawnTheme.MakeDarkPanel(0.90f);
        hudStyle.BorderColor = new Color(0.239f, 0.102f, 0.102f, 0.4f);
        hudStyle.BorderWidthBottom = 1;
        AddThemeStyleboxOverride("panel", hudStyle);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", UiMarginSmall);
        margin.AddThemeConstantOverride("margin_right", UiMarginSmall);
        margin.AddThemeConstantOverride("margin_top", UiMarginTiny);
        margin.AddThemeConstantOverride("margin_bottom", UiMarginTiny);
        AddChild(margin);

        var mainVbox = new VBoxContainer();
        mainVbox.AddThemeConstantOverride("separation", UiSpacingHairline);
        margin.AddChild(mainVbox);

        // Row 1
        var row1 = new HBoxContainer();
        row1.AddThemeConstantOverride("separation", UiSpacingMedium);
        mainVbox.AddChild(row1);

        var hpWrapper = new HBoxContainer();
        hpWrapper.AddThemeConstantOverride("separation", UiSpacingTight);
        hpWrapper.CustomMinimumSize = new Vector2(240, 0);
        hpWrapper.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        row1.AddChild(hpWrapper);

        _healthBar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(180, 14),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            ShowPercentage = false
        };
        _healthBar.AddThemeStyleboxOverride("background", HandDrawnTheme.MakeHpBarBg());
        _healthBar.AddThemeStyleboxOverride("fill", HandDrawnTheme.MakeHpBarFill(HandDrawnTheme.Colors.HpHigh));
        hpWrapper.AddChild(_healthBar);

        _healthLabel = new Label { Text = "120/120" };
        _healthLabel.AddThemeFontSizeOverride("font_size", UiFontMicro);
        _healthLabel.AddThemeColorOverride("font_color", new Color(0.94f, 0.91f, 0.91f));
        _healthLabel.AddThemeConstantOverride("outline_size", UiOutlineNorm);
        _healthLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        _healthLabel.VerticalAlignment = VerticalAlignment.Center;
        hpWrapper.AddChild(_healthLabel);

        var centerBox = new HBoxContainer();
        centerBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        centerBox.Alignment = BoxContainer.AlignmentMode.Center;
        centerBox.AddThemeConstantOverride("separation", UiSpacingWide);
        row1.AddChild(centerBox);

        _roundLabel = new Label { Text = "第 1 波" };
        _roundLabel.AddThemeFontSizeOverride("font_size", UiFontBody);
        _roundLabel.AddThemeColorOverride("font_color", HandDrawnTheme.Colors.BloodBright);
        _roundLabel.AddThemeConstantOverride("outline_size", UiOutlineNorm);
        _roundLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.6f));
        centerBox.AddChild(_roundLabel);

        _zombiesLabel = new Label { Text = "剩余 0" };
        _zombiesLabel.AddThemeFontSizeOverride("font_size", UiFontSmall);
        _zombiesLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.6f, 0.6f));
        centerBox.AddChild(_zombiesLabel);

        _scoreLabel = new Label
        {
            Text = "0",
            HorizontalAlignment = HorizontalAlignment.Right,
            CustomMinimumSize = new Vector2(80, 0)
        };
        _scoreLabel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        _scoreLabel.AddThemeFontSizeOverride("font_size", UiFontBody);
        _scoreLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.55f, 0.45f));
        _scoreLabel.AddThemeConstantOverride("outline_size", UiOutlineNorm);
        _scoreLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.6f));
        row1.AddChild(_scoreLabel);

        // Row 2
        var row2 = new HBoxContainer();
        row2.AddThemeConstantOverride("separation", UiSpacingNormal);
        mainVbox.AddChild(row2);

        _damageLabel = _MakeStatLabel("攻击 10", new Color(0.8f, 0.5f, 0.4f), UiFontSmall);
        row2.AddChild(_damageLabel);
        _attackIntervalLabel = _MakeStatLabel("间隔 0.5s", new Color(0.75f, 0.65f, 0.45f), UiFontSmall);
        row2.AddChild(_attackIntervalLabel);
        _critLabel = _MakeStatLabel("暴击 0%", new Color(0.75f, 0.45f, 0.55f), UiFontSmall);
        row2.AddChild(_critLabel);
        _lifestealLabel = _MakeStatLabel("吸血 0%", new Color(0.7f, 0.35f, 0.35f), UiFontSmall);
        row2.AddChild(_lifestealLabel);
        _attackTypeLabel = _MakeStatLabel("近战", new Color(0.6f, 0.55f, 0.6f), UiFontSmall);
        row2.AddChild(_attackTypeLabel);
        _targetPriorityLabel = _MakeStatLabel("最近", new Color(0.55f, 0.55f, 0.5f), UiFontSmall);
        row2.AddChild(_targetPriorityLabel);
        _dashCooldownLabel = _MakeStatLabel("闪避 就绪", new Color(0.55f, 0.6f, 0.55f), UiFontSmall);
        row2.AddChild(_dashCooldownLabel);
        _speedLabel = _MakeStatLabel("速度 0", new Color(0.65f, 0.55f, 0.55f), UiFontSmall);
        row2.AddChild(_speedLabel);

        _effectsContainer = new HBoxContainer();
        _effectsContainer.AddThemeConstantOverride("separation", UiSpacingHairline);
        _effectsContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row2.AddChild(_effectsContainer);

        _soundToggleButton = new Button
        {
            Text = "🔊",
            CustomMinimumSize = new Vector2(24, 20),
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "点击切换音效"
        };
        _soundToggleButton.Pressed += _onToggleSound;
        row2.AddChild(_soundToggleButton);

        var helpBtn = new Button
        {
            Text = "?",
            CustomMinimumSize = new Vector2(20, 20),
            FocusMode = Control.FocusModeEnum.None
        };
        helpBtn.Pressed += _onToggleHelp;
        row2.AddChild(helpBtn);
    }

    // ═══ Public API ═══

    public void UpdateHealth(int currentHealth, int maxHealth)
    {
        _healthBar.MaxValue = maxHealth;
        _healthBar.Value = currentHealth;
        float healthPercent = (float)currentHealth / maxHealth;

        var hpColor = healthPercent > GameConfig.HealthHighThreshold ? HandDrawnTheme.Colors.HpHigh
            : healthPercent > GameConfig.HealthMedThreshold ? HandDrawnTheme.Colors.HpMed
            : HandDrawnTheme.Colors.HpLow;
        _healthBar.AddThemeStyleboxOverride("fill", HandDrawnTheme.MakeHpBarFill(hpColor));

        _healthLabel.Text = $"{currentHealth}/{maxHealth}";

        if (healthPercent <= 0.3f)
        {
            if (_lowHealthTween == null || !_lowHealthTween.IsRunning())
            {
                _lowHealthTween = CreateTween();
                _lowHealthTween.SetLoops();
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
        _roundLabel.Text = $"🏆 回合 {roundNumber}";
        _roundLabel.TooltipText = $"当前回合: {roundNumber}";
    }

    public void UpdateZombies(int zombiesRemaining, int totalZombies)
    {
        _zombiesLabel.Text = $"剩余: {zombiesRemaining}/{totalZombies}";
    }

    public void UpdateScore(int newScore)
    {
        _scoreLabel.Text = newScore.ToString();
    }

    public void UpdateDashCooldown(float remaining)
    {
        if (remaining <= 0.0f)
        {
            _dashCooldownLabel.Text = "闪避 就绪";
            _dashCooldownLabel.TooltipText = "闪避已就绪";
        }
        else
        {
            string secs = remaining < 2.0f ? remaining.ToString("F1") + "s" : Mathf.FloorToInt(remaining) + "s";
            _dashCooldownLabel.Text = $"闪避 {secs}";
            _dashCooldownLabel.TooltipText = $"闪避冷却中，还需 {secs}";
        }
    }

    public void UpdateAttackInterval(float interval, float cooldown)
    {
        _attackIntervalLabel.Text = $"间隔 {interval:F2}s";
        string intervalText = $"{interval:F2}s";
        string cooldownText = cooldown > 0 ? $"{cooldown:F2}s" : "就绪";
        _attackIntervalLabel.TooltipText = $"攻击间隔: {intervalText}\n下次可攻击: {cooldownText}";
    }

    public void UpdatePlayerStatus(Player player)
    {
        _damageLabel.Text = $"攻击 {Mathf.RoundToInt(player.CurrentDamage)}";
        _critLabel.Text = $"暴击 {Mathf.RoundToInt(player.CriticalChance)}%";
        _lifestealLabel.Text = $"吸血 {Mathf.RoundToInt(player.LifeStealPercent)}%";
        _speedLabel.Text = $"速度 {Mathf.RoundToInt(player.CurrentSpeed)}";
        _attackTypeLabel.Text = player.CurrentAttackType switch
        {
            AttackType.Melee => "近战",
            AttackType.Ranged => "远程",
            AttackType.Area => "范围",
            AttackType.Magic => "魔法",
            _ => "近战"
        };
        _targetPriorityLabel.Text = player.TargetPriority switch
        {
            0 => "锁定: 最近",
            1 => "锁定: 最远",
            2 => "锁定: 密集",
            _ => "锁定: 最近"
        };
        _UpdateStatusTooltips(player);
        UpdateEffects(player);
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
            var style = new StyleBoxFlat
            {
                BgColor = ColorUtils.Darken(itemColor, 0.6f) with { A = 0.7f },
                BorderWidthBottom = 2,
                BorderColor = itemColor,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4
            };
            btn.AddThemeStyleboxOverride("normal", style);

            var hoverStyle = new StyleBoxFlat
            {
                BgColor = ColorUtils.Darken(itemColor, 0.4f),
                BorderWidthBottom = 2,
                BorderColor = itemColor,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4
            };
            btn.AddThemeStyleboxOverride("hover", hoverStyle);

            string effectTooltip = (string)item["tooltip"];
            btn.MouseEntered += () => _showTooltip(effectTooltip);
            btn.MouseExited += () => _hideTooltip();

            _effectsContainer.AddChild(btn);
            _effectIcons[(string)item["name"]] = btn;
        }

        if (activeItems.Count == 0)
        {
            var hint = new Label { Text = "无" };
            hint.AddThemeFontSizeOverride("font_size", UiFontMicro);
            hint.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.6f));
            _effectsContainer.AddChild(hint);
        }
    }

    // ═══ 私有工具 ═══

    private void _UpdateStatusTooltips(Player player)
    {
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
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", fontSize);
        lbl.AddThemeColorOverride("font_color", color);
        lbl.AddThemeConstantOverride("outline_size", UiOutlineThin);
        lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.5f));
        return lbl;
    }

    public void SetSoundIcon(bool sfxEnabled)
    {
        if (_soundToggleButton != null)
            _soundToggleButton.Text = sfxEnabled ? "🔊" : "🔇";
    }
}
