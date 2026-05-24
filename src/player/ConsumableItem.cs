namespace ZombieWars.Player;

using Godot;
using ZombieWars.Core;
using ZombieWars.Vfx;
using static ZombieWars.Core.GameEnums;

/// <summary>
/// A single consumable item definition — used as a template to create inventory slots.
/// </summary>
public partial class ConsumableItem : Resource
{
    public ConsumableType Type { get; set; } = ConsumableType.HealthPotion;
    public string Name { get; set; } = "药水";
    public string Icon { get; set; } = "🧪";
    public int MaxStack { get; set; } = 3;
    public float Cooldown { get; set; } = 8.0f;
    public int Hotkey { get; set; } = -1; // 1-based slot index, -1 = auto-assign

    /// <summary>Apply the consumable's effect to the player. Returns true if used successfully.</summary>
    public virtual bool Apply(Player player)
    {
        return false;
    }
}

public partial class HealthPotionItem : ConsumableItem
{
    public int HealAmount { get; set; } = 25;

    public HealthPotionItem()
    {
        Type = ConsumableType.HealthPotion;
        Name = "生命药水";
        Icon = "🧪";
        Cooldown = 8.0f;
        MaxStack = 3;
    }

    public override bool Apply(Player player)
    {
        if (player.IsDead) return false;
        int before = player.CurrentHealth;
        player.Heal(HealAmount);
        int healed = player.CurrentHealth - before;
        if (healed > 0)
        {
            GameServices.UI?.ShowDamageNumber(player.GlobalPosition, -healed, false);
            VFXManager.SpawnFloatingLabel(player.GetParent(), player.GlobalPosition,
                $"+{healed} HP", new Color(0.3f, 1.0f, 0.4f), 16);
        }
        return healed > 0;
    }
}

public partial class SpeedPotionItem : ConsumableItem
{
    public float SpeedMult { get; set; } = 1.5f;
    public float Duration { get; set; } = 5.0f;

    public SpeedPotionItem()
    {
        Type = ConsumableType.SpeedPotion;
        Name = "速度药水";
        Icon = "⚡";
        Cooldown = 10.0f;
        MaxStack = 3;
    }

    public override bool Apply(Player player)
    {
        if (player.IsDead) return false;
        player.BuffSystem?.AddBuff(BuffType.SpeedBoost, BuffSource.PlayerItem, Duration, SpeedMult,
            description: $"速度 +{(SpeedMult - 1.0f) * 100:F0}%");
        VFXManager.SpawnImpactRing(player.GetParent(), player.GlobalPosition, 30,
            new Color(1.0f, 0.9f, 0.3f, 0.8f), 2.0f);
        return true;
    }
}