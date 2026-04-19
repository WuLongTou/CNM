namespace ZombieWars.Weapons;

using System.Collections.Generic;
using static ZombieWars.Core.GameEnums;

/// <summary>
/// Data-driven effect definitions - replaces switch statements with table-driven approach.
/// </summary>
public static class EffectData
{
    public record EffectInfo(string Name, string Description, string DetailFormat);

    private static readonly Dictionary<MeleeEffect, EffectInfo> MeleeEffects = new()
    {
        { MeleeEffect.Splash, new("溅射", "范围伤害", "半径{0}的范围伤害") },
        { MeleeEffect.MultiTarget, new("多目标", "攻击多个敌人", "最多{0}个目标") },
        { MeleeEffect.KillExplosion, new("击杀爆炸", "击杀时爆炸", "爆炸半径{0}") },
        { MeleeEffect.KillShield, new("击杀护盾", "击杀时获得护盾", "获得{0}点护盾") },
        { MeleeEffect.Knockback, new("击退", "推开敌人", "击退距离{0}") },
    };

    private static readonly Dictionary<RangedEffect, EffectInfo> RangedEffects = new()
    {
        { RangedEffect.MultiShot, new("多重射击", "发射多颗子弹", "发射{0}颗子弹") },
        { RangedEffect.TrajectoryDamage, new("弹道伤害", "沿弹道造成伤害", "额外{0}%伤害") },
        { RangedEffect.Returning, new("回旋", "子弹返回", "返回时再次造成伤害") },
        { RangedEffect.Piercing, new("贯穿", "穿透敌人", "穿透{0}个敌人") },
    };

    private static readonly Dictionary<MagicEffect, EffectInfo> MagicEffects = new()
    {
        { MagicEffect.Charm, new("魅惑", "控制敌人", "控制{0}秒") },
        { MagicEffect.Push, new("击退", "推开敌人", "击退距离{0}") },
        { MagicEffect.Stun, new("眩晕", "使敌人眩晕", "眩晕{0}秒") },
        { MagicEffect.Chain, new("连锁", "连锁伤害", "最多{0}个目标") },
        { MagicEffect.Dot, new("持续伤害", "持续毒伤", "每秒{0}伤害，持续{1}秒") },
    };

    public static EffectInfo? GetMeleeEffectInfo(MeleeEffect effect) =>
        MeleeEffects.GetValueOrDefault(effect);

    public static EffectInfo? GetRangedEffectInfo(RangedEffect effect) =>
        RangedEffects.GetValueOrDefault(effect);

    public static EffectInfo? GetMagicEffectInfo(MagicEffect effect) =>
        MagicEffects.GetValueOrDefault(effect);

    public static string FormatMeleeEffect(MeleeEffect effect, int level)
    {
        if (effect == MeleeEffect.None || !MeleeEffects.TryGetValue(effect, out var info))
            return "无效果";

        return effect switch
        {
            MeleeEffect.Splash => $"范围伤害，半径{30 + level * 10}",
            MeleeEffect.MultiTarget => $"攻击{1 + level}个目标",
            MeleeEffect.KillExplosion => $"击杀时爆炸，半径{50 + level * 10}",
            MeleeEffect.KillShield => $"击杀获得{5 + level * 5}护盾",
            MeleeEffect.Knockback => $"击退距离{20 + level * 5}",
            _ => info.Description
        };
    }

    public static string FormatRangedEffect(RangedEffect effect, int level)
    {
        if (effect == RangedEffect.None || !RangedEffects.TryGetValue(effect, out var info))
            return "无效果";

        return effect switch
        {
            RangedEffect.MultiShot => $"发射{1 + level}颗子弹",
            RangedEffect.TrajectoryDamage => $"弹道额外{20 + level * 10}%伤害",
            RangedEffect.Returning => "子弹返回时再次伤害",
            RangedEffect.Piercing => $"穿透{level}个敌人",
            _ => info.Description
        };
    }

    public static string FormatMagicEffect(MagicEffect effect, int level)
    {
        if (effect == MagicEffect.None || !MagicEffects.TryGetValue(effect, out var info))
            return "无效果";

        return effect switch
        {
            MagicEffect.Charm => $"控制{1 + level * 0.5f}秒",
            MagicEffect.Push => $"击退{30 + level * 10}距离",
            MagicEffect.Stun => $"眩晕{0.5f + level * 0.2f}秒",
            MagicEffect.Chain => $"连锁{1 + level}个目标",
            MagicEffect.Dot => $"每秒{2 + level}伤害，持续{3 + level}秒",
            _ => info.Description
        };
    }
}
