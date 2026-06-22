namespace ZombieWars.Enemy.Behaviors;

using Godot;
using ZombieWars.Core;

/// <summary>弓箭手僵尸行为：周期发射追踪箭。</summary>
public class ArcherBehavior : IZombieBehavior
{
    private float _cooldown;
    private readonly float _cooldownBase = 2.0f;
    private const float Range = 300.0f;

    public void OnReady(Zombie zombie) { }

    public void OnProcess(Zombie zombie, float delta)
    {
        _cooldown -= delta;
        if (_cooldown <= 0)
        {
            _cooldown = _cooldownBase;
            var player = zombie.Player;
            if (player != null && zombie.GlobalPosition.DistanceTo(player.GlobalPosition) <= Range)
                _ShootArrow(zombie, player);
        }
    }

    private static void _ShootArrow(Zombie zombie, Node2D target)
    {
        Vector2 direction = target.GlobalPosition - zombie.GlobalPosition;
        var arrow = new ZombieArrow(direction, zombie.Damage, zombie)
        {
            Target = target,
            GlobalPosition = zombie.GlobalPosition
        };
        zombie.GetParent().AddChild(arrow);
    }
}
