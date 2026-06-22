namespace ZombieWars.Enemy.Behaviors;

using Godot;
using ZombieWars.Core;

/// <summary>飞龙僵尸行为：盘旋 → 俯冲 → 拉高 循环。</summary>
public class DragonBehavior : IZombieBehavior
{
    private float _diveTimer;
    private float _diveCooldown = 4.0f;
    private bool _diving;
    private bool _flying;
    private Vector2 _diveTarget;
    private float _height = 150.0f;
    private float _hoverAngle;

    public void OnReady(Zombie zombie) { }

    public void OnProcess(Zombie zombie, float delta)
    {
        var player = zombie.Player;
        _diveTimer -= delta;
        if (_diving)
        {
            zombie.Velocity = (_diveTarget - zombie.GlobalPosition).Normalized()
                * zombie.Speed * 2.0f * zombie.SlowMultiplier;
            zombie.MoveAndSlide();
            if (zombie.GlobalPosition.DistanceTo(_diveTarget) < 20.0f)
            {
                _diving = false;
                _flying = true;
                zombie.Vfx?.ShowFlyUpEffect();
            }
        }
        else if (_flying)
        {
            Vector2 retreatDir = zombie.Velocity.LengthSquared() > 0.01f
                ? -zombie.Velocity.Normalized()
                : new Vector2(0, -1);
            zombie.Velocity = retreatDir * zombie.Speed * 0.5f * zombie.SlowMultiplier;
            zombie.MoveAndSlide();
            if (zombie.GlobalPosition.Y < _diveTarget.Y - _height)
                _flying = false;
        }
        else if (player != null)
        {
            float dist = zombie.GlobalPosition.DistanceTo(player.GlobalPosition);
            if (dist <= 350.0f && _diveTimer <= 0)
            {
                _diving = true;
                _diveTarget = player.GlobalPosition;
                _diveTimer = _diveCooldown;
                zombie.Vfx?.ShowDiveEffect();
            }
            else
            {
                _hoverAngle += delta * 0.5f;
                Vector2 hover = player.GlobalPosition + new Vector2(
                    Mathf.Cos(_hoverAngle) * 100.0f,
                    -_height + Mathf.Sin(_hoverAngle * 0.3f) * 20.0f);
                zombie.Velocity = (hover - zombie.GlobalPosition).Normalized()
                    * zombie.Speed * 0.6f * zombie.SlowMultiplier;
                zombie.MoveAndSlide();
            }
        }
    }
}
