namespace ZombieWars.Enemy.Behaviors;

using Godot;

/// <summary>飞船僵尸行为：椭圆轨道绕飞 + 水平扫射。</summary>
public class ShipBehavior : IZombieBehavior
{
    private float _strafeTimer;
    private float _strafeDir = 1.0f;
    private float _orbitRadius = 120.0f;
    private float _angle;

    public void OnReady(Zombie zombie) { }

    public void OnProcess(Zombie zombie, float delta)
    {
        var player = zombie.Player;
        _strafeTimer -= delta;
        if (_strafeTimer <= 0)
        {
            _strafeDir *= -1.0f;
            _strafeTimer = (float)GD.RandRange(1.5f, 3.0f);
        }

        _angle += delta * 0.8f * _strafeDir;
        if (player != null)
        {
            Vector2 target = player.GlobalPosition + new Vector2(
                Mathf.Cos(_angle) * _orbitRadius,
                Mathf.Sin(_angle) * _orbitRadius * 0.6f);
            zombie.Velocity = (target - zombie.GlobalPosition).Normalized()
                * zombie.Speed * zombie.SlowMultiplier;
            zombie.MoveAndSlide();
        }
    }
}
