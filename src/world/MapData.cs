namespace ZombieWars.World;

using System.Collections.Generic;
using Godot;
using ZombieWars.Core;

/// <summary>地图主题 —— 决定视觉色调与障碍风格。</summary>
public enum MapTheme
{
    Graveyard,   // 墓地：散布墓碑墙 + 栅栏
    Ruins,       // 废墟：十字石墙通道
    Dungeon,     // 地牢：围合房间与分隔
    Swamp,       // 沼泽：低矮水池 + 枯树
    Cathedral,   // 大教堂：对称石柱阵
    Wasteland    // 荒原：零散掩体 + 陷阱地带
}

/// <summary>障碍物种类 —— 决定碰撞与交互行为。</summary>
public enum ObstacleKind
{
    Solid,          // 固体墙/掩体：完全阻挡，玩家与地面僵尸绕行
    Destructible,   // 可破坏：有血量，受攻击/僵尸推挤会破碎
    Low,            // 低矮装饰：可穿过，仅减速
    Hazard          // 陷阱/危险区域：踩入掉血 + 减速
}

/// <summary>单个障碍物的布局定义（值类型，高效）。</summary>
public struct ObstacleDef
{
    public Vector2 Position;
    public Vector2 Size;
    public float Rotation;
    public ObstacleKind Kind;
    public int Variant;

    public ObstacleDef(Vector2 position, Vector2 size, ObstacleKind kind,
        float rotation = 0f, int variant = 0)
    {
        Position = position;
        Size = size;
        Kind = kind;
        Rotation = rotation;
        Variant = variant;
    }
}

/// <summary>一张地图的完整数据：尺寸、主题、色调、障碍布局。</summary>
public class MapData
{
    public string Id = "";
    public string Name = "";
    public MapTheme Theme;
    public Vector2 WorldSize = GameConfig.WorldSize;
    public Color GroundTint = new(1.0f, 0.95f, 0.90f);
    public Color AtmosphereTint = new(1.0f, 0.82f, 0.72f);
    public float AtmosphereAlpha = 0.22f;
    public Color FogTint = new(0.12f, 0.10f, 0.08f);
    public string? AtmosphereTexturePath;
    public string? GroundTexturePath;
    public List<ObstacleDef> Obstacles = new();
    public int Seed;

    public bool InSpawnSafeZone(Vector2 p)
    {
        var nx = (p.X / WorldSize.X - 0.5f) / GameConfig.MapSpawnSafeRadiusX;
        var ny = (p.Y / WorldSize.Y - 0.5f) / GameConfig.MapSpawnSafeRadiusY;
        return nx * nx + ny * ny < 1f;
    }
}

/// <summary>预设地图库 —— 程序化生成 6 张布局各异的地图。</summary>
public static class MapLibrary
{
    public static readonly Vector2 StandardSize = GameConfig.WorldSize;

    public static List<MapData> BuildAll()
    {
        return new List<MapData>
        {
            _Graveyard(),
            _Ruins(),
            _Dungeon(),
            _Swamp(),
            _Cathedral(),
            _Wasteland()
        };
    }

    // ── 墓地：散布墓碑短墙 + 木栅栏，边缘密集 ──────────────
    private static MapData _Graveyard()
    {
        var ws = StandardSize;
        var m = new MapData
        {
            Id = "graveyard",
            Name = "荒废墓地",
            Theme = MapTheme.Graveyard,
            WorldSize = ws,
            Seed = 11111,
            GroundTint = new Color(0.85f, 0.88f, 0.80f),
            AtmosphereTint = new Color(0.55f, 0.62f, 0.70f),
            AtmosphereAlpha = 0.24f,
            FogTint = new Color(0.14f, 0.16f, 0.18f),
            AtmosphereTexturePath = "res://assets/sprites/backgrounds/atmosphere_blood_moon2.jpg",
            GroundTexturePath = "res://assets/sprites/backgrounds/ground_graveyard.jpg"
        };
        var rng = new RandomNumberGenerator { Seed = (ulong)m.Seed };

        // 边缘墓碑短墙（固体）
        for (int i = 0; i < 7; i++)
        {
            var edge = rng.RandfRange(0.06f, 0.94f);
            bool horiz = i % 2 == 0;
            var pos = horiz
                ? new Vector2(ws.X * edge, ws.Y * (i < 4 ? 0.18f : 0.82f))
                : new Vector2(ws.X * (i < 4 ? 0.16f : 0.84f), ws.Y * edge);
            if (m.InSpawnSafeZone(pos)) continue;
            m.Obstacles.Add(new ObstacleDef(pos, new Vector2(90, 26), ObstacleKind.Solid,
                rng.RandfRange(-0.2f, 0.2f), variant: 1));
        }
        // 散布栅栏（可破坏）
        for (int i = 0; i < 5; i++)
        {
            var pos = new Vector2(
                ws.X * rng.RandfRange(0.12f, 0.88f),
                ws.Y * rng.RandfRange(0.14f, 0.86f));
            if (m.InSpawnSafeZone(pos)) continue;
            m.Obstacles.Add(new ObstacleDef(pos, new Vector2(70, 16), ObstacleKind.Destructible,
                rng.RandfRange(0f, Mathf.Tau), variant: 2));
        }
        // 中心墓碑群（低矮装饰）
        _ScatterLow(m, rng, 8);
        return m;
    }

    // ── 废墟：十字形石墙通道，形成战斗走廊 ─────────────────
    private static MapData _Ruins()
    {
        var ws = StandardSize;
        var m = new MapData
        {
            Id = "ruins",
            Name = "破败废墟",
            Theme = MapTheme.Ruins,
            WorldSize = ws,
            Seed = 22222,
            GroundTint = new Color(0.92f, 0.88f, 0.82f),
            AtmosphereTint = new Color(0.75f, 0.55f, 0.40f),
            AtmosphereAlpha = 0.20f,
            FogTint = new Color(0.16f, 0.12f, 0.09f),
            AtmosphereTexturePath = "res://assets/sprites/backgrounds/atmosphere_storm.jpg",
            GroundTexturePath = "res://assets/sprites/backgrounds/ground_ruins.jpg"
        };

        // 水平长墙（两段，中间留缺口）
        _AddWallWithGap(m, new Vector2(ws.X * 0.5f, ws.Y * 0.28f), true, 360, 90);
        _AddWallWithGap(m, new Vector2(ws.X * 0.5f, ws.Y * 0.72f), true, 360, 90);
        // 垂直长墙
        _AddWallWithGap(m, new Vector2(ws.X * 0.24f, ws.Y * 0.5f), false, 300, 80);
        _AddWallWithGap(m, new Vector2(ws.X * 0.76f, ws.Y * 0.5f), false, 300, 80);
        // 角落石柱（固体）
        var rng = new RandomNumberGenerator { Seed = (ulong)m.Seed };
        foreach (var f in new[] { 0.18f, 0.82f })
            foreach (var g in new[] { 0.18f, 0.82f })
            {
                var pos = new Vector2(ws.X * f, ws.Y * g);
                m.Obstacles.Add(new ObstacleDef(pos, new Vector2(44, 44), ObstacleKind.Solid,
                    variant: 3));
            }
        // 散布可破坏碎石
        for (int i = 0; i < 4; i++)
        {
            var pos = new Vector2(
                ws.X * rng.RandfRange(0.2f, 0.8f),
                ws.Y * rng.RandfRange(0.2f, 0.8f));
            if (m.InSpawnSafeZone(pos)) continue;
            m.Obstacles.Add(new ObstacleDef(pos, new Vector2(50, 50), ObstacleKind.Destructible,
                variant: 1));
        }
        return m;
    }

    // ── 地牢：围合房间与分隔通道 ─────────────────────────
    private static MapData _Dungeon()
    {
        var ws = StandardSize;
        var m = new MapData
        {
            Id = "dungeon",
            Name = "地下地牢",
            Theme = MapTheme.Dungeon,
            WorldSize = ws,
            Seed = 33333,
            GroundTint = new Color(0.70f, 0.72f, 0.78f),
            AtmosphereTint = new Color(0.40f, 0.42f, 0.60f),
            AtmosphereAlpha = 0.28f,
            FogTint = new Color(0.10f, 0.10f, 0.14f),
            AtmosphereTexturePath = "res://assets/sprites/backgrounds/atmosphere_storm2.jpg",
            GroundTexturePath = "res://assets/sprites/backgrounds/ground_dungeon.jpg"
        };

        // 四个象限各围一个半开放房间（三面墙，朝中心开口）
        var centers = new[]
        {
            new Vector2(ws.X * 0.25f, ws.Y * 0.25f),
            new Vector2(ws.X * 0.75f, ws.Y * 0.25f),
            new Vector2(ws.X * 0.25f, ws.Y * 0.75f),
            new Vector2(ws.X * 0.75f, ws.Y * 0.75f)
        };
        foreach (var c in centers)
        {
            float roomW = ws.X * 0.26f;
            // 上墙、左墙、右墙（朝中心一侧开口）
            m.Obstacles.Add(new ObstacleDef(
                new Vector2(c.X, c.Y - roomW * 0.5f), new Vector2(roomW, 24), ObstacleKind.Solid, variant: 4));
            m.Obstacles.Add(new ObstacleDef(
                new Vector2(c.X - roomW * 0.5f, c.Y), new Vector2(24, roomW), ObstacleKind.Solid, variant: 4));
            // 朝外侧墙
            bool leftRoom = c.X < ws.X * 0.5f;
            m.Obstacles.Add(new ObstacleDef(
                new Vector2(c.X + (leftRoom ? -roomW * 0.5f : roomW * 0.5f), c.Y),
                new Vector2(24, roomW), ObstacleKind.Solid, variant: 4));
        }
        // 通道处放可破坏门
        var rng = new RandomNumberGenerator { Seed = (ulong)m.Seed };
        for (int i = 0; i < 3; i++)
        {
            var pos = new Vector2(
                ws.X * rng.RandfRange(0.3f, 0.7f),
                ws.Y * rng.RandfRange(0.3f, 0.7f));
            if (m.InSpawnSafeZone(pos)) continue;
            m.Obstacles.Add(new ObstacleDef(pos, new Vector2(60, 22), ObstacleKind.Destructible,
                rng.Randf() < 0.5f ? 0f : Mathf.Pi / 2f, variant: 3));
        }
        return m;
    }

    // ── 沼泽：低矮水池（减速）+ 枯树掩体 ──────────────────
    private static MapData _Swamp()
    {
        var ws = StandardSize;
        var m = new MapData
        {
            Id = "swamp",
            Name = "腐朽沼泽",
            Theme = MapTheme.Swamp,
            WorldSize = ws,
            Seed = 44444,
            GroundTint = new Color(0.72f, 0.80f, 0.68f),
            AtmosphereTint = new Color(0.50f, 0.62f, 0.45f),
            AtmosphereAlpha = 0.30f,
            FogTint = new Color(0.14f, 0.18f, 0.14f),
            AtmosphereTexturePath = "res://assets/sprites/backgrounds/atmosphere_swamp.jpg",
            GroundTexturePath = "res://assets/sprites/backgrounds/atmosphere_swamp.jpg"
        };
        var rng = new RandomNumberGenerator { Seed = (ulong)m.Seed };

        // 大面积水池（低矮减速）
        for (int i = 0; i < 6; i++)
        {
            var pos = new Vector2(
                ws.X * rng.RandfRange(0.12f, 0.88f),
                ws.Y * rng.RandfRange(0.12f, 0.88f));
            if (m.InSpawnSafeZone(pos)) continue;
            var sz = new Vector2(rng.RandfRange(110, 180), rng.RandfRange(80, 130));
            m.Obstacles.Add(new ObstacleDef(pos, sz, ObstacleKind.Low,
                rng.RandfRange(0f, Mathf.Tau), variant: 1));
        }
        // 枯树掩体（固体）
        for (int i = 0; i < 6; i++)
        {
            var pos = new Vector2(
                ws.X * rng.RandfRange(0.10f, 0.90f),
                ws.Y * rng.RandfRange(0.10f, 0.90f));
            if (m.InSpawnSafeZone(pos)) continue;
            m.Obstacles.Add(new ObstacleDef(pos, new Vector2(36, 36), ObstacleKind.Solid, variant: 2));
        }
        // 毒沼陷阱
        for (int i = 0; i < 4; i++)
        {
            var pos = new Vector2(
                ws.X * rng.RandfRange(0.15f, 0.85f),
                ws.Y * rng.RandfRange(0.15f, 0.85f));
            if (m.InSpawnSafeZone(pos)) continue;
            m.Obstacles.Add(new ObstacleDef(pos, new Vector2(90, 90), ObstacleKind.Hazard, variant: 1));
        }
        return m;
    }

    // ── 大教堂：对称石柱阵（网格）──────────────────────────
    private static MapData _Cathedral()
    {
        var ws = StandardSize;
        var m = new MapData
        {
            Id = "cathedral",
            Name = "血色圣堂",
            Theme = MapTheme.Cathedral,
            WorldSize = ws,
            Seed = 55555,
            GroundTint = new Color(0.90f, 0.86f, 0.78f),
            AtmosphereTint = new Color(0.80f, 0.45f, 0.30f),
            AtmosphereAlpha = 0.26f,
            FogTint = new Color(0.18f, 0.12f, 0.10f),
            AtmosphereTexturePath = "res://assets/sprites/backgrounds/atmosphere_blood_moon.jpg",
            GroundTexturePath = "res://assets/sprites/backgrounds/ground_stone.jpg"
        };

        // 对称石柱网格（5x3），跳过中心安全区
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 5; col++)
            {
                var pos = new Vector2(
                    ws.X * (0.16f + col * 0.17f),
                    ws.Y * (0.22f + row * 0.28f));
                if (m.InSpawnSafeZone(pos)) continue;
                m.Obstacles.Add(new ObstacleDef(pos, new Vector2(48, 48), ObstacleKind.Solid, variant: 4));
            }
        }
        // 中央祭坛（可破坏大块）
        m.Obstacles.Add(new ObstacleDef(
            new Vector2(ws.X * 0.5f, ws.Y * 0.16f), new Vector2(120, 40),
            ObstacleKind.Destructible, variant: 4));
        m.Obstacles.Add(new ObstacleDef(
            new Vector2(ws.X * 0.5f, ws.Y * 0.84f), new Vector2(120, 40),
            ObstacleKind.Destructible, variant: 4));
        return m;
    }

    // ── 荒原：零散掩体 + 陷阱地带 ─────────────────────────
    private static MapData _Wasteland()
    {
        var ws = StandardSize;
        var m = new MapData
        {
            Id = "wasteland",
            Name = "焦土荒原",
            Theme = MapTheme.Wasteland,
            WorldSize = ws,
            Seed = 66666,
            GroundTint = new Color(0.88f, 0.80f, 0.68f),
            AtmosphereTint = new Color(0.85f, 0.50f, 0.25f),
            AtmosphereAlpha = 0.24f,
            FogTint = new Color(0.18f, 0.14f, 0.10f),
            AtmosphereTexturePath = "res://assets/sprites/backgrounds/atmosphere_storm.jpg",
            GroundTexturePath = "res://assets/sprites/backgrounds/ground_cemetery.jpg"
        };
        var rng = new RandomNumberGenerator { Seed = (ulong)m.Seed };

        // 随机散布的金属掩体（固体）
        for (int i = 0; i < 8; i++)
        {
            var pos = new Vector2(
                ws.X * rng.RandfRange(0.10f, 0.90f),
                ws.Y * rng.RandfRange(0.10f, 0.90f));
            if (m.InSpawnSafeZone(pos)) continue;
            m.Obstacles.Add(new ObstacleDef(pos, new Vector2(60, 28), ObstacleKind.Solid,
                rng.RandfRange(0f, Mathf.Tau), variant: 5));
        }
        // 燃烧地带（陷阱）
        for (int i = 0; i < 5; i++)
        {
            var pos = new Vector2(
                ws.X * rng.RandfRange(0.12f, 0.88f),
                ws.Y * rng.RandfRange(0.12f, 0.88f));
            if (m.InSpawnSafeZone(pos)) continue;
            m.Obstacles.Add(new ObstacleDef(pos, new Vector2(80, 80), ObstacleKind.Hazard,
                variant: 2));
        }
        // 低矮碎石
        _ScatterLow(m, rng, 6);
        return m;
    }

    // ── 辅助：带缺口的墙（拆成两段固体）──────────────────
    private static void _AddWallWithGap(MapData m, Vector2 center, bool horizontal,
        float totalLen, float gapWidth)
    {
        float segLen = (totalLen - gapWidth) * 0.5f;
        if (horizontal)
        {
            m.Obstacles.Add(new ObstacleDef(
                center + new Vector2(-(segLen * 0.5f + gapWidth * 0.5f), 0),
                new Vector2(segLen, 26), ObstacleKind.Solid, variant: 4));
            m.Obstacles.Add(new ObstacleDef(
                center + new Vector2(segLen * 0.5f + gapWidth * 0.5f, 0),
                new Vector2(segLen, 26), ObstacleKind.Solid, variant: 4));
        }
        else
        {
            m.Obstacles.Add(new ObstacleDef(
                center + new Vector2(0, -(segLen * 0.5f + gapWidth * 0.5f)),
                new Vector2(26, segLen), ObstacleKind.Solid, variant: 4));
            m.Obstacles.Add(new ObstacleDef(
                center + new Vector2(0, segLen * 0.5f + gapWidth * 0.5f),
                new Vector2(26, segLen), ObstacleKind.Solid, variant: 4));
        }
    }

    // ── 辅助：散布低矮装饰 ──────────────────────────────
    private static void _ScatterLow(MapData m, RandomNumberGenerator rng, int count)
    {
        var ws = m.WorldSize;
        for (int i = 0; i < count; i++)
        {
            var pos = new Vector2(
                ws.X * rng.RandfRange(0.14f, 0.86f),
                ws.Y * rng.RandfRange(0.14f, 0.86f));
            if (m.InSpawnSafeZone(pos)) continue;
            var sz = new Vector2(rng.RandfRange(50, 80), rng.RandfRange(40, 60));
            m.Obstacles.Add(new ObstacleDef(pos, sz, ObstacleKind.Low,
                rng.RandfRange(0f, Mathf.Tau), variant: 3));
        }
    }
}
