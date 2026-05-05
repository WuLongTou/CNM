namespace ZombieWars.Core;

using System.Collections.Generic;
using Godot;
using ZombieWars.Enemy;

/// <summary>
/// Spatial utilities for distance calculations and spatial queries.
/// Includes spatial hashing grid for O(1) range queries with many entities.
/// </summary>
public static class SpatialUtils
{
    // Spatial hash grid for efficient range queries
    private const float GridCellSize = 100.0f;
    private static readonly Dictionary<Vector2I, List<Zombie>> _spatialGrid = new();

    /// <summary>
    /// Rebuilds the spatial hash grid.
    /// Called on every query to ensure zombie movement is reflected.
    /// </summary>
    private static void RebuildGrid()
    {
        _spatialGrid.Clear();
        foreach (var zombie in ZombieRegistry.All)
        {
            if (!IsValidZombie(zombie))
                continue;

            Vector2I cell = GetCell(zombie.GlobalPosition);
            if (!_spatialGrid.TryGetValue(cell, out var list))
            {
                list = new List<Zombie>();
                _spatialGrid[cell] = list;
            }
            list.Add(zombie);
        }
    }

    /// <summary>
    /// Marks the spatial grid as needing rebuild.
    /// No-op since grid is now rebuilt on every query.
    /// </summary>
    public static void InvalidateGrid() { }

    private static Vector2I GetCell(Vector2 pos)
    {
        return new Vector2I(
            (int)(pos.X / GridCellSize),
            (int)(pos.Y / GridCellSize)
        );
    }

    /// <summary>
    /// Gets all zombies in cells that could be within range of a position.
    /// Uses spatial hashing for faster queries.
    /// Always rebuilds grid because zombies move every frame.
    /// </summary>
    private static List<Zombie> GetZombiesInCells(Vector2 position, float range)
    {
        RebuildGrid();

        var result = new List<Zombie>();
        int cellRange = (int)(range / GridCellSize) + 1;
        Vector2I center = GetCell(position);

        for (int dx = -cellRange; dx <= cellRange; dx++)
        {
            for (int dy = -cellRange; dy <= cellRange; dy++)
            {
                Vector2I cell = center + new Vector2I(dx, dy);
                if (_spatialGrid.TryGetValue(cell, out var list))
                {
                    result.AddRange(list);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Checks if a zombie is valid (alive and instance is valid).
    /// </summary>
    private static bool IsValidZombie(Zombie? zombie)
    {
        if (zombie == null)
            return false;
        if (!GodotObject.IsInstanceValid(zombie))
            return false;
        return zombie.CurrentHealth > 0;
    }

    /// <summary>
    /// Finds the nearest zombie to a position within range.
    /// </summary>
    public static Zombie? FindNearestZombie(Vector2 position, float maxRange = float.MaxValue)
    {
        Zombie? nearest = null;
        float nearestDistSq = maxRange * maxRange;

        // Use spatial grid for large ranges
        if (maxRange > GridCellSize * 3)
        {
            foreach (var zombie in GetZombiesInCells(position, maxRange))
            {
                if (!IsValidZombie(zombie))
                    continue;

                float distSq = position.DistanceSquaredTo(zombie.GlobalPosition);
                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearest = zombie;
                }
            }
        }
        else
        {
            // Direct search for small ranges
            foreach (var zombie in ZombieRegistry.All)
            {
                if (!IsValidZombie(zombie))
                    continue;

                float distSq = position.DistanceSquaredTo(zombie.GlobalPosition);
                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearest = zombie;
                }
            }
        }

        return nearest;
    }

    /// <summary>
    /// Finds the farthest zombie from a position within range.
    /// </summary>
    public static Zombie? FindFarthestZombie(Vector2 position, float maxRange = float.MaxValue)
    {
        Zombie? farthest = null;
        float farthestDistSq = 0;
        float maxRangeSq = maxRange * maxRange;

        foreach (var zombie in ZombieRegistry.All)
        {
            if (!IsValidZombie(zombie))
                continue;

            float distSq = position.DistanceSquaredTo(zombie.GlobalPosition);
            if (distSq > farthestDistSq && distSq <= maxRangeSq)
            {
                farthestDistSq = distSq;
                farthest = zombie;
            }
        }

        return farthest;
    }

    /// <summary>
    /// Finds the zombie with most nearby zombies (densest cluster).
    /// Uses spatial grid for O(n) average instead of O(n²).
    /// </summary>
    public static Zombie? FindDensestZombie(Vector2 position, float clusterRange = 200f)
    {
        Zombie? densest = null;
        int maxCount = 0;

        // Use spatial grid to reduce candidate set
        var candidates = GetZombiesInCells(position, clusterRange);
        float rangeSq = clusterRange * clusterRange;

        foreach (var zombie in candidates)
        {
            if (!IsValidZombie(zombie))
                continue;

            // Count only valid zombies within range
            int count = 0;
            var zombiePos = zombie.GlobalPosition;

            foreach (var other in candidates)
            {
                if (!IsValidZombie(other))
                    continue;

                float distSq = zombiePos.DistanceSquaredTo(other.GlobalPosition);
                if (distSq <= rangeSq)
                    count++;
            }

            if (count > maxCount)
            {
                maxCount = count;
                densest = zombie;
            }
        }

        return densest;
    }

    /// <summary>
    /// Gets all zombies within range of a position.
    /// Uses direct search for small ranges (grid can be stale due to zombie movement).
    /// </summary>
    public static List<Zombie> GetZombiesInRange(Vector2 position, float range)
    {
        var result = new List<Zombie>();
        float rangeSq = range * range;

        // Spatial grid is only rebuilt on spawn/death, not movement.
        // For small ranges (melee), use direct search to avoid stale positions.
        IEnumerable<Zombie> candidates = range > GridCellSize * 3
            ? GetZombiesInCells(position, range)
            : ZombieRegistry.All;

        foreach (var zombie in candidates)
        {
            if (!IsValidZombie(zombie))
                continue;

            if (position.DistanceSquaredTo(zombie.GlobalPosition) <= rangeSq)
                result.Add(zombie);
        }

        return result;
    }

    public static void Clear()
    {
        _spatialGrid.Clear();
    }

    /// <summary>
    /// Calculates distance squared (avoids sqrt for performance when exact distance not needed).
    /// </summary>
    public static float DistanceSquared(Vector2 a, Vector2 b) => a.DistanceSquaredTo(b);

    /// <summary>
    /// Checks if a position is within range without calculating actual distance.
    /// </summary>
    public static bool IsInRange(Vector2 a, Vector2 b, float range)
    {
        float distSq = a.DistanceSquaredTo(b);
        return distSq <= range * range;
    }
}
