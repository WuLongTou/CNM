namespace ZombieWars.Player;

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using ZombieWars.Core;
using static ZombieWars.Core.GameEnums;
using ZombieWars.Vfx;

/// <summary>
/// Buff/debuff system for the player.
/// Manages active buffs with tick-based effects, visual overlays,
/// signal emissions, and UI icon display.
/// Attach to Player via AddChild.
/// </summary>
public partial class PlayerBuffSystem : Node
{
    // ═══════════════════════════════════════════════════
    // Signals
    // ═══════════════════════════════════════════════════

    public event Action<PlayerBuff>? BuffApplied;
    public event Action<PlayerBuff>? BuffRemoved;
    public event Action<PlayerBuff>? BuffExpired;
    public event Action<PlayerBuff, float>? BuffTick; // buff + tick damage/heal value

    // ═══════════════════════════════════════════════════
    // State
    // ═══════════════════════════════════════════════════

    private readonly Dictionary<BuffType, PlayerBuff> _activeBuffs = new();
    private Player? _player;

    // UI references
    private Control? _buffIconContainer;
    private readonly Dictionary<BuffType, Label> _buffTimers = new();

    // Cached list to avoid allocation in _Process
    private readonly List<BuffType> _expiredBuffer = new();

    // ═══════════════════════════════════════════════════
    // Buff Color Presets
    // ═══════════════════════════════════════════════════

    private static readonly Dictionary<BuffType, Color> BuffColors = new()
    {
        { BuffType.Poison,      new Color(0.4f, 0.9f, 0.2f) },
        { BuffType.Burn,        new Color(1.0f, 0.35f, 0.1f) },
        { BuffType.Frozen,      new Color(0.3f, 0.7f, 1.0f) },
        { BuffType.Stunned,     new Color(0.9f, 0.9f, 0.2f) },
        { BuffType.Charmed,     new Color(0.9f, 0.3f, 0.9f) },
        { BuffType.Invincible,  new Color(1.0f, 0.85f, 0.3f) },
        { BuffType.SpeedBoost,  new Color(0.3f, 1.0f, 0.6f) },
        { BuffType.DamageBoost, new Color(1.0f, 0.3f, 0.3f) },
    };

    // ═══════════════════════════════════════════════════
    // Setup
    // ═══════════════════════════════════════════════════

    public void Setup(Player player)
    {
        _player = player;
        _player.Died += _OnPlayerDied;
    }

    private void _OnPlayerDied()
    {
        ClearAllBuffs();
    }

    // ═══════════════════════════════════════════════════
    // Core API
    // ═══════════════════════════════════════════════════

    /// <summary>Add a buff to the player. Stacks duration but not value.</summary>
    public bool AddBuff(BuffType type, BuffSource source, float duration, float value,
        float tickInterval = 1.0f, string? description = null)
    {
        if (_player == null) return false;

        bool isDebuff = _IsDebuff(type);

        // Prevent duplicate debuffs; refresh duration instead
        if (isDebuff && _activeBuffs.TryGetValue(type, out var existing))
        {
            existing.RemainingTime = Mathf.Max(existing.RemainingTime, duration);
            existing.NextTick = tickInterval; // reset tick timer
            return false;
        }

        var buff = new PlayerBuff
        {
            Type = type,
            Source = source,
            Duration = duration,
            RemainingTime = duration,
            TickInterval = tickInterval,
            NextTick = tickInterval,
            Value = value,
            Description = description ?? _GetDefaultDescription(type, value),
            VisualColor = BuffColors.TryGetValue(type, out var c) ? c : Colors.White
        };

        _activeBuffs[type] = buff;

        _ApplyBuffEffects(buff);
        _UpdateVisualOverlay(buff);
        _SpawnApplyVFX(buff);

        BuffApplied?.Invoke(buff);
        return true;
    }

    /// <summary>Immediately remove a buff without firing Expired.</summary>
    public void RemoveBuff(BuffType type)
    {
        if (!_activeBuffs.TryGetValue(type, out var buff))
            return;

        _RevertBuffEffects(buff);
        _activeBuffs.Remove(type);
        _ClearVisualOverlay(type);
        BuffRemoved?.Invoke(buff);
    }

    /// <summary>Clear all active buffs (e.g. on death).</summary>
    public void ClearAllBuffs()
    {
        var buffsToExpire = _activeBuffs.Values.ToList();
        _activeBuffs.Clear();
        foreach (var buff in buffsToExpire)
        {
            _RevertBuffEffects(buff);
            BuffExpired?.Invoke(buff);
        }
        _ClearAllVisualOverlays();
    }

    /// <summary>Get a currently active buff by type.</summary>
    public PlayerBuff? GetBuff(BuffType type)
    {
        return _activeBuffs.TryGetValue(type, out var buff) ? buff : null;
    }

    /// <summary>Returns true if the given buff type is currently active.</summary>
    public bool HasBuff(BuffType type) => _activeBuffs.ContainsKey(type);

    /// <summary>Returns the sum of all active speed modifiers (positive or negative).</summary>
    public float GetSpeedModifier()
    {
        float mod = 0.0f;
        if (_activeBuffs.TryGetValue(BuffType.SpeedBoost, out var sb))
            mod += sb.Value - 1.0f;
        if (_activeBuffs.TryGetValue(BuffType.Frozen, out _))
            mod -= 1.0f - GameConfig.FreezeSlowMult;
        if (_activeBuffs.TryGetValue(BuffType.Stunned, out _))
            mod = -0.999f; // nearly zero
        return mod;
    }

    /// <summary>Returns the sum of all active damage modifiers.</summary>
    public float GetDamageModifier()
    {
        float mod = 0.0f;
        if (_activeBuffs.TryGetValue(BuffType.DamageBoost, out var db))
            mod += db.Value - 1.0f;
        return mod;
    }

    // ═══════════════════════════════════════════════════
    // Process Loop
    // ═══════════════════════════════════════════════════

    public override void _Process(double delta)
    {
        if (_player == null) return;

        float d = (float)delta;
        _expiredBuffer.Clear();

        foreach (var kvp in _activeBuffs)
        {
            var buff = kvp.Value;

            // Countdown
            buff.RemainingTime -= d;

            // Tick-based effects
            if (buff.TickInterval > 0)
            {
                buff.NextTick -= d;
                if (buff.NextTick <= 0)
                {
                    buff.NextTick = buff.TickInterval;
                    _ProcessBuffTick(buff);
                }
            }

            // Update timer UI
            _UpdateBuffTimerLabel(buff);

            // Check expiry
            if (buff.RemainingTime <= 0)
            {
                _expiredBuffer.Add(buff.Type);
            }
        }

        // Expire buffs after loop to avoid modifying collection during iteration
        foreach (var type in _expiredBuffer)
        {
            _ExpireBuff(type);
        }
    }

    private void _ProcessBuffTick(PlayerBuff buff)
    {
        if (_player == null || _player.IsDead) return;

        switch (buff.Type)
        {
            case BuffType.Poison:
                _player.TakePassiveDamage((int)buff.Value);
                VFXManager.SpawnFloatingLabel(_player.GetParent(), _player.GlobalPosition,
                    $"-{buff.Value:F0}", new Color(0.3f, 0.9f, 0.2f), 12);
                break;

            case BuffType.Burn:
                _player.TakePassiveDamage((int)buff.Value);
                VFXManager.SpawnFloatingLabel(_player.GetParent(), _player.GlobalPosition,
                    $"-{buff.Value:F0}", new Color(1.0f, 0.4f, 0.1f), 12);
                break;

            case BuffType.Invincible:
                break;

            case BuffType.SpeedBoost:
            case BuffType.DamageBoost:
                // Passive modifiers — no per-tick action needed
                break;
        }

        BuffTick?.Invoke(buff, buff.Value);
    }

    private void _ExpireBuff(BuffType type)
    {
        if (!_activeBuffs.TryGetValue(type, out var buff))
            return;

        _RevertBuffEffects(buff);
        _activeBuffs.Remove(type);
        _ClearVisualOverlay(type);
        BuffExpired?.Invoke(buff);
    }

    // ═══════════════════════════════════════════════════
    // Effect Application / Revert
    // ═══════════════════════════════════════════════════

    private void _ApplyBuffEffects(PlayerBuff buff)
    {
        if (_player == null) return;

        switch (buff.Type)
        {
            case BuffType.Poison:
                _player.PoisonedDps = Mathf.Max(_player.PoisonedDps, buff.Value);
                _player.PoisonedTimer = Mathf.Max(_player.PoisonedTimer, buff.Duration);
                _player.PoisonedTickTimer = Mathf.Min(_player.PoisonedTickTimer, 0.1f);
                if (_player.Sprite != null)
                    _player.Sprite.Modulate = new Color(0.7f, 0.3f, 0.8f);
                break;

            case BuffType.Burn:
                // Burn stacks with poison; apply via TakeDamage ticks
                break;

            case BuffType.Frozen:
                _player.FreezeDuration = Mathf.Max(_player.FreezeDuration, buff.Duration);
                break;

            case BuffType.Stunned:
                _player.StunDuration = Mathf.Max(_player.StunDuration, buff.Duration);
                break;

            case BuffType.Charmed:
                _player.CharmDuration = Mathf.Max(_player.CharmDuration, buff.Duration);
                break;

            case BuffType.Invincible:
                _player.InvincibleTimer = buff.Duration;
                if (_player.Sprite != null)
                    _player.Sprite.Modulate = new Color(1.0f, 0.9f, 0.4f, 0.7f);
                break;

            case BuffType.SpeedBoost:
                _player.CurrentSpeed = _player.BaseSpeed * buff.Value;
                break;

            case BuffType.DamageBoost:
                _player.CurrentDamage = _player.BaseDamage * buff.Value;
                break;
        }
    }

    private void _RevertBuffEffects(PlayerBuff buff)
    {
        if (_player == null) return;

        switch (buff.Type)
        {
            case BuffType.Poison:
                _player.PoisonedDps = 0;
                _player.PoisonedTimer = 0;
                if (_player.Sprite != null && !_activeBuffs.ContainsKey(BuffType.Burn))
                    _player.Sprite.Modulate = Colors.White;
                break;

            case BuffType.Burn:
                if (!_activeBuffs.ContainsKey(BuffType.Poison) && _player.Sprite != null)
                    _player.Sprite.Modulate = Colors.White;
                break;

            case BuffType.Frozen:
                _player.FreezeDuration = 0;
                break;

            case BuffType.Stunned:
                _player.StunDuration = 0;
                break;

            case BuffType.Charmed:
                _player.CharmDuration = 0;
                break;

            case BuffType.Invincible:
                _player.InvincibleTimer = 0;
                if (_player.Sprite != null)
                    _player.Sprite.Modulate = Colors.White;
                break;

            case BuffType.SpeedBoost:
                _RecalculateSpeed();
                break;

            case BuffType.DamageBoost:
                _RecalculateDamage();
                break;
        }
    }

    private void _RecalculateSpeed()
    {
        if (_player == null) return;
        float mod = 1.0f + GetSpeedModifier();
        _player.CurrentSpeed = _player.BaseSpeed * Mathf.Max(0.1f, mod);
    }

    private void _RecalculateDamage()
    {
        if (_player == null) return;
        float mod = 1.0f + GetDamageModifier();
        _player.CurrentDamage = _player.BaseDamage * Mathf.Max(0.1f, mod);
    }

    // ═══════════════════════════════════════════════════
    // Visual Overlay
    // ═══════════════════════════════════════════════════

    private void _UpdateVisualOverlay(PlayerBuff buff)
    {
        if (_player == null || _player.Sprite == null) return;

        // Compose color from all active debuffs
        Color overlay = Colors.White;
        bool hasPoison = _activeBuffs.ContainsKey(BuffType.Poison);
        bool hasBurn = _activeBuffs.ContainsKey(BuffType.Burn);
                bool hasInvincible = _activeBuffs.ContainsKey(BuffType.Invincible);

        if (hasInvincible)
            overlay = new Color(1.0f, 0.9f, 0.4f, 0.7f);
        else if (hasPoison && hasBurn)
            overlay = new Color(0.6f, 0.4f, 0.5f);
        else if (hasPoison)
            overlay = new Color(0.7f, 0.3f, 0.8f);
        else if (hasBurn)
            overlay = new Color(1.0f, 0.5f, 0.2f);

        _player.Sprite.Modulate = overlay;
    }

    private void _ClearVisualOverlay(BuffType type)
    {
        if (!_activeBuffs.ContainsKey(type)) // was last of this type
        {
            // If another buff still active, re-apply overlay; otherwise clear
            if (_player?.Sprite != null)
            {
                bool hasPoison = _activeBuffs.ContainsKey(BuffType.Poison);
                bool hasBurn = _activeBuffs.ContainsKey(BuffType.Burn);
        bool hasInvincible = _activeBuffs.ContainsKey(BuffType.Invincible);

                if (hasInvincible)
                    _player.Sprite.Modulate = new Color(1.0f, 0.9f, 0.4f, 0.7f);
                else if (hasPoison && hasBurn)
                    _player.Sprite.Modulate = new Color(0.6f, 0.4f, 0.5f);
                else if (hasPoison)
                    _player.Sprite.Modulate = new Color(0.7f, 0.3f, 0.8f);
                else if (hasBurn)
                    _player.Sprite.Modulate = new Color(1.0f, 0.5f, 0.2f);
                else
                    _player.Sprite.Modulate = Colors.White;
            }
        }

        if (_buffTimers.TryGetValue(type, out var label))
        {
            if (IsInstanceValid(label)) label.QueueFree();
            _buffTimers.Remove(type);
        }
    }

    private void _ClearAllVisualOverlays()
    {
        if (_player?.Sprite != null)
            _player.Sprite.Modulate = Colors.White;

        foreach (var label in _buffTimers.Values)
        {
            if (IsInstanceValid(label)) label.QueueFree();
        }
        _buffTimers.Clear();
    }

    private void _SpawnApplyVFX(PlayerBuff buff)
    {
        if (_player == null) return;
        var parent = _player.GetParent();
        if (parent == null) return;

        switch (buff.Type)
        {
            case BuffType.Poison:
                VFXManager.SpawnParticleBurst(parent, _player.GlobalPosition,
                    new Color(0.3f, 0.9f, 0.2f, 0.8f), 12, 0.4f, 20, 60);
                break;
            case BuffType.Burn:
                VFXManager.SpawnParticleBurst(parent, _player.GlobalPosition,
                    new Color(1.0f, 0.4f, 0.1f, 0.9f), 15, 0.5f, 30, 80);
                break;
            case BuffType.Frozen:
                VFXManager.SpawnImpactRing(parent, _player.GlobalPosition, 30, Colors.LightBlue, 3.0f);
                break;
            case BuffType.Invincible:
                VFXManager.SpawnImpactRing(parent, _player.GlobalPosition, 35,
                    new Color(1.0f, 0.85f, 0.3f, 0.8f), 2.5f);
                break;
            case BuffType.SpeedBoost:
                VFXManager.SpawnImpactRing(parent, _player.GlobalPosition, 30,
                    new Color(0.3f, 1.0f, 0.6f, 0.7f), 2.0f);
                break;
            case BuffType.DamageBoost:
                VFXManager.SpawnImpactRing(parent, _player.GlobalPosition, 30,
                    new Color(1.0f, 0.3f, 0.3f, 0.7f), 2.0f);
                break;
            default:
                VFXManager.SpawnFloatingLabel(parent, _player.GlobalPosition,
                    buff.Type.ToString(), buff.VisualColor);
                break;
        }
    }

    private void _UpdateBuffTimerLabel(PlayerBuff buff)
    {
        // Timer labels are shown via UIManager buff bar integration point.
        // Emit a signal so the UI can update.
    }

    // ═══════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════

    private static bool _IsDebuff(BuffType type) =>
        type is BuffType.Poison or BuffType.Burn or BuffType.Frozen
         or BuffType.Stunned or BuffType.Charmed;

    private static string _GetDefaultDescription(BuffType type, float value) =>
        type switch
        {
            BuffType.Poison       => $"每跳中毒 {value:F0} 伤害",
            BuffType.Burn         => $"每跳燃烧 {value:F0} 伤害",
            BuffType.Frozen       => "移动速度大幅降低",
            BuffType.Stunned      => "无法移动或攻击",
            BuffType.Charmed      => "控制方向反转",
            BuffType.Invincible  => "免疫所有伤害",
            BuffType.SpeedBoost  => $"速度 +{(value - 1.0f) * 100:F0}%",
            BuffType.DamageBoost => $"伤害 +{(value - 1.0f) * 100:F0}%",
            _                       => type.ToString()
        };
}

// ═══════════════════════════════════════════════════════
// PlayerBuff Data Class
// ═══════════════════════════════════════════════════════

/// <summary>Immutable-ish data container for a single active buff on the player.</summary>
public class PlayerBuff
{
    public BuffType Type { get; init; }
    public BuffSource Source { get; init; }
    public float Duration { get; init; }
    public float RemainingTime { get; set; }
    public float TickInterval { get; set; }
    public float NextTick { get; set; }
    public float Value { get; init; }
    public string Description { get; init; } = "";
    public Color VisualColor { get; init; }
}
