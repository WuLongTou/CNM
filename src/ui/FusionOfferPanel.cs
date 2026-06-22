namespace ZombieWars.UI;

using Godot;
using System;
using ZombieWars.Core;
using ZombieWars.Weapons;
using static ZombieWars.Core.GameConfig;
using static ZombieWars.UI.HandDrawnTheme;
using static ZombieWars.UI.HandDrawnTheme.Colors;

/// <summary>武器融合确认面板 — 祭坛触发的模态选择。</summary>
public partial class FusionOfferPanel : Control
{
    public event Action? FusionAccepted;
    public event Action? FusionRejected;

    private Label _descLabel = null!;
    private Button _acceptBtn = null!;
    private Button _rejectBtn = null!;
    private GameManager? _gameManager;

    public override void _Ready()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        ProcessMode = ProcessModeEnum.Always;
        ZIndex = 250;

        var bg = new ColorRect { Color = new Color(0, 0, 0, 0.75f), MouseFilter = MouseFilterEnum.Stop };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new Panel { CustomMinimumSize = new Vector2(480, 260) };
        panel.AddThemeStyleboxOverride("panel", MakeDarkPanel(0.92f));
        center.AddChild(panel);

        var vbox = new VBoxContainer
        {
            OffsetLeft = 20, OffsetRight = -20, OffsetTop = 16, OffsetBottom = -16
        };
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 10);
        panel.AddChild(vbox);

        var titleLabel = new Label
        {
            Text = "【献祭融合】",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 22);
        titleLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.3f, 1.0f));
        vbox.AddChild(titleLabel);

        _descLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(400, 0)
        };
        _descLabel.AddThemeFontSizeOverride("font_size", 14);
        _descLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.8f));
        vbox.AddChild(_descLabel);

        vbox.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });

        var btnRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        btnRow.AddThemeConstantOverride("separation", 20);

        _acceptBtn = new Button { Text = "接受融合 [Enter]" };
        _acceptBtn.AddThemeFontSizeOverride("font_size", 15);
        _acceptBtn.Pressed += () => { FusionAccepted?.Invoke(); Visible = false; };
        btnRow.AddChild(_acceptBtn);

        _rejectBtn = new Button { Text = "拒绝 [Delete]" };
        _rejectBtn.AddThemeFontSizeOverride("font_size", 15);
        _rejectBtn.Pressed += () => { FusionRejected?.Invoke(); Visible = false; };
        btnRow.AddChild(_rejectBtn);

        vbox.AddChild(btnRow);
    }

    public void ShowOffer(string currentWeapon, string mateWeapon, string resultName, string resultDesc, GameManager gm)
    {
        _gameManager = gm;
        _descLabel.Text = $"你的 {currentWeapon}\n+ {mateWeapon}\n\n→ {resultName} ({resultDesc})";
        Visible = true;
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.Enter || key.Keycode == Key.KpEnter)
            {
                FusionAccepted?.Invoke();
                Visible = false;
                GetViewport().SetInputAsHandled();
            }
            else if (key.Keycode == Key.Delete)
            {
                FusionRejected?.Invoke();
                Visible = false;
                GetViewport().SetInputAsHandled();
            }
        }
    }
}
