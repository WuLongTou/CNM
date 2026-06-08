namespace ZombieWars.Weapons.Effects;

using Godot;
using ZombieWars.Core;

/// <summary>
/// 魅惑效果：持续时间内僵尸反戈为玩家战斗（Tank/Fast 有抗性）。
/// Chance 字段控制触发概率（0-100）；100 表示必触发，0 表示永不触发。
/// </summary>
public class CharmEffect : IAttackEffect
{
    /// <summary>触发概率（0-100）。</summary>
    public float Chance { get; init; }

    /// <summary>魅惑持续时间（秒）。</summary>
    public float Duration { get; init; }

    public void ApplyTo(IZombie target)
    {
        if (Chance < 100.0f && GD.Randf() * 100.0f >= Chance) return;
        target.ApplyCharm(Duration);
    }
}
