namespace ZombieWars.UI;

using Godot;
using System.Collections.Generic;
using ZombieWars.Core;
using static ZombieWars.UI.HandDrawnTheme;
using static ZombieWars.UI.HandDrawnTheme.Colors;
using static ZombieWars.Core.GameConfig;
using Player = ZombieWars.Player.Player;

/// <summary>合成台面板（饥荒式）：列出配方，消耗库存材料合成装备/消耗品。E 键工作台打开。</summary>
public partial class CraftingPanel : Panel
{
    private VBoxContainer _list = null!;
    private Player? _player;

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

        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(ResponsiveUi.FitWidth(460, viewport, 0.85f), 0),
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
        vbox.AddThemeConstantOverride("separation", 10);
        margin.AddChild(vbox);

        var title = new Label { Text = "工作台", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", CandleGlow);
        vbox.AddChild(title);

        var hint = new Label { Text = "消耗材料合成装备/道具 · ESC 关闭", HorizontalAlignment = HorizontalAlignment.Center };
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", NoteTextDim);
        vbox.AddChild(hint);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 340) };
        vbox.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_list);

        Inventory.Changed += _OnInventoryChanged;
    }

    public void Open(Player player)
    {
        _player = player;
        Visible = true;
        _RebuildList();
    }

    public void Close() => Visible = false;

    private void _OnInventoryChanged(ItemId _, int __) { if (Visible) _RebuildList(); }

    private void _RebuildList()
    {
        foreach (var c in _list.GetChildren())
            if (c is Node n) n.QueueFree();
        foreach (var recipe in RecipeDatabase.All)
            _list.AddChild(_MakeRecipeRow(recipe));
    }

    private Control _MakeRecipeRow(Recipe recipe)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 10);

        var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        info.AddThemeConstantOverride("separation", 2);
        var name = new Label { Text = recipe.Name };
        name.AddThemeFontSizeOverride("font_size", 15);
        name.AddThemeColorOverride("font_color", recipe.Accent);
        info.AddChild(name);
        var desc = new Label { Text = $"{recipe.Description}  ·  {_CostText(recipe)}", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        desc.AddThemeFontSizeOverride("font_size", 11);
        desc.AddThemeColorOverride("font_color", NoteTextDim);
        info.AddChild(desc);
        row.AddChild(info);

        var btn = new Button { Text = "合成", Disabled = !recipe.CanCraft() };
        btn.AddThemeFontSizeOverride("font_size", 13);
        btn.CustomMinimumSize = new Vector2(64, 32);
        btn.Pressed += () => _Craft(recipe);
        row.AddChild(btn);
        return row;
    }

    private void _Craft(Recipe recipe)
    {
        if (_player == null) return;
        if (!recipe.CanCraft())
        {
            GameServices.UI?.ShowLevelUpNotification("材料不足", new Color(0.9f, 0.3f, 0.3f));
            return;
        }
        recipe.Craft(_player);
        GameServices.UI?.ShowLevelUpNotification($"合成 {recipe.Name}", recipe.Accent);
        GameServices.Audio?.PlayUpgrade();
    }

    private static string _CostText(Recipe recipe)
    {
        var parts = new List<string>();
        foreach (var kv in recipe.Cost)
            parts.Add($"{Inventory.NameOf(kv.Key)}×{kv.Value}");
        return string.Join(" ", parts);
    }

    public override void _ExitTree() => Inventory.Changed -= _OnInventoryChanged;
}
