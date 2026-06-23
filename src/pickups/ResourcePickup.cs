namespace ZombieWars.Pickups;

using Godot;
using ZombieWars.Core;
using ZombieWars.World;
using Player = ZombieWars.Player.Player;

/// <summary>资源拾取物（木材/石头/纤维/浆果）。阶段 1：拾取显示通知；阶段 2：接入 Inventory。</summary>
public sealed partial class ResourcePickup : Pickup
{
    public ResourceType Type;
    public int Amount = 1;

    protected override void _CreateSprite()
    {
        int size = 18;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var (baseCol, accent) = _Colors(Type);
        float cx = size / 2f, cy = size / 2f;
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                float dist = new Vector2(x - cx, y - cy).Length();
                if (dist < size * 0.4f)
                    img.SetPixel(x, y, dist < size * 0.18f ? accent : baseCol);
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
        string name = Type switch
        {
            ResourceType.Wood => "木材",
            ResourceType.Stone => "石头",
            ResourceType.Fiber => "纤维",
            ResourceType.Berry => "浆果",
            _ => "资源",
        };
        // 阶段 2：资源进库存 + 通知反馈
        var item = Type switch
        {
            ResourceType.Wood => ItemId.Wood,
            ResourceType.Stone => ItemId.Stone,
            ResourceType.Fiber => ItemId.Fiber,
            ResourceType.Berry => ItemId.Berry,
            _ => ItemId.Wood,
        };
        Inventory.Add(item, Amount);
        GameServices.UI?.ShowLevelUpNotification($"+{Amount} {name}", _Colors(Type).Item1);
        LogUtils.Print($"采集 {name} +{Amount}");
    }

    private static (Color, Color) _Colors(ResourceType t) => t switch
    {
        ResourceType.Wood => (new Color(0.55f, 0.36f, 0.18f), new Color(0.78f, 0.56f, 0.30f)),
        ResourceType.Stone => (new Color(0.52f, 0.50f, 0.48f), new Color(0.75f, 0.73f, 0.70f)),
        ResourceType.Fiber => (new Color(0.40f, 0.55f, 0.25f), new Color(0.65f, 0.80f, 0.40f)),
        ResourceType.Berry => (new Color(0.80f, 0.15f, 0.20f), new Color(1.0f, 0.45f, 0.45f)),
        _ => (Colors.White, Colors.Gray),
    };
}
