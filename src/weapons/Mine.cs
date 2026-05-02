namespace ZombieWars.Weapons;

using System.Collections.Generic;
using Godot;
using ZombieWars.Core;
using ZombieWars.Enemy;
using ZombieWars.Player;
using static ZombieWars.Core.GameEnums;

public partial class Mine : Area2D
{
    public float Damage { get; set; } = 80.0f;
    public float BlastRadius { get; set; } = 100.0f;
    public float ArmTime { get; set; } = 0.5f;
    public float Lifetime { get; set; } = 15.0f;
    public bool StunOnHit { get; set; } = false;
    public int ClusterCount { get; set; } = 0;
    public Node? Shooter { get; set; }

    private bool _armed;
    private readonly HashSet<Zombie> _hitTargets = new();

    public override void _Ready()
    {
        var col = new CollisionShape2D
        {
            Shape = new CircleShape2D { Radius = 16.0f },
            Disabled = true
        };
        AddChild(col);

        BodyEntered += _OnBodyEntered;

        var armTimer = new Timer { WaitTime = ArmTime, OneShot = true };
        armTimer.Timeout += () =>
        {
            _armed = true;
            col.Disabled = false;
        };
        AddChild(armTimer);
        armTimer.Start();

        var lifeTimer = new Timer { WaitTime = Lifetime, OneShot = true };
        lifeTimer.Timeout += QueueFree;
        AddChild(lifeTimer);
        lifeTimer.Start();

        _DrawMineVisual();
    }

    private void _DrawMineVisual()
    {
        var body = new Line2D();
        for (int i = 0; i <= 12; i++)
        {
            float a = i * Mathf.Pi * 2.0f / 12.0f;
            body.AddPoint(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 8.0f);
        }
        body.Width = 3.0f;
        body.DefaultColor = _armed ? Colors.Red : Colors.Gray;
        AddChild(body);

        var blink = new Line2D();
        for (int i = 0; i <= 8; i++)
        {
            float a = i * Mathf.Pi * 2.0f / 8.0f;
            blink.AddPoint(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 3.0f);
        }
        blink.Width = 2.0f;
        blink.DefaultColor = new Color(1.0f, 0.3f, 0.1f);
        AddChild(blink);
    }

    private void _OnBodyEntered(Node body)
    {
        if (!_armed || body is not Zombie z) return;
        _Detonate();
    }

    private bool _ShouldDetonateOnZombie()
    {
        if (Shooter is Player p && IsInstanceValid(p))
        {
            float dist = GlobalPosition.DistanceTo(p.GlobalPosition);
            if (dist < BlastRadius * 0.4f)
                return false; // too close to shooter, don't detonate
        }
        return true;
    }

    private void _Detonate()
    {
        if (!_ShouldDetonateOnZombie())
            return;
        foreach (var z in ZombieRegistry.All)
        {
            if (!IsInstanceValid(z) || _hitTargets.Contains(z)) continue;
            float dist = GlobalPosition.DistanceTo(z.GlobalPosition);
            if (dist <= BlastRadius)
            {
                float falloff = 1.0f - (dist / BlastRadius) * 0.5f;
                int dmg = (int)(Damage * falloff);
                z.TakeDamage(dmg);
                if (Shooter is Player p) p.RegisterDamageDealt(z, dmg);
                if (StunOnHit) z.ApplyFreeze(1.0f);
                _hitTargets.Add(z);
            }
        }

        if (ClusterCount > 0)
            _SpawnClusterMines();

        _SpawnExplosionVfx();
        GameServices.Audio.PlayExplosion();
        QueueFree();
    }

    private void _SpawnClusterMines()
    {
        for (int i = 0; i < ClusterCount; i++)
        {
            float angle = (float)i / ClusterCount * Mathf.Pi * 2.0f;
            var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 40.0f;
            var clusterMine = new Mine
            {
                Damage = Damage * 0.5f,
                BlastRadius = BlastRadius * 0.6f,
                ArmTime = 0.3f,
                Lifetime = 8.0f,
                ClusterCount = 0,
                Shooter = Shooter
            };
            clusterMine.GlobalPosition = GlobalPosition + offset;
            GetParent().AddChild(clusterMine);
        }
    }

    private void _SpawnExplosionVfx()
    {
        var ring = new Line2D();
        for (int i = 0; i <= 24; i++)
        {
            float a = i * Mathf.Pi * 2.0f / 24.0f;
            ring.AddPoint(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * BlastRadius);
        }
        ring.Width = 4.0f;
        ring.DefaultColor = new Color(1.0f, 0.5f, 0.1f, 0.9f);
        var ringNode = new Node2D { GlobalPosition = GlobalPosition };
        ringNode.AddChild(ring);
        GetParent().AddChild(ringNode);

        var tw = ringNode.CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(ringNode, "scale", new Vector2(1.5f, 1.5f), 0.3f)
          .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(ringNode, "modulate:a", 0.0f, 0.4f);
        tw.SetParallel(false);
        tw.TweenCallback(Callable.From(ringNode.QueueFree));

        var particles = new GpuParticles2D
        {
            GlobalPosition = GlobalPosition,
            Emitting = true,
            Amount = 30,
            Lifetime = 0.5f,
            OneShot = true
        };
        var mat = new ParticleProcessMaterial
        {
            Direction = Vector3.Zero,
            InitialVelocityMin = 60.0f,
            InitialVelocityMax = 150.0f,
            Gravity = new Vector3(0, 80, 0),
            ScaleMin = 1.0f,
            ScaleMax = 3.0f,
            Color = new Color(1.0f, 0.6f, 0.1f, 0.9f)
        };
        particles.ProcessMaterial = mat;
        GetParent().AddChild(particles);
        var cleanup = new Timer { WaitTime = 1.0f, OneShot = true };
        cleanup.Timeout += () => particles.QueueFree();
        particles.AddChild(cleanup);
        cleanup.Start();
    }
}
