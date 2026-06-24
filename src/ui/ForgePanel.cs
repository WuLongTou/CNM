namespace ZombieWars.UI;

using Godot;
using System.Collections.Generic;
using ZombieWars.Core;
using ZombieWars.Weapons;
using static ZombieWars.UI.HandDrawnTheme;
using static ZombieWars.UI.HandDrawnTheme.Colors;
using static ZombieWars.Core.GameConfig;
using static ZombieWars.Core.GameEnums;
using Player = ZombieWars.Player.Player;

public class ForgeOption
{
    public string Id = "";
    public string Name = "";
    public string Description = "";
    public Dictionary<ItemId, int> BaseCost = new();
    public Color Accent = new(0.8f, 0.6f, 0.3f);
    public int TimesApplied { get; set; } = 0;

    public Dictionary<ItemId, int> GetCost()
    {
        var result = new Dictionary<ItemId, int>();
        float mult = 1.0f + TimesApplied * 0.5f;
        foreach (var kv in BaseCost)
            result[kv.Key] = System.Math.Max(1, (int)(kv.Value * mult));
        return result;
    }

    public void Apply(WeaponCard weapon)
    {
        switch (Id)
        {
            case "sharpen":
                weapon.DamageModifier += 0.15f;
                break;
            case "lightweight":
                weapon.AttackSpeedModifier += 0.10f;
                break;
            case "precision":
                weapon.CriticalBonus += 8;
                break;
            case "bloodpact":
                weapon.LifeStealBonus += 6;
                break;
            case "reinforce":
                weapon.HealthModifier += 15;
                break;
            case "speed":
                weapon.SpeedModifier += 12;
                break;
        }
        weapon.Tier++;
        weapon.UpgradeCount++;
        TimesApplied++;
    }
}

public partial class ForgePanel : Panel
{
    private VBoxContainer _list = null!;
    private Label _weaponLabel = null!;
    private Player? _player;
    private WeaponCard? _targetWeapon;
    private readonly Dictionary<string, ForgeOption> _forgeOptions = new();

    public override void _Ready()
    {
        var viewport = ResponsiveUi.ViewportSize(this);
        Visible = false;
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(BattlefieldBg, 0.75f) });

        _InitForgeOptions();

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(center);

        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(ResponsiveUi.FitWidth(500, viewport, 0.85f), 0),
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

        var title = new Label { Text = "锻造台", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", CandleGlow);
        vbox.AddChild(title);

        var hint = new Label { Text = "消耗材料锻造装备 · ESC 关闭", HorizontalAlignment = HorizontalAlignment.Center };
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", NoteTextDim);
        vbox.AddChild(hint);

        _weaponLabel = new Label { Text = "当前武器: 无", HorizontalAlignment = HorizontalAlignment.Center };
        _weaponLabel.AddThemeFontSizeOverride("font_size", 12);
        _weaponLabel.AddThemeColorOverride("font_color", BloodBright);
        vbox.AddChild(_weaponLabel);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 340) };
        vbox.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_list);

        Inventory.Changed += _OnInventoryChanged;
    }

    private void _InitForgeOptions()
    {
        var options = new List<ForgeOption>
        {
            new() { Id = "sharpen", Name = "锋刃打磨", Description = "伤害 +15%", BaseCost = new() { { ItemId.Stone, 3 } }, Accent = new(0.85f, 0.50f, 0.30f) },
            new() { Id = "lightweight", Name = "轻量化改造", Description = "攻速 +10%", BaseCost = new() { { ItemId.Wood, 3 } }, Accent = new(0.40f, 0.70f, 0.40f) },
            new() { Id = "precision", Name = "精准校准", Description = "暴击 +8%", BaseCost = new() { { ItemId.Berry, 2 } }, Accent = new(0.90f, 0.75f, 0.30f) },
            new() { Id = "bloodpact", Name = "血之契约", Description = "吸血 +6%", BaseCost = new() { { ItemId.Berry, 2 }, { ItemId.Fiber, 2 } }, Accent = new(0.75f, 0.25f, 0.25f) },
            new() { Id = "reinforce", Name = "坚固强化", Description = "生命上限 +15", BaseCost = new() { { ItemId.Stone, 2 }, { ItemId.Fiber, 2 } }, Accent = new(0.50f, 0.60f, 0.70f) },
            new() { Id = "speed", Name = "增速附魔", Description = "移动速度 +12", BaseCost = new() { { ItemId.Wood, 2 }, { ItemId.Berry, 2 } }, Accent = new(0.40f, 0.80f, 0.60f) },
        };
        foreach (var opt in options)
            _forgeOptions[opt.Id] = opt;
    }

    public void Open(Player player)
    {
        _player = player;
        _ResetForgeCounts();
        _targetWeapon = _FindWeapon(player);
        Visible = true;
        _RebuildList();
    }

    public void Close() => Visible = false;

    private void _ResetForgeCounts()
    {
        foreach (var opt in _forgeOptions.Values)
            opt.TimesApplied = 0;
    }

    private static WeaponCard? _FindWeapon(Player player)
    {
        foreach (var e in player.ActiveEffects)
        {
            if (e.Category == WeaponCategory.Weapon)
                return e;
        }
        return null;
    }

    private void _OnInventoryChanged(ItemId _, int __) { if (Visible) _RebuildList(); }

    private void _RebuildList()
    {
        foreach (var c in _list.GetChildren())
            if (c is Node n) n.QueueFree();

        if (_targetWeapon == null)
        {
            var empty = new Label { Text = "请先装备一把武器再来锻造", HorizontalAlignment = HorizontalAlignment.Center };
            empty.AddThemeFontSizeOverride("font_size", 13);
            empty.AddThemeColorOverride("font_color", NoteTextDim);
            _list.AddChild(empty);
            _weaponLabel.Text = "当前武器: 无";
            return;
        }

        _weaponLabel.Text = $"当前武器: {_targetWeapon.CardName}  (Lv.{_targetWeapon.Tier})";

        foreach (var option in _forgeOptions.Values)
            _list.AddChild(_MakeForgeRow(option));
    }

    private Control _MakeForgeRow(ForgeOption option)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 10);

        var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        info.AddThemeConstantOverride("separation", 2);
        var name = new Label { Text = option.Name };
        name.AddThemeFontSizeOverride("font_size", 15);
        name.AddThemeColorOverride("font_color", option.Accent);
        info.AddChild(name);
        var desc = new Label { Text = $"{option.Description}  ·  {_CostText(option)}", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        desc.AddThemeFontSizeOverride("font_size", 11);
        desc.AddThemeColorOverride("font_color", NoteTextDim);
        info.AddChild(desc);
        row.AddChild(info);

        var btn = new Button { Text = "锻造", Disabled = !option.GetCost().CanAfford() };
        btn.AddThemeFontSizeOverride("font_size", 13);
        btn.CustomMinimumSize = new Vector2(64, 32);
        btn.Pressed += () => _Forge(option);
        row.AddChild(btn);
        return row;
    }

    private void _Forge(ForgeOption option)
    {
        if (_player == null || _targetWeapon == null) return;

        var cost = option.GetCost();
        if (!cost.CanAfford())
        {
            GameServices.UI?.ShowLevelUpNotification("材料不足", new Color(0.9f, 0.3f, 0.3f));
            return;
        }

        Inventory.TryConsume(cost);

        option.Apply(_targetWeapon);
        _player.Combat?.RecalculateStats();
        GameServices.UI?.ShowLevelUpNotification($"锻造成功! {_targetWeapon.CardName} {option.Name}", option.Accent);
        GameServices.Audio?.PlayUpgrade();
        _RebuildList();
    }

    private static string _CostText(ForgeOption option)
    {
        var parts = new List<string>();
        foreach (var kv in option.GetCost())
            parts.Add($"{Inventory.NameOf(kv.Key)}×{kv.Value}");
        return string.Join(" ", parts);
    }

    public override void _ExitTree() => Inventory.Changed -= _OnInventoryChanged;
}

internal static class ForgeCostExtensions
{
    public static bool CanAfford(this Dictionary<ItemId, int> cost)
    {
        foreach (var kv in cost)
            if (Inventory.Get(kv.Key) < kv.Value) return false;
        return true;
    }
}
