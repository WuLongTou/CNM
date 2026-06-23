namespace ZombieWars.UI;

using Godot;
using ZombieWars.Core;

public static class HandDrawnTheme
{
    public static class Colors
    {
        // ══ 饥荒式暗黑童话棕褐调（原炼金术手稿→暖棕复古）══
        public static readonly Color BattlefieldBg = new(0.055f, 0.038f, 0.028f);
        public static readonly Color BattlefieldMid = new(0.105f, 0.072f, 0.048f);
        public static readonly Color BattlefieldAccent = new(0.68f, 0.52f, 0.22f);
        public static readonly Color BattlefieldBright = new(0.82f, 0.70f, 0.35f);

        public static readonly Color NoteBg = new(0.78f, 0.72f, 0.60f);           // 旧羊皮纸
        public static readonly Color NoteBgHover = new(0.86f, 0.81f, 0.72f);
        public static readonly Color NoteMid = new(0.60f, 0.52f, 0.40f);
        public static readonly Color NoteBorder = new(0.35f, 0.28f, 0.20f);
        public static readonly Color NoteText = new(0.22f, 0.16f, 0.10f);         // 深棕墨
        public static readonly Color NoteTextDim = new(0.42f, 0.34f, 0.24f);

        public static readonly Color BloodRed = new(0.48f, 0.18f, 0.12f);         // 猪肝棕
        public static readonly Color BloodBright = new(0.70f, 0.55f, 0.25f);      // 暗金
        public static readonly Color InkBlack = new(0.09f, 0.07f, 0.05f);
        public static readonly Color PencilGray = new(0.25f, 0.22f, 0.18f);       // 炭灰
        public static readonly Color NetherPurple = new(0.32f, 0.22f, 0.14f);     // 土棕（原炼紫）
        public static readonly Color CandleGlow = new(0.88f, 0.72f, 0.38f);       // 烛光金

        public static readonly Color RarityCommon = new(0.55f, 0.48f, 0.38f);
        public static readonly Color RarityRare = new(0.25f, 0.42f, 0.35f);      // 冷绿
        public static readonly Color RarityEpic = new(0.70f, 0.55f, 0.25f);      // 暗金
        public static readonly Color RarityLegendary = new(0.95f, 0.75f, 0.22f);
        public static readonly Color RarityCommonBg = new(0.18f, 0.14f, 0.10f, 0.95f);
        public static readonly Color RarityRareBg = new(0.10f, 0.16f, 0.14f, 0.95f);
        public static readonly Color RarityEpicBg = new(0.20f, 0.16f, 0.08f, 0.95f);

        public static readonly Color UpgradeBg = new(0.14f, 0.10f, 0.07f, 0.95f);
        public static readonly Color UpgradeBorder = new(0.50f, 0.38f, 0.20f);
        public static readonly Color UpgradeTitle = new(0.82f, 0.72f, 0.48f);

        public static readonly Color HpHigh = new(0.22f, 0.42f, 0.30f);          // 暖绿
        public static readonly Color HpMed = new(0.72f, 0.50f, 0.20f);           // 琥珀
        public static readonly Color HpLow = new(0.48f, 0.18f, 0.12f);           // 猪肝

        // ═══ HUD 通用底色与描边（暗棕暗黑童话风格）══
        public static readonly Color HudBottomBg = new(0.06f, 0.04f, 0.03f, 0.92f);
        public static readonly Color HudBottomBorder = new(0.45f, 0.15f, 0.10f, 0.6f);
        public static readonly Color HudTopBorder = new(0.239f, 0.102f, 0.102f, 0.4f);
        public static readonly Color HudChipBg = new(0.10f, 0.08f, 0.06f, 0.70f);
        public static readonly Color HudGroupBg = new(0.05f, 0.03f, 0.02f, 0.50f);
        public static readonly Color HudGroupBorder = new(0.20f, 0.12f, 0.08f, 0.35f);

        public static readonly Color NotePaperShadow = new(0f, 0f, 0f, 0.50f);
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
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
        };
    }

    public static StyleBoxFlat MakeHpBarFill(Color color)
    {
        return new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
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
}
