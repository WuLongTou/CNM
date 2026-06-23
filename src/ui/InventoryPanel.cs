namespace ZombieWars.UI;

using Godot;
using System.Collections.Generic;
using ZombieWars.Core;
using static ZombieWars.UI.HandDrawnTheme;
using static ZombieWars.UI.HandDrawnTheme.Colors;
using static ZombieWars.Core.GameConfig;

/// <summary>背包面板(饥荒式):显示采集的资源数量。I 键开关,打开时暂停游戏操作。</summary>
public partial class InventoryPanel : Panel
{
    private readonly Dictionary<ItemId, Label> _countLabels = new();

    // 资源色块背景色 + 中心单字标识(色块+单字组合,色盲友好)
    private static readonly (Color bg, string glyph)[] _itemStyles = new[]
    {
        ((Color)(new Color(0.55f, 0.36f, 0.18f)), "木"),  // Wood - 棕色
        ((Color)(new Color(0.62f, 0.60f, 0.57f)), "石"),  // Stone - 灰色
        ((Color)(new Color(0.40f, 0.55f, 0.25f)), "草"),  // Fiber - 绿色
        ((Color)(new Color(0.80f, 0.15f, 0.20f)), "果"),  // Berry - 红色
    };

    public override void _Ready()
    {
        var viewport = ResponsiveUi.ViewportSize(this);
        Visible = false;
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(BattlefieldBg, 0.75f) });

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(center);

        var card = new Panel
        {
            CustomMinimumSize = ResponsiveUi.FitPanel(new Vector2(420, 300), viewport, 0.85f, 0.7f),
            MouseFilter = MouseFilterEnum.Stop
        };
        var cardStyle = MakeNotePanel(borderRadius: 8f, shadowSize: 10f);
        cardStyle.ShadowOffset = new Vector2(4, 4);
        card.AddThemeStyleboxOverride("panel", cardStyle);
        center.AddChild(card);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        card.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);
        margin.AddChild(vbox);

        // 标题:原 CandleGlow(0.88,0.72,0.38)在 NoteBg(0.78,0.72,0.60)上几乎同色,完全看不清。
        // 改用 NoteText(深棕墨)+ outline 2,任何背景下都清晰。
        var title = new Label { Text = "背包", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", NoteText);
        title.AddThemeConstantOverride("outline_size", 2);
        title.AddThemeColorOverride("font_outline_color", InkBlack);
        vbox.AddChild(title);

        var hint = new Label { Text = "采集的资源在此显示 · 按 I 关闭", HorizontalAlignment = HorizontalAlignment.Center };
        hint.AddThemeFontSizeOverride("font_size", 12);
        hint.AddThemeColorOverride("font_color", NoteTextDim);
        vbox.AddChild(hint);

        var grid = new GridContainer { Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", 18);
        grid.AddThemeConstantOverride("v_separation", 14);
        vbox.AddChild(grid);

        var items = System.Enum.GetValues(typeof(ItemId));
        for (int i = 0; i < items.Length && i < _itemStyles.Length; i++)
            grid.AddChild(_MakeItemSlot((ItemId)items.GetValue(i), _itemStyles[i].bg, _itemStyles[i].glyph));

        Inventory.Changed += _OnInventoryChanged;
    }

    /// <summary>每个资源槽:色块(带单字标识 + 边框) + 名称 + 数量 三行布局。
    /// 用 Panel 替代 ColorRect 可加 border + 圆角 + 居中文字(单字更易辨识)。</summary>
    private Control _MakeItemSlot(ItemId item, Color iconBg, string glyph)
    {
        var slot = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        slot.AddThemeConstantOverride("separation", 4);

        // Panel 容器(色块 + 单字)
        var iconPanel = new Panel { CustomMinimumSize = new Vector2(56, 56) };
        var iconStyle = new StyleBoxFlat
        {
            BgColor = iconBg,
            BorderColor = new Color(0, 0, 0, 0.55f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3
        };
        iconPanel.AddThemeStyleboxOverride("panel", iconStyle);
        slot.AddChild(iconPanel);

        // 单字标识(深棕墨 + outline,在任意背景色上都清晰)
        var glyphLabel = new Label
        {
            Text = glyph,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        glyphLabel.AddThemeFontSizeOverride("font_size", 22);
        glyphLabel.AddThemeColorOverride("font_color", NoteBg);  // 米色字,对比强
        glyphLabel.AddThemeConstantOverride("outline_size", 1);
        glyphLabel.AddThemeColorOverride("font_outline_color", InkBlack);
        glyphLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        iconPanel.AddChild(glyphLabel);

        // 资源名称
        var name = new Label { Text = Inventory.NameOf(item), HorizontalAlignment = HorizontalAlignment.Center };
        name.AddThemeFontSizeOverride("font_size", 12);
        name.AddThemeColorOverride("font_color", NoteText);
        slot.AddChild(name);

        // 数量:同标题问题 CandleGlow 不可读 → NoteText + outline
        var count = new Label { Text = "0", HorizontalAlignment = HorizontalAlignment.Center };
        count.AddThemeFontSizeOverride("font_size", 18);
        count.AddThemeColorOverride("font_color", NoteText);
        count.AddThemeConstantOverride("outline_size", 1);
        count.AddThemeColorOverride("font_outline_color", InkBlack);
        _countLabels[item] = count;
        slot.AddChild(count);
        return slot;
    }

    private void _OnInventoryChanged(ItemId item, int total)
    {
        if (_countLabels.TryGetValue(item, out var lbl))
            lbl.Text = total.ToString();
    }

    public void Toggle()
    {
        Visible = !Visible;
        if (Visible) _RefreshAll();
    }

    private void _RefreshAll()
    {
        foreach (var kv in _countLabels)
            kv.Value.Text = Inventory.Get(kv.Key).ToString();
    }

    public override void _ExitTree()
    {
        Inventory.Changed -= _OnInventoryChanged;
    }
}
