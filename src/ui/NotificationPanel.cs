namespace ZombieWars.UI;

using Godot;
using ZombieWars.Core;
using static ZombieWars.Core.GameConfig;
using static ZombieWars.UI.HandDrawnTheme;
using static ZombieWars.UI.HandDrawnTheme.Colors;

/// <summary>通知面板 — 处理升级/连击/成就等顶部弹出通知。</summary>
public partial class NotificationPanel : Control
{
    private Panel? _panel;
    private Label? _label;
    private Tween? _tween;

    public override void _Ready()
    {
        var viewport = ResponsiveUi.ViewportSize(this);
        _panel = new Panel
        {
            Visible = false,
            ZIndex = 60,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        _panel.OffsetTop = ResponsiveUi.Px(90, viewport, 64);
        _panel.OffsetBottom = _panel.OffsetTop + ResponsiveUi.Px(40, viewport, 30);
        _panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0) });
        AddChild(_panel);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _panel.AddChild(center);

        _label = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _label.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontTitle, viewport, 15));
        _label.AddThemeConstantOverride("outline_size", ResponsiveUi.Px(UiOutlineHero, viewport, 2));
        _label.AddThemeColorOverride("font_outline_color", InkBlack);
        center.AddChild(_label);
    }

    public void Show(string text, Color color)
    {
        if (_panel == null || _label == null) return;
        if (_tween != null && _tween.IsRunning()) _tween.Kill();

        _label.Text = text;
        _label.AddThemeColorOverride("font_color", color);
        _panel.Visible = true;
        _panel.Modulate = _panel.Modulate with { A = 0.0f };
        _panel.Scale = new Vector2(0.5f, 0.5f);

        _tween = CreateTween();
        _tween.SetParallel(true);
        _tween.TweenProperty(_panel, Control.PropertyName.Modulate.ToString() + ":a", 1.0f, 0.2f);
        _tween.TweenProperty(_panel, "scale", new Vector2(1.1f, 1.1f), 0.2f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _tween.TweenProperty(_panel, "scale", new Vector2(1.0f, 1.0f), 0.1f);
        _tween.TweenInterval(1.2f);
        _tween.SetParallel(true);
        _tween.TweenProperty(_panel, Control.PropertyName.Modulate.ToString() + ":a", 0.0f, 0.4f);
        _tween.SetParallel(false);
        _tween.TweenCallback(Callable.From(() => { if (_panel != null) _panel.Visible = false; }));
    }
}
