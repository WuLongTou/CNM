namespace ZombieWars.Enemy.Behaviors;

using Godot;

/// <summary>虫群僵尸行为：随机游走为主、缓慢靠近玩家（自移动）。</summary>
public class SwarmBehavior : IZombieBehavior
{
    private float _wanderTimer;
    private Vector2 _wanderDir;

    public void OnReady(Zombie zombie) { }

    public void OnProcess(Zombie zombie, float delta)
    {
        _wanderTimer -= delta;
        if (_wanderTimer <= 0)
        {
            _wanderTimer = (float)GD.RandRange(0.5f, 1.5f);
            float angle = (float)GD.RandRange(0, Mathf.Pi * 2);
            _wanderDir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        var player = zombie.Player;
        if (player != null)
        {
            Vector2 toPlayer = (player.GlobalPosition - zombie.GlobalPosition).Normalized();
            zombie.Velocity = (toPlayer * 0.7f + _wanderDir * 0.3f).Normalized()
                * zombie.Speed * zombie.SlowMultiplier;
            zombie.MoveAndSlide();
        }
    }
}
