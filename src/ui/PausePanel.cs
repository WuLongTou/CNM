namespace ZombieWars.UI;

using Godot;

/// <summary>
/// 全屏暂停遮罩 + 居中"暂停中"提示卡。仅在 Visible 时拦截视线与点击。
/// 状态由调用方（UIManager）通过 Show / Hide 显式控制。
/// </summary>
public partial class PausePanel : Panel
{
    public PausePanel()
    {
        Name = "PausePanel";
        Visible = false;
        ZIndex = 90;
        MouseFilter = MouseFilterEnum.Ignore;
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var styleBox = new StyleBoxFlat { BgColor = new Color(HandDrawnTheme.Colors.BattlefieldBg, 0.7f) };
        AddThemeStyleboxOverride("panel", styleBox);
    }

    public override void _Ready()
    {
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);

        var pauseCard = new Panel();
        pauseCard.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        var pauseCardStyle = HandDrawnTheme.MakeNotePanel(borderRadius: 5f, shadowSize: 8f);
        pauseCard.AddThemeStyleboxOverride("panel", pauseCardStyle);
        center.AddChild(pauseCard);

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

        var pauseLabel = new Label
        {
            Text = "⏸ 暂停中\n按 ESC 继续",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        pauseLabel.AddThemeFontSizeOverride("font_size", 28);
        pauseLabel.AddThemeColorOverride("font_color", HandDrawnTheme.Colors.NoteText);
        pauseMargin.AddChild(pauseLabel);
    }
}
