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

    public static int Count => _zombies.Count;

    public static IReadOnlyList<Zombie> All => _zombies;

    public static void Register(Zombie z)
    {
        if (z == null || _zombies.Contains(z))
            return;
        _zombies.Add(z);
        // Invalidate spatial grid when new zombie spawns
        SpatialUtils.InvalidateGrid();
    }

    public static void Unregister(Zombie z)
    {
        if (z == null)
            return;
        _zombies.Remove(z);
        // Invalidate spatial grid when zombie dies
        SpatialUtils.InvalidateGrid();
    }

    public static void RefreshFromGroup(SceneTree tree)
    {
        _zombies.Clear();
        foreach (var node in tree.GetNodesInGroup("zombies"))
        {
            if (node is Zombie z)
                _zombies.Add(z);
        }
        // Rebuild spatial grid
        SpatialUtils.InvalidateGrid();
    }
}
