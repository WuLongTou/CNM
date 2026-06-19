namespace ZombieWars.Pickups;

using System.Collections.Generic;
using Godot;
using ZombieWars.Core;
using ZombieWars.Enemy;
using ZombieWars.Vfx;
using Player = ZombieWars.Player.Player;

/// <summary>
/// 炸弹：拾取后引爆，对全场敌人造成一次性伤害 + 爆炸特效 + 震屏。
/// 给玩家一个"清场救场"的稀有手段。
/// </summary>
public partial class BombPickup : Pickup
{
    public int Damage { get; set; } = 60;

    protected override void _CreateSprite()
    {
        int size = 22;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float c = size / 2f;
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float d = new Vector2(x - c, y - c).Length();
                if (d < size * 0.35f)
                    img.SetPixel(x, y, new Color(0.12f, 0.12f, 0.14f, 0.97f));
                else if (d < size * 0.42f)
                    img.SetPixel(x, y, new Color(0.25f, 0.25f, 0.28f, 0.9f));
            }
        }
        // 引信（右上）
        for (int i = 0; i < 4; i++)
        {
            int fx = (int)c + i, fy = (int)(c - size * 0.35f) - i;
            if (fx < size && fy >= 0)
                img.SetPixel(fx, fy, new Color(0.6f, 0.4f, 0.2f, 0.95f));
        }
        _sprite = new Sprite2D
        {
            Texture = ImageTexture.CreateFromImage(img),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        AddChild(_sprite);
    }

    protected override void _Collect(Player? player)
    {
        if (player == null) return;
        Node? parent = player.GetParent();

        var all = new List<Zombie>(ZombieRegistry.All);
        foreach (var z in all)
        {
            if (GodotObject.IsInstanceValid(z))
                z.TakeDamage(Damage);
        }

        if (parent != null)
        {
            VFXManager.SpawnParticleBurst(parent, player.GlobalPosition,
                new Color(1f, 0.5f, 0.1f), 30, 0.6f, 80f, 200f,
                new Vector3(0, 80, 0), 2f, 4f);
            VFXManager.SpawnImpactRing(parent, player.GlobalPosition, 120f, new Color(1f, 0.5f, 0.1f), 5f);
        }
        GetTree().CallGroup("game_camera", "AddShake", 10f);
        LogUtils.Print($"拾取炸弹，全屏 {Damage} 伤害（命中 {all.Count} 只敌人）");
    }
}
