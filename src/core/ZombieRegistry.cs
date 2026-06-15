namespace ZombieWars.Core;

using System.Collections.Generic;
using Godot;
using ZombieWars.Enemy;

/// <summary>
/// Centralized zombie registry — caches the zombie list and provides O(1) access.
/// Updated incrementally on spawn/death to avoid repeated GetNodesInGroup calls.
/// </summary>
public static partial class ZombieRegistry
{
    private static readonly List<Zombie> _zombies = new();
    private static readonly object _lock = new();

    // 快照缓存：注册表未变更（_version 不变）时复用同一数组，避免高频 All 调用
    // （如魅惑态每帧 _FindNearestZombie、SplashRadius 命中）反复 ToArray 产生 GC 压力。
    private static Zombie[]? _cachedAll;
    private static int _cachedVersion = -1;
    private static int _version;

    public static int Count
    {
        get
        {
            lock (_lock) return _zombies.Count;
        }
    }

    public static IReadOnlyList<Zombie> All
    {
        get
        {
            lock (_lock)
            {
                if (_cachedAll == null || _cachedVersion != _version)
                {
                    _cachedAll = _zombies.ToArray();
                    _cachedVersion = _version;
                }
                return _cachedAll;
            }
        }
    }

    public static void Register(Zombie z)
    {
        if (z == null) return;
        lock (_lock)
        {
            if (_zombies.Contains(z)) return;
            _zombies.Add(z);
            _version++;
        }
        SpatialUtils.InvalidateGrid();
    }

    public static void Unregister(Zombie z)
    {
        if (z == null) return;
        lock (_lock)
        {
            _zombies.Remove(z);
            _version++;
        }
        SpatialUtils.InvalidateGrid();
    }

    public static void RefreshFromGroup(SceneTree tree)
    {
        lock (_lock)
        {
            _zombies.Clear();
            foreach (var node in tree.GetNodesInGroup("zombies"))
            {
                if (node is Zombie z)
                    _zombies.Add(z);
            }
            _version++;
        }
        SpatialUtils.InvalidateGrid();
    }

    public static void Clear()
    {
        lock (_lock)
        {
            _zombies.Clear();
            _version++;
        }
        SpatialUtils.Clear();
    }
}
