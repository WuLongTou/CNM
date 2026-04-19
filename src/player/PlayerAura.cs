namespace ZombieWars.Player;

using Godot;
using Godot.Collections;
using System;
using ZombieWars.Core;
using ZombieWars.Vfx;
using Zombie = ZombieWars.Enemy.Zombie;

public partial class PlayerAura : Node2D
{
    public Player? Player { get; set; }

    public bool Active { get; set; } = false;
    public float Damage { get; set; } = Core.GameConfig.AuraBaseDamage;
    public float Radius { get; set; } = Core.GameConfig.AuraBaseRadius;

    private float _tickTimer = 0.0f;
    private float _tickInterval = Core.GameConfig.AuraTickInterval;
    private int _orbCount = Core.GameConfig.AuraOrbCount;
    private float _orbRadius = Core.GameConfig.AuraOrbRadius;

    private readonly Godot.Collections.Array<Area2D> _hitboxes = new();
    private readonly Dictionary<long, float> _hitCooldowns = new();

    public void Setup(Player p)
    {
        Player = p;
    }

    public void Handle(float delta)
    {
        if (!Active)
            return;

        Rotation += Core.GameConfig.AuraRotationSpeed * delta;

        var expired = new Godot.Collections.Array<long>();
        foreach (long key in _hitCooldowns.Keys)
        {
            _hitCooldowns[key] -= delta;
            if (_hitCooldowns[key] <= 0)
                expired.Add(key);
        }
        foreach (long key in expired)
            _hitCooldowns.Remove(key);

        _tickTimer -= delta;
        if (_tickTimer > 0)
            return;
        _tickTimer = _tickInterval;

        float detectRange = _orbRadius * Core.GameConfig.AuraDetectRadiusMult + Core.GameConfig.AuraDetectExtra;

        foreach (var zombie in ZombieRegistry.All)
        {
            if (!IsInstanceValid(zombie))
                continue;

            long zid = (long)zombie.GetInstanceId();
            if (_hitCooldowns.ContainsKey(zid))
                continue;

            foreach (var hitbox in _hitboxes)
            {
                if (!IsInstanceValid(hitbox))
                    continue;
                if (hitbox.GlobalPosition.DistanceTo(zombie.GlobalPosition) < detectRange)
                {
                    zombie.TakeDamage((int)Damage);
                    Player?.RegisterDamageDealt(zombie, Damage);
                    GameServices.UI.ShowDamageNumber(zombie.GlobalPosition, (int)Damage, false);
                    zombie.ApplyFreeze(1.0f);
                    VFXManager.SpawnHitEffect(Player?.GetParent(), zombie.GlobalPosition, new Color(0.3f, 0.8f, 1.0f));
                    _hitCooldowns[zid] = Core.GameConfig.AuraHitCooldown;
                    break;
                }
            }
        }
    }

    public void Activate()
    {
        if (Active)
            return;

        Active = true;
        _hitboxes.Clear();

        for (int i = 0; i < _orbCount; i++)
            _CreateOrb(i);

        _UpdateOrbPositions();
    }

    public void Upgrade(float extraDamage = 3.0f, float extraRadius = 15.0f)
    {
        if (!Active)
            Activate();

        Damage += extraDamage;
        Radius += extraRadius;
        _UpdateOrbPositions();
    }

    private void _CreateOrb(int index)
    {
        var orb = new Area2D
        {
            Monitoring = true,
            Monitorable = false,
            CollisionLayer = 0,
            CollisionMask = 1
        };

        var col = new CollisionShape2D();
        var shape = new CircleShape2D { Radius = _orbRadius * 2 };
        col.Shape = shape;
        orb.AddChild(col);

        var sprite = new Sprite2D();
        int size = 16;
        using var img = Image.Create(size, size, false, Image.Format.Rgba8);
        float center = size / 2.0f;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float dist = new Vector2(x - center, y - center).Length();
                if (dist < 7)
                {
                    float alpha = 1.0f - dist / 7.0f;
                    img.SetPixel(x, y, new Color(0.3f, 0.8f, 1.0f, alpha * 0.9f));
                }
            }
        }
        sprite.Texture = ImageTexture.CreateFromImage(img);
        orb.AddChild(sprite);

        orb.SetMeta("aura_index", index);
        AddChild(orb);
        _hitboxes.Add(orb);
    }

    private void _UpdateOrbPositions()
    {
        foreach (var orb in _hitboxes)
        {
            if (!IsInstanceValid(orb))
                continue;

            int idx = orb.GetMeta("aura_index").AsInt32();
            float angle = idx * Mathf.Tau / Math.Max(1, _orbCount);
            orb.Position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Radius;
        }
    }
}
