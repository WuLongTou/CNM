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

    // 当前报价内容（供外部指令接口 game_state 读取）
    private string _offerCurrent = "";
    private string _offerMate = "";
    private string _offerResult = "";
    private string _offerResultDesc = "";

    /// <summary>被献祭的原武器名（无报价时为空串）。</summary>
    public string OfferCurrentWeapon => _offerCurrent;

    /// <summary>祭坛提供的融合对象武器名。</summary>
    public string OfferMateWeapon => _offerMate;

    /// <summary>融合结果武器名。</summary>
    public string OfferResultName => _offerResult;

    /// <summary>融合结果描述。</summary>
    public string OfferResultDesc => _offerResultDesc;

    public override void _Ready()
    {
        var viewport = ResponsiveUi.ViewportSize(this);

        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
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
            OffsetLeft = ResponsiveUi.Px(20, viewport, 12),
            OffsetRight = -ResponsiveUi.Px(20, viewport, 12),
            OffsetTop = ResponsiveUi.Px(16, viewport, 10),
            OffsetBottom = -ResponsiveUi.Px(16, viewport, 10)
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
        _acceptBtn.Pressed += Accept;
        btnRow.AddChild(_acceptBtn);

        _rejectBtn = new Button { Text = "拒绝 [Delete]" };
        _rejectBtn.AddThemeFontSizeOverride("font_size", 15);
        _rejectBtn.Pressed += Reject;
        btnRow.AddChild(_rejectBtn);

        vbox.AddChild(btnRow);
    }

    public void ShowOffer(string currentWeapon, string mateWeapon, string resultName, string resultDesc, GameManager gm)
    {
        _gameManager = gm;
        _offerCurrent = currentWeapon;
        _offerMate = mateWeapon;
        _offerResult = resultName;
        _offerResultDesc = resultDesc;
        _descLabel.Text = $"你的 {currentWeapon}\n+ {mateWeapon}\n\n→ {resultName} ({resultDesc})";
        Visible = true;
    }

    /// <summary>接受融合（按钮/Enter 共用入口）。</summary>
    public void Accept()
    {
        if (!Visible) return;
        FusionAccepted?.Invoke();
        Visible = false;
    }

    /// <summary>拒绝融合（按钮/Delete/ESC 共用入口，ESC 由 UIManager 转调）。</summary>
    public void Reject()
    {
        if (!Visible) return;
        FusionRejected?.Invoke();
        Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.Enter || key.Keycode == Key.KpEnter)
            {
                Accept();
                GetViewport().SetInputAsHandled();
            }
            else if (key.Keycode == Key.Delete)
            {
                Reject();
                GetViewport().SetInputAsHandled();
            }
        }
    }
}
