namespace ZombieWars.Pickups;

using Godot;
using ZombieWars.Core;
using static ZombieWars.Core.GameEnums;
using Player = ZombieWars.Player.Player;

public partial class SkillBook : Pickup
{
    protected override void _CreateSprite()
    {
        var tex = TextureUtils.LoadNormalizedSprite("res://assets/sprites/pickups/pickup_book.png");
        if (tex == null)
        {
            // Fallback: generate book sprite
            int bw = 24, bh = 20;
            var img = Image.CreateEmpty(bw, bh, false, Image.Format.Rgba8);

            // Book spine (left side)
            for (int y = 0; y < bh; y++)
                img.SetPixel(0, y, new Color(0.5f, 0.3f, 0.15f, 0.9f));

            // Pages
            for (int x = 1; x < bw; x++)
            {
                for (int y = 2; y < bh - 2; y++)
                {
                    float t = (float)x / bw;
                    img.SetPixel(x, y, new Color(0.95f, 0.9f, 0.8f, 0.9f));
                }
            }

            // Cover
            for (int x = bw - 5; x < bw; x++)
            {
                for (int y = 1; y < bh - 1; y++)
                {
                    img.SetPixel(x, y, new Color(0.7f, 0.15f, 0.15f, 0.9f));
                }
            }

            // Page lines
            for (int x = 3; x < bw - 6; x += 4)
            {
                for (int y = 3; y < bh - 3; y++)
                {
                    if (y % 3 == 0)
                        img.SetPixel(x, y, new Color(0.7f, 0.7f, 0.7f, 0.5f));
                }
            }

            tex = ImageTexture.CreateFromImage(img);
        }

        _sprite = new Sprite2D
        {
            Texture = tex,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
        AddChild(_sprite);
    }

    protected override void _Collect(Player? player)
    {
        if (player == null) return;
        player.ApplyBuffFromZombie(BuffType.DamageBoost, 8.0f, 1.25f);
        player.ApplyBuffFromZombie(BuffType.SpeedBoost, 5.0f, 1.20f);

        // Show notification
        GameServices.UI?.ShowKillEffect(player.GlobalPosition, 0);

        LogUtils.Print("拾取技能书，获得伤害和速度提升！");
    }
}