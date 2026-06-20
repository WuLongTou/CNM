namespace ZombieWars.Weapons;

using System;
using System.Collections.Generic;
using Godot;
using ZombieWars.Core;
using static ZombieWars.Core.GameEnums;

public partial class WeaponCard : Resource
{
    [Export] public string CardName { get; set; } = "";
    [Export] public string Description { get; set; } = "";
    [Export] public WeaponType WeaponType { get; set; } = WeaponType.Melee;
    [Export] public WeaponCategory Category { get; set; } = WeaponCategory.Weapon;
    [Export] public int Tier { get; set; } = 1;
    [Export] public string UpgradeCategory { get; set; } = "";

    [Export] public float DamageModifier { get; set; } = 1.0f;
    [Export] public float AttackSpeedModifier { get; set; } = 1.0f;
    [Export] public float RangeModifier { get; set; } = 1.0f;

    [Export] public float HealthModifier { get; set; } = 0.0f;
    [Export] public float SpeedModifier { get; set; } = 0.0f;

    [Export] public float Cooldown { get; set; } = 0.0f;
    [Export] public int HealthCost { get; set; } = 0;

    [Export] public float CriticalBonus { get; set; } = 0.0f;
    [Export] public float LifeStealBonus { get; set; } = 0.0f;
    [Export] public float FreezeBonus { get; set; } = 0.0f;
    [Export] public float PoisonBonus { get; set; } = 0.0f;
    [Export] public bool GrantsAura { get; set; } = false;

    [Export] public float ThornsChance { get; set; } = 0.0f;
    [Export] public float ThornsRatio { get; set; } = 0.0f;
    [Export] public float DodgeChance { get; set; } = 0.0f;
    [Export] public int PiercingBonus { get; set; } = 0;
    [Export] public int BounceBonus { get; set; } = 0;
    [Export] public float SplashBonus { get; set; } = 0.0f;
    [Export] public float Tenacity { get; set; } = 0.0f;

    public Godot.Collections.Array MeleeEffects { get; set; } = new();
    public Godot.Collections.Dictionary MeleeEffectLevels { get; set; } = new();

    public Godot.Collections.Array RangedEffects { get; set; } = new();
    public Godot.Collections.Dictionary RangedEffectLevels { get; set; } = new();

    public Godot.Collections.Array MagicEffects { get; set; } = new();
    public Godot.Collections.Dictionary MagicEffectLevels { get; set; } = new();

    [Export] public TargetPriority TargetPriority { get; set; } = TargetPriority.Nearest;

    [Export] public string Icon { get; set; } = "剑";
    public string IconTexturePath { get; set; } = "";
    private Texture2D? _iconTexture;
    [Export] public Texture2D? IconTexture
    {
        get => _iconTexture ??= _LoadIconTexture();
        set => _iconTexture = value;
    }
    [Export] public int Rarity { get; set; } = 1;
    [Export] public int Duration { get; set; } = -1;

    // 数学系统：波形共振频率（近战=高频、远程=中频、魔法=低频）
    [Export] public float Frequency { get; set; } = 0.5f;

    public Godot.Collections.Array Tags { get; set; } = new();

    public int UpgradeCount { get; set; } = 0;
    public Godot.Collections.Dictionary UpgradeHistory { get; set; } = new();
    public string BaseName { get; set; } = "";

    public WeaponCard Init(string name, string desc, WeaponType weaponKind, Godot.Collections.Array cardTags = null!)
    {
        CardName = name;
        BaseName = name;
        Description = desc;
        WeaponType = weaponKind;
        MeleeEffects = new Godot.Collections.Array();
        RangedEffects = new Godot.Collections.Array();
        MagicEffects = new Godot.Collections.Array();
        MeleeEffectLevels = new Godot.Collections.Dictionary();
        RangedEffectLevels = new Godot.Collections.Dictionary();
        MagicEffectLevels = new Godot.Collections.Dictionary();
        Tags = cardTags ?? new Godot.Collections.Array();
        return this;
    }

    private Texture2D? _LoadIconTexture()
    {
        if (string.IsNullOrEmpty(IconTexturePath))
            return null;
        if (!FileAccess.FileExists(IconTexturePath))
            return null;
        return TextureUtils.LoadTexture(IconTexturePath);
    }

    public string GetEffectDescription()
    {
        var descParts = new Godot.Collections.Array<string>();

        if (DamageModifier != 1.0f)
        {
            string modText = "伤害" + (DamageModifier > 1.0f ? "+" : "");
            descParts.Add($"{modText}{(int)(Mathf.Abs(DamageModifier) - 1.0f) * 100}%");
        }

        if (AttackSpeedModifier != 1.0f)
        {
            string modText = "攻速" + (AttackSpeedModifier > 1.0f ? "+" : "");
            descParts.Add($"{modText}{(int)(Mathf.Abs(AttackSpeedModifier) - 1.0f) * 100}%");
        }

        if (HealthModifier > 0)
            descParts.Add($"生命+{HealthModifier}");
        else if (HealthModifier < 0)
            descParts.Add($"生命{HealthModifier}");

        if (SpeedModifier > 0)
            descParts.Add($"速度+{SpeedModifier}");
        else if (SpeedModifier < 0)
            descParts.Add($"速度{SpeedModifier}");

        if (Cooldown > 0)
            descParts.Add($"冷却{Cooldown}秒");

        if (HealthCost > 0)
            descParts.Add($"消耗{HealthCost}生命");

        if (CriticalBonus > 0)
            descParts.Add($"暴击+{(int)CriticalBonus}%");
        if (LifeStealBonus > 0)
            descParts.Add($"吸血+{(int)LifeStealBonus}%");
        if (FreezeBonus > 0)
            descParts.Add($"冰冻+{(int)FreezeBonus}%");
        if (PoisonBonus > 0)
            descParts.Add($"中毒+{(int)PoisonBonus}%");
        if (GrantsAura)
            descParts.Add("开启护盾领域");

        if (descParts.Count == 0)
            return "基础武器";

        return string.Join(" | ", descParts);
    }

    public string GetWeaponTypeText()
    {
        switch (WeaponType)
        {
            case WeaponType.Melee: return "近战";
            case WeaponType.Ranged: return "远程";
            case WeaponType.Magic: return "魔法";
            default: return "未知";
        }
    }

    public string GetCategoryText()
    {
        switch (Category)
        {
            case WeaponCategory.Weapon: return "武器";
            case WeaponCategory.Equipment: return "装备";
            default: return "未知";
        }
    }

    public bool IsUpgradeFor(Godot.Collections.Array weaponNames)
    {
        foreach (string tag in Tags)
        {
            foreach (string name in weaponNames)
            {
                if (name.Contains(tag))
                    return true;
            }
        }
        return false;
    }

    public bool HasNegativeEffects()
    {
        return HealthModifier < 0 || SpeedModifier < 0 || DamageModifier < 1.0f || HealthCost > 0;
    }

    public string GetEffectDetails()
    {
        string details;

        switch (WeaponType)
        {
            case WeaponType.Melee:
                details = "【特性】伤害高·攻速快\n";
                break;
            case WeaponType.Ranged:
                details = "【特性】自动索敌·攻速较慢\n";
                break;
            case WeaponType.Magic:
                details = "【特性】自动攻击·特殊效果\n";
                break;
            default:
                details = "";
                break;
        }

        var effectList = new Godot.Collections.Array<string>();

        foreach (Variant ve in MeleeEffects)
        {
            MeleeEffect effect = (MeleeEffect)(int)ve;
            int level = MeleeEffectLevels.ContainsKey((int)effect) ? (int)MeleeEffectLevels[(int)effect] : 1;
            effectList.Add(GetMeleeEffectDesc(effect, level));
        }

        foreach (Variant ve in RangedEffects)
        {
            RangedEffect effect = (RangedEffect)(int)ve;
            int level = RangedEffectLevels.ContainsKey((int)effect) ? (int)RangedEffectLevels[(int)effect] : 1;
            effectList.Add(GetRangedEffectDesc(effect, level));
        }

        foreach (Variant ve in MagicEffects)
        {
            MagicEffect effect = (MagicEffect)(int)ve;
            int level = MagicEffectLevels.ContainsKey((int)effect) ? (int)MagicEffectLevels[(int)effect] : 1;
            effectList.Add(GetMagicEffectDesc(effect, level));
        }

        if (effectList.Count > 0)
        {
            details += "\n【特效】\n" + string.Join("\n", effectList);
        }

        return details;
    }

    public static string GetMeleeEffectDesc(MeleeEffect effect, int level)
        => EffectCatalog.GetSummary(effect, level);

    public static string GetMeleeEffectDetail(MeleeEffect effect, int level)
        => EffectCatalog.GetDetail(effect, level);

    public static string GetRangedEffectDesc(RangedEffect effect, int level)
        => EffectCatalog.GetSummary(effect, level);

    public static string GetRangedEffectDetail(RangedEffect effect, int level)
        => EffectCatalog.GetDetail(effect, level);

    public static string GetMagicEffectDesc(MagicEffect effect, int level)
        => EffectCatalog.GetSummary(effect, level);

    public static string GetMagicEffectDetail(MagicEffect effect, int level)
        => EffectCatalog.GetDetail(effect, level);

    public string GetEffectDetail(string effectName, string effectType, int level)
    {
        string detail = "";

        switch (effectType)
        {
            case "melee":
                switch (effectName)
                {
                    case "SPLASH": detail = GetMeleeEffectDetail(MeleeEffect.Splash, level); break;
                    case "MULTI_TARGET": detail = GetMeleeEffectDetail(MeleeEffect.MultiTarget, level); break;
                    case "KILL_EXPLOSION": detail = GetMeleeEffectDetail(MeleeEffect.KillExplosion, level); break;
                    case "KILL_SHIELD": detail = GetMeleeEffectDetail(MeleeEffect.KillShield, level); break;
                    case "KNOCKBACK": detail = GetMeleeEffectDetail(MeleeEffect.Knockback, level); break;
                }
                break;
            case "ranged":
                switch (effectName)
                {
                    case "MULTI_SHOT": detail = GetRangedEffectDetail(RangedEffect.MultiShot, level); break;
                    case "TRAJECTORY_DAMAGE": detail = GetRangedEffectDetail(RangedEffect.TrajectoryDamage, level); break;
                    case "RETURNING": detail = GetRangedEffectDetail(RangedEffect.Returning, level); break;
                    case "PIERCING": detail = GetRangedEffectDetail(RangedEffect.Piercing, level); break;
                }
                break;
            case "magic":
                switch (effectName)
                {
                    case "CHARM": detail = GetMagicEffectDetail(MagicEffect.Charm, level); break;
                    case "PUSH": detail = GetMagicEffectDetail(MagicEffect.Push, level); break;
                    case "STUN": detail = GetMagicEffectDetail(MagicEffect.Stun, level); break;
                    case "CHAIN": detail = GetMagicEffectDetail(MagicEffect.Chain, level); break;
                    case "DOT": detail = GetMagicEffectDetail(MagicEffect.Dot, level); break;
                }
                break;
        }

        return detail;
    }

    private static readonly Dictionary<int, Dictionary<UpgradeCard.UpgradeDirection, string>> EvolvePrefixes = new()
    {
        {
            2, new Dictionary<UpgradeCard.UpgradeDirection, string>
            {
                { UpgradeCard.UpgradeDirection.Damage,   "锋利" },
                { UpgradeCard.UpgradeDirection.Speed,    "迅捷" },
                { UpgradeCard.UpgradeDirection.Crit,     "精准" },
                { UpgradeCard.UpgradeDirection.Leech,   "嗜血" },
                { UpgradeCard.UpgradeDirection.Defense, "坚固" },
                { UpgradeCard.UpgradeDirection.Effect,   "附魔" },
            }
        },
        {
            5, new Dictionary<UpgradeCard.UpgradeDirection, string>
            {
                { UpgradeCard.UpgradeDirection.Damage,   "狂暴" },
                { UpgradeCard.UpgradeDirection.Speed,    "疾风" },
                { UpgradeCard.UpgradeDirection.Crit,     "致命" },
                { UpgradeCard.UpgradeDirection.Leech,    "吸血" },
                { UpgradeCard.UpgradeDirection.Defense,  "铁壁" },
                { UpgradeCard.UpgradeDirection.Effect,   "奥术" },
            }
        },
        {
            8, new Dictionary<UpgradeCard.UpgradeDirection, string>
            {
                { UpgradeCard.UpgradeDirection.Damage,   "毁灭" },
                { UpgradeCard.UpgradeDirection.Speed,    "闪电" },
                { UpgradeCard.UpgradeDirection.Crit,     "审判" },
                { UpgradeCard.UpgradeDirection.Leech,    "不朽" },
                { UpgradeCard.UpgradeDirection.Defense,  "永恒" },
                { UpgradeCard.UpgradeDirection.Effect,   "传说" },
            }
        },
    };

    public void EvolveName()
    {
        if (BaseName == "")
            BaseName = CardName;

        var dominant = _GetDominantDirection();
        if (dominant == UpgradeCard.UpgradeDirection.None)
            return;

        string? prefixVal = null;
        int[] thresholds = { 8, 5, 2 };
        foreach (int t in thresholds)
        {
            if (UpgradeCount >= t && EvolvePrefixes.TryGetValue(t, out var tier) && tier.TryGetValue(dominant, out prefixVal))
            {
                break;
            }
        }

        if (prefixVal != null)
            CardName = $"{prefixVal}·{BaseName}";
    }

    private UpgradeCard.UpgradeDirection _GetDominantDirection()
    {
        var bestDir = UpgradeCard.UpgradeDirection.None;
        int bestCount = 0;

        foreach (long dir in UpgradeHistory.Keys)
        {
            int count = (int)UpgradeHistory[dir];
            if (count > bestCount)
            {
                bestCount = count;
                bestDir = (UpgradeCard.UpgradeDirection)(int)dir;
            }
        }

        return bestDir;
    }
}
