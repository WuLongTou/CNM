namespace ZombieWars.Core;

using Godot;

/// <summary>
/// Extension methods for Tween operations to reduce code duplication.
/// Pre-computed property paths avoid repeated string operations.
/// </summary>
public static class TweenUtils
{
    // Pre-computed property paths to avoid GC pressure
    private static readonly NodePath ScalePath = new(Node2D.PropertyName.Scale);
    private static readonly NodePath ModulatePath = new(Node2D.PropertyName.Modulate);
    private static readonly NodePath ModulateAPath = new(Node2D.PropertyName.Modulate + ":a");

    /// <summary>
    /// Fades out and frees a node. Only works with Node2D.
    /// </summary>
    public static Tween FadeOutAndFree(this Node2D node, float duration = 0.3f)
    {
        var tw = node.CreateTween();
        tw.TweenProperty(node, ModulateAPath, 0.0f, duration);
        tw.TweenCallback(Callable.From(node.QueueFree));
        return tw;
    }

    /// <summary>
    /// Scales up and fades in a node, then fades out and frees it.
    /// </summary>
    public static Tween AnimatePopup(this Node2D node, float scaleUp = 1.2f, float duration = 0.6f)
    {
        node.Modulate = node.Modulate with { A = 0.0f };
        node.Scale = new Vector2(0.5f, 0.5f);

        var tw = node.CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(node, ModulateAPath, 1.0f, 0.15f)
            .SetTrans(Tween.TransitionType.Back);
        tw.TweenProperty(node, ScalePath, new Vector2(scaleUp, scaleUp), 0.15f)
            .SetTrans(Tween.TransitionType.Back);
        tw.SetParallel(false);
        tw.TweenProperty(node, ScalePath, new Vector2(1.0f, 1.0f), 0.1f);
        tw.TweenInterval(duration - 0.25f);
        tw.Parallel().TweenProperty(node, ModulateAPath, 0.0f, 0.2f);
        tw.TweenCallback(Callable.From(node.QueueFree));
        return tw;
    }

    /// <summary>
    /// Pulses scale up and back.
    /// </summary>
    public static Tween PulseScale(this Node2D node, float scale = 1.15f, float duration = 0.15f)
    {
        var tw = node.CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(node, ScalePath, new Vector2(scale, scale), duration / 2)
            .SetTrans(Tween.TransitionType.Back);
        tw.TweenProperty(node, ScalePath, new Vector2(1.0f, 1.0f), duration / 2)
            .SetTrans(Tween.TransitionType.Back);
        return tw;
    }

    /// <summary>
    /// Flashes a color and returns to white.
    /// </summary>
    public static Tween FlashColor(this Sprite2D sprite, Color flashColor, float flashDuration = 0.05f)
    {
        var tw = sprite.CreateTween();
        tw.TweenProperty(sprite, ModulatePath, flashColor, flashDuration);
        tw.TweenProperty(sprite, ModulatePath, Colors.White, 0.1f);
        return tw;
    }

    /// <summary>
    /// Expands and fades a ring effect.
    /// </summary>
    public static Node2D CreateExpandingRing(
        this Node parent, Vector2 position, float startRadius, float endRadius,
        Color color, float duration = 0.3f)
    {
        var ringNode = new Node2D { GlobalPosition = position };
        var ring = new Line2D { Width = 3.0f, DefaultColor = color };

        const int segments = 24;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.Tau / segments;
            ring.AddPoint(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * startRadius);
        }

        ringNode.AddChild(ring);
        parent.AddChild(ringNode);

        var tw = ringNode.CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(ringNode, ScalePath,
            new Vector2(endRadius / startRadius, endRadius / startRadius), duration)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(ringNode, ModulateAPath, 0.0f, duration);
        tw.TweenCallback(Callable.From(ringNode.QueueFree));

        return ringNode;
    }
}
