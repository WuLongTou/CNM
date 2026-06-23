namespace ZombieWars.Core;

using System;
using System.Collections.Generic;

/// <summary>资源/物品类型。阶段 1 采集产出，阶段 3 合成消耗。</summary>
public enum ItemId { Wood, Stone, Fiber, Berry }

/// <summary>
/// 全局库存（饥荒式资源/物品计数）—— 静态单例，一局一个。
/// 采集 Pickup 产出 Add，合成台消耗 TryConsume，背包 UI 订阅 Changed 刷新。
/// </summary>
public static class Inventory
{
    private static readonly Dictionary<ItemId, int> _counts = new();

    /// <summary>(物品, 新总量) — 背包 UI 订阅以实时刷新。</summary>
    public static event Action<ItemId, int>? Changed;

    public static int Get(ItemId item) => _counts.TryGetValue(item, out var v) ? v : 0;

    public static void Add(ItemId item, int amount)
    {
        if (amount <= 0) return;
        _counts[item] = Get(item) + amount;
        Changed?.Invoke(item, _counts[item]);
    }

    /// <summary>检查并扣除一组材料（合成用）。不足返回 false 且不扣。</summary>
    public static bool TryConsume(IReadOnlyDictionary<ItemId, int> cost)
    {
        foreach (var kv in cost)
            if (Get(kv.Key) < kv.Value) return false;
        foreach (var kv in cost)
        {
            _counts[kv.Key] = Get(kv.Key) - kv.Value;
            Changed?.Invoke(kv.Key, _counts[kv.Key]);
        }
        return true;
    }

    public static IReadOnlyDictionary<ItemId, int> All => _counts;

    public static void Clear()
    {
        var keys = new List<ItemId>(_counts.Keys);
        _counts.Clear();
        foreach (var k in keys) Changed?.Invoke(k, 0);
    }

    public static string NameOf(ItemId item) => item switch
    {
        ItemId.Wood => "木材",
        ItemId.Stone => "石头",
        ItemId.Fiber => "纤维",
        ItemId.Berry => "浆果",
        _ => item.ToString(),
    };
}
