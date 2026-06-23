namespace ZombieWars.World;

using Godot;
using ZombieWars.Core;
using ZombieWars.Pickups;

/// <summary>资源类型 —— 决定产出材料与节点外观。</summary>
public enum ResourceType { Wood, Stone, Fiber, Berry }

/// <summary>
/// 可采集资源节点（饥荒式）：树/石头/灌木/浆果丛。
/// 实现 IDamageable，被玩家近战攻击时扣耐久，归零掉落资源 Pickup。
/// 不阻挡通行（阶段 1 简化），加入 "resource_nodes" group 供攻击查询。
/// </summary>
public sealed partial class ResourceNode : Node2D, IDamageable
{
    public ResourceType Type;
    private int _maxHealth = 3;
    private int _health;
    private int _yield = 2;
    private float _size;
    private int _variant;
    private float _shakeTime;
    private Vector2 _drawShake;

    int IDamageable.CurrentHealth => _health;
    int IDamageable.MaxHealth => _maxHealth;
    bool IDamageable.IsDead => _health <= 0;

    public void Setup(ResourceType type, Vector2 pos)
    {
        Type = type;
        GlobalPosition = pos;
        (_maxHealth, _yield, _size, _variant) = type switch
        {
            ResourceType.Wood => (GameConfig.ResourceNodeHealthTree, GameConfig.ResourceWoodPerTree, 60f, 1),
            ResourceType.Stone => (GameConfig.ResourceNodeHealthRock, GameConfig.ResourceStonePerRock, 56f, 2),
            ResourceType.Fiber => (GameConfig.ResourceNodeHealthBush, GameConfig.ResourceFiberPerBush, 44f, 3),
            ResourceType.Berry => (GameConfig.ResourceNodeHealthBush, GameConfig.ResourceBerryPerBush, 44f, 4),
            _ => (3, 2, 50f, 1),
        };
        _health = _maxHealth;
    }

    public override void _Ready()
    {
        AddToGroup("resource_nodes");
        AddToGroup("mm_resource"); // 小地图标记（后续扩展）
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_shakeTime > 0)
        {
            _shakeTime -= (float)delta;
            _drawShake = new Vector2((float)GD.RandRange(-1.5, 1.5), (float)GD.RandRange(-1.5, 1.5));
            if (_shakeTime <= 0) _drawShake = Vector2.Zero;
            QueueRedraw();
        }
    }

    public void TakeDamage(int amount)
    {
        if (_health <= 0 || amount <= 0) return;
        _health -= amount;
        _shakeTime = 0.12f;
        GameServices.UI?.ShakeScreen(GameConfig.HarvestShakeIntensity);
        QueueRedraw();
        if (_health <= 0) _Harvest();
    }

    public void Heal(int amount) { /* 资源节点不治疗 */ }

    private void _Harvest()
    {
        // 掉落资源 Pickup（散布几堆，便于拾取动画）
        for (int i = 0; i < _yield; i++)
        {
            var offset = new Vector2((float)GD.RandRange(-20, 20), (float)GD.RandRange(-20, 20));
            var pickup = new ResourcePickup { Type = Type, GlobalPosition = GlobalPosition + offset };
            GetTree().CurrentScene.AddChild(pickup);
        }
        // 碎屑粒子（树叶/碎石）
        var debris = new GpuParticles2D
        {
            GlobalPosition = GlobalPosition, Emitting = true, Amount = 12, Lifetime = 0.5f, OneShot = true
        };
        var col = Type == ResourceType.Stone ? new Color(0.5f, 0.48f, 0.45f)
            : Type == ResourceType.Wood ? new Color(0.35f, 0.24f, 0.12f)
            : new Color(0.3f, 0.5f, 0.25f);
        debris.ProcessMaterial = new ParticleProcessMaterial
        {
            Color = col, Direction = new Vector3(0, -1, 0), Spread = 50f,
            InitialVelocityMin = 40f, InitialVelocityMax = 100f, Gravity = new Vector3(0, 300, 0),
            ScaleMin = 1.5f, ScaleMax = 3f
        };
        GetTree().CurrentScene.AddChild(debris);
        QueueFree();
    }

    public override void _Draw()
    {
        DrawSetTransform(_drawShake, 0f, Vector2.One);
        float dmgRatio = 1f - (float)_health / _maxHealth;
        var rng = new RandomNumberGenerator { Seed = (ulong)((long)GlobalPosition.X * 7 + (long)GlobalPosition.Y * 13 + (int)Type) };
        switch (_variant)
        {
            case 1: _DrawTree(rng, dmgRatio); break;
            case 2: _DrawRock(rng, dmgRatio); break;
            case 3: _DrawBush(rng, dmgRatio, false); break;
            case 4: _DrawBush(rng, dmgRatio, true); break;
        }
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    // 树：棕树干 + 多团绿冠（饥荒侧视纸片感）
    private void _DrawTree(RandomNumberGenerator rng, float dmg)
    {
        float trunkW = _size * 0.16f;
        float trunkH = _size * 0.5f;
        // 阴影
        DrawCircle(new Vector2(_size * 0.1f, trunkH * 0.5f), _size * 0.28f, new Color(0, 0, 0, 0.25f));
        // 树干
        var trunkRect = new Rect2(-trunkW * 0.5f, 0, trunkW, trunkH);
        DrawRect(trunkRect, new Color(0.26f, 0.17f, 0.09f), true);
        DrawRect(new Rect2(-trunkW * 0.5f, 0, trunkW * 0.3f, trunkH), new Color(0.35f, 0.24f, 0.14f), true);
        // 树冠（多团深浅绿）
        float crownY = -_size * 0.05f;
        var greens = new[] { new Color(0.18f, 0.32f, 0.14f), new Color(0.24f, 0.40f, 0.18f), new Color(0.30f, 0.48f, 0.22f) };
        for (int i = 0; i < 5; i++)
        {
            float a = i * (Mathf.Tau / 5f) + rng.RandfRange(-0.2f, 0.2f);
            float r = _size * (0.28f + rng.RandfRange(0, 0.08f));
            var cp = new Vector2(Mathf.Cos(a) * _size * 0.16f, crownY + Mathf.Sin(a) * _size * 0.12f) - new Vector2(0, _size * 0.18f);
            DrawCircle(cp, r, greens[i % greens.Length]);
        }
        DrawCircle(new Vector2(0, crownY - _size * 0.18f), _size * 0.30f, greens[2]);
        // 受损：缺口 + 裂纹
        if (dmg > 0.3f)
            DrawCircle(new Vector2(_size * 0.12f, crownY - _size * 0.1f), _size * 0.1f, new Color(0.10f, 0.08f, 0.05f));
        // 描边
        DrawRect(trunkRect, new Color(0.14f, 0.09f, 0.04f), false, 1.2f);
    }

    // 石头：灰岩石块（多边形 + 棱角高光）
    private void _DrawRock(RandomNumberGenerator rng, float dmg)
    {
        DrawCircle(new Vector2(_size * 0.1f, _size * 0.2f), _size * 0.32f, new Color(0, 0, 0, 0.25f));
        var baseCol = new Color(0.46f, 0.44f, 0.40f);
        var lit = new Color(0.58f, 0.56f, 0.52f);
        var dark = new Color(0.30f, 0.28f, 0.26f);
        // 主体多边形
        var pts = new Vector2[]
        {
            new(-_size*0.30f, _size*0.18f), new(-_size*0.34f, -_size*0.10f),
            new(-_size*0.12f, -_size*0.30f), new(_size*0.18f, -_size*0.28f),
            new(_size*0.34f, -_size*0.04f), new(_size*0.28f, _size*0.20f),
        };
        DrawPolygon(pts, new[] { baseCol });
        // 顶面高光
        DrawPolygon(new Vector2[] { pts[1], pts[2], pts[3], new(0, -_size*0.12f) }, new[] { lit });
        // 底面暗
        DrawPolygon(new Vector2[] { pts[0], pts[5], new(_size*0.10f, _size*0.18f), new(-_size*0.10f, _size*0.16f) }, new[] { dark });
        // 裂纹（受损）
        if (dmg > 0.3f)
        {
            DrawLine(new Vector2(-_size*0.1f, -_size*0.2f), new Vector2(_size*0.08f, _size*0.1f), dark.Darkened(0.3f), 1.5f);
            DrawLine(new Vector2(_size*0.05f, -_size*0.25f), new Vector2(-_size*0.05f, 0), dark.Darkened(0.3f), 1.2f);
        }
        for (int i = 0; i < pts.Length; i++)
            DrawLine(pts[i], pts[(i + 1) % pts.Length], dark, 1.2f);
    }

    // 灌木/浆果丛：绿色圆丛 + 浆果红点
    private void _DrawBush(RandomNumberGenerator rng, float dmg, bool berry)
    {
        DrawCircle(new Vector2(_size * 0.1f, _size * 0.2f), _size * 0.28f, new Color(0, 0, 0, 0.22f));
        var greens = new[] { new Color(0.16f, 0.30f, 0.12f), new Color(0.24f, 0.42f, 0.18f), new Color(0.32f, 0.52f, 0.24f) };
        for (int i = 0; i < 4; i++)
        {
            float a = i * (Mathf.Tau / 4f) + 0.4f;
            var cp = new Vector2(Mathf.Cos(a) * _size * 0.18f, Mathf.Sin(a) * _size * 0.12f);
            DrawCircle(cp, _size * 0.24f, greens[i % greens.Length]);
        }
        DrawCircle(new Vector2(-_size*0.08f, -_size*0.04f), _size * 0.22f, greens[2]);
        // 浆果
        if (berry)
        {
            for (int i = 0; i < 4; i++)
            {
                var bp = new Vector2(rng.RandfRange(-_size*0.2f, _size*0.2f), rng.RandfRange(-_size*0.15f, _size*0.1f));
                DrawCircle(bp, _size * 0.06f, new Color(0.8f, 0.15f, 0.2f));
                DrawCircle(bp - new Vector2(1, 1), _size * 0.025f, new Color(1f, 0.5f, 0.5f));
            }
        }
    }
}
