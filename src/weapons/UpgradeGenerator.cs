namespace ZombieWars.Weapons;

using System;
using System.Collections.Generic;
using Godot;
using ZombieWars.Core;
using static ZombieWars.Core.GameEnums;

public static partial class UpgradeGenerator
{
    private const int MAX_EFFECT_LEVEL = 5;

    private static readonly (string Name, float Min, float Max)[] _DIMENSIONS = {
        ("damage", 0.05f, 0.15f),
        ("attack_speed", 0.05f, 0.12f),
        ("health", 3.0f, 8.0f),
        ("speed", 3.0f, 8.0f),
        ("crit", 2.0f, 5.0f),
        ("life_steal", 1.0f, 3.0f),
        ("freeze", 2.0f, 5.0f),
        ("poison", 2.0f, 5.0f),
    };

    private static readonly (UpgradeCard.TradeoffType, float, float)[] _TRADEOFFS = {
        (UpgradeCard.TradeoffType.HealthCost, 1.0f, 3.0f),
        (UpgradeCard.TradeoffType.Speed, -2.0f, -5.0f),
        (UpgradeCard.TradeoffType.HealthMax, -3.0f, -8.0f),
    };

    public static UpgradeCard Generate(WeaponCard target)
    {
        var card = new UpgradeCard
        {
            TargetRef = target,
            DisplayName = "强化·" + target.CardName
        };

        int dimCount = GD.Randf() < 0.6f ? 1 : 2;

        if (GD.Randf() < 0.4f && _HasEffects(target))
        {
            _UpgradeRandomEffect(target, card);
            dimCount = Math.Max(1, dimCount - 1);
        }

        var chosenDims = _PickDimensions(target, dimCount);

        foreach (var dim in chosenDims)
            _ApplyDimension(card, dim);

        if (GD.Randf() < 0.3f)
            _ApplyTradeoff(card);

        card.UpgradeDirection_ = _DetermineDirection(card);
        card.Rarity = dimCount >= 2 ? 2 : 1;

        return card;
    }

    private static List<(string Stat, float Min, float Max)> _PickDimensions(WeaponCard target, int count)
    {
        var weighted = new List<(string Stat, float Min, float Max, float Weight)>();
        foreach (var dim in _DIMENSIONS)
        {
            float weight = 1.0f;
            switch (dim.Name)
            {
                case "damage":
                    if (target.DamageModifier > 1.0f) weight = 2.0f;
                    break;
                case "attack_speed":
                    if (target.AttackSpeedModifier > 1.0f) weight = 2.0f;
                    break;
                case "crit":
                    if (target.CriticalBonus > 0) weight = 2.0f;
                    break;
                case "life_steal":
                    if (target.LifeStealBonus > 0) weight = 2.0f;
                    break;
                case "freeze":
                    if (target.FreezeBonus > 0) weight = 2.0f;
                    break;
                case "poison":
                    if (target.PoisonBonus > 0) weight = 2.0f;
                    break;
            }
            weighted.Add((dim.Name, dim.Min, dim.Max, weight));
        }

        var chosen = new List<(string, float, float)>();
        for (int i = 0; i < count && weighted.Count > 0; i++)
        {
            var pick = _WeightedPick(weighted);
            chosen.Add((pick.Stat, pick.Min, pick.Max));
            weighted.RemoveAll(w => w.Stat == pick.Stat);
        }
        return chosen;
    }

    private static void _ApplyDimension(UpgradeCard card, (string, float, float) dim)
    {
        string statName = dim.Item1;
        float minVal = dim.Item2;
        float maxVal = dim.Item3;
        float val = (float)GD.RandRange(minVal, maxVal);

        switch (statName)
        {
            case "damage":
                card.DamageDelta = val;
                break;
            case "attack_speed":
                card.AttackSpeedDelta = val;
                break;
            case "health":
                card.HealthDelta = val;
                break;
            case "speed":
                card.SpeedDelta = val;
                break;
            case "crit":
                card.CritDelta = val;
                break;
            case "life_steal":
                card.LifeStealDelta = val;
                break;
            case "freeze":
                card.FreezeDelta = val;
                break;
            case "poison":
                card.PoisonDelta = val;
                break;
        }
    }

    private static bool _HasEffects(WeaponCard target)
    {
        return target.MeleeEffects.Count > 0
            || target.RangedEffects.Count > 0
            || target.MagicEffects.Count > 0;
    }

    private static void _UpgradeRandomEffect(WeaponCard target, UpgradeCard card)
    {
        var candidates = new List<(int EffectKey, string Type, int Current)>();

        foreach (Variant ve in target.MeleeEffects)
        {
            int ek = (int)ve;
            int lv = target.MeleeEffectLevels.ContainsKey(ek)
                ? (int)target.MeleeEffectLevels[ek] : 0;
            if (lv < MAX_EFFECT_LEVEL)
                candidates.Add((ek, "melee", lv));
        }

        foreach (Variant ve in target.RangedEffects)
        {
            int ek = (int)ve;
            int lv = target.RangedEffectLevels.ContainsKey(ek)
                ? (int)target.RangedEffectLevels[ek] : 0;
            if (lv < MAX_EFFECT_LEVEL)
                candidates.Add((ek, "ranged", lv));
        }

        foreach (Variant ve in target.MagicEffects)
        {
            int ek = (int)ve;
            int lv = target.MagicEffectLevels.ContainsKey(ek)
                ? (int)target.MagicEffectLevels[ek] : 0;
            if (lv < MAX_EFFECT_LEVEL)
                candidates.Add((ek, "magic", lv));
        }

        if (candidates.Count == 0)
            return;

        var pick = candidates[(int)(GD.Randi() % candidates.Count)];
        int effectKey = pick.EffectKey;
        int current = card.EffectUpgrades.ContainsKey(effectKey) ? (int)card.EffectUpgrades[effectKey] : 0;
        card.EffectUpgrades[effectKey] = current + 1;
    }

    private static void _ApplyTradeoff(UpgradeCard card)
    {
        var t = _TRADEOFFS[(int)(GD.Randi() % _TRADEOFFS.Length)];
        card.TradeoffKind = t.Item1;
        card.TradeoffValue = (float)GD.RandRange(t.Item2, t.Item3);
    }

    private static UpgradeCard.UpgradeDirection _DetermineDirection(UpgradeCard card)
    {
        if (card.DamageDelta > 0)
            return UpgradeCard.UpgradeDirection.Damage;
        if (card.AttackSpeedDelta > 0 || card.SpeedDelta > 0)
            return UpgradeCard.UpgradeDirection.Speed;
        if (card.CritDelta > 0)
            return UpgradeCard.UpgradeDirection.Crit;
        if (card.LifeStealDelta > 0)
            return UpgradeCard.UpgradeDirection.Leech;
        if (card.HealthDelta > 0)
            return UpgradeCard.UpgradeDirection.Defense;
        if (card.EffectUpgrades.Count > 0 || card.FreezeDelta > 0 || card.PoisonDelta > 0)
            return UpgradeCard.UpgradeDirection.Effect;
        return UpgradeCard.UpgradeDirection.Damage;
    }

    private static (string Stat, float Min, float Max) _WeightedPick(
        List<(string Stat, float Min, float Max, float Weight)> items)
    {
        float total = 0.0f;
        foreach (var item in items)
            total += item.Weight;

        float roll = GD.Randf() * total;
        float cumulative = 0.0f;
        foreach (var item in items)
        {
            cumulative += item.Weight;
            if (roll <= cumulative)
                return (item.Stat, item.Min, item.Max);
        }
        return (items[^1].Stat, items[^1].Min, items[^1].Max);
    }
}
