namespace ZombieWars.UI;

using System;
using Godot;

/// <summary>
/// 游戏启动欢迎面板：标题 + 副标题 + 玩法说明 + "开始游戏" / "操作说明" 按钮。
/// Setup 注入开始/帮助回调；状态由 Show / Hide 显式控制。
/// </summary>
public partial class WelcomePanel : Panel
{
    private Action _onStart = null!;
    private Action _onToggleHelp = null!;

    public WelcomePanel()
    {
        Name = "WelcomePanel";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var styleBox = new StyleBoxFlat { BgColor = new Color(HandDrawnTheme.Colors.BattlefieldBg, 0.85f) };
        AddThemeStyleboxOverride("panel", styleBox);
    }

    public void Setup(Action onStart, Action onToggleHelp)
    {
        _onStart = onStart;
        _onToggleHelp = onToggleHelp;
    }

    public override void _Ready()
    {
        var marginContainer = new MarginContainer();
        marginContainer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        marginContainer.AddThemeConstantOverride("margin_left", 50);
        marginContainer.AddThemeConstantOverride("margin_right", 50);
        marginContainer.AddThemeConstantOverride("margin_top", 30);
        marginContainer.AddThemeConstantOverride("margin_bottom", 30);
        AddChild(marginContainer);

        var centerContainer = new CenterContainer();
        centerContainer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        marginContainer.AddChild(centerContainer);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 25);
        centerContainer.AddChild(vbox);

        var title = new Label { Text = "🧟 僵尸大战 🧟" };
        title.AddThemeFontSizeOverride("font_size", 36);
        title.AddThemeColorOverride("font_color", HandDrawnTheme.Colors.BloodBright);
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
        var (startNormal, startHover, startPressed) = HandDrawnTheme.MakeButtonStyles(
            new Color(0.25f, 0.12f, 0.12f, 0.9f),
            new Color(0.35f, 0.18f, 0.18f, 0.9f),
            HandDrawnTheme.Colors.NoteBorder,
            radius: 4, shadowSize: 4);
        startButton.AddThemeStyleboxOverride("normal", startNormal);
        startButton.AddThemeStyleboxOverride("hover", startHover);
        startButton.AddThemeStyleboxOverride("pressed", startPressed);
        startButton.AddThemeColorOverride("font_color", HandDrawnTheme.Colors.NoteBg);
        startButton.Pressed += _onStart;
        vbox.AddChild(startButton);

        var helpButton = new Button
        {
            Text = "操作说明",
            CustomMinimumSize = new Vector2(120, 40)
        };
        helpButton.AddThemeFontSizeOverride("font_size", 16);
        var (helpNormal, helpHover, helpPressed) = HandDrawnTheme.MakeButtonStyles(
            new Color(0.2f, 0.15f, 0.15f, 0.9f),
            new Color(0.3f, 0.22f, 0.22f, 0.9f),
            HandDrawnTheme.Colors.NoteBorder,
            radius: 3, shadowSize: 3);
        helpButton.AddThemeStyleboxOverride("normal", helpNormal);
        helpButton.AddThemeStyleboxOverride("hover", helpHover);
        helpButton.AddThemeStyleboxOverride("pressed", helpPressed);
        helpButton.AddThemeColorOverride("font_color", HandDrawnTheme.Colors.NoteBg);
        helpButton.Pressed += _onToggleHelp;
        vbox.AddChild(helpButton);
    }
}
