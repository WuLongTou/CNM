namespace ZombieWars.UI;

using System;
using Godot;
using ZombieWars.Audio;
using static ZombieWars.Core.GameConfig;
using static ZombieWars.UI.HandDrawnTheme;
using static ZombieWars.UI.HandDrawnTheme.Colors;

/// <summary>
/// 设置面板：全屏模态遮罩 + 居中卡片。
/// 当前实现:音量三滑条(Master/SFX/Music) + 屏幕/键位/语言占位说明。
/// 由 PausePanel 的"设置"按钮打开;按 ESC 或点击关闭按钮关闭。
/// </summary>
public partial class SettingsPanel : Control
{
    /// <summary>用户关闭面板时触发(关闭按钮 / ESC)。UIManager 监听此事件以决定是否恢复 PausePanel。</summary>
    public event Action? CloseRequested = delegate { };

    private HSlider _masterSlider = null!;
    private HSlider _sfxSlider = null!;
    private HSlider _musicSlider = null!;

    public override void _Ready()
    {
        var viewport = ResponsiveUi.ViewportSize(this);

        Visible = false;
        ZIndex = 200; // 高于 PausePanel(90)
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ProcessMode = ProcessModeEnum.Always;

        // 暗背景遮罩
        var bg = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.78f),
            MouseFilter = MouseFilterEnum.Stop
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        // 居中卡片
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(center);

        var card = new Panel();
        card.CustomMinimumSize = ResponsiveUi.FitPanel(new Vector2(480, 520), viewport, 0.78f, 0.85f);
        card.MouseFilter = MouseFilterEnum.Stop;
        var cardStyle = MakeNotePanel(borderRadius: 8f, shadowSize: 10f);
        cardStyle.ShadowOffset = new Vector2(4, 4);
        card.AddThemeStyleboxOverride("panel", cardStyle);
        center.AddChild(card);

        // 卡片内边距
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", ResponsiveUi.Px(30, viewport, 18));
        margin.AddThemeConstantOverride("margin_right", ResponsiveUi.Px(30, viewport, 18));
        margin.AddThemeConstantOverride("margin_top", ResponsiveUi.Px(20, viewport, 12));
        margin.AddThemeConstantOverride("margin_bottom", ResponsiveUi.Px(20, viewport, 12));
        card.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", ResponsiveUi.Px(12, viewport, 8));
        margin.AddChild(vbox);

        // 标题行（标题 + 关闭按钮）
        var titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", ResponsiveUi.Px(8, viewport, 4));
        vbox.AddChild(titleRow);

        var title = new Label { Text = "设置", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontTitle, viewport, 18));
        title.AddThemeColorOverride("font_color", BattlefieldBright);
        title.AddThemeConstantOverride("outline_size", UiOutlineBold);
        title.AddThemeColorOverride("font_outline_color", InkBlack);
        titleRow.AddChild(title);

        var closeBtn = new Button { Text = "关闭 [ESC]", FocusMode = FocusModeEnum.None };
        closeBtn.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontSmall, viewport, 10));
        closeBtn.Pressed += () => { Visible = false; CloseRequested?.Invoke(); };
        titleRow.AddChild(closeBtn);

        // ── 音量分区 ──
        _AddSectionLabel(vbox, "-- 音量 --", viewport);
        var sm = SoundManager.Instance;
        _masterSlider = _AddVolumeRow(vbox, "总体", sm?.SfxVolume ?? 1.0f, viewport,
            v => { /* Master 由 SFX+Music 联动,这里只显示 */ },
            "整体音量（控制 SFX 和 Music 同时调整；下方可单独微调）");
        _sfxSlider = _AddVolumeRow(vbox, "音效", sm?.SfxVolume ?? 1.0f, viewport,
            v => { if (SoundManager.Instance != null) SoundManager.Instance.SfxVolume = v; },
            "音效音量 (SFX:武器/击中/UI 音)");
        _musicSlider = _AddVolumeRow(vbox, "音乐", sm?.MusicVolume ?? 1.0f, viewport,
            v => { if (SoundManager.Instance != null) SoundManager.Instance.MusicVolume = v; },
            "背景音乐音量");

        // ── 屏幕分区(占位) ──
        _AddSectionLabel(vbox, "-- 屏幕 --", viewport);
        var screenHint = new Label
        {
            Text = "全屏 / 分辨率 / 画质(即将推出)\n默认全屏,1280×720 参考分辨率",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        screenHint.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontSmall, viewport, 10));
        screenHint.AddThemeColorOverride("font_color", NoteTextDim);
        vbox.AddChild(screenHint);

        // ── 键位分区(占位) ──
        _AddSectionLabel(vbox, "-- 键位 --", viewport);
        var keysHint = new Label
        {
            Text = "键位自定义(即将推出)\n当前默认: WASD 移动 / Space 攻击 / Shift 冲刺 / Tab 切目标",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        keysHint.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontSmall, viewport, 10));
        keysHint.AddThemeColorOverride("font_color", NoteTextDim);
        vbox.AddChild(keysHint);

        // ── 语言分区(占位) ──
        _AddSectionLabel(vbox, "-- 语言 --", viewport);
        var langHint = new Label { Text = "当前: 简体中文 (其他语言即将推出)" };
        langHint.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontSmall, viewport, 10));
        langHint.AddThemeColorOverride("font_color", NoteTextDim);
        vbox.AddChild(langHint);
    }

    /// <summary>区块标题(类似 HelpPanel 风格)。</summary>
    private static void _AddSectionLabel(VBoxContainer parent, string text, Vector2 viewport)
    {
        var spacer = new Control { CustomMinimumSize = new Vector2(0, ResponsiveUi.Px(4, viewport, 2)) };
        parent.AddChild(spacer);
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontBody, viewport, 12));
        label.AddThemeColorOverride("font_color", BattlefieldBright);
        label.AddThemeConstantOverride("outline_size", UiOutlineNorm);
        label.AddThemeColorOverride("font_outline_color", InkBlack);
        parent.AddChild(label);
    }

    /// <summary>添加一行 "标签 + 滑条 + 当前值"。</summary>
    private static HSlider _AddVolumeRow(
        VBoxContainer parent, string label, float initialValue, Vector2 viewport,
        System.Action<float> onChange, string tooltip)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", ResponsiveUi.Px(8, viewport, 4));
        parent.AddChild(row);

        var nameLabel = new Label { Text = label };
        nameLabel.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontBody, viewport, 12));
        nameLabel.AddThemeColorOverride("font_color", NoteText);
        nameLabel.CustomMinimumSize = new Vector2(ResponsiveUi.Px(54, viewport, 40), 0);
        row.AddChild(nameLabel);

        var slider = new HSlider
        {
            MinValue = 0.0f,
            MaxValue = 2.0f,
            Step = 0.1f,
            Value = initialValue,
            CustomMinimumSize = new Vector2(0, ResponsiveUi.Px(16, viewport, 12)),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = tooltip
        };
        var valueLabel = new Label { Text = $"{Mathf.RoundToInt(initialValue * 100)}%" };
        valueLabel.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontSmall, viewport, 10));
        valueLabel.AddThemeColorOverride("font_color", NoteTextDim);
        valueLabel.CustomMinimumSize = new Vector2(ResponsiveUi.Px(40, viewport, 32), 0);
        valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
        slider.ValueChanged += (double v) =>
        {
            valueLabel.Text = $"{Mathf.RoundToInt(v * 100)}%";
            onChange((float)v);
        };
        row.AddChild(slider);
        row.AddChild(valueLabel);
        return slider;
    }

    public override void _Input(InputEvent @event)
    {
        if (Visible && @event is InputEventKey key && key.Pressed && !key.Echo
            && key.Keycode == Key.Escape)
        {
            Visible = false;
            CloseRequested?.Invoke();
            GetViewport().SetInputAsHandled();
        }
    }
}
