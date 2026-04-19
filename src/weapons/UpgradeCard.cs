namespace ZombieWars.Weapons;

using Godot;
using ZombieWars.Core;

public partial class UpgradeCard : WeaponCard
{
    public bool IsUpgradeCard { get; set; } = true;

    public enum TradeoffType
    {
        None,
        HealthCost,
        Speed,
        HealthMax
    }

    public enum UpgradeDirection
    {
        None,
        Damage,
        Speed,
        Crit,
        Leech,
        Defense,
        Effect
    }

    public WeaponCard TargetRef { get; set; } = null!;
    public string DisplayName { get; set; } = "";

    public new string CardName => DisplayName;
    public new string Description => _BuildDescription();

    public float DamageDelta { get; set; } = 0.0f;
    public float AttackSpeedDelta { get; set; } = 0.0f;
    public float HealthDelta { get; set; } = 0.0f;
    public float SpeedDelta { get; set; } = 0.0f;
    public float CritDelta { get; set; } = 0.0f;
    public float LifeStealDelta { get; set; } = 0.0f;
    public float FreezeDelta { get; set; } = 0.0f;
    public float PoisonDelta { get; set; } = 0.0f;

    public Godot.Collections.Dictionary EffectUpgrades { get; set; } = new();

    public TradeoffType TradeoffKind { get; set; } = TradeoffType.None;
    public float TradeoffValue { get; set; } = 0.0f;

    public UpgradeDirection UpgradeDirection_ { get; set; } = UpgradeDirection.None;
    public string UpgradeCategory { get; set; } = "强化";

    private string _BuildDescription()
    {
        var parts = new Godot.Collections.Array<string>();

        if (DamageDelta > 0)
            parts.Add($"伤害+{FmtPct(DamageDelta)}");
        if (AttackSpeedDelta > 0)
            parts.Add($"攻速+{FmtPct(AttackSpeedDelta)}");
        if (HealthDelta > 0)
            parts.Add($"生命+{(int)HealthDelta}");
        if (SpeedDelta > 0)
            parts.Add($"速度+{(int)SpeedDelta}");
        if (CritDelta > 0)
            parts.Add($"暴击+{(int)CritDelta}%");
        if (LifeStealDelta > 0)
            parts.Add($"吸血+{(int)LifeStealDelta}%");
        if (FreezeDelta > 0)
            parts.Add($"冰冻+{(int)FreezeDelta}%");
        if (PoisonDelta > 0)
            parts.Add($"中毒+{(int)PoisonDelta}%");

        foreach (long fxKey in EffectUpgrades.Keys)
        {
            parts.Add("特效强化");
        }

        if (TradeoffKind != TradeoffType.None)
        {
            switch (TradeoffKind)
            {
                case TradeoffType.HealthCost:
                    parts.Add($"消耗生命+{(int)TradeoffValue}");
                    break;
                case TradeoffType.Speed:
                    parts.Add($"速度{(int)TradeoffValue}");
                    break;
                case TradeoffType.HealthMax:
                    parts.Add($"生命{(int)TradeoffValue}");
                    break;
            }
        }

        return parts.Count > 0 ? string.Join("  ", parts) : "强化";
    }

    private static string FmtPct(float val)
    {
        return $"{(int)(val * 100)}%";
    }
}
