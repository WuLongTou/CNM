namespace ZombieWars.UI;

using Godot;
using System.Collections.Generic;
using ZombieWars.Core;
using static ZombieWars.UI.HandDrawnTheme;
using static ZombieWars.UI.HandDrawnTheme.Colors;
using static ZombieWars.Core.GameConfig;

/// <summary>背包面板（饥荒式）：显示采集的资源数量。I 键开关，打开时暂停游戏操作。</summary>
public partial class InventoryPanel : Panel
{
    private readonly Dictionary<ItemId, Label> _countLabels = new();

    private static readonly Dictionary<ItemId, Color> _itemColors = new()
    {
        { ItemId.Wood, new Color(0.55f, 0.36f, 0.18f) },
        { ItemId.Stone, new Color(0.62f, 0.60f, 0.57f) },
        { ItemId.Fiber, new Color(0.40f, 0.55f, 0.25f) },
        { ItemId.Berry, new Color(0.80f, 0.15f, 0.20f) },
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

        var title = new Label { Text = "背包", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", CandleGlow);
        vbox.AddChild(title);

        var hint = new Label { Text = "采集的资源在此显示 · 按 I 关闭", HorizontalAlignment = HorizontalAlignment.Center };
        hint.AddThemeFontSizeOverride("font_size", 12);
        hint.AddThemeColorOverride("font_color", NoteTextDim);
        vbox.AddChild(hint);

        var grid = new GridContainer { Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", 18);
        grid.AddThemeConstantOverride("v_separation", 14);
        vbox.AddChild(grid);

        foreach (ItemId item in System.Enum.GetValues(typeof(ItemId)))
            grid.AddChild(_MakeItemSlot(item));

        Inventory.Changed += _OnInventoryChanged;
    }

    private Control _MakeItemSlot(ItemId item)
    {
        var slot = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        slot.AddThemeConstantOverride("separation", 4);

        var icon = new ColorRect
        {
            Color = _itemColors.GetValueOrDefault(item, Godot.Colors.Gray),
            CustomMinimumSize = new Vector2(56, 56)
        };
        slot.AddChild(icon);

        var name = new Label { Text = Inventory.NameOf(item), HorizontalAlignment = HorizontalAlignment.Center };
        name.AddThemeFontSizeOverride("font_size", 12);
        name.AddThemeColorOverride("font_color", NoteText);
        slot.AddChild(name);

        var count = new Label { Text = "0", HorizontalAlignment = HorizontalAlignment.Center };
        count.AddThemeFontSizeOverride("font_size", 18);
        count.AddThemeColorOverride("font_color", CandleGlow);
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
