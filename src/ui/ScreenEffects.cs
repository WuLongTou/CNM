namespace ZombieWars.UI;

using System.Collections.Generic;
using Godot;
using ZombieWars.Core;
using ZombieWars.Vfx;
using static ZombieWars.UI.HandDrawnTheme;
using static ZombieWars.UI.HandDrawnTheme.Colors;

/// <summary>
/// Screen effects: shake, damage flash, low-HP vignette, attack flash, damage numbers, combo.
/// Optimized with damage number pooling and throttling for better performance.
/// </summary>
public partial class ScreenEffects : Node
{
    private float _shakeIntensity;
    private Vector2 _originalPos;

    private ColorRect _damageFlash = null!;
    private ColorRect _lowHpVignette = null!;
    private ColorRect _attackFlash = null!;
    private ColorRect _critFlash = null!; // 暴击全屏快闪（独立层，最上层，避免与攻击闪光互相覆盖）
    private ColorRect _bossOverlay = null!; // Boss 出场/击杀的全屏冲击层（最高 ZIndex）

    private Label? _comboLabel;
    private int _comboCount;
    private float _comboTimer;

    // Damage number optimization: pooling and throttling
    private const int MaxDamageNumbers = 15;
    private const float DamageNumberThrottle = 0.05f; // Minimum interval between damage numbers in same area
    private readonly Queue<Label> _damageNumberPool = new();
    private readonly List<Label> _activeDamageNumbers = new();
    private float _damageNumberThrottleTimer = 0.0f;
    private Vector2 _lastDamagePos;

    public override void _Ready()
    {
        _CreateDamageFlash();
        _CreateLowHpVignette();
        _CreateAttackFlash();
        _CreateWarningFlash();
        _CreateCritFlash();
        _CreateBossOverlay();
        _InitDamageNumberPool();
    }

    private void _InitDamageNumberPool()
    {
        for (int i = 0; i < MaxDamageNumbers; i++)
        {
            var label = new Label
            {
                Visible = false,
                ZIndex = 20
            };
            label.AddThemeConstantOverride("outline_size", 3);
            label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
            AddChild(label);
            _damageNumberPool.Enqueue(label);
        }
    }

    public void ProcessEffects(float delta)
    {
        if (_shakeIntensity > 0)
        {
            _shakeIntensity = Mathf.Max(0, _shakeIntensity - GameConfig.ShakeDecay * delta);
            var offset = new Vector2(
                (float)GD.RandRange(-_shakeIntensity, _shakeIntensity),
                (float)GD.RandRange(-_shakeIntensity, _shakeIntensity));
            if (GetParent() is Node2D parentNode)
                parentNode.Position = _originalPos + offset;
        }
        else if (GetParent() is Node2D p && p.Position != _originalPos)
        {
            p.Position = _originalPos;
        }

        if (_comboTimer > 0)
        {
            _comboTimer -= delta;
            if (_comboTimer <= 0)
                _comboCount = 0;
        }

        // Update damage number throttle
        if (_damageNumberThrottleTimer > 0)
            _damageNumberThrottleTimer -= delta;

        // Clean up inactive damage numbers
        _activeDamageNumbers.RemoveAll(l => !IsInstanceValid(l) || !l.Visible);
    }

    public void SetOriginalPos(Vector2 pos) => _originalPos = pos;

    // ══ Screen Shake ══

    public void ShakeScreen(float intensity)
    {
        _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);
    }

    // ══ Damage Flash ══

    private void _CreateDamageFlash()
    {
        _damageFlash = new ColorRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 40
        };
        _damageFlash.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _damageFlash.Color = new Color(0, 0, 0, 0);
        AddChild(_damageFlash);
    }

    public void FlashDamage()
    {
        if (_damageFlash == null) return;
        _damageFlash.Color = new Color(BloodBright, 0.2f);
        var tw = CreateTween();
        tw.TweenProperty(_damageFlash, "color:a", 0.0f, 0.5f);
    }

    // ══ Low HP Vignette ══

    private void _CreateLowHpVignette()
    {
        _lowHpVignette = new ColorRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 38,
            Visible = false
        };
        _lowHpVignette.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var shader = new Shader();
        shader.Code = @"
shader_type canvas_item;
uniform float intensity : hint_range(0.0, 1.0) = 0.0;
void fragment() {
	vec2 uv = UV;
	float dist = distance(uv, vec2(0.5));
	float vignette = smoothstep(0.25, 0.7, dist);
	COLOR = vec4(0.5, 0.0, 0.0, vignette * intensity * 0.6);
}";
        var mat = new ShaderMaterial { Shader = shader };
        _lowHpVignette.Material = mat;
        AddChild(_lowHpVignette);
    }

    public void UpdateLowHpVignette(float healthPercent)
    {
        if (_lowHpVignette == null) return;
        if (healthPercent < GameConfig.HealthMedThreshold)
        {
            _lowHpVignette.Visible = true;
            float intensity = (GameConfig.HealthMedThreshold - healthPercent) / GameConfig.HealthMedThreshold;
            ((ShaderMaterial)_lowHpVignette.Material).SetShaderParameter("intensity", intensity);
        }
        else
        {
            _lowHpVignette.Visible = false;
        }
    }

    // ══ Attack Flash ══

    private void _CreateAttackFlash()
    {
        _attackFlash = new ColorRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 39
        };
        _attackFlash.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _attackFlash.Color = new Color(0, 0, 0, 0);
        AddChild(_attackFlash);
    }

    public void FlashAttackColor(Color color)
    {
        if (_attackFlash == null) return;
        _attackFlash.Color = new Color(color.R, color.G, color.B, 0.12f);
        var tw = CreateTween();
        tw.TweenProperty(_attackFlash, "color:a", 0.0f, 0.15f);
    }

    // ══ Crit Flash（暴击全屏锐利快闪，配合命中停顿强化重击感）══

    private void _CreateCritFlash()
    {
        _critFlash = new ColorRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 42 // 高于 warning(41) / damage(40) / attack(39)，暴击瞬间最抢眼
        };
        _critFlash.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _critFlash.Color = new Color(0, 0, 0, 0);
        AddChild(_critFlash);
    }

    /// <summary>暴击命中：暖金高亮全屏快闪（0.16s 锐利淡出，比受击红闪更短更亮）。</summary>
    public void FlashCrit()
    {
        if (_critFlash == null) return;
        _critFlash.Color = new Color(1.0f, 0.92f, 0.55f, 0.32f);
        var tw = CreateTween();
        tw.TweenProperty(_critFlash, "color:a", 0.0f, 0.16f);
    }

    /// <summary>黄金掉落：柔和金色微闪（弱 alpha + 慢淡出），与暴击锐闪区分，提示斐波那契掉落时刻。</summary>
    public void FlashGoldenDrop()
    {
        if (_critFlash == null) return;
        _critFlash.Color = new Color(1.0f, 0.85f, 0.3f, 0.15f);
        var tw = CreateTween();
        tw.TweenProperty(_critFlash, "color:a", 0.0f, 0.3f);
    }

    // ══ Boss 演出层（出场暗化 / 阶段切换 / 击杀强闪）══

    private void _CreateBossOverlay()
    {
        _bossOverlay = new ColorRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 43 // 全 UI 最高层，压过一切（tooltip 200 不参与战斗演出）
        };
        _bossOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _bossOverlay.Color = new Color(0, 0, 0, 0);
        AddChild(_bossOverlay);
    }

    /// <summary>Boss 出场：深红暗化冲击后渐隐，配合命中停顿+震屏营造压迫感。</summary>
    public void FlashBossSpawn()
    {
        if (_bossOverlay == null) return;
        _bossOverlay.Color = new Color(0.18f, 0.0f, 0.0f, 0.55f);
        var tw = CreateTween();
        tw.TweenProperty(_bossOverlay, "color:a", 0.0f, 0.45f);
    }

    /// <summary>Boss 阶段切换：橙红短促爆闪，提示"它变强了"。</summary>
    public void FlashBossPhase()
    {
        if (_bossOverlay == null) return;
        _bossOverlay.Color = new Color(0.8f, 0.25f, 0.0f, 0.4f);
        var tw = CreateTween();
        tw.TweenProperty(_bossOverlay, "color:a", 0.0f, 0.35f);
    }

    /// <summary>Boss 击杀：白金强光爆发，配合慢动作的胜利仪式感。</summary>
    public void FlashBossKill()
    {
        if (_bossOverlay == null) return;
        _bossOverlay.Color = new Color(1.0f, 0.95f, 0.7f, 0.7f);
        var tw = CreateTween();
        tw.TweenProperty(_bossOverlay, "color:a", 0.0f, 0.5f);
    }

    // ══ Warning Flash (for low health / insufficient resources) ══

    private ColorRect? _warningFlash;

    private void _CreateWarningFlash()
    {
        _warningFlash = new ColorRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 41
        };
        _warningFlash.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _warningFlash.Color = new Color(0, 0, 0, 0);
        AddChild(_warningFlash);
    }

    /// <summary>Flash a warning overlay (red edge glow) for events like low health.</summary>
    public void FlashWarning()
    {
        if (_warningFlash == null) return;
        _warningFlash.Color = new Color(BloodRed, 0.25f);
        var tw = CreateTween();
        tw.TweenProperty(_warningFlash, "color:a", 0.0f, 0.6f);
    }

    // ══ Damage Numbers ══

    public void ShowDamageNumber(Vector2 pos, int amount, bool isCritical = false)
    {
        // Throttle: if same position and throttle active, aggregate damage
        if (amount > 0 && _damageNumberThrottleTimer > 0 &&
            pos.DistanceTo(_lastDamagePos) < 30)
        {
            // Skip this damage number - will be handled by aggregation
            return;
        }
        _damageNumberThrottleTimer = DamageNumberThrottle;
        _lastDamagePos = pos;

        // Get pooled label
        Label? label = null;
        if (_damageNumberPool.Count > 0)
        {
            label = _damageNumberPool.Dequeue();
        }
        else if (_activeDamageNumbers.Count >= MaxDamageNumbers)
        {
            // Reuse oldest active label
            label = _activeDamageNumbers[0];
            _activeDamageNumbers.RemoveAt(0);
        }

        if (label == null)
            return;

        int fontSize;
        Color color;
        string text;

        // Scale damage number by damage amount for visual hierarchy
        if (amount < 0)
        {
            color = new Color(0.55f, 0.75f, 0.55f);
            fontSize = 16;
            text = "+" + Mathf.Abs(amount);
        }
        else if (isCritical)
        {
            color = BloodBright;
            fontSize = 32;
            text = amount + "!";
        }
        else if (amount >= 50)
        {
            color = BloodRed;
            fontSize = 22;
            text = amount.ToString();
        }
        else if (amount >= 20)
        {
            color = new Color(0.8f, 0.45f, 0.35f);
            fontSize = 16;
            text = amount.ToString();
        }
        else
        {
            color = NoteText;
            fontSize = 12;
            text = amount.ToString();
        }

        label.Text = text;
        label.GlobalPosition = pos + new Vector2((float)GD.RandRange(-15, 15), (float)GD.RandRange(-25, -15));
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        label.Visible = true;
        label.Scale = Vector2.One;
        label.Modulate = label.Modulate with { A = 1.0f };

        _activeDamageNumbers.Add(label);

        var tw = CreateTween();
        if (isCritical)
        {
            label.Scale = new Vector2(0.5f, 0.5f);
            tw.TweenProperty(label, "scale", new Vector2(1.5f, 1.5f), 0.08f)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(label, "scale", new Vector2(1, 1), 0.1f);
            tw.Parallel().TweenProperty(label, Node2D.PropertyName.Position.ToString() + ":y", label.Position.Y - 55, 0.6f)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tw.Parallel().TweenProperty(label, Node2D.PropertyName.Modulate.ToString() + ":a", 0.0f, 0.6f)
                .SetDelay(0.25f);
            tw.TweenCallback(Callable.From(() => _ReturnToPool(label)));
        }
        else if (amount < 0)
        {
            tw.Parallel().TweenProperty(label, Node2D.PropertyName.Position.ToString() + ":y", label.Position.Y - 35, 0.6f)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tw.Parallel().TweenProperty(label, Node2D.PropertyName.Modulate.ToString() + ":a", 0.0f, 0.6f)
                .SetDelay(0.2f);
            tw.TweenCallback(Callable.From(() => _ReturnToPool(label)));
        }
        else
        {
            tw.Parallel().TweenProperty(label, Node2D.PropertyName.Position.ToString() + ":y", label.Position.Y - 40, 0.55f)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tw.Parallel().TweenProperty(label, Node2D.PropertyName.Modulate.ToString() + ":a", 0.0f, 0.55f)
                .SetDelay(0.12f);
            tw.TweenCallback(Callable.From(() => _ReturnToPool(label)));
        }
    }

    private void _ReturnToPool(Label label)
    {
        if (label == null || !IsInstanceValid(label))
            return;

        label.Visible = false;
        _activeDamageNumbers.Remove(label);
        _damageNumberPool.Enqueue(label);
    }

    // ══ Combo System ══

    public void AddCombo()
    {
        _comboCount++;
        _comboTimer = GameConfig.ComboDecayTime;
        _UpdateComboDisplay();
    }

    private void _UpdateComboDisplay()
    {
        if (_comboCount < 3)
        {
            if (_comboLabel != null && IsInstanceValid(_comboLabel))
            {
                _comboLabel.QueueFree();
                _comboLabel = null;
            }
            return;
        }

        if (_comboLabel == null || !IsInstanceValid(_comboLabel))
        {
            _comboLabel = new Label
            {
                ZIndex = 25
            };
            _comboLabel.AddThemeConstantOverride("outline_size", 3);
            _comboLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
            AddChild(_comboLabel);
        }

        _comboLabel.Text = _comboCount + " COMBO!";
        _comboLabel.AddThemeFontSizeOverride("font_size", 20 + Mathf.Min(_comboCount, 10));

        Color color;
        var comboColors = new[]
        {
            NoteBg,
            new Color(0.95f, 0.75f, 0.65f),
            new Color(0.9f, 0.5f, 0.4f),
            BloodBright
        };
        if (_comboCount >= 12)
            color = comboColors[3];
        else if (_comboCount >= 8)
            color = comboColors[2];
        else if (_comboCount >= 5)
            color = comboColors[1];
        else
            color = comboColors[0];
        _comboLabel.AddThemeColorOverride("font_color", color);

        var screen = GetViewport().GetVisibleRect().Size;
        _comboLabel.Position = new Vector2(screen.X - 200, GameConfig.HudHeight + 6);

        _comboLabel.Scale = new Vector2(0.5f, 0.5f);
        var tw = CreateTween();
        tw.TweenProperty(_comboLabel, "scale", new Vector2(1.2f, 1.2f), 0.08f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(_comboLabel, "scale", new Vector2(1, 1), 0.1f);
    }

    // ══ Kill Effect ══

    public void ShowKillEffect(Vector2 pos, int points)
    {
        VFXManager.SpawnFloatingLabel(this, pos, "+" + points, HandDrawnTheme.Colors.RarityEpic, 16);
    }

    // ══ Offscreen Indicator ══

    public void ShowOffscreenIndicator(Vector2 direction, float dangerLevel)
    {
        var screen = GetViewport().GetVisibleRect().Size;
        var indicator = new ColorRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 35,
            Color = new Color(BloodRed, Mathf.Clamp(dangerLevel * 0.3f, 0.05f, 0.3f))
        };

        float thickness = Mathf.Clamp(dangerLevel * 8, 3, 12);
        if (Mathf.Abs(direction.X) > Mathf.Abs(direction.Y))
        {
            indicator.Size = new Vector2(thickness, screen.Y * 0.3f);
            if (direction.X > 0)
                indicator.Position = new Vector2(screen.X - thickness, screen.Y * 0.35f);
            else
                indicator.Position = new Vector2(0, screen.Y * 0.35f);
        }
        else
        {
            indicator.Size = new Vector2(screen.X * 0.3f, thickness);
            if (direction.Y > 0)
                indicator.Position = new Vector2(screen.X * 0.35f, screen.Y - thickness);
            else
                indicator.Position = new Vector2(screen.X * 0.35f, GameConfig.HudHeight);
        }
        AddChild(indicator);

        var tw = CreateTween();
        tw.TweenProperty(indicator, Node2D.PropertyName.Modulate.ToString() + ":a", 0.0f, 0.5f);
        tw.TweenCallback(Callable.From(indicator.QueueFree));
    }

    // ══ Boss Atmosphere ══

    public void TriggerBossAtmosphere()
    {
        var overlay = new ColorRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 36,
            Color = new Color(BloodRed, 0.25f)
        };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(overlay);

        var tw = CreateTween();
        tw.TweenProperty(overlay, Node2D.PropertyName.Modulate.ToString() + ":a", 0.0f, 2.0f);
        tw.TweenCallback(Callable.From(overlay.QueueFree));
    }
}
