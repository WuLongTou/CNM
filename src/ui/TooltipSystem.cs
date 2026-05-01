namespace ZombieWars.UI;

using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Tooltip system for displaying weapon and upgrade card information.
/// </summary>
public partial class TooltipSystem : Control
{
    private Panel _tooltipPanel = null!;
    private RichTextLabel _tooltipLabel = null!;
    private Timer? _hideTimer;
    private float _maxWidth = 360.0f;
    private Vector2 _offset = new(15.0f, 15.0f);

    public override void _Ready()
    {
        _CreateTooltipPanel();
        MouseFilter = Control.MouseFilterEnum.Ignore;
        ProcessMode = ProcessModeEnum.Always;
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

        var style = HandDrawnTheme.MakeNotePanel(shadowSize: 6f);
        style.BgColor = new Color(HandDrawnTheme.Colors.NoteBg, 0.98f);
        style.ShadowSize = 8;
        style.ShadowOffset = new Vector2(3, 3);
        _tooltipPanel.AddThemeStyleboxOverride("panel", style);

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
        _tooltipLabel.AddThemeColorOverride("default_color", HandDrawnTheme.Colors.NoteText);

        var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        _tooltipPanel.AddChild(margin);
        margin.AddChild(_tooltipLabel);

        AddChild(_tooltipPanel);

        _hideTimer = new Timer { OneShot = true, WaitTime = 0.2f };
        _hideTimer.Timeout += _HideTooltipPanel;
        AddChild(_hideTimer);
    }

    public void ShowTooltip(string text)
    {
        if (_tooltipPanel == null || _tooltipLabel == null) return;

        if (_hideTimer != null && !_hideTimer.IsStopped())
            _hideTimer.Stop();

        _tooltipLabel.Text = text;
        _tooltipLabel.ResetSize();

        var screenSize = GetViewport().GetVisibleRect().Size;
        float contentWidth = Mathf.Min(_maxWidth, Mathf.Max(260.0f, screenSize.X * 0.32f));
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
            mousePos + _offset,
            mousePos + new Vector2(_offset.X, -panelSize.Y - _offset.Y),
            mousePos + new Vector2(-panelSize.X - _offset.X, _offset.Y),
            mousePos + new Vector2(-panelSize.X - _offset.X, -panelSize.Y - _offset.Y)
        };

        var bestPos = mousePos + _offset;
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
            Mathf.Clamp(mousePos.X + _offset.X, 10, screenSize.X - panelSize.X - 10),
            Mathf.Clamp(mousePos.Y + _offset.Y, 10, screenSize.Y - panelSize.Y - 10)
        );
    }

    public void HideTooltip()
    {
        if (_tooltipPanel != null)
            _tooltipPanel.Visible = false;
        if (_hideTimer != null && !_hideTimer.IsStopped())
            _hideTimer.Stop();
    }

    private void _HideTooltipPanel()
    {
        if (_tooltipPanel != null)
            _tooltipPanel.Visible = false;
    }

    public void SetMaxWidth(float width) => _maxWidth = width;
    public void SetOffset(Vector2 offset) => _offset = offset;
}