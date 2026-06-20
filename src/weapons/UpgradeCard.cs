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
    public string DisplayName
    {
        get => _displayName;
        set
        {
            _displayName = value;
            CardName = value;
        }
    }
    private string _displayName = "";

    public float DamageDelta { get; set; } = 0.0f;
    public float AttackSpeedDelta { get; set; } = 0.0f;
    public float HealthDelta { get; set; } = 0.0f;
    public float SpeedDelta { get; set; } = 0.0f;
    public float CritDelta { get; set; } = 0.0f;
    public float LifeStealDelta { get; set; } = 0.0f;
    public float FreezeDelta { get; set; } = 0.0f;
    public float PoisonDelta { get; set; } = 0.0f;
    public float ThornsChanceDelta { get; set; } = 0.0f;
    public float ThornsRatioDelta { get; set; } = 0.0f;
    public float DodgeChanceDelta { get; set; } = 0.0f;
    public float PiercingBonusDelta { get; set; } = 0.0f;
    public float BounceBonusDelta { get; set; } = 0.0f;
    public float SplashBonusDelta { get; set; } = 0.0f;
    public float TenacityDelta { get; set; } = 0.0f;

    public Godot.Collections.Dictionary EffectUpgrades { get; set; } = new();

    public TradeoffType TradeoffKind { get; set; } = TradeoffType.None;
    public float TradeoffValue { get; set; } = 0.0f;

    public UpgradeDirection UpgradeDirection_ { get; set; } = UpgradeDirection.None;
    public new string UpgradeCategory { get; set; } = "强化";

    public void SyncDescription()
    {
        Description = _BuildDescription();
    }

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
        if (ThornsChanceDelta > 0)
            parts.Add($"反伤几率+{(int)ThornsChanceDelta}%");
        if (ThornsRatioDelta > 0)
            parts.Add($"反伤比例+{(int)ThornsRatioDelta}%");
        if (DodgeChanceDelta > 0)
            parts.Add($"闪避+{(int)DodgeChanceDelta}%");
        if (PiercingBonusDelta > 0)
            parts.Add($"穿透+{(int)PiercingBonusDelta}");
        if (BounceBonusDelta > 0)
            parts.Add($"弹跳+{(int)BounceBonusDelta}");
        if (SplashBonusDelta > 0)
            parts.Add($"溅射+{(int)SplashBonusDelta}");
        if (TenacityDelta > 0)
            parts.Add($"减控+{(int)TenacityDelta}%");

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
