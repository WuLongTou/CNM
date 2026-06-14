namespace ZombieWars.UI;

using System;
using Godot;
using ZombieWars.Core;

/// <summary>
/// 游戏启动欢迎面板：标题 + 副标题 + 玩法说明 + "开始游戏" / "操作说明" 按钮。
/// Setup 注入开始/帮助回调；状态由 Show / Hide 显式控制。
/// </summary>
public partial class WelcomePanel : Panel
{
    private const string BackgroundArtPath = "res://assets/sprites/ui/alchemy_bg_001.jpg";
    private const string HeroArtPath = "res://assets/sprites/ui/alchemist_hero_001.jpg";

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
        var viewport = ResponsiveUi.ViewportSize(this);
        _CreateBackgroundArt();

        var marginContainer = new MarginContainer();
        marginContainer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        marginContainer.AddThemeConstantOverride("margin_left", ResponsiveUi.Px(50, viewport, 18));
        marginContainer.AddThemeConstantOverride("margin_right", ResponsiveUi.Px(50, viewport, 18));
        marginContainer.AddThemeConstantOverride("margin_top", ResponsiveUi.Px(30, viewport, 12));
        marginContainer.AddThemeConstantOverride("margin_bottom", ResponsiveUi.Px(30, viewport, 12));
        AddChild(marginContainer);

        var centerContainer = new CenterContainer();
        centerContainer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        marginContainer.AddChild(centerContainer);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", ResponsiveUi.Px(16, viewport, 6));
        centerContainer.AddChild(vbox);

        var title = new Label { Text = "🧟 僵尸大战 🧟" };
        title.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(36, viewport, 24));
        title.AddThemeColorOverride("font_color", HandDrawnTheme.Colors.BloodBright);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        var subtitle = new Label { Text = "Zombie vs Survivors" };
        subtitle.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(18, viewport, 13));
        subtitle.AddThemeColorOverride("font_color", new Color(0.75f, 0.6f, 0.6f));
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(subtitle);

        if (!ResponsiveUi.Compact(viewport))
            _AddHeroArt(vbox, viewport);

        string[] introTexts = {
            "炼金荒原正在被尸潮吞没。",
            "选择强化、完成战术目标、在变化的战场中活下去。",
            "移动/攻击之外，你还可以用力场和天气节奏反制尸群。",
            "按 H 查看 HUD 图例，按 C 查看装备详情。"
        };

        foreach (var text in introTexts)
        {
            var label = new Label { Text = text };
            label.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(15, viewport, 11));
            label.AddThemeColorOverride("font_color", new Color(0.7f, 0.55f, 0.55f));
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.CustomMinimumSize = new Vector2(ResponsiveUi.FitWidth(620, viewport, 0.86f), 0);
            vbox.AddChild(label);
        }

        var startButton = new Button
        {
            Text = "开始游戏",
            CustomMinimumSize = ResponsiveUi.Size(180, 50, viewport, 32)
        };
        startButton.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(22, viewport, 16));
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
            CustomMinimumSize = ResponsiveUi.Size(120, 40, viewport, 28)
        };
        helpButton.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(16, viewport, 12));
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

    private void _CreateBackgroundArt()
    {
        if (!FileAccess.FileExists(BackgroundArtPath))
            return;

        var texture = TextureUtils.LoadTexture(BackgroundArtPath);
        if (texture == null)
            return;

        var art = new TextureRect
        {
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            Modulate = new Color(1.0f, 0.82f, 0.72f, 0.28f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(art);
        MoveChild(art, 0);
    }

    private void _AddHeroArt(VBoxContainer parent, Vector2 viewport)
    {
        if (!FileAccess.FileExists(HeroArtPath))
            return;

        var texture = TextureUtils.LoadTexture(HeroArtPath);
        if (texture == null)
            return;

        var heroWrap = new CenterContainer
        {
            CustomMinimumSize = new Vector2(0, ResponsiveUi.Px(118, viewport, 78)),
            MouseFilter = MouseFilterEnum.Ignore
        };
        var hero = new TextureRect
        {
            Texture = texture,
            CustomMinimumSize = ResponsiveUi.Size(112, 112, viewport, 72),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            MouseFilter = MouseFilterEnum.Ignore
        };
        heroWrap.AddChild(hero);
        parent.AddChild(heroWrap);
    }
}
