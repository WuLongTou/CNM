namespace ZombieWars.UI;

using Godot;
using System.Collections.Generic;
using ZombieWars.Core;
using static ZombieWars.Core.GameConfig;
using static ZombieWars.UI.HandDrawnTheme;
using static ZombieWars.UI.HandDrawnTheme.Colors;

/// <summary>工具提示系统 — 从 UIManager 提取的独立组件。</summary>
public partial class TooltipSystem : Control
{
    private Panel _panel = null!;
    private RichTextLabel _label = null!;
    private Timer? _hideTimer;
    private float _maxWidth = GameConfig.TooltipMaxWidth;
    private Vector2 _offset = GameConfig.TooltipOffset;

    public override void _Ready()
    {
        var viewport = ResponsiveUi.ViewportSize(this);
        _panel = new Panel
        {
            Visible = false,
            ZIndex = 200,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ProcessMode = ProcessModeEnum.Always
        };
        var style = MakeNotePanel(shadowSize: 6f);
        style.BgColor = new Color(NoteBg, 0.98f);
        style.ShadowSize = 8;
        style.ShadowOffset = new Vector2(3, 3);
        _panel.AddThemeStyleboxOverride("panel", style);

        _label = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(ResponsiveUi.FitWidth(320, viewport, 0.48f), 0)
        };
        _label.AddThemeFontSizeOverride("normal_font_size", ResponsiveUi.Font(14, viewport, 10));
        _label.AddThemeFontSizeOverride("bold_font_size", ResponsiveUi.Font(15, viewport, 11));
        _label.AddThemeColorOverride("default_color", NoteText);

        var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", ResponsiveUi.Px(UiMarginXLarge, viewport, 8));
        margin.AddThemeConstantOverride("margin_right", ResponsiveUi.Px(UiMarginXLarge, viewport, 8));
        margin.AddThemeConstantOverride("margin_top", ResponsiveUi.Px(12, viewport, 6));
        margin.AddThemeConstantOverride("margin_bottom", ResponsiveUi.Px(12, viewport, 6));
        _panel.AddChild(margin);
        margin.AddChild(_label);
        AddChild(_panel);

        _hideTimer = new Timer { OneShot = true, WaitTime = 0.2f };
        _hideTimer.Timeout += () => _panel.Visible = false;
        AddChild(_hideTimer);
    }

    public void Show(string text)
    {
        if (_hideTimer != null && !_hideTimer.IsStopped()) _hideTimer.Stop();
        _label.Text = text;
        _label.ResetSize();

        var screenSize = GetViewport().GetVisibleRect().Size;
        float contentWidth = Mathf.Min(_maxWidth, Mathf.Max(ResponsiveUi.Px(260, screenSize, 180), screenSize.X * 0.32f));
        float contentHeight = Mathf.Max(_label.GetContentHeight(), ResponsiveUi.Px(100, screenSize, 72));
        float panelWidth = contentWidth + ResponsiveUi.Px(32, screenSize, 16);
        float panelHeight = contentHeight + ResponsiveUi.Px(24, screenSize, 12);

        _label.CustomMinimumSize = new Vector2(contentWidth, contentHeight);
        _panel.CustomMinimumSize = new Vector2(panelWidth, panelHeight);
        _panel.Size = new Vector2(panelWidth, panelHeight);
        _Position(screenSize);
        _panel.Visible = true;
    }

    public void HideTooltip()
    {
        _panel.Visible = false;
        if (_hideTimer != null && !_hideTimer.IsStopped()) _hideTimer.Stop();
    }

    public override void _Process(double delta)
    {
        // 工具提示可见时跟随鼠标位置
        if (_panel.Visible)
            _Position(GetViewport().GetVisibleRect().Size);
    }

    private void _Position(Vector2 screenSize)
    {
        var mousePos = GetViewport().GetMousePosition();
        var panelSize = _panel.GetCombinedMinimumSize();

        var candidates = new List<Vector2>
        {
            mousePos + _offset,
            mousePos + new Vector2(_offset.X, -panelSize.Y - _offset.Y),
            mousePos + new Vector2(-panelSize.X - _offset.X, _offset.Y),
            mousePos + new Vector2(-panelSize.X - _offset.X, -panelSize.Y - _offset.Y)
        };

        var bestPos = mousePos + _offset;
        float bestDist = Mathf.Inf;
        foreach (var pos in candidates)
        {
            if (pos.X >= 0 && pos.Y >= 0 &&
                pos.X + panelSize.X <= screenSize.X &&
                pos.Y + panelSize.Y <= screenSize.Y)
            {
                var dist = pos.DistanceTo(mousePos);
                if (dist < bestDist) { bestDist = dist; bestPos = pos; }
            }
        }

        if (bestDist < Mathf.Inf) { _panel.Position = bestPos; return; }
        _panel.Position = new Vector2(
            Mathf.Clamp(mousePos.X + _offset.X, ResponsiveUi.Px(10, screenSize, 5), screenSize.X - panelSize.X - ResponsiveUi.Px(10, screenSize, 5)),
            Mathf.Clamp(mousePos.Y + _offset.Y, ResponsiveUi.Px(10, screenSize, 5), screenSize.Y - panelSize.Y - ResponsiveUi.Px(10, screenSize, 5))
        );
    }
}
