namespace ZombieWars.UI;

using System;
using System.Collections.Generic;
using Godot;
using ZombieWars.Core;
using ZombieWars.Weapons;
using static ZombieWars.UI.HandDrawnTheme;
using static ZombieWars.UI.HandDrawnTheme.Colors;
using Player = ZombieWars.Player.Player;
using MeleeEffect = ZombieWars.Core.GameEnums.MeleeEffect;
using RangedEffect = ZombieWars.Core.GameEnums.RangedEffect;
using MagicEffect = ZombieWars.Core.GameEnums.MagicEffect;
using WeaponCategory = ZombieWars.Core.GameEnums.WeaponCategory;

/// <summary>
/// Handles the weapon/upgrade card selection panel shown between rounds.
/// </summary>
public partial class WeaponSelectionPanel : Control
{
    private Panel _panel = null!;
    private readonly List<Button> _cardButtons = new();
    private Button? _skipButton;
    private readonly List<object> _currentOptions = new();
    private Label _currentWeaponLabel = null!;
    private WeaponDeck? _weaponDeck;
    private GameManager? _gameManager;

    public event Action<object?> CardSelected = delegate { };
    public event Action Skipped = delegate { };

    public override void _Ready()
    {
        _CreatePanel();
        MouseFilter = Control.MouseFilterEnum.Ignore;
        ProcessMode = ProcessModeEnum.Always;
    }

    public void SetWeaponDeck(WeaponDeck deck) => _weaponDeck = deck;
    public void SetGameManager(GameManager gm) => _gameManager = gm;
    public new bool IsVisible() => _panel.Visible;

    private void _CreatePanel()
    {
        _panel = new Panel
        {
            Visible = false,
            ProcessMode = ProcessModeEnum.Always,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var style = MakeDarkPanel(0.92f);
        style.BgColor = new Color(BattlefieldBg, 0.95f);
        _panel.AddThemeStyleboxOverride("panel", style);
        AddChild(_panel);

        var centerContainer = new CenterContainer();
        centerContainer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _panel.AddChild(centerContainer);

        var mainVbox = new VBoxContainer();
        mainVbox.AddThemeConstantOverride("separation", 30);
        centerContainer.AddChild(mainVbox);

        // Title
        var title = new Label { Text = "⚔️ 选择本回合强化 ⚔️" };
        title.AddThemeFontSizeOverride("font_size", 32);
        title.AddThemeColorOverride("font_color", BloodBright);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        mainVbox.AddChild(title);

        // Current weapon info
        _currentWeaponLabel = new Label
        {
            Text = "当前武器: 无",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(720, 0)
        };
        _currentWeaponLabel.AddThemeFontSizeOverride("font_size", 16);
        _currentWeaponLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.9f));
        _currentWeaponLabel.HorizontalAlignment = HorizontalAlignment.Center;
        mainVbox.AddChild(_currentWeaponLabel);

        // Description
        var desc = new Label
        {
            Text = "你可能抽到新武器、装备，或对已有装备的强化卡",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(720, 0)
        };
        desc.AddThemeFontSizeOverride("font_size", 18);
        desc.AddThemeColorOverride("font_color", Godot.Colors.White);
        desc.HorizontalAlignment = HorizontalAlignment.Center;
        mainVbox.AddChild(desc);

        // Card buttons container
        var buttonContainer = new HBoxContainer();
        buttonContainer.AddThemeConstantOverride("separation", 20);
        buttonContainer.Alignment = BoxContainer.AlignmentMode.Center;
        mainVbox.AddChild(buttonContainer);

        // Create 3 card buttons
        for (int i = 0; i < 3; i++)
        {
            int cardIndex = i;
            var button = new Button
            {
                CustomMinimumSize = new Vector2(200, 280),
                FocusMode = Control.FocusModeEnum.None
            };
            button.MouseEntered += () => _OnCardMouseEntered(cardIndex);
            button.MouseExited += _OnCardMouseExited;
            buttonContainer.AddChild(button);
            _cardButtons.Add(button);

            var cardVbox = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            cardVbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            cardVbox.AddThemeConstantOverride("separation", 10);

            var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_top", 15);
            margin.AddThemeConstantOverride("margin_bottom", 15);
            margin.AddThemeConstantOverride("margin_left", 15);
            margin.AddThemeConstantOverride("margin_right", 15);
            button.AddChild(margin);
            margin.AddChild(cardVbox);

            _AddCardLabels(cardVbox);
        }

        // Keyboard hint
        var hintLabel = new Label
        {
            Text = "💡 按 [1] [2] [3] 选择 | 按 [S] 或 [Enter] 跳过",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        hintLabel.AddThemeFontSizeOverride("font_size", 14);
        hintLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.7f));
        mainVbox.AddChild(hintLabel);

        // Skip button
        var skipContainer = new CenterContainer();
        mainVbox.AddChild(skipContainer);

        _skipButton = new Button
        {
            Text = "跳过升级",
            CustomMinimumSize = new Vector2(150, 40)
        };
        _skipButton.AddThemeFontSizeOverride("font_size", 16);
        var skipNormal = MakeNotePanel(borderRadius: 3f, shadowSize: 3f);
        skipNormal.BgColor = new Color(0.25f, 0.2f, 0.2f, 0.9f);
        _skipButton.AddThemeStyleboxOverride("normal", skipNormal);
        var skipHover = MakeNotePanel(borderRadius: 3f, shadowSize: 3f);
        skipHover.BgColor = new Color(0.35f, 0.28f, 0.28f, 0.9f);
        _skipButton.AddThemeStyleboxOverride("hover", skipHover);
        _skipButton.AddThemeColorOverride("font_color", NoteBg);
        skipContainer.AddChild(_skipButton);
    }

    private void _AddCardLabels(VBoxContainer vbox)
    {
        _AddLabel(vbox, "Category", "【类型】", 12, NoteText);
        _AddLabel(vbox, "Title", "武器名称", 22, NoteText, autowrap: true);
        vbox.AddChild(new HSeparator { MouseFilter = Control.MouseFilterEnum.Ignore });
        _AddIconContainer(vbox);
        _AddLabel(vbox, "Type", "类型", 14, NoteText);
        _AddLabel(vbox, "Desc", "", 13, NoteText, autowrap: true);
        vbox.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
        _AddLabel(vbox, "Rarity", "稀有度", 14, NoteText);
        _AddLabel(vbox, "Hint", "悬停查看详情", 11, NoteText);
    }

    private static void _AddLabel(VBoxContainer parent, string name, string text, int fontSize, Color color, bool autowrap = false)
    {
        var label = new Label
        {
            Name = name,
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = autowrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        parent.AddChild(label);
    }

    private static void _AddIconContainer(VBoxContainer parent)
    {
        var container = new Control
        {
            Name = "IconContainer",
            CustomMinimumSize = new Vector2(0, 70),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        parent.AddChild(container);

        var iconLabel = new Label { Name = "Icon", Text = "⚔️", HorizontalAlignment = HorizontalAlignment.Center };
        iconLabel.AddThemeFontSizeOverride("font_size", 55);
        container.AddChild(iconLabel);
        iconLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
    }

    public void ShowSelection()
    {
        _currentOptions.Clear();

        if (_gameManager?.PlayerRef != null)
        {
            _UpdateCurrentWeaponLabel(_gameManager.PlayerRef);
            var drawn = _weaponDeck?.DrawThreeCards(_gameManager.PlayerRef);
            if (drawn != null)
                foreach (var w in drawn) _currentOptions.Add(w);
        }
        else
        {
            var drawn = _weaponDeck?.DrawThreeCards();
            if (drawn != null)
                foreach (var w in drawn) _currentOptions.Add(w);
        }

        for (int i = 0; i < 3; i++)
        {
            if (i < _currentOptions.Count)
                _RenderCard(_cardButtons[i], _currentOptions[i]);
            else
                _cardButtons[i].Visible = false;
        }

        _panel.Visible = true;
    }

    public void HideSelection()
    {
        _panel.Visible = false;
    }

    private void _UpdateCurrentWeaponLabel(Player player)
    {
        string weaponName = "无";
        string weaponTypeText = "";
        var effects = new List<string>();

        foreach (var effect in player.ActiveEffects)
        {
            if (effect.Category == WeaponCategory.Weapon)
            {
                weaponName = effect.CardName;
                weaponTypeText = effect.GetWeaponTypeText();
                foreach (var m in effect.MeleeEffects)
                {
                    var level = effect.MeleeEffectLevels.TryGetValue((int)m, out var l) ? l.AsInt32() : 1;
                    effects.Add(WeaponCard.GetMeleeEffectDesc((MeleeEffect)(int)m, level));
                }
                foreach (var r in effect.RangedEffects)
                {
                    var level = effect.RangedEffectLevels.TryGetValue((int)r, out var l) ? l.AsInt32() : 1;
                    effects.Add(WeaponCard.GetRangedEffectDesc((RangedEffect)(int)r, level));
                }
                foreach (var g in effect.MagicEffects)
                {
                    var level = effect.MagicEffectLevels.TryGetValue((int)g, out var l) ? l.AsInt32() : 1;
                    effects.Add(WeaponCard.GetMagicEffectDesc((MagicEffect)(int)g, level));
                }
                break;
            }
        }

        var effectText = effects.Count > 0 ? string.Join(" | ", effects) : "无特效";
        effectText = _TruncateText(effectText, 36);
        _currentWeaponLabel.Text = weaponName == "无"
            ? "当前武器: 无"
            : $"当前武器: {weaponName} ({weaponTypeText}) | {effectText}";
    }

    private static string _TruncateText(string text, int maxLen)
        => text.Length <= maxLen ? text : text.Substring(0, maxLen - 1) + "…";

    private void _RenderCard(Button button, object card)
    {
        button.Visible = true;

        var margin = (MarginContainer)button.GetChild(0);
        var vbox = (VBoxContainer)margin.GetChild(0);
        var categoryNode = (Label)vbox.GetNode("Category");
        var titleNode = (Label)vbox.GetNode("Title");
        var typeNode = (Label)vbox.GetNode("Type");
        var iconNode = vbox.FindChild("Icon", true, false) as Label;
        var descNode = (Label)vbox.GetNode("Desc");
        var rarityNode = (Label)vbox.GetNode("Rarity");

        // Reset colors
        var dimText = new Color(0.6f, 0.5f, 0.5f);
        categoryNode.AddThemeColorOverride("font_color", dimText);
        titleNode.AddThemeColorOverride("font_color", RarityCommon);
        typeNode.AddThemeColorOverride("font_color", dimText);
        descNode.AddThemeColorOverride("font_color", dimText);
        rarityNode.AddThemeColorOverride("font_color", RarityCommon);

        if (card is UpgradeCard upgrade)
        {
            _RenderUpgradeCard(button, upgrade, categoryNode, titleNode, typeNode, iconNode, descNode, rarityNode, vbox);
            return;
        }

        if (card is not WeaponCard wc) return;

        titleNode.Text = wc.CardName;
        var upgradeCategory = string.IsNullOrEmpty(wc.UpgradeCategory)
            ? (wc.Category == WeaponCategory.Weapon ? "武器" : "装备")
            : wc.UpgradeCategory;
        categoryNode.Text = $"【{upgradeCategory} Lv.{wc.Tier}】";
        typeNode.Text = wc.Category == WeaponCategory.Weapon ? wc.GetWeaponTypeText() : "装备";

        // Keywords
        var keywords = new List<string>();
        if (wc.DamageModifier > 1.0f) keywords.Add("伤害↑");
        else if (wc.DamageModifier < 1.0f) keywords.Add("伤害↓");
        if (wc.AttackSpeedModifier > 1.0f) keywords.Add("攻速↑");
        else if (wc.AttackSpeedModifier < 1.0f) keywords.Add("攻速↓");
        if (wc.HealthModifier > 0) keywords.Add("生命↑");
        else if (wc.HealthModifier < 0) keywords.Add("生命↓");
        if (wc.SpeedModifier > 0) keywords.Add("速度↑");
        if (wc.CriticalBonus > 0) keywords.Add("暴击");
        if (wc.LifeStealBonus > 0) keywords.Add("吸血");
        if (wc.FreezeBonus > 0) keywords.Add("冰冻");
        if (wc.PoisonBonus > 0) keywords.Add("中毒");
        if (wc.GrantsAura) keywords.Add("护盾");
        if (wc.HealthCost > 0) keywords.Add("⚠消耗生命");
        if (wc.MeleeEffects.Count > 0 || wc.RangedEffects.Count > 0 || wc.MagicEffects.Count > 0)
            keywords.Add("特效");

        descNode.Text = keywords.Count > 0 ? string.Join(" ", keywords) : "基础";
        rarityNode.Text = WeaponFormatter.GetRarityText(wc.Rarity);

        // Icon
        if (wc.IconTexture != null && iconNode != null)
        {
            var existingTex = iconNode.GetParent()?.GetNodeOrNull<TextureRect>("IconTex");
            if (existingTex != null)
            {
                existingTex.Texture = wc.IconTexture;
            }
            else
            {
                var texRect = new TextureRect
                {
                    Name = "IconTex",
                    Texture = wc.IconTexture,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                    CustomMinimumSize = new Vector2(60, 60)
                };
                iconNode.GetParent()?.RemoveChild(iconNode);
                iconNode.GetParent()?.AddChild(texRect);
            }
        }
        else if (iconNode != null)
        {
            iconNode.Text = wc.Icon;
        }

        // Rarity colors
        Color borderColor;
        switch (wc.Rarity)
        {
            case 2: borderColor = RarityRare; break;
            case 3: borderColor = RarityEpic; break;
            case 4: borderColor = new Color(1f, 0.8f, 0.25f); titleNode.AddThemeColorOverride("font_color", borderColor); rarityNode.AddThemeColorOverride("font_color", borderColor); break;
            default: borderColor = RarityCommon; break;
        }

        var hintNode = (Label)vbox.GetNode("Hint");
        hintNode.AddThemeColorOverride("font_color", new Color(0.45f, 0.38f, 0.38f));

        var cardBg = new Color(0.18f, 0.13f, 0.13f, 0.95f);
        button.AddThemeStyleboxOverride("normal", MakeCardPanel(cardBg, borderColor));

        var hoverBg = new Color(0.25f, 0.18f, 0.18f, 0.95f);
        var hoverBorder = _Lighten(borderColor, 0.2f);
        button.AddThemeStyleboxOverride("hover", MakeCardPanel(hoverBg, hoverBorder));
    }

    private void _RenderUpgradeCard(Button button, UpgradeCard upgrade, Label categoryNode, Label titleNode,
        Label typeNode, Label? iconNode, Label descNode, Label rarityNode, VBoxContainer vbox)
    {
        categoryNode.Text = "【强化卡】";
        titleNode.Text = upgrade.DisplayName;
        typeNode.Text = "强化已有装备";
        if (iconNode != null)
            iconNode.Text = upgrade.Icon;
        upgrade.SyncDescription();
        descNode.Text = upgrade.Description;
        rarityNode.Text = "升级";

        var upBg = new Color(0.18f, 0.13f, 0.13f, 0.95f);
        var upBorder = RarityRare;
        titleNode.AddThemeColorOverride("font_color", RarityRare);
        rarityNode.AddThemeColorOverride("font_color", RarityRare);

        button.AddThemeStyleboxOverride("normal", MakeCardPanel(upBg, upBorder));
        button.AddThemeStyleboxOverride("hover", MakeCardPanel(new Color(0.25f, 0.18f, 0.18f, 0.95f), _Lighten(upBorder, 0.2f)));
    }

    private void _OnCardMouseEntered(int index)
    {
        // Tooltip showing is handled by UIManager
    }

    private void _OnCardMouseExited()
    {
        // Handled by UIManager
    }

    public void OnCardPressed(int index)
    {
        if (index < _currentOptions.Count)
        {
            CardSelected?.Invoke(_currentOptions[index]);
            HideSelection();
        }
    }

    public void OnSkipPressed()
    {
        Skipped?.Invoke();
        HideSelection();
    }

    private static Color _Lighten(Color c, float amount)
    {
        return new Color(
            Mathf.Min(1.0f, c.R + amount),
            Mathf.Min(1.0f, c.G + amount),
            Mathf.Min(1.0f, c.B + amount),
            c.A);
    }
}