namespace ZombieWars.Enemy.Behaviors;

using Godot;
using ZombieWars.Core;

/// <summary>召唤师僵尸行为：周期通过 game_manager 组召唤 1-2 个小怪。</summary>
public class SummonerBehavior : IZombieBehavior
{
    private float _cooldown;
    private readonly float _cooldownBase = GameConfig.SummonerCooldown;

    public void OnReady(Zombie zombie) { }

    public void OnProcess(Zombie zombie, float delta)
    {
        _cooldown -= delta;
        if (_cooldown <= 0)
        {
            _cooldown = _cooldownBase;
            _SummonMinions(zombie);
        }
    }

    private static void _SummonMinions(Zombie zombie)
    {
        var pos = zombie.GlobalPosition + new Vector2(
            (float)GD.RandRange(-50, 50), (float)GD.RandRange(-50, 50));
        zombie.GetTree().CallGroup("game_manager", "spawn_minion_zombie", pos);

        if (GD.Randf() < 0.5f)
        {
            var pos2 = zombie.GlobalPosition + new Vector2(
                (float)GD.RandRange(-50, 50), (float)GD.RandRange(-50, 50));
            zombie.GetTree().CallGroup("game_manager", "spawn_minion_zombie", pos2);
        }
    }
}
