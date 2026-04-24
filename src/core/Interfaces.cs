namespace ZombieWars.Core;

using System;
using Godot;
using ZombieWars.Enemy;
using static ZombieWars.Core.GameEnums;

/// <summary>
/// Interface for entities that can take damage.
/// </summary>
public interface IDamageable
{
    int CurrentHealth { get; }
    int MaxHealth { get; }
    bool IsDead { get; }
    Vector2 GlobalPosition { get; }

    void TakeDamage(int amount);
    void Heal(int amount);
}

/// <summary>
/// Interface for entities that can deal damage.
/// </summary>
public interface IDamager
{
    int Damage { get; }
    Vector2 GlobalPosition { get; }
}

/// <summary>
/// Interface for the player character.
/// </summary>
public interface IPlayer : IDamageable
{
    float CurrentSpeed { get; }
    float CurrentDamage { get; }
    float CriticalChance { get; }
    float LifeStealPercent { get; }
    float CritMultiplier { get; }

    void RegisterDamageDealt(Zombie target, float damage);
    new void TakeDamage(int amount);
}

/// <summary>
/// Interface for zombies.
/// </summary>
public interface IZombie : IDamageable
{
    ZombieType ZombieType { get; }
    int Damage { get; }
    float Speed { get; }
    bool IsElite { get; }
    bool IsBoss { get; }
    new Vector2 GlobalPosition { get; }
    Vector2 Velocity { get; set; }
}

/// <summary>
/// Interface for attack effects.
/// </summary>
public interface IAttackEffect
{
    void ApplyTo(IZombie target);
}

/// <summary>
/// Extension methods for common operations.
/// </summary>
public static class CoreExtensions
{
    /// <summary>
    /// Calculates damage with critical hit chance.
    /// </summary>
    public static (int damage, bool isCrit) CalculateDamage(
        this IDamager damager, int baseDamage, int critChance)
    {
        bool isCrit = Random.Shared.Next(100) < critChance;
        int damage = isCrit
            ? (int)(baseDamage * 2.5f)  // Use CritMultiplier from config
            : baseDamage;
        return (damage, isCrit);
    }

    /// <summary>
    /// Clamps health between 0 and maxHealth.
    /// </summary>
    public static int ClampHealth(this IDamageable entity, int health)
    {
        return Math.Clamp(health, 0, entity.MaxHealth);
    }
}
