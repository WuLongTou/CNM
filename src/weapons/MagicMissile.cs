namespace ZombieWars.Weapons;

using System;
using System.Collections.Generic;
using Godot;
using ZombieWars.Core;
using ZombieWars.Enemy;
using ZombieWars.Player;

public partial class MagicMissile : Area2D
{
    public Zombie? Target { get; set; }
    public float Damage { get; set; } = 10.0f;
    public float Speed { get; set; } = GameConfig.MissileSpeed;
    public float HomingStrength { get; set; } = GameConfig.MissileHomingStrength;
    public float Lifetime { get; set; } = GameConfig.MissileLifetime;
    public Node? Shooter { get; set; }

    public float CharmDuration { get; set; } = 0.0f;
    public float PushForce { get; set; } = 0.0f;
    public float StunDuration { get; set; } = 0.0f;
    public float DotDamage { get; set; } = 0.0f;
    public int ChainCount { get; set; } = 0;
    public string? SpritePath { get; set; }

    private readonly HashSet<Node> _chainTargets = new();

    public override void _Ready()
    {
        BodyEntered += _OnBodyEntered;

        // Visual: use sprite if provided, otherwise generate orb
        if (!string.IsNullOrEmpty(SpritePath))
        {
            var tex = TextureUtils.LoadNormalizedSprite(SpritePath);
            if (tex != null)
            {
                var sprite = new Sprite2D
                {
                    Texture = tex,
                    TextureFilter = CanvasItem.TextureFilterEnum.Nearest
                };
                AddChild(sprite);
            }
            else
            {
                _AddOrbVisual();
            }
        }
        else
        {
            _AddOrbVisual();
        }

        var timer = new Timer
        {
            WaitTime = Lifetime,
            OneShot = true
        };
        timer.Timeout += () => QueueFree();
        AddChild(timer);
        timer.Start();
    }

    private void _AddOrbVisual()
    {
        int size = 12;
        using var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float dist = new Vector2(x - size / 2.0f, y - size / 2.0f).Length();
                if (dist < 2)
                    img.SetPixel(x, y, new Color(1.0f, 1.0f, 1.0f, 0.95f));
                else if (dist < 4)
                {
                    float t = (dist - 2) / 2.0f;
                    img.SetPixel(x, y, new Color(0.8f - t * 0.3f, 0.5f - t * 0.2f, 1.0f, 0.9f - t * 0.3f));
                }
                else if (dist < 6)
                {
                    float t = (dist - 4) / 2.0f;
                    img.SetPixel(x, y, new Color(0.4f, 0.2f, 0.8f, 0.4f * (1.0f - t)));
                }
            }
        }

        var tex = ImageTexture.CreateFromImage(img);
        var sprite = new Sprite2D { Texture = tex };
        AddChild(sprite);

        var trail = new GpuParticles2D
        {
            Emitting = true,
            Amount = 12,
            Lifetime = 0.4f
        };
        var tmat = new ParticleProcessMaterial
        {
            Direction = Vector3.Zero,
            InitialVelocityMin = 5.0f,
            InitialVelocityMax = 20.0f,
            Gravity = Vector3.Zero,
            ScaleMin = 1.0f,
            ScaleMax = 2.5f,
            Color = new Color(0.6f, 0.3f, 1.0f, 0.5f)
        };
        trail.ProcessMaterial = tmat;
        AddChild(trail);
    }

    public override void _PhysicsProcess(double delta)
    {
        float fdelta = (float)delta;

        if (Target != null && IsInstanceValid(Target))
        {
            Vector2 toTarget = Target.GlobalPosition - GlobalPosition;
            float dist = toTarget.Length();
            Vector2 direction = toTarget.Normalized();

            float adaptiveStrength = HomingStrength + 8.0f / Mathf.Max(dist, 10.0f) * 100.0f;

            if (dist < 30.0f)
            {
                GlobalPosition += direction * Speed * fdelta;
            }
            else
            {
                Vector2 currentVelocity = new Vector2(Mathf.Cos(Rotation), Mathf.Sin(Rotation)) * Speed;
                float blend = Mathf.Min(adaptiveStrength * fdelta, 1.0f);
                Vector2 newVelocity = currentVelocity.Lerp(direction * Speed, blend);
                GlobalPosition += newVelocity * fdelta;
                Rotation = newVelocity.Angle();
            }
        }
        else
        {
            GlobalPosition += new Vector2(Mathf.Cos(Rotation), Mathf.Sin(Rotation)) * Speed * fdelta;
        }

        // Auto-cleanup if off-screen
        var screen = GetViewport().GetVisibleRect();
        if (!screen.HasPoint(GlobalPosition))
            QueueFree();
    }

    private void _OnBodyEntered(Node body)
    {
        if (body is not Zombie z || _chainTargets.Contains(z))
            return;

        _DealDamage(z);

        if (ChainCount > 0 && _chainTargets.Count < ChainCount)
        {
            _ChainLightning(z);
        }
        else
        {
            _CreateMagicExplosion();
            QueueFree();
        }
    }

    private void _DealDamage(Zombie zombie)
    {
        zombie.TakeDamage((int)Damage);
        if (Shooter is Player p)
            p.RegisterDamageDealt(zombie, Damage);

        if (CharmDuration > 0)
            zombie.ApplyCharm(CharmDuration);

        if (PushForce > 0)
        {
            Vector2 pushDir = (zombie.GlobalPosition - GlobalPosition).Normalized();
            zombie.Velocity += pushDir * PushForce;
        }

        if (StunDuration > 0)
            zombie.ApplyStun(StunDuration);

        if (DotDamage > 0)
            zombie.ApplyPoison(DotDamage, GameConfig.MissilePoisonDuration);

        _chainTargets.Add(zombie);
    }

    private void _ChainLightning(Zombie currentTarget)
    {
        _chainTargets.Add(currentTarget);

        Zombie? nextTarget = null;
        float minDistance = GameConfig.ChainSearchRange;

        foreach (var zombie in ZombieRegistry.All)
        {
            if (!IsInstanceValid(zombie) || _chainTargets.Contains(zombie))
                continue;

            float distance = currentTarget.GlobalPosition.DistanceTo(zombie.GlobalPosition);
            if (distance < minDistance)
            {
                minDistance = distance;
                nextTarget = zombie;
            }
        }

        if (nextTarget != null)
        {
            Target = nextTarget;
            ChainCount--;
            Damage *= GameConfig.ChainDamageDecay;
            _CreateChainEffect(currentTarget.GlobalPosition, nextTarget.GlobalPosition);
        }
        else
        {
            _CreateMagicExplosion();
            QueueFree();
        }
    }

    private void _CreateChainEffect(Vector2 from, Vector2 to)
    {
        var lightning = new Line2D();
        float dist = from.DistanceTo(to);
        int segments = Mathf.Max(4, (int)(dist / 15.0f));
        Vector2 dir = (to - from).Normalized();
        Vector2 perp = new Vector2(-dir.Y, dir.X);

        lightning.AddPoint(from);
        for (int i = 1; i < segments; i++)
        {
            float t = (float)i / segments;
            Vector2 basePoint = from.Lerp(to, t);
            float offset = (float)GD.RandRange(-8.0, 8.0) * (1.0f - Mathf.Abs(t - 0.5f) * 2.0f);
            lightning.AddPoint(basePoint + perp * offset);
        }
        lightning.AddPoint(to);
        lightning.Width = 3.0f;
        lightning.DefaultColor = new Color(0.5f, 0.7f, 1.0f, 0.9f);
        GetParent().AddChild(lightning);

        var glow = (Line2D)lightning.Duplicate();
        glow.Width = 8.0f;
        glow.DefaultColor = new Color(0.4f, 0.5f, 1.0f, 0.3f);
        GetParent().AddChild(glow);

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(lightning, "modulate:a", 0.0f, 0.25f);
        tween.TweenProperty(glow, "modulate:a", 0.0f, 0.25f);
        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(lightning.QueueFree));
        tween.TweenCallback(Callable.From(glow.QueueFree));
    }

    private void _CreateMagicExplosion()
    {
        var explosion = new GpuParticles2D
        {
            GlobalPosition = GlobalPosition,
            Emitting = true,
            Amount = 35,
            Lifetime = 0.6f,
            OneShot = true
        };

        var particleMat = new ParticleProcessMaterial
        {
            Direction = Vector3.Zero,
            InitialVelocityMin = 50.0f,
            InitialVelocityMax = 160.0f,
            Gravity = Vector3.Zero,
            ScaleMin = 0.8f,
            ScaleMax = 2.0f,
            Color = new Color(0.7f, 0.3f, 1.0f, 0.9f)
        };
        explosion.ProcessMaterial = particleMat;
        GetParent().AddChild(explosion);

        var ring = new Line2D();
        for (int i = 0; i <= 20; i++)
        {
            float a = i * 2.0f * Mathf.Pi / 20.0f;
            ring.AddPoint(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 10.0f);
        }
        ring.Width = 3.0f;
        ring.DefaultColor = new Color(0.8f, 0.4f, 1.0f, 0.9f);

        var ringNode = new Node2D { GlobalPosition = GlobalPosition };
        ringNode.AddChild(ring);
        GetParent().AddChild(ringNode);

        var ringTw = ringNode.CreateTween();
        ringTw.SetParallel(true);
        ringTw.TweenProperty(ringNode, "scale", new Vector2(4, 4), 0.3f)
              .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        ringTw.TweenProperty(ringNode, "modulate:a", 0.0f, 0.3f);
        ringTw.SetParallel(false);
        ringTw.TweenCallback(Callable.From(ringNode.QueueFree));

        var timer = new Timer { WaitTime = 1.0f, OneShot = true };
        timer.Timeout += () => explosion.QueueFree();
        explosion.AddChild(timer);
        timer.Start();
    }
}
