namespace ZombieWars.Weapons.Effects;

using Godot;
using ZombieWars.Core;

/// <summary>
/// 冰冻效果：减速 + 持续时间内基本静止。
/// Chance 字段控制触发概率（0-100）；100 表示必触发，0 表示永不触发。
/// </summary>
public class FreezeEffect : IAttackEffect
{
    /// <summary>触发概率（0-100）。</summary>
    public float Chance { get; init; }

    /// <summary>冰冻持续时间（秒）。</summary>
    public float Duration { get; init; } = 0.8f;

    public void ApplyTo(IZombie target)
    {
        if (Chance < 100.0f && GD.Randf() * 100.0f >= Chance) return;
        target.ApplyFreeze(Duration);
    }
}
