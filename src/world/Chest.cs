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
    private MeshInstance2D? _chestInstance;
    private int _currentFrame = -1;
    private Mesh?[]? _chestFrames;

    public override void _Ready()
    {
        AddToGroup("mm_chest"); // 小地图追踪
        CollisionLayer = 0;
        CollisionMask = GameConfig.CollisionLayerPlayer;
        BodyEntered += _OnBodyEntered;

        var col = new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(48, 40) } };
        AddChild(col);

        ZIndex = 1;

        // 惰性加载 4 帧 chest mesh（_Ready 时 Godot 资源系统已就绪）
        _chestFrames = new Mesh?[]
        {
            ModelLoader.GetMesh("res://assets/models/props/chest_0.glb"),
            ModelLoader.GetMesh("res://assets/models/props/chest_1.glb"),
            ModelLoader.GetMesh("res://assets/models/props/chest_2.glb"),
            ModelLoader.GetMesh("res://assets/models/props/chest_3.glb"),
        };
        if (_chestFrames[0] != null)
        {
            _chestInstance = new MeshInstance2D { Mesh = _chestFrames[0] };
            AddChild(_chestInstance);
        }

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
            // 切换 chest mesh 帧（0/1/2/3 对应开启动画 0%/33%/66%/100%）
            int frame = Mathf.Min(3, (int)(_openAnim * 3.99f));
            if (frame != _currentFrame && _chestInstance != null && _chestFrames != null && _chestFrames[frame] != null)
            {
                _chestInstance.Mesh = _chestFrames[frame];
                _currentFrame = frame;
            }
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
        // 金光（开启时迸发）—— 静态视觉由 MeshInstance2D 提供
        if (_openAnim > 0.3f)
        {
            float open = _openAnim;
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
        // 选卡弱化：25% 概率额外给随机卡牌奖励（roguelike 选卡改为宝箱奖励）
        if (GD.Randf() < 0.25f)
            GameServices.Game.GrantChestReward();

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
