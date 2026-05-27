namespace ZombieWars.Weapons;

using System;
using System.Collections.Generic;
using Godot;
using ZombieWars.Core;
using static ZombieWars.Core.GameEnums;

/// <summary>
/// Weapon evolution and fusion system.
/// Handles weapon combination recipes, ascension upgrades, dual-wielding,
/// and ultimate form transformations.
/// Attach to WeaponDeck or a dedicated system node.
/// </summary>
public partial class WeaponEvolutionSystem : Node
{
    // ═══════════════════════════════════════════════════
    // Evolution Types
    // ═══════════════════════════════════════════════════

    public enum EvolutionType
    {
        Fusion,   // Combine two weapons
        Ascension,// Upgrade existing weapon with special properties
        Duality,  // Unlock dual-wielding for the weapon
        Ultimate  // Transform into ultimate weapon form
    }

    // ═══════════════════════════════════════════════════
    // State
    // ═══════════════════════════════════════════════════

    public static readonly int AscensionThreshold = 3; // upgrades needed to evolve
    public static readonly int UltimateThreshold = 6;

    // ═══════════════════════════════════════════════════
    // Fusion Recipe Registry
    // Format: (WeaponName1 + WeaponName2) → ResultWeaponName
    // ═══════════════════════════════════════════════════

    private static readonly Dictionary<(string, string), FusionRecipe> FusionRecipes = new()
    {
        {
            ("战斧", "冰霜法杖"),
            new FusionRecipe(
                "元素双刃剑",
                "融合了烈焰与寒冰之力的双持武器，攻击附带火与冰的双重效果",
                WeaponType.Melee,
                new MeleeEffect[] { MeleeEffect.Splash },
                new MagicEffect[] { MagicEffect.Dot },
                new Godot.Collections.Dictionary<long, int>
                {
                    { (long)MeleeEffect.Splash, 2 },
                    { (long)MagicEffect.Dot, 2 }
                },
                "战斧",
                "冰霜法杖",
                5
            )
        },
        {
            ("重弩", "狙击弩"),
            new FusionRecipe(
                "弹幕加农炮",
                "发射多发穿透弹，每发子弹都可贯穿多个敌人",
                WeaponType.Ranged,
                new[] { RangedEffect.MultiShot, RangedEffect.Piercing },
                new Godot.Collections.Dictionary<long, int>
                {
                    { (long)RangedEffect.MultiShot, 2 },
                    { (long)RangedEffect.Piercing, 2 }
                },
                "重弩",
                "狙击弩",
                5
            )
        },
        {
            ("雷电法杖", "魅惑法杖"),
            new FusionRecipe(
                "心灵控制法杖",
                "连锁闪电在敌人间跳跃，最后一个目标会被魅惑",
                WeaponType.Magic,
                new MagicEffect[] { MagicEffect.Chain, MagicEffect.Charm },
                new Godot.Collections.Dictionary<long, int>
                {
                    { (long)MagicEffect.Chain, 2 },
                    { (long)MagicEffect.Charm, 1 }
                },
                "雷电法杖",
                "魅惑法杖",
                5
            )
        },
        {
            ("巨斧", "长枪"),
            new FusionRecipe(
                "毁灭之锤",
                "毁灭之锤：超大面积溅射并击退所有命中敌人",
                WeaponType.Melee,
                new MeleeEffect[] { MeleeEffect.Splash, MeleeEffect.Knockback },
                new Godot.Collections.Dictionary<long, int>
                {
                    { (long)MeleeEffect.Splash, 3 },
                    { (long)MeleeEffect.Knockback, 2 }
                },
                "巨斧",
                "长枪",
                5
            )
        },
        {
            ("双刀", "淬毒短刀"),
            new FusionRecipe(
                "致命之刃",
                "致命之刃：暴击时吸取敌人生命，击杀回复大量生命",
                WeaponType.Melee,
                new MeleeEffect[] { MeleeEffect.KillShield, MeleeEffect.KillExplosion },
                new Godot.Collections.Dictionary<long, int>
                {
                    { (long)MeleeEffect.KillShield, 2 },
                    { (long)MeleeEffect.KillExplosion, 2 }
                },
                "双刀",
                "淬毒短刀",
                5
            )
        },
        {
            ("冰霜护盾", "火球法杖"),
            new FusionRecipe(
                "瘟疫领域法杖",
                "瘟疫领域：对范围内所有敌人持续造成伤害",
                WeaponType.Magic,
                new MagicEffect[] { MagicEffect.Dot, MagicEffect.Stun },
                new Godot.Collections.Dictionary<long, int>
                {
                    { (long)MagicEffect.Dot, 3 },
                    { (long)MagicEffect.Stun, 1 }
                },
                "冰霜护盾",
                "火球法杖",
                5
            )
        },
        {
            ("战斧", "战斧"),
            new FusionRecipe(
                "炽焰大剑",
                "传说中的炽焰大剑，攻击引发大范围火焰爆炸",
                WeaponType.Melee,
                new MeleeEffect[] { MeleeEffect.KillExplosion, MeleeEffect.Splash },
                new Godot.Collections.Dictionary<long, int>
                {
                    { (long)MeleeEffect.KillExplosion, 3 },
                    { (long)MeleeEffect.Splash, 2 }
                },
                "战斧",
                "战斧",
                6
            )
        },
        {
            ("重弩", "重弩"),
            new FusionRecipe(
                "弹幕加农",
                "同时发射6发子弹形成弹幕覆盖",
                WeaponType.Ranged,
                new RangedEffect[] { RangedEffect.MultiShot, RangedEffect.TrajectoryDamage },
                new Godot.Collections.Dictionary<long, int>
                {
                    { (long)RangedEffect.MultiShot, 3 },
                    { (long)RangedEffect.TrajectoryDamage, 2 }
                },
                "重弩",
                "重弩",
                6
            )
        },
        {
            ("雷电法杖", "雷电法杖"),
            new FusionRecipe(
                "雷霆之神杖",
                "连锁闪电跳跃10次，每次伤害递增",
                WeaponType.Magic,
                new MagicEffect[] { MagicEffect.Chain, MagicEffect.Dot },
                new Godot.Collections.Dictionary<long, int>
                {
                    { (long)MagicEffect.Chain, 3 },
                    { (long)MagicEffect.Dot, 2 }
                },
                "雷电法杖",
                "雷电法杖",
                6
            )
        },
        {
            ("冰霜法杖", "寒霜徽章"),
            new FusionRecipe(
                "绝对零度之杖",
                "冻结所有命中敌人3秒，范围极大",
                WeaponType.Magic,
                new[] { MagicEffect.Stun, MagicEffect.Push },
                new Godot.Collections.Dictionary<long, int>
                {
                    { (long)MagicEffect.Stun, 3 },
                    { (long)MagicEffect.Push, 2 }
                },
                "冰霜法杖",
                "寒霜徽章",
                5
            )
        }
    };

    // ═══════════════════════════════════════════════════
    // Fusion Recipe Data
    // ═══════════════════════════════════════════════════

    private class FusionRecipe
    {
        public string ResultName { get; }
        public string ResultDescription { get; }
        public WeaponType ResultType { get; }
        public MeleeEffect[] MeleeEffects { get; }
        public RangedEffect[] RangedEffects { get; }
        public MagicEffect[] MagicEffects { get; }
        public Godot.Collections.Dictionary<long, int> EffectLevels { get; }
        public string Weapon1Tag { get; }
        public string Weapon2Tag { get; }
        public int Tier { get; }

        public FusionRecipe(string name, string desc, WeaponType type,
            MeleeEffect[] melee,
            Godot.Collections.Dictionary<long, int> levels,
            string w1, string w2, int tier)
        {
            ResultName = name;
            ResultDescription = desc;
            ResultType = type;
            MeleeEffects = melee ?? Array.Empty<MeleeEffect>();
            RangedEffects = Array.Empty<RangedEffect>();
            MagicEffects = Array.Empty<MagicEffect>();
            EffectLevels = levels;
            Weapon1Tag = w1;
            Weapon2Tag = w2;
            Tier = tier;
        }

        public FusionRecipe(string name, string desc, WeaponType type,
            MeleeEffect[] melee, MagicEffect[] magic,
            Godot.Collections.Dictionary<long, int> levels,
            string w1, string w2, int tier)
        {
            ResultName = name;
            ResultDescription = desc;
            ResultType = type;
            MeleeEffects = melee ?? Array.Empty<MeleeEffect>();
            RangedEffects = Array.Empty<RangedEffect>();
            MagicEffects = magic ?? Array.Empty<MagicEffect>();
            EffectLevels = levels;
            Weapon1Tag = w1;
            Weapon2Tag = w2;
            Tier = tier;
        }

        public FusionRecipe(string name, string desc, WeaponType type,
            RangedEffect[] ranged, MagicEffect[] magic,
            Godot.Collections.Dictionary<long, int> levels,
            string w1, string w2, int tier)
        {
            ResultName = name;
            ResultDescription = desc;
            ResultType = type;
            MeleeEffects = Array.Empty<MeleeEffect>();
            RangedEffects = ranged ?? Array.Empty<RangedEffect>();
            MagicEffects = magic ?? Array.Empty<MagicEffect>();
            EffectLevels = levels;
            Weapon1Tag = w1;
            Weapon2Tag = w2;
            Tier = tier;
        }

        public FusionRecipe(string name, string desc, WeaponType type,
            RangedEffect[] ranged,
            Godot.Collections.Dictionary<long, int> levels,
            string w1, string w2, int tier)
        {
            ResultName = name;
            ResultDescription = desc;
            ResultType = type;
            MeleeEffects = Array.Empty<MeleeEffect>();
            RangedEffects = ranged ?? Array.Empty<RangedEffect>();
            MagicEffects = Array.Empty<MagicEffect>();
            EffectLevels = levels;
            Weapon1Tag = w1;
            Weapon2Tag = w2;
            Tier = tier;
        }

        public FusionRecipe(string name, string desc, WeaponType type,
            MagicEffect[] magic,
            Godot.Collections.Dictionary<long, int> levels,
            string w1, string w2, int tier)
        {
            ResultName = name;
            ResultDescription = desc;
            ResultType = type;
            MeleeEffects = Array.Empty<MeleeEffect>();
            RangedEffects = Array.Empty<RangedEffect>();
            MagicEffects = magic ?? Array.Empty<MagicEffect>();
            EffectLevels = levels;
            Weapon1Tag = w1;
            Weapon2Tag = w2;
            Tier = tier;
        }

        public FusionRecipe(string name, string desc, WeaponType type,
            MeleeEffect[] melee, RangedEffect[] ranged, MagicEffect[] magic,
            Godot.Collections.Dictionary<long, int> levels,
            string w1, string w2, int tier)
        {
            ResultName = name;
            ResultDescription = desc;
            ResultType = type;
            MeleeEffects = melee ?? Array.Empty<MeleeEffect>();
            RangedEffects = ranged ?? Array.Empty<RangedEffect>();
            MagicEffects = magic ?? Array.Empty<MagicEffect>();
            EffectLevels = levels;
            Weapon1Tag = w1;
            Weapon2Tag = w2;
            Tier = tier;
        }
    }

    // ═══════════════════════════════════════════════════
    // Fusion API
    // ═══════════════════════════════════════════════════

    /// <summary>Check if two weapons have a valid fusion recipe.</summary>
    public bool CanFuse(WeaponCard w1, WeaponCard w2)
    {
        if (w1 == null || w2 == null) return false;
        var key = _MakeKey(w1.CardName, w2.CardName);
        return FusionRecipes.ContainsKey(key);
    }

    /// <summary>Attempt to fuse two weapons. Returns null if no recipe found.</summary>
    public WeaponCard? Fuse(WeaponCard w1, WeaponCard w2)
    {
        if (w1 == null || w2 == null) return null;

        var key = _MakeKey(w1.CardName, w2.CardName);
        if (!FusionRecipes.TryGetValue(key, out var recipe))
            return null;

        var result = new WeaponCard();
        if (result == null) return null;

        result.CardName = recipe.ResultName;
        result.Description = recipe.ResultDescription;
        result.BaseName = recipe.ResultName;
        result.WeaponType = recipe.ResultType;
        result.Category = WeaponCategory.Weapon;
        result.Tier = recipe.Tier;
        result.Rarity = recipe.Tier;
        result.UpgradeCount = 0;
        result.Icon = _GetIconForType(recipe.ResultType);
        result.Tags = new Godot.Collections.Array { recipe.Weapon1Tag, recipe.Weapon2Tag, "FUSED" };

        // Apply effects from recipe
        foreach (var effect in recipe.MeleeEffects)
            result.MeleeEffects.Add((int)effect);
        foreach (var effect in recipe.RangedEffects)
            result.RangedEffects.Add((int)effect);
        foreach (var effect in recipe.MagicEffects)
            result.MagicEffects.Add((int)effect);
        foreach (var kvp in recipe.EffectLevels)
            result.MeleeEffectLevels[(Variant)(int)kvp.Key] = (Variant)kvp.Value;

        // Inherit stats from stronger parent
        result.DamageModifier = Mathf.Max(w1.DamageModifier, w2.DamageModifier) * 1.2f;
        result.AttackSpeedModifier = Mathf.Max(w1.AttackSpeedModifier, w2.AttackSpeedModifier);
        result.RangeModifier = Mathf.Max(w1.RangeModifier, w2.RangeModifier);
        result.CriticalBonus = Mathf.Max(w1.CriticalBonus, w2.CriticalBonus);
        result.LifeStealBonus = Mathf.Max(w1.LifeStealBonus, w2.LifeStealBonus);

        LogUtils.Print($"武器融合: {w1.CardName} + {w2.CardName} → {result.CardName}");
        return result;
    }

    // ═══════════════════════════════════════════════════
    // Evolution API
    // ═══════════════════════════════════════════════════

    /// <summary>Check if a weapon has reached the ascension threshold.</summary>
    public bool CanEvolve(WeaponCard weapon, int upgradeCount)
    {
        if (weapon == null) return false;
        return upgradeCount >= AscensionThreshold;
    }

    /// <summary>Check if a weapon has reached the ultimate transformation threshold.</summary>
    public bool CanTransformUltimate(WeaponCard weapon, int upgradeCount)
    {
        if (weapon == null) return false;
        return upgradeCount >= UltimateThreshold;
    }

    /// <summary>Evolve a weapon to its ascension form.</summary>
    public WeaponCard? Evolve(WeaponCard weapon)
    {
        if (weapon == null) return null;
        if (!CanEvolve(weapon, weapon.UpgradeCount)) return null;

        // Ascension: boost all existing effects by 1 level
        var evolved = new WeaponCard();
        if (evolved == null) return null;

        _CopyWeaponCard(weapon, evolved);

        evolved.Tier = weapon.Tier + 1;
        evolved.Rarity = Mathf.Min(evolved.Rarity + 1, 5);
        evolved.CardName = $"[ASCENDED] {weapon.CardName}";
        evolved.BaseName = weapon.BaseName != "" ? weapon.BaseName : weapon.CardName;
        evolved.Description = weapon.Description + "\n【进阶】所有效果增强！";

        // Boost effect levels
        _BoostEffectLevels(evolved, 1);
        evolved.DamageModifier *= 1.25f;
        evolved.AttackSpeedModifier *= 1.15f;

        LogUtils.Print($"武器进阶: {weapon.CardName} → {evolved.CardName}");
        return evolved;
    }

    /// <summary>Transform a highly-upgraded weapon into its ultimate form.</summary>
    public WeaponCard? TransformUltimate(WeaponCard weapon)
    {
        if (weapon == null) return null;
        if (!CanTransformUltimate(weapon, weapon.UpgradeCount)) return null;

        var ult = new WeaponCard();
        if (ult == null) return null;

        _CopyWeaponCard(weapon, ult);

        ult.Tier = 7;
        ult.Rarity = 5;
        ult.CardName = $"★终极★ {weapon.CardName}";
        ult.BaseName = weapon.BaseName != "" ? weapon.BaseName : weapon.CardName;
        ult.Description = weapon.Description
            + $"\n【究极形态】基础伤害 ×2.0，攻速 ×1.5，所有效果达到最高等级！";

        _BoostEffectLevels(ult, 2);
        ult.DamageModifier *= 2.0f;
        ult.AttackSpeedModifier *= 1.5f;
        ult.CriticalBonus = Mathf.Min(ult.CriticalBonus + 20.0f, 75.0f);
        ult.LifeStealBonus = Mathf.Min(ult.LifeStealBonus + 15.0f, 50.0f);
        ult.Tags.Add("ULTIMATE");

        LogUtils.Print($"武器究极化: {weapon.CardName} → {ult.CardName}");
        return ult;
    }

    // ═══════════════════════════════════════════════════
    // Dual Wielding
    // ═══════════════════════════════════════════════════

    /// <summary>Create a dual-wield version of a weapon. Returns two cards.</summary>
    public (WeaponCard left, WeaponCard right)? CreateDualWield(WeaponCard weapon)
    {
        if (weapon == null) return null;

        var left = new WeaponCard();
        var right = new WeaponCard();
        if (left == null || right == null) return null;

        _CopyWeaponCard(weapon, left);
        _CopyWeaponCard(weapon, right);

        left.CardName = $"{weapon.CardName} (Left)";
        left.Description = weapon.Description + "\n【双持】左手武器，攻击速度+30%";
        left.AttackSpeedModifier *= 1.3f;
        left.Tags.Add("DUAL_LEFT");

        right.CardName = $"{weapon.CardName} (Right)";
        right.Description = weapon.Description + "\n【双持】右手武器，伤害+20%";
        right.DamageModifier *= 1.2f;
        right.Tags.Add("DUAL_RIGHT");

        return (left, right);
    }

    // ═══════════════════════════════════════════════════
    // Recipe Discovery
    // ═══════════════════════════════════════════════════

    /// <summary>Return all known fusion recipes as readable strings for the UI.</summary>
    public string[] GetAllRecipes()
    {
        var recipes = new Godot.Collections.Array<string>();
        foreach (var kvp in FusionRecipes)
        {
            recipes.Add($"{kvp.Value.Weapon1Tag} + {kvp.Value.Weapon2Tag} → {kvp.Value.ResultName}");
        }
        var arr = new string[recipes.Count];
        recipes.CopyTo(arr, 0);
        return arr;
    }

    /// <summary>Check if a fusion result exists for the given weapon pair.</summary>
    public string? GetFusionResultName(WeaponCard w1, WeaponCard w2)
    {
        if (w1 == null || w2 == null) return null;
        var key = _MakeKey(w1.CardName, w2.CardName);
        return FusionRecipes.TryGetValue(key, out var recipe) ? recipe.ResultName : null;
    }

    // ═══════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════

    private static (string, string) _MakeKey(string a, string b)
    {
        // Normalize: sort alphabetically so (A,B) and (B,A) map to the same key
        return string.Compare(a, b, StringComparison.Ordinal) <= 0
            ? (a, b)
            : (b, a);
    }

    private void _CopyWeaponCard(WeaponCard from, WeaponCard to)
    {
        to.CardName = from.CardName;
        to.BaseName = from.BaseName;
        to.Description = from.Description;
        to.WeaponType = from.WeaponType;
        to.Category = from.Category;
        to.Tier = from.Tier;
        to.Rarity = from.Rarity;
        to.DamageModifier = from.DamageModifier;
        to.AttackSpeedModifier = from.AttackSpeedModifier;
        to.RangeModifier = from.RangeModifier;
        to.HealthModifier = from.HealthModifier;
        to.SpeedModifier = from.SpeedModifier;
        to.Cooldown = from.Cooldown;
        to.HealthCost = from.HealthCost;
        to.CriticalBonus = from.CriticalBonus;
        to.LifeStealBonus = from.LifeStealBonus;
        to.FreezeBonus = from.FreezeBonus;
        to.PoisonBonus = from.PoisonBonus;
        to.GrantsAura = from.GrantsAura;
        to.TargetPriority = from.TargetPriority;
        to.Icon = from.Icon;
        to.Duration = from.Duration;
        to.Tags = new Godot.Collections.Array(from.Tags);
        to.UpgradeCount = from.UpgradeCount;
        to.UpgradeHistory = new Godot.Collections.Dictionary();
        foreach (var kv in from.UpgradeHistory)
            to.UpgradeHistory[kv.Key] = kv.Value;
        to.MeleeEffects = new Godot.Collections.Array(from.MeleeEffects);
        to.RangedEffects = new Godot.Collections.Array(from.RangedEffects);
        to.MagicEffects = new Godot.Collections.Array(from.MagicEffects);
        to.MeleeEffectLevels = new Godot.Collections.Dictionary();
        foreach (var kv in from.MeleeEffectLevels)
            to.MeleeEffectLevels[kv.Key] = kv.Value;
        to.RangedEffectLevels = new Godot.Collections.Dictionary();
        foreach (var kv in from.RangedEffectLevels)
            to.RangedEffectLevels[kv.Key] = kv.Value;
        to.MagicEffectLevels = new Godot.Collections.Dictionary();
        foreach (var kv in from.MagicEffectLevels)
            to.MagicEffectLevels[kv.Key] = kv.Value;
    }

    private void _BoostEffectLevels(WeaponCard weapon, int boost)
    {
        foreach (long key in weapon.MeleeEffectLevels.Keys)
        {
            weapon.MeleeEffectLevels[key] = (int)weapon.MeleeEffectLevels[key] + boost;
        }
        foreach (long key in weapon.RangedEffectLevels.Keys)
        {
            weapon.RangedEffectLevels[key] = (int)weapon.RangedEffectLevels[key] + boost;
        }
        foreach (long key in weapon.MagicEffectLevels.Keys)
        {
            weapon.MagicEffectLevels[key] = (int)weapon.MagicEffectLevels[key] + boost;
        }
    }

    private static string _GetIconForType(WeaponType type) =>
        type switch
        {
            WeaponType.Melee => "⚔️",
            WeaponType.Ranged => "🏹",
            WeaponType.Magic => "🔮",
            _ => "❓"
        };
}
