namespace ZombieWars.Enemy.Behaviors;

using Godot;
using ZombieWars.Core;

/// <summary>骑士僵尸行为：接近 → 冲锋 → 格挡 三态循环。</summary>
public class KnightBehavior : IZombieBehavior
{
    private bool _charging;
    private bool _blocking;
    private float _chargeTimer;
    private float _blockTimer;
    private Vector2 _chargeDir;

    public void OnReady(Zombie zombie) { }

    public void OnProcess(Zombie zombie, float delta)
    {
        var player = zombie.Player;
        if (_charging)
        {
            _chargeTimer -= delta;
            zombie.Velocity = _chargeDir * zombie.Speed * 2.5f * zombie.SlowMultiplier;
            zombie.MoveAndSlide();
            if (_chargeTimer <= 0)
            {
                _charging = false;
                _blocking = true;
                _blockTimer = (float)GD.RandRange(1.5f, 2.5f);
            }
        }
        else if (_blocking)
        {
            _blockTimer -= delta;
            zombie.Velocity = Vector2.Zero;
            if (_blockTimer <= 0)
                _blocking = false;
        }
        else
        {
            if (player != null)
            {
                float dist = zombie.GlobalPosition.DistanceTo(player.GlobalPosition);
                if (dist <= GameConfig.ZombieDefaultAttackRange * 1.5f && GD.Randf() < 0.02f)
                {
                    _charging = true;
                    _chargeTimer = 0.6f;
                    _chargeDir = (player.GlobalPosition - zombie.GlobalPosition).Normalized();
                    zombie.Vfx?.ShowChargeEffect();
                }
                else
                {
                    zombie.Velocity = (player.GlobalPosition - zombie.GlobalPosition).Normalized()
                        * zombie.Speed * zombie.SlowMultiplier;
                    zombie.MoveAndSlide();
                }
            }
        }
    }
}
