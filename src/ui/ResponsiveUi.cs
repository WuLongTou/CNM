namespace ZombieWars.UI;

using Godot;

/// <summary>
/// Small responsive layout helper for code-built UI. Values scale from the
/// project's 1280x720 reference while staying readable on compact windows.
/// </summary>
public static class ResponsiveUi
{
    private static readonly Vector2 ReferenceViewport = new(1280.0f, 720.0f);

    public static Vector2 ViewportSize(Control control)
    {
        var viewport = control.GetViewport();
        if (viewport == null)
            return ReferenceViewport;

        var size = viewport.GetVisibleRect().Size;
        return size.X > 0.0f && size.Y > 0.0f ? size : ReferenceViewport;
    }

    public static float Scale(Vector2 viewportSize)
    {
        if (viewportSize.X <= 0.0f || viewportSize.Y <= 0.0f)
            return 1.0f;

        float widthScale = viewportSize.X / ReferenceViewport.X;
        float heightScale = viewportSize.Y / ReferenceViewport.Y;
        return Mathf.Clamp(Mathf.Min(widthScale, heightScale), 0.74f, 1.22f);
    }

    public static bool Compact(Vector2 viewportSize) => viewportSize.X < 1100.0f || viewportSize.Y < 650.0f;

    public static int Px(float baseValue, Vector2 viewportSize, int min = 1)
    {
        return Mathf.Max(min, Mathf.RoundToInt(baseValue * Scale(viewportSize)));
    }

    public static int Font(int baseSize, Vector2 viewportSize, int min = 9)
    {
        return Mathf.Max(min, Mathf.RoundToInt(baseSize * Scale(viewportSize)));
    }

    public static Vector2 Size(float width, float height, Vector2 viewportSize, int min = 1)
    {
        return new Vector2(Px(width, viewportSize, min), Px(height, viewportSize, min));
    }

    public static Vector2 FitPanel(Vector2 baseSize, Vector2 viewportSize, float maxWidthRatio = 0.92f, float maxHeightRatio = 0.88f)
    {
        float scale = Scale(viewportSize);
        float width = Mathf.Min(baseSize.X * scale, viewportSize.X * maxWidthRatio);
        float height = Mathf.Min(baseSize.Y * scale, viewportSize.Y * maxHeightRatio);
        return new Vector2(Mathf.Max(1.0f, width), Mathf.Max(1.0f, height));
    }

    public static float FitWidth(float baseWidth, Vector2 viewportSize, float maxWidthRatio = 0.9f)
    {
        return Mathf.Min(baseWidth * Scale(viewportSize), viewportSize.X * maxWidthRatio);
    }
}
