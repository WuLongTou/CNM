namespace ZombieWars.Core;

using Godot;

/// <summary>
/// 颜色处理工具（darken / lighten）。所有颜色通道按 amount 线性加减，clamp 到 [0,1]，保留 alpha。
/// 用于 UI 主题的悬停/按下变体与 buff 图标色板变体。
/// </summary>
public static class ColorUtils
{
    /// <summary>将颜色各通道按 amount 减暗，clamp 到 [0,1]，保留 alpha。</summary>
    public static Color Darken(Color c, float amount)
    {
        return new Color(
            Mathf.Max(0.0f, c.R - amount),
            Mathf.Max(0.0f, c.G - amount),
            Mathf.Max(0.0f, c.B - amount),
            c.A);
    }

    /// <summary>将颜色各通道按 amount 增亮，clamp 到 [1]，保留 alpha。</summary>
    public static Color Lighten(Color c, float amount)
    {
        return new Color(
            Mathf.Min(1.0f, c.R + amount),
            Mathf.Min(1.0f, c.G + amount),
            Mathf.Min(1.0f, c.B + amount),
            c.A);
    }
}
