namespace ZombieWars.World;

using System;
using System.Collections.Generic;
using Godot;
using ZombieWars.Core;
using ZombieWars.Enemy;
using ZombieWars.Render;
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
            ObstacleKind.Explosive => new ExplosiveBarrel(),
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

    // 其他 Draw* 函数已删除（被 .glb mesh 替代）—— 兜底时仅画阴影

}

// ── 固体障碍：StaticBody2D，完全阻挡 ─────────────────────
public sealed partial class SolidObstacle : StaticBody2D, IObstacleSetup
{
    // Solid 障碍 3D mesh 缓存（按 variant 0-9 索引）
    // 注：variant 0 砖石墙使用 4 共享（DrawStoneBlock 通用）
    private static readonly Dictionary<int, string> _solidMeshPathByVariant = new()
    {
        [0] = "res://assets/models/obstacles/solid_stone_block.glb",
        [1] = "res://assets/models/obstacles/solid_tombstone_row.glb",
        [2] = "res://assets/models/obstacles/solid_pillar.glb",
        [3] = "res://assets/models/obstacles/solid_pillar.glb",
        [4] = "res://assets/models/obstacles/solid_stone_block.glb",
        [5] = "res://assets/models/obstacles/solid_metal_bunker.glb",
        [6] = "res://assets/models/obstacles/solid_obelisk.glb",
        [7] = "res://assets/models/obstacles/solid_archway.glb",
        [8] = "res://assets/models/obstacles/solid_giant_tree.glb",
        [9] = "res://assets/models/obstacles/solid_tower.glb",
    };

    private Vector2 _size;
    private int _variant;
    private Color _baseCol;
    private Color _litCol;
    private Color _darkCol;
    private ModelProxy3D? _proxy;

    public void Setup(in ObstacleDef def)
    {
        _size = def.Size;
        _variant = def.Variant;
        Position = def.Position;
        Rotation = def.Rotation;
        CollisionLayer = GameConfig.CollisionLayerObstacleSolid;
        CollisionMask = 0;

        var col = new CollisionShape2D();
        // 碰撞体略大于视觉尺寸，防止角色视觉穿模
        col.Shape = new RectangleShape2D { Size = def.Size * 1.12f };
        AddChild(col);

        // 按变体选材质色
        (_baseCol, _litCol, _darkCol) = def.Variant switch
        {
            1 => (new Color(0.30f, 0.30f, 0.30f), new Color(0.40f, 0.40f, 0.40f), new Color(0.18f, 0.18f, 0.18f)), // 墓碑石
            2 => (new Color(0.20f, 0.14f, 0.08f), new Color(0.28f, 0.19f, 0.11f), new Color(0.12f, 0.08f, 0.04f)), // 枯树/木
            3 => (new Color(0.32f, 0.30f, 0.27f), new Color(0.42f, 0.40f, 0.36f), new Color(0.20f, 0.18f, 0.16f)), // 石柱
            4 => (new Color(0.28f, 0.26f, 0.24f), new Color(0.38f, 0.36f, 0.32f), new Color(0.16f, 0.14f, 0.12f)), // 城墙石
            5 => (new Color(0.22f, 0.22f, 0.24f), new Color(0.34f, 0.34f, 0.38f), new Color(0.12f, 0.12f, 0.14f)), // 金属掩体
            6 => (new Color(0.30f, 0.28f, 0.25f), new Color(0.42f, 0.39f, 0.34f), new Color(0.17f, 0.15f, 0.12f)), // 方尖碑（地标）
            7 => (new Color(0.31f, 0.28f, 0.24f), new Color(0.42f, 0.38f, 0.32f), new Color(0.18f, 0.15f, 0.11f)), // 拱门（地标）
            8 => (new Color(0.19f, 0.13f, 0.07f), new Color(0.27f, 0.18f, 0.10f), new Color(0.11f, 0.07f, 0.03f)), // 枯巨树（地标）
            9 => (new Color(0.33f, 0.31f, 0.28f), new Color(0.44f, 0.41f, 0.37f), new Color(0.20f, 0.18f, 0.15f)), // 钟楼（地标）
            _ => (new Color(0.28f, 0.26f, 0.24f), new Color(0.38f, 0.36f, 0.32f), new Color(0.16f, 0.14f, 0.12f))
        };
    }

    public override void _Ready()
    {
        // 主视觉:3D 代理(MeshInstance3D 原生渲染,材质正常显色)
        if (_solidMeshPathByVariant.TryGetValue(_variant, out var meshPath))
            _proxy = ModelProxy3D.CreateFor(this, meshPath, _size.X);
        // 兜底:模型缺失时走老 _Draw
        if (_proxy == null || !_proxy.HasModel) QueueRedraw();
    }

    public override void _Draw()
    {
        // 3D 模型由代理渲染;这里画阴影(2D overlay),模型缺失也画阴影兜底
        ObstacleDraw.DrawShadow(this, new Vector2(0, _size.Y * 0.4f), _size.X * 0.5f, 8f);
    }
}

// ── 可破坏障碍：StaticBody2D + IDamageable + 血量 ────────
public sealed partial class DestructibleObstacle : StaticBody2D, IDamageable, IObstacleSetup
{
    // Destructible 障碍 3D mesh（variant 2/3 = 木栅栏，其他 = 石堆）
    private static readonly string _woodFencePath = "res://assets/models/obstacles/destruct_wood_fence.glb";
    private static readonly string _stoneRubblePath = "res://assets/models/obstacles/destruct_stone_rubble.glb";

    private Vector2 _size;
    private int _variant;
    private int _maxHealth = GameConfig.DestructibleObstacleHealth;
    private int _health;
    private float _shakeTime;
    private float _biteTick; // 僵尸啃咬累计
    private Area2D? _biteArea; // 僵尸啃咬监测区（替代全局距离，避免跨障碍误伤）
    private Vector2 _drawShake;
    private ModelProxy3D? _proxy;

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
        _biteArea.BodyEntered += _OnBiteBodyEntered;
        _biteArea.BodyExited += _OnBiteBodyExited;
    }

    private int _biteCount;
    private void _OnBiteBodyEntered(Node body) => _biteCount++;
    private void _OnBiteBodyExited(Node body) => _biteCount = Math.Max(0, _biteCount - 1);

    public override void _Ready()
    {
        AddToGroup("destructibles");
        // 主视觉:3D 代理
        var meshPath = _variant is 2 or 3 ? _woodFencePath : _stoneRubblePath;
        _proxy = ModelProxy3D.CreateFor(this, meshPath, _size.X);
        if (_proxy == null || !_proxy.HasModel) QueueRedraw();
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

        // 僵尸啃咬：用信号维护的计数判断，避免每帧 GetOverlappingBodies 物理查询
        _biteTick += d;
        if (_biteTick >= 0.5f)
        {
            _biteTick = 0;
            if (_biteCount > 0)
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

        // 可破坏障碍有概率掉落拾取物——给"破坏环境"以探索奖励
        if (GD.Randf() < 0.30f)
        {
            string[] types = { "health_potion", "shield_orb", "magnet" };
            string type = types[(int)GD.RandRange(0, types.Length - 1)];
            Node? pickup = type switch
            {
                "health_potion" => new ZombieWars.Pickups.HealthPotion { GlobalPosition = GlobalPosition },
                "shield_orb" => new ZombieWars.Pickups.ShieldOrb { GlobalPosition = GlobalPosition },
                "magnet" => new ZombieWars.Pickups.MagnetPickup { GlobalPosition = GlobalPosition },
                _ => null
            };
            if (pickup != null)
                GetTree().CurrentScene.AddChild(pickup);
        }

        // 通知地图重建导航（移除该障碍后区域可通行）
        GetTree().CallGroup("map_builder", "RequestNavRebuild");
        QueueFree();
    }

    public override void _Draw()
    {
        DrawSetTransform(_drawShake, 0f, Vector2.One);
        // mesh 模式：只画阴影 + 受损裂纹 overlay
        if (_proxy != null && _proxy.HasModel)
        {
            ObstacleDraw.DrawShadow(this, new Vector2(0, _size.Y * 0.4f), _size.X * 0.5f, 7f);
            float dmgRatio = 1f - (float)_health / _maxHealth;
            if (dmgRatio > 0.1f)
            {
                var rng = new RandomNumberGenerator { Seed = (ulong)(_variant * 7777 + 11) };
                var crackCol = new Color(0.05f, 0.04f, 0.03f, dmgRatio);
                int crackCount = (int)(dmgRatio * 5) + 1;
                var rect = new Rect2(-_size * 0.5f, _size);
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
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
            return;
        }
        // 兜底：mesh 缺失时只画阴影（具体视觉由 mesh 提供）
        ObstacleDraw.DrawShadow(this, new Vector2(0, _size.Y * 0.4f), _size.X * 0.5f, 7f);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}

// ── 爆炸桶：可破坏战术物，破碎时范围爆炸 + 击退（实现 IDamageable）──
public sealed partial class ExplosiveBarrel : StaticBody2D, IDamageable, IObstacleSetup
{
    private static readonly string _barrelPath = "res://assets/models/obstacles/explosive_barrel.glb";

    private Vector2 _size;
    private readonly int _maxHealth = GameConfig.ExplosiveBarrelHealth;
    private int _health;
    private float _shakeTime;
    private float _biteTick;
    private int _biteCount;
    private Area2D? _biteArea;
    private Vector2 _drawShake;
    private ModelProxy3D? _proxy;

    int IDamageable.CurrentHealth => _health;
    int IDamageable.MaxHealth => _maxHealth;
    bool IDamageable.IsDead => _health <= 0;

    public void Setup(in ObstacleDef def)
    {
        _size = def.Size;
        _health = _maxHealth;
        Position = def.Position;
        Rotation = def.Rotation;
        CollisionLayer = GameConfig.CollisionLayerObstacleDestructible;
        CollisionMask = 0;

        var col = new CollisionShape2D();
        col.Shape = new RectangleShape2D { Size = def.Size };
        AddChild(col);

        // 僵尸啃咬监测区（僵尸靠近会啃咬引爆）
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
        _biteArea.BodyEntered += _OnBiteBodyEntered;
        _biteArea.BodyExited += _OnBiteBodyExited;
    }

    private void _OnBiteBodyEntered(Node body) => _biteCount++;
    private void _OnBiteBodyExited(Node body) => _biteCount = Math.Max(0, _biteCount - 1);

    public override void _Ready()
    {
        AddToGroup("destructibles");
        _proxy = ModelProxy3D.CreateFor(this, _barrelPath, _size.X);
        if (_proxy == null || !_proxy.HasModel) QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_health <= 0) return;
        float d = (float)delta;
        // 受击抖动衰减
        if (_shakeTime > 0)
        {
            _shakeTime -= d;
            _drawShake = new Vector2(
                (float)GD.RandRange(-1, 1) * GameConfig.DestructibleObstacleShakeOnHit,
                (float)GD.RandRange(-1, 1) * GameConfig.DestructibleObstacleShakeOnHit);
            if (_shakeTime <= 0) _drawShake = Vector2.Zero;
            QueueRedraw();
        }
        // 僵尸啃咬累计引爆
        _biteTick += d;
        if (_biteTick >= 0.5f)
        {
            _biteTick = 0;
            if (_biteCount > 0) TakeDamage(4);
        }
    }

    public void TakeDamage(int amount)
    {
        if (_health <= 0 || amount <= 0) return;
        _health -= amount;
        _shakeTime = 0.15f;
        QueueRedraw();
        if (_health <= 0) _Explode();
    }

    public void Heal(int amount) { /* 爆炸桶不治疗 */ }

    // 破碎引爆：范围伤害（僵尸+玩家+可破坏物，距离衰减）+ 击退 + VFX + 震屏
    private void _Explode()
    {
        var scene = GetTree().CurrentScene;
        float radius = GameConfig.ExplosiveBarrelRadius;

        // 爆炸 VFX + 音效 + 震屏
        ZombieWars.Vfx.VFXManager.SpawnParticleBurst(scene, GlobalPosition,
            new Color(1f, 0.6f, 0.1f), 30, 0.6f, 60f, 150f);
        ZombieWars.Vfx.VFXManager.SpawnImpactRing(scene, GlobalPosition, radius,
            new Color(1f, 0.5f, 0.1f), 3f);
        GameServices.UI?.ShakeScreen(0.8f);
        GameServices.Audio.PlayExplosion();

        // 范围伤害僵尸（距离衰减）+ 击退
        foreach (var z in Core.ZombieRegistry.All)
        {
            if (!GodotObject.IsInstanceValid(z)) continue;
            float dist = GlobalPosition.DistanceTo(z.GlobalPosition);
            if (dist > radius) continue;
            float falloff = 1f - (dist / radius) * 0.5f;
            int dmg = (int)(GameConfig.ExplosiveBarrelDamage * falloff);
            z.TakeDamage(dmg);
            Vector2 push = (z.GlobalPosition - GlobalPosition).Normalized();
            z.Velocity += push * GameConfig.ExplosiveBarrelKnockback;
        }

        // 范围伤害玩家（靠近时）
        var player = GameServices.Game?.PlayerRef;
        if (player != null && GodotObject.IsInstanceValid(player))
        {
            float dist = GlobalPosition.DistanceTo(player.GlobalPosition);
            if (dist <= radius)
            {
                float falloff = 1f - (dist / radius) * 0.5f;
                player.TakeDamage((int)(GameConfig.ExplosiveBarrelDamage * falloff));
            }
        }

        // 链式引爆/破坏范围内其他可破坏物（含其他爆炸桶）
        foreach (var n in GetTree().GetNodesInGroup("destructibles"))
        {
            if (n is not IDamageable d || ReferenceEquals(n, this)) continue;
            float dist = GlobalPosition.DistanceTo(((Node2D)n).GlobalPosition);
            if (dist <= radius)
                d.TakeDamage((int)(GameConfig.ExplosiveBarrelDamage * 0.6f));
        }

        // 通知地图重建导航（爆炸桶消失后区域可通行）
        GetTree().CallGroup("map_builder", "RequestNavRebuild");
        QueueFree();
    }

    public override void _Draw()
    {
        DrawSetTransform(_drawShake, 0f, Vector2.One);
        // 3D 模型由代理渲染;这里画阴影(2D overlay)
        ObstacleDraw.DrawShadow(this, new Vector2(0, _size.Y * 0.4f), _size.X * 0.5f, 7f);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}

// ── 低矮障碍：Area2D，可穿过但减速 ──────────────────────
public sealed partial class LowObstacle : Area2D, IObstacleSetup
{
    // Low 障碍 3D mesh（variant 1 = 水洼，其他 = 草丛）
    private static readonly string _waterPath = "res://assets/models/obstacles/low_water_puddle.glb";
    private static readonly string _grassPath = "res://assets/models/obstacles/low_grass.glb";

    private Vector2 _size;
    private int _variant;
    private ModelProxy3D? _proxy;

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
        // variant 1 = 水洼,其他 = 草丛
        var lowPath = _variant == 1 ? _waterPath : _grassPath;
        _proxy = ModelProxy3D.CreateFor(this, lowPath, _size.X);
        if (_proxy == null || !_proxy.HasModel) QueueRedraw();
    }

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
        if (_proxy != null && _proxy.HasModel) return;  // 静态视觉已由 3D 代理提供
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
    // Hazard 3D mesh（variant 1/3 = 毒沼，2/4 = 燃烧）
    private static readonly string _toxicPath = "res://assets/models/obstacles/hazard_toxic.glb";
    private static readonly string _firePath = "res://assets/models/obstacles/hazard_fire.glb";

    private Vector2 _size;
    private int _variant;
    private float _tickTimer;
    private float _redrawTimer;
    private ModelProxy3D? _proxy;

    // 脉冲陷阱（variant 3=脉冲毒 / 4=脉冲火）：周期激活/休眠，休眠期无害，逼玩家踩时机
    private bool _IsPulse => _variant is 3 or 4;
    private bool _PulseActive
    {
        get
        {
            if (!_IsPulse) return true;
            double t = Time.GetTicksMsec() / 1000.0;
            return (t % GameConfig.HazardPulsePeriod) < GameConfig.HazardPulsePeriod * GameConfig.HazardPulseActiveRatio;
        }
    }

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
        // variant 1/3 = 毒沼,2/4 = 燃烧
        var hazPath = _variant is 1 or 3 ? _toxicPath : _firePath;
        _proxy = ModelProxy3D.CreateFor(this, hazPath, _size.X);
        // 脉冲陷阱需要每帧 QueueRedraw 画光圈
        if (_IsPulse || _proxy == null || !_proxy.HasModel) QueueRedraw();
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

        // 脉冲陷阱：休眠期不减速不掉血，仅激活期生效
        bool active = _PulseActive;
        // 视觉:休眠期整体压暗,激活期恢复(3D 代理调色,提示玩家当前是否危险)
        var targetMod = active ? Colors.White : new Color(0.45f, 0.45f, 0.45f, 0.55f);
        if (_proxy != null && _proxy.Modulate != targetMod) _proxy.SetModulate(targetMod);
        if (!active) return;

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
                    GameServices.UI?.UpdateHazardHUD($"危险地带 -{dmgInt}/s");
            }
        }
    }

    private static readonly System.Collections.Generic.HashSet<GameEnums.ZombieType> _zombieFlyers = new()
    {
        GameEnums.ZombieType.Ship, GameEnums.ZombieType.Dragon
    };

    public override void _Draw()
    {
        // 脉冲陷阱：画光圈（提示危险范围）
        if (_IsPulse && _proxy != null && _proxy.HasModel)
        {
            var rect = new Rect2(-_size * 0.5f, _size);
            double t = Time.GetTicksMsec() / 1000.0;
            float pulse = 0.4f + 0.3f * Mathf.Sin((float)t * 4f);
            var c = _variant == 3 ? new Color(0.4f, 0.8f, 0.2f) : new Color(1.0f, 0.5f, 0.1f);
            // 外圈脉动警告线
            DrawArc(Vector2.Zero, _size.X * 0.5f, 0, Mathf.Tau, 24, new Color(c.R, c.G, c.B, pulse), 2f);
            return;
        }
        // 兜底：mesh 缺失时走老 _Draw 全路径
        var rect2 = new Rect2(-_size * 0.5f, _size);
        double t2 = Time.GetTicksMsec() / 1000.0;
        float pulse2 = 0.5f + 0.5f * Mathf.Sin((float)t2 * 4f);
        var rng = new RandomNumberGenerator { Seed = (ulong)(_size.X * 127.0 + _size.Y * 31.0 + _variant * 7L) };
        // 兜底仅保留（具体视觉由 mesh 提供）
        _ = rect2; _ = pulse2; _ = rng;
    }
}
