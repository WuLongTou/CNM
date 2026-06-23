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
    Wasteland,   // 荒原：零散掩体 + 陷阱地带
    Glacier,     // 冰窟：冰柱掩体 + 冰面 + 冰刺脉冲陷阱
    Volcano,     // 火山：岩浆河 + 玄武岩 + 炸药桶
    Desert       // 沙漠：方尖碑遗迹 + 沙丘 + 流沙
}

/// <summary>障碍物种类 —— 决定碰撞与交互行为。</summary>
public enum ObstacleKind
{
    Solid,          // 固体墙/掩体：完全阻挡，玩家与地面僵尸绕行
    Destructible,   // 可破坏：有血量，受攻击/僵尸推挤会破碎
    Low,            // 低矮装饰：可穿过，仅减速
    Hazard,         // 陷阱/危险区域：踩入掉血 + 减速
    Explosive       // 爆炸桶：可破坏，破碎时范围爆炸 + 击退
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
            _Wasteland(),
            _Glacier(),
            _Volcano(),
            _Desert()
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

        // 中心环形残墙防御带（4 段带缺口，围绕出生点，可绕圈走位）
        _AddWallWithGap(m, new Vector2(ws.X * 0.5f, ws.Y * 0.28f), true, 460, 130);
        _AddWallWithGap(m, new Vector2(ws.X * 0.5f, ws.Y * 0.72f), true, 460, 130);
        _AddWallWithGap(m, new Vector2(ws.X * 0.28f, ws.Y * 0.5f), false, 400, 120);
        _AddWallWithGap(m, new Vector2(ws.X * 0.72f, ws.Y * 0.5f), false, 400, 120);

        // 4 象限墓碑掩体集群（阵地）
        _Cluster(m, new Vector2(0.20f, 0.20f), 4, 0.08f, ObstacleKind.Solid, new Vector2(70, 24), 1, rng);
        _Cluster(m, new Vector2(0.80f, 0.20f), 4, 0.08f, ObstacleKind.Solid, new Vector2(70, 24), 1, rng);
        _Cluster(m, new Vector2(0.20f, 0.80f), 4, 0.08f, ObstacleKind.Solid, new Vector2(70, 24), 1, rng);
        _Cluster(m, new Vector2(0.80f, 0.80f), 4, 0.08f, ObstacleKind.Solid, new Vector2(70, 24), 1, rng);

        // 外圈可破坏木栅栏
        _Ring(m, 0.42f, 0.40f, 8, ObstacleKind.Destructible, new Vector2(70, 16), 2, rng);

        // 低矮墓碑减速装饰
        _ScatterLow(m, rng, 14);

        // 方尖碑地标（东西边缘导航参考）
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.12f, ws.Y * 0.5f),
            new Vector2(46, 120), ObstacleKind.Solid, variant: 6));
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.88f, ws.Y * 0.5f),
            new Vector2(46, 120), ObstacleKind.Solid, variant: 6));
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

        var rng = new RandomNumberGenerator { Seed = (ulong)m.Seed };

        // 放射状十字石墙（4 臂带缺口，从中心向外延伸，形成通道与遮挡）
        _AddWallWithGap(m, new Vector2(ws.X * 0.5f, ws.Y * 0.27f), true, 520, 130);
        _AddWallWithGap(m, new Vector2(ws.X * 0.5f, ws.Y * 0.73f), true, 520, 130);
        _AddWallWithGap(m, new Vector2(ws.X * 0.27f, ws.Y * 0.5f), false, 460, 120);
        _AddWallWithGap(m, new Vector2(ws.X * 0.73f, ws.Y * 0.5f), false, 460, 120);

        // 外圈石柱环（防御带，可绕柱走位）
        _Ring(m, 0.38f, 0.36f, 10, ObstacleKind.Solid, new Vector2(44, 44), 3, rng);

        // 4 象限可破坏碎石集群
        _Cluster(m, new Vector2(0.22f, 0.22f), 3, 0.06f, ObstacleKind.Destructible, new Vector2(50, 50), 1, rng);
        _Cluster(m, new Vector2(0.78f, 0.78f), 3, 0.06f, ObstacleKind.Destructible, new Vector2(50, 50), 1, rng);

        // 方尖碑地标 ×2 + 拱门地标（四角导航参考）
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.14f, ws.Y * 0.16f),
            new Vector2(48, 130), ObstacleKind.Solid, variant: 6));
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.86f, ws.Y * 0.84f),
            new Vector2(48, 130), ObstacleKind.Solid, variant: 6));
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.86f, ws.Y * 0.16f),
            new Vector2(110, 100), ObstacleKind.Solid, variant: 7));
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

        // 九宫格分区短墙（2 横 2 竖，每段带缺口，中心格留空形成出生安全区）
        _AddWallWithGap(m, new Vector2(ws.X * 0.5f, ws.Y * 0.33f), true, 560, 150);
        _AddWallWithGap(m, new Vector2(ws.X * 0.5f, ws.Y * 0.67f), true, 560, 150);
        _AddWallWithGap(m, new Vector2(ws.X * 0.33f, ws.Y * 0.5f), false, 500, 140);
        _AddWallWithGap(m, new Vector2(ws.X * 0.67f, ws.Y * 0.5f), false, 500, 140);

        // 4 角石柱集群（房间感）
        _Cluster(m, new Vector2(0.16f, 0.16f), 3, 0.05f, ObstacleKind.Solid, new Vector2(40, 40), 3, rng);
        _Cluster(m, new Vector2(0.84f, 0.16f), 3, 0.05f, ObstacleKind.Solid, new Vector2(40, 40), 3, rng);
        _Cluster(m, new Vector2(0.16f, 0.84f), 3, 0.05f, ObstacleKind.Solid, new Vector2(40, 40), 3, rng);
        _Cluster(m, new Vector2(0.84f, 0.84f), 3, 0.05f, ObstacleKind.Solid, new Vector2(40, 40), 3, rng);

        // 可破坏铁门（房间入口）
        _Cluster(m, new Vector2(0.33f, 0.16f), 2, 0.04f, ObstacleKind.Destructible, new Vector2(55, 20), 3, rng);
        _Cluster(m, new Vector2(0.67f, 0.84f), 2, 0.04f, ObstacleKind.Destructible, new Vector2(55, 20), 3, rng);

        // 脉冲陷阱地带（周期激活，逼玩家踩时机：左脉冲毒 variant3 / 右脉冲火 variant4）
        _Cluster(m, new Vector2(0.16f, 0.5f), 2, 0.05f, ObstacleKind.Hazard, new Vector2(85, 85), 3, rng);
        _Cluster(m, new Vector2(0.84f, 0.5f), 2, 0.05f, ObstacleKind.Hazard, new Vector2(85, 85), 4, rng);

        // 地面水洼减速
        _ScatterLow(m, rng, 8);

        // 拱门地标（南北房间标识）
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.5f, ws.Y * 0.12f),
            new Vector2(110, 100), ObstacleKind.Solid, variant: 7));
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.5f, ws.Y * 0.88f),
            new Vector2(110, 100), ObstacleKind.Solid, variant: 7));
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

        // S 形蜿蜒水道（连贯减速走廊，沿正弦曲线分布水池）
        for (int i = 0; i < 12; i++)
        {
            float t = i / 11f;
            var pos = new Vector2(
                ws.X * (0.15f + t * 0.70f),
                ws.Y * (0.5f + Mathf.Sin(t * Mathf.Pi * 2.5f) * 0.26f));
            if (m.InSpawnSafeZone(pos)) continue;
            var sz = new Vector2(rng.RandfRange(130, 180), rng.RandfRange(80, 120));
            m.Obstacles.Add(new ObstacleDef(pos, sz, ObstacleKind.Low, rng.RandfRange(-0.2f, 0.2f), variant: 1));
        }

        // 沿岸枯树掩体（水道两侧集群）
        _Cluster(m, new Vector2(0.25f, 0.20f), 4, 0.10f, ObstacleKind.Solid, new Vector2(34, 34), 2, rng);
        _Cluster(m, new Vector2(0.75f, 0.80f), 4, 0.10f, ObstacleKind.Solid, new Vector2(34, 34), 2, rng);
        _Cluster(m, new Vector2(0.75f, 0.20f), 4, 0.10f, ObstacleKind.Solid, new Vector2(34, 34), 2, rng);
        _Cluster(m, new Vector2(0.25f, 0.80f), 4, 0.10f, ObstacleKind.Solid, new Vector2(34, 34), 2, rng);

        // 毒沼陷阱（水道交汇处）
        var hazards = new[] { new Vector2(0.30f, 0.5f), new Vector2(0.70f, 0.5f), new Vector2(0.5f, 0.24f), new Vector2(0.5f, 0.76f) };
        foreach (var hf in hazards)
        {
            var pos = new Vector2(ws.X * hf.X, ws.Y * hf.Y);
            if (m.InSpawnSafeZone(pos)) continue;
            m.Obstacles.Add(new ObstacleDef(pos, new Vector2(90, 90), ObstacleKind.Hazard, variant: 1));
        }

        // 枯巨树地标（沼泽深处导航参考）
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.15f, ws.Y * 0.85f),
            new Vector2(90, 130), ObstacleKind.Solid, variant: 8));
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.85f, ws.Y * 0.15f),
            new Vector2(90, 130), ObstacleKind.Solid, variant: 8));
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

        var rng = new RandomNumberGenerator { Seed = (ulong)m.Seed };

        // 双排长廊柱（左右各 6 柱，加密形成对称长廊通道）
        for (int i = 0; i < 6; i++)
        {
            float t = 0.12f + i * 0.15f;
            var posL = new Vector2(ws.X * 0.28f, ws.Y * t);
            if (!m.InSpawnSafeZone(posL))
                m.Obstacles.Add(new ObstacleDef(posL, new Vector2(46, 46), ObstacleKind.Solid, variant: 3));
            var posR = new Vector2(ws.X * 0.72f, ws.Y * t);
            if (!m.InSpawnSafeZone(posR))
                m.Obstacles.Add(new ObstacleDef(posR, new Vector2(46, 46), ObstacleKind.Solid, variant: 3));
        }

        // 中央圣坛围栏（可破坏，南北各一段）
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.5f, ws.Y * 0.15f),
            new Vector2(150, 42), ObstacleKind.Destructible, variant: 4));
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.5f, ws.Y * 0.85f),
            new Vector2(150, 42), ObstacleKind.Destructible, variant: 4));

        // 侧廊可破坏长椅（掩体）
        _Cluster(m, new Vector2(0.5f, 0.32f), 3, 0.06f, ObstacleKind.Destructible, new Vector2(70, 22), 2, rng);
        _Cluster(m, new Vector2(0.5f, 0.68f), 3, 0.06f, ObstacleKind.Destructible, new Vector2(70, 22), 2, rng);

        // 血迹低洼减速
        _ScatterLow(m, rng, 10);

        // 钟楼地标 ×4（四角，对称庄严）
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.12f, ws.Y * 0.12f),
            new Vector2(70, 140), ObstacleKind.Solid, variant: 9));
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.88f, ws.Y * 0.12f),
            new Vector2(70, 140), ObstacleKind.Solid, variant: 9));
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.12f, ws.Y * 0.88f),
            new Vector2(70, 140), ObstacleKind.Solid, variant: 9));
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.88f, ws.Y * 0.88f),
            new Vector2(70, 140), ObstacleKind.Solid, variant: 9));
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

        // 3 组金属掩体集群（阵地，玩家可据守）
        _Cluster(m, new Vector2(0.25f, 0.25f), 4, 0.07f, ObstacleKind.Solid, new Vector2(60, 28), 5, rng);
        _Cluster(m, new Vector2(0.75f, 0.75f), 4, 0.07f, ObstacleKind.Solid, new Vector2(60, 28), 5, rng);
        _Cluster(m, new Vector2(0.75f, 0.25f), 3, 0.06f, ObstacleKind.Solid, new Vector2(60, 28), 5, rng);

        // 燃烧弹坑陷阱集群（危险地带）
        _Cluster(m, new Vector2(0.25f, 0.72f), 3, 0.06f, ObstacleKind.Hazard, new Vector2(80, 80), 2, rng);
        _Cluster(m, new Vector2(0.6f, 0.6f), 2, 0.05f, ObstacleKind.Hazard, new Vector2(80, 80), 2, rng);

        // 散布炸药桶（战术清怪，可引爆）+ 可破坏废墟
        _Cluster(m, new Vector2(0.25f, 0.5f), 3, 0.08f, ObstacleKind.Explosive, new Vector2(40, 50), 0, rng);
        _Cluster(m, new Vector2(0.55f, 0.4f), 2, 0.05f, ObstacleKind.Destructible, new Vector2(55, 40), 1, rng);

        // 低矮碎石
        _ScatterLow(m, rng, 10);

        // 拱门 + 方尖碑地标（废墟导航参考）
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.5f, ws.Y * 0.12f),
            new Vector2(110, 100), ObstacleKind.Solid, variant: 7));
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.12f, ws.Y * 0.88f),
            new Vector2(48, 130), ObstacleKind.Solid, variant: 6));
        return m;
    }

    // ── 布局辅助：环形 / 集群（围绕出生点的战术结构）─────────

    // 沿椭圆环形布置障碍（围绕出生点的防御带）
    private static void _Ring(MapData m, float rFracX, float rFracY, int count, ObstacleKind kind,
        Vector2 size, int variant, RandomNumberGenerator rng, float rotJitter = 0f)
    {
        var ws = m.WorldSize;
        for (int i = 0; i < count; i++)
        {
            float a = Mathf.Tau * i / count + rng.RandfRange(-0.08f, 0.08f);
            var pos = new Vector2(
                ws.X * (0.5f + Mathf.Cos(a) * rFracX),
                ws.Y * (0.5f + Mathf.Sin(a) * rFracY));
            if (m.InSpawnSafeZone(pos)) continue;
            float rot = rotJitter > 0 ? rng.RandfRange(-rotJitter, rotJitter) : 0f;
            m.Obstacles.Add(new ObstacleDef(pos, size, kind, rot, variant));
        }
    }

    // 在某分式位置附近放一组障碍（掩体阵地）
    private static void _Cluster(MapData m, Vector2 centerFrac, int count, float spreadFrac,
        ObstacleKind kind, Vector2 size, int variant, RandomNumberGenerator rng)
    {
        var ws = m.WorldSize;
        var center = new Vector2(ws.X * centerFrac.X, ws.Y * centerFrac.Y);
        for (int i = 0; i < count; i++)
        {
            var pos = center + new Vector2(
                (rng.Randf() - 0.5f) * ws.X * spreadFrac,
                (rng.Randf() - 0.5f) * ws.Y * spreadFrac);
            if (m.InSpawnSafeZone(pos)) continue;
            m.Obstacles.Add(new ObstacleDef(pos, size, kind, rng.RandfRange(0f, Mathf.Tau), variant));
        }
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

    // ── 冰窟：冰柱掩体 + 冰面走廊 + 地热脉冲，冷蓝白色调 ────────
    private static MapData _Glacier()
    {
        var ws = StandardSize;
        var m = new MapData
        {
            Id = "glacier",
            Name = "寒冰墓穴",
            Theme = MapTheme.Glacier,
            WorldSize = ws,
            Seed = 77777,
            GroundTint = new Color(0.80f, 0.88f, 0.92f),
            AtmosphereTint = new Color(0.42f, 0.58f, 0.78f),
            AtmosphereAlpha = 0.32f,
            FogTint = new Color(0.10f, 0.14f, 0.22f),
            AtmosphereTexturePath = "res://assets/sprites/backgrounds/atmosphere_storm2.jpg",
            GroundTexturePath = "res://assets/sprites/backgrounds/ground_dungeon.jpg"
        };
        var rng = new RandomNumberGenerator { Seed = (ulong)m.Seed };

        // 冰柱掩体环（外圈防御带）+ 内圈集群
        _Ring(m, 0.36f, 0.34f, 12, ObstacleKind.Solid, new Vector2(44, 44), 3, rng);
        _Cluster(m, new Vector2(0.25f, 0.5f), 4, 0.08f, ObstacleKind.Solid, new Vector2(40, 40), 3, rng);
        _Cluster(m, new Vector2(0.75f, 0.5f), 4, 0.08f, ObstacleKind.Solid, new Vector2(40, 40), 3, rng);

        // 冰面减速走廊（横贯，中心安全区自然断开形成通道）
        for (int i = 0; i < 8; i++)
        {
            float t = i / 7f;
            var pos = new Vector2(ws.X * (0.15f + t * 0.70f), ws.Y * 0.5f);
            if (m.InSpawnSafeZone(pos)) continue;
            m.Obstacles.Add(new ObstacleDef(pos, new Vector2(rng.RandfRange(110, 150), rng.RandfRange(70, 100)),
                ObstacleKind.Low, rng.RandfRange(-0.1f, 0.1f), variant: 1));
        }

        // 地热间歇泉（脉冲火 variant4，冰窟深处的周期喷发）
        var spikes = new[] { new Vector2(0.2f, 0.25f), new Vector2(0.8f, 0.25f), new Vector2(0.2f, 0.75f), new Vector2(0.8f, 0.75f) };
        foreach (var sf in spikes)
        {
            var pos = new Vector2(ws.X * sf.X, ws.Y * sf.Y);
            if (m.InSpawnSafeZone(pos)) continue;
            m.Obstacles.Add(new ObstacleDef(pos, new Vector2(80, 80), ObstacleKind.Hazard, variant: 4));
        }

        // 拱门地标（冰窟入口）×2
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.5f, ws.Y * 0.12f),
            new Vector2(110, 100), ObstacleKind.Solid, variant: 7));
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.5f, ws.Y * 0.88f),
            new Vector2(110, 100), ObstacleKind.Solid, variant: 7));
        return m;
    }

    // ── 火山：岩浆河 + 玄武岩柱 + 炸药桶，炽热烈焰色调 ──────────
    private static MapData _Volcano()
    {
        var ws = StandardSize;
        var m = new MapData
        {
            Id = "volcano",
            Name = "烈焰熔岩",
            Theme = MapTheme.Volcano,
            WorldSize = ws,
            Seed = 88888,
            GroundTint = new Color(0.50f, 0.34f, 0.28f),
            AtmosphereTint = new Color(0.90f, 0.42f, 0.15f),
            AtmosphereAlpha = 0.34f,
            FogTint = new Color(0.16f, 0.08f, 0.05f),
            AtmosphereTexturePath = "res://assets/sprites/backgrounds/atmosphere_storm.jpg",
            GroundTexturePath = "res://assets/sprites/backgrounds/ground_cemetery.jpg"
        };
        var rng = new RandomNumberGenerator { Seed = (ulong)m.Seed };

        // 岩浆河（蜿蜒陷阱带，贯穿地图）
        for (int i = 0; i < 10; i++)
        {
            float t = i / 9f;
            var pos = new Vector2(
                ws.X * (0.12f + t * 0.76f),
                ws.Y * (0.5f + Mathf.Sin(t * Mathf.Pi * 2.2f) * 0.22f));
            if (m.InSpawnSafeZone(pos)) continue;
            m.Obstacles.Add(new ObstacleDef(pos, new Vector2(rng.RandfRange(90, 130), rng.RandfRange(70, 100)),
                ObstacleKind.Hazard, rng.RandfRange(-0.1f, 0.1f), variant: 2));
        }

        // 玄武岩柱掩体集群（岩浆两岸安全高地）
        _Cluster(m, new Vector2(0.22f, 0.22f), 4, 0.08f, ObstacleKind.Solid, new Vector2(42, 42), 3, rng);
        _Cluster(m, new Vector2(0.78f, 0.78f), 4, 0.08f, ObstacleKind.Solid, new Vector2(42, 42), 3, rng);
        _Cluster(m, new Vector2(0.78f, 0.22f), 3, 0.07f, ObstacleKind.Solid, new Vector2(42, 42), 3, rng);

        // 炸药桶（火山口附近，引爆连清）
        _Cluster(m, new Vector2(0.5f, 0.22f), 3, 0.06f, ObstacleKind.Explosive, new Vector2(40, 50), 0, rng);
        _Cluster(m, new Vector2(0.5f, 0.78f), 2, 0.05f, ObstacleKind.Explosive, new Vector2(40, 50), 0, rng);

        // 方尖碑地标（火山岩塔）×2
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.14f, ws.Y * 0.5f),
            new Vector2(46, 130), ObstacleKind.Solid, variant: 6));
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.86f, ws.Y * 0.5f),
            new Vector2(46, 130), ObstacleKind.Solid, variant: 6));
        return m;
    }

    // ── 沙漠：方尖碑遗迹 + 沙丘 + 流沙，黄沙金色调 ────────────
    private static MapData _Desert()
    {
        var ws = StandardSize;
        var m = new MapData
        {
            Id = "desert",
            Name = "黄沙遗迹",
            Theme = MapTheme.Desert,
            WorldSize = ws,
            Seed = 99999,
            GroundTint = new Color(0.86f, 0.76f, 0.52f),
            AtmosphereTint = new Color(0.90f, 0.76f, 0.42f),
            AtmosphereAlpha = 0.28f,
            FogTint = new Color(0.26f, 0.18f, 0.10f),
            AtmosphereTexturePath = "res://assets/sprites/backgrounds/atmosphere_storm.jpg",
            GroundTexturePath = "res://assets/sprites/backgrounds/ground_ruins.jpg"
        };
        var rng = new RandomNumberGenerator { Seed = (ulong)m.Seed };

        // 残破方尖碑地标阵（沙漠遗迹核心记忆点，3 座）
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.30f, ws.Y * 0.22f),
            new Vector2(48, 135), ObstacleKind.Solid, variant: 6));
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.70f, ws.Y * 0.22f),
            new Vector2(48, 135), ObstacleKind.Solid, variant: 6));
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.5f, ws.Y * 0.82f),
            new Vector2(48, 135), ObstacleKind.Solid, variant: 6));

        // 石柱掩体环（防御带）
        _Ring(m, 0.34f, 0.32f, 10, ObstacleKind.Solid, new Vector2(44, 44), 3, rng);

        // 拱门地标（遗迹入口）
        m.Obstacles.Add(new ObstacleDef(new Vector2(ws.X * 0.5f, ws.Y * 0.15f),
            new Vector2(110, 100), ObstacleKind.Solid, variant: 7));

        // 流沙陷阱（南北各一片，吞噬减速）
        _Cluster(m, new Vector2(0.3f, 0.5f), 2, 0.06f, ObstacleKind.Hazard, new Vector2(90, 90), 1, rng);
        _Cluster(m, new Vector2(0.7f, 0.5f), 2, 0.06f, ObstacleKind.Hazard, new Vector2(90, 90), 1, rng);

        // 散布沙丘（减速）+ 可破坏石棺（探索奖励）
        _ScatterLow(m, rng, 12);
        _Cluster(m, new Vector2(0.5f, 0.4f), 3, 0.07f, ObstacleKind.Destructible, new Vector2(55, 35), 4, rng);
        return m;
    }
}
