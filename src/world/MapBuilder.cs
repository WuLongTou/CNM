namespace ZombieWars.World;

using System.Collections.Generic;
using Godot;
using ZombieWars.Core;

/// <summary>
/// 地图构建器 —— 根据当前 MapData 构建可交互场景层：
/// 1. NavigationRegion2D：烘焙导航网格，外边界=世界矩形，每个固体/可破坏障碍挖洞
/// 2. 障碍物节点层：SolidObstacle / DestructibleObstacle / LowObstacle / HazardZone
/// 加入 "map_builder" group；可破坏障碍破碎时调用 RequestNavRebuild 重烘焙（移除该区域洞）。
/// </summary>
public partial class MapBuilder : Node2D
{
    private NavigationRegion2D _navRegion = null!;
    private Node2D _obstacleLayer = null!;
    private MapData? _current;

    // 固体/可破坏障碍的 (def, node) 引用 —— 用于导航重烘焙时过滤已破碎的
    private readonly List<(ObstacleDef def, Node node)> _solidRefs = new();

    public override void _Ready()
    {
        AddToGroup("map_builder");
        Name = "MapBuilder";
        ZIndex = 0;

        _navRegion = new NavigationRegion2D { Name = "NavigationRegion" };
        AddChild(_navRegion);

        _obstacleLayer = new Node2D { Name = "Obstacles" };
        AddChild(_obstacleLayer);
    }

    /// <summary>构建一张新地图：清空旧障碍 → 实例化新障碍 → 烘焙导航。</summary>
    public void Build(MapData map)
    {
        _current = map;
        _solidRefs.Clear();

        // 清空旧障碍节点
        foreach (var child in _obstacleLayer.GetChildren())
            if (child is Node n) n.QueueFree();

        // 创建新障碍
        foreach (var def in map.Obstacles)
        {
            var node = ObstacleFactory.Create(def);
            _obstacleLayer.AddChild(node);
            if (def.Kind is ObstacleKind.Solid or ObstacleKind.Destructible)
                _solidRefs.Add((def, node));
        }

        // 等一帧让旧节点 free 后再烘焙，避免导航引用已失效的碰撞
        CallDeferred(nameof(_BakeNavigation));
    }

    /// <summary>可破坏障碍破碎时由 CallGroup 触发，重新烘焙导航网格。</summary>
    public void RequestNavRebuild()
    {
        CallDeferred(nameof(_BakeNavigation));
    }

    private void _BakeNavigation()
    {
        if (_current == null) return;
        var ws = _current.WorldSize;
        float pad = GameConfig.BoundsPadding;

        var navPoly = new NavigationPolygon();

        // 外边界（略收缩，留出边界 padding）
        navPoly.AddOutline(new Vector2[]
        {
            new(pad, pad),
            new(ws.X - pad, pad),
            new(ws.X - pad, ws.Y - pad),
            new(pad, ws.Y - pad)
        });

        // 每个仍存活的固体/可破坏障碍挖一个洞
        foreach (var (def, node) in _solidRefs)
        {
            if (!IsInstanceValid(node)) continue;
            navPoly.AddOutline(_RotatedRectOutline(def));
        }

        _navRegion.NavigationPolygon = navPoly;
        _navRegion.BakeNavigationPolygon(false);
    }

    /// <summary>计算旋转矩形的世界坐标轮廓（4 点，逆时针）。
    /// NavigationPolygon 中外边界为顺时针（实体），障碍轮廓必须逆时针才能被识别为“洞”挖空；
    /// 两者 winding 相同会导致障碍不被挖洞，导航路径直接穿越障碍。</summary>
    private static Vector2[] _RotatedRectOutline(ObstacleDef def)
    {
        float cos = Mathf.Cos(def.Rotation);
        float sin = Mathf.Sin(def.Rotation);
        float hx = def.Size.X * 0.5f;
        float hy = def.Size.Y * 0.5f;
        Vector2[] local = {
            new(-hx, -hy), new(-hx, hy), new(hx, hy), new(hx, -hy)
        };
        var result = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            result[i] = def.Position + new Vector2(
                local[i].X * cos - local[i].Y * sin,
                local[i].X * sin + local[i].Y * cos);
        }
        return result;
    }
}
