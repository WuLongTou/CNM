namespace ZombieWars.Pickups;

using Godot;
using ZombieWars.Core;
using static ZombieWars.Core.GameEnums;
using Player = ZombieWars.Player.Player;

public partial class HealthPotion : Pickup
{
    public int HealAmount { get; set; } = 20;

    protected override void _CreateSprite()
    {
        var tex = TextureUtils.LoadNormalizedSprite("res://assets/sprites/pickups/pickup_potion.png");
        if (tex == null)
        {
            // Fallback: generate potion sprite
            int size = 20;
            var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float cx = size / 2.0f, cy = size / 2.0f;
                    float dist = new Vector2(x - cx, y - cy).Length();
                    if (dist < size * 0.35f)
                    {
                        float t = (y - size * 0.3f) / (size * 0.7f);
                        float r = Mathf.Lerp(0.9f, 0.3f, t);
                        float g = Mathf.Lerp(0.2f, 0.1f, t);
                        float b = Mathf.Lerp(0.1f, 0.3f, t);
                        img.SetPixel(x, y, new Color(r, g, b, 0.95f));
                    }
                    else if (dist < size * 0.45f && y < size * 0.35f)
                    {
                        img.SetPixel(x, y, new Color(0.7f, 0.6f, 0.5f, 0.9f));
                    }
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
        int before = player.CurrentHealth;
        player.Heal(HealAmount);
        int actualHeal = player.CurrentHealth - before;
        GameServices.UI?.ShowDamageNumber(player.GlobalPosition, -actualHeal, false);

        player.ApplyBuffFromZombie(BuffType.DamageBoost, 5.0f, 1.15f);
        LogUtils.Print($"拾取生命药水，回复 {actualHeal} HP");
    }
}