namespace ZombieWars.Core;

using Godot;

/// <summary>
/// 2D↔3D 坐标映射(2.5D 渲染桥)。
///
/// 约定:2D 世界 (x, y) → 3D 世界 (x, 0, y),1:1 无换算(3D 单位 = 像素)。
/// 3D 地面在 XZ 平面,Camera3D 朝 -Y 正交俯视,顶视画面与现有 2D 俯视一致。
/// </summary>
public static class Coord
{
    /// <summary>2D 世界坐标(像素)→ 3D 世界坐标(单位 = 像素,Y = 0 地面)。</summary>
    public static Vector3 World2DTo3D(Vector2 v) => new(v.X, 0f, v.Y);

    /// <summary>3D 世界坐标 → 2D 世界坐标(丢弃 Y 高度)。</summary>
    public static Vector2 World3DTo2D(Vector3 v) => new(v.X, v.Z);
}
