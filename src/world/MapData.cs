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

    // ── 墓地：月下墓碑群，冷蓝灰色调 ───────────────────
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
            GroundTint = new Color(0.88f, 0.90f, 0.92f),
            AtmosphereTint = new Color(0.45f, 0.55f, 0.72f),
            AtmosphereAlpha = 0.28f,
            FogTint = new Color(0.12f, 0.14f, 0.20f),
            AtmosphereTexturePath = "res://assets/sprites/backgrounds/atmosphere_blood_moon2.jpg",
            GroundTexturePath = "res://assets/sprites/backgrounds/ground_graveyard.jpg"
        };
        var rng = new RandomNumberGenerator { Seed = (ulong)m.Seed };

        // 散落墓碑群（固体）—— 各处散布，不形成连续墙
        for (int i = 0; i < 12; i++)
        {
            var pos = new Vector2(
                ws.X * rng.RandfRange(0.12f, 0.88f),
                ws.Y * rng.RandfRange(0.12f, 0.88f));
            if (m.InSpawnSafeZone(pos)) continue;
            m.Obstacles.Add(new ObstacleDef(pos, new Vector2(60, 20), ObstacleKind.Solid,
                rng.RandfRange(-0.3f, 0.3f), variant: 1));
        }
        // 可破坏木栅栏
        for (int i = 0; i < 5; i++)
        {
            var pos = new Vector2(
                ws.X * rng.RandfRange(0.15f, 0.85f),
                ws.Y * rng.RandfRange(0.15f, 0.85f));
            if (m.InSpawnSafeZone(pos)) continue;
            m.Obstacles.Add(new ObstacleDef(pos, new Vector2(70, 16), ObstacleKind.Destructible,
                rng.RandfRange(0f, Mathf.Tau), variant: 2));
        }
        // 低矮墓碑（减速装饰）
        for (int i = 0; i < 10; i++)
        {
            var pos = new Vector2(
                ws.X * rng.RandfRange(0.10f, 0.90f),
                ws.Y * rng.RandfRange(0.10f, 0.90f));
            if (m.InSpawnSafeZone(pos)) continue;
            m.Obstacles.Add(new ObstacleDef(pos, new Vector2(rng.RandfRange(35, 50), rng.RandfRange(25, 35)),
                ObstacleKind.Low, rng.RandfRange(0f, Mathf.Tau), variant: 3));
        }
        return m;
    }

    // ── 废墟：十字形石墙通道 + 散布石柱 ─────────────────
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
            AtmosphereTint = new Color(0.75f, 0.48f, 0.30f),
            AtmosphereAlpha = 0.28f,
            FogTint = new Color(0.18f, 0.14f, 0.10f),
            AtmosphereTexturePath = "res://assets/sprites/backgrounds/atmosphere_storm.jpg",
            GroundTexturePath = "res://assets/sprites/backgrounds/ground_ruins.jpg"
        };

        // 中央十字通道（每段带缺口）
        _AddWallWithGap(m, new Vector2(ws.X * 0.5f, ws.Y * 0.30f), true, 350, 120);
        _AddWallWithGap(m, new Vector2(ws.X * 0.5f, ws.Y * 0.70f), true, 350, 120);
        _AddWallWithGap(m, new Vector2(ws.X * 0.30f, ws.Y * 0.5f), false, 300, 100);
        _AddWallWithGap(m, new Vector2(ws.X * 0.70f, ws.Y * 0.5f), false, 300, 100);
        // 角落石柱群
        var rng = new RandomNumberGenerator { Seed = (ulong)m.Seed };
        foreach (var fx in new[] { 0.18f, 0.82f })
            foreach (var fy in new[] { 0.18f, 0.82f })
            {
                var pos = new Vector2(ws.X * fx, ws.Y * fy);
                if (m.InSpawnSafeZone(pos)) continue;
                m.Obstacles.Add(new ObstacleDef(pos, new Vector2(44, 44), ObstacleKind.Solid,
                    variant: 3));
            }
        // 散布可破坏碎石
        for (int i = 0; i < 5; i++)
        {
            var pos = new Vector2(
                ws.X * rng.RandfRange(0.20f, 0.80f),
                ws.Y * rng.RandfRange(0.20f, 0.80f));
            if (m.InSpawnSafeZone(pos)) continue;
            m.Obstacles.Add(new ObstacleDef(pos, new Vector2(50, 50), ObstacleKind.Destructible,
                variant: 1));
        }
        return m;
    }

    // ── 地牢：中央十字墙 + 散落立柱，暗紫阴森 ────────────
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
            GroundTint = new Color(0.65f, 0.68f, 0.75f),
            AtmosphereTint = new Color(0.35f, 0.30f, 0.55f),
            AtmosphereAlpha = 0.32f,
            FogTint = new Color(0.08f, 0.06f, 0.12f),
            AtmosphereTexturePath = "res://assets/sprites/backgrounds/atmosphere_storm2.jpg",
            GroundTexturePath = "res://assets/sprites/backgrounds/ground_dungeon.jpg"
        };

        var rng = new RandomNumberGenerator { Seed = (ulong)m.Seed };

        // 中央短墙（留缺口）—— 提供遮挡但不分割地图
        _AddWallWithGap(m, new Vector2(ws.X * 0.5f, ws.Y * 0.30f), true, 360, 140);
        _AddWallWithGap(m, new Vector2(ws.X * 0.5f, ws.Y * 0.70f), true, 360, 140);
        // 四角立柱（固体）
        foreach (var fx in new[] { 0.16f, 0.84f })
            foreach (var fy in new[] { 0.16f, 0.84f })
            {
                var pos = new Vector2(ws.X * fx, ws.Y * fy);
                m.Obstacles.Add(new ObstacleDef(pos, new Vector2(44, 44), ObstacleKind.Solid, variant: 3));
            }
        // 可破坏铁门（分散）
        for (int i = 0; i < 4; i++)
        {
            var pos = new Vector2(
                ws.X * rng.RandfRange(0.20f, 0.80f),
                ws.Y * rng.RandfRange(0.20f, 0.80f));
            if (m.InSpawnSafeZone(pos)) continue;
            m.Obstacles.Add(new ObstacleDef(pos, new Vector2(55, 20), ObstacleKind.Destructible,
                rng.Randf() < 0.5f ? 0f : Mathf.Pi / 2f, variant: 3));
        }
        // 地面水洼减速
        for (int i = 0; i < 6; i++)
        {
            var pos = new Vector2(
                ws.X * rng.RandfRange(0.10f, 0.90f),
                ws.Y * rng.RandfRange(0.10f, 0.90f));
            if (m.InSpawnSafeZone(pos)) continue;
            m.Obstacles.Add(new ObstacleDef(pos, new Vector2(rng.RandfRange(80, 120), rng.RandfRange(60, 90)),
                ObstacleKind.Low, rng.RandfRange(0f, Mathf.Tau), variant: 1));
        }
        return m;
    }

    // ── 沼泽：蜿蜒水道 + 毒雾陷阱，墨绿腐败氛围 ───────────
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
            GroundTint = new Color(0.65f, 0.75f, 0.60f),
            AtmosphereTint = new Color(0.30f, 0.55f, 0.30f), // 墨绿雾霭
            AtmosphereAlpha = 0.35f,
            FogTint = new Color(0.10f, 0.16f, 0.10f),
            AtmosphereTexturePath = "res://assets/sprites/backgrounds/atmosphere_swamp.jpg",
            GroundTexturePath = "res://assets/sprites/backgrounds/atmosphere_swamp.jpg"
        };
        var rng = new RandomNumberGenerator { Seed = (ulong)m.Seed };

        // 蜿蜒水池链（低矮减速）—— 形成水道走廊
        for (int i = 0; i < 8; i++)
        {
            float t = i / 8f;
            float angle = t * Mathf.Tau * 0.8f + 0.3f;
            var pos = new Vector2(
                ws.X * (0.5f + Mathf.Cos(angle) * 0.30f),
                ws.Y * (0.5f + Mathf.Sin(angle) * 0.25f));
            if (m.InSpawnSafeZone(pos)) continue;
            var sz = new Vector2(rng.RandfRange(100, 160), rng.RandfRange(70, 110));
            m.Obstacles.Add(new ObstacleDef(pos, sz, ObstacleKind.Low,
                angle, variant: 1));
        }
        // 枯树掩体（固体）
        for (int i = 0; i < 8; i++)
        {
            var pos = new Vector2(
                ws.X * rng.RandfRange(0.10f, 0.90f),
                ws.Y * rng.RandfRange(0.10f, 0.90f));
            if (m.InSpawnSafeZone(pos)) continue;
            m.Obstacles.Add(new ObstacleDef(pos, new Vector2(34, 34), ObstacleKind.Solid, variant: 2));
        }
        // 毒沼陷阱 — 分布在水道交汇处
        for (int i = 0; i < 5; i++)
        {
            var pos = new Vector2(
                ws.X * rng.RandfRange(0.20f, 0.80f),
                ws.Y * rng.RandfRange(0.20f, 0.80f));
            if (m.InSpawnSafeZone(pos)) continue;
            m.Obstacles.Add(new ObstacleDef(pos, new Vector2(85, 85), ObstacleKind.Hazard, variant: 1));
        }
        return m;
    }

    // ── 大教堂：双排廊柱 + 圣坛区域，猩红庄严氛围 ─────────
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
            GroundTint = new Color(0.88f, 0.82f, 0.75f),
            AtmosphereTint = new Color(0.85f, 0.35f, 0.20f), // 猩红血色
            AtmosphereAlpha = 0.30f,
            FogTint = new Color(0.20f, 0.10f, 0.08f),
            AtmosphereTexturePath = "res://assets/sprites/backgrounds/atmosphere_blood_moon.jpg",
            GroundTexturePath = "res://assets/sprites/backgrounds/ground_stone.jpg"
        };

        // 双排石柱走廊（左右各 4 柱，形成长廊通道）
        for (int i = 0; i < 4; i++)
        {
            float t = 0.18f + i * 0.18f;
            // 左排
            var posL = new Vector2(ws.X * 0.28f, ws.Y * t);
            if (!m.InSpawnSafeZone(posL))
                m.Obstacles.Add(new ObstacleDef(posL, new Vector2(46, 46), ObstacleKind.Solid, variant: 3));
            // 右排
            var posR = new Vector2(ws.X * 0.72f, ws.Y * t);
            if (!m.InSpawnSafeZone(posR))
                m.Obstacles.Add(new ObstacleDef(posR, new Vector2(46, 46), ObstacleKind.Solid, variant: 3));
        }
        // 中央圣坛区域（可破坏祭坛 + 固体围栏）
        m.Obstacles.Add(new ObstacleDef(
            new Vector2(ws.X * 0.5f, ws.Y * 0.15f), new Vector2(130, 42),
            ObstacleKind.Destructible, variant: 4));
        m.Obstacles.Add(new ObstacleDef(
            new Vector2(ws.X * 0.5f, ws.Y * 0.85f), new Vector2(130, 42),
            ObstacleKind.Destructible, variant: 4));
        // 地面减速区（血迹低洼）
        var rng = new RandomNumberGenerator { Seed = (ulong)m.Seed };
        for (int i = 0; i < 6; i++)
        {
            var pos = new Vector2(
                ws.X * rng.RandfRange(0.12f, 0.88f),
                ws.Y * rng.RandfRange(0.12f, 0.88f));
            if (m.InSpawnSafeZone(pos)) continue;
            m.Obstacles.Add(new ObstacleDef(pos, new Vector2(rng.RandfRange(60, 100), rng.RandfRange(50, 80)),
                ObstacleKind.Low, rng.RandfRange(0f, Mathf.Tau), variant: 3));
        }
        return m;
    }

    // ── 荒原：零散掩体 + 燃烧弹坑，炽热烈焰氛围 ─────────
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
            GroundTint = new Color(0.88f, 0.75f, 0.60f),
            AtmosphereTint = new Color(0.90f, 0.45f, 0.15f),
            AtmosphereAlpha = 0.28f,
            FogTint = new Color(0.20f, 0.12f, 0.06f),
            AtmosphereTexturePath = "res://assets/sprites/backgrounds/atmosphere_storm.jpg",
            GroundTexturePath = "res://assets/sprites/backgrounds/ground_cemetery.jpg"
        };
        var rng = new RandomNumberGenerator { Seed = (ulong)m.Seed };

        // 随机散布金属掩体（固体）
        for (int i = 0; i < 8; i++)
        {
            var pos = new Vector2(
                ws.X * rng.RandfRange(0.10f, 0.90f),
                ws.Y * rng.RandfRange(0.10f, 0.90f));
            if (m.InSpawnSafeZone(pos)) continue;
            m.Obstacles.Add(new ObstacleDef(pos, new Vector2(60, 28), ObstacleKind.Solid,
                rng.RandfRange(0f, Mathf.Tau), variant: 5));
        }
        // 燃烧弹坑（陷阱）
        for (int i = 0; i < 6; i++)
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
