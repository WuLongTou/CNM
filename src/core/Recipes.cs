namespace ZombieWars.Core;

using System;
using System.Collections.Generic;
using Godot;
using ZombieWars.Weapons;
using static ZombieWars.Core.GameEnums;
using PlayerClass = ZombieWars.Player.Player;

/// <summary>合成配方（饥荒式）：消耗资源 → 产出装备（复用 WeaponCard）或消耗品效果。</summary>
public class Recipe
{
    public string Id = "";
    public string Name = "";
    public string Description = "";
    public Dictionary<ItemId, int> Cost = new();
    public Color Accent = new(0.8f, 0.6f, 0.3f);
    public WeaponCard? Weapon;            // 装备产出（合成后 ApplyWeaponEffect）
    public Action<PlayerClass>? Consume;  // 消耗品产出

    public bool CanCraft()
    {
        foreach (var kv in Cost)
            if (Inventory.Get(kv.Key) < kv.Value) return false;
        return true;
    }

    public void Craft(PlayerClass player)
    {
        if (!Inventory.TryConsume(Cost)) return;
        if (Weapon != null) player.ApplyWeaponEffect(Weapon);
        Consume?.Invoke(player);
    }
}

/// <summary>配方库 —— 工具/武器/装备/消耗品。合成台 UI 读取此列表。</summary>
public static class RecipeDatabase
{
    public static readonly List<Recipe> All = new()
    {
        new() { Id="wood_sword",   Name="木剑",   Description="近战 · 伤害 +30%",
                Cost=new(){{ItemId.Wood,3}}, Accent=new(0.55f,0.36f,0.18f),
                Weapon=_Weapon("木剑", WeaponType.Melee, dmg:1.3f, icon:"剑") },
        new() { Id="stone_axe",    Name="石斧",   Description="近战 · 伤害 +60% · 攻速 +20%",
                Cost=new(){{ItemId.Wood,3},{ItemId.Stone,2}}, Accent=new(0.62f,0.60f,0.57f),
                Weapon=_Weapon("石斧", WeaponType.Melee, dmg:1.6f, atkSpeed:1.2f, icon:"斧") },
        new() { Id="wood_bow",     Name="木弓",   Description="切换为远程射击",
                Cost=new(){{ItemId.Wood,4},{ItemId.Fiber,1}}, Accent=new(0.40f,0.55f,0.25f),
                Weapon=_Weapon("木弓", WeaponType.Ranged, icon:"弓") },
        new() { Id="magic_staff",  Name="魔法杖", Description="切换为魔法攻击",
                Cost=new(){{ItemId.Wood,2},{ItemId.Berry,2}}, Accent=new(0.5f,0.3f,0.8f),
                Weapon=_Weapon("魔法杖", WeaponType.Magic, icon:"杖") },
        new() { Id="leather",      Name="皮甲",   Description="+30 生命上限",
                Cost=new(){{ItemId.Fiber,3},{ItemId.Wood,2}}, Accent=new(0.5f,0.35f,0.2f),
                Weapon=_Equip("皮甲", "护甲", hp:30) },
        new() { Id="poultice",     Name="草药膏", Description="立即回复 50 生命",
                Cost=new(){{ItemId.Berry,2},{ItemId.Fiber,1}}, Accent=new(0.80f,0.15f,0.20f),
                Consume=p => p.Heal(50) },
    };

    private static WeaponCard _Weapon(string name, WeaponType type, float dmg = 1f, float atkSpeed = 1f, string icon = "剑") => new()
    {
        CardName = name,
        WeaponType = type,
        Category = WeaponCategory.Weapon,
        DamageModifier = dmg,
        AttackSpeedModifier = atkSpeed,
        Icon = icon,
        Tier = 1,
    };

    private static WeaponCard _Equip(string name, string upcat, float hp = 0, float spd = 0) => new()
    {
        CardName = name,
        Category = WeaponCategory.Equipment,
        UpgradeCategory = upcat,
        HealthModifier = hp,
        SpeedModifier = spd,
        Icon = "甲",
        Tier = 1,
    };
}
