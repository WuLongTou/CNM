namespace ZombieWars.Enemy.Behaviors;

using Godot;
using ZombieWars.Core;

/// <summary>喷毒僵尸行为：周期向玩家吐出抛物线毒液。</summary>
public class SpitterBehavior : IZombieBehavior
{
    private float _cooldown;
    private readonly float _cooldownBase = GameConfig.SpitterCooldown;

    public void OnReady(Zombie zombie) { }

    public void OnProcess(Zombie zombie, float delta)
    {
        _cooldown -= delta;
        if (_cooldown <= 0)
        {
            _cooldown = _cooldownBase;
            var player = zombie.Player;
            if (player != null && zombie.GlobalPosition.DistanceTo(player.GlobalPosition) <= GameConfig.SpitterRange)
                _SpitPoison(zombie, player);
        }
    }

    private static void _SpitPoison(Zombie zombie, Node2D target)
    {
        var proj = new Sprite2D
        {
            Texture = PoisonTextureCache.Get(),
            GlobalPosition = zombie.GlobalPosition
        };
        zombie.GetParent().AddChild(proj);

        var tw = zombie.CreateTween();
        tw.TweenProperty(proj, "global_position", target.GlobalPosition, 0.5);
        tw.TweenCallback(Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(zombie))
            {
                if (GodotObject.IsInstanceValid(proj)) proj.QueueFree();
                return;
            }
            if (GodotObject.IsInstanceValid(target))
            {
                if (target is ZombieWars.Player.Player p)
                {
                    p.TakeDamage(zombie.Damage);
                    p.ApplyBuffFromZombie(GameEnums.BuffType.Poison, GameConfig.SpitterPoisonDuration, GameConfig.SpitterPoisonDps);
                }
                else if (target is IDamageable d)
                    d.TakeDamage(zombie.Damage);
            }
            if (GodotObject.IsInstanceValid(proj)) proj.QueueFree();
        }));
    }
}

/// <summary>毒液贴图缓存，避免每发毒液重复加载。</summary>
internal static class PoisonTextureCache
{
    private static ImageTexture? _tex;

    public static ImageTexture Get()
    {
        if (_tex != null) return _tex;
        var loaded = Core.TextureUtils.LoadNormalizedSprite("res://assets/sprites/effects/effect_poison.jpg");
        if (loaded != null) { _tex = (ImageTexture)loaded; return _tex; }
        var img = Image.CreateEmpty(10, 10, false, Image.Format.Rgba8);
        img.Fill(Colors.Green);
        _tex = ImageTexture.CreateFromImage(img);
        return _tex;
    }
}
