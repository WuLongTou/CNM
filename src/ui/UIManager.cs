namespace ZombieWars.UI;

using Godot;
using System;
using System.Collections.Generic;
using ZombieWars.Core;
using ZombieWars.Weapons;

using Player = ZombieWars.Player.Player;
using static ZombieWars.Core.GameEnums;
using static ZombieWars.UI.HandDrawnTheme;
using static ZombieWars.UI.HandDrawnTheme.Colors;

/// <summary>
/// UI Manager — creates and manages all in-game UI elements.
/// Handles HUD, weapon selection panel, tooltip, pause overlay, controls panel, and welcome screen.
///
/// REFACTORING IN PROGRESS: 部分功能已拆分到独立组件:
/// - HudManager: HUD 元素管理
/// - WeaponSelectionPanel: 武器选择面板
/// - TooltipSystem: 提示信息显示
///
/// 这些组件作为子节点添加到场景中，当前 UIManager 保留所有功能以确保向后兼容。
/// 未来计划：逐步将功能迁移到独立组件，减少 UIManager 的复杂度。
/// </summary>
public partial class UIManager : Control
{
    // ═══════════════════════════════════════════════
    // Signals
    // ═══════════════════════════════════════════════
    public event Action<object?> WeaponCardSelected = delegate { };
    public event Action UpgradeSkipped = delegate { };

    // ═══════════════════════════════════════════════
    // Component References
    // ═══════════════════════════════════════════════
    private ScreenEffects _screenFx = null!;
    private GameOverUI _gameOverUi = null!;

    // ═══════════════════════════════════════════════
    // HUD
    // ═══════════════════════════════════════════════
    private Panel _weaponSelectionPanel = null!;
    private readonly List<Button> _weaponButtons = new();
    private Button? _skipButton;
    private HudPanel? _hudPanel;
    private HelpPanel _helpPanel = null!;
    private WelcomePanel? _welcomePanel;
    private ProgressBar _bossHealthBar = null!;
    private Label _bossHealthLabel = null!;
    private bool _bossUiVisible;
    private int _bossUiHealth = -1;
    private int _bossUiMaxHealth = -1;

    // ═══════════════════════════════════════════════
    // Weapon Selection
    // ═══════════════════════════════════════════════
    private readonly List<object> _currentWeaponOptions = new();
    private GameManager? _gameManager;
    private WeaponDeck? _weaponDeck;

    // ═══════════════════════════════════════════════
    // Tooltip
    // ═══════════════════════════════════════════════
    private Panel _tooltipPanel = null!;
    private RichTextLabel _tooltipLabel = null!;
    private Timer? _tooltipHideTimer;
    private float _tooltipMaxWidth = GameConfig.TooltipMaxWidth;
    private Vector2 _tooltipOffset = GameConfig.TooltipOffset;

    // ═══════════════════════════════════════════════
    // Pause
    // ═══════════════════════════════════════════════
    private PausePanel? _pausePanel;
    private bool _pauseLockedByUpgrade;
    private bool _userPaused;
    private Label _currentWeaponLabel = null!;

    private RoundBanner? _roundBanner;
    private Panel? _roundSummaryPanel;

    // ═══════════════════════════════════════════════
    // Level Up / Milestone
    // ═══════════════════════════════════════════════
    private Panel? _levelUpPanel;
    private Label? _levelUpLabel;
    private Tween? _levelUpTween;

    // ═══════════════════════════════════════════════
    // Lifecycle
    // ═══════════════════════════════════════════════

    public override void _Ready()
    {
        AddToGroup("ui_manager");
        ProcessMode = ProcessModeEnum.Always;

        _screenFx = new ScreenEffects();
        _screenFx.Name = "ScreenEffects";
        AddChild(_screenFx);

        _gameOverUi = new GameOverUI();
        _gameOverUi.Name = "GameOverUI";
        AddChild(_gameOverUi);

        // Setup achievement system callbacks
        Core.AchievementSystem.OnAchievementUnlocked += _ShowAchievementNotification;
        Core.AchievementSystem.OnMilestoneReached += _ShowMilestoneNotification;

        // 构造各 UI 子面板
        _hudPanel = new HudPanel();
        _hudPanel.Setup(
            onToggleSound: _ToggleSound,
            onToggleHelp: _ToggleControlsPanel,
            showTooltip: _ShowTooltip,
            hideTooltip: _HideTooltip);
        AddChild(_hudPanel);
        _CreateBossHealthBar();

        _CreateWeaponSelectionPanel();
        _helpPanel = new HelpPanel { Name = "HelpPanel", ZIndex = 80 };
        AddChild(_helpPanel);
        _welcomePanel = new WelcomePanel();
        _welcomePanel.Setup(onStart: _StartGame, onToggleHelp: _ToggleControlsPanel);
        AddChild(_welcomePanel);
        _roundBanner = new RoundBanner();
        AddChild(_roundBanner);

        _SetupConnections();
        _CreateTooltipPanel();
        _pausePanel = new PausePanel();
        AddChild(_pausePanel);
        GetViewport().SizeChanged += _OnUiViewportSizeChanged;
        _OnUiViewportSizeChanged();
        SetProcess(true);
    }

    public override void _ExitTree()
    {
        // 退订静态事件，防止 UIManager 实例被 AchievementSystem 静态委托长期持有。
        Core.AchievementSystem.OnAchievementUnlocked -= _ShowAchievementNotification;
        Core.AchievementSystem.OnMilestoneReached -= _ShowMilestoneNotification;
        if (GetViewport() != null)
            GetViewport().SizeChanged -= _OnUiViewportSizeChanged;
    }

    // ═══════════════════════════════════════════════
    // UI Construction
    // ═══════════════════════════════════════════════

    private void _CreateBossHealthBar()
    {
        _bossHealthBar = new ProgressBar
        {
            ShowPercentage = false
        };
        _bossHealthBar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        _bossHealthBar.OffsetTop = 66;
        _bossHealthBar.OffsetBottom = 78;
        _bossHealthBar.OffsetLeft = 16;
        _bossHealthBar.OffsetRight = -16;
        _bossHealthBar.CustomMinimumSize = new Vector2(0, 12);
        _bossHealthBar.Visible = false;
        _bossHealthBar.ZIndex = 5;
        _bossHealthBar.AddThemeStyleboxOverride("background", MakeHpBarBg());
        _bossHealthBar.AddThemeStyleboxOverride("fill", MakeHpBarFill(HandDrawnTheme.Colors.BloodBright));
        AddChild(_bossHealthBar);

        _bossHealthLabel = new Label();
        _bossHealthLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        _bossHealthLabel.OffsetTop = 66;
        _bossHealthLabel.OffsetBottom = 84;
        _bossHealthLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _bossHealthLabel.VerticalAlignment = VerticalAlignment.Center;
        _bossHealthLabel.AddThemeFontSizeOverride("font_size", 12);
        _bossHealthLabel.AddThemeColorOverride("font_color", HandDrawnTheme.Colors.BloodBright);
        _bossHealthLabel.AddThemeConstantOverride("outline_size", 2);
        _bossHealthLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.7f));
        _bossHealthLabel.Visible = false;
        _bossHealthLabel.ZIndex = 6;
        AddChild(_bossHealthLabel);
    }

    private void _CreateWeaponSelectionPanel()
    {
        _weaponSelectionPanel = new Panel
        {
            Visible = false,
            ProcessMode = ProcessModeEnum.Always,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _weaponSelectionPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var weaponStyle = MakeDarkPanel(0.92f);
        weaponStyle.BgColor = new Color(BattlefieldBg, 0.95f);
        _weaponSelectionPanel.AddThemeStyleboxOverride("panel", weaponStyle);
        AddChild(_weaponSelectionPanel);

        var centerContainer = new CenterContainer();
        centerContainer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _weaponSelectionPanel.AddChild(centerContainer);

        var mainVbox = new VBoxContainer();
        mainVbox.AddThemeConstantOverride("separation", 30);
        centerContainer.AddChild(mainVbox);

        // Title
        var titleLabel = new Label
        {
            Text = "⚔️ 选择本回合强化 ⚔️"
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 32);
        titleLabel.AddThemeColorOverride("font_color", BloodBright);
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        mainVbox.AddChild(titleLabel);

        _currentWeaponLabel = new Label
        {
            Text = "当前武器: 无",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(720, 0)
        };
        _currentWeaponLabel.AddThemeFontSizeOverride("font_size", 16);
        _currentWeaponLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.9f));
        _currentWeaponLabel.HorizontalAlignment = HorizontalAlignment.Center;
        mainVbox.AddChild(_currentWeaponLabel);

        // Description
        var descLabel = new Label
        {
            Text = "你可能抽到新武器、装备，或对已有装备的强化卡",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(720, 0)
        };
        descLabel.AddThemeFontSizeOverride("font_size", 18);
        descLabel.AddThemeColorOverride("font_color", Godot.Colors.White); descLabel.HorizontalAlignment = HorizontalAlignment.Center;
        mainVbox.AddChild(descLabel);

        // Card button container
        var buttonContainer = new HBoxContainer();
        buttonContainer.AddThemeConstantOverride("separation", 20);
        buttonContainer.Alignment = BoxContainer.AlignmentMode.Center;
        mainVbox.AddChild(buttonContainer);

        // Create 3 weapon buttons
        for (int i = 0; i < 3; i++)
        {
            int cardIndex = i; // capture for closure
            var button = new Button
            {
                CustomMinimumSize = new Vector2(200, 280),
                FocusMode = Control.FocusModeEnum.None
            };
            button.MouseEntered += () => _OnCardMouseEntered(cardIndex);
            button.MouseExited += _OnCardMouseExited;
            button.Pressed += () => _OnWeaponButtonPressed(cardIndex);
            buttonContainer.AddChild(button);
            _weaponButtons.Add(button);

            var cardVbox = new VBoxContainer
            {
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            cardVbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            cardVbox.AddThemeConstantOverride("separation", 10);

            var margin = new MarginContainer
            {
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_top", 15);
            margin.AddThemeConstantOverride("margin_bottom", 15);
            margin.AddThemeConstantOverride("margin_left", 15);
            margin.AddThemeConstantOverride("margin_right", 15);
            button.AddChild(margin);
            margin.AddChild(cardVbox);

            // 1. Category
            var categoryLabel = new Label
            {
                Name = "Category",
                Text = "【类型】",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            categoryLabel.AddThemeFontSizeOverride("font_size", 12);
            categoryLabel.AddThemeColorOverride("font_color", NoteText);
            cardVbox.AddChild(categoryLabel);

            // 2. Title
            var title = new Label
            {
                Name = "Title",
                Text = "武器名称",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            title.AddThemeFontSizeOverride("font_size", 22);
            cardVbox.AddChild(title);

            // Separator
            var sep1 = new HSeparator { MouseFilter = Control.MouseFilterEnum.Ignore };
            cardVbox.AddChild(sep1);

            // 3. Icon
            var iconPlaceholder = new Control
            {
                Name = "IconContainer",
                CustomMinimumSize = new Vector2(0, 70),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            cardVbox.AddChild(iconPlaceholder);

            var iconLabel = new Label
            {
                Name = "Icon",
                Text = "⚔️",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            iconLabel.AddThemeFontSizeOverride("font_size", 55);
            iconPlaceholder.AddChild(iconLabel);
            iconLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);

            // 4. Type
            var typeLabel = new Label
            {
                Name = "Type",
                Text = "类型",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            typeLabel.AddThemeFontSizeOverride("font_size", 14);
            typeLabel.AddThemeColorOverride("font_color", NoteText);
            cardVbox.AddChild(typeLabel);

            // 5. Description (keywords only)
            var desc = new Label
            {
                Name = "Desc",
                Text = "",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            desc.AddThemeFontSizeOverride("font_size", 13);
            desc.AddThemeColorOverride("font_color", NoteText);
            cardVbox.AddChild(desc);

            // Spacer
            var spacer = new Control
            {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            cardVbox.AddChild(spacer);

            // 6. Rarity
            var rarity = new Label
            {
                Name = "Rarity",
                Text = "稀有度",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            rarity.AddThemeFontSizeOverride("font_size", 14);
            cardVbox.AddChild(rarity);

            // 7. Hint
            var hintLabel = new Label
            {
                Name = "Hint",
                Text = "悬停查看详情",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            hintLabel.AddThemeFontSizeOverride("font_size", 11);
            hintLabel.AddThemeColorOverride("font_color", NoteText);
            cardVbox.AddChild(hintLabel);
        }

        // Keyboard hint label
        var keyboardHintLabel = new Label
        {
            Text = "💡 按 [1] [2] [3] 选择 | 按 [S] 或 [Enter] 跳过",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        keyboardHintLabel.AddThemeFontSizeOverride("font_size", 14);
        keyboardHintLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.7f));
        mainVbox.AddChild(keyboardHintLabel);

        // Skip button
        var skipContainer = new CenterContainer();
        mainVbox.AddChild(skipContainer);

        _skipButton = new Button
        {
            Text = "跳过升级",
            CustomMinimumSize = new Vector2(150, 40)
        };
        _skipButton.AddThemeFontSizeOverride("font_size", 16);
        var skipNormal = MakeNotePanel(borderRadius: 3f, shadowSize: 3f);
        skipNormal.BgColor = new Color(0.25f, 0.2f, 0.2f, 0.9f);
        _skipButton.AddThemeStyleboxOverride("normal", skipNormal);
        var skipHover = MakeNotePanel(borderRadius: 3f, shadowSize: 3f);
        skipHover.BgColor = new Color(0.35f, 0.28f, 0.28f, 0.9f);
        _skipButton.AddThemeStyleboxOverride("hover", skipHover);
        _skipButton.AddThemeColorOverride("font_color", NoteBg);
        skipContainer.AddChild(_skipButton);
    }

    private void _CreateTooltipPanel()
    {
        _tooltipPanel = new Panel
        {
            Visible = false,
            ZIndex = 100,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ProcessMode = ProcessModeEnum.Always
        };

        var tooltipStyle = MakeNotePanel(shadowSize: 6f);
        tooltipStyle.BgColor = new Color(NoteBg, 0.98f);
        tooltipStyle.ShadowSize = 8;
        tooltipStyle.ShadowOffset = new Vector2(3, 3);
        _tooltipPanel.AddThemeStyleboxOverride("panel", tooltipStyle);

        _tooltipLabel = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(320, 0)
        };
        _tooltipLabel.AddThemeFontSizeOverride("normal_font_size", 14);
        _tooltipLabel.AddThemeFontSizeOverride("bold_font_size", 15);
        _tooltipLabel.AddThemeColorOverride("default_color", NoteText);

        var margin = new MarginContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        _tooltipPanel.AddChild(margin);
        margin.AddChild(_tooltipLabel);

        AddChild(_tooltipPanel);

        _tooltipHideTimer = new Timer
        {
            OneShot = true,
            WaitTime = 0.2f
        };
        _tooltipHideTimer.Timeout += _HideTooltipPanel;
        AddChild(_tooltipHideTimer);
    }

    // ═══════════════════════════════════════════════
    // Connections
    // ═══════════════════════════════════════════════

    private void _SetupConnections()
    {
        if (_gameManager != null)
        {
            _gameManager.WeaponSelectionStarted += _OnWeaponSelectionStarted;
            _gameManager.RoundStarted += _OnRoundStarted;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (IsUpgradeActive())
            {
                switch (key.Keycode)
                {
                    case Key.Key1:
                    case Key.Kp1:
                        _OnWeaponButtonPressed(0);
                        GetViewport().SetInputAsHandled();
                        return;
                    case Key.Key2:
                    case Key.Kp2:
                        _OnWeaponButtonPressed(1);
                        GetViewport().SetInputAsHandled();
                        return;
                    case Key.Key3:
                    case Key.Kp3:
                        _OnWeaponButtonPressed(2);
                        GetViewport().SetInputAsHandled();
                        return;
                    case Key.S:
                    case Key.Enter:
                    case Key.KpEnter:
                        _OnSkipPressed();
                        GetViewport().SetInputAsHandled();
                        return;
                }
            }

            if (key.Keycode == Key.H)
                _ToggleControlsPanel();
            else if (key.Keycode == Key.Escape && _helpPanel.Visible)
            {
                _helpPanel.Close();
                GetViewport().SetInputAsHandled();
            }
        }

        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left && IsUpgradeActive())
        {
            if (_skipButton != null && _skipButton.GetGlobalRect().HasPoint(mb.GlobalPosition))
            {
                _OnSkipPressed();
                GetViewport().SetInputAsHandled();
                return;
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("pause"))
        {
            if (IsUpgradeActive())
                return;
            SetUserPaused(!_userPaused);
            GetViewport().SetInputAsHandled();
        }
    }

    // ═══════════════════════════════════════════════
    // Process
    // ═══════════════════════════════════════════════

    public override void _Process(double delta)
    {
        if (_tooltipPanel != null && _tooltipPanel.Visible)
            _PositionTooltip();

        if (_gameManager != null && _gameManager.PlayerRef != null)
        {
            _UpdateDashDisplay(_gameManager.PlayerRef);
            _UpdateAttackIntervalDisplay(_gameManager.PlayerRef);
        }

        // Boss health bar
        if (_gameManager?.BossZombie != null && IsInstanceValid(_gameManager.BossZombie))
        {
            var boss = _gameManager.BossZombie;
            if (!_bossUiVisible)
            {
                _bossUiVisible = true;
                if (_bossHealthBar != null) _bossHealthBar.Visible = true;
                if (_bossHealthLabel != null) _bossHealthLabel.Visible = true;
            }
            if (boss.CurrentHealth != _bossUiHealth || boss.MaxHealth != _bossUiMaxHealth)
            {
                _bossUiHealth = boss.CurrentHealth;
                _bossUiMaxHealth = boss.MaxHealth;
                if (_bossHealthBar != null)
                {
                    _bossHealthBar.MaxValue = boss.MaxHealth;
                    _bossHealthBar.Value = boss.CurrentHealth;
                }
                if (_bossHealthLabel != null)
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
                if (_bossHealthBar != null) _bossHealthBar.Visible = false;
                if (_bossHealthLabel != null) _bossHealthLabel.Visible = false;
            }
        }

        if (_screenFx != null)
            _screenFx.ProcessEffects((float)delta);
    }

    private void _UpdateDashDisplay(Player player)
    {
        if (player.CanDash)
        {
            _hudPanel?.UpdateDashCooldown(0.0f);
        }
        else
        {
            var remaining = (float)(player.DashCooldownTimer?.TimeLeft ?? 0.0);
            _hudPanel?.UpdateDashCooldown(remaining);
        }
    }

    private void _UpdateAttackIntervalDisplay(Player player)
    {
        var intervalText = Mathf.Snapped(player.AttackCooldown, 0.1f);
        float cooldown = 0.0f;
        if (player.Combat != null && player.Combat.AttackTimer.TimeLeft >= 0.05f)
            cooldown = (float)player.Combat.AttackTimer.TimeLeft;
        _hudPanel?.UpdateAttackInterval((float)intervalText, cooldown);
    }

    // ═══════════════════════════════════════════════
    // Pause
    // ═══════════════════════════════════════════════

    public void SetUserPaused(bool paused)
    {
        _userPaused = paused;
        if (_pausePanel != null) _pausePanel.Visible = paused;
        if (paused)
            GetTree().Paused = true;
        else if (!_pauseLockedByUpgrade)
            GetTree().Paused = false;
    }

    public bool IsUpgradeActive() => _weaponSelectionPanel.Visible;

    private void _ToggleControlsPanel()
    {
        if (_weaponSelectionPanel.Visible && !_helpPanel.Visible) return;
        _helpPanel.Toggle();
    }

    public void ToggleHelp()
    {
        _helpPanel.Toggle();
    }

    private void _ToggleSound()
    {
        var sm = GameServices.Audio;
        sm.SfxEnabled = !sm.SfxEnabled;
        _hudPanel?.SetSoundIcon(sm.SfxEnabled);
    }

    private void _StartGame()
    {
        if (_welcomePanel != null) _welcomePanel.Visible = false;
    }

    // ═══════════════════════════════════════════════
    // Weapon Selection
    // ═══════════════════════════════════════════════

    public void ShowWeaponSelection()
    {
        if (_weaponDeck == null)
        {
            LogUtils.PrintErr("错误：武器牌堆未设置");
            return;
        }
        _pauseLockedByUpgrade = true;
        GetTree().Paused = true;
        if (_pausePanel != null) _pausePanel.Visible = false;

        if (_helpPanel.Visible)
            _helpPanel.Close();

        if (_gameManager?.PlayerRef != null)
        {
            _UpdateCurrentWeaponLabel(_gameManager.PlayerRef);
            _currentWeaponOptions.Clear();
            var drawn = _weaponDeck.DrawThreeCards(_gameManager.PlayerRef);
            foreach (var w in drawn) _currentWeaponOptions.Add(w);
        }
        else
        {
            _currentWeaponOptions.Clear();
            var drawn = _weaponDeck.DrawThreeCards();
            foreach (var w in drawn) _currentWeaponOptions.Add(w);
        }

        for (int i = 0; i < 3; i++)
        {
            if (i < _currentWeaponOptions.Count)
            {
                var weapon = _currentWeaponOptions[i];
                var button = _weaponButtons[i];
                button.Visible = true;

                if (weapon is UpgradeCard upgrade)
                {
                    _RenderUpgradeCard(button, upgrade);
                    continue;
                }

                if (weapon is not WeaponCard wc) continue;

                // Get internal card nodes
                var margin = (MarginContainer)button.GetChild(0);
                var vbox = (VBoxContainer)margin.GetChild(0);
                var categoryNode = (Label)vbox.GetNode("Category");
                var titleNode = (Label)vbox.GetNode("Title");
                var typeNode = (Label)vbox.GetNode("Type");
                var iconNode = vbox.FindChild("Icon", true, false) as Label;
                var descNode = (Label)vbox.GetNode("Desc");
                var rarityNode = (Label)vbox.GetNode("Rarity");

                titleNode.Text = wc.CardName;

                var upgradeCategory = wc.UpgradeCategory;
                if (string.IsNullOrEmpty(upgradeCategory))
                    upgradeCategory = wc.Category == WeaponCategory.Weapon ? "武器" : "装备";
                categoryNode.Text = $"【{upgradeCategory} Lv.{wc.Tier}】";

                if (wc.Category == WeaponCategory.Weapon)
                    typeNode.Text = wc.GetWeaponTypeText();
                else
                    typeNode.Text = "装备";

                // Keywords
                var keywords = new List<string>();
                if (wc.DamageModifier > 1.0f) keywords.Add("伤害↑");
                else if (wc.DamageModifier < 1.0f) keywords.Add("伤害↓");
                if (wc.AttackSpeedModifier > 1.0f) keywords.Add("攻速↑");
                else if (wc.AttackSpeedModifier < 1.0f) keywords.Add("攻速↓");
                if (wc.HealthModifier > 0) keywords.Add("生命↑");
                else if (wc.HealthModifier < 0) keywords.Add("生命↓");
                if (wc.SpeedModifier > 0) keywords.Add("速度↑");
                if (wc.CriticalBonus > 0) keywords.Add("暴击");
                if (wc.LifeStealBonus > 0) keywords.Add("吸血");
                if (wc.FreezeBonus > 0) keywords.Add("冰冻");
                if (wc.PoisonBonus > 0) keywords.Add("中毒");
                if (wc.GrantsAura) keywords.Add("护盾");
                if (wc.HealthCost > 0) keywords.Add("⚠消耗生命");
                if (wc.MeleeEffects.Count > 0 || wc.RangedEffects.Count > 0 || wc.MagicEffects.Count > 0)
                    keywords.Add("特效");

                descNode.Text = keywords.Count > 0 ? string.Join(" ", keywords) : "基础";
                rarityNode.Text = WeaponFormatter.GetRarityText(wc.Rarity);

                // 使用真实武器图片替代 emoji 图标
                if (wc.IconTexture != null && iconNode != null)
                {
                    // Check if we already have a TextureRect (avoid duplicates on reuse)
                    var existingTex = iconNode.GetParent()?.GetNodeOrNull<TextureRect>("IconTex");
                    if (existingTex != null)
                    {
                        existingTex.Texture = wc.IconTexture;
                    }
                    else
                    {
                        var texRect = new TextureRect
                        {
                            Name = "IconTex",
                            Texture = wc.IconTexture,
                            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                            CustomMinimumSize = new Vector2(60, 60)
                        };
                        iconNode.GetParent()?.RemoveChild(iconNode);
                        iconNode.GetParent()?.AddChild(texRect);
                    }
                }
                else if (iconNode != null)
                {
                    iconNode.Text = wc.Icon;
                }

                var cardBg = new Color(0.18f, 0.13f, 0.13f, 0.95f);
                Color borderColor;

                switch (wc.Rarity)
                {
                    case 1:
                        borderColor = RarityCommon;
                        titleNode.AddThemeColorOverride("font_color", RarityCommon);
                        rarityNode.AddThemeColorOverride("font_color", RarityCommon);
                        break;
                    case 2:
                        borderColor = RarityRare;
                        titleNode.AddThemeColorOverride("font_color", RarityRare);
                        rarityNode.AddThemeColorOverride("font_color", RarityRare);
                        break;
                    case 3:
                        borderColor = RarityEpic;
                        titleNode.AddThemeColorOverride("font_color", RarityEpic);
                        rarityNode.AddThemeColorOverride("font_color", RarityEpic);
                        break;
                    case 4:
                        borderColor = new Color(1f, 0.8f, 0.25f);
                        titleNode.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.25f));
                        rarityNode.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.25f));
                        break;
                    default:
                        borderColor = RarityCommon;
                        titleNode.AddThemeColorOverride("font_color", RarityCommon);
                        rarityNode.AddThemeColorOverride("font_color", RarityCommon);
                        break;
                }

                var dimText = new Color(0.6f, 0.5f, 0.5f);
                categoryNode.AddThemeColorOverride("font_color", dimText);
                typeNode.AddThemeColorOverride("font_color", dimText);
                descNode.AddThemeColorOverride("font_color", dimText);
                var hintNode = (Label)vbox.GetNode("Hint");
                hintNode.AddThemeColorOverride("font_color", new Color(0.45f, 0.38f, 0.38f));

                var cardStyle = MakeCardPanel(cardBg, borderColor);
                button.AddThemeStyleboxOverride("normal", cardStyle);

                var hoverBg = new Color(0.25f, 0.18f, 0.18f, 0.95f);
                var hoverBorder = ColorUtils.Lighten(borderColor, 0.2f);
                var cardHoverStyle = MakeCardPanel(hoverBg, hoverBorder);
                button.AddThemeStyleboxOverride("hover", cardHoverStyle);
            }
            else
            {
                _weaponButtons[i].Visible = false;
            }
        }

        _weaponSelectionPanel.Visible = true;
    }

    public void HideWeaponSelection()
    {
        _weaponSelectionPanel.Visible = false;
        _HideTooltip();
        _pauseLockedByUpgrade = false;
        if (!_userPaused)
            GetTree().Paused = false;
        else if (_pausePanel != null)
            _pausePanel.Visible = true;
    }

    private void _UpdateCurrentWeaponLabel(Player player)
    {
        if (_currentWeaponLabel == null) return;
        string weaponName = "无";
        string weaponTypeText = "";
        var effects = new List<string>();

        foreach (var effect in player.ActiveEffects)
        {
            if (effect.Category == WeaponCategory.Weapon)
            {
                weaponName = effect.CardName;
                weaponTypeText = effect.GetWeaponTypeText();
                foreach (var m in effect.MeleeEffects)
                {
                    var level = effect.MeleeEffectLevels.TryGetValue((int)m, out var l) ? l.AsInt32() : 1;
                    effects.Add(WeaponCard.GetMeleeEffectDesc((MeleeEffect)(int)m, level));
                }
                foreach (var r in effect.RangedEffects)
                {
                    var level = effect.RangedEffectLevels.TryGetValue((int)r, out var l) ? l.AsInt32() : 1;
                    effects.Add(WeaponCard.GetRangedEffectDesc((RangedEffect)(int)r, level));
                }
                foreach (var g in effect.MagicEffects)
                {
                    var level = effect.MagicEffectLevels.TryGetValue((int)g, out var l) ? l.AsInt32() : 1;
                    effects.Add(WeaponCard.GetMagicEffectDesc((MagicEffect)(int)g, level));
                }
                break;
            }
        }

        var effectText = effects.Count > 0 ? string.Join(" | ", effects) : "无特效";
        effectText = _TruncateText(effectText, 36);
        if (weaponName == "无")
            _currentWeaponLabel.Text = "当前武器: 无";
        else
            _currentWeaponLabel.Text = $"当前武器: {weaponName} ({weaponTypeText}) | {effectText}";
    }

    private string _TruncateText(string text, int maxLen)
    {
        if (text.Length <= maxLen) return text;
        return text.Substring(0, maxLen - 1) + "…";
    }

    private string _GetRarityText(int rarity) => WeaponFormatter.GetRarityText(rarity);

    private void _OnWeaponButtonPressed(int index)
    {
        if (index < _currentWeaponOptions.Count)
        {
            var selectedWeapon = _currentWeaponOptions[index];
            WeaponCardSelected?.Invoke(selectedWeapon);
            HideWeaponSelection();
            _HideTooltip();

            GameServices.Audio.PlayWeaponSelect();
        }
    }

    private void _OnSkipPressed()
    {
        UpgradeSkipped?.Invoke();
        HideWeaponSelection();
        _HideTooltip();
    }

    private void _OnCardMouseEntered(int index)
    {
        if (index < _currentWeaponOptions.Count)
        {
            var weapon = _currentWeaponOptions[index];
            if (weapon is UpgradeCard upgrade)
                _ShowTooltip(_GetUpgradeTooltipText(upgrade));
            else if (weapon is WeaponCard wc)
                _ShowTooltip(_GetWeaponTooltipText(wc));
        }
    }

    private void _OnCardMouseExited()
    {
        _HideTooltip();
    }

    // ═══════════════════════════════════════════════
    // Tooltip Display
    // ═══════════════════════════════════════════════

    private void _HideTooltip()
    {
        if (_tooltipPanel != null)
            _tooltipPanel.Visible = false;
        if (_tooltipHideTimer != null && !_tooltipHideTimer.IsStopped())
            _tooltipHideTimer.Stop();
    }

    public void ShowTooltip(string text)
    {
        _ShowTooltip(text);
    }

    public void HideTooltip()
    {
        _HideTooltip();
    }

    private void _ShowTooltip(string text)
    {
        if (_tooltipPanel == null || _tooltipLabel == null) return;

        if (_tooltipHideTimer != null && !_tooltipHideTimer.IsStopped())
            _tooltipHideTimer.Stop();

        _tooltipLabel.Text = text;
        _tooltipLabel.ResetSize();

        var screenSize = GetViewport().GetVisibleRect().Size;
        float contentWidth = Mathf.Min(_tooltipMaxWidth, Mathf.Max(260.0f, screenSize.X * 0.32f));
        float contentHeight = _tooltipLabel.GetContentHeight();

        contentHeight = Mathf.Max(contentHeight, 100.0f);

        float panelWidth = contentWidth + 32.0f;
        float panelHeight = contentHeight + 24.0f;

        _tooltipLabel.CustomMinimumSize = new Vector2(contentWidth, contentHeight);
        _tooltipPanel.CustomMinimumSize = new Vector2(panelWidth, panelHeight);
        _tooltipPanel.Size = new Vector2(panelWidth, panelHeight);

        _PositionTooltip();
        _tooltipPanel.Visible = true;
    }

    private void _PositionTooltip()
    {
        var mousePos = GetViewport().GetMousePosition();
        var panelSize = _tooltipPanel.GetCombinedMinimumSize();
        var screenSize = GetViewport().GetVisibleRect().Size;

        var candidates = new List<Vector2>
        {
            mousePos + _tooltipOffset,
            mousePos + new Vector2(_tooltipOffset.X, -panelSize.Y - _tooltipOffset.Y),
            mousePos + new Vector2(-panelSize.X - _tooltipOffset.X, _tooltipOffset.Y),
            mousePos + new Vector2(-panelSize.X - _tooltipOffset.X, -panelSize.Y - _tooltipOffset.Y)
        };

        var bestPos = mousePos + _tooltipOffset;
        float bestDist = Mathf.Inf;
        foreach (var pos in candidates)
        {
            bool inside = pos.X >= 0 && pos.Y >= 0 &&
                          pos.X + panelSize.X <= screenSize.X &&
                          pos.Y + panelSize.Y <= screenSize.Y;
            if (inside)
            {
                var dist = pos.DistanceTo(mousePos);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestPos = pos;
                }
            }
        }

        if (bestDist < Mathf.Inf)
        {
            _tooltipPanel.Position = bestPos;
            return;
        }

        _tooltipPanel.Position = new Vector2(
            Mathf.Clamp(mousePos.X + _tooltipOffset.X, 10, screenSize.X - panelSize.X - 10),
            Mathf.Clamp(mousePos.Y + _tooltipOffset.Y, 10, screenSize.Y - panelSize.Y - 10)
        );
    }

    private void _HideTooltipPanel()
    {
        if (_tooltipPanel != null)
            _tooltipPanel.Visible = false;
    }

    // ═══════════════════════════════════════════════
    // Card Rendering
    // ═══════════════════════════════════════════════

    private void _RenderUpgradeCard(Button button, UpgradeCard upgrade)
    {
        var margin = (MarginContainer)button.GetChild(0);
        var vbox = (VBoxContainer)margin.GetChild(0);
        var categoryNode = (Label)vbox.GetNode("Category");
        var titleNode = (Label)vbox.GetNode("Title");
        var typeNode = (Label)vbox.GetNode("Type");
        var iconNode = vbox.FindChild("Icon", true, false) as Label;
        var descNode = (Label)vbox.GetNode("Desc");
        var rarityNode = (Label)vbox.GetNode("Rarity");

        categoryNode.Text = "【强化卡】";
        titleNode.Text = upgrade.DisplayName;
        typeNode.Text = "强化已有装备";
        if (iconNode != null) iconNode.Text = upgrade.Icon;
        upgrade.SyncDescription();
        descNode.Text = upgrade.Description;
        rarityNode.Text = "升级";

        var upBg = new Color(0.18f, 0.13f, 0.13f, 0.95f);
        var upBorder = RarityRare;
        titleNode.AddThemeColorOverride("font_color", RarityRare);
        rarityNode.AddThemeColorOverride("font_color", RarityRare);

        var style = MakeCardPanel(upBg, upBorder);
        button.AddThemeStyleboxOverride("normal", style);

        var hoverBg = new Color(0.25f, 0.18f, 0.18f, 0.95f);
        var hoverBorder = ColorUtils.Lighten(upBorder, 0.2f);
        var hoverStyle = MakeCardPanel(hoverBg, hoverBorder);
        button.AddThemeStyleboxOverride("hover", hoverStyle);
    }

    // ═══════════════════════════════════════════════
    // Tooltip Text Generators
    // ═══════════════════════════════════════════════

    private string _GetUpgradeTooltipText(UpgradeCard upgrade)
    {
        var bb = "";
        var targetName = upgrade.TargetRef?.CardName ?? "未知";
        var count = upgrade.TargetRef?.UpgradeCount ?? 0;
        bb += $"[center][b][color=#c0392b]{upgrade.DisplayName}[/color][/b][/center]\n";
        bb += $"[center][color=#8a6060]强化目标: {targetName} · 已强化 {count} 次[/color][/center]\n";
        bb += "[center]━━━━━━━━━━━━━━━━━━━━[/center]\n\n";
        bb += "[color=#d47a7a]【强化效果】[/color]\n";

        if (upgrade.DamageDelta > 0)
            bb += $"• 伤害 [color=#d47a7a]+{Mathf.RoundToInt(upgrade.DamageDelta * 100)}%[/color]\n";
        if (upgrade.AttackSpeedDelta > 0)
            bb += $"• 攻速 [color=#d47a7a]+{Mathf.RoundToInt(upgrade.AttackSpeedDelta * 100)}%[/color]\n";
        if (upgrade.HealthDelta > 0)
            bb += $"• 生命 [color=#d47a7a]+{Mathf.RoundToInt(upgrade.HealthDelta)}[/color]\n";
        if (upgrade.SpeedDelta > 0)
            bb += $"• 速度 [color=#d47a7a]+{Mathf.RoundToInt(upgrade.SpeedDelta)}[/color]\n";
        if (upgrade.CritDelta > 0)
            bb += $"• 暴击 [color=#d47a7a]+{Mathf.RoundToInt(upgrade.CritDelta)}%[/color]\n";
        if (upgrade.LifeStealDelta > 0)
            bb += $"• 吸血 [color=#d47a7a]+{Mathf.RoundToInt(upgrade.LifeStealDelta)}%[/color]\n";
        if (upgrade.FreezeDelta > 0)
            bb += $"• 冰冻 [color=#d47a7a]+{Mathf.RoundToInt(upgrade.FreezeDelta)}%[/color]\n";
        if (upgrade.PoisonDelta > 0)
            bb += $"• 中毒 [color=#d47a7a]+{Mathf.RoundToInt(upgrade.PoisonDelta)}%[/color]\n";
        if (upgrade.EffectUpgrades.Count > 0)
            bb += "• [color=#d47a7a]特效等级 +1[/color]\n";

        var tradeoffKind = upgrade.TradeoffKind;
        if (tradeoffKind != UpgradeCard.TradeoffType.None)
        {
            bb += "\n[color=#e74c3c]【副作用】[/color]\n";
            if (tradeoffKind == UpgradeCard.TradeoffType.HealthCost)
                bb += $"• 每次攻击额外消耗 [color=#e74c3c]+{Mathf.RoundToInt(upgrade.TradeoffValue)}[/color] 生命\n";
            else if (tradeoffKind == UpgradeCard.TradeoffType.Speed)
                bb += $"• 速度 [color=#e74c3c]{Mathf.RoundToInt(upgrade.TradeoffValue)}[/color]\n";
            else if (tradeoffKind == UpgradeCard.TradeoffType.HealthMax)
                bb += $"• 生命上限 [color=#e74c3c]{Mathf.RoundToInt(upgrade.TradeoffValue)}[/color]\n";
        }
        bb += "\n[color=#8a6060]选择后将直接强化你已有的装备。[/color]";
        return bb;
    }

    private string _GetWeaponTooltipText(WeaponCard weapon)
    {
        var bb = "";

        var rarityColor = _GetRarityColor(weapon.Rarity);
        bb += $"[center][b][color={rarityColor}]{weapon.CardName}[/color][/b][/center]\n";

        string typeText = weapon.Category == WeaponCategory.Weapon
            ? weapon.GetWeaponTypeText() + "武器"
            : "装备";
        typeText += $" · Lv.{weapon.Tier} · {_GetRarityText(weapon.Rarity)}";
        bb += $"[center][color=#8a6060]{typeText}[/color][/center]\n";
        bb += "[center]━━━━━━━━━━━━━━━━━━━━[/center]\n\n";

        Player? player = _gameManager?.PlayerRef;

        bb += "[color=#e0b060]【属性效果】[/color]\n";
        bool hasStats = false;

        if (!Mathf.IsEqualApprox(weapon.DamageModifier, 1.0f))
        {
            int mod = Mathf.RoundToInt((weapon.DamageModifier - 1.0f) * 100);
            string color = mod > 0 ? "#d47a7a" : "#e74c3c";
            bb += $"  [color={color}]● 伤害: {(mod > 0 ? "+" : "")}{mod}%[/color]";
            if (player != null)
            {
                int currentVal = Mathf.RoundToInt(player.CurrentDamage);
                int newVal = Mathf.RoundToInt(player.CurrentDamage * weapon.DamageModifier);
                bb += $"  [color=#5a4040]({currentVal} → {newVal})[/color]";
            }
            bb += "\n";
            hasStats = true;
        }

        if (!Mathf.IsEqualApprox(weapon.AttackSpeedModifier, 1.0f))
        {
            int mod = Mathf.RoundToInt((weapon.AttackSpeedModifier - 1.0f) * 100);
            string color = mod > 0 ? "#d47a7a" : "#e74c3c";
            bb += $"  [color={color}]● 攻速: {(mod > 0 ? "+" : "")}{mod}%[/color]\n";
            hasStats = true;
        }

        if (weapon.Category == WeaponCategory.Weapon)
        {
            float attackInterval = 0.5f / weapon.AttackSpeedModifier;
            if (weapon.Cooldown > 0)
                attackInterval = Mathf.Max(attackInterval, (float)weapon.Cooldown);
            bb += $"  [color=#d4a574]● 攻击间隔: {Mathf.Snapped(attackInterval, 0.01f)}秒[/color]";
            if (player != null)
                bb += $"  [color=#5a4040](当前: {Mathf.Snapped(player.AttackCooldown, 0.01f)}秒)[/color]";
            bb += "\n";
            hasStats = true;
        }

        if (!Mathf.IsEqualApprox(weapon.HealthModifier, 0))
        {
            string color = weapon.HealthModifier > 0 ? "#d47a7a" : "#e74c3c";
            bb += $"  [color={color}]● 生命上限: {(weapon.HealthModifier > 0 ? "+" : "")}{Mathf.RoundToInt(weapon.HealthModifier)}[/color]";
            if (player != null)
            {
                int currentVal = player.MaxHealth;
                int newVal = currentVal + Mathf.RoundToInt(weapon.HealthModifier);
                bb += $"  [color=#5a4040]({currentVal} → {newVal})[/color]";
            }
            bb += "\n";
            hasStats = true;
        }

        if (!Mathf.IsEqualApprox(weapon.SpeedModifier, 0))
        {
            string color = weapon.SpeedModifier > 0 ? "#d47a7a" : "#e74c3c";
            bb += $"  [color={color}]● 移动速度: {(weapon.SpeedModifier > 0 ? "+" : "")}{Mathf.RoundToInt(weapon.SpeedModifier)}[/color]";
            if (player != null)
            {
                int currentVal = Mathf.RoundToInt(player.CurrentSpeed);
                int newVal = currentVal + Mathf.RoundToInt(weapon.SpeedModifier);
                bb += $"  [color=#5a4040]({currentVal} → {newVal})[/color]";
            }
            bb += "\n";
            hasStats = true;
        }

        if (weapon.CriticalBonus > 0)
        {
            bb += $"  [color=#e0a040]● 暴击几率: +{Mathf.RoundToInt(weapon.CriticalBonus)}%[/color]";
            if (player != null)
            {
                int currentVal = Mathf.RoundToInt(player.CriticalChance);
                int newVal = currentVal + Mathf.RoundToInt(weapon.CriticalBonus);
                bb += $"  [color=#5a4040]({currentVal}% → {newVal}%)[/color]";
            }
            bb += "\n";
            hasStats = true;
        }

        if (weapon.LifeStealBonus > 0)
        {
            bb += $"  [color=#c07979]● 吸血比例: +{Mathf.RoundToInt(weapon.LifeStealBonus)}%[/color]";
            if (player != null)
            {
                int currentVal = Mathf.RoundToInt(player.LifeStealPercent);
                int newVal = currentVal + Mathf.RoundToInt(weapon.LifeStealBonus);
                bb += $"  [color=#5a4040]({currentVal}% → {newVal}%)[/color]";
            }
            bb += "\n";
            hasStats = true;
        }

        if (weapon.FreezeBonus > 0)
        {
            bb += $"  [color=#a08070]● 冰冻几率: +{Mathf.RoundToInt(weapon.FreezeBonus)}%[/color]";
            if (player != null)
            {
                int currentVal = Mathf.RoundToInt(player.FreezeDuration);
                int newVal = currentVal + Mathf.RoundToInt(weapon.FreezeBonus);
                bb += $"  [color=#5a4040]({currentVal}% → {newVal}%)[/color]";
            }
            bb += "\n";
            hasStats = true;
        }

        if (weapon.PoisonBonus > 0)
        {
            bb += $"  [color=#a09070]● 中毒几率: +{Mathf.RoundToInt(weapon.PoisonBonus)}%[/color]";
            if (player != null)
            {
                int currentVal = Mathf.RoundToInt(player.PoisonDamage);
                int newVal = currentVal + Mathf.RoundToInt(weapon.PoisonBonus);
                bb += $"  [color=#5a4040]({currentVal}% → {newVal}%)[/color]";
            }
            bb += "\n";
            hasStats = true;
        }

        if (weapon.GrantsAura)
        {
            if (player?.Aura != null && player.Aura.Active)
                bb += "  [color=#b09080]● 护盾领域: 升级 (伤害+3, 范围+15)[/color]\n";
            else
                bb += "  [color=#b09080]● 护盾领域: 启用[/color]\n";
            hasStats = true;
        }

        if (!hasStats)
            bb += "  [color=#5a4040]● 无属性变化[/color]\n";

        bb += "\n";

        // Effects
        bool hasEffects = weapon.MeleeEffects.Count > 0 || weapon.RangedEffects.Count > 0 || weapon.MagicEffects.Count > 0;

        if (hasEffects)
        {
            bb += "[b][color=#b09080]【特殊效果】[/color][/b]\n";
            foreach (var effect in weapon.MeleeEffects)
            {
                int key = (int)effect;
                int level = weapon.MeleeEffectLevels.TryGetValue(key, out var l) ? l.AsInt32() : 1;
                bb += $"  [color=#ddaaff]★ {WeaponCard.GetMeleeEffectDesc((MeleeEffect)key, level)}[/color]\n";
                string detail = weapon.GetEffectDetail(((MeleeEffect)key).ToString(), "melee", level);
                if (!string.IsNullOrEmpty(detail))
                    bb += $"     [color=#999999]{detail}[/color]\n";
            }
            foreach (var effect in weapon.RangedEffects)
            {
                int key = (int)effect;
                int level = weapon.RangedEffectLevels.TryGetValue(key, out var l) ? l.AsInt32() : 1;
                bb += $"  [color=#ddaaff]★ {WeaponCard.GetRangedEffectDesc((RangedEffect)key, level)}[/color]\n";
                string detail = weapon.GetEffectDetail(((RangedEffect)key).ToString(), "ranged", level);
                if (!string.IsNullOrEmpty(detail))
                    bb += $"     [color=#999999]{detail}[/color]\n";
            }
            foreach (var effect in weapon.MagicEffects)
            {
                int key = (int)effect;
                int level = weapon.MagicEffectLevels.TryGetValue(key, out var l) ? l.AsInt32() : 1;
                bb += $"  [color=#ddaaff]★ {WeaponCard.GetMagicEffectDesc((MagicEffect)key, level)}[/color]\n";
                string detail = weapon.GetEffectDetail(((MagicEffect)key).ToString(), "magic", level);
                if (!string.IsNullOrEmpty(detail))
                    bb += $"     [color=#999999]{detail}[/color]\n";
            }
            bb += "\n";
        }

        // Risk section
        bool hasRisk = weapon.HealthCost > 0 || weapon.HealthModifier < 0 ||
                       weapon.SpeedModifier < 0 || weapon.DamageModifier < 1.0f ||
                       weapon.AttackSpeedModifier < 1.0f;
        if (hasRisk)
        {
            bb += "[color=#ff6666]【⚠ 风险提示】[/color]\n";
            if (weapon.HealthCost > 0)
                bb += $"  [color=#e74c3c]● 每次攻击消耗 {weapon.HealthCost} 点生命[/color]\n";
            if (weapon.HealthModifier < 0)
                bb += $"  [color=#e74c3c]● 生命上限降低 {Mathf.Abs(Mathf.RoundToInt(weapon.HealthModifier))} 点[/color]\n";
            if (weapon.SpeedModifier < 0)
                bb += $"  [color=#e74c3c]● 移动速度降低 {Mathf.Abs(Mathf.RoundToInt(weapon.SpeedModifier))} 点[/color]\n";
            if (weapon.DamageModifier < 1.0f)
                bb += $"  [color=#e74c3c]● 伤害降低 {Mathf.RoundToInt((1.0f - weapon.DamageModifier) * 100)}%[/color]\n";
            if (weapon.AttackSpeedModifier < 1.0f)
                bb += $"  [color=#e74c3c]● 攻击速度降低 {Mathf.RoundToInt((1.0f - weapon.AttackSpeedModifier) * 100)}%[/color]\n";
            bb += "\n";
        }

        // Replacement hint
        if (weapon.Category == WeaponCategory.Weapon && player != null)
        {
            string currentWeaponName = "";
            foreach (var eff in player.ActiveEffects)
            {
                if (eff.Category == WeaponCategory.Weapon)
                {
                    currentWeaponName = eff.CardName;
                    break;
                }
            }
            if (!string.IsNullOrEmpty(currentWeaponName) && currentWeaponName != weapon.CardName)
                bb += $"[color=#e0a040]⚔ 将替换当前武器: {currentWeaponName}[/color]\n";
        }

        return bb;
    }

    private string _GetRarityColor(int rarity) => WeaponFormatter.GetRarityColor(rarity);

    // ═══════════════════════════════════════════════
    // Viewport Resize
    // ═══════════════════════════════════════════════

    private void _OnUiViewportSizeChanged()
    {
        var viewportSize = GetViewport().GetVisibleRect().Size;
        _roundBanner?.OnViewportSizeChanged(viewportSize);
    }

    // ═══════════════════════════════════════════════
    // Game Manager Events
    // ═══════════════════════════════════════════════

    private void _OnWeaponSelectionStarted() => ShowWeaponSelection();

    private void _OnRoundStarted(int roundNumber)
    {
        UpdateRoundDisplay(roundNumber);
    }

    public void UpdateRoundDisplay(int roundNumber)
    {
        _hudPanel?.UpdateRound(roundNumber);
    }

    public void ShowRoundBanner(string text, bool persistent = false)
    {
        _roundBanner?.Show(text, persistent);
    }

    public void HideRoundBanner()
    {
        _roundBanner?.Hide();
    }
    public void ShowRoundSummary(int round, int kills, int totalKills, int score)
    {
        HideRoundBanner();

        if (_roundSummaryPanel != null && IsInstanceValid(_roundSummaryPanel))
            _roundSummaryPanel.QueueFree();

        // Wrapper fills the screen so inner panel centers correctly
        _roundSummaryPanel = new Panel();
        _roundSummaryPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _roundSummaryPanel.ZIndex = 55;
        _roundSummaryPanel.MouseFilter = Control.MouseFilterEnum.Ignore;

        var bgStyle = new StyleBoxFlat
        {
            BgColor = new Color(BattlefieldBg, 0.6f)
        };
        _roundSummaryPanel.AddThemeStyleboxOverride("panel", bgStyle);
        AddChild(_roundSummaryPanel);

        // CenterContainer ensures card is always at screen center
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        center.MouseFilter = Control.MouseFilterEnum.Ignore;
        _roundSummaryPanel.AddChild(center);

        // The actual card
        var card = new Panel();
        card.CustomMinimumSize = new Vector2(340, 260);
        card.MouseFilter = Control.MouseFilterEnum.Ignore;

        var cardStyle = MakeNotePanel(borderRadius: 8f, shadowSize: 8f);
        cardStyle.ShadowOffset = new Vector2(4, 4);
        card.AddThemeStyleboxOverride("panel", cardStyle);
        center.AddChild(card);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        margin.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
        vbox.MouseFilter = Control.MouseFilterEnum.Ignore;
        margin.AddChild(vbox);

        // Title
        var titleLbl = new Label
        {
            Text = $"第 {round} 回合完成",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        titleLbl.AddThemeFontSizeOverride("font_size", 26);
        titleLbl.AddThemeColorOverride("font_color", BloodBright);
        titleLbl.AddThemeConstantOverride("outline_size", 3);
        titleLbl.AddThemeColorOverride("font_outline_color", InkBlack);
        vbox.AddChild(titleLbl);

        // Subtitle
        var subLbl = new Label
        {
            Text = "ROUND COMPLETE",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        subLbl.AddThemeFontSizeOverride("font_size", 11);
        subLbl.AddThemeColorOverride("font_color", new Color(0.6f, 0.55f, 0.40f));
        vbox.AddChild(subLbl);

        // Separator
        var sep = new HSeparator();
        sep.AddThemeColorOverride("separator", new Color(0.85f, 0.70f, 0.25f, 0.3f));
        sep.CustomMinimumSize = new Vector2(0, 6);
        vbox.AddChild(sep);

        // Spacer
        var sp1 = new Control { CustomMinimumSize = new Vector2(0, 4), MouseFilter = Control.MouseFilterEnum.Ignore };
        vbox.AddChild(sp1);

        _AddStatRow(vbox, "本波击杀", kills.ToString(), new Color(0.8f, 0.5f, 0.4f));
        _AddStatRow(vbox, "累计击杀", totalKills.ToString(), BloodRed);
        _AddStatRow(vbox, "当前得分", score.ToString(), new Color(0.85f, 0.6f, 0.45f));

        // Spacer
        var sp2 = new Control { CustomMinimumSize = new Vector2(0, 6), MouseFilter = Control.MouseFilterEnum.Ignore };
        vbox.AddChild(sp2);

        // Bottom hint
        var hint = new Label
        {
            Text = "准备进入下一波...",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        hint.AddThemeFontSizeOverride("font_size", 12);
        hint.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.55f));
        vbox.AddChild(hint);

        // Animate in
        card.Modulate = card.Modulate with { A = 0.0f };
        card.Scale = new Vector2(0.7f, 0.7f);
        var tween = CreateTween();
        tween.Parallel().TweenProperty(card, Control.PropertyName.Modulate.ToString() + ":a", 1.0f, 0.4f)
            .SetTrans(Tween.TransitionType.Cubic);
        tween.Parallel().TweenProperty(card, "scale", new Vector2(1.0f, 1.0f), 0.4f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        // Dim background in
        _roundSummaryPanel.Modulate = _roundSummaryPanel.Modulate with { A = 0.0f };
        tween.Parallel().TweenProperty(_roundSummaryPanel, Control.PropertyName.Modulate.ToString() + ":a", 1.0f, 0.3f);
    }

    private void _AddStatRow(VBoxContainer parent, string label, string value, Color accentColor)
    {
        var row = new HBoxContainer();
        row.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddThemeConstantOverride("separation", 0);
        parent.AddChild(row);

        // Label (left-aligned, takes half width)
        var lbl = new Label
        {
            Text = label,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        lbl.AddThemeFontSizeOverride("font_size", 16);
        lbl.AddThemeColorOverride("font_color", new Color(0.65f, 0.65f, 0.70f));
        lbl.AddThemeConstantOverride("outline_size", 1);
        lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.5f));
        row.AddChild(lbl);

        // Value (right-aligned, takes half width)
        var val = new Label
        {
            Text = value,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        val.AddThemeFontSizeOverride("font_size", 20);
        val.AddThemeColorOverride("font_color", accentColor);
        val.AddThemeConstantOverride("outline_size", 2);
        val.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.6f));
        row.AddChild(val);
    }

    public void HideRoundSummary()
    {
        if (_roundSummaryPanel == null || !IsInstanceValid(_roundSummaryPanel)) return;

        var panel = _roundSummaryPanel;
        var tween = CreateTween();
        tween.Parallel().TweenProperty(panel, Control.PropertyName.Modulate.ToString() + ":a", 0.0f, 0.3f);
        tween.TweenCallback(Callable.From(() =>
        {
            if (IsInstanceValid(panel)) panel.QueueFree();
        }));
        _roundSummaryPanel = null;
    }


    public void UpdateScoreDisplay(int newScore)
    {
        _hudPanel?.UpdateScore(newScore);
    }

    public void UpdateHealthDisplay(int currentHealth, int maxHealth)
    {
        if (_hudPanel == null) return;
        _hudPanel.UpdateHealth(currentHealth, maxHealth);
        float healthPercent = (float)currentHealth / maxHealth;
        UpdateLowHpVignette(healthPercent);
    }

    public void UpdateZombiesDisplay(int zombiesRemaining, int totalZombies)
    {
        _hudPanel?.UpdateZombies(zombiesRemaining, totalZombies);
    }

    public void UpdatePlayerStatus(Player player)
    {
        _hudPanel?.UpdatePlayerStatus(player);
    }

    // ═══════════════════════════════════════════════
    // Public API / Delegation
    // ═══════════════════════════════════════════════

    public void SetGameManager(GameManager manager)
    {
        _gameManager = manager;
        _SetupConnections();
    }

    public void SetWeaponDeck(WeaponDeck deck) => _weaponDeck = deck;

    public void ShowGameOver()
    {
        HideRoundBanner();
        HideRoundSummary();
        if (_weaponSelectionPanel != null && _weaponSelectionPanel.Visible)
            HideWeaponSelection();

        // Pause game to prevent input during transition
        GetTree().Paused = true;

        _gameOverUi.Setup(_gameManager);
        _gameOverUi.ShowGameOver(_hudPanel);
    }

    public void ShowDamageNumber(Vector2 pos, int amount, bool isCritical = false)
    {
        _screenFx?.ShowDamageNumber(pos, amount, isCritical);
    }

    public void FlashDamage()
    {
        _screenFx?.FlashDamage();
    }

    public void FlashWarning()
    {
        _screenFx?.FlashWarning();
    }

    public void ShakeScreen(float intensity)
    {
        _screenFx?.ShakeScreen(intensity);
    }

    public void FlashAttackColor(Color attackColor)
    {
        _screenFx?.FlashAttackColor(attackColor);
    }

    public void AddCombo()
    {
        _screenFx?.AddCombo();
    }

    public void ShowKillEffect(Vector2 pos, int points)
    {
        _screenFx?.ShowKillEffect(pos, points);
    }

    public void UpdateLowHpVignette(float healthPercent)
    {
        _screenFx?.UpdateLowHpVignette(healthPercent);
    }

    public void ShowOffscreenIndicator(Vector2 direction, float dangerLevel)
    {
        _screenFx?.ShowOffscreenIndicator(direction, dangerLevel);
    }

    /// <summary>显示升级/强化通知</summary>
    public void ShowLevelUpNotification(string text, Color color)
    {
        if (_levelUpPanel == null)
        {
            _levelUpPanel = new Panel
            {
                Visible = true,
                ZIndex = 60,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _levelUpPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
            _levelUpPanel.OffsetTop = 90;
            _levelUpPanel.OffsetBottom = 130;
            var style = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0) };
            _levelUpPanel.AddThemeStyleboxOverride("panel", style);
            AddChild(_levelUpPanel);

            var center = new CenterContainer();
            center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _levelUpPanel.AddChild(center);

            _levelUpLabel = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _levelUpLabel.AddThemeFontSizeOverride("font_size", 22);
            _levelUpLabel.AddThemeConstantOverride("outline_size", 4);
            _levelUpLabel.AddThemeColorOverride("font_outline_color", InkBlack);
            center.AddChild(_levelUpLabel);
        }

        if (_levelUpTween != null && _levelUpTween.IsRunning())
            _levelUpTween.Kill();

        _levelUpLabel!.Text = text;
        _levelUpLabel.AddThemeColorOverride("font_color", color);
        _levelUpPanel!.Visible = true;
        _levelUpPanel.Modulate = _levelUpPanel.Modulate with { A = 0.0f };
        _levelUpPanel.Scale = new Vector2(0.5f, 0.5f);

        _levelUpTween = CreateTween();
        _levelUpTween.SetParallel(true);
        _levelUpTween.TweenProperty(_levelUpPanel, Control.PropertyName.Modulate.ToString() + ":a", 1.0f, 0.2f);
        _levelUpTween.TweenProperty(_levelUpPanel, "scale", new Vector2(1.1f, 1.1f), 0.2f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _levelUpTween.TweenProperty(_levelUpPanel, "scale", new Vector2(1.0f, 1.0f), 0.1f);
        _levelUpTween.TweenInterval(1.2f);
        _levelUpTween.SetParallel(true);
        _levelUpTween.TweenProperty(_levelUpPanel, Control.PropertyName.Modulate.ToString() + ":a", 0.0f, 0.4f);
        _levelUpTween.SetParallel(false);
        _levelUpTween.TweenCallback(Callable.From(() =>
        {
            if (_levelUpPanel != null) _levelUpPanel.Visible = false;
        }));
    }

    /// <summary>显示连击计数</summary>
    public void ShowComboNotification(int comboCount)
    {
        if (comboCount < 3) return;
        ShowLevelUpNotification($"连击 x{comboCount}", new Color(1.0f, 0.6f, 0.2f));
    }

    /// <summary>显示目标切换提示</summary>
    public void ShowTargetSwitchNotification(int targetPriority)
    {
        string[] names = { "最近", "最远", "密集" };
        string name = targetPriority >= 0 && targetPriority < names.Length ? names[targetPriority] : "最近";
        ShowLevelUpNotification($"目标: {name}", new Color(0.5f, 0.8f, 0.9f));
    }

    private void _ShowAchievementNotification(string text, Color color)
    {
        ShowLevelUpNotification(text, color);
    }

    private void _ShowMilestoneNotification(string text, Color color)
    {
        ShowLevelUpNotification(text, color);
    }

    /// <summary>显示天气变化通知 (由 WeatherSystem 调用)</summary>
    public void ShowWeatherNotification(string weatherName, Color weatherColor)
    {
        ShowLevelUpNotification($"天气: {weatherName}", weatherColor);
    }

    public void TriggerBossAtmosphere()
    {
        _screenFx?.TriggerBossAtmosphere();
    }

    public bool GetWelcomeVisible() => _welcomePanel != null && _welcomePanel.Visible;

    public Control GetHudPanel() => _hudPanel!;
}
