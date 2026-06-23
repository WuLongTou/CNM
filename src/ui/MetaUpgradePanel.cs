namespace ZombieWars.UI;

using Godot;
using ZombieWars.Core;

/// <summary>
/// 跨局永久升级面板：用金币购买永久 buff（血/伤/速），存档持久化，新局自动应用。
/// 由 GameOverUI 的"永久升级"按钮打开。
/// </summary>
public partial class MetaUpgradePanel : Control
{
    private Label _coinsLabel = null!;
    private VBoxContainer _rowsBox = null!;

    private static readonly string[] Keys = { "health", "damage", "speed" };
    private static readonly string[] Names = { "心 生命上限", "剑 伤害", "靴 移动速度" };
    private static readonly string[] Descs = { "+20/级", "+10%/级", "+5%/级" };

    public override void _Ready()
    {
        var viewport = ResponsiveUi.ViewportSize(this);

        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        ZIndex = 250; // 高于 GameOver(50)

        var bg = new ColorRect { Color = new Color(0, 0, 0, 0.88f), MouseFilter = MouseFilterEnum.Stop };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(center);

        var panel = new Panel { CustomMinimumSize = new Vector2(440, 380) };
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

        var title = new Label { Text = "永久升级", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.3f));
        vbox.AddChild(title);

        _coinsLabel = new Label { Text = "", HorizontalAlignment = HorizontalAlignment.Center };
        _coinsLabel.AddThemeFontSizeOverride("font_size", 16);
        _coinsLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.8f, 0.2f));
        vbox.AddChild(_coinsLabel);

        _rowsBox = new VBoxContainer();
        _rowsBox.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(_rowsBox);
        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) }); // spacer

        var closeBtn = new Button { Text = "关闭", SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        closeBtn.Pressed += () => Visible = false;
        vbox.AddChild(closeBtn);
    }

    public override void _Input(InputEvent @event)
    {
        if (Visible && @event.IsActionPressed("ui_cancel"))
        {
            Visible = false;
            GetViewport().SetInputAsHandled();
        }
    }

    public void Open()
    {
        _Refresh();
        Visible = true;
    }

    private void _Refresh()
    {
        _coinsLabel.Text = $"金币: {SaveManager.GetCoins()}";
        foreach (var c in _rowsBox.GetChildren())
            if (c is Node n) n.QueueFree();

        int coins = SaveManager.GetCoins();
        for (int i = 0; i < Keys.Length; i++)
        {
            string key = Keys[i];
            int level = SaveManager.GetPermLevel(key);
            int cost = SaveManager.GetPermCost(key);
            bool affordable = coins >= cost;

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 14);

            var name = new Label
            {
                Text = $"{Names[i]}  Lv.{level}  ({Descs[i]})",
                CustomMinimumSize = new Vector2(250, 0)
            };
            row.AddChild(name);

            var btn = new Button { Text = affordable ? $"升级 −{cost}" : $"需 {cost}", Disabled = !affordable };
            string capturedKey = key;
            btn.Pressed += () =>
            {
                if (SaveManager.UpgradePerm(capturedKey))
                    _Refresh();
            };
            row.AddChild(btn);

            _rowsBox.AddChild(row);
        }
    }
}
