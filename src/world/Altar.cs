namespace ZombieWars.World;

using Godot;
using ZombieWars.Core;
using ZombieWars.Player;
using ZombieWars.Vfx;
using ZombieWars.Weapons;
using static ZombieWars.Core.GameEnums;

/// <summary>
/// 祭坛：玩家触碰激活，获得一次性强力增益（伤害/回血/护盾/疾速/武器融合）。
/// 融合祭坛触发时弹出模态面板供玩家确认，其余 boon 即时生效。
/// </summary>
public partial class Altar : Area2D
{
    public enum BoonKind { Damage, Heal, Shield, Haste, WeaponFusion }
    public BoonKind Boon { get; set; } = BoonKind.Damage;
    /// <summary>融合祭坛的"对偶武器"名：玩家武器与此名匹配方可触发融合。</summary>
    public string? MateWeaponName { get; set; }
    public string? FusionResultName { get; set; }
    public string? FusionResultDesc { get; set; }

    private bool _activated;
    private float _glowPhase;

    /// <summary>供融合面板关闭后调用：拒绝时退化为回血，接受时已 QueueFree。</summary>
    public void ActivateAndDestroy()
    {
        var player = GameServices.Game?.PlayerRef as Player;
        if (Boon != BoonKind.WeaponFusion && player != null)
            _ApplyAndDestroy(player);
        else
            QueueFree();
    }

    public override void _Ready()
    {
        AddToGroup("mm_altar"); // 小地图追踪
        CollisionLayer = 0;
        CollisionMask = GameConfig.CollisionLayerPlayer;
        BodyEntered += _OnBodyEntered;

        var col = new CollisionShape2D { Shape = new CircleShape2D { Radius = 32f } };
        AddChild(col);
        ZIndex = 1;

        // 远距离指引光柱（祭坛增益色脉动）
        var beam = new Sprite2D
        {
            Texture = _MakeBeamTexture(_BoonColor()),
            Position = new Vector2(0, -55),
            ZIndex = 15
        };
        AddChild(beam);
        var beamTw = beam.CreateTween().SetLoops(999999);
        beamTw.TweenProperty(beam, "modulate:a", 0.35f, 1.3f);
        beamTw.TweenProperty(beam, "modulate:a", 0.85f, 1.3f);
    }

    public override void _Process(double delta)
    {
        _glowPhase += (float)delta * 2.2f;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var c = _BoonColor();
        float pulse = 0.65f + Mathf.Sin(_glowPhase) * 0.35f;
        // 石祭坛底座
        DrawRect(new Rect2(-24, -4, 48, 14), new Color(0.36f, 0.33f, 0.30f), filled: true);
        DrawRect(new Rect2(-28, 2, 56, 8), new Color(0.27f, 0.25f, 0.23f), filled: true);
        // 符文圆环
        DrawArc(new Vector2(0, -2), 22f, 0f, Mathf.Tau, 28,
            new Color(c.R, c.G, c.B, pulse * 0.7f), 2.2f);
        // 五颗脉动符文
        for (int i = 0; i < 5; i++)
        {
            float a = i / 5f * Mathf.Tau + _glowPhase * 0.3f;
            DrawCircle(new Vector2(Mathf.Cos(a) * 20f, Mathf.Sin(a) * 11f - 2f), 3.5f,
                new Color(c.R, c.G, c.B, pulse));
        }
        // 中心光柱
        DrawRect(new Rect2(-3, -34, 6, 30), new Color(c.R, c.G, c.B, pulse * 0.45f), filled: true);
    }

    private Color _BoonColor() => Boon switch
    {
        BoonKind.Damage => new Color(1.0f, 0.40f, 0.20f),
        BoonKind.Heal => new Color(0.30f, 1.0f, 0.40f),
        BoonKind.Shield => new Color(0.40f, 0.70f, 1.0f),
        BoonKind.Haste => new Color(1.0f, 0.85f, 0.30f),
        BoonKind.WeaponFusion => new Color(0.70f, 0.30f, 1.0f),
        _ => Colors.White,
    };

    private string _BoonName() => Boon switch
    {
        BoonKind.Damage => "伤害强化!",
        BoonKind.Heal => "生命回复!",
        BoonKind.Shield => "护盾加持!",
        BoonKind.Haste => "疾速祝福!",
        BoonKind.WeaponFusion => "武器融合!",
        _ => "",
    };

    private void _OnBodyEntered(Node body)
    {
        if (_activated) return;
        if (body is not Player player) return;
        _activated = true;
        CollisionMask = 0;

        if (Boon == BoonKind.WeaponFusion)
        {
            // 融合祭坛：检查玩家武器是否匹配配方
            WeaponCard? currentWeapon = null;
            foreach (var e in player.ActiveEffects)
            {
                if (e.Category == WeaponCategory.Weapon) { currentWeapon = e; break; }
            }
            if (currentWeapon != null && GameServices.UI is UI.UIManager ui)
            {
                bool canFuse = ui.TryShowFusionOffer(this, currentWeapon);
                if (canFuse) return; // Panel 处理完毕后自行销毁
            }
            // 不匹配或无法弹面板，退化为伤害 boon
            Boon = BoonKind.Damage;
        }

        _ApplyAndDestroy(player);
    }

    /// <summary>应用即时生效的 boon 并销毁祭坛。</summary>
    private void _ApplyAndDestroy(Player player)
    {
        _ApplyBoon(player);
        Node? parent = GetParent();
        if (parent != null)
        {
            VFXManager.SpawnImpactRing(parent, GlobalPosition, 55f, _BoonColor(), 3f);
            VFXManager.SpawnParticleBurst(parent, GlobalPosition, _BoonColor(), 22, 0.6f, 40f, 120f);
            VFXManager.SpawnFloatingLabel(parent, GlobalPosition, _BoonName(), _BoonColor(), 18);
        }
        GameServices.Audio.PlayUpgrade();
        LogUtils.Print($"激活祭坛：{_BoonName()}");
        var t = new Timer { WaitTime = 0.4f, OneShot = true };
        t.Timeout += QueueFree;
        AddChild(t);
        t.Start();
    }

    private void _ApplyBoon(Player player)
    {
        switch (Boon)
        {
            case BoonKind.Damage:
                player.ApplyBuffFromZombie(BuffType.DamageBoost, 10f, 1.35f);
                break;
            case BoonKind.Heal:
                player.Heal(50);
                break;
            case BoonKind.Shield:
                player.ShieldAmount = Mathf.Min(player.ShieldAmount + 40, GameConfig.MaxShield);
                break;
            case BoonKind.Haste:
                player.ApplyBuffFromZombie(BuffType.SpeedBoost, 10f, 1.35f);
                break;
            case BoonKind.WeaponFusion:
                // 融合已由面板处理，此处不执行额外逻辑
                break;
        }
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
                float yt = (float)y / h;
                float alpha = (1f - yt) * 0.85f;
                float edge = 1f - Mathf.Abs(x - w / 2f) / (w / 2f);
                img.SetPixel(x, y, new Color(color.R, color.G, color.B, alpha * edge));
            }
        }
        return ImageTexture.CreateFromImage(img);
    }
}
