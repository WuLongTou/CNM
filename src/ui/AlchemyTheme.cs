namespace ZombieWars.UI;

using Godot;
using System;

/// <summary>
/// 炼金术手稿视觉主题——替代 HandDrawnTheme。
///
/// 色彩体系灵感来自中世纪炼金术手稿：
/// - 羊皮纸底色 + 棕墨文字 + 金箔高光
/// - 铜锈绿/铁锈红/炼紫作为辅助色
/// - 蜡封稀有度徽章（圆→星→五芒星→六芒星）
///
/// 所有 StyleBox 通过工厂方法创建，保持全局一致性。
/// </summary>
public static class AlchemyTheme
{
    // ═══════════════════════════════════════════════
    // 色彩体系
    // ═══════════════════════════════════════════════

    public static class Colors
    {
        // 基础色
        public static readonly Color Parchment = new(0.831f, 0.769f, 0.659f);       // #D4C4A8
        public static readonly Color ParchmentDark = new(0.659f, 0.596f, 0.471f);   // #A89878
        public static readonly Color ParchmentLight = new(0.906f, 0.863f, 0.780f);  // #E7DCC7
        public static readonly Color InkBlack = new(0.102f, 0.078f, 0.063f);        // #1A1410
        public static readonly Color InkBrown = new(0.239f, 0.169f, 0.122f);        // #3D2B1F

        // 强调色
        public static readonly Color GoldAccent = new(0.788f, 0.659f, 0.298f);      // #C9A84C
        public static readonly Color GoldBright = new(0.925f, 0.820f, 0.439f);      // #ECD170
        public static readonly Color RustRed = new(0.545f, 0.227f, 0.184f);         // #8B3A2F
        public static readonly Color RustBright = new(0.753f, 0.325f, 0.255f);      // #C05341

        // 辅助色
        public static readonly Color Verdigris = new(0.290f, 0.486f, 0.435f);       // #4A7C6F
        public static readonly Color AetherBlue = new(0.165f, 0.290f, 0.420f);      // #2A4A6B
        public static readonly Color NetherPurple = new(0.357f, 0.227f, 0.420f);    // #5B3A6B
        public static readonly Color CandleGlow = new(0.910f, 0.757f, 0.439f);      // #E8C170

        // 稀有度色
        public static readonly Color RarityCommon = ParchmentDark;
        public static readonly Color RarityRare = Verdigris;
        public static readonly Color RarityEpic = GoldAccent;
        public static readonly Color RarityLegendary = NetherPurple;

        // 稀有度背景色
        public static readonly Color RarityCommonBg = new(0.20f, 0.16f, 0.12f, 0.95f);
        public static readonly Color RarityRareBg = new(0.12f, 0.18f, 0.16f, 0.95f);
        public static readonly Color RarityEpicBg = new(0.22f, 0.18f, 0.10f, 0.95f);
        public static readonly Color RarityLegendaryBg = new(0.18f, 0.12f, 0.22f, 0.95f);

        // 战场色
        public static readonly Color BattlefieldBg = new(0.063f, 0.047f, 0.039f);   // 深棕黑
        public static readonly Color BattlefieldMid = new(0.122f, 0.078f, 0.055f);  // 暗棕

        // HP色
        public static readonly Color HpHigh = new(0.290f, 0.486f, 0.435f);          // 铜锈绿
        public static readonly Color HpMed = new(0.788f, 0.659f, 0.298f);           // 金色
        public static readonly Color HpLow = RustRed;

        // 文本色
        public static readonly Color TextPrimary = InkBrown;
        public static readonly Color TextDim = ParchmentDark;
        public static readonly Color TextBright = ParchmentLight;

        // 阴影
        public static readonly Color ShadowColor = new(0f, 0f, 0f, 0.50f);
        public static readonly Color CandleShadow = new(CandleGlow, 0.15f);

        // UI 边框
        public static readonly Color BorderDefault = ParchmentDark;
        public static readonly Color BorderHighlight = GoldAccent;

        // 升级面板
        public static readonly Color UpgradeBg = new(0.15f, 0.11f, 0.08f, 0.95f);
        public static readonly Color UpgradeBorder = new(0.55f, 0.42f, 0.25f);
        public static readonly Color UpgradeTitle = new(0.88f, 0.78f, 0.55f);
    }

    // ═══════════════════════════════════════════════
    // StyleBox 工厂方法
    // ═══════════════════════════════════════════════

    private static readonly Random Rng = new();

    /// <summary>
    /// 创建羊皮纸面板（主面板背景）。
    /// 模拟中世纪手稿的温暖纸张质感。
    /// </summary>
    public static StyleBoxFlat MakeParchmentPanel(
        float borderWidth = 2.0f,
        float borderRadius = 3.0f,
        float shadowSize = 5.0f,
        bool withGoldBorder = false)
    {
        int bw = Mathf.RoundToInt(borderWidth);
        int cr = Mathf.RoundToInt(borderRadius);
        int ss = Mathf.RoundToInt(shadowSize);
        var borderColor = withGoldBorder ? Colors.GoldAccent : Colors.BorderDefault;

        return new StyleBoxFlat
        {
            BgColor = Colors.Parchment,
            BorderColor = borderColor,
            BorderWidthBottom = bw,
            BorderWidthLeft = bw,
            BorderWidthRight = bw,
            BorderWidthTop = bw,
            ShadowColor = Colors.ShadowColor,
            ShadowSize = ss,
            CornerRadiusBottomLeft = cr,
            CornerRadiusBottomRight = cr,
            CornerRadiusTopLeft = cr,
            CornerRadiusTopRight = cr,
        };
    }

    /// <summary>
    /// 创建武器/装备卡片面板。
    /// 根据稀有度使用不同色调。
    /// </summary>
    public static StyleBoxFlat MakeCardPanel(
        int rarity, // 1=普通, 2=稀有, 3=史诗, 4=传说
        float borderWidth = 2.0f,
        float borderRadius = 4.0f)
    {
        int bw = Mathf.RoundToInt(borderWidth);
        int cr = Mathf.RoundToInt(borderRadius);

        var (bgColor, borderColor) = rarity switch
        {
            >= 4 => (Colors.RarityLegendaryBg, Colors.RarityLegendary),
            3 => (Colors.RarityEpicBg, Colors.RarityEpic),
            2 => (Colors.RarityRareBg, Colors.RarityRare),
            _ => (Colors.RarityCommonBg, Colors.RarityCommon),
        };

        return new StyleBoxFlat
        {
            BgColor = bgColor,
            BorderColor = borderColor,
            BorderWidthBottom = bw,
            BorderWidthLeft = bw,
            BorderWidthRight = bw,
            BorderWidthTop = bw,
            ShadowColor = Colors.ShadowColor,
            ShadowSize = 8,
            CornerRadiusBottomLeft = cr,
            CornerRadiusBottomRight = cr,
            CornerRadiusTopLeft = cr,
            CornerRadiusTopRight = cr,
        };
    }

    /// <summary>
    /// 创建深色背景面板（用于 HUD、弹窗背景）。
    /// </summary>
    public static StyleBoxFlat MakeDarkPanel(float alpha = 0.92f)
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(Colors.BattlefieldBg, alpha),
            BorderColor = new Color(Colors.BorderDefault, alpha * 0.6f),
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

    /// <summary>
    /// 创建 HP 条背景。
    /// </summary>
    public static StyleBoxFlat MakeHpBarBg()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.05f, 0.03f),
            BorderColor = Colors.BorderDefault with { A = 0.5f },
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
        };
    }

    /// <summary>
    /// 创建 HP 条填充。
    /// </summary>
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

    /// <summary>
    /// 创建升级面板。
    /// </summary>
    public static StyleBoxFlat MakeUpgradePanel()
    {
        return new StyleBoxFlat
        {
            BgColor = Colors.UpgradeBg,
            BorderColor = Colors.UpgradeBorder,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            ShadowColor = Colors.ShadowColor,
            ShadowSize = 6,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
        };
    }

    /// <summary>
    /// 创建魔法阵环形 HP 条的背景。
    /// </summary>
    public static StyleBoxFlat MakeMagicCircleBg()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(Colors.AetherBlue, 0.3f),
            BorderColor = Colors.GoldAccent with { A = 0.4f },
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusBottomLeft = 50, // 圆角设为很大以模拟圆形
            CornerRadiusBottomRight = 50,
            CornerRadiusTopLeft = 50,
            CornerRadiusTopRight = 50,
        };
    }

    // ═══════════════════════════════════════════════
    // 标签工厂
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 创建标准羊皮纸风格标签。
    /// </summary>
    public static Label MakeLabel(string text, int fontSize = 14, Color? color = null)
    {
        var label = new Label
        {
            Text = text,
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color ?? Colors.TextPrimary);
        label.AddThemeColorOverride("font_outline_color", Colors.InkBlack);
        label.AddThemeConstantOverride("outline_size", 2);
        return label;
    }

    /// <summary>
    /// 创建金色标题标签。
    /// </summary>
    public static Label MakeTitleLabel(string text, int fontSize = 22)
    {
        return MakeLabel(text, fontSize, Colors.GoldAccent);
    }

    /// <summary>
    /// 创建暗淡的辅助标签。
    /// </summary>
    public static Label MakeDimLabel(string text, int fontSize = 11)
    {
        return MakeLabel(text, fontSize, Colors.TextDim);
    }

    // ═══════════════════════════════════════════════
    // 工具
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 根据稀有度获取对应的蜡封符号。
    /// </summary>
    public static string GetWaxSealSymbol(int rarity) => rarity switch
    {
        >= 4 => "✦",  // 六芒星（传说）
        3 => "★",     // 五芒星（史诗）
        2 => "✧",     // 星（稀有）
        _ => "●",     // 圆（普通）
    };

    /// <summary>
    /// 获取稀有度对应的中文名称。
    /// </summary>
    public static string GetRarityName(int rarity) => rarity switch
    {
        >= 4 => "传说",
        3 => "史诗",
        2 => "稀有",
        _ => "普通",
    };

    /// <summary>
    /// 微小随机旋转，模拟手稿的不完美感。
    /// </summary>
    public static float RandomRotation(float minDeg = -0.5f, float maxDeg = 0.5f)
    {
        return (float)(Rng.NextDouble() * (maxDeg - minDeg) + minDeg);
    }
}
