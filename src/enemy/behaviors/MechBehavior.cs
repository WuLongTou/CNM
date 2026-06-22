namespace ZombieWars.Enemy.Behaviors;

using Godot;
using ZombieWars.Core;

/// <summary>机甲僵尸行为：缓慢推进 + 周期踩踏地震。</summary>
public class MechBehavior : IZombieBehavior
{
    private float _stompTimer;
    private float _stompCooldown = 3.0f;
    private bool _stomping;

    public void OnReady(Zombie zombie) { }

    public void OnProcess(Zombie zombie, float delta)
    {
        var player = zombie.Player;
        _stompTimer -= delta;
        if (_stompTimer <= 0)
        {
            _stompTimer = _stompCooldown;
            _stomping = true;
            zombie.Vfx?.ShowStompShockwave();
            if (player != null && zombie.GlobalPosition.DistanceTo(player.GlobalPosition) <= 80.0f
                && player is IDamageable d)
                d.TakeDamage((int)(zombie.Damage * 0.5f));
        }

        if (_stomping)
        {
            zombie.Velocity = zombie.Velocity * 0.3f;
            zombie.MoveAndSlide();
            _stomping = false;
        }
        else if (player != null)
        {
            zombie.Velocity = (player.GlobalPosition - zombie.GlobalPosition).Normalized()
                * zombie.Speed * zombie.SlowMultiplier;
            zombie.MoveAndSlide();
        }
    }
}
