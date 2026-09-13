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
    // 区域 mask（MapGenerator 生成，地面 shader 按通道混色；null = 纯底图模式）
    public Image? GroundMask;
    public Color MaskForestTint = new(0.35f, 0.42f, 0.30f);
    public Color MaskRockTint = new(0.45f, 0.43f, 0.40f);
    public Color MaskWaterTint = new(0.25f, 0.32f, 0.38f);
    public float MaskStrength = 0.55f;

    public bool InSpawnSafeZone(Vector2 p)
    {
        var nx = (p.X / WorldSize.X - 0.5f) / GameConfig.MapSpawnSafeRadiusX;
        var ny = (p.Y / WorldSize.Y - 0.5f) / GameConfig.MapSpawnSafeRadiusY;
        return nx * nx + ny * ny < 1f;
    }
}
