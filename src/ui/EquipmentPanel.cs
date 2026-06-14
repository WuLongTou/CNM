namespace ZombieWars.UI;

using System;
using Godot;
using System.Collections.Generic;
using ZombieWars.Core;
using ZombieWars.Weapons;
using static ZombieWars.UI.HandDrawnTheme;
using static ZombieWars.UI.HandDrawnTheme.Colors;
using static ZombieWars.Core.GameConfig;
using static ZombieWars.Core.GameEnums;
using Player = ZombieWars.Player.Player;

/// <summary>
/// MMO 式装备面板：装备图标按身体部位定位。
/// 头盔(上)/护甲(中)/武器(中)/靴子(下) 为单槽位，戒指/宝石/饰品为多槽位。
/// 空槽显示虚框，让玩家一眼看出哪里还缺装备。
/// 按 C 键或 HUD [装备] 按钮打开。
/// </summary>
public partial class EquipmentPanel : Panel
{
    private Action<string> _showTooltip = null!;
    private Action _hideTooltip = null!;

    // 槽位 → 节点引用（按身体位置布局）
    private TextureRect? _weaponSlot;
    private TextureRect? _helmetSlot;
    private TextureRect? _armorSlot;
    private TextureRect? _bootsSlot;
    private readonly TextureRect?[] _ringSlots = new TextureRect[2];
    private readonly TextureRect?[] _gemSlots = new TextureRect[2];
    private readonly TextureRect?[] _trinketSlots = new TextureRect[2];

    // 槽位 → 占位文字，用于空槽提示
    private readonly Dictionary<TextureRect, string> _slotHints = new();
    private readonly Dictionary<TextureRect, PanelContainer> _slotFrames = new();

    public override void _Ready()
    {
        Visible = false;
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        var bgStyle = new StyleBoxFlat { BgColor = new Color(BattlefieldBg, 0.75f) };
        AddThemeStyleboxOverride("panel", bgStyle);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(center);

        var card = new Panel();
        card.CustomMinimumSize = new Vector2(440, 420);
        card.MouseFilter = MouseFilterEnum.Stop;
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

        var mainVbox = new VBoxContainer();
        mainVbox.AddThemeConstantOverride("separation", 12);
        mainVbox.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        margin.AddChild(mainVbox);

        // 标题行
        var titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", 8);
        mainVbox.AddChild(titleRow);

        var titleLabel = new Label
        {
            Text = "角色装备",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 22);
        titleLabel.AddThemeColorOverride("font_color", BloodBright);
        titleLabel.AddThemeConstantOverride("outline_size", 2);
        titleLabel.AddThemeColorOverride("font_outline_color", InkBlack);
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        titleRow.AddChild(titleLabel);

        var closeHint = new Label { Text = "按 C 关闭" };
        closeHint.AddThemeFontSizeOverride("font_size", 12);
        closeHint.AddThemeColorOverride("font_color", NoteText);
        closeHint.VerticalAlignment = VerticalAlignment.Center;
        titleRow.AddChild(closeHint);

        // 装备网格：5 列 × 4 行，身体定位
        // Row 0:          [头盔]
        // Row 1: [戒指1]   [护甲]   [宝石1]
        // Row 2: [戒指2]   [武器]   [宝石2]
        // Row 3: [饰品1]   [靴子]   [饰品2]
        var grid = new GridContainer
        {
            Columns = 5
        };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 10);
        grid.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        mainVbox.AddChild(grid);

        // 占位空白单元
        static Control _Empty() => new Control { CustomMinimumSize = new Vector2(60, 60) };

        // Row 0: _ _ 头盔 _ _
        grid.AddChild(_Empty());
        grid.AddChild(_Empty());
        _helmetSlot = _MakeSlot(grid, "头盔");
        grid.AddChild(_Empty());
        grid.AddChild(_Empty());

        // Row 1: 戒指1 _ 护甲 _ 宝石1
        _ringSlots[0] = _MakeSlot(grid, "戒指");
        grid.AddChild(_Empty());
        _armorSlot = _MakeSlot(grid, "护甲");
        grid.AddChild(_Empty());
        _gemSlots[0] = _MakeSlot(grid, "宝石");

        // Row 2: 戒指2 _ 武器 _ 宝石2
        _ringSlots[1] = _MakeSlot(grid, "戒指");
        grid.AddChild(_Empty());
        _weaponSlot = _MakeSlot(grid, "武器");
        grid.AddChild(_Empty());
        _gemSlots[1] = _MakeSlot(grid, "宝石");

        // Row 3: 饰品1 _ 靴子 _ 饰品2
        _trinketSlots[0] = _MakeSlot(grid, "饰品");
        grid.AddChild(_Empty());
        _bootsSlot = _MakeSlot(grid, "靴子");
        grid.AddChild(_Empty());
        _trinketSlots[1] = _MakeSlot(grid, "饰品");
    }

    public void Setup(Action<string> showTooltip, Action hideTooltip)
    {
        _showTooltip = showTooltip;
        _hideTooltip = hideTooltip;
    }

    public void Toggle()
    {
        Visible = !Visible;
    }

    public void Close()
    {
        Visible = false;
    }

    /// <summary>从玩家 ActiveEffects 刷新所有槽位显示。</summary>
    public void Refresh(Player player)
    {
        // 按身体部位归类装备
        var weapon = (WeaponCard?)null;
        var byCat = new Dictionary<string, List<WeaponCard>>();
        foreach (var e in player.ActiveEffects)
        {
            if (e.Category == WeaponCategory.Weapon)
            {
                weapon = e;
                continue;
            }
            string cat = string.IsNullOrEmpty(e.UpgradeCategory) ? "buff" : e.UpgradeCategory;
            if (!byCat.TryGetValue(cat, out var list))
            {
                list = new List<WeaponCard>();
                byCat[cat] = list;
            }
            list.Add(e);
        }

        // 单槽位
        _FillSingleSlot(_weaponSlot, weapon, "武器");
        _FillSingleSlot(_helmetSlot, PickFirst(byCat, "头盔"), "头盔");
        _FillSingleSlot(_armorSlot, PickFirst(byCat, "护甲"), "护甲");
        _FillSingleSlot(_bootsSlot, PickFirst(byCat, "鞋子"), "靴子");

        // 多槽位
        _FillMultiSlot(_ringSlots, GetList(byCat, "戒指"), "戒指");
        _FillMultiSlot(_gemSlots, GetList(byCat, "特效"), "宝石");
        _FillMultiSlot(_trinketSlots, GetList(byCat, "buff"), "饰品");
    }

    private static WeaponCard? PickFirst(Dictionary<string, List<WeaponCard>> byCat, string cat) =>
        byCat.TryGetValue(cat, out var list) && list.Count > 0 ? list[0] : null;

    private static List<WeaponCard> GetList(Dictionary<string, List<WeaponCard>> byCat, string cat) =>
        byCat.TryGetValue(cat, out var list) ? list : new List<WeaponCard>();

    // ═══ 槽位构建/填充 ═══

    private TextureRect _MakeSlot(Container parent, string hint)
    {
        var frame = new PanelContainer
        {
            CustomMinimumSize = new Vector2(60, 60),
            MouseFilter = MouseFilterEnum.Stop,
            TooltipText = hint
        };
        parent.AddChild(frame);

        var slot = new TextureRect
        {
            CustomMinimumSize = new Vector2(60, 60),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            TooltipText = hint
        };
        frame.AddChild(slot);
        _slotHints[slot] = hint;
        _slotFrames[slot] = frame;
        frame.MouseEntered += () => _OnSlotHover(slot);
        frame.MouseExited += () => _hideTooltip?.Invoke();
        _ApplyEmptyStyle(slot);
        return slot;
    }

    private void _OnSlotHover(TextureRect slot)
    {
        // tooltip 内容在 Refresh 时通过 metadata 绑定到槽位
        string? tip = slot.GetMeta("tooltip", "").AsString();
        if (!string.IsNullOrEmpty(tip))
            _showTooltip?.Invoke(tip);
        else if (_slotHints.TryGetValue(slot, out var hint))
            _showTooltip?.Invoke($"【空 {hint}槽】\n选择对应部位的装备卡即可装备");
    }

    private void _FillSingleSlot(TextureRect? slot, WeaponCard? card, string hint)
    {
        if (slot == null) return;
        _ApplyCardToSlot(slot, card, hint);
    }

    private void _FillMultiSlot(TextureRect?[] slots, List<WeaponCard> cards, string hint)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            WeaponCard? card = i < cards.Count ? cards[i] : null;
            _ApplyCardToSlot(slots[i]!, card, hint, overflowCount: i == slots.Length - 1 && cards.Count > slots.Length
                ? cards.Count - slots.Length : 0);
        }
    }

    private void _ApplyCardToSlot(TextureRect slot, WeaponCard? card, string hint, int overflowCount = 0)
    {
        if (card != null && card.IconTexture != null)
        {
            slot.Texture = card.IconTexture;
            _ApplyFilledStyle(slot, card.Rarity);
            string tip = WeaponFormatter.GetEffectTooltip(card);
            if (overflowCount > 0)
                tip += $"\n[color=#e0a040]● 同部位还有 {overflowCount} 件未显示[/color]";
            slot.SetMeta("tooltip", tip);
            slot.TooltipText = card.CardName;
            if (_slotFrames.TryGetValue(slot, out var frame))
                frame.TooltipText = card.CardName;
        }
        else
        {
            slot.Texture = null;
            _ApplyEmptyStyle(slot);
            slot.SetMeta("tooltip", "");
            slot.TooltipText = hint;
            if (_slotFrames.TryGetValue(slot, out var frame))
                frame.TooltipText = hint;
        }
    }

    private void _ApplyEmptyStyle(TextureRect slot)
    {
        if (!_slotFrames.TryGetValue(slot, out var frame)) return;
        var style = new StyleBoxFlat
        {
            BgColor = new Color(NoteBg, 0.25f),
            BorderColor = new Color(NoteBorder, 0.35f),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 2,
            ContentMarginRight = 2,
            ContentMarginTop = 2,
            ContentMarginBottom = 2
        };
        frame.AddThemeStyleboxOverride("panel", style);
    }

    private void _ApplyFilledStyle(TextureRect slot, int rarity)
    {
        if (!_slotFrames.TryGetValue(slot, out var frame)) return;
        Color border = rarity switch
        {
            2 => RarityRare,
            3 => RarityEpic,
            4 => BloodBright,
            _ => NoteBorder
        };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(BattlefieldBg, 0.85f),
            BorderColor = border,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 2,
            ContentMarginRight = 2,
            ContentMarginTop = 2,
            ContentMarginBottom = 2
        };
        frame.AddThemeStyleboxOverride("panel", style);
    }
}
