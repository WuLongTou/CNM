namespace ZombieWars.Pickups;

using Godot;
using ZombieWars.Core;
using Player = ZombieWars.Player.Player;

/// <summary>
/// 护盾球：拾取后为玩家补充护盾值（上限受 MaxShield 约束）。
/// </summary>
public partial class ShieldOrb : Pickup
{
    public int ShieldGain { get; set; } = 30;

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
                if (d < size * 0.40f)
                {
                    float t = d / (size * 0.40f);
                    img.SetPixel(x, y, new Color(
                        0.35f + 0.4f * (1 - t),
                        0.75f + 0.2f * (1 - t),
                        1.0f,
                        0.95f - t * 0.2f));
                }
                else if (d < size * 0.48f)
                {
                    img.SetPixel(x, y, new Color(0.5f, 0.85f, 1.0f, 0.6f));
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
        player.ShieldAmount = Mathf.Min(player.ShieldAmount + ShieldGain, GameConfig.MaxShield);
        GameServices.UI?.ShowDamageNumber(player.GlobalPosition, -ShieldGain, false);
        LogUtils.Print($"拾取护盾球，护盾 +{ShieldGain}");
    }
}
