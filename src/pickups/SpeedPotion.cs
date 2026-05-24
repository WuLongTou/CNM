namespace ZombieWars.Pickups;

using Godot;
using ZombieWars.Core;
using ZombieWars.Player;
using static ZombieWars.Core.GameEnums;
using Player = ZombieWars.Player.Player;

/// <summary>
/// A ground pickup that awards one speed potion to the player's consumable inventory.
/// </summary>
public partial class SpeedPotion : Pickup
{
    protected override void _CreateSprite()
    {
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
                    float r = Mathf.Lerp(1.0f, 0.9f, t);
                    float g = Mathf.Lerp(0.9f, 0.6f, t);
                    float b = Mathf.Lerp(0.2f, 0.1f, t);
                    img.SetPixel(x, y, new Color(r, g, b, 0.95f));
                }
                else if (dist < size * 0.45f && y < size * 0.35f)
                {
                    img.SetPixel(x, y, new Color(0.7f, 0.6f, 0.5f, 0.9f));
                }
            }
        }
        var tex = ImageTexture.CreateFromImage(img);

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

        var item = new SpeedPotionItem();
        bool accepted = player.Inventory?.AddItem(item) ?? false;

        if (accepted)
        {
            LogUtils.Print($"拾取速度药水");
        }
        else
        {
            LogUtils.Print("背包已满，无法拾取速度药水");
        }
    }
}