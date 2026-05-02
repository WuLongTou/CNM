namespace ZombieWars.Weapons;

using System;
using System.Collections.Generic;
using Godot;
using GodotArray = Godot.Collections.Array;
using GodotDictionary = Godot.Collections.Dictionary;
using ZombieWars.Core;
using static ZombieWars.Core.GameEnums;

/// <summary>
/// 数据驱动武器工厂
/// 用紧凑的数据表替代重复的 WeaponCard.new() 代码
/// </summary>
public static partial class WeaponFactory
{
    // 缩写映射，保持数据表紧凑
    private const WeaponType ME = WeaponType.Melee;
    private const WeaponType RA = WeaponType.Ranged;
    private const WeaponType MA = WeaponType.Magic;
    private const WeaponType MI = WeaponType.Mine;
    private const WeaponCategory W = WeaponCategory.Weapon;
    private const WeaponCategory E = WeaponCategory.Equipment;

    /// <summary>构建整个武器库</summary>
    public static GodotArray BuildAll()
    {
        var cards = new GodotArray();
        foreach (var def in _WEAPON_DATA)
        {
            cards.Add(_BuildCard((GodotDictionary)def));
        }
        _AutoAssignRarity(cards);
        return cards;
    }

    /// <summary>前置条件表</summary>
    public static GodotDictionary GetPrerequisites() => new GodotDictionary
    {
        { "战斧", "铁剑" }, { "双刀", "铁剑" },
        { "淬毒短刀", "双刀" },
        { "巨斧", "战斧" }, { "长枪", "巨斧" }, { "死神镰刀", "长枪" },
        { "重弩", "弓箭" }, { "散弹枪", "弓箭" },
        { "狙击弩", "重弩" }, { "轨迹弩", "重弩" },
        { "回旋镖", "散弹枪" },
        { "火球法杖", "法杖" }, { "魅惑法杖", "法杖" },
        { "力场法杖", "法杖" },
        { "冰霜法杖", "火球法杖" }, { "雷电法杖", "火球法杖" },
        { "死神法杖", "雷电法杖" },
    };

    // ═══════════════════════════════════════════════
    // 数据表
    // 字段: name, desc, type, cat, tier, upcat, icon, icon_tex, [tags]
    // 可选: dmg, spd, asp, cd, hp, spd_mod, crit, ls, frz, psn, aura, hcost
    // 效果: melee_fx, ranged_fx, magic_fx (dict: effect_enum -> level)
    // ═══════════════════════════════════════════════

    // 武器图标纹理映射
    private static readonly Dictionary<string, string> _WeaponIconTextures = new()
    {
        { "铁剑", "res://assets/sprites/weapons/sword_basic.jpg" },
        { "战斧", "res://assets/sprites/weapons/axe_battle.jpg" },
        { "双刀", "res://assets/sprites/weapons/dagger_twin.jpg" },
        { "淬毒短刀", "res://assets/sprites/weapons/dagger_twin.jpg" },
        { "巨斧", "res://assets/sprites/weapons/axe_battle.jpg" },
        { "长枪", "res://assets/sprites/weapons/sword_basic.jpg" },
        { "死神镰刀", "res://assets/sprites/weapons/axe_battle.jpg" },
        { "弓箭", "res://assets/sprites/weapons/bow_long.jpg" },
        { "重弩", "res://assets/sprites/weapons/crossbow_heavy.jpg" },
        { "散弹枪", "res://assets/sprites/weapons/crossbow_heavy.jpg" },
        { "狙击弩", "res://assets/sprites/weapons/crossbow_heavy.jpg" },
        { "轨迹弩", "res://assets/sprites/weapons/crossbow_heavy.jpg" },
        { "回旋镖", "res://assets/sprites/weapons/throwing_knives.jpg" },
        { "法杖", "res://assets/sprites/weapons/pistol_magic.jpg" },
        { "火球法杖", "res://assets/sprites/weapons/sword_fire.jpg" },
        { "魅惑法杖", "res://assets/sprites/weapons/pistol_magic.jpg" },
        { "冰霜法杖", "res://assets/sprites/weapons/pistol_magic.jpg" },
        { "雷电法杖", "res://assets/sprites/weapons/pistol_magic.jpg" },
        { "死神法杖", "res://assets/sprites/weapons/sword_fire.jpg" },
        { "力场法杖", "res://assets/sprites/weapons/pistol_magic.jpg" },
    };

    private static readonly GodotArray _WEAPON_DATA = new GodotArray
    {
        // ═══ 近战武器 ═══
        // 铁剑
        new GodotDictionary
        {
            { "name", "铁剑" }, { "desc", "平衡的近战武器，适合新手" },
            { "type", (int)ME }, { "cat", (int)W }, { "tier", 1 }, { "upcat", "武器" }, { "icon", "⚔️" }, { "rarity", 1 }
        },

        // 战斧
        new GodotDictionary
        {
            { "name", "战斧" }, { "desc", "高伤害低攻速" },
            { "type", (int)ME }, { "cat", (int)W }, { "tier", 2 }, { "upcat", "武器" }, { "icon", "🪓" }, { "rarity", 2 },
            { "tags", new GodotArray {"铁剑"} }, { "dmg", 1.5f }, { "asp", 0.7f },
            { "melee_fx", new GodotDictionary { { "SPLASH", 1 } } }
        },

        // 双刀
        new GodotDictionary
        {
            { "name", "双刀" }, { "desc", "高攻速多目标" },
            { "type", (int)ME }, { "cat", (int)W }, { "tier", 2 }, { "upcat", "武器" }, { "icon", "🗡️" }, { "rarity", 2 },
            { "tags", new GodotArray {"铁剑"} }, { "dmg", 0.7f }, { "asp", 1.5f }, { "hp", -10f },
            { "melee_fx", new GodotDictionary { { "MULTI_TARGET", 1 } } }
        },

        // 淬毒短刀
        new GodotDictionary
        {
            { "name", "淬毒短刀" }, { "desc", "近战带毒持续伤害" },
            { "type", (int)ME }, { "cat", (int)W }, { "tier", 2 }, { "upcat", "武器" }, { "icon", "🗡️" }, { "rarity", 2 },
            { "tags", new GodotArray {"双刀"} }, { "dmg", 0.8f }, { "asp", 1.4f }, { "psn", 10f },
            { "melee_fx", new GodotDictionary { { "MULTI_TARGET", 1 } } }
        },

        // 巨斧
        new GodotDictionary
        {
            { "name", "巨斧" }, { "desc", "超高伤害溅射" },
            { "type", (int)ME }, { "cat", (int)W }, { "tier", 3 }, { "upcat", "武器" }, { "icon", "🪓" }, { "rarity", 3 },
            { "tags", new GodotArray {"战斧"} }, { "dmg", 1.8f }, { "asp", 0.6f }, { "hcost", 5 },
            { "melee_fx", new GodotDictionary { { "SPLASH", 2 }, { "KNOCKBACK", 1 } } }
        },

        // 长枪
        new GodotDictionary
        {
            { "name", "长枪" }, { "desc", "穿透多目标" },
            { "type", (int)ME }, { "cat", (int)W }, { "tier", 3 }, { "upcat", "武器" }, { "icon", "🔱" }, { "rarity", 3 },
            { "tags", new GodotArray {"巨斧"} }, { "dmg", 1.4f }, { "asp", 0.8f },
            { "melee_fx", new GodotDictionary { { "MULTI_TARGET", 2 }, { "KNOCKBACK", 1 } } }
        },

        // 死神镰刀
        new GodotDictionary
        {
            { "name", "死神镰刀" }, { "desc", "击杀爆炸并获得护盾" },
            { "type", (int)ME }, { "cat", (int)W }, { "tier", 4 }, { "upcat", "武器" }, { "icon", "⚰️" }, { "rarity", 4 },
            { "tags", new GodotArray {"长枪"} }, { "dmg", 2.0f }, { "asp", 0.9f }, { "hcost", 10 },
            { "melee_fx", new GodotDictionary { { "KILL_EXPLOSION", 2 }, { "KILL_SHIELD", 1 } } }
        },

        // ═══ 远程武器 ═══
        // 弓箭
        new GodotDictionary
        {
            { "name", "弓箭" }, { "desc", "基础远程武器" },
            { "type", (int)RA }, { "cat", (int)W }, { "tier", 1 }, { "upcat", "武器" }, { "icon", "🏹" }, { "rarity", 1 },
            { "dmg", 0.9f }, { "asp", 0.8f }, { "cd", 0.6f }
        },

        // 重弩
        new GodotDictionary
        {
            { "name", "重弩" }, { "desc", "连发武器" },
            { "type", (int)RA }, { "cat", (int)W }, { "tier", 2 }, { "upcat", "武器" }, { "icon", "🎯" }, { "rarity", 2 },
            { "tags", new GodotArray {"弓箭"} }, { "dmg", 0.8f }, { "asp", 0.7f }, { "cd", 0.8f },
            { "ranged_fx", new GodotDictionary { { "MULTI_SHOT", 1 } } }
        },

        // 散弹枪
        new GodotDictionary
        {
            { "name", "散弹枪" }, { "desc", "扇形攻击" },
            { "type", (int)RA }, { "cat", (int)W }, { "tier", 2 }, { "upcat", "武器" }, { "icon", "🔫" }, { "rarity", 2 },
            { "tags", new GodotArray {"弓箭"} }, { "dmg", 0.6f }, { "asp", 0.6f }, { "cd", 1.0f },
            { "ranged_fx", new GodotDictionary { { "MULTI_SHOT", 2 } } }
        },

        // 狙击弩
        new GodotDictionary
        {
            { "name", "狙击弩" }, { "desc", "超高伤害穿透" },
            { "type", (int)RA }, { "cat", (int)W }, { "tier", 3 }, { "upcat", "武器" }, { "icon", "🎯" }, { "rarity", 3 },
            { "tags", new GodotArray {"重弩"} }, { "dmg", 2.2f }, { "asp", 0.4f }, { "cd", 1.5f },
            { "ranged_fx", new GodotDictionary { { "PIERCING", 2 } } }
        },

        // 轨迹弩
        new GodotDictionary
        {
            { "name", "轨迹弩" }, { "desc", "路径伤害" },
            { "type", (int)RA }, { "cat", (int)W }, { "tier", 3 }, { "upcat", "武器" }, { "icon", "➰" }, { "rarity", 2 },
            { "tags", new GodotArray {"重弩"} }, { "dmg", 1.0f }, { "asp", 0.75f }, { "cd", 0.7f },
            { "ranged_fx", new GodotDictionary { { "TRAJECTORY_DAMAGE", 1 } } }
        },

        // 回旋镖
        new GodotDictionary
        {
            { "name", "回旋镖" }, { "desc", "弹道返回" },
            { "type", (int)RA }, { "cat", (int)W }, { "tier", 3 }, { "upcat", "武器" }, { "icon", "🪃" }, { "rarity", 2 },
            { "tags", new GodotArray {"散弹枪"} }, { "dmg", 0.7f }, { "asp", 0.8f }, { "cd", 0.6f },
            { "ranged_fx", new GodotDictionary { { "RETURNING", 1 } } }
        },

        // ═══ 魔法武器 ═══
        // 法杖
        new GodotDictionary
        {
            { "name", "法杖" }, { "desc", "基础魔法武器" },
            { "type", (int)MA }, { "cat", (int)W }, { "tier", 1 }, { "upcat", "武器" }, { "icon", "🪄" }, { "rarity", 1 },
            { "asp", 0.5f }, { "magic_fx", new GodotDictionary { { "DOT", 1 } } }
        },

        // 火球法杖
        new GodotDictionary
        {
            { "name", "火球法杖" }, { "desc", "连锁击退" },
            { "type", (int)MA }, { "cat", (int)W }, { "tier", 2 }, { "upcat", "武器" }, { "icon", "🔥" }, { "rarity", 2 },
            { "tags", new GodotArray {"法杖"} }, { "dmg", 1.2f }, { "asp", 0.5f },
            { "magic_fx", new GodotDictionary { { "CHAIN", 1 }, { "PUSH", 1 } } }
        },

        // 魅惑法杖
        new GodotDictionary
        {
            { "name", "魅惑法杖" }, { "desc", "让敌人互相攻击" },
            { "type", (int)MA }, { "cat", (int)W }, { "tier", 2 }, { "upcat", "武器" }, { "icon", "💖" }, { "rarity", 2 },
            { "tags", new GodotArray {"法杖"} }, { "dmg", 0.8f }, { "asp", 0.6f },
            { "magic_fx", new GodotDictionary { { "CHARM", 3 } } }
        },

        // 冰霜法杖
        new GodotDictionary
        {
            { "name", "冰霜法杖" }, { "desc", "定身敌人" },
            { "type", (int)MA }, { "cat", (int)W }, { "tier", 3 }, { "upcat", "武器" }, { "icon", "❄️" }, { "rarity", 2 },
            { "tags", new GodotArray {"火球法杖"} }, { "dmg", 0.9f }, { "asp", 0.55f },
            { "magic_fx", new GodotDictionary { { "STUN", 2 } } }
        },

        // 雷电法杖
        new GodotDictionary
        {
            { "name", "雷电法杖" }, { "desc", "强力连锁" },
            { "type", (int)MA }, { "cat", (int)W }, { "tier", 3 }, { "upcat", "武器" }, { "icon", "⚡" }, { "rarity", 3 },
            { "tags", new GodotArray {"火球法杖"} }, { "dmg", 1.3f }, { "asp", 0.45f },
            { "magic_fx", new GodotDictionary { { "CHAIN", 3 } } }
        },

        // 死神法杖
        new GodotDictionary
        {
            { "name", "死神法杖" }, { "desc", "死亡连锁" },
            { "type", (int)MA }, { "cat", (int)W }, { "tier", 4 }, { "upcat", "武器" }, { "icon", "💀" }, { "rarity", 4 },
            { "tags", new GodotArray {"雷电法杖"} }, { "dmg", 1.5f }, { "asp", 0.4f }, { "hcost", 15 },
            { "magic_fx", new GodotDictionary { { "CHAIN", 4 }, { "DOT", 3 } } }
        },

        // 力场法杖
        new GodotDictionary
        {
            { "name", "力场法杖" }, { "desc", "范围击退所有敌人" },
            { "type", (int)MA }, { "cat", (int)W }, { "tier", 2 }, { "upcat", "武器" }, { "icon", "🌀" }, { "rarity", 2 },
            { "tags", new GodotArray {"法杖"} }, { "dmg", 0.9f }, { "asp", 0.55f },
            { "magic_fx", new GodotDictionary { { "PUSH", 2 } } }
        },

        // ═══ 装备 ═══
        // 生命药水
        new GodotDictionary
        {
            { "name", "生命药水" }, { "desc", "提高生命上限" },
            { "type", (int)ME }, { "cat", (int)E }, { "tier", 1 }, { "upcat", "buff" }, { "icon", "🧪" }, { "rarity", 1 }, { "hp", 30f }
        },

        // 敏捷靴
        new GodotDictionary
        {
            { "name", "敏捷靴" }, { "desc", "提升速度" },
            { "type", (int)ME }, { "cat", (int)E }, { "tier", 1 }, { "upcat", "鞋子" }, { "icon", "👢" }, { "rarity", 1 },
            { "spd_mod", 20f }, { "hp", -5f }
        },

        // 攻击宝石
        new GodotDictionary
        {
            { "name", "攻击宝石" }, { "desc", "提升伤害" },
            { "type", (int)ME }, { "cat", (int)E }, { "tier", 1 }, { "upcat", "特效" }, { "icon", "💎" }, { "rarity", 1 },
            { "dmg", 1.15f }, { "hp", -10f }
        },

        // 暴击指环
        new GodotDictionary
        {
            { "name", "暴击指环" }, { "desc", "提升暴击率" },
            { "type", (int)ME }, { "cat", (int)E }, { "tier", 2 }, { "upcat", "戒指" }, { "icon", "💍" }, { "rarity", 2 },
            { "crit", 12f }
        },

        // 吸血护符
        new GodotDictionary
        {
            { "name", "吸血护符" }, { "desc", "攻击附带吸血效果" },
            { "type", (int)ME }, { "cat", (int)E }, { "tier", 2 }, { "upcat", "buff" }, { "icon", "🧿" }, { "rarity", 2 },
            { "ls", 6f }
        },

        // 寒霜徽章
        new GodotDictionary
        {
            { "name", "寒霜徽章" }, { "desc", "增加冻结几率" },
            { "type", (int)ME }, { "cat", (int)E }, { "tier", 2 }, { "upcat", "特效" }, { "icon", "❄️" }, { "rarity", 2 },
            { "frz", 15f }
        },

        // 剧毒之戒
        new GodotDictionary
        {
            { "name", "剧毒之戒" }, { "desc", "增加中毒几率" },
            { "type", (int)ME }, { "cat", (int)E }, { "tier", 2 }, { "upcat", "戒指" }, { "icon", "☠️" }, { "rarity", 2 },
            { "psn", 12f }
        },

        // 冰霜护盾
        new GodotDictionary
        {
            { "name", "冰霜护盾" }, { "desc", "开启环绕护盾" },
            { "type", (int)ME }, { "cat", (int)E }, { "tier", 3 }, { "upcat", "护甲" }, { "icon", "🛡️" }, { "rarity", 3 },
            { "aura", true }
        },

        // 铁盔
        new GodotDictionary
        {
            { "name", "铁盔" }, { "desc", "提高生命上限" },
            { "type", (int)ME }, { "cat", (int)E }, { "tier", 1 }, { "upcat", "头盔" }, { "icon", "⛑️" }, { "hp", 15f }
        },

        // 皮甲
        new GodotDictionary
        {
            { "name", "皮甲" }, { "desc", "提高生命上限，略降速度" },
            { "type", (int)ME }, { "cat", (int)E }, { "tier", 1 }, { "upcat", "护甲" }, { "icon", "🧥" },
            { "hp", 20f }, { "spd_mod", -5f }
        },

        // 疾风护符
        new GodotDictionary
        {
            { "name", "疾风护符" }, { "desc", "提升攻击速度" },
            { "type", (int)ME }, { "cat", (int)E }, { "tier", 2 }, { "upcat", "buff" }, { "icon", "💨" },
            { "asp", 1.2f }
        },

        // 再生戒指
        new GodotDictionary
        {
            { "name", "再生戒指" }, { "desc", "提高生命上限并附带少量吸血" },
            { "type", (int)ME }, { "cat", (int)E }, { "tier", 2 }, { "upcat", "戒指" }, { "icon", "💚" },
            { "hp", 10f }, { "ls", 3f }
        },

        // 狂战士护腕
        new GodotDictionary
        {
            { "name", "狂战士护腕" }, { "desc", "大幅提升伤害，但降低生命上限" },
            { "type", (int)ME }, { "cat", (int)E }, { "tier", 2 }, { "upcat", "buff" }, { "icon", "💪" },
            { "dmg", 1.35f }, { "hp", -20f }
        },

        // 猎人印记
        new GodotDictionary
        {
            { "name", "猎人印记" }, { "desc", "增加暴击并提升伤害" },
            { "type", (int)ME }, { "cat", (int)E }, { "tier", 3 }, { "upcat", "buff" }, { "icon", "🎯" },
            { "crit", 15f }, { "dmg", 1.1f }
        },

        // 幽灵斗篷
        new GodotDictionary
        {
            { "name", "幽灵斗篷" }, { "desc", "提升移动速度，代价是少量生命上限" },
            { "type", (int)ME }, { "cat", (int)E }, { "tier", 2 }, { "upcat", "护甲" }, { "icon", "👻" },
            { "spd_mod", 30f }, { "hp", -5f }
        },

        // 反伤护甲 - NEW: 受伤时反弹伤害
        new GodotDictionary
        {
            { "name", "荆棘护甲" }, { "desc", "受伤时反弹部分伤害给攻击者" },
            { "type", (int)ME }, { "cat", (int)E }, { "tier", 2 }, { "upcat", "护甲" }, { "icon", "🌿" }, { "rarity", 2 },
            { "hp", 20f }
        },

        // 闪避护符 - NEW: 额外闪避能力
        new GodotDictionary
        {
            { "name", "闪避护符" }, { "desc", "略微提升移动速度和闪避能力" },
            { "type", (int)ME }, { "cat", (int)E }, { "tier", 2 }, { "upcat", "buff" }, { "icon", "💨" }, { "rarity", 2 },
            { "spd_mod", 15f }, { "hp", 5f }
        },

        // 生命汲取 - NEW: 提升吸血效果
        new GodotDictionary
        {
            { "name", "生命汲取" }, { "desc", "大幅提升吸血效果和生命上限" },
            { "type", (int)ME }, { "cat", (int)E }, { "tier", 3 }, { "upcat", "buff" }, { "icon", "🩸" }, { "rarity", 3 },
            { "ls", 12f }, { "hp", 15f }
        },

        // 穿甲弹 - NEW: 穿透效果增强
        new GodotDictionary
        {
            { "name", "穿甲弹" }, { "desc", "提升穿透能力，子弹可贯穿更多敌人" },
            { "type", (int)ME }, { "cat", (int)E }, { "tier", 3 }, { "upcat", "特效" }, { "icon", "💎" }, { "rarity", 3 },
            { "dmg", 1.1f }, { "crit", 8f }
        },

        // 弹跳之刃 - NEW: 弹道跳跃能力
        new GodotDictionary
        {
            { "name", "弹跳之刃" }, { "desc", "远程武器可弹跳攻击多个目标" },
            { "type", (int)ME }, { "cat", (int)E }, { "tier", 3 }, { "upcat", "特效" }, { "icon", "🪃" }, { "rarity", 3 },
            { "dmg", 1.05f }, { "asp", 1.1f }
        },

        // 毁灭之息 - NEW: 范围伤害提升
        new GodotDictionary
        {
            { "name", "毁灭之息" }, { "desc", "大幅提升范围伤害效果" },
            { "type", (int)ME }, { "cat", (int)E }, { "tier", 4 }, { "upcat", "buff" }, { "icon", "🔥" }, { "rarity", 4 },
            { "dmg", 1.25f }, { "hp", -15f }
        },

        // 钢铁意志 - NEW: 减少负面效果
        new GodotDictionary
        {
            { "name", "钢铁意志" }, { "desc", "提升生命上限并减少控制效果持续时间" },
            { "type", (int)ME }, { "cat", (int)E }, { "tier", 3 }, { "upcat", "护甲" }, { "icon", "🛡️" }, { "rarity", 3 },
            { "hp", 25f }, { "frz", 5f }
        },
    };

    // ═══════════════════════════════════════════════
    // 内部构建
    // ═══════════════════════════════════════════════

    private static WeaponCard _BuildCard(GodotDictionary d)
    {
        var c = new WeaponCard();
        var tags = d.ContainsKey("tags") ? (GodotArray)d["tags"] : new GodotArray();
        var name = d["name"].AsString();
        var desc = d["desc"].AsString();
        var type = (WeaponType)d["type"].AsInt32();
        c.Init(name, desc, type, tags);
        c.Category = (WeaponCategory)d["cat"].AsInt32();
        c.Tier = d.ContainsKey("tier") ? d["tier"].AsInt32() : 1;
        c.UpgradeCategory = d.ContainsKey("upcat") ? d["upcat"].AsString() : "";
        c.Icon = d.ContainsKey("icon") ? d["icon"].AsString() : "⚔️";

        // 加载武器图标纹理
        if (_WeaponIconTextures.TryGetValue(name, out var texPath))
        {
            if (FileAccess.FileExists(texPath))
                c.IconTexture = GD.Load<Texture2D>(texPath);
        }

        // 属性修改器
        if (d.ContainsKey("dmg")) c.DamageModifier = d["dmg"].AsSingle();
        if (d.ContainsKey("asp")) c.AttackSpeedModifier = d["asp"].AsSingle();
        if (d.ContainsKey("cd")) c.Cooldown = d["cd"].AsSingle();
        if (d.ContainsKey("hp")) c.HealthModifier = d["hp"].AsSingle();
        if (d.ContainsKey("spd_mod")) c.SpeedModifier = d["spd_mod"].AsSingle();
        if (d.ContainsKey("crit")) c.CriticalBonus = d["crit"].AsSingle();
        if (d.ContainsKey("ls")) c.LifeStealBonus = d["ls"].AsSingle();
        if (d.ContainsKey("frz")) c.FreezeBonus = d["frz"].AsSingle();
        if (d.ContainsKey("psn")) c.PoisonBonus = d["psn"].AsSingle();
        if (d.ContainsKey("hcost")) c.HealthCost = d["hcost"].AsInt32();
        if (d.ContainsKey("aura")) c.GrantsAura = d["aura"].AsBool();
        if (d.ContainsKey("rarity")) c.Rarity = d["rarity"].AsInt32();

        // 效果（字符串 enum 名 → enum 值）
        if (d.ContainsKey("melee_fx"))
        {
            var meleeFx = (GodotDictionary)d["melee_fx"];
            foreach (var key in meleeFx.Keys)
            {
                string fxName = key.AsString();
                var fx = _ParseMeleeEffect(fxName);
                c.MeleeEffects.Add((int)fx);
                c.MeleeEffectLevels[(int)fx] = meleeFx[key].AsInt32();
            }
        }
        if (d.ContainsKey("ranged_fx"))
        {
            var rangedFx = (GodotDictionary)d["ranged_fx"];
            foreach (var key in rangedFx.Keys)
            {
                string fxName = key.AsString();
                var fx = _ParseRangedEffect(fxName);
                c.RangedEffects.Add((int)fx);
                c.RangedEffectLevels[(int)fx] = rangedFx[key].AsInt32();
            }
        }
        if (d.ContainsKey("magic_fx"))
        {
            var magicFx = (GodotDictionary)d["magic_fx"];
            foreach (var key in magicFx.Keys)
            {
                string fxName = key.AsString();
                var fx = _ParseMagicEffect(fxName);
                c.MagicEffects.Add((int)fx);
                c.MagicEffectLevels[(int)fx] = magicFx[key].AsInt32();
            }
        }
        return c;
    }

    private static MeleeEffect _ParseMeleeEffect(string name)
    {
        return name switch
        {
            "SPLASH" => MeleeEffect.Splash,
            "MULTI_TARGET" => MeleeEffect.MultiTarget,
            "KILL_EXPLOSION" => MeleeEffect.KillExplosion,
            "KILL_SHIELD" => MeleeEffect.KillShield,
            "KNOCKBACK" => MeleeEffect.Knockback,
            _ => MeleeEffect.None,
        };
    }

    private static RangedEffect _ParseRangedEffect(string name)
    {
        return name switch
        {
            "MULTI_SHOT" => RangedEffect.MultiShot,
            "TRAJECTORY_DAMAGE" => RangedEffect.TrajectoryDamage,
            "RETURNING" => RangedEffect.Returning,
            "PIERCING" => RangedEffect.Piercing,
            _ => RangedEffect.None,
        };
    }

    private static MagicEffect _ParseMagicEffect(string name)
    {
        return name switch
        {
            "CHARM" => MagicEffect.Charm,
            "PUSH" => MagicEffect.Push,
            "STUN" => MagicEffect.Stun,
            "CHAIN" => MagicEffect.Chain,
            "DOT" => MagicEffect.Dot,
            _ => MagicEffect.None,
        };
    }

    private static void _AutoAssignRarity(GodotArray cards)
    {
        foreach (WeaponCard card in cards)
        {
            if (card.Rarity > 0)
                continue; // 已手动指定

            int score = 0;
            if (!Mathf.IsEqualApprox(card.DamageModifier, 1.0f)) score++;
            if (!Mathf.IsEqualApprox(card.AttackSpeedModifier, 1.0f)) score++;
            if (card.HealthModifier != 0) score++;
            if (card.SpeedModifier != 0) score++;
            if (card.CriticalBonus > 0) score++;
            if (card.LifeStealBonus > 0) score++;
            if (card.FreezeBonus > 0) score++;
            if (card.PoisonBonus > 0) score++;
            if (card.GrantsAura) score += 2;
            score += card.MeleeEffects.Count;
            score += card.RangedEffects.Count;
            score += card.MagicEffects.Count;

            if (score <= 0) card.Rarity = 1;
            else if (score == 1) card.Rarity = 2;
            else if (score <= 3) card.Rarity = 3;
            else card.Rarity = 4;
        }
    }
}
