namespace ZombieWars.World;

using System;
using Godot;
using ZombieWars.Core;
using ZombieWars.Enemy;
using PlayerClass = ZombieWars.Player.Player;

/// <summary>
/// 地图障碍物节点集合 —— 四类真实环境交互：
/// SolidObstacle（固体墙/掩体，完全阻挡）
/// DestructibleObstacle（可破坏，受击/啃咬破碎，实现 IDamageable）
/// LowObstacle（低矮装饰，减速但不阻挡）
/// HazardZone（陷阱，持续掉血 + 减速）
/// 所有障碍用 _Draw 自绘，保持暗黑血色风格；碰撞靠 CollisionShape2D。
/// </summary>
public static class ObstacleFactory
{
    public static Node2D Create(in ObstacleDef def)
    {
        Node2D node = def.Kind switch
        {
            ObstacleKind.Solid => new SolidObstacle(),
            ObstacleKind.Destructible => new DestructibleObstacle(),
            ObstacleKind.Low => new LowObstacle(),
            ObstacleKind.Hazard => new HazardZone(),
            _ => new SolidObstacle()
        };
        ((IObstacleSetup)node).Setup(def);
        return node;
    }
}

internal interface IObstacleSetup
{
    void Setup(in ObstacleDef def);
}

// ══ 共用绘制辅助 ════════════════════════════════════════
internal static class ObstacleDraw
{
    public static void DrawShadow(CanvasItem c, Vector2 center, float rx, float ry)
    {
        // 光从左上，阴影投右下：中心右偏 + X 拉长，模拟低角度斜光长投影
        var pos = new Vector2(center.X + rx * 0.35f, center.Y);
        c.DrawSetTransform(pos, 0f, new Vector2(rx / 8f * 1.25f, ry / 4f));
        c.DrawCircle(Vector2.Zero, 8f, new Color(0f, 0f, 0f, 0.40f));
        c.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    // 绘制带轻微纹理变化的矩形石块
    public static void DrawStoneBlock(CanvasItem c, Rect2 rect, Color baseCol, Color lit, Color dark,
        int variant, RandomNumberGenerator rng)
    {
        c.DrawRect(rect, baseCol, true);
        // 顶面高光、底面阴影
        c.DrawRect(new Rect2(rect.Position.X, rect.Position.Y, rect.Size.X, 3f), lit, true);
        c.DrawRect(new Rect2(rect.Position.X, rect.End.Y - 3f, rect.Size.X, 3f), dark, true);
        // 砖块分隔线
        int blocks = Math.Max(2, (int)(rect.Size.X / 26f));
        for (int i = 1; i < blocks; i++)
        {
            float x = rect.Position.X + i * (rect.Size.X / blocks);
            c.DrawLine(new Vector2(x, rect.Position.Y), new Vector2(x, rect.End.Y), dark, 1.5f);
        }
        // 偶尔破损缺口
        if (variant % 2 == 0)
        {
            var notch = new Rect2(
                rect.Position.X + rect.Size.X * 0.3f,
                rect.Position.Y + rect.Size.Y * 0.2f,
                rect.Size.X * 0.18f, rect.Size.Y * 0.5f);
            c.DrawRect(notch, dark.Darkened(0.3f), true);
        }
    }

    // ══ 各 variant 专用绘制：让每种障碍一眼可辨 ══

    // 墓碑排（Solid variant 1）：扁矩形内并排圆顶墓碑 + 苔藓底座
    public static void DrawTombstoneRow(CanvasItem c, Rect2 rect, Color stone, Color lit, Color dark,
        RandomNumberGenerator rng)
    {
        var moss = new Color(0.13f, 0.19f, 0.11f);
        // 地面苔藓底座
        c.DrawRect(new Rect2(rect.Position.X, rect.End.Y - 5f, rect.Size.X, 5f), moss, true);
        c.DrawCircle(new Vector2(rect.Position.X + 4f, rect.End.Y - 3f), 3f, moss);
        c.DrawCircle(new Vector2(rect.End.X - 4f, rect.End.Y - 3f), 3f, moss);

        int count = Mathf.Max(2, Mathf.RoundToInt(rect.Size.X / 32f));
        float slot = rect.Size.X / count;
        float stoneW = Mathf.Min(slot * 0.72f, 22f);
        float stoneH = Mathf.Min(rect.Size.Y - 6f, 22f);
        for (int i = 0; i < count; i++)
        {
            float cx = rect.Position.X + slot * (i + 0.5f);
            float by = rect.End.Y - 5f;
            float topY = by - stoneH;
            // 碑体 + 圆顶（圆心在碑顶，上半圆凸出形成墓碑轮廓）
            c.DrawRect(new Rect2(cx - stoneW * 0.5f, topY, stoneW, stoneH), stone, true);
            c.DrawCircle(new Vector2(cx, topY), stoneW * 0.5f, stone);
            // 左侧高光
            c.DrawRect(new Rect2(cx - stoneW * 0.5f, topY, 2f, stoneH), lit, true);
            // 右侧暗面（圆柱体积感）
            c.DrawRect(new Rect2(cx + stoneW * 0.5f - 2.5f, topY, 2.5f, stoneH), dark.Darkened(0.15f), true);
            // 圆顶轮廓
            c.DrawArc(new Vector2(cx, topY), stoneW * 0.5f, Mathf.Pi, Mathf.Tau, 8, dark);
            c.DrawRect(new Rect2(cx - stoneW * 0.5f, topY, stoneW, stoneH), dark, false, 1f);
            // 十字刻痕
            c.DrawLine(new Vector2(cx, topY + 4f), new Vector2(cx, topY + 13f), dark.Darkened(0.3f), 1.2f);
            c.DrawLine(new Vector2(cx - 3f, topY + 7.5f), new Vector2(cx + 3f, topY + 7.5f),
                dark.Darkened(0.3f), 1.2f);
            // 随机残缺（被砸掉一角）
            if (rng.Randf() < 0.35f)
            {
                float nh = stoneH * rng.RandfRange(0.3f, 0.55f);
                c.DrawRect(new Rect2(cx - stoneW * 0.5f, topY, stoneW, nh), dark.Darkened(0.4f), true);
            }
        }
    }

    // 枯树（Solid variant 2）：树干 + 向上分叉的枯枝
    public static void DrawDeadTree(CanvasItem c, Rect2 rect, Color wood, Color woodLit, Color dark,
        RandomNumberGenerator rng)
    {
        float groundY = rect.End.Y;
        float trunkH = rect.Size.Y * 0.6f;
        float topY = groundY - trunkH;
        float trunkW = Mathf.Min(rect.Size.X * 0.3f, 11f);
        // 树干主体 + 左侧高光 + 右侧暗面
        c.DrawRect(new Rect2(-trunkW * 0.5f, topY, trunkW, trunkH), wood, true);
        c.DrawRect(new Rect2(-trunkW * 0.5f, topY, trunkW * 0.32f, trunkH), woodLit, true);
        c.DrawRect(new Rect2(trunkW * 0.5f - trunkW * 0.3f, topY, trunkW * 0.3f, trunkH), dark, true);
        // 根部加宽底座
        c.DrawRect(new Rect2(-trunkW * 0.75f, groundY - 5f, trunkW * 1.5f, 5f), dark, true);
        // 枯枝（从树干上段两侧伸出，向上倾斜）
        for (int i = 0; i < 6; i++)
        {
            float t = rng.RandfRange(0.4f, 0.92f);
            float by = groundY - trunkH * t;
            float side = rng.Randf() < 0.5f ? -1f : 1f;
            float len = rng.RandfRange(rect.Size.X * 0.24f, rect.Size.X * 0.46f);
            float ang = rng.RandfRange(0.5f, 1.25f);
            float ex = side * Mathf.Cos(ang) * len;
            float ey = by - Mathf.Sin(ang) * len;
            c.DrawLine(new Vector2(0f, by), new Vector2(ex, ey), wood, 3f);
            c.DrawLine(new Vector2(0f, by), new Vector2(ex, ey), woodLit, 1.1f);
            // 末梢小分叉
            if (rng.Randf() < 0.55f)
            {
                float mx = ex * 0.6f;
                float my = by - Mathf.Sin(ang) * len * 0.6f;
                float s2 = Mathf.Sign(ex);
                c.DrawLine(new Vector2(mx, my),
                    new Vector2(mx + s2 * len * 0.35f, my - len * 0.3f), dark, 1.5f);
            }
        }
    }

    // 古典石柱（Solid variant 3）：柱头 + 凹槽柱身 + 柱础
    public static void DrawPillar(CanvasItem c, Rect2 rect, Color stone, Color lit, Color dark,
        RandomNumberGenerator rng)
    {
        float capW = rect.Size.X;
        float shaftW = rect.Size.X * 0.66f;
        float capH = Mathf.Min(rect.Size.Y * 0.16f, 7f);
        float yTop = rect.Position.Y;
        float yBot = rect.End.Y;
        // 柱头
        c.DrawRect(new Rect2(-capW * 0.5f, yTop, capW, capH), stone, true);
        c.DrawRect(new Rect2(-capW * 0.5f, yTop, capW, 2f), lit, true);
        // 柱础
        c.DrawRect(new Rect2(-capW * 0.5f, yBot - capH, capW, capH), stone, true);
        c.DrawRect(new Rect2(-capW * 0.5f, yBot - 2f, capW, 2f), dark, true);
        // 柱身
        float shaftTop = yTop + capH;
        float shaftBot = yBot - capH;
        c.DrawRect(new Rect2(-shaftW * 0.5f, shaftTop, shaftW, shaftBot - shaftTop), stone, true);
        // 左侧高光
        c.DrawRect(new Rect2(-shaftW * 0.5f, shaftTop, shaftW * 0.22f, shaftBot - shaftTop), lit, true);
        // 右侧暗面（圆柱体积感）
        c.DrawRect(new Rect2(shaftW * 0.5f - shaftW * 0.2f, shaftTop, shaftW * 0.2f, shaftBot - shaftTop), dark, true);
        // 竖向凹槽
        int flutes = 3;
        for (int i = 1; i < flutes; i++)
        {
            float x = -shaftW * 0.5f + i * (shaftW / flutes);
            c.DrawLine(new Vector2(x, shaftTop + 1f), new Vector2(x, shaftBot - 1f), dark, 1.2f);
        }
        // 描边
        c.DrawRect(new Rect2(-capW * 0.5f, yTop, capW, capH), dark, false, 1f);
        c.DrawRect(new Rect2(-shaftW * 0.5f, shaftTop, shaftW, shaftBot - shaftTop), dark, false, 1f);
        c.DrawRect(new Rect2(-capW * 0.5f, yBot - capH, capW, capH), dark, false, 1f);
    }

    // 金属掩体（Solid variant 5）：金属板 + 警告条 + 铆钉 + 反光带
    public static void DrawMetalBunker(CanvasItem c, Rect2 rect, Color metal, Color lit, Color dark,
        RandomNumberGenerator rng)
    {
        c.DrawRect(rect, metal, true);
        // 顶高光 / 底阴影
        c.DrawRect(new Rect2(rect.Position.X, rect.Position.Y, rect.Size.X, 2.5f), lit, true);
        c.DrawRect(new Rect2(rect.Position.X, rect.End.Y - 2.5f, rect.Size.X, 2.5f), dark, true);
        // 斜向金属反光带
        c.DrawLine(new Vector2(rect.Position.X, rect.End.Y), new Vector2(rect.End.X, rect.Position.Y),
            lit.Lightened(0.1f), 2f);
        // 警告色横条
        var warn = new Color(0.82f, 0.60f, 0.10f);
        float midY = rect.Position.Y + rect.Size.Y * 0.5f - 1.5f;
        c.DrawRect(new Rect2(rect.Position.X, midY, rect.Size.X, 3f), warn, true);
        // 四角铆钉
        var rivet = new Color(0.5f, 0.5f, 0.54f);
        float rx = rect.Size.X * 0.5f - 5f;
        float ry = rect.Size.Y * 0.5f - 4f;
        c.DrawCircle(new Vector2(-rx, -ry), 2f, rivet);
        c.DrawCircle(new Vector2(rx, -ry), 2f, rivet);
        c.DrawCircle(new Vector2(-rx, ry), 2f, rivet);
        c.DrawCircle(new Vector2(rx, ry), 2f, rivet);
        // 描边
        c.DrawRect(rect, dark, false, 1.5f);
    }

    // 木栅栏/木板（Destructible 木）：竖板缝 + 木纹 + 铁钉
    public static void DrawWoodFence(CanvasItem c, Rect2 rect, Color wood, Color lit, Color dark,
        RandomNumberGenerator rng)
    {
        c.DrawRect(rect, wood, true);
        c.DrawRect(new Rect2(rect.Position.X, rect.Position.Y, rect.Size.X, 2f), lit, true);
        // 底部暗面（接地阴影面）
        c.DrawRect(new Rect2(rect.Position.X, rect.End.Y - 2f, rect.Size.X, 2f), dark, true);
        // 竖向板缝
        int boards = Mathf.Max(2, Mathf.RoundToInt(rect.Size.X / 18f));
        for (int i = 1; i < boards; i++)
        {
            float x = rect.Position.X + i * (rect.Size.X / boards);
            c.DrawLine(new Vector2(x, rect.Position.Y), new Vector2(x, rect.End.Y), dark, 1.2f);
        }
        // 木纹（横向暗线）
        for (int i = 0; i < 2; i++)
        {
            float y = rect.Position.Y + rect.Size.Y * (0.35f + i * 0.3f);
            c.DrawLine(new Vector2(rect.Position.X + 2f, y), new Vector2(rect.End.X - 2f, y),
                dark.Darkened(0.2f), 0.8f);
        }
        // 四角铁钉
        var nail = dark.Lightened(0.25f);
        c.DrawCircle(new Vector2(rect.Position.X + 5f, rect.Position.Y + 3.5f), 1.5f, nail);
        c.DrawCircle(new Vector2(rect.End.X - 5f, rect.Position.Y + 3.5f), 1.5f, nail);
        c.DrawCircle(new Vector2(rect.Position.X + 5f, rect.End.Y - 3.5f), 1.5f, nail);
        c.DrawCircle(new Vector2(rect.End.X - 5f, rect.End.Y - 3.5f), 1.5f, nail);
    }

    // 粗凿石堆/石墙（Destructible 石）：错位石块 + 凿痕缺口
    public static void DrawStoneRubble(CanvasItem c, Rect2 rect, Color stone, Color lit, Color dark,
        RandomNumberGenerator rng)
    {
        int blocks = Mathf.Max(2, Mathf.RoundToInt(rect.Size.X / 26f));
        float bw = rect.Size.X / blocks;
        for (int i = 0; i < blocks; i++)
        {
            float x = rect.Position.X + i * bw;
            float yo = (i % 2) * (rect.Size.Y * 0.12f); // 上下错位
            var br = new Rect2(x + 1.5f, rect.Position.Y + yo, bw - 3f, rect.Size.Y - yo);
            c.DrawRect(br, stone, true);
            c.DrawRect(new Rect2(br.Position.X, br.Position.Y, br.Size.X, 2f), lit, true);
            c.DrawRect(new Rect2(br.Position.X, br.End.Y - 2f, br.Size.X, 2f), dark, true);
            // 随机凿痕缺口
            if (rng.Randf() < 0.4f)
            {
                var notch = new Rect2(br.Position.X + br.Size.X * 0.3f, br.Position.Y,
                    br.Size.X * 0.25f, br.Size.Y * 0.4f);
                c.DrawRect(notch, dark.Darkened(0.3f), true);
            }
        }
    }

    // ══ 陷阱专属绘制：让危险区一眼可辨 ══

    // 毒沼（Hazard variant 1）：暗绿泥浆 + 翻滚气泡 + 毒气 + 中心骷髅警示
    public static void DrawToxicSwamp(CanvasItem c, Rect2 rect, float pulse, RandomNumberGenerator rng)
    {
        var slimeDeep = new Color(0.10f, 0.24f, 0.09f, 0.72f);
        var slimeMid = new Color(0.16f, 0.38f, 0.13f, 0.68f);
        var edge = new Color(0.22f, 0.48f, 0.14f, 0.55f + pulse * 0.25f);

        // 底层泥浆：外暗内亮，制造沼泽下凹感
        c.DrawRect(rect, slimeDeep, true);
        c.DrawRect(rect.GrowIndividual(-4f, -3f, -4f, -3f), slimeMid, true);

        // 翻滚气泡：随脉动膨胀，带高光
        for (int i = 0; i < 6; i++)
        {
            float bx = rng.RandfRange(rect.Position.X, rect.End.X);
            float by = rng.RandfRange(rect.Position.Y, rect.End.Y);
            float br = rng.RandfRange(2f, 4.5f) * (0.65f + pulse * 0.6f);
            c.DrawCircle(new Vector2(bx, by), br, new Color(0.30f, 0.55f, 0.20f, 0.35f + pulse * 0.2f));
            c.DrawCircle(new Vector2(bx - br * 0.3f, by - br * 0.3f), br * 0.32f,
                new Color(0.55f, 0.85f, 0.35f, 0.5f));
        }

        // 毒气雾团：半透明飘浮
        for (int i = 0; i < 3; i++)
        {
            float mx = rng.RandfRange(rect.Position.X, rect.End.X);
            float my = rng.RandfRange(rect.Position.Y, rect.End.Y);
            c.DrawCircle(new Vector2(mx, my), rng.RandfRange(7f, 11f),
                new Color(0.40f, 0.62f, 0.24f, 0.10f + pulse * 0.08f));
        }

        c.DrawRect(rect, edge, false, 2f);

        // 中心骷髅警示：致命陷阱的通用标志
        DrawSkull(c, rect.GetCenter(), Mathf.Min(rect.Size.X, rect.Size.Y) * 0.18f, pulse);
    }

    // 燃烧地带（Hazard variant 2）：焦土 + 岩浆裂缝 + 动态火焰 + 余烬
    public static void DrawFireGround(CanvasItem c, Rect2 rect, float pulse, RandomNumberGenerator rng)
    {
        var scorched = new Color(0.28f, 0.09f, 0.04f, 0.72f);
        var ember = new Color(0.45f, 0.16f, 0.05f, 0.50f + pulse * 0.20f);

        c.DrawRect(rect, scorched, true);
        c.DrawRect(rect.GrowIndividual(-3f, -3f, -3f, -3f), ember, true);

        var lava = new Color(1.0f, 0.42f + pulse * 0.2f, 0.10f, 0.75f);
        var lavaCore = new Color(1.0f, 0.82f, 0.30f, 0.6f + pulse * 0.3f);
        // 中心十字岩浆裂缝（脉动明暗）
        var mid = rect.GetCenter();
        c.DrawLine(new Vector2(rect.Position.X, mid.Y), new Vector2(rect.End.X, mid.Y), lava, 3f + pulse * 1.5f);
        c.DrawLine(new Vector2(mid.X, rect.Position.Y), new Vector2(mid.X, rect.End.Y), lava, 2.5f + pulse);
        c.DrawLine(new Vector2(rect.Position.X, mid.Y), new Vector2(rect.End.X, mid.Y), lavaCore, 1f);

        // 火焰：3 个火苗跳动
        for (int i = 0; i < 3; i++)
        {
            float fx = rng.RandfRange(rect.Position.X, rect.End.X);
            float fy = rect.End.Y - rng.RandfRange(2f, 8f);
            float fh = rng.RandfRange(rect.Size.Y * 0.4f, rect.Size.Y * 0.7f) * (0.7f + pulse * 0.5f);
            float fw = rng.RandfRange(5f, 8f);
            DrawFlame(c, new Vector2(fx, fy), fw, fh);
        }

        // 余烬亮点
        for (int i = 0; i < 5; i++)
        {
            float ex = rng.RandfRange(rect.Position.X, rect.End.X);
            float ey = rng.RandfRange(rect.Position.Y, rect.End.Y);
            c.DrawCircle(new Vector2(ex, ey), rng.RandfRange(0.8f, 1.8f),
                new Color(1.0f, 0.6f, 0.2f, 0.6f + pulse * 0.3f));
        }

        c.DrawRect(rect, new Color(0.85f, 0.35f, 0.10f, 0.6f), false, 2f);
    }

    // 骷髅头（致命警示符号）
    private static void DrawSkull(CanvasItem c, Vector2 center, float r, float pulse)
    {
        var bone = new Color(0.92f, 0.90f, 0.80f, 0.70f + pulse * 0.2f);
        var dark = new Color(0.04f, 0.04f, 0.04f, 0.85f);
        // 颅顶 + 下颌
        c.DrawCircle(center, r, bone);
        c.DrawCircle(center + new Vector2(0, r * 0.5f), r * 0.78f, bone);
        // 眼眶（凹陷黑）
        c.DrawCircle(center + new Vector2(-r * 0.4f, -r * 0.05f), r * 0.28f, dark);
        c.DrawCircle(center + new Vector2(r * 0.4f, -r * 0.05f), r * 0.28f, dark);
        // 鼻孔
        c.DrawCircle(center + new Vector2(0, r * 0.28f), r * 0.12f, dark);
        // 牙齿
        for (int i = -2; i <= 2; i++)
            c.DrawLine(center + new Vector2(i * r * 0.18f, r * 0.5f),
                       center + new Vector2(i * r * 0.18f, r * 0.78f), dark, 1.2f);
    }

    // 火苗：外暗红、内亮黄双层多边形
    private static void DrawFlame(CanvasItem c, Vector2 basePt, float w, float h)
    {
        var outer = new Color(1.0f, 0.35f, 0.10f, 0.75f);
        var inner = new Color(1.0f, 0.85f, 0.30f, 0.85f);
        var outerPts = new Vector2[] {
            basePt + new Vector2(-w, 0),
            basePt + new Vector2(-w * 0.6f, -h * 0.5f),
            basePt + new Vector2(-w * 0.2f, -h * 0.8f),
            basePt + new Vector2(0, -h),
            basePt + new Vector2(w * 0.3f, -h * 0.7f),
            basePt + new Vector2(w * 0.7f, -h * 0.4f),
            basePt + new Vector2(w, 0),
        };
        c.DrawColoredPolygon(outerPts, outer);
        var innerPts = new Vector2[] {
            basePt + new Vector2(-w * 0.5f, 0),
            basePt + new Vector2(-w * 0.2f, -h * 0.4f),
            basePt + new Vector2(0, -h * 0.72f),
            basePt + new Vector2(w * 0.25f, -h * 0.4f),
            basePt + new Vector2(w * 0.5f, 0),
        };
        c.DrawColoredPolygon(innerPts, inner);
    }
}

// ── 固体障碍：StaticBody2D，完全阻挡 ─────────────────────
public sealed partial class SolidObstacle : StaticBody2D, IObstacleSetup
{
    private Vector2 _size;
    private int _variant;
    private Color _baseCol;
    private Color _litCol;
    private Color _darkCol;

    public void Setup(in ObstacleDef def)
    {
        _size = def.Size;
        _variant = def.Variant;
        Position = def.Position;
        Rotation = def.Rotation;
        CollisionLayer = GameConfig.CollisionLayerObstacleSolid;
        CollisionMask = 0;

        var col = new CollisionShape2D();
        col.Shape = new RectangleShape2D { Size = def.Size };
        AddChild(col);

        // 按变体选材质色
        (_baseCol, _litCol, _darkCol) = def.Variant switch
        {
            1 => (new Color(0.30f, 0.30f, 0.30f), new Color(0.40f, 0.40f, 0.40f), new Color(0.18f, 0.18f, 0.18f)), // 墓碑石
            2 => (new Color(0.20f, 0.14f, 0.08f), new Color(0.28f, 0.19f, 0.11f), new Color(0.12f, 0.08f, 0.04f)), // 枯树/木
            3 => (new Color(0.32f, 0.30f, 0.27f), new Color(0.42f, 0.40f, 0.36f), new Color(0.20f, 0.18f, 0.16f)), // 石柱
            4 => (new Color(0.28f, 0.26f, 0.24f), new Color(0.38f, 0.36f, 0.32f), new Color(0.16f, 0.14f, 0.12f)), // 城墙石
            5 => (new Color(0.22f, 0.22f, 0.24f), new Color(0.34f, 0.34f, 0.38f), new Color(0.12f, 0.12f, 0.14f)), // 金属掩体
            _ => (new Color(0.28f, 0.26f, 0.24f), new Color(0.38f, 0.36f, 0.32f), new Color(0.16f, 0.14f, 0.12f))
        };
    }

    public override void _Ready() => QueueRedraw();

    public override void _Draw()
    {
        var rect = new Rect2(-_size * 0.5f, _size);
        ObstacleDraw.DrawShadow(this, new Vector2(0, _size.Y * 0.4f), _size.X * 0.5f, 8f);
        var rng = new RandomNumberGenerator { Seed = (ulong)(_variant * 9999 + 7) };
        switch (_variant)
        {
            case 1: // 墓碑排
                ObstacleDraw.DrawTombstoneRow(this, rect, _baseCol, _litCol, _darkCol, rng);
                break;
            case 2: // 枯树
                ObstacleDraw.DrawDeadTree(this, rect, _baseCol, _litCol, _darkCol, rng);
                break;
            case 3: // 石柱
                ObstacleDraw.DrawPillar(this, rect, _baseCol, _litCol, _darkCol, rng);
                break;
            case 5: // 金属掩体
                ObstacleDraw.DrawMetalBunker(this, rect, _baseCol, _litCol, _darkCol, rng);
                break;
            default: // variant 4 / 其他：砖石墙
                ObstacleDraw.DrawStoneBlock(this, rect, _baseCol, _litCol, _darkCol, _variant, rng);
                DrawRect(rect, _darkCol.Darkened(0.3f), false, 1.5f);
                break;
        }
    }
}

// ── 可破坏障碍：StaticBody2D + IDamageable + 血量 ────────
public sealed partial class DestructibleObstacle : StaticBody2D, IDamageable, IObstacleSetup
{
    private Vector2 _size;
    private int _variant;
    private int _maxHealth = GameConfig.DestructibleObstacleHealth;
    private int _health;
    private float _shakeTime;
    private float _biteTick; // 僵尸啃咬累计
    private Area2D? _biteArea; // 僵尸啃咬监测区（替代全局距离，避免跨障碍误伤）
    private Vector2 _drawShake;

    int IDamageable.CurrentHealth => _health;
    int IDamageable.MaxHealth => _maxHealth;
    bool IDamageable.IsDead => _health <= 0;

    public void Setup(in ObstacleDef def)
    {
        _size = def.Size;
        _variant = def.Variant;
        _health = _maxHealth;
        Position = def.Position;
        Rotation = def.Rotation;
        CollisionLayer = GameConfig.CollisionLayerObstacleDestructible;
        CollisionMask = 0;

        var col = new CollisionShape2D();
        col.Shape = new RectangleShape2D { Size = def.Size };
        AddChild(col);

        // 僵尸啃咬监测区（检测 layer 2 僵尸接触）
        _biteArea = new Area2D
        {
            Monitoring = true,
            Monitorable = false,
            CollisionLayer = 0,
            CollisionMask = GameConfig.CollisionLayerZombie
        };
        var biteCol = new CollisionShape2D();
        biteCol.Shape = new RectangleShape2D { Size = def.Size * 1.1f };
        _biteArea.AddChild(biteCol);
        AddChild(_biteArea);
    }

    public override void _Ready()
    {
        AddToGroup("destructibles");
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_health <= 0) return;
        float d = (float)delta;

        // 受击抖动衰减（视觉偏移，不改 Position）
        if (_shakeTime > 0)
        {
            _shakeTime -= d;
            _drawShake = new Vector2(
                (float)GD.RandRange(-1, 1) * GameConfig.DestructibleObstacleShakeOnHit,
                (float)GD.RandRange(-1, 1) * GameConfig.DestructibleObstacleShakeOnHit);
            if (_shakeTime <= 0) _drawShake = Vector2.Zero;
            QueueRedraw();
        }

        // 僵尸啃咬：用 biteArea 重叠检测（替代全局距离，避免跨障碍误伤），每 0.5s 掉血
        _biteTick += d;
        if (_biteTick >= 0.5f)
        {
            _biteTick = 0;
            if (_biteArea != null && _biteArea.GetOverlappingBodies().Count > 0)
                TakeDamage(4);
        }
    }

    public void TakeDamage(int amount)
    {
        if (_health <= 0 || amount <= 0) return;
        _health -= amount;
        _shakeTime = 0.15f;
        QueueRedraw();
        if (_health <= 0) _Shatter();
    }

    public void Heal(int amount) { /* 可破坏障碍不治疗 */ }

    private void _Shatter()
    {
        // 碎片粒子
        var debris = new GpuParticles2D
        {
            Amount = 16,
            Lifetime = 0.6f,
            OneShot = true,
            Emitting = true,
            GlobalPosition = GlobalPosition
        };
        var mat = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(_size.X * 0.5f, _size.Y * 0.5f, 0),
            Direction = new Vector3(0, -1, 0),
            Spread = 60f,
            InitialVelocityMin = 40f,
            InitialVelocityMax = 120f,
            Gravity = new Vector3(0, 300, 0),
            ScaleMin = 1.5f,
            ScaleMax = 3.5f,
            Color = new Color(0.30f, 0.28f, 0.24f, 1f)
        };
        debris.ProcessMaterial = mat;
        GetParent().AddChild(debris);

        // 通知地图重建导航（移除该障碍后区域可通行）
        GetTree().CallGroup("map_builder", "RequestNavRebuild");
        QueueFree();
    }

    public override void _Draw()
    {
        DrawSetTransform(_drawShake, 0f, Vector2.One);
        var rect = new Rect2(-_size * 0.5f, _size);
        ObstacleDraw.DrawShadow(this, new Vector2(0, _size.Y * 0.4f), _size.X * 0.5f, 7f);
        var rng = new RandomNumberGenerator { Seed = (ulong)(_variant * 7777 + 11) };

        bool isWood = _variant is 2 or 3;
        var baseCol = isWood ? new Color(0.26f, 0.17f, 0.09f) : new Color(0.34f, 0.31f, 0.26f);
        var lit = isWood ? new Color(0.34f, 0.23f, 0.13f) : new Color(0.44f, 0.41f, 0.34f);
        var dark = isWood ? new Color(0.16f, 0.10f, 0.05f) : new Color(0.20f, 0.18f, 0.14f);

        if (isWood)
            ObstacleDraw.DrawWoodFence(this, rect, baseCol, lit, dark, rng);
        else
            ObstacleDraw.DrawStoneRubble(this, rect, baseCol, lit, dark, rng);

        // 按受损程度叠加裂纹
        float dmgRatio = 1f - (float)_health / _maxHealth;
        if (dmgRatio > 0.1f)
        {
            var crackCol = dark.Darkened(0.4f);
            int crackCount = (int)(dmgRatio * 5) + 1;
            for (int i = 0; i < crackCount; i++)
            {
                float cx = rng.RandfRange(rect.Position.X + 4f, rect.End.X - 4f);
                float cy = rng.RandfRange(rect.Position.Y + 4f, rect.End.Y - 4f);
                float ang = rng.RandfRange(0f, Mathf.Tau);
                float len = rng.RandfRange(6f, _size.X * 0.4f);
                DrawLine(new Vector2(cx, cy),
                    new Vector2(cx + Mathf.Cos(ang) * len, cy + Mathf.Sin(ang) * len),
                    crackCol, 1.5f);
            }
        }
        DrawRect(rect, dark, false, 1.2f);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}

// ── 低矮障碍：Area2D，可穿过但减速 ──────────────────────
public sealed partial class LowObstacle : Area2D, IObstacleSetup
{
    private Vector2 _size;
    private int _variant;

    public void Setup(in ObstacleDef def)
    {
        _size = def.Size;
        _variant = def.Variant;
        Position = def.Position;
        Rotation = def.Rotation;
        Monitoring = true;
        Monitorable = false;
        CollisionLayer = 0;
        CollisionMask = GameConfig.CollisionLayerPlayer | GameConfig.CollisionLayerZombie;

        var col = new CollisionShape2D();
        col.Shape = new RectangleShape2D { Size = def.Size };
        AddChild(col);
    }

    public override void _Ready() => QueueRedraw();

    public override void _PhysicsProcess(double delta)
    {
        // 每帧对重叠实体施加减速（实体自身每帧重置倍率，退出区域自然恢复）
        foreach (var body in GetOverlappingBodies())
        {
            if (body is PlayerClass p)
                p.ExternalSlowMult = Mathf.Min(p.ExternalSlowMult, GameConfig.LowObstacleSlowMult);
            else if (body is Zombie z && !_zombieFlyers.Contains(z.ZombieType))
                z.MapSlowMult = Mathf.Min(z.MapSlowMult, GameConfig.LowObstacleSlowMult);
        }
    }

    private static readonly System.Collections.Generic.HashSet<GameEnums.ZombieType> _zombieFlyers = new()
    {
        GameEnums.ZombieType.Ship, GameEnums.ZombieType.Dragon
    };

    public override void _Draw()
    {
        var rect = new Rect2(-_size * 0.5f, _size);
        var rng = new RandomNumberGenerator { Seed = (ulong)(_variant * 5555 + 3) };

        if (_variant == 1)
        {
            // 水洼：深色半透明椭圆斑
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
            DrawRect(rect, new Color(0.08f, 0.12f, 0.14f, 0.55f), true);
            DrawRect(rect, new Color(0.14f, 0.20f, 0.22f, 0.4f), false, 2f);
            // 反光高光
            for (int i = 0; i < 4; i++)
            {
                float hx = rng.RandfRange(rect.Position.X, rect.End.X);
                float hy = rng.RandfRange(rect.Position.Y, rect.End.Y);
                DrawCircle(new Vector2(hx, hy), rng.RandfRange(1.5f, 3f),
                    new Color(0.30f, 0.40f, 0.45f, 0.4f));
            }
        }
        else
        {
            // 草丛/碎石：暗绿褐色低矮斑
            DrawRect(rect, new Color(0.10f, 0.13f, 0.08f, 0.50f), true);
            for (int i = 0; i < 10; i++)
            {
                float gx = rng.RandfRange(rect.Position.X, rect.End.X);
                float gy = rng.RandfRange(rect.Position.Y, rect.End.Y);
                DrawLine(new Vector2(gx, gy), new Vector2(gx, gy - rng.RandfRange(4f, 9f)),
                    new Color(0.14f, 0.20f, 0.10f, 0.6f), 1.2f);
            }
        }
    }
}

// ── 陷阱/危险区域：Area2D，持续掉血 + 减速 ───────────────
public sealed partial class HazardZone : Area2D, IObstacleSetup
{
    private Vector2 _size;
    private int _variant;
    private float _tickTimer;
    private float _redrawTimer;

    public void Setup(in ObstacleDef def)
    {
        _size = def.Size;
        _variant = def.Variant;
        Position = def.Position;
        Rotation = def.Rotation;
        Monitoring = true;
        Monitorable = false;
        CollisionLayer = 0;
        CollisionMask = GameConfig.CollisionLayerPlayer | GameConfig.CollisionLayerZombie;

        var col = new CollisionShape2D();
        col.Shape = new RectangleShape2D { Size = def.Size };
        AddChild(col);
    }

    public override void _Ready()
    {
        QueueRedraw();
        SetProcess(true);
    }

    public override void _PhysicsProcess(double delta)
    {
        // 定期重绘让脉动动画生效（_Draw 内 pulse 依赖时间，需主动 QueueRedraw）
        _redrawTimer -= (float)delta;
        if (_redrawTimer <= 0)
        {
            _redrawTimer = 0.1f;
            QueueRedraw();
        }

        float slow = GameConfig.HazardSlowMult;
        foreach (var body in GetOverlappingBodies())
        {
            if (body is PlayerClass p)
                p.ExternalSlowMult = Mathf.Min(p.ExternalSlowMult, slow);
            else if (body is Zombie z && !_zombieFlyers.Contains(z.ZombieType))
                z.MapSlowMult = Mathf.Min(z.MapSlowMult, slow);
        }

        // 每 0.5 秒结算一次伤害
        _tickTimer -= (float)delta;
        if (_tickTimer > 0) return;
        _tickTimer = 0.5f;
        float dmg = GameConfig.HazardDamagePerSec * 0.5f;
        int dmgInt = (int)Mathf.Ceil(dmg);
        foreach (var body in GetOverlappingBodies())
        {
            if (body is IDamageable d && !d.IsDead)
            {
                d.TakeDamage(dmgInt);
                if (body is PlayerClass)
                    GameServices.UI?.UpdateHazardHUD($"⚠ 危险地带 -{dmgInt}/s");
            }
        }
    }

    private static readonly System.Collections.Generic.HashSet<GameEnums.ZombieType> _zombieFlyers = new()
    {
        GameEnums.ZombieType.Ship, GameEnums.ZombieType.Dragon
    };

    public override void _Draw()
    {
        var rect = new Rect2(-_size * 0.5f, _size);
        double t = Time.GetTicksMsec() / 1000.0;
        float pulse = 0.5f + 0.5f * Mathf.Sin((float)t * 4f);
        var rng = new RandomNumberGenerator { Seed = (ulong)(_size.X * 127.0 + _size.Y * 31.0 + _variant * 7L) };

        if (_variant == 1)
            ObstacleDraw.DrawToxicSwamp(this, rect, pulse, rng);
        else
            ObstacleDraw.DrawFireGround(this, rect, pulse, rng);
    }
}
