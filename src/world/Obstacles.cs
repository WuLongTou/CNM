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
        c.DrawSetTransform(new Vector2(center.X, center.Y), 0f, new Vector2(rx / 8f, ry / 4f));
        c.DrawCircle(Vector2.Zero, 8f, new Color(0f, 0f, 0f, 0.42f));
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
        ObstacleDraw.DrawStoneBlock(this, rect, _baseCol, _litCol, _darkCol, _variant, rng);
        // 描边强化轮廓
        DrawRect(rect, _darkCol.Darkened(0.3f), false, 1.5f);
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
        var biteArea = new Area2D
        {
            Monitoring = true,
            Monitorable = false,
            CollisionLayer = 0,
            CollisionMask = GameConfig.CollisionLayerZombie
        };
        var biteCol = new CollisionShape2D();
        biteCol.Shape = new RectangleShape2D { Size = def.Size * 1.1f };
        biteArea.AddChild(biteCol);
        AddChild(biteArea);
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

        // 僵尸啃咬：检测接触的僵尸，每秒掉血
        _biteTick += d;
        if (_biteTick >= 0.5f)
        {
            _biteTick = 0;
            foreach (var z in ZombieRegistry.All)
            {
                if (!IsInstanceValid(z)) continue;
                if (GlobalPosition.DistanceSquaredTo(z.GlobalPosition) < (_size.Length() * 0.5f + 24f) * (_size.Length() * 0.5f + 24f))
                {
                    TakeDamage(4);
                    break;
                }
            }
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

        ObstacleDraw.DrawStoneBlock(this, rect, baseCol, lit, dark, _variant, rng);

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
                float hx = rng.RandfRange(rect.Position.X * 0.6f, rect.End.X * 0.6f);
                float hy = rng.RandfRange(rect.Position.Y * 0.6f, rect.End.Y * 0.6f);
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
        foreach (var body in GetOverlappingBodies())
        {
            if (body is IDamageable d && !d.IsDead)
                d.TakeDamage((int)Mathf.Ceil(dmg));
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

        if (_variant == 1)
        {
            // 毒沼：绿色脉动
            DrawRect(rect, new Color(0.10f, 0.22f, 0.10f, 0.35f + pulse * 0.15f), true);
            DrawRect(rect, new Color(0.20f, 0.45f, 0.15f, 0.5f), false, 2f);
            // 气泡
            var rng = new RandomNumberGenerator { Seed = (ulong)(_size.X + _size.Y) };
            for (int i = 0; i < 6; i++)
            {
                float bx = rng.RandfRange(rect.Position.X * 0.7f, rect.End.X * 0.7f);
                float by = rng.RandfRange(rect.Position.Y * 0.7f, rect.End.Y * 0.7f);
                DrawCircle(new Vector2(bx, by), rng.RandfRange(1.5f, 3.5f),
                    new Color(0.30f, 0.55f, 0.20f, 0.35f + pulse * 0.2f));
            }
        }
        else
        {
            // 燃烧地带：橙红脉动
            DrawRect(rect, new Color(0.30f, 0.10f, 0.03f, 0.40f + pulse * 0.20f), true);
            DrawRect(rect, new Color(0.85f, 0.35f, 0.10f, 0.6f), false, 2f);
            var rng = new RandomNumberGenerator { Seed = (ulong)(_size.X * 2 + _variant) };
            for (int i = 0; i < 8; i++)
            {
                float fx = rng.RandfRange(rect.Position.X * 0.7f, rect.End.X * 0.7f);
                float fy = rng.RandfRange(rect.Position.Y * 0.7f, rect.End.Y * 0.7f);
                DrawCircle(new Vector2(fx, fy), rng.RandfRange(2f, 4f),
                    new Color(1.0f, 0.55f + pulse * 0.2f, 0.15f, 0.5f + pulse * 0.2f));
            }
        }
    }
}
