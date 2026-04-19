namespace ZombieWars.UI;

using Godot;
using ZombieWars.Core;
using ZombieWars.Audio;

/// <summary>
/// Game Over screen — death fade-in, stats, restart / quit buttons.
/// </summary>
public partial class GameOverUI : Control
{
	private GameManager? _gameManager;

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
			Color = new Color(0.3f, 0.02f, 0.02f, 0)
		};
		redTint.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(redTint);

		var bgTw = CreateTween();
		bgTw.TweenProperty(bg, "color:a", 0.75f, 0.8f).SetTrans(Tween.TransitionType.Cubic);
		bgTw.Parallel().TweenProperty(redTint, "color:a", 0.25f, 1.0f).SetTrans(Tween.TransitionType.Cubic);

		// Content panel
		var content = new Panel
		{
			ZIndex = 52,
			Modulate = new Color(1, 1, 1, 0),
			CustomMinimumSize = new Vector2(380, 360)
		};
		content.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
		content.Position -= content.CustomMinimumSize / 2;
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.06f, 0.04f, 0.04f, 0.92f),
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			BorderWidthTop = 2,
			BorderWidthBottom = 2,
			BorderWidthLeft = 2,
			BorderWidthRight = 2,
			BorderColor = new Color(0.5f, 0.1f, 0.08f, 0.5f)
		};
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
		vbox.AddThemeConstantOverride("separation", 14);
		margin.AddChild(vbox);

		// Title
		var title = new Label
		{
			Text = "YOU DIED"
		};
		title.AddThemeFontSizeOverride("font_size", 38);
		title.AddThemeColorOverride("font_color", new Color(0.8f, 0.12f, 0.08f));
		title.AddThemeConstantOverride("outline_size", 3);
		title.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
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
		int killed = _gameManager?.Score ?? 0;

		_AddStatRow(statsBox, "存活波数", roundCount.ToString(), new Color(0.9f, 0.78f, 0.35f));
		_AddStatRow(statsBox, "消灭数量", killed.ToString(), new Color(0.75f, 0.75f, 0.75f));
		_AddStatRow(statsBox, "最终得分", totalScore.ToString(), new Color(1.0f, 0.85f, 0.2f));

		int high = SaveManager.Instance != null ? SaveManager.GetHighScore() : 0;
		if (totalScore >= high && totalScore > 0)
			_AddStatRow(statsBox, "新纪录!", totalScore.ToString(), new Color(1.0f, 0.6f, 0.1f));
		else
			_AddStatRow(statsBox, "最高分", high.ToString(), new Color(0.6f, 0.6f, 0.55f));

		// Spacer
		var spacer = new Control { CustomMinimumSize = new Vector2(0, 10) };
		vbox.AddChild(spacer);

		// Buttons
		var btnBox = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center
		};
		btnBox.AddThemeConstantOverride("separation", 16);
		vbox.AddChild(btnBox);

		var restartBtn = _MakeButton("重新开始", new Color(0.18f, 0.55f, 0.25f));
		restartBtn.Pressed += () => GetTree().ReloadCurrentScene();
		btnBox.AddChild(restartBtn);

		var quitBtn = _MakeButton("退出游戏", new Color(0.5f, 0.15f, 0.12f));
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

		SoundManager.Instance?.PlayGameOver();
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
		lbl.AddThemeColorOverride("font_color", new Color(0.65f, 0.65f, 0.6f));
		row.AddChild(lbl);

		var val = new Label
		{
			Text = valueText,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		val.AddThemeFontSizeOverride("font_size", 16);
		val.AddThemeColorOverride("font_color", color);
		val.AddThemeConstantOverride("outline_size", 1);
		val.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.6f));
		row.AddChild(val);
	}

	private Button _MakeButton(string text, Color bgColor)
	{
		var btn = new Button
		{
			Text = text,
			FocusMode = Control.FocusModeEnum.None,
			CustomMinimumSize = new Vector2(140, 40)
		};
		btn.AddThemeFontSizeOverride("font_size", 15);

		var s = new StyleBoxFlat
		{
			BgColor = bgColor,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
			BorderWidthTop = 1,
			BorderWidthBottom = 1,
			BorderWidthLeft = 1,
			BorderWidthRight = 1,
			BorderColor = new Color(1, 1, 1, 0.1f)
		};
		btn.AddThemeStyleboxOverride("normal", s);

		var h = new StyleBoxFlat
		{
			BgColor = _Lighten(bgColor, 0.2f),
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
			BorderWidthTop = 1,
			BorderWidthBottom = 1,
			BorderWidthLeft = 1,
			BorderWidthRight = 1,
			BorderColor = new Color(1, 1, 1, 0.1f)
		};
		btn.AddThemeStyleboxOverride("hover", h);

		var p = new StyleBoxFlat
		{
			BgColor = _Darken(bgColor, 0.15f),
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
			BorderWidthTop = 1,
			BorderWidthBottom = 1,
			BorderWidthLeft = 1,
			BorderWidthRight = 1,
			BorderColor = new Color(1, 1, 1, 0.1f)
		};
		btn.AddThemeStyleboxOverride("pressed", p);
		return btn;
	}

	private static Color _Lighten(Color c, float amount)
	{
		return new Color(
			Mathf.Min(1.0f, c.R + amount),
			Mathf.Min(1.0f, c.G + amount),
			Mathf.Min(1.0f, c.B + amount),
			c.A);
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
