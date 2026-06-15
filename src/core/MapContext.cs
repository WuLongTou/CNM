namespace ZombieWars.Core;

using Godot;

/// <summary>
/// 当前地图上下文 —— 全局可变状态，由 World.MapManager 维护。
/// 跨命名空间（Player/Enemy/Core 等）访问当前世界尺寸时统一读这里，
/// 避免各处直接引用 GameConfig.WorldSize 导致无法支持多尺寸地图。
/// GameConfig.WorldSize 保留作为默认值与上限回退。
/// </summary>
public static class MapContext
{
    public static Vector2 CurrentWorldSize = GameConfig.WorldSize;

    public static float Width => CurrentWorldSize.X;
    public static float Height => CurrentWorldSize.Y;
}
