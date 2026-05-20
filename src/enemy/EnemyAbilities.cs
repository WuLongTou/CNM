namespace ZombieWars.Enemy;

using System;
using Godot;
using ZombieWars.Core;
using ZombieWars.Vfx;
using static ZombieWars.Core.GameEnums;
using Player = ZombieWars.Player.Player;

/// <summary>
/// Zombie ability system — extends special zombie behavior with new abilities.
/// Attach to a Zombie node or a dedicated zombie-abilities manager node.
/// </summary>
public partial class ZombieAbilitySystem : Node
{
    // ═══════════════════════════════════════════════════
    // Ability Types
    // ═══════════════════════════════════════════════════

    public enum AbilityType
    {
        GhostPhase,
        Teleport,
        Cloner,
        Bomber,
        MindController
    }

    // ═══════════════════════════════════════════════════
    // State
    // ═══════════════════════════════════════════════════

    private AbilityType _ability;
    private Zombie? _zombie;
    private Node2D? _player;

    private float _abilityTimer;
    private float _abilityCooldown;

    // Ghost phase
    private bool _isGhostPhase;

    // Teleport
    private float _teleportDistance = 150.0f;
    private bool _isTeleporting;

    // Cloner
    private bool _hasCloned;
    private int _cloneCount;

    // Bomber
    private bool _isChargingExplosion;
    private float _chargeTimer;
    private float _chargeDuration = 1.2f;

    // Mind controller
    private bool _isControllingPlayer;
    private float _controlTimer;
    private float _controlDuration = 3.0f;
    private float _controlCooldown = 8.0f;

    // ═══════════════════════════════════════════════════
    // Constants
    // ═══════════════════════════════════════════════════

    private const float GhostPhaseInterval = 2.5f;
    private const float GhostPhaseDuration = 1.0f;
    private const float TeleportCooldown = 6.0f;
    private const float CloneHealthPercent = 0.4f;
    private const int MaxClones = 2;

    // ═══════════════════════════════════════════════════
    // Setup
    // ═══════════════════════════════════════════════════

    public void Setup(Zombie zombie, AbilityType ability)
    {
        _zombie = zombie;
        _ability = ability;
        _abilityCooldown = _GetAbilityCooldown(ability);
        _abilityTimer = _abilityCooldown * 0.5f; // first use slightly delayed
    }

    private float _GetAbilityCooldown(AbilityType ability) =>
        ability switch
        {
            AbilityType.GhostPhase     => GhostPhaseInterval,
            AbilityType.Teleport       => TeleportCooldown,
            AbilityType.Cloner         => 9999.0f, // one-shot on death
            AbilityType.Bomber         => 10.0f,
            AbilityType.MindController => _controlCooldown,
            _                           => 5.0f
        };

    // ═══════════════════════════════════════════════════
    // Process Loop
    // ═══════════════════════════════════════════════════

    public override void _Process(double delta)
    {
        if (_zombie == null || _zombie.CurrentHealth <= 0) return;

        float d = (float)delta;

        switch (_ability)
        {
            case AbilityType.GhostPhase:
                _ProcessGhostPhase(d);
                break;
            case AbilityType.Teleport:
                _ProcessTeleport(d);
                break;
            case AbilityType.Cloner:
                // One-shot — handled in OnDeath
                break;
            case AbilityType.Bomber:
                _ProcessBomber(d);
                break;
            case AbilityType.MindController:
                _ProcessMindController(d);
                break;
        }
    }

    // ═══════════════════════════════════════════════════
    // Ghost Phase Ability
    // Zombie becomes ethereal and passes through other zombies
    // ═══════════════════════════════════════════════════

    private void _ProcessGhostPhase(float delta)
    {
        _abilityTimer -= delta;
        if (_abilityTimer <= 0)
        {
            _abilityTimer = _abilityCooldown;
            _TriggerGhostPhase();
        }
    }

    private void _TriggerGhostPhase()
    {
        _isGhostPhase = !_isGhostPhase;

        // Toggle collision with other zombies
        // (In Godot 4, this requires PhysicsLayer manipulation;
        //  simplified here by disabling collision temporarily)
        if (_zombie != null)
        {
            var collision = _zombie.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
            if (collision != null)
            {
                // Reduce zombie-zombie collision repulsion by removing this zombie from
                // repulsion calculations during ghost phase
                collision.SelfModulate = _isGhostPhase
                    ? new Color(1, 1, 1, 0.4f)
                    : Colors.White;
            }

            // Apply speed boost during ghost phase
            if (_isGhostPhase)
            {
                // Store and boost speed
                _zombie.Speed *= 1.3f;
                _SpawnGhostVFX();
            }
            else
            {
                // Restore speed (applied via round scaling stored value)
                _zombie.Speed *= 0.77f; // inverse of 1.3x
            }
        }
    }

    private void _SpawnGhostVFX()
    {
        if (_zombie == null) return;
        var parent = _zombie.GetParent();
        if (parent == null) return;

        // Spawn a ghost particle effect at zombie position
        var ghostSprite = new Sprite2D
        {
            GlobalPosition = _zombie.GlobalPosition,
            Modulate = new Color(0.7f, 0.7f, 1.0f, 0.5f),
            ZIndex = _zombie.ZIndex - 1
        };

        var img = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
        img.Fill(new Color(0.6f, 0.6f, 1.0f, 0.3f));
        ghostSprite.Texture = ImageTexture.CreateFromImage(img);
        parent.AddChild(ghostSprite);

        var tw = ghostSprite.CreateTween();
        tw.TweenProperty(ghostSprite, "modulate:a", 0.0f, GhostPhaseDuration);
        tw.TweenCallback(Callable.From(ghostSprite.QueueFree));
    }

    // ═══════════════════════════════════════════════════
    // Teleport Ability
    // Zombie teleports closer to player
    // ═══════════════════════════════════════════════════

    private void _ProcessTeleport(float delta)
    {
        if (_isTeleporting) return;

        _abilityTimer -= delta;
        if (_abilityTimer <= 0)
        {
            _abilityTimer = _abilityCooldown;
            _TriggerTeleport();
        }
    }

    private void _TriggerTeleport()
    {
        if (_zombie == null || _player == null || !IsInstanceValid(_player)) return;

        Vector2 from = _zombie.GlobalPosition;
        Vector2 toPlayer = _player.GlobalPosition;
        float dist = from.DistanceTo(toPlayer);

        if (dist < _teleportDistance)
        {
            // Too close — skip teleport
            _abilityTimer = _abilityCooldown * 0.5f;
            return;
        }

        _isTeleporting = true;

        // Calculate teleport destination: partway toward player
        Vector2 dir = (toPlayer - from).Normalized();
        Vector2 destination = from + dir * (_teleportDistance * 0.8f);

        // Clamp to screen bounds
        var screen = _zombie.GetViewport().GetVisibleRect().Size;
        destination.X = Mathf.Clamp(destination.X, GameConfig.BoundsPadding, screen.X - GameConfig.BoundsPadding);
        destination.Y = Mathf.Clamp(destination.Y, GameConfig.TopUiMargin, screen.Y - GameConfig.BoundsPadding);

        // Spawn warning indicator
        _SpawnTeleportWarning(destination);

        // Blink out
        var tween = _zombie.CreateTween();
        tween.TweenProperty(_zombie, "modulate:a", 0.0f, 0.15f);
        tween.TweenCallback(Callable.From(() => _CompleteTeleport(from, destination)));
    }

    private void _CompleteTeleport(Vector2 from, Vector2 destination)
    {
        if (_zombie == null) return;

        _zombie.GlobalPosition = destination;

        var tween = _zombie.CreateTween();
        tween.TweenProperty(_zombie, "modulate:a", 1.0f, 0.15f);
        tween.TweenCallback(Callable.From(() => _isTeleporting = false));

        // Spawn arrival particles
        var parent = _zombie.GetParent();
        if (parent != null)
        {
            VFXManager.SpawnImpactRing(parent, destination, 25,
                new Color(0.6f, 0.3f, 1.0f), 2.5f);
        }
    }

    private void _SpawnTeleportWarning(Vector2 pos)
    {
        if (_zombie == null) return;
        var parent = _zombie.GetParent();
        if (parent == null) return;

        // Show expanding ring at destination
        var ring = new Node2D { GlobalPosition = pos };
        var line = new Line2D { Width = 2.0f, DefaultColor = new Color(0.6f, 0.3f, 1.0f, 0.8f) };
        for (int i = 0; i <= 16; i++)
        {
            float a = i * Mathf.Tau / 16;
            line.AddPoint(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 20.0f);
        }
        ring.AddChild(line);
        parent.AddChild(ring);

        var tw = ring.CreateTween();
        tw.TweenProperty(ring, Node2D.PropertyName.Scale.ToString(),
            new Vector2(2.0f, 2.0f), 0.4f);
        tw.Parallel().TweenProperty(ring, Node2D.PropertyName.Modulate.ToString() + ":a", 0.0f, 0.4f);
        tw.TweenCallback(Callable.From(ring.QueueFree));
    }

    // ═══════════════════════════════════════════════════
    // Cloner Ability
    // Zombie splits into 2 smaller copies on death
    // ═══════════════════════════════════════════════════

    public void OnDeath()
    {
        if (_ability != AbilityType.Cloner || _hasCloned) return;
        if (_zombie == null || _cloneCount >= MaxClones) return;

        _hasCloned = true;

        var parent = _zombie.GetParent();
        if (parent == null) return;

        int clonesToSpawn = (_zombie.IsElite || _zombie.IsBoss) ? 2 : 1;

        for (int i = 0; i < clonesToSpawn; i++)
        {
            Vector2 offset = new Vector2(
                (float)GD.RandRange(-40, 40),
                (float)GD.RandRange(-40, 40)
            );
            _SpawnClone(_zombie.GlobalPosition + offset);
            _cloneCount++;
        }
    }

    private void _SpawnClone(Vector2 position)
    {
        if (_zombie == null) return;
        var parent = _zombie.GetParent();
        if (parent == null) return;

        var scene = GD.Load<PackedScene>("res://scenes/Zombie.tscn");
        if (scene == null) return;

        var clone = scene.Instantiate() as Zombie;
        if (clone == null) return;

        clone.GlobalPosition = position;
        clone.Set("zombie_type", (int)ZombieType.Basic);

        // Reduce health to CloneHealthPercent of current
        int cloneHealth = (int)(_zombie.CurrentHealth * CloneHealthPercent);
        clone.Set("max_health", cloneHealth);
        clone.Set("current_health", cloneHealth);
        clone.Scale = new Vector2(0.6f, 0.6f);

        // Inherit player target
        if (_player != null && IsInstanceValid(_player))
            clone.SetPlayerTarget(_player);

        clone.Died += _OnCloneDeath;
        parent.AddChild(clone);

        // Brief spawn effect
        VFXManager.SpawnImpactRing(parent, position, 20,
            new Color(0.5f, 0.8f, 1.0f), 2.0f);

        LogUtils.Print($"克隆僵尸生成于 {position}, 血量: {cloneHealth}");
    }

    private void _OnCloneDeath(Zombie clone)
    {
        _cloneCount--;
        // Clones don't clone further (depth limit)
    }

    // ═══════════════════════════════════════════════════
    // Bomber Ability
    // Enhanced exploder: larger radius + leaves fire trail
    // ═══════════════════════════════════════════════════

    private void _ProcessBomber(float delta)
    {
        if (_isChargingExplosion)
        {
            _chargeTimer -= delta;
            if (_chargeTimer <= 0)
            {
                _TriggerExplosion();
            }
            return;
        }

        _abilityTimer -= delta;
        if (_abilityTimer <= 0)
        {
            _abilityTimer = _abilityCooldown;
            _StartChargingExplosion();
        }
    }

    private void _StartChargingExplosion()
    {
        if (_zombie == null) return;
        _isChargingExplosion = true;
        _chargeTimer = _chargeDuration;

        // Visual: glow red
        if (_zombie.Sprite != null)
            _zombie.Sprite.Modulate = new Color(1.0f, 0.2f, 0.1f);

        // Warning ring
        var parent = _zombie.GetParent();
        if (parent != null)
        {
            var ring = new Node2D { GlobalPosition = _zombie.GlobalPosition };
            var line = new Line2D
            {
                Width = 2.0f,
                DefaultColor = new Color(1.0f, 0.3f, 0.0f, 0.6f)
            };
            float range = GameConfig.ExploderRange * 2.0f;
            for (int i = 0; i <= 20; i++)
            {
                float a = i * Mathf.Tau / 20;
                line.AddPoint(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * range);
            }
            ring.AddChild(line);
            parent.AddChild(ring);
            ((CanvasItem)ring).SetAsTopLevel(true);
            ring.ZIndex = -5;

            var tw = ring.CreateTween();
            tw.TweenProperty(ring, Node2D.PropertyName.Modulate.ToString() + ":a", 0.0f, _chargeDuration);
            tw.TweenCallback(Callable.From(ring.QueueFree));
        }
    }

    private void _TriggerExplosion()
    {
        if (_zombie == null) return;
        var self = _zombie;
        _isChargingExplosion = false;

        float range = GameConfig.ExploderRange * 2.0f;
        int damage = (int)(self.Damage * GameConfig.ExploderDamageMult * 1.5f);

        // Damage player
        if (_player != null && IsInstanceValid(_player) && self.GlobalPosition.DistanceTo(_player.GlobalPosition) <= range)
        {
            if (_player is Player p)
                p.TakeDamage(damage, self);
        }

        // Damage zombies
        foreach (var z in ZombieRegistry.All)
        {
            if (!IsInstanceValid(z) || z == self)
                continue;
            if (self.GlobalPosition.DistanceTo(z.GlobalPosition) <= range)
            {
                z.TakeDamage(damage);
            }
        }

        // Visual explosion
        var parent = self.GetParent();
        if (parent != null)
        {
            VFXManager.SpawnParticleBurst(parent, self.GlobalPosition,
                new Color(1.0f, 0.4f, 0.0f, 0.9f), 40, 0.8f, 60.0f, 150.0f,
                new Vector3(0, -50, 0), 1.0f, 2.5f);
            VFXManager.SpawnImpactRing(parent, self.GlobalPosition, range * 0.5f,
                new Color(1.0f, 0.6f, 0.1f, 0.8f), 4.0f);

            // Spawn fire trail
            _SpawnFireTrail(self.GlobalPosition);
        }

        // Sound
        GameServices.Audio.PlayExplosion();

        // Kill self
        self.TakeDamage(self.CurrentHealth);
    }

    private void _SpawnFireTrail(Vector2 origin)
    {
        if (_zombie == null) return;
        var parent = _zombie.GetParent();
        if (parent == null) return;

        // Leave fire patches on the ground
        for (int i = 0; i < 5; i++)
        {
            Vector2 pos = origin + new Vector2(
                (float)GD.RandRange(-60, 60),
                (float)GD.RandRange(-60, 60)
            );

            var fireNode = new Node2D { GlobalPosition = pos };

            var fireSprite = new Sprite2D
            {
                Modulate = new Color(1.0f, 0.4f, 0.1f, 0.7f)
            };

            var img = Image.CreateEmpty(20, 20, false, Image.Format.Rgba8);
            img.Fill(new Color(1.0f, 0.4f, 0.1f, 0.6f));
            fireSprite.Texture = ImageTexture.CreateFromImage(img);

            fireNode.AddChild(fireSprite);
            parent.AddChild(fireNode);
            ((CanvasItem)fireNode).SetAsTopLevel(true);
            fireNode.ZIndex = -3;

            // Fade out after 3 seconds
            var tw = fireNode.CreateTween();
            tw.TweenProperty(fireNode, Node2D.PropertyName.Modulate.ToString() + ":a", 0.0f, 3.0f);
            tw.TweenCallback(Callable.From(fireNode.QueueFree));
        }
    }

    // ═══════════════════════════════════════════════════
    // Mind Controller Ability
    // Periodically confuses player controls temporarily
    // ═══════════════════════════════════════════════════

    private void _ProcessMindController(float delta)
    {
        if (_isControllingPlayer)
        {
            _controlTimer -= delta;
            if (_controlTimer <= 0)
            {
                _EndMindControl();
            }
            return;
        }

        _abilityTimer -= delta;
        if (_abilityTimer <= 0)
        {
            _abilityTimer = _abilityCooldown;
            _StartMindControl();
        }
    }

    private void _StartMindControl()
    {
        if (_player == null || _zombie == null) return;
        _isControllingPlayer = true;
        _controlTimer = _controlDuration;

        // Show visual effect on player
        if (_player is Player p2)
            p2.ApplyCharmed(_controlDuration);

        // Show mind-control effect
        var parent = _zombie.GetParent();
        if (parent != null)
        {
            VFXManager.SpawnFloatingLabel(parent, _zombie.GlobalPosition,
                "精神控制!", new Color(0.8f, 0.2f, 0.9f), 16);
            VFXManager.SpawnImpactRing(parent, _zombie.GlobalPosition, 60,
                new Color(0.8f, 0.2f, 0.9f, 0.6f), 2.0f);
        }

        LogUtils.Print($"僵尸开始精神控制玩家，持续 {_controlDuration} 秒");
    }

    private void _EndMindControl()
    {
        _isControllingPlayer = false;
        _abilityTimer = _abilityCooldown;

        if (_player != null && IsInstanceValid(_player))
        {
            VFXManager.SpawnFloatingLabel(_player.GetParent(), _player.GlobalPosition,
                "精神控制解除!", new Color(0.5f, 0.8f, 1.0f), 14);
        }
    }

    // ═══════════════════════════════════════════════════
    // Public API
    // ═══════════════════════════════════════════════════

    public void SetPlayerTarget(Node player)
    {
        _player = (Node2D)player;
    }

    public bool IsGhostPhaseActive => _ability == AbilityType.GhostPhase && _isGhostPhase;
    public bool IsMindControlling => _ability == AbilityType.MindController && _isControllingPlayer;
    public AbilityType CurrentAbility => _ability;
}
