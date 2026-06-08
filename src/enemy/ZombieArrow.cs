namespace ZombieWars.Enemy;

using Godot;
using ZombieWars.Core;
using ZombieWars.Vfx;
using ZombieWars.Player;

/// <summary>
/// ZombieArrow - physics-based homing projectile fired by Archer zombies.
/// Uses Area2D for collision detection against the player layer.
/// </summary>
public partial class ZombieArrow : Area2D, IDamager
{
    public Node2D Target { get; set; } = null!;
    public int Damage { get; set; }
    public Zombie Shooter { get; set; } = null!;

    private Vector2 _velocity;
    private readonly Vector2 _direction;

    public ZombieArrow(Vector2 direction, int damage, Zombie shooter)
    {
        _direction = direction.Normalized();
        Damage = damage;
        Shooter = shooter;
        CollisionLayer = 0;
        CollisionMask = 0b001; // layer 1 (player)
        var col = new CollisionShape2D { Shape = new CircleShape2D { Radius = 6.0f } };
        AddChild(col);
        BodyEntered += _OnBodyEntered;
    }

    public override void _Ready()
    {
        var tex = _GetArrowTexture();
        var sprite = new Sprite2D { Texture = tex, Rotation = _direction.Angle() };
        AddChild(sprite);

        var timer = new Timer { WaitTime = 2.0f, OneShot = true };
        timer.Timeout += QueueFree;
        AddChild(timer);
        timer.Start();

        _velocity = _direction * 350.0f;
    }

    public override void _PhysicsProcess(double delta)
    {
        float fdelta = (float)delta;

        if (IsInstanceValid(Target))
        {
            Vector2 toTarget = Target.GlobalPosition - GlobalPosition;
            Vector2 targetDir = toTarget.Normalized();
            Vector2 desiredVel = targetDir * _velocity.Length();
            _velocity = _velocity.Lerp(desiredVel, fdelta * 4.0f);
        }

        GlobalPosition += _velocity * fdelta;

        if (GetChildCount() > 0 && GetChild(0) is Sprite2D sprite)
            sprite.Rotation = _velocity.Angle();

        // Auto-cleanup if off-screen
        var screen = GetViewport().GetVisibleRect();
        if (!screen.HasPoint(GlobalPosition))
            QueueFree();
    }

    private void _OnBodyEntered(Node body)
    {
        if (body is Player player)
        {
            player.TakeDamage(Damage, IsInstanceValid(Shooter) ? Shooter : null);
            if (IsInstanceValid(Shooter))
                VFXManager.SpawnHitEffect(Shooter.GetParent(), GlobalPosition, new Color(0.8f, 0.6f, 0.3f));
        }
        else if (body is IDamageable d)
        {
            d.TakeDamage(Damage);
        }
        else if (body.HasMethod("TakeDamage"))
        {
            body.Call("TakeDamage", Damage);
        }
        QueueFree();
    }

    private static ImageTexture? _arrowTexture;
    private static ImageTexture _GetArrowTexture()
    {
        if (_arrowTexture != null) return _arrowTexture;
        int bw = 20, bh = 4;
        using var img = Image.CreateEmpty(bw, bh, false, Image.Format.Rgba8);
        for (int x = 0; x < bw; x++)
        {
            for (int y = 0; y < bh; y++)
            {
                img.SetPixel(x, y, x < bw - 5
                    ? new Color(0.5f, 0.3f, 0.1f)
                    : new Color(0.8f, 0.6f, 0.2f));
            }
        }
        _arrowTexture = ImageTexture.CreateFromImage(img);
        return _arrowTexture;
    }
}
