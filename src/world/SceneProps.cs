namespace ZombieWars.World;

using System;
using System.Collections.Generic;
using Godot;
using ZombieWars.Core;

/// <summary>
/// 程序化场景装饰物:用 _Draw 自绘的细节化物件(枯树/墓碑/栅栏/废墟墙/骨头堆/篝火/火把)。
/// 范火与火把带动态火焰、加色发光与火花粒子。零美术依赖,保持暗黑血色 Q 版风格。
/// </summary>
public abstract partial class SceneProp : Node2D
{
    protected static readonly Color ShadowCol = new(0.0f, 0.0f, 0.0f, 0.42f);

    protected float _Seed;

    public override void _Ready()
    {
        QueueRedraw();
    }

    // 椭圆地面投影
    protected void DrawGroundShadow(Vector2 center, float rx, float ry)
    {
        DrawSetTransform(new Vector2(center.X, center.Y), 0f, new Vector2(rx / 8f, ry / 4f));
        DrawCircle(Vector2.Zero, 8f, ShadowCol);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}

// ── 枯树 ──────────────────────────────────────────────
public sealed partial class DeadTreeProp : SceneProp
{
    private readonly float _height;
    private readonly float _lean;

    public DeadTreeProp(float seed)
    {
        _Seed = seed;
        var rng = new RandomNumberGenerator { Seed = (ulong)(seed * 1000) };
        _height = rng.RandfRange(64f, 92f);
        _lean = rng.RandfRange(-0.12f, 0.12f);
    }

    public override void _Draw()
    {
        DrawGroundShadow(new Vector2(0, 4), 22f, 8f);

        var trunkDark = new Color(0.10f, 0.07f, 0.05f);
        var trunkLit = new Color(0.16f, 0.11f, 0.07f);
        float w = 9f;

        // 树干:两段渐变柱
        DrawCol(new Vector2(-w * 0.5f, 0), new Vector2(-w * 0.5f + _lean * _height, -_height),
            w, trunkDark);
        DrawCol(new Vector2(-w * 0.15f, 0), new Vector2(-w * 0.15f + _lean * _height, -_height),
            w * 0.6f, trunkLit);

        // 枝杈
        var rng = new RandomNumberGenerator { Seed = (ulong)(_Seed * 1000 + 7) };
        float topX = _lean * _height;
        float topY = -_height;
        for (int i = 0; i < 5; i++)
        {
            float t = rng.RandfRange(0.45f, 0.95f);
            float bx = topX * (1 - t);
            float by = topY * t;
            float ang = rng.RandfRange(-1.4f, 1.4f);
            float blen = rng.RandfRange(16f, 34f);
            if (Math.Abs(ang) < 0.3f) ang += ang >= 0 ? 0.5f : -0.5f;
            float ex = bx + Mathf.Cos(ang - Mathf.Pi / 2f) * blen;
            float ey = by + Mathf.Sin(ang - Mathf.Pi / 2f) * blen;
            DrawLine(new Vector2(bx, by), new Vector2(ex, ey), trunkDark, 3f);
            DrawLine(new Vector2(bx, by), new Vector2(ex, ey), trunkLit, 1.2f);
        }
    }

    // 画带宽度的渐变柱(两色侧向过渡)
    private void DrawCol(Vector2 from, Vector2 to, float width, Color col)
    {
        Vector2 dir = (to - from).Normalized();
        Vector2 perp = new(-dir.Y, dir.X);
        Vector2 a = from + perp * (width * 0.5f);
        Vector2 b = from - perp * (width * 0.5f);
        Vector2 c = to - perp * (width * 0.5f);
        Vector2 d = to + perp * (width * 0.5f);
        DrawPolygon(new Vector2[] { a, b, c, d }, new Color[] { col, col, col, col });
    }
}

// ── 墓碑 ──────────────────────────────────────────────
public sealed partial class TombstoneProp : SceneProp
{
    private readonly float _h;
    private readonly float _rot;

    public TombstoneProp(float seed)
    {
        _Seed = seed;
        var rng = new RandomNumberGenerator { Seed = (ulong)(seed * 1000 + 13) };
        _h = rng.RandfRange(40f, 54f);
        _rot = rng.RandfRange(-0.22f, 0.22f);
    }

    public override void _Draw()
    {
        DrawGroundShadow(new Vector2(0, 2), 18f, 7f);
        DrawSetTransform(Vector2.Zero, _rot, Vector2.One);

        var stone = new Color(0.30f, 0.30f, 0.30f);
        var stoneLit = new Color(0.40f, 0.40f, 0.40f);
        var moss = new Color(0.12f, 0.18f, 0.10f);
        float w = 20f;

        // 主体圆顶矩形
        DrawRect(new Rect2(-w * 0.5f, -_h, w, _h), stone, true);
        DrawRect(new Rect2(-w * 0.5f + 2f, -_h + 2f, w * 0.4f, _h - 2f), stoneLit, true);
        DrawArc(new Vector2(0, -_h), w * 0.5f, Mathf.Pi, Mathf.Tau, 10, stone);
        DrawArc(new Vector2(0, -_h), w * 0.5f - 2f, Mathf.Pi, Mathf.Tau, 8, stoneLit);

        // 苔藓底座
        DrawRect(new Rect2(-w * 0.5f, -10f, w, 10f), moss, true);
        DrawCircle(new Vector2(-w * 0.3f, -6f), 3f, moss);
        DrawCircle(new Vector2(w * 0.25f, -4f), 2.5f, moss);

        // 十字刻痕
        DrawLine(new Vector2(0, -_h + 8f), new Vector2(0, -_h + 22f),
            stone.Darkened(0.4f), 1.5f);
        DrawLine(new Vector2(-4f, -_h + 13f), new Vector2(4f, -_h + 13f),
            stone.Darkened(0.4f), 1.5f);

        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}

// ── 木栅栏段 ──────────────────────────────────────────
public sealed partial class FenceProp : SceneProp
{
    private readonly float _len;

    public FenceProp(float seed)
    {
        _Seed = seed;
        var rng = new RandomNumberGenerator { Seed = (ulong)(seed * 1000 + 29) };
        _len = rng.RandfRange(64f, 104f);
    }

    public override void _Draw()
    {
        DrawGroundShadow(new Vector2(0, 3), _len * 0.5f, 7f);
        var wood = new Color(0.17f, 0.11f, 0.06f);
        var woodLit = new Color(0.24f, 0.15f, 0.08f);

        // 横杆
        DrawRect(new Rect2(-_len * 0.5f, -26f, _len, 4f), wood, true);
        DrawRect(new Rect2(-_len * 0.5f, -14f, _len, 4f), wood, true);
        DrawLine(new Vector2(-_len * 0.5f, -24f), new Vector2(_len * 0.5f, -24f), woodLit, 1f);
        DrawLine(new Vector2(-_len * 0.5f, -12f), new Vector2(_len * 0.5f, -12f), woodLit, 1f);

        // 立柱(含随机破损)
        var rng = new RandomNumberGenerator { Seed = (ulong)(_Seed * 1000 + 29) };
        int posts = Mathf.Max(2, (int)(_len / 26f));
        for (int i = 0; i <= posts; i++)
        {
            float x = -_len * 0.5f + i * (_len / posts);
            float ph = rng.Randf() < 0.25f ? rng.RandfRange(20f, 28f) : 36f;
            DrawRect(new Rect2(x - 3f, -ph, 6f, ph), wood, true);
            DrawRect(new Rect2(x - 1.5f, -ph, 1.5f, ph), woodLit, true);
            // 尖顶
            DrawPolygon(
                new Vector2[] { new(x - 3f, -ph), new(x + 3f, -ph), new(x, -ph - 4f) },
                new Color[] { wood, wood, wood });
        }
    }
}

// ── 废墟石墙 ──────────────────────────────────────────
public sealed partial class RuinWallProp : SceneProp
{
    private readonly float _len;

    public RuinWallProp(float seed)
    {
        _Seed = seed;
        var rng = new RandomNumberGenerator { Seed = (ulong)(seed * 1000 + 41) };
        _len = rng.RandfRange(80f, 120f);
    }

    public override void _Draw()
    {
        DrawGroundShadow(new Vector2(0, 3), _len * 0.5f, 9f);
        var rng = new RandomNumberGenerator { Seed = (ulong)(_Seed * 1000 + 41) };
        var stone = new Color(0.26f, 0.24f, 0.22f);
        var stoneDark = new Color(0.18f, 0.16f, 0.14f);
        var stoneLit = new Color(0.34f, 0.32f, 0.29f);

        // 堆叠石块(两排)
        int blocks = Mathf.Max(3, (int)(_len / 26f));
        for (int i = 0; i < blocks; i++)
        {
            float x = -_len * 0.5f + i * (_len / blocks);
            float bw = (_len / blocks) - 2f;
            float bh = rng.RandfRange(14f, 20f);
            float by = -bh - (i % 2) * 6f;
            DrawRect(new Rect2(x, by, bw, bh), stone, true);
            DrawRect(new Rect2(x, by, bw, 2f), stoneLit, true);
            DrawRect(new Rect2(x, by + bh - 2f, bw, 2f), stoneDark, true);
            // 破损缺口
            if (rng.Randf() < 0.3f)
                DrawRect(new Rect2(x + bw * 0.4f, by, bw * 0.3f, bh * 0.5f),
                    stoneDark.Darkened(0.3f), true);
        }
    }
}

// ── 骨头堆 ────────────────────────────────────────────
public sealed partial class BonePileProp : SceneProp
{
    public BonePileProp(float seed) { _Seed = seed; }

    public override void _Draw()
    {
        DrawGroundShadow(new Vector2(0, 2), 16f, 6f);
        var rng = new RandomNumberGenerator { Seed = (ulong)(_Seed * 1000 + 53) };
        var bone = new Color(0.62f, 0.58f, 0.50f);
        var boneShade = new Color(0.42f, 0.39f, 0.33f);

        // 散落骨头
        for (int i = 0; i < 5; i++)
        {
            float ang = rng.RandfRange(0, Mathf.Tau);
            float cx = rng.RandfRange(-10f, 10f);
            float cy = rng.RandfRange(-6f, -2f);
            float len = rng.RandfRange(10f, 18f);
            Vector2 a = new(cx, cy);
            Vector2 b = new(cx + Mathf.Cos(ang) * len, cy + Mathf.Sin(ang) * len);
            DrawLine(a, b, boneShade, 3.5f);
            DrawLine(a, b, bone, 1.6f);
            // 骨头两端膨大
            DrawCircle(a, 2f, bone);
            DrawCircle(b, 2f, bone);
        }

        // 头骨
        float sx = rng.RandfRange(-4f, 4f);
        DrawCircle(new Vector2(sx, -14f), 7f, bone);
        DrawCircle(new Vector2(sx, -14f), 7f, boneShade);
        DrawCircle(new Vector2(sx, -14f), 6f, bone);
        // 眼窝
        DrawCircle(new Vector2(sx - 2.4f, -15f), 1.6f, new Color(0.05f, 0.0f, 0.0f));
        DrawCircle(new Vector2(sx + 2.4f, -15f), 1.6f, new Color(0.05f, 0.0f, 0.0f));
    }
}

// ── 篝火 / 火把:动态火焰 + 加色发光 + 火花 ──────────
public abstract partial class FirePropBase : SceneProp
{
    protected GradientTexture2D? _glowTex;
    protected GpuParticles2D? _sparks;

    protected GradientTexture2D _MakeGlow(int size, Color inner, Color outer)
    {
        var grad = new Gradient();
        grad.SetColor(0, inner);
        grad.SetColor(1, outer);
        grad.SetOffset(0, 0f);
        grad.SetOffset(1, 1f);
        return new GradientTexture2D { Gradient = grad, Width = size, Height = size };
    }

    protected void _SetupSparks(Color color, int amount, float lifetime)
    {
        _sparks = new GpuParticles2D
        {
            Amount = amount,
            Lifetime = lifetime,
            Preprocess = 0.5f,
            Position = new Vector2(0, -22f)
        };
        var mat = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 4f,
            Direction = new Vector3(0, -1, 0),
            Spread = 18f,
            InitialVelocityMin = 10f,
            InitialVelocityMax = 26f,
            Gravity = new Vector3(0, 8, 0),
            ScaleMin = 0.6f,
            ScaleMax = 1.4f
        };
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(color.R, color.G, color.B, 0.0f));
        ramp.SetColor(1, new Color(color.R, color.G, color.B, 0.9f));
        ramp.SetOffset(0, 0f);
        ramp.SetOffset(1, 0.2f);
        var ramp2 = new Gradient();
        ramp2.SetColor(0, new Color(color.R, color.G, color.B, 0.9f));
        ramp2.SetColor(1, new Color(color.R * 0.4f, color.G * 0.2f, color.B * 0.1f, 0f));
        ramp2.SetOffset(0, 0.2f);
        ramp2.SetOffset(1, 1f);
        mat.ColorRamp = new GradientTexture1D { Gradient = ramp2 };
        _sparks.ProcessMaterial = mat;
        AddChild(_sparks);
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    // 绘制抖动火焰(多层椭圆)
    protected void DrawFlame(Vector2 base_, float height, float width, double t, float seed)
    {
        float flick = Mathf.Sin((float)t * 14f + seed * 6f) * 0.12f
                    + Mathf.Sin((float)t * 23f + seed * 3f) * 0.08f;
        float h = height * (1f + flick);
        float w = width * (1f - flick * 0.5f);
        // 外焰(暗红)
        DrawFlameOval(base_, h, w * 1.3f, new Color(0.7f, 0.18f, 0.04f, 0.85f));
        // 中焰(橙)
        DrawFlameOval(base_, h * 0.78f, w, new Color(1.0f, 0.5f, 0.10f, 0.9f));
        // 内焰(亮黄)
        DrawFlameOval(base_, h * 0.5f, w * 0.6f, new Color(1.0f, 0.85f, 0.35f, 0.95f));
    }

    private void DrawFlameOval(Vector2 base_, float h, float w, Color col)
    {
        // 用多段圆近似一个尖头火苗(底部宽、顶部尖)
        int seg = 6;
        for (int i = 0; i < seg; i++)
        {
            float t = i / (float)(seg - 1);
            float y = base_.Y - h * t;
            float r = w * (1f - t) * (1f - t * 0.4f);
            DrawCircle(new Vector2(base_.X, y), Mathf.Max(r, 0.5f), col);
        }
    }
}

// ── 篝火 ──────────────────────────────────────────────
public sealed partial class CampfireProp : FirePropBase
{
    public CampfireProp(float seed) { _Seed = seed; }

    public override void _Ready()
    {
        base._Ready();
        _glowTex = _MakeGlow(160, new Color(1.0f, 0.55f, 0.15f, 0.55f), Colors.Transparent);
        _SetupSparks(new Color(1.0f, 0.6f, 0.15f), 14, 1.2f);
        SetProcess(true);
    }

    public override void _Draw()
    {
        DrawGroundShadow(new Vector2(0, 4), 20f, 8f);
        var wood = new Color(0.13f, 0.08f, 0.04f);
        var woodLit = new Color(0.22f, 0.13f, 0.07f);

        // 交叉木柴
        DrawLine(new Vector2(-14f, 2f), new Vector2(14f, -2f), wood, 5f);
        DrawLine(new Vector2(14f, 2f), new Vector2(-14f, -2f), wood, 5f);
        DrawLine(new Vector2(-12f, 0f), new Vector2(12f, 0f), woodLit, 1.5f);

        // 发光底
        if (_glowTex != null)
        {
            DrawSetTransform(new Vector2(0, -10f), 0f, Vector2.One);
            DrawTextureRect(_glowTex, new Rect2(-80f, -80f, 160f, 160f), false);
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }

        // 动态火焰
        DrawFlame(new Vector2(0, -2f), 30f, 11f, Time.GetTicksMsec() / 1000.0, _Seed);
    }
}

// ── 火把 ──────────────────────────────────────────────
public sealed partial class TorchProp : FirePropBase
{
    public TorchProp(float seed) { _Seed = seed; }

    public override void _Ready()
    {
        base._Ready();
        _glowTex = _MakeGlow(110, new Color(1.0f, 0.5f, 0.12f, 0.5f), Colors.Transparent);
        _SetupSparks(new Color(1.0f, 0.55f, 0.12f), 8, 0.9f);
        SetProcess(true);
    }

    public override void _Draw()
    {
        DrawGroundShadow(new Vector2(0, 2), 9f, 4f);
        var wood = new Color(0.14f, 0.09f, 0.05f);
        // 火把杆
        DrawRect(new Rect2(-2f, -44f, 4f, 44f), wood, true);
        // 顶部缠布
        DrawRect(new Rect2(-3.5f, -48f, 7f, 6f), new Color(0.20f, 0.08f, 0.05f), true);

        if (_glowTex != null)
        {
            DrawSetTransform(new Vector2(0, -46f), 0f, Vector2.One);
            DrawTextureRect(_glowTex, new Rect2(-55f, -55f, 110f, 110f), false);
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }

        DrawFlame(new Vector2(0, -48f), 20f, 7f, Time.GetTicksMsec() / 1000.0, _Seed);
    }
}

/// <summary>在世界中分布装饰物,避开中心战斗区。</summary>
public static class ScenePropPlacer
{
    private const float CenterBanRadiusX = 0.32f;
    private const float CenterBanRadiusY = 0.30f;

    public static void Populate(Node2D parent, int seed = 777)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)seed };
        var ws = GameConfig.WorldSize;

        bool InBattleZone(Vector2 p)
        {
            float nx = (p.X / ws.X - 0.5f) / CenterBanRadiusX;
            float ny = (p.Y / ws.Y - 0.5f) / CenterBanRadiusY;
            return nx * nx + ny * ny < 1f;
        }

        Vector2 RandomEdgePos(float edgeBias)
        {
            // edgeBias 越大越偏向边缘
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float tx, ty;
                if (rng.Randf() < edgeBias)
                {
                    // 强制边缘带
                    tx = rng.Randf() < 0.5f
                        ? rng.RandfRange(0.02f, 0.16f)
                        : rng.RandfRange(0.84f, 0.98f);
                    ty = rng.RandfRange(0.08f, 0.92f);
                }
                else
                {
                    tx = rng.RandfRange(0.04f, 0.96f);
                    ty = rng.RandfRange(0.10f, 0.90f);
                }
                var p = new Vector2(ws.X * tx, ws.Y * ty);
                if (!InBattleZone(p))
                    return p;
            }
            return new Vector2(ws.X * rng.RandfRange(0.05f, 0.95f),
                               ws.Y * rng.RandfRange(0.12f, 0.88f));
        }

        var props = new List<SceneProp>();

        // 枯树:边缘带密集
        for (int i = 0; i < 14; i++)
        {
            var t = new DeadTreeProp(rng.RandiRange(1, 999999));
            t.Position = RandomEdgePos(0.7f);
            t.Rotation = rng.RandfRange(-0.08f, 0.08f);
            props.Add(t);
        }
        // 墓碑
        for (int i = 0; i < 10; i++)
        {
            var g = new TombstoneProp(rng.RandiRange(1, 999999));
            g.Position = RandomEdgePos(0.55f);
            props.Add(g);
        }
        // 骨头堆
        for (int i = 0; i < 8; i++)
        {
            var b = new BonePileProp(rng.RandiRange(1, 999999));
            b.Position = RandomEdgePos(0.4f);
            props.Add(b);
        }
        // 栅栏段:成对分布在边缘
        for (int i = 0; i < 5; i++)
        {
            var f = new FenceProp(rng.RandiRange(1, 999999));
            f.Position = RandomEdgePos(0.8f);
            f.Rotation = rng.RandfRange(-0.3f, 0.3f);
            props.Add(f);
        }
        // 废墟墙
        for (int i = 0; i < 4; i++)
        {
            var r = new RuinWallProp(rng.RandiRange(1, 999999));
            r.Position = RandomEdgePos(0.8f);
            r.Rotation = rng.RandfRange(-0.25f, 0.25f);
            props.Add(r);
        }
        // 篝火:少量,均匀
        for (int i = 0; i < 3; i++)
        {
            var c = new CampfireProp(rng.RandiRange(1, 999999));
            c.Position = RandomEdgePos(0.5f);
            props.Add(c);
        }
        // 火把:沿边缘
        for (int i = 0; i < 6; i++)
        {
            var tr = new TorchProp(rng.RandiRange(1, 999999));
            tr.Position = RandomEdgePos(0.85f);
            props.Add(tr);
        }

        foreach (var p in props)
            parent.AddChild(p);
    }
}
