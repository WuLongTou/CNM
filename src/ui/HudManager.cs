namespace ZombieWars.UI;

using Godot;
using System.Collections.Generic;
using ZombieWars.Core;
using ZombieWars.Enemy;
using ZombieWars.Weapons;
using Player = ZombieWars.Player.Player;
using WeaponCategory = ZombieWars.Core.GameEnums.WeaponCategory;
using static ZombieWars.UI.HandDrawnTheme;
using static ZombieWars.UI.HandDrawnTheme.Colors;

/// <summary>
/// Manages the HUD (heads-up display) shown during combat.
/// </summary>
public partial class HudManager : Control
{
    private Panel _hudPanel = null!;
    private Label _healthLabel = null!;
    private Label _roundLabel = null!;
    private Label _zombiesLabel = null!;
    private Label _damageLabel = null!;
    private Label _critLabel = null!;
    private Label _lifestealLabel = null!;
    private Label _attackIntervalLabel = null!;
    private Label _attackTypeLabel = null!;
    private Label _targetPriorityLabel = null!;
    private Label _dashCooldownLabel = null!;
    private Label _speedLabel = null!;
    private Label _scoreLabel = null!;
    private ProgressBar _healthBar = null!;
    private ProgressBar _bossHealthBar = null!;
    private Label _bossHealthLabel = null!;
    private HBoxContainer _effectsContainer = null!;
    private readonly Dictionary<string, Button> _effectIcons = new();
    private Button _soundToggleButton = null!;
    private Tween? _lowHealthTween;
    private bool _bossUiVisible;
    private int _bossUiHealth = -1;
    private int _bossUiMaxHealth = -1;

    public override void _Ready()
    {
        _CreateHudPanel();
        _CreateBossHealthBar();
    }

    private void _CreateHudPanel()
    {
        _hudPanel = new Panel();
        _hudPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        _hudPanel.CustomMinimumSize = new Vector2(0, 48);
        _hudPanel.OffsetBottom = 52;

        var style = MakeDarkPanel(0.90f);
        style.BorderColor = new Color(0.239f, 0.102f, 0.102f, 0.4f);
        style.BorderWidthBottom = 1;
        _hudPanel.AddThemeStyleboxOverride("panel", style);
        AddChild(_hudPanel);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        _hudPanel.AddChild(margin);

        var mainVbox = new VBoxContainer();
        mainVbox.AddThemeConstantOverride("separation", 3);
        margin.AddChild(mainVbox);

        // Row 1: HP bar | Round | Score
        var row1 = new HBoxContainer();
        row1.AddThemeConstantOverride("separation", 12);
        mainVbox.AddChild(row1);

        // Left: HP bar
        var hpWrapper = new HBoxContainer();
        hpWrapper.AddThemeConstantOverride("separation", 6);
        hpWrapper.CustomMinimumSize = new Vector2(240, 0);
        hpWrapper.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        row1.AddChild(hpWrapper);

        _healthBar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(180, 14),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            ShowPercentage = false
        };
        _healthBar.AddThemeStyleboxOverride("background", MakeHpBarBg());
        _healthBar.AddThemeStyleboxOverride("fill", MakeHpBarFill(HandDrawnTheme.Colors.HpHigh));
        hpWrapper.AddChild(_healthBar);

        _healthLabel = new Label { Text = "120/120" };
        _healthLabel.AddThemeFontSizeOverride("font_size", 11);
        _healthLabel.AddThemeColorOverride("font_color", new Color(0.94f, 0.91f, 0.91f));
        _healthLabel.AddThemeConstantOverride("outline_size", 2);
        _healthLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        _healthLabel.VerticalAlignment = VerticalAlignment.Center;
        hpWrapper.AddChild(_healthLabel);

        // Center: round + zombies
        var centerBox = new HBoxContainer();
        centerBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        centerBox.Alignment = BoxContainer.AlignmentMode.Center;
        centerBox.AddThemeConstantOverride("separation", 16);
        row1.AddChild(centerBox);

        _roundLabel = new Label { Text = "第 1 波" };
        _roundLabel.AddThemeFontSizeOverride("font_size", 14);
        _roundLabel.AddThemeColorOverride("font_color", BloodBright);
        _roundLabel.AddThemeConstantOverride("outline_size", 2);
        _roundLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.6f));
        centerBox.AddChild(_roundLabel);

        _zombiesLabel = new Label { Text = "剩余 0" };
        _zombiesLabel.AddThemeFontSizeOverride("font_size", 12);
        _zombiesLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.6f, 0.6f));
        centerBox.AddChild(_zombiesLabel);

        // Right: score
        _scoreLabel = new Label
        {
            Text = "0",
            HorizontalAlignment = HorizontalAlignment.Right,
            CustomMinimumSize = new Vector2(80, 0)
        };
        _scoreLabel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        _scoreLabel.AddThemeFontSizeOverride("font_size", 14);
        _scoreLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.55f, 0.45f));
        _scoreLabel.AddThemeConstantOverride("outline_size", 2);
        _scoreLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.6f));
        row1.AddChild(_scoreLabel);

        // Row 2: stats
        var row2 = new HBoxContainer();
        row2.AddThemeConstantOverride("separation", 10);
        mainVbox.AddChild(row2);

        _damageLabel = _MakeStatLabel("攻击 10", new Color(0.8f, 0.5f, 0.4f));
        row2.AddChild(_damageLabel);
        _attackIntervalLabel = _MakeStatLabel("间隔 0.50s", new Color(0.75f, 0.65f, 0.45f));
        row2.AddChild(_attackIntervalLabel);
        _critLabel = _MakeStatLabel("暴击 0%", new Color(0.75f, 0.45f, 0.55f));
        row2.AddChild(_critLabel);
        _lifestealLabel = _MakeStatLabel("吸血 0%", new Color(0.7f, 0.35f, 0.35f));
        row2.AddChild(_lifestealLabel);
        _attackTypeLabel = _MakeStatLabel("近战", new Color(0.6f, 0.55f, 0.6f));
        row2.AddChild(_attackTypeLabel);
        _targetPriorityLabel = _MakeStatLabel("最近", new Color(0.55f, 0.55f, 0.5f));
        row2.AddChild(_targetPriorityLabel);
        _dashCooldownLabel = _MakeStatLabel("闪避 就绪", new Color(0.55f, 0.6f, 0.55f));
        row2.AddChild(_dashCooldownLabel);
        _speedLabel = _MakeStatLabel("速度 0", new Color(0.65f, 0.55f, 0.55f));
        row2.AddChild(_speedLabel);

        // Effect icons
        _effectsContainer = new HBoxContainer();
        _effectsContainer.AddThemeConstantOverride("separation", 3);
        _effectsContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row2.AddChild(_effectsContainer);

        // Sound toggle
        var soundBtn = new Button
        {
            Text = "🔊",
            CustomMinimumSize = new Vector2(24, 20),
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "点击切换音效"
        };
        soundBtn.Pressed += _ToggleSound;
        row2.AddChild(soundBtn);
        _soundToggleButton = soundBtn;

        // Help button
        var helpBtn = new Button
        {
            Text = "?",
            CustomMinimumSize = new Vector2(20, 20),
            FocusMode = Control.FocusModeEnum.None
        };
        helpBtn.Pressed += () => GetTree().Root.GetNodeOrNull("Main")?.Call("toggle_help");
        row2.AddChild(helpBtn);
    }

    private void _CreateBossHealthBar()
    {
        _bossHealthBar = new ProgressBar
        {
            ShowPercentage = false,
            Visible = false,
            ZIndex = 5
        };
        _bossHealthBar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        _bossHealthBar.OffsetTop = 66;
        _bossHealthBar.OffsetBottom = 78;
        _bossHealthBar.OffsetLeft = 16;
        _bossHealthBar.OffsetRight = -16;
        _bossHealthBar.CustomMinimumSize = new Vector2(0, 12);
        var bossBg = MakeHpBarBg();
        var bossFill = MakeHpBarFill(BloodBright);
        _bossHealthBar.AddThemeStyleboxOverride("background", bossBg);
        _bossHealthBar.AddThemeStyleboxOverride("fill", bossFill);
        AddChild(_bossHealthBar);

        _bossHealthLabel = new Label
        {
            Visible = false,
            ZIndex = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _bossHealthLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        _bossHealthLabel.OffsetTop = 66;
        _bossHealthLabel.OffsetBottom = 84;
        _bossHealthLabel.AddThemeFontSizeOverride("font_size", 12);
        _bossHealthLabel.AddThemeColorOverride("font_color", BloodBright);
        _bossHealthLabel.AddThemeConstantOverride("outline_size", 2);
        _bossHealthLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.7f));
        AddChild(_bossHealthLabel);
    }

    private static Label _MakeStatLabel(string text, Color color)
    {
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", 10);
        lbl.AddThemeColorOverride("font_color", color);
        lbl.AddThemeConstantOverride("outline_size", 1);
        lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.5f));
        return lbl;
    }

    public void UpdateHealthDisplay(int currentHealth, int maxHealth)
    {
        if (_healthBar == null) return;
        _healthBar.MaxValue = maxHealth;
        _healthBar.Value = currentHealth;
        float healthPercent = (float)currentHealth / maxHealth;

        var hpColor = healthPercent > GameConfig.HealthHighThreshold ? HandDrawnTheme.Colors.HpHigh
            : healthPercent > GameConfig.HealthMedThreshold ? HandDrawnTheme.Colors.HpMed
            : HandDrawnTheme.Colors.HpLow;
        _healthBar.AddThemeStyleboxOverride("fill", MakeHpBarFill(hpColor));

        if (_healthLabel != null)
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

    public void UpdateRoundDisplay(int round)
    {
        if (_roundLabel != null)
        {
            _roundLabel.Text = $"🏆 回合 {round}";
            _roundLabel.TooltipText = $"当前回合: {round}";
        }
    }

    public void UpdateZombiesDisplay(int remaining, int total)
    {
        if (_zombiesLabel != null)
            _zombiesLabel.Text = $"剩余: {remaining}/{total}";
    }

    public void UpdateScoreDisplay(int score)
    {
        if (_scoreLabel != null)
            _scoreLabel.Text = score.ToString();
    }

    public void UpdatePlayerStatus(Player player)
    {
        if (_damageLabel != null)
            _damageLabel.Text = $"攻击 {Mathf.RoundToInt(player.CurrentDamage)}";
        if (_critLabel != null)
            _critLabel.Text = $"暴击 {Mathf.RoundToInt(player.CriticalChance)}%";
        if (_lifestealLabel != null)
            _lifestealLabel.Text = $"吸血 {Mathf.RoundToInt(player.LifeStealPercent)}%";
        if (_speedLabel != null)
            _speedLabel.Text = $"速度 {Mathf.RoundToInt(player.CurrentSpeed)}";
        if (_attackTypeLabel != null)
        {
            _attackTypeLabel.Text = player.CurrentAttackType switch
            {
                Core.GameEnums.AttackType.Melee => "近战",
                Core.GameEnums.AttackType.Ranged => "远程",
                Core.GameEnums.AttackType.Area => "范围",
                Core.GameEnums.AttackType.Magic => "魔法",
                _ => "近战"
            };
        }
        if (_targetPriorityLabel != null)
        {
            _targetPriorityLabel.Text = player.TargetPriority switch
            {
                0 => "锁定: 最近",
                1 => "锁定: 最远",
                2 => "锁定: 密集",
                _ => "锁定: 最近"
            };
        }

        _UpdateEffectsDisplay(player);
    }

    public void UpdateDashDisplay(Player player)
    {
        if (_dashCooldownLabel == null) return;
        if (player.CanDash)
        {
            _dashCooldownLabel.Text = "闪避 就绪";
            _dashCooldownLabel.TooltipText = "闪避已就绪，按 Shift 使用";
        }
        else
        {
            var remaining = player.DashCooldownTimer?.TimeLeft ?? 0.0f;
            if (remaining < 0.5f)
            {
                _dashCooldownLabel.Text = "闪避 就绪";
                _dashCooldownLabel.TooltipText = "闪避已就绪，按 Shift 使用";
            }
            else
            {
                var secs = Mathf.CeilToInt(remaining);
                _dashCooldownLabel.Text = $"闪避 {secs}s";
                _dashCooldownLabel.TooltipText = $"闪避冷却中，还需 {secs}s";
            }
        }
    }

    public void UpdateAttackIntervalDisplay(Player player)
    {
        if (_attackIntervalLabel == null || player.Combat == null) return;
        var intervalText = Mathf.Snapped(player.AttackCooldown, 0.1f).ToString("F1") + "s";
        string cooldownText = player.Combat.AttackTimer.TimeLeft < 0.05f
            ? "就绪"
            : ((float)player.Combat.AttackTimer.TimeLeft).ToString("F1") + "s";
        _attackIntervalLabel.Text = $"间隔 {intervalText} | 冷却 {cooldownText}";
        _attackIntervalLabel.TooltipText = $"攻击间隔: {intervalText}\n下次可攻击: {cooldownText}";
    }

    public void UpdateBossHealth(Zombie? boss)
    {
        if (boss != null && IsInstanceValid(boss))
        {
            if (!_bossUiVisible)
            {
                _bossUiVisible = true;
                _bossHealthBar.Visible = true;
                _bossHealthLabel.Visible = true;
            }
            if (boss.CurrentHealth != _bossUiHealth || boss.MaxHealth != _bossUiMaxHealth)
            {
                _bossUiHealth = boss.CurrentHealth;
                _bossUiMaxHealth = boss.MaxHealth;
                _bossHealthBar.MaxValue = boss.MaxHealth;
                _bossHealthBar.Value = boss.CurrentHealth;
                _bossHealthLabel.Text = $"BOSS  {boss.CurrentHealth}/{boss.MaxHealth}";
            }
        }
        else
        {
            if (_bossUiVisible)
            {
                _bossUiVisible = false;
                _bossUiHealth = -1;
                _bossUiMaxHealth = -1;
                _bossHealthBar.Visible = false;
                _bossHealthLabel.Visible = false;
            }
        }
    }

    private void _UpdateEffectsDisplay(Player player)
    {
        if (_effectsContainer == null) return;

        foreach (Node child in _effectsContainer.GetChildren())
            child.QueueFree();
        _effectIcons.Clear();

        var activeItems = new List<Godot.Collections.Dictionary>();

        // Current weapon
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

        // Equipment
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

        // Shield aura
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

        // Stats
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

        foreach (var item in activeItems)
        {
            var btn = new Button
            {
                Text = (string)item["icon"],
                CustomMinimumSize = new Vector2(28, 24),
                FocusMode = Control.FocusModeEnum.None,
                Flat = true
            };
            btn.AddThemeFontSizeOverride("font_size", 14);

            var itemColor = (Color)item["color"];
            var style = new StyleBoxFlat
            {
                BgColor = _Darken(itemColor, 0.6f) with { A = 0.7f },
                BorderWidthBottom = 2,
                BorderColor = itemColor,
                CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
            };
            btn.AddThemeStyleboxOverride("normal", style);

            var hoverStyle = new StyleBoxFlat
            {
                BgColor = _Darken(itemColor, 0.4f),
                BorderWidthBottom = 2,
                BorderColor = itemColor,
                CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
            };
            btn.AddThemeStyleboxOverride("hover", hoverStyle);

            btn.MouseEntered += () => ShowEffectTooltip((string)item["tooltip"]);
            btn.MouseExited += () => HideEffectTooltip();

            _effectsContainer.AddChild(btn);
            _effectIcons[(string)item["name"]] = btn;
        }

        if (activeItems.Count == 0)
        {
            var hint = new Label { Text = "无" };
            hint.AddThemeFontSizeOverride("font_size", 11);
            hint.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.6f));
            _effectsContainer.AddChild(hint);
        }
    }

    private void ShowEffectTooltip(string text)
    {
        // Delegate to UIManager's tooltip system
        GetNode("/root/Main/UILayer/UIManager")?.Call("show_tooltip", text);
    }

    private void HideEffectTooltip()
    {
        GetNode("/root/Main/UILayer/UIManager")?.Call("hide_tooltip");
    }

    private void _ToggleSound()
    {
        var sm = GameServices.Audio;
        sm.SfxEnabled = !sm.SfxEnabled;
        if (_soundToggleButton != null)
            _soundToggleButton.Text = sm.SfxEnabled ? "🔊" : "🔇";
    }

    private static Color _Darken(Color c, float amount)
    {
        return new Color(
            Mathf.Max(0.0f, c.R - amount),
            Mathf.Max(0.0f, c.G - amount),
            Mathf.Max(0.0f, c.B - amount),
            c.A);
    }

    public Panel GetHudPanel() => _hudPanel;
}