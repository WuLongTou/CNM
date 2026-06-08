namespace ZombieWars.Weapons.Effects;

using Godot;
using ZombieWars.Core;

/// <summary>
/// 中毒效果：持续时间内按 Dps 造成 dot 伤害。
/// Chance 字段控制触发概率（0-100）；100 表示必触发，0 表示永不触发。
/// </summary>
public class PoisonEffect : IAttackEffect
{
    /// <summary>触发概率（0-100）。</summary>
    public float Chance { get; init; }

    /// <summary>每秒伤害。</summary>
    public float Dps { get; init; } = 3.0f;

    /// <summary>持续时间（秒）。</summary>
    public float Duration { get; init; } = 3.0f;

    public void ApplyTo(IZombie target)
    {
        if (Chance < 100.0f && GD.Randf() * 100.0f >= Chance) return;
        target.ApplyPoison(Dps, Duration);
    }
}
