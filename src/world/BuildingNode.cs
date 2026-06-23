namespace ZombieWars.World;

using System.Collections.Generic;
using Godot;
using ZombieWars.Core;

/// <summary>建筑类型（饥荒式可建造防御/设施）。</summary>
public enum BuildingType { Wall, SpikeTrap, Campfire, StorageBox }

/// <summary>
/// 玩家建造的建筑（消耗 Inventory 材料放置）。墙阻挡僵尸，陷阱伤害，营火回血等。
/// </summary>
public sealed partial class BuildingNode : StaticBody2D
{
    public BuildingType Type;
    private Area2D? _detector;
    private float _tickTimer;

    // 预加载陷阱贴图(64x64 PNG,适配 48x48 碰撞体)。
    // 用真实 sprite 替代代码绘制:细节更丰富(血渍/金属高光/警示三角都在贴图里),
    // 运行时 0 重绘开销,style 跟项目其他精灵统一。
    private static readonly Texture2D? _spikeTex =
        ResourceLoader.Exists("res://assets/sprites/buildings/spike_trap.png")
            ? GD.Load<Texture2D>("res://assets/sprites/buildings/spike_trap.png")
            : null;

    public override void _Ready()
    {
        switch (Type)
        {
            case BuildingType.Wall:
                CollisionLayer = GameConfig.CollisionLayerBuilding;
                CollisionMask = 0; // 由僵尸/玩家的 mask 碰它
                var wallCol = new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(50, 16) } };
                AddChild(wallCol);
                AddToGroup("destructibles");
                break;

            case BuildingType.SpikeTrap:
                CollisionLayer = 0;
                CollisionMask = GameConfig.CollisionLayerZombie;
                _detector = new Area2D { Monitoring = true, Monitorable = false };
                _detector.CollisionLayer = 0;
                _detector.CollisionMask = GameConfig.CollisionLayerZombie;
                var trapCol = new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(48, 48) } };
                _detector.AddChild(trapCol);
                AddChild(_detector);
                break;
        }
        ZIndex = 2;
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Type == BuildingType.SpikeTrap && _detector != null)
        {
            _tickTimer -= (float)delta;
            if (_tickTimer > 0) return;
            _tickTimer = 0.5f;
            foreach (var body in _detector.GetOverlappingBodies())
            {
                if (body is Enemy.Zombie z && !z.IsDead)
                {
                    z.TakeDamage(GameConfig.BuildingSpikeTrapDamage);
                    z.MapSlowMult = Mathf.Min(z.MapSlowMult, GameConfig.BuildingSpikeTrapSlowMult);
                }
            }
        }
    }

    public override void _Draw()
    {
        switch (Type)
        {
            case BuildingType.Wall: _DrawWall(); break;
            case BuildingType.SpikeTrap: _DrawSpike(); break;
            case BuildingType.Campfire: _DrawFire(); break;
            case BuildingType.StorageBox: _DrawBox(); break;
        }
    }

    private void _DrawWall()
    {
        DrawRect(new Rect2(-25, -8, 50, 16), new Color(0.30f, 0.22f, 0.16f), true);
        DrawRect(new Rect2(-25, -8, 50, 3), new Color(0.42f, 0.32f, 0.24f), true);
        DrawRect(new Rect2(-25, -8, 50, 16), new Color(0.18f, 0.12f, 0.08f), false, 2f);
        // 木纹
        DrawLine(new Vector2(-22, -4), new Vector2(-18, 4), new Color(0.22f, 0.15f, 0.10f), 1f);
        DrawLine(new Vector2(-8, -6), new Vector2(-4, 6), new Color(0.22f, 0.15f, 0.10f), 1f);
        DrawLine(new Vector2(6, -4), new Vector2(10, 4), new Color(0.22f, 0.15f, 0.10f), 1f);
        DrawLine(new Vector2(18, -6), new Vector2(22, 6), new Color(0.22f, 0.15f, 0.10f), 1f);
    }

    private void _DrawSpike()
    {
        // 用真实贴图渲染(spike_trap.png 64x64,适配 48x48 碰撞体)。
        // 仅做 Modulate 脉冲呼吸(1.5s 节奏,让贴图"活"起来,提示危险)。
        // 备份方案:贴图缺失时退回占位红框(确保 Bug 可见)。
        if (_spikeTex != null)
        {
            float t = (float)Time.GetTicksMsec() / 1000f;
            float breath = 0.5f + 0.5f * Mathf.Sin(t * 1.5f);
            // R 通道偏暖 + alpha 微脉冲,让贴图有"危险发光"感
            var col = new Color(1f, 0.95f + breath * 0.05f, 0.95f, 0.92f + breath * 0.08f);
            // 64x64 贴图居中绘制(中心 0,0 → 贴图左上角在 -32,-32)
            DrawTexture(_spikeTex, new Vector2(-32, -32), col);
            return;
        }

        // Fallback:贴图缺失时,显示一个占位红框(确保 Bug 可见)
        DrawRect(new Rect2(-24, -24, 48, 48), new Color(0.8f, 0.1f, 0.1f, 0.7f), false, 2f);
    }

    private void _DrawFire()
    {
        float t = (float)Time.GetTicksMsec() / 1000f;
        for (int i = 0; i < 3; i++)
        {
            float fx = -4 + i * 4 + Mathf.Sin(t * 5f + i) * 2f;
            float fy = Mathf.Sin(t * 7f + i * 2f) * 3f;
            DrawCircle(new Vector2(fx, -4 + fy), 6 - i, new Color(1f, 0.6f - i * 0.15f, 0.15f - i * 0.05f, 0.7f));
        }
    }

    private void _DrawBox()
    {
        DrawRect(new Rect2(-16, -16, 32, 32), new Color(0.35f, 0.28f, 0.18f), true);
        DrawRect(new Rect2(-16, -16, 32, 6), new Color(0.45f, 0.36f, 0.24f), true);
        DrawRect(new Rect2(-16, -16, 32, 32), new Color(0.20f, 0.14f, 0.08f), false, 2f);
        DrawLine(new Vector2(0, -16), new Vector2(0, 16), new Color(0.22f, 0.16f, 0.10f), 1f);
    }

    /// <summary>建造成本（消耗 Inventory 材料）。</summary>
    public static Dictionary<ItemId, int> GetCost(BuildingType type) => type switch
    {
        BuildingType.Wall => new() { { ItemId.Wood, 3 } },
        BuildingType.SpikeTrap => new() { { ItemId.Wood, 2 }, { ItemId.Stone, 2 } },
        BuildingType.Campfire => new() { { ItemId.Wood, 3 }, { ItemId.Fiber, 2 } },
        BuildingType.StorageBox => new() { { ItemId.Wood, 4 } },
        _ => new() { { ItemId.Wood, 1 } },
    };
}
