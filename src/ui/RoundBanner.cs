namespace ZombieWars.UI;

using Godot;

/// <summary>
/// 新回合开始时的"第 N 回合"横幅：血红色发光背景 + 主标题 + 副标题。
/// 入场有 overshoot 弹跳 + 发光脉冲；非持久模式下自动淡出。
/// </summary>
public partial class RoundBanner : CenterContainer
{
    public const float BannerFixedHeight = 100.0f;
    public const float BannerMaxWidth = 460.0f;
    public const float BannerViewportPadding = 40.0f;

    private Panel _panel = null!;
    private ColorRect _glow = null!;
    private Label _title = null!;
    private Label _sub = null!;
    private Tween? _activeTween;

    public RoundBanner()
    {
        Name = "RoundBanner";
        ZIndex = 48;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public override void _Ready()
    {
        var size = new Vector2(BannerMaxWidth, BannerFixedHeight);

        _glow = new ColorRect
        {
            Visible = false,
            Color = new Color(HandDrawnTheme.Colors.BloodBright, 0.0f),
            CustomMinimumSize = size,
            PivotOffset = size / 2.0f
        };
        AddChild(_glow);

        _panel = new Panel
        {
            Visible = false,
            ZIndex = 49,
            CustomMinimumSize = size,
            PivotOffset = size / 2.0f
        };
        var panelStyle = HandDrawnTheme.MakeNotePanel(borderRadius: 8f, shadowSize: 12f);
        panelStyle.BgColor = new Color(HandDrawnTheme.Colors.BattlefieldBg, 0.92f);
        panelStyle.BorderColor = new Color(HandDrawnTheme.Colors.BloodBright, 0.8f);
        panelStyle.BorderWidthTop = 2;
        panelStyle.BorderWidthBottom = 2;
        panelStyle.BorderWidthLeft = 2;
        panelStyle.BorderWidthRight = 2;
        panelStyle.ShadowColor = new Color(HandDrawnTheme.Colors.BloodBright, 0.25f);
        panelStyle.ShadowOffset = new Vector2(4, 4);
        _panel.AddThemeStyleboxOverride("panel", panelStyle);
        AddChild(_panel);

        _title = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _title.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _title.AddThemeFontSizeOverride("font_size", 36);
        _title.AddThemeColorOverride("font_color", HandDrawnTheme.Colors.BloodBright);
        _title.AddThemeConstantOverride("outline_size", 6);
        _title.AddThemeColorOverride("font_outline_color", HandDrawnTheme.Colors.InkBlack);
        _panel.AddChild(_title);

        _sub = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        _sub.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _sub.AddThemeFontSizeOverride("font_size", 14);
        _sub.AddThemeColorOverride("font_color", new Color(HandDrawnTheme.Colors.NoteText, 0.7f));
        _sub.AddThemeConstantOverride("outline_size", 2);
        _sub.AddThemeColorOverride("font_outline_color", HandDrawnTheme.Colors.InkBlack);
        _panel.AddChild(_sub);

        OnViewportSizeChanged(ResponsiveUi.ViewportSize(this));
    }

    /// <summary>显示回合横幅；persistent=true 时不自动淡出，由调用方负责 Hide。</summary>
    public void Show(string text, bool persistent)
    {
        if (_activeTween != null && _activeTween.IsValid())
            _activeTween.Kill();

        _title.Text = text;
        _sub.Text = "";
        _panel.Visible = true;
        _panel.Modulate = new Color(1, 1, 1, 0);
        _panel.Scale = new Vector2(0.3f, 0.3f);
        _glow.Visible = true;
        _glow.Color = new Color(HandDrawnTheme.Colors.BloodBright, 0.0f);
        _glow.Scale = new Vector2(0.3f, 0.3f);

        _activeTween = CreateTween();
        _activeTween.SetParallel(true);
        _activeTween.TweenProperty(_panel, "scale", new Vector2(1.05f, 1.05f), 0.3f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _activeTween.TweenProperty(_panel, Node2D.PropertyName.Modulate.ToString() + ":a", 1.0f, 0.2f)
            .SetTrans(Tween.TransitionType.Cubic);
        _activeTween.TweenProperty(_glow, "scale", new Vector2(1.6f, 1.6f), 0.3f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _activeTween.TweenProperty(_glow, "color:a", 0.4f, 0.2f)
            .SetTrans(Tween.TransitionType.Cubic);

        _activeTween.SetParallel(false);
        _activeTween.TweenProperty(_panel, "scale", new Vector2(1.0f, 1.0f), 0.1f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _activeTween.Parallel().TweenProperty(_glow, "color:a", 0.0f, 0.2f);

        if (!persistent)
        {
            _activeTween.TweenInterval(0.6f);
            _activeTween.SetParallel(true);
            _activeTween.TweenProperty(_panel, "scale", new Vector2(1.8f, 1.8f), 0.3f)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
            _activeTween.TweenProperty(_panel, Node2D.PropertyName.Modulate.ToString() + ":a", 0.0f, 0.3f)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
            _activeTween.TweenProperty(_glow, "scale", new Vector2(2.5f, 2.5f), 0.3f)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
            _activeTween.TweenProperty(_glow, "color:a", 0.0f, 0.3f)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
            _activeTween.SetParallel(false);
            _activeTween.TweenCallback(Callable.From(() =>
            {
                _panel.Visible = false;
                _glow.Visible = false;
            }));
        }
    }

    public new void Hide()
    {
        if (_activeTween != null && _activeTween.IsValid())
            _activeTween.Kill();
        _panel.Visible = false;
        _glow.Visible = false;
    }

    /// <summary>视口尺寸变化时调用，重新计算 banner 尺寸上限。</summary>
    public void OnViewportSizeChanged(Vector2 viewportSize)
    {
        float padding = ResponsiveUi.Px(BannerViewportPadding, viewportSize, 18);
        var size = new Vector2(
            Mathf.Min(BannerMaxWidth * ResponsiveUi.Scale(viewportSize), viewportSize.X - padding),
            ResponsiveUi.Px(BannerFixedHeight, viewportSize, 72));
        _panel.CustomMinimumSize = size;
        _panel.PivotOffset = size / 2.0f;
        _glow.CustomMinimumSize = size;
        _glow.PivotOffset = size / 2.0f;
        _title.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(36, viewportSize, 24));
        _title.AddThemeConstantOverride("outline_size", ResponsiveUi.Px(6, viewportSize, 3));
        _sub.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(14, viewportSize, 10));
        _sub.AddThemeConstantOverride("outline_size", ResponsiveUi.Px(2, viewportSize, 1));
    }
}
