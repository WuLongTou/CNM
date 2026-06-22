namespace ZombieWars.Enemy.Behaviors;

using Godot;
using ZombieWars.Core;

/// <summary>隐身僵尸行为：周期显隐切换。</summary>
public class InvisibleBehavior : IZombieBehavior
{
    private float _timer;
    private bool _revealed;

    public void OnReady(Zombie zombie)
    {
        _timer = (float)GD.RandRange(GameConfig.ZombieInvisibleRevealMin, GameConfig.ZombieInvisibleRevealMax);
    }

    public void OnProcess(Zombie zombie, float delta)
    {
        _timer -= delta;
        if (_timer <= 0)
        {
            _timer = (float)GD.RandRange(GameConfig.ZombieInvisibleRevealMin, GameConfig.ZombieInvisibleRevealMax);
            _revealed = !_revealed;
            zombie.Vfx?.ShowInvisible(_revealed);
        }
    }
}
