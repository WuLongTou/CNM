namespace ZombieWars.World;

using System.Collections.Generic;
using Godot;
using ZombieWars.Core;
using ZombieWars.Weapons;

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
        foreach (var rawDef in map.Obstacles)
        {
            // 世界放大后障碍物同比例放大，避免大场景里障碍显得过小过稀
            var def = rawDef;
            def.Size *= GameConfig.WorldScale;

            // 统一兜底：过滤任何与出生安全区重叠的障碍。
            // 各地图生成时虽有 InSpawnSafeZone 检查，但只查障碍中心点、且部分地图
            // （地牢/大教堂/废墟的固定墙体）完全跳过检查，导致大墙体边缘把玩家围住。
            // 这里用障碍四角+中心采样 + 安全区中心点是否落在障碍内，做最终拦截。
            if (_OverlapsSpawnSafeZone(def, map.WorldSize))
                continue;

            var node = ObstacleFactory.Create(def);
            _obstacleLayer.AddChild(node);
            if (def.Kind is ObstacleKind.Solid or ObstacleKind.Destructible or ObstacleKind.Explosive)
                _solidRefs.Add((def, node));
        }

        // 放置探索奖励（宝箱 + 祭坛）—— 大场景探索动机
        _SpawnExplorationProps(map.WorldSize);

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

        // 每个仍存活的固体/可破坏障碍挖一个洞（轮廓略大于视觉，与碰撞体对齐）
        foreach (var (def, node) in _solidRefs)
        {
            if (!IsInstanceValid(node)) continue;
            var padded = def;
            padded.Size *= 1.12f;
            navPoly.AddOutline(_RotatedRectOutline(padded));
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

    /// <summary>放置探索奖励：宝箱（触碰开启掉落拾取物）+ 祭坛（触碰获得增益）。</summary>
    private void _SpawnExplorationProps(Vector2 ws)
    {
        // 宝箱：大世界放更多，每张图散布若干
        int chestCount = 3 + (int)GameConfig.WorldScale;
        for (int i = 0; i < chestCount; i++)
        {
            var chest = new Chest
            {
                GlobalPosition = _RandomPosAwayFromCenter(ws),
                LootCount = (int)GD.RandRange(2, 3)
            };
            _obstacleLayer.AddChild(chest);
        }

        // 祭坛：每张图 1~2 个，随机增益类型；15% 概率为武器融合祭坛
        int altarCount = GameConfig.WorldScale >= 2f ? 2 : 1;
        var boons = System.Enum.GetValues(typeof(Altar.BoonKind));
        var weaponDeck = GetTree().Root.GetNodeOrNull<WeaponDeck>("Main/WeaponDeck");
        for (int i = 0; i < altarCount; i++)
        {
            var altar = new Altar
            {
                GlobalPosition = _RandomPosAwayFromCenter(ws),
            };

            // 15% 概率生成为武器融合祭坛
            if (weaponDeck != null && GD.Randf() < 0.15f)
            {
                var recipes = weaponDeck.GetAllFusionRecipes();
                if (recipes.Length > 0)
                {
                    string recipe = recipes[(int)(GD.Randi() % recipes.Length)];
                    var parts = recipe.Split(" + ");
                    if (parts.Length == 2)
                    {
                        altar.Boon = Altar.BoonKind.WeaponFusion;
                        altar.MateWeaponName = parts[1];
                        altar.FusionResultName = recipe;
                        altar.FusionResultDesc = "献祭当前武器与对偶武器融合为更强大的存在";
                    }
                }
            }
            else
            {
                altar.Boon = (Altar.BoonKind)boons.GetValue((int)GD.RandRange(0, boons.Length - 1))!;
            }

            _obstacleLayer.AddChild(altar);
        }

        // 资源节点（饥荒式采集）：树/石/灌木散布
        _SpawnResourceNodes(ws);

        // 工作台（合成）—— 2 处，便于玩家就近合成
        for (int i = 0; i < 2; i++)
        {
            var wb = new Workbench { GlobalPosition = _RandomPosAwayFromCenter(ws) };
            _obstacleLayer.AddChild(wb);
        }
        // 锻造台（锻造）—— 1 处
        var fs = new ForgeStation { GlobalPosition = _RandomPosAwayFromCenter(ws) };
        _obstacleLayer.AddChild(fs);
    }

    /// <summary>散布可采集资源节点：树丛（木材）、矿点（石头）、灌木（纤维/浆果）。</summary>
    private void _SpawnResourceNodes(Vector2 ws)
    {
        // 树丛（木材）—— 4 片小树林
        for (int g = 0; g < 4; g++)
        {
            var center = _RandomPosAwayFromCenter(ws);
            int count = (int)GD.RandRange(3, 5);
            for (int i = 0; i < count; i++)
            {
                var pos = center + new Vector2((float)GD.RandRange(-90, 90), (float)GD.RandRange(-90, 90));
                _AddResource(ResourceType.Wood, pos);
            }
        }
        // 矿点（石头）—— 3 处
        for (int g = 0; g < 3; g++)
        {
            var center = _RandomPosAwayFromCenter(ws);
            int count = (int)GD.RandRange(2, 3);
            for (int i = 0; i < count; i++)
            {
                var pos = center + new Vector2((float)GD.RandRange(-70, 70), (float)GD.RandRange(-70, 70));
                _AddResource(ResourceType.Stone, pos);
            }
        }
        // 灌木/浆果丛 —— 散布
        for (int i = 0; i < 10; i++)
        {
            var pos = _RandomPosAwayFromCenter(ws);
            _AddResource(GD.Randf() < 0.5f ? ResourceType.Fiber : ResourceType.Berry, pos);
        }
    }

    private void _AddResource(ResourceType type, Vector2 pos)
    {
        var node = new ResourceNode();
        node.Setup(type, pos);
        _obstacleLayer.AddChild(node);
    }

    /// <summary>在地图内随机取一个远离出生安全区的位置。</summary>
    private static Vector2 _RandomPosAwayFromCenter(Vector2 ws)
    {
        Vector2 pos;
        int tries = 0;
        do
        {
            pos = new Vector2(
                (float)GD.RandRange(ws.X * 0.08, ws.X * 0.92),
                (float)GD.RandRange(ws.Y * 0.08, ws.Y * 0.65));
            tries++;
        } while (_InSafeEllipse(pos, ws) && tries < 20);
        return pos;
    }

    /// <summary>
    /// 障碍是否与中心出生安全椭圆重叠（Build 时兜底过滤，避免把玩家围住）。
    /// 检测障碍的采样点是否落入安全椭圆，以及安全椭圆中心是否被障碍覆盖
    /// （长墙横穿中心、四角却在椭圆外的情形）。
    /// </summary>
    private static bool _OverlapsSpawnSafeZone(ObstacleDef def, Vector2 ws)
    {
        float cos = Mathf.Cos(def.Rotation), sin = Mathf.Sin(def.Rotation);
        float hx = def.Size.X * 0.5f, hy = def.Size.Y * 0.5f;
        Vector2[] localPts = {
            new(0, 0),
            new(hx, hy), new(hx, -hy), new(-hx, hy), new(-hx, -hy),
            new(hx, 0), new(-hx, 0), new(0, hy), new(0, -hy)
        };
        foreach (var lp in localPts)
        {
            var wp = def.Position + new Vector2(lp.X * cos - lp.Y * sin, lp.X * sin + lp.Y * cos);
            if (_InSafeEllipse(wp, ws)) return true;
        }
        return _PointInRotatedRect(ws * 0.5f, def);
    }

    private static bool _InSafeEllipse(Vector2 p, Vector2 ws)
    {
        var nx = (p.X / ws.X - 0.5f) / GameConfig.MapSpawnSafeRadiusX;
        var ny = (p.Y / ws.Y - 0.5f) / GameConfig.MapSpawnSafeRadiusY;
        return nx * nx + ny * ny < 1f;
    }

    private static bool _PointInRotatedRect(Vector2 p, ObstacleDef def)
    {
        float cos = Mathf.Cos(-def.Rotation), sin = Mathf.Sin(-def.Rotation);
        var d = p - def.Position;
        var local = new Vector2(d.X * cos - d.Y * sin, d.X * sin + d.Y * cos);
        return Mathf.Abs(local.X) <= def.Size.X * 0.5f && Mathf.Abs(local.Y) <= def.Size.Y * 0.5f;
    }
}
