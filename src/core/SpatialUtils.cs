namespace ZombieWars.Core;

using System.Collections.Generic;
using Godot;
using ZombieWars.Enemy;

/// <summary>
/// Spatial utilities for distance calculations and spatial queries.
/// Replaces duplicated distance calculation code throughout the codebase.
/// </summary>
public static class SpatialUtils
{
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
    /// </summary>
    public static Zombie? FindDensestZombie(Vector2 position, float clusterRange = 200f)
    {
        Zombie? densest = null;
        int maxCount = 0;
        float rangeSq = clusterRange * clusterRange;

        foreach (var zombie in ZombieRegistry.All)
        {
            if (!IsValidZombie(zombie))
                continue;

            int count = 0;
            foreach (var other in ZombieRegistry.All)
            {
                if (!IsValidZombie(other))
                    continue;

                float distSq = zombie.GlobalPosition.DistanceSquaredTo(other.GlobalPosition);
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
    /// </summary>
    public static List<Zombie> GetZombiesInRange(Vector2 position, float range)
    {
        var result = new List<Zombie>();
        float rangeSq = range * range;

        foreach (var zombie in ZombieRegistry.All)
        {
            if (!IsValidZombie(zombie))
                continue;

            if (position.DistanceSquaredTo(zombie.GlobalPosition) <= rangeSq)
                result.Add(zombie);
        }

        return result;
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
