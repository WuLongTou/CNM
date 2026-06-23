namespace ZombieWars.UI;

using Godot;
using System;
using System.Collections.Generic;
using ZombieWars.Core;
using ZombieWars.Weapons;
using ZombieWars.World;

using Player = ZombieWars.Player.Player;
using static ZombieWars.Core.GameConfig;
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
    private WeaponSelectionPanel _weaponSelectionPanel = null!;
    private HudPanel? _hudPanel;
    private HelpPanel _helpPanel = null!;
    private EquipmentPanel _equipmentPanel = null!;
    private InventoryPanel? _inventoryPanel;
    private CraftingPanel? _craftingPanel;
    private MetaUpgradePanel? _metaPanel;  // 跨局永久升级面板
    private FusionOfferPanel? _fusionPanel;  // 武器融合确认面板
    private WelcomePanel? _welcomePanel;
    private ProgressBar _bossHealthBar = null!;
    private Label _bossHealthLabel = null!;
    private bool _bossUiVisible;
    private int _bossUiHealth = -1;
    private int _bossUiMaxHealth = -1;
    private bool _gameStarted;

    // ═══════════════════════════════════════════════
    // Weapon Selection
    // ═══════════════════════════════════════════════
    private GameManager? _gameManager;
    private WeaponDeck? _weaponDeck;

    // ═══════════════════════════════════════════════
    // Tooltip（委托给 TooltipSystem）
    // ═══════════════════════════════════════════════
    private TooltipSystem _tooltipSystem = null!;

    // ═══════════════════════════════════════════════
    // Pause
    // ═══════════════════════════════════════════════
    private PausePanel? _pausePanel;
    private SettingsPanel? _settingsPanel;
    private bool _pauseLockedByUpgrade;
    private bool _userPaused;
    private float _hazardTimer;  // 陷阱 HUD 提示残留时间（玩家离开陷阱区后倒计时清除）
    private RoundBanner? _roundBanner;
    private Panel? _roundSummaryPanel;

    // ═══════════════════════════════════════════════
    // Notifications（委托给 NotificationPanel）
    // ═══════════════════════════════════════════════
    // ═══ PackedScene 导出（编辑器中可替换 .tscn；运行时 Instantiate）══
    [Export] public PackedScene? HudPanelScene { get; set; } = GD.Load<PackedScene>("res://scenes/HudPanel.tscn");
    [Export] public PackedScene? GameOverUIScene { get; set; } = GD.Load<PackedScene>("res://scenes/GameOverUI.tscn");
    [Export] public PackedScene? WeaponSelectionPanelScene { get; set; } = GD.Load<PackedScene>("res://scenes/WeaponSelectionPanel.tscn");
    [Export] public PackedScene? HelpPanelScene { get; set; } = GD.Load<PackedScene>("res://scenes/HelpPanel.tscn");
    [Export] public PackedScene? PausePanelScene { get; set; } = GD.Load<PackedScene>("res://scenes/PausePanel.tscn");
    [Export] public PackedScene? SettingsPanelScene { get; set; } = GD.Load<PackedScene>("res://scenes/SettingsPanel.tscn");

    private NotificationPanel _notificationPanel = null!;

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

        _gameOverUi = GameOverUIScene!.Instantiate<GameOverUI>();
        _gameOverUi.Name = "GameOverUI";
        AddChild(_gameOverUi);

        // Setup achievement system callbacks
        Core.AchievementSystem.OnAchievementUnlocked += _ShowAchievementNotification;
        Core.AchievementSystem.OnMilestoneReached += _ShowMilestoneNotification;

        // 构造各 UI 子面板（HudPanel/GameOverUI/WeaponSelection/Help/Pause 走 .tscn 加载，其余仍代码创建）
        _hudPanel = HudPanelScene!.Instantiate<HudPanel>();
        _hudPanel.Setup(
            onToggleSound: _ToggleSound,
            onToggleHelp: _ToggleControlsPanel,
            onToggleEquipment: _ToggleEquipmentPanel,
            showTooltip: _ShowTooltip,
            hideTooltip: _HideTooltip);
        AddChild(_hudPanel);
        _CreateBossHealthBar();

        _weaponSelectionPanel = WeaponSelectionPanelScene!.Instantiate<WeaponSelectionPanel>();
        _weaponSelectionPanel.Name = "WeaponSelectionPanel";
        _weaponSelectionPanel.ZIndex = 160;
        AddChild(_weaponSelectionPanel);
        // 转发 WeaponSelectionPanel 事件到 UIManager 同名事件（GameManager 订阅的是后者）
        _weaponSelectionPanel.WeaponCardSelected += (card) => WeaponCardSelected?.Invoke(card);
        _weaponSelectionPanel.UpgradeSkipped += () => UpgradeSkipped?.Invoke();
        _helpPanel = HelpPanelScene!.Instantiate<HelpPanel>();
        _helpPanel.Name = "HelpPanel";
        _helpPanel.ZIndex = 170;
        AddChild(_helpPanel);
        _equipmentPanel = new EquipmentPanel { Name = "EquipmentPanel", ZIndex = 170 };
        _equipmentPanel.Setup(showTooltip: _ShowTooltip, hideTooltip: _HideTooltip);
        AddChild(_equipmentPanel);
        _inventoryPanel = new InventoryPanel { Name = "InventoryPanel", ZIndex = 170 };
        AddChild(_inventoryPanel);
        _craftingPanel = new CraftingPanel { Name = "CraftingPanel", ZIndex = 170 };
        AddChild(_craftingPanel);
        // _weaponSelectionPanel.Setup 延迟到 SetWeaponDeck 时调用（此时 _gameManager/_weaponDeck 已就位）
        _metaPanel = new MetaUpgradePanel { Name = "MetaUpgradePanel" };
        AddChild(_metaPanel);
        _fusionPanel = new FusionOfferPanel { Name = "FusionOfferPanel" };
        AddChild(_fusionPanel);
        _welcomePanel = new WelcomePanel();
        _welcomePanel.ZIndex = 160;
        _welcomePanel.Setup(onStart: _StartGame, onToggleHelp: _ToggleControlsPanel);
        AddChild(_welcomePanel);
        _roundBanner = new RoundBanner();
        AddChild(_roundBanner);

        // 数学系统：波形共振指示器
        _CreateResonanceIndicator();

        _SetupConnections();
        _tooltipSystem = new TooltipSystem { Name = "TooltipSystem" };
        AddChild(_tooltipSystem);
        _notificationPanel = new NotificationPanel { Name = "NotificationPanel" };
        AddChild(_notificationPanel);
        _pausePanel = PausePanelScene!.Instantiate<PausePanel>();
        _pausePanel.ResumeRequested += () => SetUserPaused(false);
        _pausePanel.SettingsRequested += ShowSettings;
        AddChild(_pausePanel);
        _settingsPanel = SettingsPanelScene!.Instantiate<SettingsPanel>();
        _settingsPanel.CloseRequested += _OnSettingsClosed;
        AddChild(_settingsPanel);
        _SetGameplayHudVisible(false);
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


    // ═══════════════════════════════════════════════
    // 数学系统 UI
    // ═══════════════════════════════════════════════

    private ResonanceIndicator? _resonanceIndicator;
    private Label? _fieldChargeLabel;
    private Label? _strategyLabel;
    private Label? _fieldHintLabel;

    private void _CreateResonanceIndicator()
    {
        if (!Core.GameConfig.MathSystemsEnabled) return;
        var viewport = ResponsiveUi.ViewportSize(this);
        _resonanceIndicator = new ResonanceIndicator { Name = "ResonanceIndicator", MouseFilter = MouseFilterEnum.Ignore };
        _resonanceIndicator.SetAnchorsPreset(LayoutPreset.TopRight);
        _resonanceIndicator.OffsetLeft = -ResponsiveUi.Px(210, viewport, 150);
        _resonanceIndicator.OffsetTop = ResponsiveUi.Px(58, viewport, 42);
        _resonanceIndicator.OffsetRight = -ResponsiveUi.Px(12, viewport, 6);
        _resonanceIndicator.OffsetBottom = _resonanceIndicator.OffsetTop + ResponsiveUi.Px(72, viewport, 52);
        AddChild(_resonanceIndicator);
    }

    public void BindWaveSystem(ZombieWars.Math.WaveResonanceSystem? waveSystem)
    {
        if (_resonanceIndicator != null && waveSystem != null)
            _resonanceIndicator.Bind(waveSystem);
    }

    public void UpdateWeaponFrequency(float freq) => _resonanceIndicator?.SetWeaponFrequency(freq);

    public void UpdateFieldCharges(int available, int maxSlots)
    {
        if (!Core.GameConfig.MathSystemsEnabled) return;
        if (_fieldChargeLabel == null)
        {
            var viewport = ResponsiveUi.ViewportSize(this);
            _fieldChargeLabel = new Label { Name = "FieldChargeLabel" };
            _fieldChargeLabel.SetAnchorsPreset(LayoutPreset.BottomLeft);
            _fieldChargeLabel.OffsetLeft = ResponsiveUi.Px(12, viewport, 6);
            _fieldChargeLabel.OffsetBottom = -ResponsiveUi.Px(94, viewport, 70);
            _fieldChargeLabel.OffsetTop = -ResponsiveUi.Px(122, viewport, 96);
            _fieldChargeLabel.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(13, viewport, 9));
            _fieldChargeLabel.AddThemeColorOverride("font_color", BloodBright);
            _fieldChargeLabel.AddThemeColorOverride("font_outline_color", InkBlack);
            _fieldChargeLabel.AddThemeConstantOverride("outline_size", 3);
            AddChild(_fieldChargeLabel);
        }
        _fieldChargeLabel.Text = maxSlots <= 0
            ? ""
            : $"力场: {available}/{maxSlots}  `放置  [+]吸引 [x]排斥 [o]旋涡";
    }

    public void UpdateStrategyIndicator(ZombieWars.Math.StrategySystem? strategySys)
    {
        if (!Core.GameConfig.MathSystemsEnabled || strategySys == null) return;
        if (_strategyLabel == null)
        {
            var viewport = ResponsiveUi.ViewportSize(this);
            _strategyLabel = new Label { Name = "StrategyLabel" };
            _strategyLabel.SetAnchorsPreset(LayoutPreset.BottomLeft);
            _strategyLabel.OffsetLeft = ResponsiveUi.Px(12, viewport, 6);
            _strategyLabel.OffsetBottom = -ResponsiveUi.Px(124, viewport, 96);
            _strategyLabel.OffsetTop = -ResponsiveUi.Px(150, viewport, 116);
            _strategyLabel.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(11, viewport, 9));
            _strategyLabel.AddThemeColorOverride("font_color", NetherPurple);
            _strategyLabel.AddThemeColorOverride("font_outline_color", InkBlack);
            _strategyLabel.AddThemeConstantOverride("outline_size", 2);
            AddChild(_strategyLabel);
        }
        if (!ZombieWars.Math.StrategySystem.IsStrategyActive(strategySys.CurrentRound)) { _strategyLabel.Text = ""; return; }
        string desc = strategySys.GetCurrentProfile() switch
        {
            Core.GameEnums.StrategyProfile.MeleeDominant => "敌人适应: 你偏近战，坦克僵尸变多",
            Core.GameEnums.StrategyProfile.RangedDominant => "敌人适应: 你偏远程，蜂群僵尸变多",
            Core.GameEnums.StrategyProfile.MagicDominant => "敌人适应: 你偏魔法，远程僵尸变多",
            Core.GameEnums.StrategyProfile.Balanced => "敌人适应: 你的流派均衡",
            _ => ""
        };
        _strategyLabel.Text = $"[策] {desc}  强度 {strategySys.AdaptationRate:F0%}";
    }

    public void ShowFieldPlacementHint(bool show, Core.GameEnums.FieldType? type)
    {
        if (_fieldHintLabel == null)
        {
            var viewport = ResponsiveUi.ViewportSize(this);
            _fieldHintLabel = new Label { Name = "FieldHintLabel" };
            _fieldHintLabel.SetAnchorsPreset(LayoutPreset.CenterBottom);
            _fieldHintLabel.OffsetBottom = -ResponsiveUi.Px(120, viewport, 82);
            _fieldHintLabel.OffsetTop = -ResponsiveUi.Px(160, viewport, 116);
            _fieldHintLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _fieldHintLabel.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(16, viewport, 11));
            _fieldHintLabel.AddThemeColorOverride("font_color", CandleGlow);
            _fieldHintLabel.AddThemeColorOverride("font_outline_color", InkBlack);
            _fieldHintLabel.AddThemeConstantOverride("outline_size", 3);
            AddChild(_fieldHintLabel);
        }
        if (!show) { _fieldHintLabel.Visible = false; return; }
        string typeDesc = type switch
        {
            Core.GameEnums.FieldType.Attractor => "[+] 吸引（拉拢僵尸）",
            Core.GameEnums.FieldType.Repulsor => "[x] 排斥（推开僵尸）",
            Core.GameEnums.FieldType.Vortex => "[o] 旋涡（环绕陷阱）",
            _ => ""
        };
        _fieldHintLabel.Text = $"{typeDesc}\n左键放置力场 · 右键或 ` 取消 · 1/2/3 切换力场类型";
        _fieldHintLabel.Visible = true;
    }

    private void _CreateBossHealthBar()
    {
        var viewport = ResponsiveUi.ViewportSize(this);
        _bossHealthBar = new ProgressBar
        {
            ShowPercentage = false
        };
        _bossHealthBar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        _bossHealthBar.OffsetTop = ResponsiveUi.Px(66, viewport, 46);
        _bossHealthBar.OffsetBottom = _bossHealthBar.OffsetTop + ResponsiveUi.Px(12, viewport, 8);
        _bossHealthBar.OffsetLeft = ResponsiveUi.Px(16, viewport, 8);
        _bossHealthBar.OffsetRight = -ResponsiveUi.Px(16, viewport, 8);
        _bossHealthBar.CustomMinimumSize = new Vector2(0, ResponsiveUi.Px(12, viewport, 8));
        _bossHealthBar.Visible = false;
        _bossHealthBar.ZIndex = 5;
        _bossHealthBar.AddThemeStyleboxOverride("background", MakeHpBarBg());
        _bossHealthBar.AddThemeStyleboxOverride("fill", MakeHpBarFill(HandDrawnTheme.Colors.BloodBright));
        AddChild(_bossHealthBar);

        _bossHealthLabel = HandDrawnTheme.MakeTitleLabel(
            text: "",
            fontSize: ResponsiveUi.Font(UiFontSmall, viewport, 9),
            color: HandDrawnTheme.Colors.BloodBright,
            outlineSize: UiOutlineNorm,
            outlineColor: new Color(0, 0, 0, 0.7f),
            hAlign: HorizontalAlignment.Center,
            vAlign: VerticalAlignment.Center);
        _bossHealthLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        _bossHealthLabel.OffsetTop = _bossHealthBar.OffsetTop;
        _bossHealthLabel.OffsetBottom = _bossHealthBar.OffsetTop + ResponsiveUi.Px(18, viewport, 12);
        _bossHealthLabel.Visible = false;
        _bossHealthLabel.ZIndex = 6;
        AddChild(_bossHealthLabel);
    }

    /// <summary>Boss 出场警告演出：全屏红光闪 + 顶部警告横幅（含 boss 名）。
    /// 由 GameManager._SpawnBossZombie 调用，每次 boss 出现播放一次。</summary>
    public void PlayBossWarning(string bossName)
    {
        var viewport = ResponsiveUi.ViewportSize(this);

        // 全屏红光闪（alpha 0 → 0.35 → 0，自动销毁）
        var redFlash = new ColorRect
        {
            Color = new Color(0.6f, 0.05f, 0.02f, 0f),
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 200,
        };
        redFlash.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(redFlash);
        var flashTween = CreateTween();
        flashTween.TweenProperty(redFlash, "color:a", 0.35f, 0.15f);
        flashTween.TweenProperty(redFlash, "color:a", 0.0f, 0.55f);
        flashTween.TweenCallback(Callable.From(redFlash.QueueFree));

        // 警告横幅（从顶部滑入 → 保持 1.5s → 淡出，自动销毁）
        var banner = new Panel();
        banner.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        banner.CustomMinimumSize = new Vector2(0, ResponsiveUi.Px(70, viewport, 52));
        banner.MouseFilter = MouseFilterEnum.Ignore;
        banner.ZIndex = 201;
        var bannerStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.4f, 0.05f, 0.05f, 0.88f),
            BorderColor = HandDrawnTheme.Colors.BloodBright,
            BorderWidthBottom = 3,
            BorderWidthTop = 3,
            ContentMarginTop = ResponsiveUi.Px(6, viewport, 4),
            ContentMarginBottom = ResponsiveUi.Px(6, viewport, 4),
        };
        banner.AddThemeStyleboxOverride("panel", bannerStyle);
        banner.Modulate = new Color(1, 1, 1, 0);
        AddChild(banner);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        vbox.Alignment = BoxContainer.AlignmentMode.Center;
        vbox.AddThemeConstantOverride("separation", ResponsiveUi.Px(2, viewport, 1));
        banner.AddChild(vbox);

        var title = new Label { Text = "! BOSS 来袭 !" };
        title.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontTitle, viewport, 18));
        title.AddThemeColorOverride("font_color", HandDrawnTheme.Colors.BloodBright);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeConstantOverride("outline_size", UiOutlineBold);
        title.AddThemeColorOverride("font_outline_color", HandDrawnTheme.Colors.InkBlack);
        vbox.AddChild(title);

        var nameLabel = new Label { Text = bossName };
        nameLabel.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontBody, viewport, 12));
        nameLabel.AddThemeColorOverride("font_color", HandDrawnTheme.Colors.NoteBg);
        nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        nameLabel.AddThemeConstantOverride("outline_size", UiOutlineThin);
        nameLabel.AddThemeColorOverride("font_outline_color", HandDrawnTheme.Colors.InkBlack);
        vbox.AddChild(nameLabel);

        // 动画：先淡入（modulate.a 0→1），保持 1.5s，再淡出 + QueueFree
        var bannerTween = CreateTween();
        bannerTween.TweenProperty(banner, "modulate:a", 1.0f, 0.3f);
        bannerTween.TweenInterval(1.5f);
        bannerTween.TweenProperty(banner, "modulate:a", 0.0f, 0.5f);
        bannerTween.TweenCallback(Callable.From(banner.QueueFree));
    }

    public string GetWeaponTooltipText(WeaponCard weapon)
    {
        return _GetWeaponTooltipText(weapon);
    }

    public string GetUpgradeTooltipText(UpgradeCard upgrade)
    {
        return _GetUpgradeTooltipText(upgrade);
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
            // 武器选择快捷键 (1/2/3/Enter/Delete) 由 WeaponSelectionPanel._Input 处理

            if (@event.IsActionPressed("toggle_help") || key.Keycode == Key.H)
            {
                _ToggleControlsPanel();
                GetViewport().SetInputAsHandled();
                return;
            }
            else if (@event.IsActionPressed("toggle_equipment") || key.Keycode == Key.C)
            {
                _ToggleEquipmentPanel();
                GetViewport().SetInputAsHandled();
                return;
            }
            else if (key.Keycode == Key.I)
            {
                _ToggleInventoryPanel();
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        // 武器选择面板的 skip 按钮点击由 WeaponSelectionPanel 内部处理
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("pause") && TryHandleEscape())
            GetViewport().SetInputAsHandled();
    }

    /// <summary>
    /// 统一 ESC 键处理：按优先级短路求值状态机。
    /// 优先级：GameOver / WelcomePanel > HelpPanel > 武器选择 > 普通暂停。
    /// 返回 true 表示事件已消费（调用方应 SetInputAsHandled）。
    /// </summary>
    private bool TryHandleEscape()
    {
        // 1. 退出游戏状态（最高）
        if (_gameOverUi.Visible || (_welcomePanel != null && _welcomePanel.Visible))
        {
            GetTree().Quit();
            return true;
        }
        // 2. 关闭帮助面板
        if (_helpPanel.Visible)
        {
            _CloseHelpPanel();
            return true;
        }
        // 2c. 关闭背包面板
        if (_inventoryPanel != null && _inventoryPanel.Visible)
        {
            _CloseInventoryPanel();
            return true;
        }
        // 2d. 关闭合成面板
        if (_craftingPanel != null && _craftingPanel.Visible)
        {
            _CloseCrafting();
            return true;
        }
        // 2b. 关闭装备面板
        if (_equipmentPanel.Visible)
        {
            _CloseEquipmentPanel();
            return true;
        }
        // 3. 关闭武器选择
        if (_weaponSelectionPanel.IsOpen())
        {
            CloseWeaponSelection();
            return true;
        }
        // 4. 切换暂停
        SetUserPaused(!_userPaused);
        return true;
    }

    // ═══════════════════════════════════════════════
    // Process
    // ═══════════════════════════════════════════════

    public override void _Process(double delta)
    {
        // tooltip 跟随由 TooltipSystem._Process 自行处理

        // 陷阱 HUD 提示倒计时清除（玩家离开陷阱区后 0.6s 自动消失）
        if (_hazardTimer > 0)
        {
            _hazardTimer -= (float)delta;
            if (_hazardTimer <= 0)
                _hudPanel?.ShowHazard("");
        }

        // HUD 更新：只在值变化时赋值，避免每帧无意义的字符串分配和 canvas 重绘
        if (_gameManager != null && _gameManager.PlayerRef != null)
        {
            var p = _gameManager.PlayerRef;
            _hudPanel?.UpdateDashCooldown(p.CanDash ? 0.0f : (float)(p.DashCooldownTimer?.TimeLeft ?? 0.0));
            float interval = Mathf.Snapped(p.AttackCooldown, 0.1f);
            float atkCd = (p.Combat != null && p.Combat.AttackTimer.TimeLeft >= 0.05f) ? (float)p.Combat.AttackTimer.TimeLeft : 0.0f;
            _hudPanel?.UpdateAttackInterval(interval, atkCd);
            _hudPanel?.UpdateDebuffStatus(p);
        }

        // Boss health bar
        if (_gameManager?.BossZombie != null && IsInstanceValid(_gameManager.BossZombie))
        {
            var boss = _gameManager.BossZombie;
            if (!_bossUiVisible)
            {
                _bossUiVisible = true;
                if (_bossHealthBar != null) _bossHealthBar.Visible = _gameStarted;
                if (_bossHealthLabel != null) _bossHealthLabel.Visible = _gameStarted;
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
        else if (!_pauseLockedByUpgrade && !_IsUiModalVisible())
            GetTree().Paused = false;
    }

    public bool IsUpgradeActive() => _weaponSelectionPanel.IsOpen();

    private void _ToggleControlsPanel()
    {
        if (_weaponSelectionPanel.IsOpen() && !_helpPanel.Visible) return;
        if (!_helpPanel.Visible && _equipmentPanel.Visible)
            _equipmentPanel.Close();
        _helpPanel.Toggle();
        _RefreshUiModalPauseLock();
    }

    /// <summary>C 键 / HUD [装备] 按钮：开关 MMO 式装备面板。</summary>
    private void _ToggleEquipmentPanel()
    {
        // 武器选择阶段不弹装备面板，避免遮挡选卡
        if (_weaponSelectionPanel.IsOpen() && !_equipmentPanel.Visible) return;
        if (!_equipmentPanel.Visible && _helpPanel.Visible)
            _helpPanel.Close();
        _equipmentPanel.Toggle();
        if (_equipmentPanel.Visible)
        {
            var player = _gameManager?.PlayerRef;
            if (player != null)
                _equipmentPanel.Refresh(player);
        }
        _RefreshUiModalPauseLock();
    }

    private void _CloseHelpPanel()
    {
        _helpPanel.Close();
        _RefreshUiModalPauseLock();
    }

    private void _CloseEquipmentPanel()
    {
        _equipmentPanel.Close();
        _RefreshUiModalPauseLock();
    }

    /// <summary>I 键：开关背包面板。</summary>
    private void _ToggleInventoryPanel()
    {
        if (_inventoryPanel == null) return;
        if (_weaponSelectionPanel.IsOpen() && !_inventoryPanel.Visible) return;
        _inventoryPanel.Toggle();
        _RefreshUiModalPauseLock();
    }

    private void _CloseInventoryPanel()
    {
        if (_inventoryPanel != null) _inventoryPanel.Visible = false;
        _RefreshUiModalPauseLock();
    }

    /// <summary>工作台交互：打开合成面板（模态）。</summary>
    public void OpenCrafting()
    {
        if (_craftingPanel == null) return;
        var player = _gameManager?.PlayerRef;
        if (player == null) return;
        if (_weaponSelectionPanel.IsOpen()) return;
        _craftingPanel.Open(player);
        GetTree().Paused = true;
    }

    public bool IsCraftingOpen() => _craftingPanel?.Visible ?? false;

    private void _CloseCrafting()
    {
        if (_craftingPanel != null) _craftingPanel.Close();
        _RefreshUiModalPauseLock();
    }

    private bool _IsUiModalVisible() => _helpPanel.Visible || _equipmentPanel.Visible
        || (_inventoryPanel?.Visible ?? false) || (_craftingPanel?.Visible ?? false);

    private void _RefreshUiModalPauseLock()
    {
        if (_IsUiModalVisible())
        {
            GetTree().Paused = true;
        }
        else if (!_pauseLockedByUpgrade && !_userPaused)
        {
            GetTree().Paused = false;
        }
    }

    public void ToggleHelp()
    {
        _ToggleControlsPanel();
    }

    /// <summary>从 PausePanel 打开设置面板：隐藏暂停 + 显示设置。
    /// 必须保证只有从暂停态才能调用（由 PausePanel "设置" 按钮触发）。</summary>
    public void ShowSettings()
    {
        if (_pausePanel == null || _settingsPanel == null) return;
        _pausePanel.Visible = false;
        _settingsPanel.Visible = true;
    }

    /// <summary>设置面板关闭时（关闭按钮 / ESC）自动恢复 PausePanel。
    /// 这样"打开设置 → 关闭"会回到暂停态,而不是回到游戏。</summary>
    private void _OnSettingsClosed()
    {
        if (_pausePanel != null && _userPaused)
            _pausePanel.Visible = true;
    }

    private void _ToggleSound()
    {
        var sm = GameServices.Audio;
        sm.SfxEnabled = !sm.SfxEnabled;
        _hudPanel?.SetSoundIcon(sm.SfxEnabled);
    }

    private void _StartGame()
    {
        _gameStarted = true;
        if (_welcomePanel != null) _welcomePanel.Visible = false;
        _SetGameplayHudVisible(true);
    }

    private void _SetGameplayHudVisible(bool visible)
    {
        if (_hudPanel != null) _hudPanel.Visible = visible;
        if (_bossHealthBar != null) _bossHealthBar.Visible = visible && _bossUiVisible;
        if (_bossHealthLabel != null) _bossHealthLabel.Visible = visible && _bossUiVisible;
        if (_roundBanner != null) _roundBanner.Visible = visible;
        if (_resonanceIndicator != null) _resonanceIndicator.Visible = visible && Core.GameConfig.MathSystemsEnabled;
        if (_fieldChargeLabel != null) _fieldChargeLabel.Visible = visible;
        if (_strategyLabel != null) _strategyLabel.Visible = visible;
        if (_fieldHintLabel != null) _fieldHintLabel.Visible = visible;
    }

    // ═══════════════════════════════════════════════
    // Weapon Selection
    // ═══════════════════════════════════════════════

    public void OpenWeaponSelection()
    {
        _pauseLockedByUpgrade = true;
        GetTree().Paused = true;
        if (_pausePanel != null) _pausePanel.Visible = false;
        if (_helpPanel.Visible) _CloseHelpPanel();
        if (_equipmentPanel.Visible) _CloseEquipmentPanel();

        if (_gameManager?.PlayerRef != null)
            _weaponSelectionPanel.UpdateCurrentWeaponLabel(_gameManager.PlayerRef);
        _weaponSelectionPanel.Open();
    }

    public void CloseWeaponSelection()
    {
        _weaponSelectionPanel.Close();
        _HideTooltip();
        _pauseLockedByUpgrade = false;
        if (!_userPaused && !_IsUiModalVisible())
            GetTree().Paused = false;
        else if (_userPaused && _pausePanel != null)
            _pausePanel.Visible = true;
    }

    // ═══════════════════════════════════════════════
    // Tooltip Display（委托给 TooltipSystem）
    // ═══════════════════════════════════════════════

    private void _HideTooltip() => _tooltipSystem?.HideTooltip();

    public void ShowTooltip(string text) => _tooltipSystem?.Show(text);

    public void HideTooltip() => _tooltipSystem?.HideTooltip();

    private void _ShowTooltip(string text) => _tooltipSystem?.Show(text);

    // ═══════════════════════════════════════════════
    // Card Rendering
    // ═══════════════════════════════════════════════



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
        typeText += $" · Lv.{weapon.Tier} · {WeaponFormatter.GetRarityText(weapon.Rarity)}";
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

        if (weapon.ThornsChance > 0)
        {
            bb += $"  [color=#c08060]● 反伤: {Mathf.RoundToInt(weapon.ThornsChance)}%几率 ({Mathf.RoundToInt(weapon.ThornsRatio)}%反弹)[/color]\n";
            hasStats = true;
        }
        if (weapon.DodgeChance > 0)
        {
            bb += $"  [color=#80d080]● 闪避: +{Mathf.RoundToInt(weapon.DodgeChance)}%[/color]\n";
            hasStats = true;
        }
        if (weapon.PiercingBonus > 0)
        {
            bb += $"  [color=#a0a0d0]● 穿透: +{weapon.PiercingBonus}[/color]\n";
            hasStats = true;
        }
        if (weapon.BounceBonus > 0)
        {
            bb += $"  [color=#a0a0d0]● 弹跳: +{weapon.BounceBonus}[/color]\n";
            hasStats = true;
        }
        if (weapon.SplashBonus > 0)
        {
            bb += $"  [color=#d0a070]● 溅射: +{Mathf.RoundToInt(weapon.SplashBonus)}半径[/color]\n";
            hasStats = true;
        }
        if (weapon.Tenacity > 0)
        {
            bb += $"  [color=#a0c0c0]● 减控: -{Mathf.RoundToInt(weapon.Tenacity)}%负面时长[/color]\n";
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
            bb += "[color=#ff6666]【风险提示】[/color]\n";
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
                bb += $"[color=#e0a040]剑 将替换当前武器: {currentWeaponName}[/color]\n";
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
        _ApplyResponsiveLayout(viewportSize);
    }

    private void _ApplyResponsiveLayout(Vector2 viewportSize)
    {
        float scale = ResponsiveUi.Scale(viewportSize);
        // tooltip/levelup 响应式布局由各自组件内部处理

        _ApplyWeaponSelectionResponsive(viewportSize);
        _ApplyBossHealthResponsive(viewportSize);
        _ApplyMathUiResponsive(viewportSize);
    }

    private void _ApplyWeaponSelectionResponsive(Vector2 viewportSize)
    {
        _weaponSelectionPanel.ApplyResponsiveLayout();
    }

    private void _ApplyBossHealthResponsive(Vector2 viewportSize)
    {
        if (_bossHealthBar == null || _bossHealthLabel == null)
            return;

        float top = ResponsiveUi.Px(66, viewportSize, 46);
        float height = ResponsiveUi.Px(12, viewportSize, 8);
        _bossHealthBar.OffsetTop = top;
        _bossHealthBar.OffsetBottom = top + height;
        _bossHealthBar.OffsetLeft = ResponsiveUi.Px(16, viewportSize, 8);
        _bossHealthBar.OffsetRight = -ResponsiveUi.Px(16, viewportSize, 8);
        _bossHealthBar.CustomMinimumSize = new Vector2(0, height);
        _bossHealthLabel.OffsetTop = top;
        _bossHealthLabel.OffsetBottom = top + ResponsiveUi.Px(18, viewportSize, 12);
        _bossHealthLabel.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontSmall, viewportSize, 9));
    }

    private void _ApplyMathUiResponsive(Vector2 viewportSize)
    {
        if (_resonanceIndicator != null)
        {
            _resonanceIndicator.OffsetLeft = -ResponsiveUi.Px(210, viewportSize, 150);
            _resonanceIndicator.OffsetTop = ResponsiveUi.Px(58, viewportSize, 42);
            _resonanceIndicator.OffsetRight = -ResponsiveUi.Px(12, viewportSize, 6);
            _resonanceIndicator.OffsetBottom = _resonanceIndicator.OffsetTop + ResponsiveUi.Px(72, viewportSize, 52);
        }

        if (_fieldChargeLabel != null)
        {
            _fieldChargeLabel.OffsetLeft = ResponsiveUi.Px(12, viewportSize, 6);
            _fieldChargeLabel.OffsetBottom = -ResponsiveUi.Px(94, viewportSize, 70);
            _fieldChargeLabel.OffsetTop = -ResponsiveUi.Px(122, viewportSize, 96);
            _fieldChargeLabel.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(13, viewportSize, 9));
        }

        if (_strategyLabel != null)
        {
            _strategyLabel.OffsetLeft = ResponsiveUi.Px(12, viewportSize, 6);
            _strategyLabel.OffsetBottom = -ResponsiveUi.Px(124, viewportSize, 96);
            _strategyLabel.OffsetTop = -ResponsiveUi.Px(150, viewportSize, 116);
            _strategyLabel.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(11, viewportSize, 9));
        }

        if (_fieldHintLabel != null)
        {
            _fieldHintLabel.OffsetBottom = -ResponsiveUi.Px(120, viewportSize, 82);
            _fieldHintLabel.OffsetTop = -ResponsiveUi.Px(160, viewportSize, 116);
            _fieldHintLabel.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(16, viewportSize, 11));
        }
    }

    // ═══════════════════════════════════════════════
    // Game Manager Events
    // ═══════════════════════════════════════════════

    private void _OnWeaponSelectionStarted() => OpenWeaponSelection();

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
        var viewport = ResponsiveUi.ViewportSize(this);
        HideRoundBanner();
        // 关闭可能打开的模态面板，避免遮挡回合结算卡（Help/Equipment ZIndex 170 高于 RoundSummary 55）
        _helpPanel?.Close();
        _equipmentPanel?.Close();
        _HideTooltip();

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
        card.CustomMinimumSize = ResponsiveUi.FitPanel(new Vector2(340, 260), viewport, 0.84f, 0.72f);
        card.MouseFilter = Control.MouseFilterEnum.Ignore;

        var cardStyle = MakeNotePanel(borderRadius: 8f, shadowSize: 8f);
        cardStyle.ShadowOffset = new Vector2(4, 4);
        card.AddThemeStyleboxOverride("panel", cardStyle);
        center.AddChild(card);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", ResponsiveUi.Px(UiMarginHeroCard, viewport, 12));
        margin.AddThemeConstantOverride("margin_right", ResponsiveUi.Px(UiMarginHeroCard, viewport, 12));
        margin.AddThemeConstantOverride("margin_top", ResponsiveUi.Px(UiMarginHeroPad, viewport, 10));
        margin.AddThemeConstantOverride("margin_bottom", ResponsiveUi.Px(UiMarginHeroPad, viewport, 10));
        margin.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", ResponsiveUi.Px(UiSpacingNormal, viewport, 4));
        vbox.MouseFilter = Control.MouseFilterEnum.Ignore;
        margin.AddChild(vbox);

        // Title
        var titleLbl = new Label
        {
            Text = $"第 {round} 回合完成",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        titleLbl.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(26, viewport, 18));
        titleLbl.AddThemeColorOverride("font_color", BloodBright);
        titleLbl.AddThemeConstantOverride("outline_size", UiOutlineBold);
        titleLbl.AddThemeColorOverride("font_outline_color", InkBlack);
        vbox.AddChild(titleLbl);

        // Subtitle
        var subLbl = new Label
        {
            Text = "ROUND COMPLETE",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        subLbl.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontMicro, viewport, 9));
        subLbl.AddThemeColorOverride("font_color", new Color(0.6f, 0.55f, 0.40f));
        vbox.AddChild(subLbl);

        // Separator
        var sep = new HSeparator();
        sep.AddThemeColorOverride("separator", new Color(0.85f, 0.70f, 0.25f, 0.3f));
        sep.CustomMinimumSize = new Vector2(0, ResponsiveUi.Px(6, viewport, 3));
        vbox.AddChild(sep);

        // Spacer
        var sp1 = new Control { CustomMinimumSize = new Vector2(0, ResponsiveUi.Px(4, viewport, 2)), MouseFilter = Control.MouseFilterEnum.Ignore };
        vbox.AddChild(sp1);

        _AddStatRow(vbox, "本波击杀", kills.ToString(), new Color(0.8f, 0.5f, 0.4f));
        _AddStatRow(vbox, "累计击杀", totalKills.ToString(), BloodRed);
        _AddStatRow(vbox, "当前得分", score.ToString(), new Color(0.85f, 0.6f, 0.45f));

        // Spacer
        var sp2 = new Control { CustomMinimumSize = new Vector2(0, ResponsiveUi.Px(6, viewport, 3)), MouseFilter = Control.MouseFilterEnum.Ignore };
        vbox.AddChild(sp2);

        // Bottom hint
        var hint = new Label
        {
            Text = "准备进入下一波...",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        hint.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontSmall, viewport, 9));
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
        var viewport = ResponsiveUi.ViewportSize(this);
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
        lbl.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontEmph, viewport, 11));
        lbl.AddThemeColorOverride("font_color", new Color(0.65f, 0.65f, 0.70f));
        lbl.AddThemeConstantOverride("outline_size", UiOutlineThin);
        lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.5f));
        row.AddChild(lbl);

        // Value (right-aligned, takes half width)
        var val = new Label
        {
            Text = value,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        val.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(20, viewport, 14));
        val.AddThemeColorOverride("font_color", accentColor);
        val.AddThemeConstantOverride("outline_size", UiOutlineNorm);
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

    public void UpdateZombiesDisplay(int alive, int total)
    {
        _hudPanel?.UpdateZombies(alive, total);
    }

    public void UpdatePlayerStatus(Player player)
    {
        _hudPanel?.UpdatePlayerStatus(player);
        // 装备面板开着时实时刷新（选卡后立刻看到新装备入槽）
        if (_equipmentPanel.Visible)
            _equipmentPanel.Refresh(player);
    }

    // ═══════════════════════════════════════════════
    // Public API / Delegation
    // ═══════════════════════════════════════════════

    public void SetGameManager(GameManager manager)
    {
        _gameManager = manager;
        _SetupConnections();
    }

    public void SetWeaponDeck(WeaponDeck deck)
    {
        _weaponDeck = deck;
        // 现在两个依赖都已就位，初始化武器选择面板
        _weaponSelectionPanel.Setup(
            _gameManager!, _weaponDeck!,
            GetWeaponTooltipText, GetUpgradeTooltipText,
            (ctrl, text) => _ShowTooltip(text),
            _HideTooltip);
    }

    public bool TryShowFusionOffer(World.Altar altar, WeaponCard currentWeapon)
    {
        if (_fusionPanel == null || _weaponDeck == null || _gameManager?.PlayerRef == null) return false;
        string? resultName = _weaponDeck.GetFusionResultName(currentWeapon,
            new WeaponCard { CardName = altar.MateWeaponName ?? "" });
        if (resultName == null) return false;

        var player = _gameManager.PlayerRef;
        _fusionPanel.ShowOffer(currentWeapon.CardName, altar.MateWeaponName ?? "",
            resultName, altar.FusionResultDesc ?? "", _gameManager);

        _fusionPanel.FusionAccepted += () =>
        {
            ApplyFusion(currentWeapon, altar);
            altar.QueueFree();
        };
        _fusionPanel.FusionRejected += () =>
        {
            altar.Boon = Altar.BoonKind.Damage;
            altar.ActivateAndDestroy();
        };
        return true;
    }

    private void ApplyFusion(WeaponCard currentWeapon, World.Altar altar)
    {
        if (_weaponDeck == null || _gameManager?.PlayerRef == null) return;
        var mate = new WeaponCard { CardName = altar.MateWeaponName ?? "" };
        var fused = _weaponDeck.TryFuse(currentWeapon, mate);
        if (fused == null) return;

        var player = _gameManager.PlayerRef;
        int idx = player.ActiveEffects.IndexOf(currentWeapon);
        if (idx >= 0)
        {
            player.ActiveEffects[idx] = fused;
            player.Combat?.RecalculateStats();
        }
        GameServices.Audio.PlayUpgrade();
        _weaponSelectionPanel.UpdateCurrentWeaponLabel(player);
        ShowLevelUpNotification($"融合成功! 获得 {fused.CardName}", new Color(0.7f, 0.3f, 1.0f));
    }

    public void ShowGameOver()
    {
        HideRoundBanner();
        HideRoundSummary();
        if (_weaponSelectionPanel != null && _weaponSelectionPanel.IsOpen())
            CloseWeaponSelection();
        // 关闭可能打开的模态面板，避免遮挡 GameOver（Help/Equipment ZIndex 170 高于 GameOver 50）
        _helpPanel?.Close();
        _equipmentPanel?.Close();
        if (_pausePanel != null) _pausePanel.Visible = false;
        _HideTooltip();

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
        // 同步驱动游戏摄像机震动 + zoom punch（group 调用，无摄像机时静默忽略）
        GetTree().CallGroup("game_camera", "AddShake", intensity);
    }

    public void FlashAttackColor(Color attackColor)
    {
        _screenFx?.FlashAttackColor(attackColor);
    }

    /// <summary>暴击命中全屏锐利快闪（配合命中停顿）。</summary>
    public void FlashCrit()
    {
        _screenFx?.FlashCrit();
    }

    /// <summary>Boss 出场：全屏深红暗化冲击。</summary>
    public void FlashBossSpawn()
    {
        _screenFx?.FlashBossSpawn();
    }

    /// <summary>Boss 阶段切换：橙红爆闪。</summary>
    public void FlashBossPhase()
    {
        _screenFx?.FlashBossPhase();
    }

    /// <summary>Boss 击杀：白金强光爆发。</summary>
    public void FlashBossKill()
    {
        _screenFx?.FlashBossKill();
    }

    /// <summary>黄金掉落：柔和金色微闪（斐波那契掉落时刻）。</summary>
    public void FlashGoldenDrop()
    {
        _screenFx?.FlashGoldenDrop();
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

    /// <summary>显示升级/强化通知（委托给 NotificationPanel）</summary>
    public void ShowLevelUpNotification(string text, Color color)
        => _notificationPanel?.Show(text, color);

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

    /// <summary>
    /// 持续更新 HUD 天气显示（由 WeatherSystem 每 0.5s 推一次）。
    /// 区别于 ShowWeatherNotification 的"变化时弹一次"——
    /// HUD 顶部常驻 label 让玩家随时看到当前天气，特别是酸雨持续掉血时。
    /// </summary>
    public void UpdateWeatherHUD(string weatherName, Color weatherColor, bool isHazard, int damagePerTick, string effectsDesc)
    {
        _hudPanel?.UpdateWeather(weatherName, weatherColor, isHazard, damagePerTick, effectsDesc);
    }

    /// <summary>打开跨局永久升级面板（GameOverUI 的"永久升级"按钮调用）。</summary>
    public void ShowMetaUpgrade()
    {
        _metaPanel?.Open();
    }

    /// <summary>陷阱持续伤害 HUD 提示（HazardZone 伤害玩家时调用，0.6s 无更新则清除）。</summary>
    public void UpdateHazardHUD(string text)
    {
        _hudPanel?.ShowHazard(text);
        _hazardTimer = 0.6f;  // 略大于 HazardZone tick(0.5s)，玩家离开后自动消失
    }

    public void TriggerBossAtmosphere()
    {
        _screenFx?.TriggerBossAtmosphere();
    }

    public bool GetWelcomeVisible() => _welcomePanel != null && _welcomePanel.Visible;

    public Control GetHudPanel() => _hudPanel!;
}
