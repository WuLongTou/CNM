namespace ZombieWars.UI;

using Godot;
using ZombieWars.Core;

public static class HandDrawnTheme
{
    public static class Colors
    {
        // ══ 手绘血色涂鸦调（2026-06 涂鸦版原版色，恢复自 8a73dcf）══
        public static readonly Color BattlefieldBg = new(0.078f, 0.039f, 0.039f);
        public static readonly Color BattlefieldMid = new(0.239f, 0.102f, 0.102f);
        public static readonly Color BattlefieldAccent = new(0.753f, 0.224f, 0.169f);
        public static readonly Color BattlefieldBright = new(0.906f, 0.298f, 0.235f);

        public static readonly Color NoteBg = new(0.941f, 0.910f, 0.910f);        // 灰白纸
        public static readonly Color NoteBgHover = new(0.96f, 0.94f, 0.94f);
        public static readonly Color NoteMid = new(0.878f, 0.816f, 0.816f);
        public static readonly Color NoteBorder = new(0.545f, 0.227f, 0.227f);    // 暗红描边
        public static readonly Color NoteText = new(0.290f, 0.102f, 0.102f);      // 深红墨
        public static readonly Color NoteTextDim = new(0.353f, 0.227f, 0.227f);

        public static readonly Color BloodRed = new(0.753f, 0.224f, 0.169f);      // 血红
        public static readonly Color BloodBright = new(0.906f, 0.298f, 0.235f);   // 亮血红
        public static readonly Color InkBlack = new(0.09f, 0.04f, 0.04f);
        public static readonly Color PencilGray = new(0.35f, 0.28f, 0.28f);
        public static readonly Color NetherPurple = new(0.32f, 0.14f, 0.14f);     // 暗红（原炼紫位）
        public static readonly Color CandleGlow = new(0.953f, 0.612f, 0.071f);    // 血橙

        public static readonly Color RarityCommon = new(0.4f, 0.35f, 0.35f);
        public static readonly Color RarityRare = BloodRed;
        public static readonly Color RarityEpic = new(0.953f, 0.612f, 0.071f);
        public static readonly Color RarityLegendary = new(0.95f, 0.75f, 0.22f);
        public static readonly Color RarityCommonBg = new(0.25f, 0.2f, 0.2f, 0.95f);
        public static readonly Color RarityRareBg = new(0.2f, 0.1f, 0.1f, 0.95f);
        public static readonly Color RarityEpicBg = new(0.25f, 0.2f, 0.1f, 0.95f);

        public static readonly Color UpgradeBg = new(0.2f, 0.15f, 0.15f, 0.95f);
        public static readonly Color UpgradeBorder = new(0.6f, 0.35f, 0.35f);
        public static readonly Color UpgradeTitle = new(0.85f, 0.65f, 0.65f);

        public static readonly Color HpHigh = new(0.65f, 0.35f, 0.35f);
        public static readonly Color HpMed = new(0.75f, 0.45f, 0.25f);
        public static readonly Color HpLow = BloodRed;

        // ═══ HUD 通用底色与描边（暗血色涂鸦风）══
        public static readonly Color HudBottomBg = new(0.078f, 0.039f, 0.039f, 0.92f);
        public static readonly Color HudBottomBorder = new(0.545f, 0.227f, 0.227f, 0.6f);
        public static readonly Color HudTopBorder = new(0.239f, 0.102f, 0.102f, 0.4f);
        public static readonly Color HudChipBg = new(0.15f, 0.07f, 0.07f, 0.70f);
        public static readonly Color HudGroupBg = new(0.06f, 0.03f, 0.03f, 0.50f);
        public static readonly Color HudGroupBorder = new(0.30f, 0.12f, 0.12f, 0.35f);

        public static readonly Color NotePaperShadow = new(0f, 0f, 0f, 0.45f);
    }

    public static StyleBoxFlat MakeNotePanel(
        float rotationDeg = 0f,
        float borderWidth = 1.5f,
        bool dashed = false,
        float borderRadius = 2f,
        float shadowSize = 4f)
    {
        var bw = Mathf.RoundToInt(borderWidth);
        var cr = Mathf.RoundToInt(borderRadius);
        var ss = Mathf.RoundToInt(shadowSize);

        var style = new StyleBoxFlat
        {
            BgColor = Colors.NoteBg,
            BorderColor = dashed ? new Color(Colors.NoteBorder, 0.6f) : Colors.NoteBorder,
            BorderWidthBottom = bw,
            BorderWidthLeft = bw,
            BorderWidthRight = bw,
            BorderWidthTop = bw,
            ShadowColor = Colors.NotePaperShadow,
            ShadowSize = ss,
            CornerRadiusBottomLeft = cr,
            CornerRadiusBottomRight = cr,
            CornerRadiusTopLeft = cr,
            CornerRadiusTopRight = cr,
        };

        return style;
    }

    public static StyleBoxFlat MakeCardPanel(
        Color bgColor,
        Color borderColor,
        float borderWidth = 1.5f,
        float borderRadius = 3f)
    {
        var bw = Mathf.RoundToInt(borderWidth);
        var cr = Mathf.RoundToInt(borderRadius);

        return new StyleBoxFlat
        {
            BgColor = bgColor,
            BorderColor = borderColor,
            BorderWidthBottom = bw,
            BorderWidthLeft = bw,
            BorderWidthRight = bw,
            BorderWidthTop = bw,
            ShadowColor = Colors.NotePaperShadow,
            ShadowSize = 6,
            CornerRadiusBottomLeft = cr,
            CornerRadiusBottomRight = cr,
            CornerRadiusTopLeft = cr,
            CornerRadiusTopRight = cr,
        };
    }

    public static StyleBoxFlat MakeDarkPanel(float alpha = 0.92f, bool withBorder = false)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(Colors.BattlefieldBg, alpha),
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
        };
        if (withBorder)
        {
            // 还原原 AlchemyTheme.MakeDarkPanel 自带的 1px 羊皮纸边框（数学系统指示器面板用）
            style.BorderColor = new Color(Colors.NoteMid, alpha * 0.6f);
            style.BorderWidthTop = 1;
            style.BorderWidthBottom = 1;
            style.BorderWidthLeft = 1;
            style.BorderWidthRight = 1;
        }
        return style;
    }

    public static StyleBoxFlat MakeHpBarBg()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.05f, 0.05f),
            BorderColor = new Color(Colors.InkBlack, 0.8f),
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
        };
    }

    public static StyleBoxFlat MakeHpBarFill(Color color)
    {
        return new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
        };
    }

    /// <summary>左侧 3px 强调色条的"信息卡"样式（用于 HelpPanel 提示行等）。</summary>
    public static StyleBoxFlat MakeAccentBorder(
        Color bgColor, Color accentColor,
        int radius = 4, int borderWidth = 3)
    {
        return new StyleBoxFlat
        {
            BgColor = bgColor,
            BorderWidthLeft = borderWidth,
            BorderColor = accentColor,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
        };
    }

    /// <summary>返回 (normal, hover, pressed) 三个 StyleBoxFlat 按钮样式，统一 shadowSize=3 / radius=4 / borderWidth=2。
    /// bottomOnlyBorder=true 时仅显示底部边框（用于 HudPanel 道具图标强调色条）；shadowSize=0 时清空 ShadowColor 字段以保持无阴影。</summary>
    public static (StyleBoxFlat normal, StyleBoxFlat hover, StyleBoxFlat pressed) MakeButtonStyles(
        Color bgColor, Color hoverColor, Color borderColor,
        int radius = 4, int shadowSize = 3,
        bool bottomOnlyBorder = false)
    {
        var normal = MakeNotePanel(borderRadius: radius, shadowSize: shadowSize);
        normal.BgColor = bgColor;
        normal.BorderColor = borderColor;
        if (bottomOnlyBorder) { normal.BorderWidthTop = 0; normal.BorderWidthRight = 0; normal.BorderWidthLeft = 0; }
        if (shadowSize == 0) { normal.ShadowColor = new Color(0, 0, 0, 0); normal.ShadowSize = 0; }

        var hover = MakeNotePanel(borderRadius: radius, shadowSize: shadowSize);
        hover.BgColor = hoverColor;
        hover.BorderColor = borderColor;
        if (bottomOnlyBorder) { hover.BorderWidthTop = 0; hover.BorderWidthRight = 0; hover.BorderWidthLeft = 0; }
        if (shadowSize == 0) { hover.ShadowColor = new Color(0, 0, 0, 0); hover.ShadowSize = 0; }

        var pressed = MakeNotePanel(borderRadius: radius, shadowSize: shadowSize);
        pressed.BgColor = ColorUtils.Darken(bgColor, 0.1f);
        pressed.BorderColor = borderColor;
        if (bottomOnlyBorder) { pressed.BorderWidthTop = 0; pressed.BorderWidthRight = 0; pressed.BorderWidthLeft = 0; }
        if (shadowSize == 0) { pressed.ShadowColor = new Color(0, 0, 0, 0); pressed.ShadowSize = 0; }

        return (normal, hover, pressed);
    }

    /// <summary>预制带主题覆盖的"标题"Label（font_size + color + outline + outline_color）。</summary>
    public static Label MakeTitleLabel(
        string text, int fontSize, Color color,
        int outlineSize = 2, Color? outlineColor = null,
        HorizontalAlignment hAlign = HorizontalAlignment.Left,
        VerticalAlignment vAlign = VerticalAlignment.Center)
    {
        var lbl = new Label
        {
            Text = text,
            HorizontalAlignment = hAlign,
            VerticalAlignment = vAlign
        };
        lbl.AddThemeFontSizeOverride("font_size", fontSize);
        lbl.AddThemeColorOverride("font_color", color);
        lbl.AddThemeConstantOverride("outline_size", outlineSize);
        lbl.AddThemeColorOverride("font_outline_color", outlineColor ?? Colors.InkBlack);
        return lbl;
    }

    /// <summary>
    /// 一体化按钮工厂：三态样式 + 字色 + 悬停/按下缩放动画（hover 1.03 / pressed 0.97）。
    /// 缩放围绕按钮中心（PivotOffset 随尺寸变化自动居中）。取代各面板手挂样式的样板代码。
    /// </summary>
    public static void StyleButton(
        Button btn, Color bgColor, Color hoverBg, Color borderColor,
        Color? fontColor = null, int radius = 4, int shadowSize = 3,
        bool bottomOnlyBorder = false)
    {
        var (n, h, p) = MakeButtonStyles(bgColor, hoverBg, borderColor, radius, shadowSize, bottomOnlyBorder);
        btn.AddThemeStyleboxOverride("normal", n);
        btn.AddThemeStyleboxOverride("hover", h);
        btn.AddThemeStyleboxOverride("pressed", p);
        btn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        if (fontColor.HasValue)
        {
            btn.AddThemeColorOverride("font_color", fontColor.Value);
            btn.AddThemeColorOverride("font_hover_color", ColorUtils.Lighten(fontColor.Value, 0.15f));
            btn.AddThemeColorOverride("font_pressed_color", fontColor.Value);
        }

        btn.Resized += () => btn.PivotOffset = btn.Size / 2f;
        btn.MouseEntered += () => _Punch(btn, 1.03f);
        btn.MouseExited += () => _Punch(btn, 1.0f);
        btn.ButtonDown += () => _Punch(btn, 0.97f);
        btn.ButtonUp += () => _Punch(btn, 1.03f);
    }

    private static void _Punch(Control ctrl, float targetScale)
    {
        var tw = ctrl.CreateTween();
        tw.TweenProperty(ctrl, "scale", new Vector2(targetScale, targetScale), 0.08f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    /// <summary>
    /// 装饰面板：双层边框（外深内亮）+ 四角角标，返回外层容器（调用方 AddChild 即用）。
    /// content 会被放进内层并自动获得边距。
    /// </summary>
    public static PanelContainer MakeOrnatePanel(Control content, Color bgColor, Color borderColor,
        int outerBorder = 2, int innerBorder = 1, int margin = 12, int radius = 6)
    {
        var outer = new StyleBoxFlat
        {
            BgColor = bgColor,
            BorderColor = borderColor,
            BorderWidthBottom = outerBorder,
            BorderWidthLeft = outerBorder,
            BorderWidthRight = outerBorder,
            BorderWidthTop = outerBorder,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            ShadowColor = Colors.NotePaperShadow,
            ShadowSize = 8,
        };
        var inner = new StyleBoxFlat
        {
            BgColor = new Color(Colors.BattlefieldBright, 0.06f),
            BorderColor = new Color(Colors.BattlefieldBright, 0.35f),
            BorderWidthBottom = innerBorder,
            BorderWidthLeft = innerBorder,
            BorderWidthRight = innerBorder,
            BorderWidthTop = innerBorder,
            CornerRadiusBottomLeft = radius - 2,
            CornerRadiusBottomRight = radius - 2,
            CornerRadiusTopLeft = radius - 2,
            CornerRadiusTopRight = radius - 2,
        };

        var outerPanel = new PanelContainer();
        outerPanel.AddThemeStyleboxOverride("panel", outer);

        var innerPanel = new PanelContainer();
        innerPanel.AddThemeStyleboxOverride("panel", inner);
        outerPanel.AddChild(innerPanel);

        var marginContainer = new MarginContainer();
        marginContainer.AddThemeConstantOverride("margin_left", margin);
        marginContainer.AddThemeConstantOverride("margin_right", margin);
        marginContainer.AddThemeConstantOverride("margin_top", margin);
        marginContainer.AddThemeConstantOverride("margin_bottom", margin);
        innerPanel.AddChild(marginContainer);
        marginContainer.AddChild(content);

        // 四角角标（小方点，暗金）
        var cornerColor = Colors.BattlefieldBright;
        foreach (var (anchorH, anchorV) in new[] {
            (Control.LayoutPreset.TopLeft, Control.LayoutPreset.TopLeft),
            (Control.LayoutPreset.TopRight, Control.LayoutPreset.TopRight),
            (Control.LayoutPreset.BottomLeft, Control.LayoutPreset.BottomLeft),
            (Control.LayoutPreset.BottomRight, Control.LayoutPreset.BottomRight) })
        {
            var dot = new ColorRect { Color = cornerColor, CustomMinimumSize = new Vector2(5, 5) };
            dot.SetAnchorsAndOffsetsPreset(anchorH);
            outerPanel.AddChild(dot);
        }

        return outerPanel;
    }
}
