namespace ZombieWars.Weapons;

using System;
using Godot;
using System.Collections.Generic;
using ZombieWars.Core;
using ZombieWars.Enemy;
using ZombieWars.Player;

public partial class Projectile : CharacterBody2D
{
    public Vector2 Velocity;
    public float Damage { get; set; } = 10.0f;
    public float Lifetime { get; set; } = 3.0f;
    public float TrajectoryDamageRatio { get; set; } = 0.0f;
    public int PiercingCount { get; set; } = 0;
    public bool IsReturning { get; set; } = false;
    public Node? Shooter { get; set; }
    public int BouncesLeft { get; set; } = 0;
    public float BoundsPadding { get; set; } = 8.0f;
    public float TopUiMargin { get; set; } = 110.0f;

    private readonly HashSet<Node> _damagedEnemies = new();
    private Area2D _hitArea = null!;

    public override void _Ready()
    {
        // Build a hit detection area so we get body_entered / area_entered signals
        _hitArea = new Area2D
        {
            CollisionLayer = 0,
            CollisionMask = 0b010 // layer 2 — zombie layer
        };
        var col = new CollisionShape2D
        {
            Shape = new CircleShape2D { Radius = 12.0f }
        };
        _hitArea.AddChild(col);
        AddChild(_hitArea);
        _hitArea.BodyEntered += OnBodyEntered;
        _hitArea.AreaEntered += OnAreaEntered;

        // Visual: small arrow/bullet sprite
        var vis = new Line2D();
        vis.AddPoint(new Vector2(-8, 0));
        vis.AddPoint(new Vector2(8, 0));
        vis.Width = 3.0f;
        vis.DefaultColor = new Color(1.0f, 0.85f, 0.3f);
        AddChild(vis);

        var glow = new Line2D();
        glow.AddPoint(new Vector2(-8, 0));
        glow.AddPoint(new Vector2(8, 0));
        glow.Width = 7.0f;
        glow.DefaultColor = new Color(1.0f, 0.95f, 0.5f, 0.35f);
        AddChild(glow);

        var timer = new Timer { WaitTime = Lifetime, OneShot = true };
        timer.Timeout += () => QueueFree();
        AddChild(timer);
        timer.Start();
    }

    public override void _PhysicsProcess(double delta)
    {
        var prevPos = Position;
        Position += Velocity * (float)delta;

        if (Velocity != Vector2.Zero)
            Rotation = Velocity.Angle();

        _HandleBoundsBounce();

        // Distance-based hit-scan: checks every frame for zombies near the flight path
        _CheckHitscan(prevPos, Position);

        if (TrajectoryDamageRatio > 0.0f)
            _CheckTrajectoryCollision();
    }

    private void _CheckHitscan(Vector2 from, Vector2 to)
    {
        float stepLen = 10.0f;
        var dir = to - from;
        float totalDist = dir.Length();
        if (totalDist < 1.0f) return;
        var norm = dir / totalDist;
        int steps = Mathf.Max(1, (int)(totalDist / stepLen));

        var zombies = ZombieRegistry.All;
        for (int s = 0; s <= steps; s++)
        {
            float t = (float)s / steps;
            var checkPos = from + dir * t;
            foreach (var zombie in zombies)
            {
                if (!IsInstanceValid(zombie) || _damagedEnemies.Contains(zombie)) continue;
                if (checkPos.DistanceTo(zombie.GlobalPosition) < 20.0f)
                {
                    _HitZombie(zombie);
                    return;
                }
            }
        }
    }

    private void _HitZombie(Zombie z)
    {
        z.TakeDamage((int)Damage);
        _ApplyShooterEffects(z);
        _damagedEnemies.Add(z);
        _CreateHitEffect();

        if (PiercingCount > 0)
        {
            PiercingCount--;
        }
        else
        {
            if (IsReturning)
                _BounceFromTarget(z);
            else
                QueueFree();
        }
    }

    private void _CheckTrajectoryCollision()
    {
        foreach (var zombie in ZombieRegistry.All)
        {
            if (!IsInstanceValid(zombie) || _damagedEnemies.Contains(zombie))
                continue;

            float dist = Position.DistanceTo(zombie.GlobalPosition);
            if (dist < 30.0f)
            {
                int trajDmg = (int)(Damage * TrajectoryDamageRatio);
                if (trajDmg > 0)
                {
                    zombie.TakeDamage(trajDmg);
                    _damagedEnemies.Add(zombie);
                    if (Shooter is Player p)
                        p.RegisterDamageDealt(zombie, trajDmg);
                    var uiMgr = GetTree().GetFirstNodeInGroup("ui_manager") as UI.UIManager;
                    uiMgr?.ShowDamageNumber(zombie.GlobalPosition, trajDmg, false);
                }
            }
        }
    }

    public void OnBodyEntered(Node body)
    {
        if (body is not Zombie z || _damagedEnemies.Contains(z))
            return;
        _HitZombie(z);
    }

    public void OnAreaEntered(Area2D area)
    {
        if (_damagedEnemies.Contains(area))
            return;
        if (area.HasMethod("TakeDamage"))
        {
            area.Call("TakeDamage", (int)Damage);
            _damagedEnemies.Add(area);
            _CreateHitEffect();
            if (PiercingCount <= 0)
            {
                if (IsReturning) _BounceFromTarget(area);
                else QueueFree();
            }
        }
    }

    private void _BounceFromTarget(Node? target)
    {
        if (BouncesLeft <= 0)
        {
            QueueFree();
            return;
        }
        Vector2 normal = Vector2.Right;
        if (target is Node2D node2d)
            normal = (Position - node2d.GlobalPosition).Normalized();
        Velocity = Velocity.Bounce(normal);
        BouncesLeft--;
        _damagedEnemies.Clear();
    }

    private void _HandleBoundsBounce()
    {
        if (!IsReturning) return;

        Vector2 screenSize = GetViewport().GetVisibleRect().Size;
        Vector2 minPos = new(BoundsPadding, TopUiMargin + BoundsPadding);
        Vector2 maxPos = new(screenSize.X - BoundsPadding, screenSize.Y - BoundsPadding);
        bool bounced = false;

        if (Position.X <= minPos.X || Position.X >= maxPos.X)
        {
            Velocity = new Vector2(-Velocity.X, Velocity.Y);
            bounced = true;
        }
        if (Position.Y <= minPos.Y || Position.Y >= maxPos.Y)
        {
            Velocity = new Vector2(Velocity.X, -Velocity.Y);
            bounced = true;
        }
        if (bounced)
        {
            Position = Position.Clamp(minPos, maxPos);
            _BounceFromTarget(null);
        }
    }

    private void _ApplyShooterEffects(Node target)
    {
        if (Shooter is not Player player) return;
        if (target is not Zombie z) return;

        player.RegisterDamageDealt(z, Damage);
        if (player.FreezeDuration > 0 && GD.Randf() < player.FreezeDuration / 100.0f)
            z.ApplyFreeze(0.8f);
        if (player.PoisonDamage > 0 && GD.Randf() < player.PoisonDamage / 100.0f)
            z.ApplyPoison(3.0f + player.PoisonDamage * 0.2f, 3.0f);
    }

    private void _CreateHitEffect()
    {
        var hitParticles = new GpuParticles2D
        {
            GlobalPosition = Position,
            Emitting = true,
            Amount = 20,
            Lifetime = 0.4f,
            OneShot = true
        };
        var particleMat = new ParticleProcessMaterial
        {
            Direction = Vector3.Zero,
            InitialVelocityMin = 40.0f,
            InitialVelocityMax = 100.0f,
            Gravity = new Vector3(0, 60, 0),
            ScaleMin = 0.5f,
            ScaleMax = 1.2f,
            Color = new Color(1.0f, 0.85f, 0.3f, 0.9f)
        };
        hitParticles.ProcessMaterial = particleMat;
        GetParent().AddChild(hitParticles);

        var ring = new Line2D();
        for (int i = 0; i <= 16; i++)
        {
            float a = i * 2.0f * Mathf.Pi / 16.0f;
            ring.AddPoint(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 8.0f);
        }
        ring.Width = 2.0f;
        ring.DefaultColor = new Color(1.0f, 0.9f, 0.4f, 0.8f);
        var ringNode = new Node2D { GlobalPosition = Position };
        ringNode.AddChild(ring);
        GetParent().AddChild(ringNode);

        var ringTw = ringNode.CreateTween();
        ringTw.SetParallel(true);
        ringTw.TweenProperty(ringNode, "scale", new Vector2(3.0f, 3.0f), 0.25f)
              .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        ringTw.TweenProperty(ringNode, "modulate:a", 0.0f, 0.25f);
        ringTw.SetParallel(false);
        ringTw.TweenCallback(Callable.From(ringNode.QueueFree));

        var timer = new Timer { WaitTime = 0.8f, OneShot = true };
        timer.Timeout += () => hitParticles.QueueFree();
        hitParticles.AddChild(timer);
        timer.Start();
    }
}
