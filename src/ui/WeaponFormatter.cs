namespace ZombieWars.UI;

using Godot;
using Godot.NativeInterop;
using ZombieWars.Core;
using static ZombieWars.Core.GameEnums;
using ZombieWars.Player;
using ZombieWars.Weapons;
using Array = Godot.Collections.Array;
using Godot.Collections;

public static partial class WeaponFormatter
{
    public static string GetRarityText(int rarity)
    {
        return rarity switch
        {
            1 => "[普通]",
            2 => "[稀有]",
            3 => "[史诗]",
            4 => "[传说]",
            _ => "[未知]",
        };
    }

    public static string GetRarityColor(int rarity)
    {
        return rarity switch
        {
            2 => "#c0392b",
            3 => "#f39c12",
            4 => "#ffcc40",
            _ => "#9a8a8a"
        };
    }

    public static Color GetRarityColorValue(int rarity)
    {
        return rarity switch
        {
            2 => HandDrawnTheme.Colors.RarityRare,
            3 => HandDrawnTheme.Colors.RarityEpic,
            4 => new Color(1f, 0.8f, 0.25f),
            _ => new Color(0.6f, 0.54f, 0.54f)
        };
    }

    public static Array<string> GetEffectLevelSummary(Player player)
    {
        var meleeLevels = new Godot.Collections.Dictionary<int, int>();
        var rangedLevels = new Godot.Collections.Dictionary<int, int>();
        var magicLevels = new Godot.Collections.Dictionary<int, int>();

        foreach (WeaponCard weapon in player.ActiveEffects)
        {
            if (weapon.WeaponType == WeaponType.Melee)
            {
                foreach (Variant item in weapon.MeleeEffects)
                {
                    MeleeEffect effect = (MeleeEffect)(int)item;
                    int key = (int)effect;
                    int level = 1;
                    if (weapon.MeleeEffectLevels.TryGetValue(key, out Variant v))
                        level = v.AsInt32();
                    if (!meleeLevels.ContainsKey(key) || meleeLevels[key] < level)
                        meleeLevels[key] = level;
                }
            }
            else if (weapon.WeaponType == WeaponType.Ranged)
            {
                foreach (Variant item in weapon.RangedEffects)
                {
                    RangedEffect effect = (RangedEffect)(int)item;
                    int key = (int)effect;
                    int level = 1;
                    if (weapon.RangedEffectLevels.TryGetValue(key, out Variant v))
                        level = v.AsInt32();
                    if (!rangedLevels.ContainsKey(key) || rangedLevels[key] < level)
                        rangedLevels[key] = level;
                }
            }
            else if (weapon.WeaponType == WeaponType.Magic)
            {
                foreach (Variant item in weapon.MagicEffects)
                {
                    MagicEffect effect = (MagicEffect)(int)item;
                    int key = (int)effect;
                    int level = 1;
                    if (weapon.MagicEffectLevels.TryGetValue(key, out Variant v))
                        level = v.AsInt32();
                    if (!magicLevels.ContainsKey(key) || magicLevels[key] < level)
                        magicLevels[key] = level;
                }
            }
        }

        var result = new Array<string>();

        foreach (int effectKey in meleeLevels.Keys)
            result.Add(WeaponCard.GetMeleeEffectDesc((MeleeEffect)effectKey, meleeLevels[effectKey]));
        foreach (int effectKey in rangedLevels.Keys)
            result.Add(WeaponCard.GetRangedEffectDesc((RangedEffect)effectKey, rangedLevels[effectKey]));
        foreach (int effectKey in magicLevels.Keys)
            result.Add(WeaponCard.GetMagicEffectDesc((MagicEffect)effectKey, magicLevels[effectKey]));

        return result;
    }

    public static string GetEffectTooltip(WeaponCard weapon)
    {
        string text = "【" + weapon.CardName + "】\n";
        text += "类型: " + (weapon.Category == WeaponCategory.Equipment
            ? "装备" : weapon.GetWeaponTypeText()) + "\n";
        text += "等级: Lv." + weapon.Tier + "\n";
        text += "稀有度: " + GetRarityText(weapon.Rarity) + "\n\n";

        if (!Mathf.IsEqualApprox(weapon.DamageModifier, 1.0f))
        {
            int mod = (int)((weapon.DamageModifier - 1.0f) * 100);
            text += "伤害: " + (mod > 0 ? "+" : "") + mod + "%\n";
        }
        if (weapon.HealthModifier != 0)
            text += "生命: " + (weapon.HealthModifier > 0 ? "+" : "") + (int)weapon.HealthModifier + "\n";
        if (weapon.SpeedModifier != 0)
            text += "速度: " + (weapon.SpeedModifier > 0 ? "+" : "") + (int)weapon.SpeedModifier + "\n";
        if (weapon.CriticalBonus > 0)
            text += "暴击: +" + (int)weapon.CriticalBonus + "%\n";
        if (weapon.LifeStealBonus > 0)
            text += "吸血: +" + (int)weapon.LifeStealBonus + "%\n";

        return text;
    }

    public static string BuildActiveEffectsSummary(Player player)
    {
        if (player.ActiveEffects.Count == 0)
            return "";

        string currentWeapon = "无";
        var effects = GetEffectLevelSummary(player);

        foreach (WeaponCard effect in player.ActiveEffects)
        {
            if (effect.Category == WeaponCategory.Weapon)
            {
                currentWeapon = effect.CardName;
                break;
            }
        }

        if (effects.Count == 0)
            return "🎒 " + currentWeapon;

        return "🎒 " + currentWeapon + " | " + string.Join(" | ", effects);
    }

    public static string BuildActiveEffectsTooltip(Player player)
    {
        if (player.ActiveEffects.Count == 0)
            return "已获得: 无";

        string currentWeapon = "无";
        foreach (WeaponCard effect in player.ActiveEffects)
        {
            if (effect.Category == WeaponCategory.Weapon)
            {
                currentWeapon = effect.CardName;
                break;
            }
        }

        var effects = GetEffectLevelSummary(player);
        if (effects.Count == 0)
            return "当前武器: " + currentWeapon + "\n特效: 无";

        return "当前武器: " + currentWeapon + "\n特效:\n- " + string.Join("\n- ", effects);
    }

    public static string BuildAttackEffectsTooltip(Player player)
    {
        var lines = new Array<string>();

        string typeText = player.CurrentAttackType switch
        {
            AttackType.Ranged => "远程",
            AttackType.Magic => "魔法",
            AttackType.Area => "范围",
            _ => "近战",
        };

        string weaponName = "无";
        foreach (WeaponCard effect in player.ActiveEffects)
        {
            if (effect.Category == WeaponCategory.Weapon)
            {
                weaponName = effect.CardName;
                break;
            }
        }

        lines.Add("当前武器: " + weaponName);
        lines.Add("攻击类型: " + typeText);
        lines.Add("基础伤害: " + (int)player.CurrentDamage);

        var meleeLevels = new Godot.Collections.Dictionary<int, int>();
        var rangedLevels = new Godot.Collections.Dictionary<int, int>();
        var magicLevels = new Godot.Collections.Dictionary<int, int>();

        foreach (WeaponCard weapon in player.ActiveEffects)
        {
            if (weapon.WeaponType == WeaponType.Melee)
            {
                foreach (Variant item in weapon.MeleeEffects)
                {
                    MeleeEffect effect = (MeleeEffect)(int)item;
                    int key = (int)effect;
                    int level = 1;
                    if (weapon.MeleeEffectLevels.TryGetValue(key, out Variant v))
                        level = v.AsInt32();
                    if (!meleeLevels.ContainsKey(key) || meleeLevels[key] < level)
                        meleeLevels[key] = level;
                }
            }
            else if (weapon.WeaponType == WeaponType.Ranged)
            {
                foreach (Variant item in weapon.RangedEffects)
                {
                    RangedEffect effect = (RangedEffect)(int)item;
                    int key = (int)effect;
                    int level = 1;
                    if (weapon.RangedEffectLevels.TryGetValue(key, out Variant v))
                        level = v.AsInt32();
                    if (!rangedLevels.ContainsKey(key) || rangedLevels[key] < level)
                        rangedLevels[key] = level;
                }
            }
            else if (weapon.WeaponType == WeaponType.Magic)
            {
                foreach (Variant item in weapon.MagicEffects)
                {
                    MagicEffect effect = (MagicEffect)(int)item;
                    int key = (int)effect;
                    int level = 1;
                    if (weapon.MagicEffectLevels.TryGetValue(key, out Variant v))
                        level = v.AsInt32();
                    if (!magicLevels.ContainsKey(key) || magicLevels[key] < level)
                        magicLevels[key] = level;
                }
            }
        }

        var effectLines = new Array<string>();

        if (player.CurrentAttackType == AttackType.Melee)
        {
            foreach (int effectKey in meleeLevels.Keys)
                effectLines.Add("• " + WeaponCard.GetMeleeEffectDesc((MeleeEffect)effectKey, meleeLevels[effectKey]));
        }
        else if (player.CurrentAttackType == AttackType.Ranged)
        {
            foreach (int effectKey in rangedLevels.Keys)
                effectLines.Add("• " + WeaponCard.GetRangedEffectDesc((RangedEffect)effectKey, rangedLevels[effectKey]));
            if (player.ReturningProjectiles && player.BounceCount > 0)
                effectLines.Add("• 反弹次数: " + player.BounceCount);
        }
        else if (player.CurrentAttackType == AttackType.Magic)
        {
            foreach (int effectKey in magicLevels.Keys)
                effectLines.Add("• " + WeaponCard.GetMagicEffectDesc((MagicEffect)effectKey, magicLevels[effectKey]));
        }
        else if (player.CurrentAttackType == AttackType.Area)
        {
            effectLines.Add("• 范围爆炸");
        }

        if (player.CriticalChance > 0)
            effectLines.Add("• 暴击率 +" + (int)player.CriticalChance + "%");
        if (player.LifeStealPercent > 0)
            effectLines.Add("• 吸血 +" + (int)player.LifeStealPercent + "%");
        if (player.FreezeChance > 0)
            effectLines.Add("• 冰冻几率 +" + (int)player.FreezeChance + "%");
        if (player.PoisonChance > 0)
            effectLines.Add("• 中毒几率 +" + (int)player.PoisonChance + "%");

        if (effectLines.Count == 0)
            effectLines.Add("• 无特效");

        lines.Add("特效:");
        lines.Add(string.Join("\n", effectLines));
        return string.Join("\n", lines);
    }
}
