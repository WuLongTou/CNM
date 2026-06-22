namespace ZombieWars.Enemy.Behaviors;

using Godot;
using ZombieWars.Core;
using ZombieWars.Enemy;

/// <summary>治疗僵尸行为：周期治疗附近友军。</summary>
public class HealerBehavior : IZombieBehavior
{
    private float _cooldown;
    private readonly float _cooldownBase = GameConfig.HealerCooldown;
    private readonly float _range = GameConfig.HealerRange;
    private readonly int _amount = GameConfig.HealerAmount;

    public void OnReady(Zombie zombie) { }

    public void OnProcess(Zombie zombie, float delta)
    {
        _cooldown -= delta;
        if (_cooldown <= 0)
        {
            _cooldown = _cooldownBase;
            _HealNearby(zombie);
        }
    }

    private void _HealNearby(Zombie zombie)
    {
        bool healedAny = false;
        foreach (var z in ZombieRegistry.All)
        {
            if (!GodotObject.IsInstanceValid(z) || z == zombie) continue;
            if (zombie.GlobalPosition.DistanceTo(z.GlobalPosition) <= _range && z.CurrentHealth < z.MaxHealth)
            {
                z.CurrentHealth = Mathf.Min(z.MaxHealth, z.CurrentHealth + _amount);
                z.Vfx?.UpdateStatusLabels();
                healedAny = true;
                zombie.Vfx?.ShowHealEffect(z.GlobalPosition, _amount);
            }
        }
        if (healedAny)
            zombie.Vfx?.ShowHealPulse(_range);
    }
}
