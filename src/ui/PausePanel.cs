namespace ZombieWars.UI;

using Godot;
using System;
using static ZombieWars.UI.HandDrawnTheme;
using static ZombieWars.UI.HandDrawnTheme.Colors;

/// <summary>
/// 全屏暂停遮罩 + 居中提示卡（含继续按钮）。
/// 状态由调用方（UIManager）通过 Visible 显式控制。
/// </summary>
public partial class PausePanel : Panel
{
    public event Action? ResumeRequested;
    public event Action? SettingsRequested;

    public PausePanel()
    {
        Name = "PausePanel";
        Visible = false;
        ZIndex = 90;
        MouseFilter = MouseFilterEnum.Ignore;
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var styleBox = new StyleBoxFlat { BgColor = new Color(BattlefieldBg, 0.75f) };
        AddThemeStyleboxOverride("panel", styleBox);
    }

    public override void _Ready()
    {
        var viewport = ResponsiveUi.ViewportSize(this);
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);

        var card = new Panel();
        card.CustomMinimumSize = ResponsiveUi.FitPanel(new Vector2(320, 220), viewport, 0.72f, 0.5f);
        var cardStyle = MakeNotePanel(borderRadius: 8f, shadowSize: 10f);
        cardStyle.ShadowOffset = new Vector2(3, 3);
        card.AddThemeStyleboxOverride("panel", cardStyle);
        center.AddChild(card);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", ResponsiveUi.Px(30, viewport, 18));
        margin.AddThemeConstantOverride("margin_right", ResponsiveUi.Px(30, viewport, 18));
        margin.AddThemeConstantOverride("margin_top", ResponsiveUi.Px(24, viewport, 16));
        margin.AddThemeConstantOverride("margin_bottom", ResponsiveUi.Px(24, viewport, 16));
        card.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", ResponsiveUi.Px(16, viewport, 10));
        margin.AddChild(vbox);

        var titleLabel = new Label
        {
            Text = "暂停中",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        titleLabel.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(28, viewport, 22));
        titleLabel.AddThemeColorOverride("font_color", BattlefieldBright);
        titleLabel.AddThemeConstantOverride("outline_size", 3);
        titleLabel.AddThemeColorOverride("font_outline_color", InkBlack);
        vbox.AddChild(titleLabel);

        var resumeBtn = new Button
        {
            Text = "继续 [ESC]",
            CustomMinimumSize = new Vector2(0, ResponsiveUi.Px(40, viewport, 32)),
            FocusMode = FocusModeEnum.None
        };
        resumeBtn.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(16, viewport, 13));
        var (n, h, _) = MakeButtonStyles(
            new Color(BattlefieldBg, 0.9f), new Color(BattlefieldMid, 0.95f),
            BattlefieldAccent, radius: 5);
        resumeBtn.AddThemeStyleboxOverride("normal", n);
        resumeBtn.AddThemeStyleboxOverride("hover", h);
        resumeBtn.AddThemeStyleboxOverride("pressed", h);
        resumeBtn.AddThemeColorOverride("font_color", BattlefieldBright);
        resumeBtn.Pressed += () => ResumeRequested?.Invoke();
        vbox.AddChild(resumeBtn);

        // 设置入口
        var settingsBtn = new Button
        {
            Text = "设置",
            CustomMinimumSize = new Vector2(0, ResponsiveUi.Px(34, viewport, 28)),
            FocusMode = FocusModeEnum.None
        };
        settingsBtn.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(14, viewport, 11));
        var (sn, sh, _) = MakeButtonStyles(
            new Color(BattlefieldBg, 0.85f), new Color(BattlefieldMid, 0.90f),
            BattlefieldAccent, radius: 4, shadowSize: 2);
        settingsBtn.AddThemeStyleboxOverride("normal", sn);
        settingsBtn.AddThemeStyleboxOverride("hover", sh);
        settingsBtn.AddThemeStyleboxOverride("pressed", sh);
        settingsBtn.AddThemeColorOverride("font_color", NoteBg);
        settingsBtn.Pressed += () => SettingsRequested?.Invoke();
        vbox.AddChild(settingsBtn);

        var hintLabel = new Label
        {
            Text = "H 帮助 · C 装备",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        hintLabel.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(12, viewport, 10));
        hintLabel.AddThemeColorOverride("font_color", new Color(NoteText, 0.5f));
        vbox.AddChild(hintLabel);
    }
}
