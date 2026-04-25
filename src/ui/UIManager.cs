namespace ZombieWars.UI;

using Godot;
using Godot.NativeInterop;
using System;
using System.Collections.Generic;
using ZombieWars.Core;
using ZombieWars.Weapons;
using ZombieWars.Audio;
using Player = ZombieWars.Player.Player;
using static ZombieWars.Core.GameEnums;
using static ZombieWars.UI.HandDrawnTheme;
using static ZombieWars.UI.HandDrawnTheme.Colors;

/// <summary>
/// UI Manager — creates and manages all in-game UI elements.
/// Handles HUD, weapon selection panel, tooltip, pause overlay, controls panel, and welcome screen.
/// </summary>
public partial class UIManager : Control
{
	// ═══════════════════════════════════════════════
	// Signals
	// ═══════════════════════════════════════════════
	public event Action<object?> WeaponCardSelected = delegate {};
	public event Action UpgradeSkipped = delegate {};

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
	private Panel _controlsPanel = null!;
	private bool _controlsVisible;
	private Panel _welcomePanel = null!;
	private bool _welcomeVisible = true;
	private Label _scoreLabel = null!;
	private ProgressBar _healthBar = null!;
	private ProgressBar _bossHealthBar = null!;
	private Label _bossHealthLabel = null!;
	private bool _bossUiVisible;
	private int _bossUiHealth = -1;
	private int _bossUiMaxHealth = -1;
	private Button _soundToggleButton = null!;

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
	private Panel _pausePanel = null!;
	private Label _pauseLabel = null!;
	private bool _pauseLockedByUpgrade;
	private bool _userPaused;
	private Label _currentWeaponLabel = null!;

		private Label _roundBanner = null!;
		private Panel? _roundSummaryPanel;
		private Tween? _lowHealthTween;

	// ═══════════════════════════════════════════════
	// Effects / Buff Icons
	// ═══════════════════════════════════════════════
	private HBoxContainer _effectsContainer = null!;
	private readonly Dictionary<string, Button> _effectIcons = new();

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

		_CreateUiElements();
		_SetupConnections();
		_CreateTooltipPanel();
		_CreatePausePanel();
		GetViewport().SizeChanged += _OnUiViewportSizeChanged;
		_OnUiViewportSizeChanged();
		SetProcess(true);
	}

	// ═══════════════════════════════════════════════
	// UI Construction
	// ═══════════════════════════════════════════════

	private void _CreateUiElements()
	{
		_CreateHudPanel();
		_CreateWeaponSelectionPanel();
		_CreateControlsPanel();
		_CreateWelcomePanel();
		_CreateRoundBanner();
	}

	private void _CreateHudPanel()
	{
		_hudPanel = new Panel();
		_hudPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
		_hudPanel.CustomMinimumSize = new Vector2(0, 48);
		_hudPanel.OffsetBottom = 52;

		var hudStyle = MakeDarkPanel(0.90f);
		hudStyle.BorderColor = new Color(0.239f, 0.102f, 0.102f, 0.4f);
		hudStyle.BorderWidthBottom = 1;
		_hudPanel.AddThemeStyleboxOverride("panel", hudStyle);
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

		// Row 1: HP bar (left) | Round (center) | Score (right)
		var row1 = new HBoxContainer();
		row1.AddThemeConstantOverride("separation", 12);
		mainVbox.AddChild(row1);

		// Left: HP bar area (fixed width, no expand)
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

		_healthLabel = new Label
		{
			Text = "120/120"
		};
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

		_roundLabel = new Label
		{
			Text = "第 1 波"
		};
		_roundLabel.AddThemeFontSizeOverride("font_size", 14);
		_roundLabel.AddThemeColorOverride("font_color", HandDrawnTheme.Colors.BloodBright);
		_roundLabel.AddThemeConstantOverride("outline_size", 2);
		_roundLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.6f));
		centerBox.AddChild(_roundLabel);

		_zombiesLabel = new Label
		{
			Text = "剩余 0"
		};
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

		// Row 2: stats + effect icons
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

		// Effect icons container
		_effectsContainer = new HBoxContainer();
		_effectsContainer.AddThemeConstantOverride("separation", 3);
		_effectsContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		row2.AddChild(_effectsContainer);

		// Sound toggle button
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

		var helpBtn = new Button
		{
			Text = "?",
			CustomMinimumSize = new Vector2(20, 20),
			FocusMode = Control.FocusModeEnum.None
		};
		helpBtn.Pressed += _ToggleControlsPanel;
		row2.AddChild(helpBtn);

		// Boss health bar
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
		var bossBg = MakeHpBarBg();
		var bossFill = MakeHpBarFill(HandDrawnTheme.Colors.BloodBright);
		_bossHealthBar.AddThemeStyleboxOverride("background", bossBg);
		_bossHealthBar.AddThemeStyleboxOverride("fill", bossFill);
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

	private Label _MakeStatLabel(string text, Color color)
	{
		var lbl = new Label { Text = text };
		lbl.AddThemeFontSizeOverride("font_size", 10);
		lbl.AddThemeColorOverride("font_color", color);
		lbl.AddThemeConstantOverride("outline_size", 1);
		lbl.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.5f));
		return lbl;
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
		descLabel.AddThemeColorOverride("font_color", Godot.Colors.White);		descLabel.HorizontalAlignment = HorizontalAlignment.Center;
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
			hintLabel.AddThemeColorOverride("font_color", PencilGray);
			cardVbox.AddChild(hintLabel);
		}

		// Keyboard hint label
		var keyboardHintLabel = new Label
		{
			Text = "💡 按 [1] [2] [3] 选择 | 按 [S] 或 [空格] 跳过",
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

	private void _CreatePausePanel()
	{
		_pausePanel = new Panel
		{
			Visible = false,
			ZIndex = 90,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ProcessMode = ProcessModeEnum.Always
		};
		_pausePanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		var styleBox = new StyleBoxFlat { BgColor = new Color(BattlefieldBg, 0.7f) };
		_pausePanel.AddThemeStyleboxOverride("panel", styleBox);
		AddChild(_pausePanel);

		var center = new CenterContainer();
		center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_pausePanel.AddChild(center);

		var pauseCard = new Panel();
		pauseCard.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
		var pauseCardStyle = MakeNotePanel(borderRadius: 5f, shadowSize: 8f);
		pauseCard.AddThemeStyleboxOverride("panel", pauseCardStyle);

		var pauseVbox = new VBoxContainer();
		pauseVbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		pauseCard.AddChild(pauseVbox);

		var pauseMargin = new MarginContainer();
		pauseMargin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		pauseMargin.AddThemeConstantOverride("margin_left", 30);
		pauseMargin.AddThemeConstantOverride("margin_right", 30);
		pauseMargin.AddThemeConstantOverride("margin_top", 20);
		pauseMargin.AddThemeConstantOverride("margin_bottom", 20);
		pauseVbox.AddChild(pauseMargin);

		_pauseLabel = new Label
		{
			Text = "⏸ 暂停中\n按 ESC 继续",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_pauseLabel.AddThemeFontSizeOverride("font_size", 28);
		_pauseLabel.AddThemeColorOverride("font_color", NoteText);
		pauseMargin.AddChild(_pauseLabel);
		center.AddChild(pauseCard);
	}

	private void _CreateRoundBanner()
	{
		_roundBanner = new Label
		{
			Text = "",
			Visible = false,
			ZIndex = 50,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		// Use Center preset for centering, then position at screen center
		_roundBanner.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
		var viewportSize = GetViewport().GetVisibleRect().Size;
		// Position at screen center
		_roundBanner.Position = new Vector2(
			(viewportSize.X - 400.0f) / 2.0f,
			(viewportSize.Y - 80.0f) / 2.0f
		);
		_roundBanner.CustomMinimumSize = new Vector2(400.0f, 80);
		_roundBanner.AddThemeFontSizeOverride("font_size", 32);
		_roundBanner.AddThemeColorOverride("font_color", BloodBright);
		_roundBanner.AddThemeConstantOverride("outline_size", 6);
		_roundBanner.AddThemeColorOverride("font_outline_color", InkBlack);
		AddChild(_roundBanner);
	}

	private void _CreateControlsPanel()
	{
		_controlsPanel = new Panel
		{
			Visible = false,
			FocusMode = Control.FocusModeEnum.None
		};
		_controlsPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
		_OnUiViewportSizeChanged();

		var styleBox = MakeNotePanel(borderRadius: 5f, shadowSize: 6f);
		_controlsPanel.AddThemeStyleboxOverride("panel", styleBox);
		AddChild(_controlsPanel);

		var scrollContainer = new ScrollContainer();
		scrollContainer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		scrollContainer.SetOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, 10);
		_controlsPanel.AddChild(scrollContainer);

		var container = new VBoxContainer();
		container.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scrollContainer.AddChild(container);

		var titleLabel = new Label
		{
			Text = "🎮 操作指南"
		};
		titleLabel.AddThemeFontSizeOverride("font_size", 20);
		titleLabel.AddThemeColorOverride("font_color", NoteText);
		titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		container.AddChild(titleLabel);

		var separator = new HSeparator();
		separator.AddThemeColorOverride("separator", new Color(NoteBorder, 0.5f));
		container.AddChild(separator);

		_CreateInfoCard(container, "🏃 移动", "WASD 或 方向键\n自由移动角色", new Color(0.3f, 0.8f, 0.3f));
		_CreateInfoCard(container, "⚔️ 攻击", "按住空格进行攻击\n魔法类自动锁定", new Color(0.8f, 0.3f, 0.3f));
		_CreateInfoCard(container, "🗡️ 武器类型", "近战: 扇形斩击\n远程: 发射子弹\n魔法: 追踪导弹\n范围: 爆炸攻击", new Color(0.8f, 0.6f, 0.2f));
		_CreateInfoCard(container, "🎯 回合目标", "消灭所有僵尸\n回合开始时选择强化\n可跳过本次强化", new Color(0.6f, 0.3f, 0.8f));
		_CreateInfoCard(container, "💨 冲刺", "按 Shift 快速闪避\n有冷却时间", new Color(0.4f, 0.7f, 1.0f));
		_CreateInfoCard(container, "🔭 锁定", "按 Tab 切换目标优先级\n最近/最远/密集", new Color(0.5f, 0.8f, 0.9f));
		_CreateInfoCard(container, "⏸ 暂停", "按 ESC 暂停/继续游戏\n强化阶段无法手动暂停", new Color(0.8f, 0.8f, 0.5f));

		var closeHint = new Label
		{
			Text = "💡 按 H 键切换显示"
		};
		closeHint.AddThemeFontSizeOverride("font_size", 12);
		closeHint.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
		closeHint.HorizontalAlignment = HorizontalAlignment.Center;
		container.AddChild(closeHint);
	}

	private void _CreateInfoCard(VBoxContainer parent, string title, string content, Color accentColor)
	{
		var card = new Panel
		{
			CustomMinimumSize = new Vector2(0, 80)
		};
		var cardStyle = new StyleBoxFlat
		{
			BgColor = new Color(NoteBg, 0.85f),
			BorderWidthLeft = 2,
			BorderColor = NoteBorder,
			CornerRadiusTopLeft = 5,
			CornerRadiusTopRight = 5,
			CornerRadiusBottomLeft = 5,
			CornerRadiusBottomRight = 5
		};
		card.AddThemeStyleboxOverride("panel", cardStyle);
		parent.AddChild(card);

		var cardContainer = new VBoxContainer();
		cardContainer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		cardContainer.SetOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, 8);
		card.AddChild(cardContainer);

		var titleLabel = new Label { Text = title };
		titleLabel.AddThemeFontSizeOverride("font_size", 14);
		titleLabel.AddThemeColorOverride("font_color", NoteText);
		cardContainer.AddChild(titleLabel);

		var contentLabel = new Label
		{
			Text = content,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		contentLabel.AddThemeFontSizeOverride("font_size", 12);
		contentLabel.AddThemeColorOverride("font_color", NoteText);
		cardContainer.AddChild(contentLabel);

		var spacer = new Control { CustomMinimumSize = new Vector2(0, 5) };
		parent.AddChild(spacer);
	}

	private void _CreateWelcomePanel()
	{
		_welcomePanel = new Panel();
		_welcomePanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		var styleBox = new StyleBoxFlat { BgColor = new Color(BattlefieldBg, 0.85f) };
		_welcomePanel.AddThemeStyleboxOverride("panel", styleBox);
		AddChild(_welcomePanel);

		var marginContainer = new MarginContainer();
		marginContainer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		marginContainer.AddThemeConstantOverride("margin_left", 50);
		marginContainer.AddThemeConstantOverride("margin_right", 50);
		marginContainer.AddThemeConstantOverride("margin_top", 30);
		marginContainer.AddThemeConstantOverride("margin_bottom", 30);
		_welcomePanel.AddChild(marginContainer);

		var centerContainer = new CenterContainer();
		centerContainer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		marginContainer.AddChild(centerContainer);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 25);
		centerContainer.AddChild(vbox);

		var title = new Label { Text = "🧟 僵尸大战 🧟" };
		title.AddThemeFontSizeOverride("font_size", 36);
		title.AddThemeColorOverride("font_color", BloodBright);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(title);

		var subtitle = new Label { Text = "Zombie vs Survivors" };
		subtitle.AddThemeFontSizeOverride("font_size", 18);
		subtitle.AddThemeColorOverride("font_color", new Color(0.75f, 0.6f, 0.6f));
		subtitle.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(subtitle);

		string[] introTexts = {
			"欢迎来到僵尸大战！",
			"",
			"在这个生存游戏中，你需要：",
			"• 每回合选择武器卡牌强化自己",
			"• 消灭不断涌现的僵尸",
			"• 尽可能坚持更多回合",
			"",
			"准备好迎接挑战了吗？"
		};

		foreach (var text in introTexts)
		{
			var label = new Label { Text = text };
			if (text == "")
				label.CustomMinimumSize = new Vector2(0, 8);
			else
				label.AddThemeFontSizeOverride("font_size", 16);
			label.AddThemeColorOverride("font_color", new Color(0.7f, 0.55f, 0.55f));
			label.HorizontalAlignment = HorizontalAlignment.Center;
			vbox.AddChild(label);
		}

		var startButton = new Button
		{
			Text = "开始游戏",
			CustomMinimumSize = new Vector2(180, 50)
		};
		startButton.AddThemeFontSizeOverride("font_size", 22);
		var startNormal = MakeNotePanel(borderRadius: 4f, shadowSize: 4f);
		startNormal.BgColor = new Color(0.25f, 0.12f, 0.12f, 0.9f);
		startButton.AddThemeStyleboxOverride("normal", startNormal);
		var startHover = MakeNotePanel(borderRadius: 4f, shadowSize: 4f);
		startHover.BgColor = new Color(0.35f, 0.18f, 0.18f, 0.9f);
		startButton.AddThemeStyleboxOverride("hover", startHover);
		startButton.AddThemeColorOverride("font_color", NoteBg);
		startButton.Pressed += _StartGame;
		vbox.AddChild(startButton);

		var helpButton = new Button
		{
			Text = "操作说明",
			CustomMinimumSize = new Vector2(120, 40)
		};
		helpButton.AddThemeFontSizeOverride("font_size", 16);
		var helpNormal = MakeNotePanel(borderRadius: 3f, shadowSize: 3f);
		helpNormal.BgColor = new Color(0.2f, 0.15f, 0.15f, 0.9f);
		helpButton.AddThemeStyleboxOverride("normal", helpNormal);
		var helpHover = MakeNotePanel(borderRadius: 3f, shadowSize: 3f);
		helpHover.BgColor = new Color(0.3f, 0.22f, 0.22f, 0.9f);
		helpButton.AddThemeStyleboxOverride("hover", helpHover);
		helpButton.AddThemeColorOverride("font_color", NoteBg);
		helpButton.Pressed += _ToggleControlsPanel;
		vbox.AddChild(helpButton);
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
					case Key.Space:
						_OnSkipPressed();
						GetViewport().SetInputAsHandled();
						return;
				}
			}

			if (key.Keycode == Key.H)
				_ToggleControlsPanel();
		}

		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left && IsUpgradeActive())
		{
			for (int i = 0; i < _weaponButtons.Count; i++)
			{
				if (i < _currentWeaponOptions.Count && _weaponButtons[i].GetGlobalRect().HasPoint(mb.GlobalPosition))
				{
					_OnWeaponButtonPressed(i);
					GetViewport().SetInputAsHandled();
					return;
				}
			}
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

	private void _UpdateAttackIntervalDisplay(Player player)
	{
		if (_attackIntervalLabel == null) return;
		var intervalText = Mathf.Snapped(player.AttackCooldown, 0.1f).ToString("F1") + "s";
		string cooldownText;
		if (player.Combat == null) return;
		if (player.Combat.AttackTimer.TimeLeft < 0.05f)
			cooldownText = "就绪";
		else
			cooldownText = ((float)player.Combat.AttackTimer.TimeLeft).ToString("F1") + "s";
		_attackIntervalLabel.Text = $"间隔 {intervalText} | 冷却 {cooldownText}";
		_attackIntervalLabel.TooltipText = $"攻击间隔: {intervalText}\n下次可攻击: {cooldownText}";
	}

	// ═══════════════════════════════════════════════
	// Pause
	// ═══════════════════════════════════════════════

	public void SetUserPaused(bool paused)
	{
		_userPaused = paused;
		_pausePanel.Visible = paused;
		if (paused)
			GetTree().Paused = true;
		else if (!_pauseLockedByUpgrade)
			GetTree().Paused = false;
	}

	public bool IsUpgradeActive() => _weaponSelectionPanel.Visible;

	private void _ToggleControlsPanel()
	{
		if (_weaponSelectionPanel.Visible && !_controlsVisible) return;
		_controlsVisible = !_controlsVisible;
		_controlsPanel.Visible = _controlsVisible;
	}

	private void _ToggleSound()
	{
		var sm = SoundManager.Instance;
		if (sm == null) return;

		sm.SfxEnabled = !sm.SfxEnabled;
		if (_soundToggleButton != null)
		{
			_soundToggleButton.Text = sm.SfxEnabled ? "🔊" : "🔇";
		}
	}

	private void _StartGame()
	{
		_welcomePanel.Visible = false;
		_welcomeVisible = false;
	}

	// ═══════════════════════════════════════════════
	// Weapon Selection
	// ═══════════════════════════════════════════════

	public void ShowWeaponSelection()
	{
		if (_weaponDeck == null)
		{
			GD.PrintErr("错误：武器牌堆未设置");
			return;
		}
		_pauseLockedByUpgrade = true;
		GetTree().Paused = true;
		if (_pausePanel != null) _pausePanel.Visible = false;

		if (_controlsVisible)
			_ToggleControlsPanel();

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

				Color baseColor = NoteBg;
				Color borderColor;

				switch (wc.Rarity)
				{
					case 1:
						borderColor = RarityCommon;
						titleNode.AddThemeColorOverride("font_color", NoteText);
						rarityNode.AddThemeColorOverride("font_color", NoteText);
						break;
					case 2:
						borderColor = new Color(BloodRed, 0.9f);
						titleNode.AddThemeColorOverride("font_color", BloodRed);
						rarityNode.AddThemeColorOverride("font_color", BloodRed);
						break;
					case 3:
						borderColor = RarityEpic;
						titleNode.AddThemeColorOverride("font_color", new Color(0.82f, 0.55f, 0.08f));
						rarityNode.AddThemeColorOverride("font_color", RarityEpic);
						break;
					case 4:
						borderColor = new Color(1f, 0.8f, 0.25f);
						titleNode.AddThemeColorOverride("font_color", new Color(0.7f, 0.5f, 0.0f));
						rarityNode.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.25f));
						break;
					default:
						borderColor = RarityCommon;
						titleNode.AddThemeColorOverride("font_color", NoteText);
						rarityNode.AddThemeColorOverride("font_color", NoteText);
						break;
				}

				var cardStyle = MakeCardPanel(baseColor, borderColor);
				button.AddThemeStyleboxOverride("normal", cardStyle);

				var hoverBg = _Lighten(baseColor, 0.04f);
				var hoverBorder = _Lighten(borderColor, 0.15f);
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
					effects.Add(effect.GetMeleeEffectDesc((MeleeEffect)(int)m, level));
				}
				foreach (var r in effect.RangedEffects)
				{
					var level = effect.RangedEffectLevels.TryGetValue((int)r, out var l) ? l.AsInt32() : 1;
					effects.Add(effect.GetRangedEffectDesc((RangedEffect)(int)r, level));
				}
				foreach (var g in effect.MagicEffects)
				{
					var level = effect.MagicEffectLevels.TryGetValue((int)g, out var l) ? l.AsInt32() : 1;
					effects.Add(effect.GetMagicEffectDesc((MagicEffect)(int)g, level));
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

			ZombieWars.Audio.SoundManager.Instance?.PlayWeaponSelect();
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
		if (_tooltipHideTimer != null && !_tooltipHideTimer.IsStopped())
			_tooltipHideTimer.Start();
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
		descNode.Text = upgrade.Description;
		rarityNode.Text = "升级";

		titleNode.AddThemeColorOverride("font_color", UpgradeTitle);
		rarityNode.AddThemeColorOverride("font_color", UpgradeBorder);

		var style = MakeCardPanel(UpgradeBg, UpgradeBorder);
		button.AddThemeStyleboxOverride("normal", style);

		var hoverStyle = MakeCardPanel(_Lighten(UpgradeBg, 0.08f), _Lighten(UpgradeBorder, 0.1f));
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
				bb += $"  [color=#ddaaff]★ {weapon.GetMeleeEffectDesc((MeleeEffect)key, level)}[/color]\n";
				string detail = weapon.GetEffectDetail(((MeleeEffect)key).ToString(), "melee", level);
				if (!string.IsNullOrEmpty(detail))
					bb += $"     [color=#999999]{detail}[/color]\n";
			}
			foreach (var effect in weapon.RangedEffects)
			{
				int key = (int)effect;
				int level = weapon.RangedEffectLevels.TryGetValue(key, out var l) ? l.AsInt32() : 1;
				bb += $"  [color=#ddaaff]★ {weapon.GetRangedEffectDesc((RangedEffect)key, level)}[/color]\n";
				string detail = weapon.GetEffectDetail(((RangedEffect)key).ToString(), "ranged", level);
				if (!string.IsNullOrEmpty(detail))
					bb += $"     [color=#999999]{detail}[/color]\n";
			}
			foreach (var effect in weapon.MagicEffects)
			{
				int key = (int)effect;
				int level = weapon.MagicEffectLevels.TryGetValue(key, out var l) ? l.AsInt32() : 1;
				bb += $"  [color=#ddaaff]★ {weapon.GetMagicEffectDesc((MagicEffect)key, level)}[/color]\n";
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
		if (_controlsPanel != null)
		{
			_controlsPanel.Size = new Vector2(
				Mathf.Min(340.0f, viewportSize.X * 0.36f),
				Mathf.Min(520.0f, viewportSize.Y - 40.0f)
			);
			_controlsPanel.Position = new Vector2(-_controlsPanel.Size.X - 20.0f, 20.0f);
		}
		if (_roundBanner != null)
			_roundBanner.CustomMinimumSize = new Vector2(Mathf.Min(720.0f, viewportSize.X - 40.0f), 80);
	}

	// ═══════════════════════════════════════════════
	// Game Manager Events
	// ═══════════════════════════════════════════════

	private void _OnWeaponSelectionStarted() => ShowWeaponSelection();

	private void _OnRoundStarted(int roundNumber)
	{
		UpdateRoundDisplay(roundNumber);
		ShowRoundBanner($"第 {roundNumber} 回合开始");
	}

	public void UpdateRoundDisplay(int roundNumber)
	{
		_roundLabel.Text = $"🏆 回合 {roundNumber}";
		_roundLabel.TooltipText = $"当前回合: {roundNumber}";
	}

	public void ShowRoundBanner(string text, bool persistent = false)
	{
		if (_roundBanner == null) return;
		_roundBanner.Text = text;
		_roundBanner.Visible = true;
		_roundBanner.Modulate = _roundBanner.Modulate with { A = 0.0f };
		_roundBanner.Scale = new Vector2(0.8f, 0.8f);

		var tween = CreateTween();
		tween.Parallel().TweenProperty(_roundBanner, Node2D.PropertyName.Modulate.ToString() + ":a", 1.0f, 0.3f)
			.SetTrans(Tween.TransitionType.Cubic);
		tween.Parallel().TweenProperty(_roundBanner, "scale", new Vector2(1.1f, 1.1f), 0.3f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(_roundBanner, "scale", new Vector2(1.0f, 1.0f), 0.15f);

		if (!persistent)
		{
			tween.TweenInterval(0.8f);
			tween.Parallel().TweenProperty(_roundBanner, Node2D.PropertyName.Modulate.ToString() + ":a", 0.0f, 0.4f);
			tween.Parallel().TweenProperty(_roundBanner, "scale", new Vector2(1.15f, 1.15f), 0.4f);
			tween.TweenCallback(Callable.From(() => _roundBanner.Visible = false));
		}
	}

	public void UpdateCountdown(int seconds)
	{
		if (_roundBanner == null) return;
		_roundBanner.Text = $"准备! {seconds}";
		_roundBanner.Scale = new Vector2(1.3f, 1.3f);

		var tween = CreateTween();
		tween.TweenProperty(_roundBanner, "scale", new Vector2(1.0f, 1.0f), 0.3f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	public void HideRoundBanner()
	{
		if (_roundBanner == null) return;
		_roundBanner.Visible = false;
	}
	public void ShowRoundSummary(int round, int kills, int totalKills, int score)
	{
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
		if (_scoreLabel != null)
			_scoreLabel.Text = newScore.ToString();
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
		var fillStyle = MakeHpBarFill(hpColor);
		_healthBar.AddThemeStyleboxOverride("fill", fillStyle);

		if (_healthLabel != null)
			_healthLabel.Text = $"{currentHealth}/{maxHealth}";

		UpdateLowHpVignette(healthPercent);

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

	public void UpdateZombiesDisplay(int zombiesRemaining, int totalZombies)
	{
		if (_zombiesLabel != null)
			_zombiesLabel.Text = $"剩余: {zombiesRemaining}/{totalZombies}";
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
				AttackType.Melee => "近战",
				AttackType.Ranged => "远程",
				AttackType.Area => "范围",
				AttackType.Magic => "魔法",
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
		_UpdateStatusTooltips(player);
	}

	private void _UpdateStatusTooltips(Player player)
	{
		if (_damageLabel != null)
			_damageLabel.TooltipText = $"攻击力: {Mathf.RoundToInt(player.CurrentDamage)}\n基础伤害与倍率已计入";
		if (_critLabel != null)
			_critLabel.TooltipText = $"暴击概率: {Mathf.RoundToInt(player.CriticalChance)}%\n暴击伤害倍率: x{Mathf.Snapped(player.CritMultiplier, 0.1f)}";
		if (_lifestealLabel != null)
			_lifestealLabel.TooltipText = $"吸血比例: {Mathf.RoundToInt(player.LifeStealPercent)}%\n命中后回复对应比例生命";
		if (_speedLabel != null)
			_speedLabel.TooltipText = $"移动速度: {Mathf.RoundToInt(player.CurrentSpeed)}\n受装备、升级和减速效果影响";
		if (_targetPriorityLabel != null)
		{
			string priorityText = player.TargetPriority switch
			{
				1 => "最远",
				2 => "密集",
				_ => "最近"
			};
			_targetPriorityLabel.TooltipText = $"目标优先级: {priorityText}\n按 Tab 切换";
		}
		if (_attackTypeLabel != null)
			_attackTypeLabel.TooltipText = WeaponFormatter.BuildAttackEffectsTooltip(player);
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

		// Create icon buttons
		foreach (Godot.Collections.Dictionary item in activeItems)
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
				CornerRadiusTopLeft = 4,
				CornerRadiusTopRight = 4,
				CornerRadiusBottomLeft = 4,
				CornerRadiusBottomRight = 4
			};
			btn.AddThemeStyleboxOverride("normal", style);

			var hoverStyle = new StyleBoxFlat
			{
				BgColor = _Darken(itemColor, 0.4f),
				BorderWidthBottom = 2,
				BorderColor = itemColor,
				CornerRadiusTopLeft = 4,
				CornerRadiusTopRight = 4,
				CornerRadiusBottomLeft = 4,
				CornerRadiusBottomRight = 4
			};
			btn.AddThemeStyleboxOverride("hover", hoverStyle);

			string effectTooltip = (string)item["tooltip"];
			btn.MouseEntered += () => _ShowTooltip(effectTooltip);
			btn.MouseExited += _OnEffectIconExited;

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

	private void _OnEffectIconExited()
	{
		if (_tooltipHideTimer != null)
			_tooltipHideTimer.Start();
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

	public void TriggerBossAtmosphere()
	{
		_screenFx?.TriggerBossAtmosphere();
	}

	public bool GetWelcomeVisible() => _welcomeVisible;

	public Control GetHudPanel() => _hudPanel;

	// ═══════════════════════════════════════════════
	// Color Utilities
	// ═══════════════════════════════════════════════

	private static Color _Lighten(Color c, float amount)
	{
		float r = Mathf.Min(1.0f, c.R + amount);
		float g = Mathf.Min(1.0f, c.G + amount);
		float b = Mathf.Min(1.0f, c.B + amount);
		return new Color(r, g, b, c.A);
	}

	private static Color _Darken(Color c, float amount)
	{
		float r = Mathf.Max(0.0f, c.R - amount);
		float g = Mathf.Max(0.0f, c.G - amount);
		float b = Mathf.Max(0.0f, c.B - amount);
		return new Color(r, g, b, c.A);
	}
}
