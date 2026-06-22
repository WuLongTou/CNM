namespace ZombieWars.Player;

using Godot;
using System;
using System.Collections.Generic;
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
    public event Action<PlayerBuff, float>? BuffTick;

    // ═══════════════════════════════════════════════════
    // State
    // ═══════════════════════════════════════════════════

    private readonly Dictionary<BuffType, PlayerBuff> _activeBuffs = new();
    private Player? _player;

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

        if (isDebuff && _player.Tenacity > 0)
            duration *= Mathf.Max(0.1f, 1.0f - _player.Tenacity / 100.0f);

        // Prevent duplicate debuffs; refresh duration instead
        if (isDebuff && _activeBuffs.TryGetValue(type, out var existing))
        {
            existing.RemainingTime = Mathf.Max(existing.RemainingTime, duration);
            existing.NextTick = tickInterval; // reset tick timer
            // 同步更新 Player 的状态字段，确保 _PhysicsProcess 读到的是刷新后的时长
            if (type == BuffType.Stunned)
                _player.StunDuration = Mathf.Max(_player.StunDuration, duration);
            else if (type == BuffType.Charmed)
                _player.CharmDuration = Mathf.Max(_player.CharmDuration, duration);
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
        foreach (var buff in _activeBuffs.Values)
        {
            _RevertBuffEffects(buff);
            BuffExpired?.Invoke(buff);
        }
        _activeBuffs.Clear();
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
                _ApplyDotDamage((int)buff.Value, new Color(0.3f, 0.9f, 0.2f));
                break;

            case BuffType.Burn:
                _ApplyDotDamage((int)buff.Value, new Color(1.0f, 0.4f, 0.1f));
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

    /// <summary>DOT 伤害直接扣血，不触发无敌帧/震屏/受击动画——否则中毒每 tick 白送 0.5s 无敌。</summary>
    private void _ApplyDotDamage(int amount, Color labelColor)
    {
        if (_player == null || _player.IsDead || amount <= 0) return;
        _player.CurrentHealth = Math.Max(0, _player.CurrentHealth - amount);
        _player.FireHealthChanged(_player.CurrentHealth);
        VFXManager.SpawnFloatingLabel(_player.GetParent(), _player.GlobalPosition,
            $"-{amount}", labelColor, 12);
        if (_player.CurrentHealth <= 0)
            _player.Die();
    }

    /// <summary>
    /// 返回当前所有活跃 debuff 的总伤害速率（每秒扣血）。
    /// HudPanel 用它在顶部显示"[!] 中毒 -X/s"标签，让玩家知道为什么会持续掉血。
    /// </summary>
    public float GetActiveDebuffDamagePerSecond()
    {
        float total = 0f;
        foreach (var kvp in _activeBuffs)
        {
            var buff = kvp.Value;
            if ((buff.Type == BuffType.Poison || buff.Type == BuffType.Burn) && buff.TickInterval > 0)
            {
                // 每次 tick 扣 buff.Value 血，频率 1/tickInterval 次/秒
                total += buff.Value / buff.TickInterval;
            }
        }
        return total;
    }

    /// <summary>
    /// 返回当前活跃 debuff 名称列表（用于 HUD 显示）。
    /// </summary>
    public string GetActiveDebuffDescription()
    {
        if (_activeBuffs.Count == 0) return "";
        var names = new List<string>();
        foreach (var kvp in _activeBuffs)
        {
            var buff = kvp.Value;
            if (buff.Type == BuffType.Poison) names.Add("中毒");
            else if (buff.Type == BuffType.Burn) names.Add("燃烧");
        }
        return string.Join("+", names);
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
                // 中毒伤害由 _ProcessBuffTick 按 tickInterval 处理
                if (_player.Sprite != null)
                    _player.Sprite.Modulate = new Color(0.7f, 0.3f, 0.8f);
                break;

            case BuffType.Burn:
                // Burn stacks with poison; apply via TakeDamage ticks
                break;

            case BuffType.Frozen:
                // FreezeDuration 是玩家冻结敌人的属性，不是玩家被冻结状态
                // 玩家被冻结的减速由 GetSpeedModifier() 统一处理
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
            case BuffType.DamageBoost:
                // 通过 RecalculateStats 统一重算（含装备加成 + buff modifier），
                // 避免直接赋值丢失装备加成、且被下次卡片重算抹掉
                _player.Combat?.RecalculateStats();
                break;
        }
    }

    private void _RevertBuffEffects(PlayerBuff buff)
    {
        if (_player == null) return;

        switch (buff.Type)
        {
            case BuffType.Poison:
                if (_player.Sprite != null && !_activeBuffs.ContainsKey(BuffType.Burn))
                    _player.Sprite.Modulate = Colors.White;
                break;

            case BuffType.Burn:
                if (!_activeBuffs.ContainsKey(BuffType.Poison) && _player.Sprite != null)
                    _player.Sprite.Modulate = Colors.White;
                break;

            case BuffType.Frozen:
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
            case BuffType.DamageBoost:
                _player.Combat?.RecalculateStats();
                break;
        }
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
    }

    private void _ClearAllVisualOverlays()
    {
        if (_player?.Sprite != null)
            _player.Sprite.Modulate = Colors.White;
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

    // ═══════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════

    private static bool _IsDebuff(BuffType type) =>
        type is BuffType.Poison or BuffType.Burn or BuffType.Frozen
         or BuffType.Stunned or BuffType.Charmed;

    private static string _GetDefaultDescription(BuffType type, float value) =>
        type switch
        {
            BuffType.Poison => $"每跳中毒 {value:F0} 伤害",
            BuffType.Burn => $"每跳燃烧 {value:F0} 伤害",
            BuffType.Frozen => "移动速度大幅降低",
            BuffType.Stunned => "无法移动或攻击",
            BuffType.Charmed => "控制方向反转",
            BuffType.Invincible => "免疫所有伤害",
            BuffType.SpeedBoost => $"速度 +{(value - 1.0f) * 100:F0}%",
            BuffType.DamageBoost => $"伤害 +{(value - 1.0f) * 100:F0}%",
            _ => type.ToString()
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
