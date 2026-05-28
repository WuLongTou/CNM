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
            lock (_lock) return _zombies.ToArray();
        }
    }

    public static void Register(Zombie z)
    {
        if (z == null) return;
        lock (_lock)
        {
            if (_zombies.Contains(z)) return;
            _zombies.Add(z);
        }
        SpatialUtils.InvalidateGrid();
    }

    public static void Unregister(Zombie z)
    {
        if (z == null) return;
        lock (_lock)
        {
            _zombies.Remove(z);
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
        }
        SpatialUtils.InvalidateGrid();
    }

    public static void Clear()
    {
        lock (_lock)
        {
            _zombies.Clear();
        }
        SpatialUtils.Clear();
    }
}
