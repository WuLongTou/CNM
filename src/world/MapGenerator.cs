namespace ZombieWars.World;

using Godot;
using ZombieWars.Core;

/// <summary>
/// 种子化地图生成器 —— 每局按（主题，种子）生成无限变化的和谐地形（魔兽式分区感）。
///
/// 流程：Cellular 噪声划区域（森林/岩石/水域/空地）→ 障碍跟随区域摆放
/// （树进林、石上岩、水洼成塘）→ 输出 192×108 区域 mask，地面 shader 按 mask
/// 把主题三色柔和混入底图GrandTexturePath），大尺度色调与障碍分布一致。
///
/// 连通性保证：墙体只用短缺口段（≤480px）；Solid 每网格 cell 最多 1 个；
/// Hazard/Low 不阻挡；出生椭圆整片清空（MapBuilder 另有兜底过滤）。
/// </summary>
public static class MapGenerator
{
    // 网格分辨率：cell ≈ 60px（3840 世界），区域块度与玩法尺度匹配
    private const int GridX = 64;
    private const int GridY = 36;
    private const int MaskW = 192;
    private const int MaskH = 108;

    private enum Region { Clear, Forest, Rock, Water }

    private static readonly string[] Epithets =
    {
        "余烬", "迷雾", "嚎风", "血月", "腐殖", "霜冻", "熔火", "黄沙", "幽暗", "荆棘",
    };

    /// <summary>主题生成参数：变体选择 + mask 三色（与障碍视觉呼应）。</summary>
    private sealed class ThemeParams
    {
        public int ForestSolid;   // 森林区 solid 变体
        public int RockSolid;     // 岩石区 solid 变体
        public int WallVariant = 4;
        public int HazardVariant = 2;
        public int LowVariant = 3;
        public bool ExplosiveDots; // 是否点缀炸药桶（火山/荒原）
        public Color ForestTint = new(0.35f, 0.42f, 0.30f);
        public Color RockTint = new(0.45f, 0.43f, 0.40f);
        public Color WaterTint = new(0.25f, 0.32f, 0.38f);
        public float MaskStrength = 0.38f;
    }

    private static ThemeParams _ParamsFor(MapTheme theme) => theme switch
    {
        MapTheme.Graveyard => new ThemeParams
            { ForestSolid = 1, RockSolid = 0, HazardVariant = 3,
              ForestTint = new Color(0.30f, 0.34f, 0.32f), RockTint = new Color(0.45f, 0.46f, 0.48f), WaterTint = new Color(0.24f, 0.30f, 0.38f) },
        MapTheme.Ruins => new ThemeParams
            { ForestSolid = 0, RockSolid = 4, HazardVariant = 2,
              ForestTint = new Color(0.42f, 0.38f, 0.30f), RockTint = new Color(0.50f, 0.46f, 0.40f), WaterTint = new Color(0.30f, 0.32f, 0.34f) },
        MapTheme.Dungeon => new ThemeParams
            { ForestSolid = 3, RockSolid = 4, HazardVariant = 3,
              ForestTint = new Color(0.26f, 0.26f, 0.34f), RockTint = new Color(0.36f, 0.35f, 0.42f), WaterTint = new Color(0.20f, 0.22f, 0.32f) },
        MapTheme.Swamp => new ThemeParams
            { ForestSolid = 2, RockSolid = 0, HazardVariant = 1, LowVariant = 1,
              ForestTint = new Color(0.24f, 0.38f, 0.22f), RockTint = new Color(0.36f, 0.38f, 0.30f), WaterTint = new Color(0.16f, 0.30f, 0.22f), MaskStrength = 0.45f },
        MapTheme.Cathedral => new ThemeParams
            { ForestSolid = 3, RockSolid = 4, HazardVariant = 2,
              ForestTint = new Color(0.40f, 0.32f, 0.26f), RockTint = new Color(0.48f, 0.42f, 0.36f), WaterTint = new Color(0.32f, 0.26f, 0.24f) },
        MapTheme.Wasteland => new ThemeParams
            { ForestSolid = 5, RockSolid = 0, HazardVariant = 2, ExplosiveDots = true,
              ForestTint = new Color(0.42f, 0.34f, 0.24f), RockTint = new Color(0.48f, 0.40f, 0.30f), WaterTint = new Color(0.36f, 0.26f, 0.18f) },
        MapTheme.Glacier => new ThemeParams
            { ForestSolid = 3, RockSolid = 4, HazardVariant = 4, LowVariant = 1,
              ForestTint = new Color(0.52f, 0.62f, 0.70f), RockTint = new Color(0.60f, 0.68f, 0.74f), WaterTint = new Color(0.36f, 0.50f, 0.62f) },
        MapTheme.Volcano => new ThemeParams
            { ForestSolid = 3, RockSolid = 0, HazardVariant = 2, ExplosiveDots = true,
              ForestTint = new Color(0.34f, 0.22f, 0.18f), RockTint = new Color(0.42f, 0.28f, 0.22f), WaterTint = new Color(0.62f, 0.22f, 0.06f), MaskStrength = 0.45f },
        _ => new ThemeParams // Desert
            { ForestSolid = 0, RockSolid = 0, HazardVariant = 1, WallVariant = 6,
              ForestTint = new Color(0.58f, 0.48f, 0.30f), RockTint = new Color(0.62f, 0.54f, 0.36f), WaterTint = new Color(0.50f, 0.40f, 0.24f) },
    };

    /// <summary>生成一张新地图（调用方负责随机种子，实现每局不同）。</summary>
    public static MapData Generate(MapTheme theme, int seed, Vector2 worldSize)
    {
        var p = _ParamsFor(theme);
        var rng = new RandomNumberGenerator { Seed = (ulong)seed };

        var regionNoise = new FastNoiseLite
        {
            Seed = seed,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular,
            Frequency = 0.045f,
            CellularJitter = 1.0f,
            FractalOctaves = 1,
        };
        var detailNoise = new FastNoiseLite
        {
            Seed = seed ^ unchecked((int)0x9E3779B9),
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            Frequency = 0.012f,
            FractalOctaves = 3,
        };

        var m = new MapData
        {
            Id = $"{theme.ToString().ToLower()}_{seed}",
            Name = $"{_ThemeName(theme)} · {Epithets[Mathf.Abs(seed) % Epithets.Length]}",
            Theme = theme,
            WorldSize = worldSize,
            Seed = seed,
        };
        _ApplyThemeLook(m, theme);

        float cellW = worldSize.X / GridX;
        float cellH = worldSize.Y / GridY;
        var regions = new Region[GridX, GridY];

        // ── 1. 分区 ──
        for (int gx = 0; gx < GridX; gx++)
        {
            for (int gy = 0; gy < GridY; gy++)
            {
                var center = new Vector2((gx + 0.5f) * cellW, (gy + 0.5f) * cellH);
                if (m.InSpawnSafeZone(center))
                {
                    regions[gx, gy] = Region.Clear;
                    continue;
                }
                // Cellular 距离场 + Simplex 扰动阈值：水域成塘、森林成片
                float n = regionNoise.GetNoise2D(gx * 1.7f, gy * 1.7f); // 约 [-1, 1]
                float d = detailNoise.GetNoise2D(center.X, center.Y);
                regions[gx, gy] = (n, d) switch
                {
                    var (cn, _) when cn > 0.42f => Region.Water,
                    var (cn, _) when cn < -0.38f => Region.Forest,
                    var (_, dn) when dn > 0.45f => Region.Rock,
                    _ => Region.Clear,
                };
            }
        }

        // ── 2. 跟随摆放（总量硬上限：性能与可玩性双保险）──
        const int MaxSolids = 110;
        const int MaxWaters = 30;
        const int MaxHazards = 20;
        const int MaxWalls = 14;
        const int MaxDestructs = 30;
        int nSolids = 0, nWaters = 0, nHazards = 0, nWalls = 0, nDestructs = 0;
        var solidTaken = new bool[GridX, GridY];
        var waterDone = new bool[GridX, GridY];
        // 3×3 邻域是否有 solid（保证掩体间距，留穿行缝）
        bool SolidNear(int x, int y)
        {
            for (int ax = Mathf.Max(0, x - 1); ax <= Mathf.Min(GridX - 1, x + 1); ax++)
                for (int ay = Mathf.Max(0, y - 1); ay <= Mathf.Min(GridY - 1, y + 1); ay++)
                    if (solidTaken[ax, ay]) return true;
            return false;
        }
        for (int gx = 0; gx < GridX; gx++)
        {
            for (int gy = 0; gy < GridY; gy++)
            {
                var center = new Vector2((gx + 0.5f) * cellW, (gy + 0.5f) * cellH);
                switch (regions[gx, gy])
                {
                    case Region.Forest:
                        // 树林：稀疏成簇（每簇 1-2 棵）+ 3×3 间距
                        if (nSolids < MaxSolids && rng.Randf() < 0.25f && !SolidNear(gx, gy))
                        {
                            int n = rng.RandiRange(1, 2);
                            for (int i = 0; i < n && nSolids < MaxSolids; i++)
                            {
                                var pos = center + new Vector2(rng.RandfRange(-22, 22), rng.RandfRange(-16, 16));
                                if (m.InSpawnSafeZone(pos)) continue;
                                m.Obstacles.Add(new ObstacleDef(pos, new Vector2(44, 44),
                                    ObstacleKind.Solid, rng.RandfRange(0f, Mathf.Tau), p.ForestSolid));
                                nSolids++;
                            }
                            solidTaken[gx, gy] = true;
                        }
                        break;
                    case Region.Rock:
                        if (nSolids < MaxSolids && rng.Randf() < 0.30f && !SolidNear(gx, gy))
                        {
                            var pos = center + new Vector2(rng.RandfRange(-14, 14), rng.RandfRange(-10, 10));
                            if (m.InSpawnSafeZone(pos)) break;
                            m.Obstacles.Add(new ObstacleDef(pos,
                                new Vector2(rng.RandfRange(50, 90), rng.RandfRange(40, 70)),
                                ObstacleKind.Solid, rng.RandfRange(0f, Mathf.Tau), p.RockSolid));
                            solidTaken[gx, gy] = true;
                            nSolids++;
                        }
                        break;
                    case Region.Water:
                        // 水域：整片连通水塘只放 1 个 Low（flood-fill 求包围盒），拒绝"每格一塘"
                        if (!waterDone[gx, gy] && nWaters < MaxWaters)
                        {
                            var bounds = _FloodWater(regions, waterDone, gx, gy, cellW, cellH);
                            if (bounds.HasValue)
                            {
                                var b = bounds.Value;
                                if (!m.InSpawnSafeZone(b.GetCenter()))
                                {
                                    m.Obstacles.Add(new ObstacleDef(b.GetCenter(), b.Size,
                                        ObstacleKind.Low, 0f, p.LowVariant));
                                    nWaters++;
                                    // 大水塘边缘 1 个陷阱点缀
                                    if (nHazards < MaxHazards && b.Size.X > cellW * 1.5f && rng.Randf() < 0.5f)
                                    {
                                        var edge = b.GetCenter() + new Vector2(b.Size.X * 0.5f - 35, 0);
                                        if (!m.InSpawnSafeZone(edge))
                                        {
                                            m.Obstacles.Add(new ObstacleDef(edge, new Vector2(70, 70),
                                                ObstacleKind.Hazard, 0f, p.HazardVariant));
                                            nHazards++;
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    default:
                        // 空地：小概率短墙段（带缺口）+ 零散碎石/炸药桶
                        float roll = rng.Randf();
                        if (roll < 0.06f && nWalls < MaxWalls)
                        {
                            bool horiz = rng.Randf() < 0.5f;
                            float len = rng.RandfRange(220, 420);
                            int before = m.Obstacles.Count;
                            _WallWithGap(m, center, horiz, len, rng.RandfRange(110, 150), p.WallVariant);
                            if (m.Obstacles.Count > before) nWalls++;
                        }
                        else if (roll < 0.10f && p.ExplosiveDots)
                        {
                            if (!m.InSpawnSafeZone(center))
                                m.Obstacles.Add(new ObstacleDef(center, new Vector2(40, 50),
                                    ObstacleKind.Explosive, 0f, 0));
                        }
                        else if (roll < 0.16f && nDestructs < MaxDestructs)
                        {
                            if (!m.InSpawnSafeZone(center))
                            {
                                m.Obstacles.Add(new ObstacleDef(center,
                                    new Vector2(rng.RandfRange(40, 70), rng.RandfRange(30, 55)),
                                    ObstacleKind.Destructible, rng.RandfRange(0f, Mathf.Tau), 1));
                                nDestructs++;
                            }
                        }
                        break;
                }
            }
        }

        // ── 3. 地标（对角最远两点，导航记忆点）──
        _AddLandmarks(m, theme, rng);

        // ── 4. 区域 mask（地面 shader 用）──
        m.GroundMask = _BuildMask(regions);

        LogUtils.Print($"[MapGenerator] 生成地图 {m.Name}（{m.Obstacles.Count} 障碍，seed={seed}）");
        return m;
    }

    /// <summary>水域 flood-fill：连通水格合并为一个包围盒（4 邻接，上限 60 格防超大）。
    /// 返回世界坐标 Rect2，无水/全在安全区则返回 null。</summary>
    private static Rect2? _FloodWater(Region[,] regions, bool[,] done, int sx, int sy, float cellW, float cellH)
    {
        var stack = new System.Collections.Generic.Stack<(int x, int y)>();
        stack.Push((sx, sy));
        int minX = sx, maxX = sx, minY = sy, maxY = sy;
        int count = 0;
        while (stack.Count > 0 && count < 60)
        {
            var (x, y) = stack.Pop();
            if (x < 0 || y < 0 || x >= GridX || y >= GridY) continue;
            if (done[x, y] || regions[x, y] != Region.Water) continue;
            done[x, y] = true;
            count++;
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            stack.Push((x + 1, y)); stack.Push((x - 1, y));
            stack.Push((x, y + 1)); stack.Push((x, y - 1));
        }
        if (count == 0) return null;
        var pos = new Vector2(minX * cellW, minY * cellH);
        var size = new Vector2((maxX - minX + 1) * cellW, (maxY - minY + 1) * cellH);
        return new Rect2(pos, size);
    }

    private static void _WallWithGap(MapData m, Vector2 center, bool horizontal,
        float totalLen, float gapWidth, int variant)
    {
        float segLen = (totalLen - gapWidth) * 0.5f;
        Vector2 off = horizontal ? new Vector2(segLen * 0.5f + gapWidth * 0.5f, 0)
                                 : new Vector2(0, segLen * 0.5f + gapWidth * 0.5f);
        Vector2 sz = horizontal ? new Vector2(segLen, 26) : new Vector2(26, segLen);
        var a = center - off;
        var b = center + off;
        if (!m.InSpawnSafeZone(a))
            m.Obstacles.Add(new ObstacleDef(a, sz, ObstacleKind.Solid, 0f, variant));
        if (!m.InSpawnSafeZone(b))
            m.Obstacles.Add(new ObstacleDef(b, sz, ObstacleKind.Solid, 0f, variant));
    }

    private static void _AddLandmarks(MapData m, MapTheme theme, RandomNumberGenerator rng)
    {
        var ws = m.WorldSize;
        var spots = new[]
        {
            new Vector2(ws.X * rng.RandfRange(0.10f, 0.16f), ws.Y * rng.RandfRange(0.12f, 0.20f)),
            new Vector2(ws.X * rng.RandfRange(0.84f, 0.90f), ws.Y * rng.RandfRange(0.80f, 0.88f)),
        };
        int variant = theme switch
        {
            MapTheme.Graveyard => 6,
            MapTheme.Cathedral => 9,
            MapTheme.Desert => 6,
            MapTheme.Swamp => 8,
            _ => 7,
        };
        foreach (var s in spots)
        {
            if (m.InSpawnSafeZone(s)) continue;
            m.Obstacles.Add(new ObstacleDef(s, new Vector2(60, 130),
                ObstacleKind.Solid, rng.RandfRange(-0.1f, 0.1f), variant));
        }
    }

    private static Image _BuildMask(Region[,] regions)
    {
        var img = Image.CreateEmpty(MaskW, MaskH, false, Image.Format.Rgba8);
        for (int x = 0; x < MaskW; x++)
        {
            for (int y = 0; y < MaskH; y++)
            {
                int gx = Mathf.Clamp(x * GridX / MaskW, 0, GridX - 1);
                int gy = Mathf.Clamp(y * GridY / MaskH, 0, GridY - 1);
                Color c = regions[gx, gy] switch
                {
                    Region.Forest => new Color(1, 0, 0, 1),
                    Region.Rock => new Color(0, 1, 0, 1),
                    Region.Water => new Color(0, 0, 1, 1),
                    _ => new Color(0, 0, 0, 1),
                };
                img.SetPixel(x, y, c);
            }
        }
        return img;
    }

    private static string _ThemeName(MapTheme theme) => theme switch
    {
        MapTheme.Graveyard => "荒废墓地",
        MapTheme.Ruins => "破败废墟",
        MapTheme.Dungeon => "地下地牢",
        MapTheme.Swamp => "腐朽沼泽",
        MapTheme.Cathedral => "血色圣堂",
        MapTheme.Wasteland => "焦土荒原",
        MapTheme.Glacier => "寒冰墓穴",
        MapTheme.Volcano => "烈焰熔岩",
        _ => "黄沙遗迹",
    };

    private static void _ApplyThemeLook(MapData m, MapTheme theme)
    {
        var p = _ParamsFor(theme);
        m.GroundTint = new Color(1, 1, 1); // shader 接管色调，底图保持原色
        m.MaskForestTint = p.ForestTint;
        m.MaskRockTint = p.RockTint;
        m.MaskWaterTint = p.WaterTint;
        m.MaskStrength = p.MaskStrength;
        switch (theme)
        {
            case MapTheme.Graveyard:
                m.AtmosphereTint = new Color(0.45f, 0.55f, 0.72f); m.AtmosphereAlpha = 0.16f;
                m.FogTint = new Color(0.12f, 0.14f, 0.20f);
                m.AtmosphereTexturePath = "res://assets/sprites/backgrounds/atmosphere_blood_moon2.jpg";
                m.GroundTexturePath = "res://assets/sprites/backgrounds/ground_graveyard.jpg";
                break;
            case MapTheme.Ruins:
                m.AtmosphereTint = new Color(0.75f, 0.48f, 0.30f); m.AtmosphereAlpha = 0.16f;
                m.FogTint = new Color(0.18f, 0.14f, 0.10f);
                m.AtmosphereTexturePath = "res://assets/sprites/backgrounds/atmosphere_storm.jpg";
                m.GroundTexturePath = "res://assets/sprites/backgrounds/ground_ruins.jpg";
                break;
            case MapTheme.Dungeon:
                m.AtmosphereTint = new Color(0.35f, 0.30f, 0.55f); m.AtmosphereAlpha = 0.18f;
                m.FogTint = new Color(0.08f, 0.06f, 0.12f);
                m.AtmosphereTexturePath = "res://assets/sprites/backgrounds/atmosphere_storm2.jpg";
                m.GroundTexturePath = "res://assets/sprites/backgrounds/ground_dungeon.jpg";
                break;
            case MapTheme.Swamp:
                m.AtmosphereTint = new Color(0.30f, 0.55f, 0.30f); m.AtmosphereAlpha = 0.20f;
                m.FogTint = new Color(0.10f, 0.16f, 0.10f);
                m.AtmosphereTexturePath = "res://assets/sprites/backgrounds/atmosphere_swamp.jpg";
                m.GroundTexturePath = "res://assets/sprites/backgrounds/ground_graveyard.jpg"; // 苔藓石板正俯视，绿调贴合沼泽
                break;
            case MapTheme.Cathedral:
                m.AtmosphereTint = new Color(0.85f, 0.35f, 0.20f); m.AtmosphereAlpha = 0.18f;
                m.FogTint = new Color(0.20f, 0.10f, 0.08f);
                m.AtmosphereTexturePath = "res://assets/sprites/backgrounds/atmosphere_blood_moon.jpg";
                m.GroundTexturePath = "res://assets/sprites/backgrounds/ground_stone.jpg";
                break;
            case MapTheme.Wasteland:
                m.AtmosphereTint = new Color(0.90f, 0.45f, 0.15f); m.AtmosphereAlpha = 0.16f;
                m.FogTint = new Color(0.20f, 0.12f, 0.06f);
                m.AtmosphereTexturePath = "res://assets/sprites/backgrounds/atmosphere_storm.jpg";
                m.GroundTexturePath = "res://assets/sprites/backgrounds/ground_cemetery.jpg";
                break;
            case MapTheme.Glacier:
                m.AtmosphereTint = new Color(0.42f, 0.58f, 0.78f); m.AtmosphereAlpha = 0.18f;
                m.FogTint = new Color(0.10f, 0.14f, 0.22f);
                m.AtmosphereTexturePath = "res://assets/sprites/backgrounds/atmosphere_storm2.jpg";
                m.GroundTexturePath = "res://assets/sprites/backgrounds/ground_dungeon.jpg";
                break;
            case MapTheme.Volcano:
                m.AtmosphereTint = new Color(0.90f, 0.42f, 0.15f); m.AtmosphereAlpha = 0.20f;
                m.FogTint = new Color(0.16f, 0.08f, 0.05f);
                m.AtmosphereTexturePath = "res://assets/sprites/backgrounds/atmosphere_storm.jpg";
                m.GroundTexturePath = "res://assets/sprites/backgrounds/ground_cemetery.jpg";
                break;
            default:
                m.AtmosphereTint = new Color(0.90f, 0.76f, 0.42f); m.AtmosphereAlpha = 0.16f;
                m.FogTint = new Color(0.26f, 0.18f, 0.10f);
                m.AtmosphereTexturePath = "res://assets/sprites/backgrounds/atmosphere_storm.jpg";
                m.GroundTexturePath = "res://assets/sprites/backgrounds/ground_ruins.jpg";
                break;
        }
    }
}
