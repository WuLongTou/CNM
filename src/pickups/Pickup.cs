namespace ZombieWars.Pickups;

using Godot;
using ZombieWars.Player;

public abstract partial class Pickup : Area2D
{
    public float Duration { get; set; } = 10.0f;
    public Vector2 FloatOffset { get; set; } = new(0, -8.0f);

    protected Sprite2D _sprite = null!;
    protected float _floatTimer;
    protected Vector2 _basePos;
    protected float _lifetime;
    protected bool _collected;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 0b001; // player layer
        AddToGroup("pickup"); // 供磁铁(MagnetPickup)批量吸引

        BodyEntered += _OnBodyEntered;

        var col = new CollisionShape2D
        {
            Shape = new CircleShape2D { Radius = 16.0f }
        };
        // deferred 添加碰撞体：拾取物常在物理回调链（僵尸死亡掉落）中创建，
        // 立即 AddChild 会触发 "Can't change state while flushing queries" 错误。
        Callable.From(() => AddChild(col)).CallDeferred();

        _CreateSprite();
        _basePos = GlobalPosition;
        _lifetime = Duration;

        var timer = new Timer { WaitTime = Duration, OneShot = true };
        timer.Timeout += _OnExpire;
        AddChild(timer);
        timer.Start();

        // Fade warning
        var fadeTimer = new Timer { WaitTime = Duration * 0.7f, OneShot = true };
        fadeTimer.Timeout += _StartFade;
        AddChild(fadeTimer);
        fadeTimer.Start();

        // Float animation
        _StartFloatAnimation();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_collected) return;

        _floatTimer += (float)delta;
        GlobalPosition = _basePos + new Vector2(0, Mathf.Sin(_floatTimer * 3.0f) * 4.0f);
    }

    private void _OnBodyEntered(Node body)
    {
        if (_collected) return;
        if (body is not Player player) return;

        _collected = true;
        _Collect(player);
        _PlayCollectEffect();
        QueueFree();
    }

    private void _OnExpire()
    {
        if (_collected) return;
        var tw = CreateTween();
        tw.TweenProperty(this, "modulate:a", 0.0f, 0.3f);
        tw.TweenCallback(Callable.From(QueueFree));
    }

    private void _StartFade()
    {
        if (_collected) return;
        var tw = CreateTween();
        tw.TweenProperty(this, "modulate:a", 0.0f, Duration * 0.3f);
    }

    private void _StartFloatAnimation()
    {
        var tw = CreateTween();
        tw.SetLoops();
        tw.TweenProperty(_sprite, "offset:y", -12.0f, 0.8f).SetTrans(Tween.TransitionType.Sine);
        tw.TweenProperty(_sprite, "offset:y", 0.0f, 0.8f).SetTrans(Tween.TransitionType.Sine);
    }

    private void _PlayCollectEffect()
    {
        var ring = new Line2D();
        for (int i = 0; i <= 20; i++)
        {
            float a = i * 2.0f * Mathf.Pi / 20.0f;
            ring.AddPoint(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 10.0f);
        }
        ring.Width = 2.5f;
        ring.DefaultColor = new Color(1.0f, 0.95f, 0.5f, 0.9f);
        var ringNode = new Node2D { GlobalPosition = GlobalPosition };
        ringNode.AddChild(ring);
        GetParent().AddChild(ringNode);

        var tw = ringNode.CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(ringNode, "scale", new Vector2(4, 4), 0.3f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(ringNode, "modulate:a", 0.0f, 0.3f);
        tw.SetParallel(false);
        tw.TweenCallback(Callable.From(ringNode.QueueFree));

        var particles = new GpuParticles2D
        {
            GlobalPosition = GlobalPosition,
            Emitting = true,
            Amount = 15,
            Lifetime = 0.5f,
            OneShot = true
        };
        var mat = new ParticleProcessMaterial
        {
            Direction = Vector3.Zero,
            InitialVelocityMin = 30.0f,
            InitialVelocityMax = 80.0f,
            Gravity = Vector3.Zero,
            ScaleMin = 0.5f,
            ScaleMax = 1.5f,
            Color = new Color(1.0f, 0.85f, 0.4f, 0.8f)
        };
        particles.ProcessMaterial = mat;
        GetParent().AddChild(particles);

        var t = new Timer { WaitTime = 0.8f, OneShot = true };
        t.Timeout += () => particles.QueueFree();
        particles.AddChild(t);
        t.Start();
    }

    /// <summary>磁铁吸引：把浮动基准点移到目标附近，浮动动画会自然过渡过去。</summary>
    public void MagnetTo(Vector2 target)
    {
        _basePos = target + new Vector2(
            (float)GD.RandRange(-25, 25),
            (float)GD.RandRange(-25, 25));
    }

    protected abstract void _CreateSprite();
    protected abstract void _Collect(Player? player);
}