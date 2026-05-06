namespace ZombieWars.UI;

using Godot;
using ZombieWars.Core;

using static ZombieWars.UI.HandDrawnTheme;
using static ZombieWars.UI.HandDrawnTheme.Colors;

/// <summary>
/// Game Over screen — death fade-in, stats, restart / quit buttons.
/// Enhanced with detailed statistics and achievements.
/// </summary>
public partial class GameOverUI : Control
{
	private GameManager? _gameManager;

	public GameOverUI()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	public void Setup(GameManager? gm)
	{
		_gameManager = gm;
	}

	public void ShowGameOver(Panel? hudPanel)
	{
		if (hudPanel != null)
			hudPanel.Visible = false;

		// Dark background fade-in
		var bg = new ColorRect
		{
			ZIndex = 50
		};
		bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		bg.Color = new Color(0, 0, 0, 0);
		AddChild(bg);

		var redTint = new ColorRect
		{
			ZIndex = 51,
			Color = new Color(0.3f, 0.05f, 0.02f, 0)
		};
		redTint.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(redTint);

		var bgTw = CreateTween();
		bgTw.TweenProperty(bg, "color", new Color(BattlefieldBg, 0.85f), 0.8f).SetTrans(Tween.TransitionType.Cubic);
		bgTw.Parallel().TweenProperty(redTint, "color:a", 0.3f, 1.0f).SetTrans(Tween.TransitionType.Cubic);

		// Content panel - larger for more stats
		var content = new Panel
		{
			ZIndex = 52,
			Modulate = new Color(1, 1, 1, 0),
			CustomMinimumSize = new Vector2(420, 440)
		};
		content.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
		content.Position -= content.CustomMinimumSize / 2;
		var style = MakeNotePanel(borderRadius: 6f, shadowSize: 10f);
		style.BgColor = new Color(NoteBg, 0.96f);
		style.BorderColor = NoteBorder;
		style.BorderWidthTop = 2;
		style.BorderWidthBottom = 2;
		style.BorderWidthLeft = 2;
		style.BorderWidthRight = 2;
		style.ShadowOffset = new Vector2(4, 4);
		content.AddThemeStyleboxOverride("panel", style);
		AddChild(content);

		var margin = new MarginContainer();
		margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 30);
		margin.AddThemeConstantOverride("margin_right", 30);
		margin.AddThemeConstantOverride("margin_top", 25);
		margin.AddThemeConstantOverride("margin_bottom", 25);
		content.AddChild(margin);

		var vbox = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		vbox.AddThemeConstantOverride("separation", 12);
		margin.AddChild(vbox);

		// Title
		var title = new Label
		{
			Text = "YOU DIED"
		};
		title.AddThemeFontSizeOverride("font_size", 38);
		title.AddThemeColorOverride("font_color", BloodRed);
		title.AddThemeConstantOverride("outline_size", 3);
		title.AddThemeColorOverride("font_outline_color", InkBlack);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(title);

		// Separator
		var sep = new ColorRect
		{
			CustomMinimumSize = new Vector2(0, 1),
			Color = new Color(0.5f, 0.15f, 0.1f, 0.4f)
		};
		vbox.AddChild(sep);

		// Stats
		var statsBox = new VBoxContainer();
		statsBox.AddThemeConstantOverride("separation", 8);
		vbox.AddChild(statsBox);

		int roundCount = _gameManager?.GetCurrentRound() ?? 0;
		int totalScore = _gameManager?.Score ?? 0;
		int kills = _gameManager?.TotalKills ?? 0;

		_AddStatRow(statsBox, "存活波数", roundCount.ToString(), BloodBright);
		_AddStatRow(statsBox, "消灭敌人", kills.ToString(), NoteText);
		_AddStatRow(statsBox, "最终得分", totalScore.ToString(), new Color(0.85f, 0.6f, 0.45f));

		int high = SaveManager.Instance != null ? SaveManager.GetHighScore() : 0;
		if (totalScore >= high && totalScore > 0)
			_AddStatRow(statsBox, "🏆 新纪录!", totalScore.ToString(), RarityEpic);
		else if (high > 0)
			_AddStatRow(statsBox, "最高分", high.ToString(), NoteTextDim);

		// Achievements section
		var achievementsLabel = new Label
		{
			Text = "━━ 成就 ━━",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		achievementsLabel.AddThemeFontSizeOverride("font_size", 12);
		achievementsLabel.AddThemeColorOverride("font_color", NoteTextDim);
		vbox.AddChild(achievementsLabel);

		// Achievement badges
		var achievementsBox = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		achievementsBox.AddThemeConstantOverride("separation", 10);
		vbox.AddChild(achievementsBox);

		// Check and display achievements
		if (roundCount >= 10)
			_AddAchievementBadge(achievementsBox, "🌟", "坚持10波", new Color(1.0f, 0.85f, 0.3f));
		if (kills >= 100)
			_AddAchievementBadge(achievementsBox, "💀", "击杀100", new Color(0.7f, 0.7f, 0.7f));
		if (totalScore >= 1000)
			_AddAchievementBadge(achievementsBox, "🏅", "得分1000", new Color(0.9f, 0.75f, 0.3f));
		if (roundCount >= 20)
			_AddAchievementBadge(achievementsBox, "👑", "坚持20波", new Color(1.0f, 0.9f, 0.4f));

		// Tips for next run
		var tipsLabel = new Label
		{
			Text = _GetTipForNextRun(roundCount, kills),
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(360, 0)
		};
		tipsLabel.AddThemeFontSizeOverride("font_size", 11);
		tipsLabel.AddThemeColorOverride("font_color", NoteTextDim);
		vbox.AddChild(tipsLabel);

		// Spacer
		var spacer = new Control { CustomMinimumSize = new Vector2(0, 8) };
		vbox.AddChild(spacer);

		// Buttons
		var btnBox = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		btnBox.AddThemeConstantOverride("separation", 16);
		vbox.AddChild(btnBox);

		var restartBtn = _MakeButton("重新开始",
			new Color(0.25f, 0.12f, 0.12f, 0.9f),
			new Color(0.35f, 0.18f, 0.18f, 0.9f),
			NoteBorder);
		restartBtn.Pressed += () => GetTree().ReloadCurrentScene();
		btnBox.AddChild(restartBtn);

		var quitBtn = _MakeButton("退出游戏",
			new Color(0.18f, 0.08f, 0.08f, 0.9f),
			new Color(0.28f, 0.14f, 0.14f, 0.9f),
			NoteBorder);
		quitBtn.Pressed += () => GetTree().Quit();
		btnBox.AddChild(quitBtn);

		// Animations
		var panelTw = CreateTween();
		panelTw.TweenInterval(0.6f);
		panelTw.TweenProperty(content, Node2D.PropertyName.Modulate.ToString() + ":a", 1.0f, 0.5f)
			.SetTrans(Tween.TransitionType.Cubic);

		var titleTw = CreateTween().SetLoops();
		titleTw.TweenProperty(title, Node2D.PropertyName.Modulate.ToString(), new Color(1.0f, 0.85f, 0.85f), 1.2f)
			.SetTrans(Tween.TransitionType.Sine);
		titleTw.TweenProperty(title, Node2D.PropertyName.Modulate.ToString(), new Color(1, 1, 1), 1.2f)
			.SetTrans(Tween.TransitionType.Sine);

		GameServices.Audio.PlayGameOver();
	}

	private void _AddAchievementBadge(HBoxContainer parent, string icon, string text, Color color)
	{
		var badge = new Label
		{
			Text = icon + " " + text,
			TooltipText = icon + " " + text
		};
		badge.AddThemeFontSizeOverride("font_size", 11);
		badge.AddThemeColorOverride("font_color", color);
		parent.AddChild(badge);
	}

	private string _GetTipForNextRun(int rounds, int kills)
	{
		if (rounds < 3)
			return "💡 提示: 多尝试不同的武器组合，找到适合你的流派！";
		if (rounds < 7)
			return "💡 提示: 注意走位和闪避，避免被包围！";
		if (kills < 50)
			return "💡 提示: 优先获取范围伤害装备来提高清怪效率！";
		if (rounds < 10)
			return "💡 提示: 吸血和护盾装备能有效提高生存能力！";
		return "💡 提示: 继续保持！你已经非常强了！";
	}

	private void _AddStatRow(VBoxContainer parent, string labelText, string valueText, Color color)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		parent.AddChild(row);

		var lbl = new Label
		{
			Text = labelText,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		lbl.AddThemeFontSizeOverride("font_size", 15);
		lbl.AddThemeColorOverride("font_color", NoteTextDim);
		row.AddChild(lbl);

		var val = new Label
		{
			Text = valueText,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		val.AddThemeFontSizeOverride("font_size", 16);
		val.AddThemeColorOverride("font_color", color);
		val.AddThemeConstantOverride("outline_size", 1);
		val.AddThemeColorOverride("font_outline_color", InkBlack);
		row.AddChild(val);
	}

	private Button _MakeButton(string text, Color bgColor, Color hoverColor, Color borderColor)
	{
		var btn = new Button
		{
			Text = text,
			FocusMode = Control.FocusModeEnum.None,
			CustomMinimumSize = new Vector2(140, 40)
		};
		btn.AddThemeFontSizeOverride("font_size", 15);
		btn.AddThemeColorOverride("font_color", NoteBg);

		var s = MakeNotePanel(borderRadius: 4f, shadowSize: 3f);
		s.BgColor = bgColor;
		s.BorderColor = borderColor;
		btn.AddThemeStyleboxOverride("normal", s);

		var h = MakeNotePanel(borderRadius: 4f, shadowSize: 3f);
		h.BgColor = hoverColor;
		h.BorderColor = borderColor;
		btn.AddThemeStyleboxOverride("hover", h);

		var p = MakeNotePanel(borderRadius: 4f, shadowSize: 3f);
		p.BgColor = _Darken(bgColor, 0.1f);
		p.BorderColor = borderColor;
		btn.AddThemeStyleboxOverride("pressed", p);
		return btn;
	}

	private static Color _Darken(Color c, float amount)
	{
		return new Color(
			Mathf.Max(0.0f, c.R - amount),
			Mathf.Max(0.0f, c.G - amount),
			Mathf.Max(0.0f, c.B - amount),
			c.A);
	}
}
