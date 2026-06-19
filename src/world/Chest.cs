namespace ZombieWars.World;

using Godot;
using ZombieWars.Core;
using ZombieWars.Pickups;
using ZombieWars.Player;
using ZombieWars.Vfx;

/// <summary>
/// 宝箱：玩家触碰自动开启，翻开盖子 + 金光迸发 + 掉落 2~3 件随机拾取物。
/// 大场景探索的核心奖励源——地图固定放置 + 可破坏障碍有概率掉落。
/// </summary>
public partial class Chest : Area2D
{
    public int LootCount { get; set; } = 2;

    private bool _opened;
    private bool _looted;
    private float _openAnim;

    public override void _Ready()
    {
        AddToGroup("mm_chest"); // 小地图追踪
        CollisionLayer = 0;
        CollisionMask = GameConfig.CollisionLayerPlayer;
        BodyEntered += _OnBodyEntered;

        var col = new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(48, 40) } };
        AddChild(col);

        ZIndex = 1;

        // 远距离指引光柱：大场景里让玩家从远处发现宝箱（金色脉动光柱）
        var beam = new Sprite2D
        {
            Texture = _MakeBeamTexture(new Color(1f, 0.85f, 0.3f)),
            Position = new Vector2(0, -52),
            ZIndex = 15
        };
        AddChild(beam);
        var beamTw = beam.CreateTween().SetLoops(999999);
        beamTw.TweenProperty(beam, "modulate:a", 0.35f, 1.3f);
        beamTw.TweenProperty(beam, "modulate:a", 0.85f, 1.3f);
    }

    public override void _Process(double delta)
    {
        if (_opened && _openAnim < 1f)
        {
            _openAnim = Mathf.Min(1f, _openAnim + (float)delta / 0.35f);
            QueueRedraw();
            if (_openAnim >= 1f && !_looted)
            {
                _looted = true;
                _DropLoot();
                var t = new Timer { WaitTime = 0.9f, OneShot = true };
                t.Timeout += QueueFree;
                AddChild(t);
                t.Start();
            }
        }
    }

    public override void _Draw()
    {
        float open = _openAnim;
        // 箱身
        var bodyRect = new Rect2(-24, -8, 48, 32);
        DrawRect(bodyRect, new Color(0.45f, 0.30f, 0.16f), filled: true);
        DrawRect(bodyRect, new Color(0.28f, 0.18f, 0.10f), filled: false, width: 2f);
        // 金属包边
        DrawRect(new Rect2(-24, 2, 48, 4), new Color(0.62f, 0.55f, 0.38f), filled: true);
        // 盖子（开启时上移 + 淡出）
        float lidOffset = -open * 16f;
        float lidA = 1f - open;
        var lidRect = new Rect2(-24, -20 + lidOffset, 48, 12);
        DrawRect(lidRect, new Color(0.50f, 0.34f, 0.18f, lidA), filled: true);
        DrawRect(lidRect, new Color(0.30f, 0.20f, 0.10f, lidA), filled: false, width: 2f);
        // 锁（关闭时可见）
        if (open < 0.2f)
            DrawRect(new Rect2(-5, -6, 10, 10), new Color(0.82f, 0.66f, 0.25f), filled: true);
        // 金光（开启时迸发）
        if (open > 0.3f)
        {
            float glowA = Mathf.Clamp((open - 0.3f) / 0.5f, 0f, 1f) * 0.7f;
            DrawCircle(new Vector2(0, -2), 14f + open * 10f, new Color(1f, 0.85f, 0.3f, glowA));
        }
    }

    private void _OnBodyEntered(Node body)
    {
        if (_opened) return;
        if (body is not Player) return;
        _opened = true;
        CollisionMask = 0;
        GameServices.Audio.PlayUpgrade();
    }

    private void _DropLoot()
    {
        Node? parent = GetTree().CurrentScene;
        if (parent != null)
        {
            VFXManager.SpawnParticleBurst(parent, GlobalPosition,
                new Color(1f, 0.85f, 0.3f), 22, 0.5f, 40f, 120f);
            VFXManager.SpawnImpactRing(parent, GlobalPosition, 42f, new Color(1f, 0.85f, 0.3f), 3f);
        }

        string[] types = { "health_potion", "shield_orb", "skill_book", "bomb", "magnet" };
        for (int i = 0; i < LootCount; i++)
        {
            string type = types[(int)GD.RandRange(0, types.Length - 1)];
            var pos = GlobalPosition + new Vector2(
                (float)GD.RandRange(-48, 48),
                (float)GD.RandRange(-30, 25));
            Node? pickup = type switch
            {
                "health_potion" => new HealthPotion { GlobalPosition = pos },
                "shield_orb" => new ShieldOrb { GlobalPosition = pos },
                "skill_book" => new SkillBook { GlobalPosition = pos },
                "bomb" => new BombPickup { GlobalPosition = pos },
                "magnet" => new MagnetPickup { GlobalPosition = pos },
                _ => null
            };
            if (pickup != null)
                parent?.AddChild(pickup);
        }
        LogUtils.Print($"宝箱开启，掉落 {LootCount} 件物品");
    }

    /// <summary>生成垂直渐变光柱纹理（底部亮、顶部透明），用于远距离指引。</summary>
    private static Texture2D _MakeBeamTexture(Color color)
    {
        int w = 12, h = 150;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                float yt = (float)y / h;                  // 0=顶 1=底
                float alpha = (1f - yt) * 0.85f;            // 顶部透明，底部亮
                float edge = 1f - Mathf.Abs(x - w / 2f) / (w / 2f); // 中间亮边缘暗
                img.SetPixel(x, y, new Color(color.R, color.G, color.B, alpha * edge));
            }
        }
        return ImageTexture.CreateFromImage(img);
    }
}
