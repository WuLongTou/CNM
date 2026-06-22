namespace ZombieWars.UI;

using Godot;
using System;
using System.Collections.Generic;
using ZombieWars.Core;
using ZombieWars.Weapons;
using Player = ZombieWars.Player.Player;
using static ZombieWars.Core.GameConfig;
using static ZombieWars.Core.GameEnums;
using static ZombieWars.UI.HandDrawnTheme;
using static ZombieWars.UI.HandDrawnTheme.Colors;

/// <summary>武器选择面板 — 从 UIManager 拆分的独立组件。</summary>
public partial class WeaponSelectionPanel : Control
{
    public event Action<object?> WeaponCardSelected = delegate { };
    public event Action UpgradeSkipped = delegate { };

    private Panel _panel = null!;
    private readonly List<Button> _weaponButtons = new();
    private Button? _skipButton;
    private VBoxContainer? _mainVbox;
    private HBoxContainer? _buttonContainer;
    private Label? _titleLabel;
    private Label? _descLabel;
    private Label? _currentWeaponLabel;
    private readonly List<object> _currentWeaponOptions = new();

    private GameManager? _gameManager;
    private WeaponDeck? _weaponDeck;
    private Func<WeaponCard, string>? _getWeaponTooltip;
    private Func<UpgradeCard, string>? _getUpgradeTooltip;
    private Action<Control, string>? _showTooltip;
    private Action? _hideTooltip;

    public void Setup(GameManager gm, WeaponDeck deck,
        Func<WeaponCard, string>? getWeaponTooltip,
        Func<UpgradeCard, string>? getUpgradeTooltip,
        Action<Control, string>? showTooltip,
        Action? hideTooltip)
    {
        _gameManager = gm;
        _weaponDeck = deck;
        _getWeaponTooltip = getWeaponTooltip;
        _getUpgradeTooltip = getUpgradeTooltip;
        _showTooltip = showTooltip;
        _hideTooltip = hideTooltip;
    }

    public void Open()
    {
        if (_weaponDeck == null)
        {
            LogUtils.PrintErr("错误：武器牌堆未设置");
            return;
        }

        _currentWeaponOptions.Clear();
        var drawn = _gameManager?.PlayerRef != null
            ? _weaponDeck.DrawThreeCards(_gameManager.PlayerRef)
            : _weaponDeck.DrawThreeCards();
        foreach (var w in drawn) _currentWeaponOptions.Add(w);

        for (int i = 0; i < 3; i++)
        {
            if (i < _currentWeaponOptions.Count)
            {
                var weapon = _currentWeaponOptions[i];
                var button = _weaponButtons[i];
                button.Visible = true;

                if (weapon is UpgradeCard upgrade)
                {
                    _RenderUpgradeCard(button, upgrade);
                    continue;
                }

                if (weapon is not WeaponCard wc) continue;

                var margin = (MarginContainer)button.GetChild(0);
                var vbox = (VBoxContainer)margin.GetChild(0);
                var categoryNode = (Label)vbox.GetNode("Category");
                var titleNode = (Label)vbox.GetNode("Title");
                var typeNode = (Label)vbox.GetNode("Type");
                var iconContainer = vbox.GetNodeOrNull<Control>("IconContainer");
                var iconNode = iconContainer?.GetNodeOrNull<Label>("Icon");
                var descNode = (RichTextLabel)vbox.GetNode("Desc");
                var rarityNode = (Label)vbox.GetNode("Rarity");

                titleNode.Text = wc.CardName;
                var upgradeCategory = wc.UpgradeCategory;
                if (string.IsNullOrEmpty(upgradeCategory))
                    upgradeCategory = wc.Category == WeaponCategory.Weapon ? "武器" : "装备";
                categoryNode.Text = $"【{upgradeCategory} Lv.{wc.Tier}】";
                typeNode.Text = wc.Category == WeaponCategory.Weapon ? wc.GetWeaponTypeText() : "装备";

                var keywords = new List<string>();
                if (wc.DamageModifier > 1.0f) keywords.Add("[color=#e6b800]伤害↑[/color]");
                else if (wc.DamageModifier < 1.0f) keywords.Add("[color=#e74c3c]伤害↓[/color]");
                if (wc.AttackSpeedModifier > 1.0f) keywords.Add("[color=#e6b800]攻速↑[/color]");
                else if (wc.AttackSpeedModifier < 1.0f) keywords.Add("[color=#e74c3c]攻速↓[/color]");
                if (wc.HealthModifier > 0) keywords.Add("[color=#4caf50]生命↑[/color]");
                else if (wc.HealthModifier < 0) keywords.Add("[color=#e74c3c]生命↓[/color]");
                if (wc.SpeedModifier > 0) keywords.Add("[color=#e6b800]速度↑[/color]");
                if (wc.CriticalBonus > 0) keywords.Add("[color=#e6b800]暴击[/color]");
                if (wc.LifeStealBonus > 0) keywords.Add("[color=#ce5c5c]吸血[/color]");
                if (wc.FreezeBonus > 0) keywords.Add("[color=#5b9bd5]冰冻[/color]");
                if (wc.PoisonBonus > 0) keywords.Add("[color=#7baf5b]中毒[/color]");
                if (wc.GrantsAura) keywords.Add("[color=#5b9bd5]护盾[/color]");
                if (wc.HealthCost > 0) keywords.Add("[color=#e74c3c]消耗生命[/color]");
                if (wc.MeleeEffects.Count > 0 || wc.RangedEffects.Count > 0 || wc.MagicEffects.Count > 0)
                    keywords.Add("[color=#8a8acc]特效[/color]");
                descNode.Text = keywords.Count > 0 ? string.Join(" ", keywords) : "[color=#666666]基础[/color]";
                rarityNode.Text = WeaponFormatter.GetRarityText(wc.Rarity);

                var viewport = ResponsiveUi.ViewportSize(this);
                if (iconContainer != null && wc.IconTexture != null)
                {
                    var existingTex = iconContainer.GetNodeOrNull<TextureRect>("IconTex");
                    if (existingTex != null)
                    {
                        existingTex.Texture = wc.IconTexture;
                        existingTex.CustomMinimumSize = ResponsiveUi.Size(60, 60, viewport, 38);
                        existingTex.Visible = true;
                    }
                    else
                    {
                        var texRect = new TextureRect
                        {
                            Name = "IconTex", Texture = wc.IconTexture,
                            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                            CustomMinimumSize = ResponsiveUi.Size(60, 60, viewport, 38)
                        };
                        iconContainer.AddChild(texRect);
                    }
                    if (iconNode != null) iconNode.Visible = false;
                }
                else if (iconContainer != null)
                {
                    var existingTex = iconContainer.GetNodeOrNull<TextureRect>("IconTex");
                    if (existingTex != null) existingTex.Visible = false;
                    if (iconNode == null)
                    {
                        iconNode = new Label
                        {
                            Name = "Icon",
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        iconNode.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(55, viewport, 32));
                        iconContainer.AddChild(iconNode);
                    }
                    iconNode.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(55, viewport, 32));
                    iconNode.Text = wc.Icon;
                    iconNode.Visible = true;
                }

                var cardBg = new Color(0.18f, 0.13f, 0.13f, 0.95f);
                Color borderColor;
                switch (wc.Rarity)
                {
                    case 1: borderColor = RarityCommon; titleNode.AddThemeColorOverride("font_color", RarityCommon); rarityNode.AddThemeColorOverride("font_color", RarityCommon); break;
                    case 2: borderColor = RarityRare; titleNode.AddThemeColorOverride("font_color", RarityRare); rarityNode.AddThemeColorOverride("font_color", RarityRare); break;
                    case 3: borderColor = RarityEpic; titleNode.AddThemeColorOverride("font_color", RarityEpic); rarityNode.AddThemeColorOverride("font_color", RarityEpic); break;
                    case 4: borderColor = RarityLegendary; titleNode.AddThemeColorOverride("font_color", RarityLegendary); rarityNode.AddThemeColorOverride("font_color", RarityLegendary); break;
                    default: borderColor = RarityCommon; titleNode.AddThemeColorOverride("font_color", RarityCommon); rarityNode.AddThemeColorOverride("font_color", RarityCommon); break;
                }

                var dimText = new Color(0.6f, 0.5f, 0.5f);
                categoryNode.AddThemeColorOverride("font_color", dimText);
                typeNode.AddThemeColorOverride("font_color", dimText);
                descNode.AddThemeColorOverride("default_color", dimText);
                var hintNode = (Label)vbox.GetNode("Hint");
                hintNode.AddThemeColorOverride("font_color", new Color(0.45f, 0.38f, 0.38f));

                button.AddThemeStyleboxOverride("normal", MakeCardPanel(cardBg, borderColor));
                var hoverBg = new Color(0.25f, 0.18f, 0.18f, 0.95f);
                button.AddThemeStyleboxOverride("hover", MakeCardPanel(hoverBg, ColorUtils.Lighten(borderColor, 0.2f)));
            }
            else
                _weaponButtons[i].Visible = false;
        }

        _panel.Visible = true;
    }

    public void Close()
    {
        _panel.Visible = false;
        _hideTooltip?.Invoke();
    }

    public bool IsOpen() => _panel.Visible;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var viewport = ResponsiveUi.ViewportSize(this);
        _panel = new Panel
        {
            Visible = false,
            ProcessMode = ProcessModeEnum.Always,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var weaponStyle = MakeDarkPanel(0.92f);
        weaponStyle.BgColor = new Color(BattlefieldBg, 0.95f);
        _panel.AddThemeStyleboxOverride("panel", weaponStyle);
        AddChild(_panel);

        var centerContainer = new CenterContainer();
        centerContainer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _panel.AddChild(centerContainer);

        var mainVbox = new VBoxContainer();
        mainVbox.AddThemeConstantOverride("separation", ResponsiveUi.Px(UiSpacingSection, viewport, 12));
        centerContainer.AddChild(mainVbox);
        _mainVbox = mainVbox;

        var titleLabel = new Label
        {
            Text = "【 选择本回合强化 】",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        titleLabel.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(32, viewport, 22));
        titleLabel.AddThemeColorOverride("font_color", BloodBright);
        mainVbox.AddChild(titleLabel);
        _titleLabel = titleLabel;

        var weaponSlotPanel = new Panel();
        weaponSlotPanel.CustomMinimumSize = new Vector2(ResponsiveUi.FitWidth(720, viewport, 0.9f), 0);
        weaponSlotPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        var slotStyle = MakeNotePanel(borderRadius: 3f, shadowSize: 2f);
        slotStyle.BgColor = new Color(0.12f, 0.08f, 0.06f, 0.6f);
        slotStyle.BorderColor = new Color(0.35f, 0.25f, 0.15f, 0.4f);
        weaponSlotPanel.AddThemeStyleboxOverride("panel", slotStyle);
        var slotMargin = new MarginContainer();
        slotMargin.AddThemeConstantOverride("margin_left", 8);
        slotMargin.AddThemeConstantOverride("margin_right", 8);
        slotMargin.AddThemeConstantOverride("margin_top", 4);
        slotMargin.AddThemeConstantOverride("margin_bottom", 4);
        weaponSlotPanel.AddChild(slotMargin);

        _currentWeaponLabel = new Label
        {
            Text = "剑 当前武器: 无",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _currentWeaponLabel.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontEmph, viewport, 13));
        _currentWeaponLabel.AddThemeColorOverride("font_color", new Color(0.86f, 0.78f, 0.55f));
        slotMargin.AddChild(_currentWeaponLabel);
        mainVbox.AddChild(weaponSlotPanel);

        var descLabel = new Label
        {
            Text = "你可能抽到新武器、装备，或对已有装备的强化卡",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(ResponsiveUi.FitWidth(720, viewport, 0.9f), 0)
        };
        descLabel.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(18, viewport, 13));
        descLabel.AddThemeColorOverride("font_color", Godot.Colors.White);
        descLabel.HorizontalAlignment = HorizontalAlignment.Center;
        mainVbox.AddChild(descLabel);
        _descLabel = descLabel;

        var buttonContainer = new HBoxContainer();
        buttonContainer.AddThemeConstantOverride("separation", ResponsiveUi.Px(UiSpacingWide, viewport, 8));
        buttonContainer.Alignment = BoxContainer.AlignmentMode.Center;
        mainVbox.AddChild(buttonContainer);
        _buttonContainer = buttonContainer;

        for (int i = 0; i < 3; i++)
        {
            int cardIndex = i;
            var button = new Button
            {
                CustomMinimumSize = ResponsiveUi.Size(200, 280, viewport, 120),
                FocusMode = Control.FocusModeEnum.None
            };
            button.MouseEntered += () => _OnCardHovered(cardIndex);
            button.MouseExited += _OnCardUnhovered;
            button.Pressed += () => _OnCardClicked(cardIndex);
            buttonContainer.AddChild(button);
            _weaponButtons.Add(button);

            var cardVbox = new VBoxContainer
            {
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            cardVbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            cardVbox.AddThemeConstantOverride("separation", ResponsiveUi.Px(UiSpacingNormal, viewport, 4));

            var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_top", ResponsiveUi.Px(UiMarginMedium, viewport, 6));
            margin.AddThemeConstantOverride("margin_bottom", ResponsiveUi.Px(UiMarginMedium, viewport, 6));
            margin.AddThemeConstantOverride("margin_left", ResponsiveUi.Px(UiMarginMedium, viewport, 6));
            margin.AddThemeConstantOverride("margin_right", ResponsiveUi.Px(UiMarginMedium, viewport, 6));
            button.AddChild(margin);
            margin.AddChild(cardVbox);

            var categoryLabel = new Label { Name = "Category", Text = "【类型】", HorizontalAlignment = HorizontalAlignment.Center };
            categoryLabel.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontSmall, viewport, 9));
            categoryLabel.AddThemeColorOverride("font_color", NoteText);
            cardVbox.AddChild(categoryLabel);

            var title = new Label { Name = "Title", Text = "武器名称", HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            title.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontTitle, viewport, 15));
            cardVbox.AddChild(title);

            cardVbox.AddChild(new HSeparator { MouseFilter = Control.MouseFilterEnum.Ignore });

            var iconContainer = new CenterContainer { Name = "IconContainer", CustomMinimumSize = new Vector2(0, ResponsiveUi.Px(70, viewport, 42)), MouseFilter = Control.MouseFilterEnum.Ignore };
            cardVbox.AddChild(iconContainer);
            var iconLabel = new Label { Name = "Icon", Text = "剑", HorizontalAlignment = HorizontalAlignment.Center };
            iconLabel.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(55, viewport, 32));
            iconContainer.AddChild(iconLabel);

            var typeLabel = new Label { Name = "Type", Text = "类型", HorizontalAlignment = HorizontalAlignment.Center };
            typeLabel.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontBody, viewport, 10));
            typeLabel.AddThemeColorOverride("font_color", NoteText);
            cardVbox.AddChild(typeLabel);

            var desc = new RichTextLabel
            {
                Name = "Desc", Text = "", BbcodeEnabled = true, FitContent = true,
                AutowrapMode = TextServer.AutowrapMode.WordSmart, ScrollActive = false,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            desc.AddThemeFontSizeOverride("normal_font_size", ResponsiveUi.Font(15, viewport, 11));
            desc.AddThemeColorOverride("default_color", NoteText);
            cardVbox.AddChild(desc);

            cardVbox.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });

            var rarity = new Label { Name = "Rarity", Text = "稀有度", HorizontalAlignment = HorizontalAlignment.Center };
            rarity.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontBody, viewport, 10));
            cardVbox.AddChild(rarity);

            var hintLabel = new Label { Name = "Hint", Text = "(i) 悬停查看详情", HorizontalAlignment = HorizontalAlignment.Center };
            hintLabel.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontMicro, viewport, 9));
            hintLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.55f, 0.45f));
            cardVbox.AddChild(hintLabel);
        }

        var keyboardHintLabel = new Label
        {
            Text = "※ [1] [2] [3] 选择  |  [Enter] 或 [Delete] 跳过",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        keyboardHintLabel.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontBody, viewport, 11));
        keyboardHintLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.6f, 0.55f));
        mainVbox.AddChild(keyboardHintLabel);

        var skipContainer = new CenterContainer();
        mainVbox.AddChild(skipContainer);
        _skipButton = new Button
        {
            Text = ">> 跳过 [Delete]",
            CustomMinimumSize = ResponsiveUi.Size(240, 44, viewport, 36)
        };
        _skipButton.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontEmph, viewport, 13));
        var skipNormal = MakeNotePanel(borderRadius: 4f, shadowSize: 3f);
        skipNormal.BgColor = new Color(0.35f, 0.25f, 0.22f, 0.92f);
        skipNormal.BorderColor = new Color(0.55f, 0.35f, 0.25f, 0.7f);
        _skipButton.AddThemeStyleboxOverride("normal", skipNormal);
        var skipHover = MakeNotePanel(borderRadius: 4f, shadowSize: 3f);
        skipHover.BgColor = new Color(0.50f, 0.35f, 0.28f, 0.95f);
        skipHover.BorderColor = new Color(0.75f, 0.50f, 0.35f, 0.9f);
        _skipButton.AddThemeStyleboxOverride("hover", skipHover);
        _skipButton.AddThemeColorOverride("font_color", new Color(0.9f, 0.75f, 0.6f));
        _skipButton.Pressed += _OnSkipClicked;
        skipContainer.AddChild(_skipButton);
    }

    public void ApplyResponsiveLayout()
    {
        var viewportSize = ResponsiveUi.ViewportSize(this);
        _mainVbox?.AddThemeConstantOverride("separation", ResponsiveUi.Px(UiSpacingSection, viewportSize, 12));
        _buttonContainer?.AddThemeConstantOverride("separation", ResponsiveUi.Px(UiSpacingWide, viewportSize, 8));
        _titleLabel?.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(32, viewportSize, 22));
        if (_currentWeaponLabel != null)
        {
            _currentWeaponLabel.CustomMinimumSize = new Vector2(ResponsiveUi.FitWidth(720, viewportSize, 0.9f), 0);
            _currentWeaponLabel.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontEmph, viewportSize, 12));
        }
        _descLabel?.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(18, viewportSize, 13));
        if (_descLabel != null)
            _descLabel.CustomMinimumSize = new Vector2(ResponsiveUi.FitWidth(720, viewportSize, 0.9f), 0);
        foreach (var button in _weaponButtons)
            button.CustomMinimumSize = ResponsiveUi.Size(200, 280, viewportSize, 120);
        if (_skipButton != null)
        {
            _skipButton.CustomMinimumSize = ResponsiveUi.Size(150, 40, viewportSize, 30);
            _skipButton.AddThemeFontSizeOverride("font_size", ResponsiveUi.Font(UiFontEmph, viewportSize, 12));
        }
    }

    public void UpdateCurrentWeaponLabel(Player player)
    {
        if (_currentWeaponLabel == null) return;
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
            ? "剑 当前武器: 无"
            : $"剑 {weaponName} ({weaponTypeText}) | {effectText}";
    }

    public override void _Input(InputEvent @event)
    {
        if (!_panel.Visible) return;

        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            int? pick = key.Keycode switch
            {
                Key.Key1 => 0,
                Key.Key2 => 1,
                Key.Key3 => 2,
                _ => null
            };
            if (pick.HasValue && pick.Value < _currentWeaponOptions.Count)
            {
                _OnCardClicked(pick.Value);
                GetViewport().SetInputAsHandled();
                return;
            }
            if (key.Keycode == Key.Enter || key.Keycode == Key.Delete)
            {
                _OnSkipClicked();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void _OnCardClicked(int index)
    {
        if (index < _currentWeaponOptions.Count)
        {
            var selected = _currentWeaponOptions[index];
            WeaponCardSelected?.Invoke(selected);
            Close();
            GameServices.Audio.PlayWeaponSelect();
        }
    }

    private void _OnSkipClicked()
    {
        UpgradeSkipped?.Invoke();
        Close();
    }

    private void _OnCardHovered(int index)
    {
        if (index < _currentWeaponOptions.Count)
        {
            var weapon = _currentWeaponOptions[index];
            if (weapon is UpgradeCard upgrade && _getUpgradeTooltip != null && _showTooltip != null)
                _showTooltip(_weaponButtons[index], _getUpgradeTooltip(upgrade));
            else if (weapon is WeaponCard wc && _getWeaponTooltip != null && _showTooltip != null)
                _showTooltip(_weaponButtons[index], _getWeaponTooltip(wc));
        }
    }

    private void _OnCardUnhovered()
    {
        _hideTooltip?.Invoke();
    }

    private void _RenderUpgradeCard(Button button, UpgradeCard upgrade)
    {
        var margin = (MarginContainer)button.GetChild(0);
        var vbox = (VBoxContainer)margin.GetChild(0);
        var categoryNode = (Label)vbox.GetNode("Category");
        var titleNode = (Label)vbox.GetNode("Title");
        var typeNode = (Label)vbox.GetNode("Type");
        var iconNode = vbox.FindChild("Icon", true, false) as Label;
        var descNode = (RichTextLabel)vbox.GetNode("Desc");
        var rarityNode = (Label)vbox.GetNode("Rarity");

        categoryNode.Text = "【强化卡】";
        titleNode.Text = upgrade.DisplayName;
        typeNode.Text = "强化已有装备";
        if (iconNode != null) iconNode.Text = upgrade.Icon;
        upgrade.SyncDescription();
        descNode.Text = _UpgradeDescToBBCode(upgrade.Description);
        rarityNode.Text = "升级";

        var upBg = new Color(0.18f, 0.13f, 0.13f, 0.95f);
        var upBorder = RarityRare;
        titleNode.AddThemeColorOverride("font_color", RarityRare);
        rarityNode.AddThemeColorOverride("font_color", RarityRare);

        button.AddThemeStyleboxOverride("normal", MakeCardPanel(upBg, upBorder));
        var hoverBg = new Color(0.25f, 0.18f, 0.18f, 0.95f);
        button.AddThemeStyleboxOverride("hover", MakeCardPanel(hoverBg, ColorUtils.Lighten(upBorder, 0.2f)));
    }

    private static string _UpgradeDescToBBCode(string plain)
    {
        var result = plain;
        result = System.Text.RegularExpressions.Regex.Replace(result,
            @"(\+[\d.]+%?)", m => $"[color=#d4a04a]{m.Value}[/color]");
        result = System.Text.RegularExpressions.Regex.Replace(result,
            @"(生命\+[\d]+)", m => $"[color=#4caf50]{m.Value}[/color]");
        result = System.Text.RegularExpressions.Regex.Replace(result,
            @"(消耗生命\+[\d]+)", m => $"[color=#e74c3c]{m.Value}[/color]");
        result = System.Text.RegularExpressions.Regex.Replace(result,
            @"(生命[\-\d]+)", m => $"[color=#e74c3c]{m.Value}[/color]");
        result = System.Text.RegularExpressions.Regex.Replace(result,
            @"(速度[\-\d]+)", m => $"[color=#e74c3c]{m.Value}[/color]");
        result = System.Text.RegularExpressions.Regex.Replace(result,
            @"(特效强化)", m => $"[color=#8a8acc]{m.Value}[/color]");
        return result;
    }

    private static string _TruncateText(string text, int maxLen)
    {
        if (text.Length <= maxLen) return text;
        return text.Substring(0, maxLen - 1) + "…";
    }
}
