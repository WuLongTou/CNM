namespace ZombieWars.Pickups;

using Godot;
using ZombieWars.Core;
using Player = ZombieWars.Player.Player;

/// <summary>
/// 磁铁：拾取后把场上所有拾取物吸引到玩家位置，快速收集散落道具。
/// </summary>
public partial class MagnetPickup : Pickup
{
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
                float dx = x - c;
                // U 形磁铁两极：左红右蓝
                if (d < size * 0.42f && Mathf.Abs(dx) > size * 0.18f)
                {
                    bool left = dx < 0;
                    img.SetPixel(x, y, left
                        ? new Color(0.9f, 0.2f, 0.2f, 0.95f)
                        : new Color(0.2f, 0.3f, 0.9f, 0.95f));
                }
            }
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
        // 通知所有拾取物移向玩家（Pickup 基类的 MagnetTo 改 _basePos，浮动动画自然过渡）
        GetTree().CallGroup("pickup", nameof(MagnetTo), player.GlobalPosition);
        LogUtils.Print("拾取磁铁，吸引场上所有物品");
    }
}
