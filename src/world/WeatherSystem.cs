namespace ZombieWars.World;

using Godot;
using Godot.Collections;
using System;
using ZombieWars.Core;
using ZombieWars.Player;
using static ZombieWars.Core.GameEnums;
using ZombieWars.Vfx;

/// <summary>
/// Dynamic weather system overlay.
/// Affects gameplay (zombie speed, player damage, visibility) and renders
/// weather-specific visual effects via particles, shaders, and overlays.
/// Add to the scene tree as a CanvasLayer at the top of the z-order.
/// </summary>
public partial class WeatherSystem : CanvasLayer
{
    // ═══════════════════════════════════════════════════
    // Weather Types
    // ═══════════════════════════════════════════════════

    public enum WeatherType
    {
        Clear,
        Rain,
        AcidRain,
        Fog,
        Night,
        BloodMoon,
        Storm
    }

    public enum WeatherIntensity
    {
        Light,
        Moderate,
        Heavy,
        Extreme
    }

    // ═══════════════════════════════════════════════════
    // Properties
    // ═══════════════════════════════════════════════════

    public WeatherType CurrentWeather { get; private set; } = WeatherType.Clear;
    public WeatherIntensity Intensity { get; private set; } = WeatherIntensity.Light;

    // ═══════════════════════════════════════════════════
    // Weather Parameters
    // ═══════════════════════════════════════════════════

    private static readonly float[] IntensityMultipliers = { 0.5f, 1.0f, 1.5f, 2.5f };

    private static readonly float[] ZombieSpeedModForWeather = { 1.0f, 0.8f, 0.6f, 0.5f };
    private static readonly float[] PlayerDamageModForWeather = { 1.0f, 1.0f, 1.2f, 1.5f };
    private static readonly float[] ZombieStealthModForWeather = { 0.0f, 0.1f, 0.25f, 0.4f };
    private static readonly float[] VisibilityModForWeather = { 1.0f, 0.85f, 0.65f, 0.45f };

    // ═══════════════════════════════════════════════════
    // State
    // ═══════════════════════════════════════════════════

    private float _tickTimer;
    private float _tickInterval = 1.0f;

    // 持续推送到 HUD 的频率（秒）。和 _tickInterval 错开，确保 HUD 状态即使
    // 在 tick 之间也能保持最新。
    private float _hudUpdateTimer;
    private float _hudUpdateInterval = 0.5f;

    private record Atmosphere(
        float TintR, float TintG, float TintB,
        float SpriteDarken,
        float FogBrightMult,
        float FireLightMult,
        float Strength
    );

    private static readonly Atmosphere ClearAtmosphere = new(1, 1, 1, 1, 1, 1, 1);
    private static readonly Atmosphere RainAtmosphere = new(0.88f, 0.90f, 1.0f, 0.88f, 0.85f, 0.7f, 1);
    private static readonly Atmosphere AcidAtmosphere = new(0.9f, 1.0f, 0.7f, 0.85f, 0.80f, 0.6f, 1);
    private static readonly Atmosphere FogAtmosphere = new(0.82f, 0.85f, 0.88f, 0.75f, 0.60f, 0.5f, 1);
    private static readonly Atmosphere NightAtmosphere = new(0.70f, 0.72f, 0.85f, 0.65f, 0.40f, 0.3f, 1);
    private static readonly Atmosphere BloodMoonAtmosphere = new(1.1f, 0.78f, 0.72f, 0.90f, 0.85f, 0.8f, 1);
    private static readonly Atmosphere StormAtmosphere = new(0.75f, 0.78f, 0.90f, 0.70f, 0.50f, 0.4f, 1);

    private Atmosphere _currentAtmosphere = ClearAtmosphere;
    private float _targetAtmosphereStrength = 1.0f;
    private float _atmosphereBlendTimer = 0.0f;

    // Lightning
    private float _lightningTimer;
    private float _lightningCooldown = 5.0f;
    private bool _lightningActive;

    private GpuParticles2D? _rainParticles;
    private GpuParticles2D? _acidParticles;
    private GpuParticles2D? _fogParticles;
    private Control? _fogSprite;
    private ColorRect? _vignetteRect;

    // Timers
    private Timer? _weatherChangeTimer;
    private Timer? _lightningTimerNode;

    // ═══════════════════════════════════════════════════
    // Setup
    // ═══════════════════════════════════════════════════

    public override void _Ready()
    {
        _CreateOverlays();
        _CreateParticles();
        _SetupTimers();
    }

    /// <summary>Call from Main after adding WeatherSystem to the tree to start the weather cycle.</summary>
    public void StartWeatherCycle()
    {
        ChangeWeather(WeatherType.Clear, WeatherIntensity.Light);
        if (_weatherChangeTimer != null)
        {
            _weatherChangeTimer.Start();
        }
    }

    private void _CreateOverlays()
    {
        _vignetteRect = new ColorRect
        {
            Name = "VignetteOverlay",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 5
        };
        _vignetteRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        var vigShader = new Shader();
        vigShader.Code = @"
shader_type canvas_item;
uniform float intensity : hint_range(0.0, 1.0) = 0.0;
void fragment() {
    float dist = distance(UV, vec2(0.5));
    float vig = smoothstep(0.35, 0.75, dist);
    COLOR = vec4(0.0, 0.0, 0.0, vig * intensity * 0.25);
}";
        _vignetteRect.Material = new ShaderMaterial { Shader = vigShader };
        AddChild(_vignetteRect);
    }

    private void _CreateParticles()
    {
        // Rain particles — world-integrated, between background and game entities
        _rainParticles = new GpuParticles2D
        {
            Name = "RainParticles",
            ZIndex = 45,
            Emitting = false,
            OneShot = false
        };
        var rainMat = new ParticleProcessMaterial
        {
            Gravity = new Vector3(0, 800, 0),
            InitialVelocityMin = 300,
            InitialVelocityMax = 500,
            Color = new Color(0.5f, 0.6f, 0.8f, 0.5f),
            Spread = 0.1f
        };
        _rainParticles.ProcessMaterial = rainMat;
        AddChild(_rainParticles);

        // Acid rain particles
        _acidParticles = new GpuParticles2D
        {
            Name = "AcidParticles",
            ZIndex = 45,
            Emitting = false,
            OneShot = false
        };
        var acidMat = new ParticleProcessMaterial
        {
            Gravity = new Vector3(0, 600, 0),
            InitialVelocityMin = 200,
            InitialVelocityMax = 400,
            Color = new Color(0.5f, 0.9f, 0.1f, 0.55f),
            Spread = 0.1f
        };
        _acidParticles.ProcessMaterial = acidMat;
        AddChild(_acidParticles);

        // Fog particles (drifting) — world space, above rain
        _fogParticles = new GpuParticles2D
        {
            Name = "FogParticles",
            ZIndex = 46,
            Emitting = false,
            OneShot = false
        };
        var fogMat = new ParticleProcessMaterial
        {
            Gravity = new Vector3(20, 0, 0),
            InitialVelocityMin = 10,
            InitialVelocityMax = 40,
            Color = new Color(0.7f, 0.7f, 0.75f, 0.25f),
            Spread = 0.3f
        };
        _fogParticles.ProcessMaterial = fogMat;
        AddChild(_fogParticles);
    }

    private void _SetupTimers()
    {
        _weatherChangeTimer = new Timer
        {
            WaitTime = 30.0f,
            OneShot = true
        };
        _weatherChangeTimer.Timeout += _OnWeatherTimerExpired;
        AddChild(_weatherChangeTimer);

        _lightningTimerNode = new Timer
        {
            WaitTime = 1.5f,
            OneShot = true
        };
        _lightningTimerNode.Timeout += _EndLightningStrike;
        AddChild(_lightningTimerNode);
    }

    private void _OnWeatherTimerExpired()
    {
        // Randomly change weather
        var types = Enum.GetValues<WeatherType>();
        var newType = types[(int)(GD.Randi() % types.Length)];
        var intensities = Enum.GetValues<WeatherIntensity>();
        var newIntensity = intensities[(int)(GD.Randi() % intensities.Length)];
        ChangeWeather(newType, newIntensity);
    }

    // ═══════════════════════════════════════════════════
    // Public API
    // ═══════════════════════════════════════════════════

    /// <summary>Transition to a new weather type and intensity.</summary>
    public void ChangeWeather(WeatherType type, WeatherIntensity intensity)
    {
        if (CurrentWeather == type && Intensity == intensity)
        {
            // 相同天气早退时仍要重启 OneShot timer，否则超时后天气自动切换永久停摆
            if (_weatherChangeTimer != null)
            {
                _weatherChangeTimer.WaitTime = _GetWeatherDuration(type);
                _weatherChangeTimer.Start();
            }
            return;
        }

        _StopAllWeatherEffects();
        _StartWeatherEffects(type, intensity);

        // Notify game systems
        GetTree().CallGroup("game_manager", "on_weather_changed", (int)type, (int)intensity);
        GetTree().CallGroup("ui_manager", "show_weather_notification", _GetWeatherName(type), _GetWeatherColor(type));

        LogUtils.Print($"天气变化: {_GetWeatherName(CurrentWeather)} → {_GetWeatherName(type)} (强度: {intensity})");

        CurrentWeather = type;
        Intensity = intensity;

        // Auto-change timer
        if (_weatherChangeTimer != null)
        {
            float duration = _GetWeatherDuration(type);
            _weatherChangeTimer.WaitTime = duration;
            _weatherChangeTimer.Start();
        }
    }

    private float _GetWeatherDuration(WeatherType type) =>
        type switch
        {
            WeatherType.BloodMoon => 20.0f,
            WeatherType.Storm => 15.0f,
            _ => 25.0f + (float)GD.RandRange(0, 20)
        };

    /// <summary>Immediately clear all weather effects.</summary>
    public void ClearWeather()
    {
        ChangeWeather(WeatherType.Clear, WeatherIntensity.Light);
    }

    // ═══════════════════════════════════════════════════
    // Gameplay Modifiers
    // Called by game systems to query current weather effects
    // ═══════════════════════════════════════════════════

    public float GetZombieSpeedModifier()
    {
        int i = (int)Intensity;
        return CurrentWeather switch
        {
            WeatherType.Rain => ZombieSpeedModForWeather[i],
            WeatherType.AcidRain => ZombieSpeedModForWeather[i] * 0.8f,
            WeatherType.Fog => ZombieSpeedModForWeather[i] * 0.9f,
            WeatherType.Night => 1.15f * IntensityMultipliers[i],
            WeatherType.BloodMoon => 1.25f,
            WeatherType.Storm => ZombieSpeedModForWeather[i],
            _ => 1.0f
        };
    }

    public float GetPlayerDamageModifier()
    {
        int i = (int)Intensity;
        return CurrentWeather switch
        {
            WeatherType.Night => 1.5f,
            WeatherType.BloodMoon => 1.2f,
            WeatherType.AcidRain => 1.0f - IntensityMultipliers[i] * 0.1f, // slightly less due to hazard
            _ => 1.0f
        };
    }

    public float GetZombieStealthBonus()
    {
        return CurrentWeather switch
        {
            WeatherType.Fog => ZombieStealthModForWeather[(int)Intensity],
            WeatherType.Night => 0.1f,
            _ => 0.0f
        };
    }

    public float GetVisibilityFactor()
    {
        return CurrentWeather switch
        {
            WeatherType.Fog => VisibilityModForWeather[(int)Intensity],
            WeatherType.Night => 0.7f,
            WeatherType.Storm => 0.8f,
            _ => 1.0f
        };
    }

    public bool IsPlayerTakingHazardDamage() => CurrentWeather == WeatherType.AcidRain;

    public float GetAcidRainDamagePerTick() =>
        Intensity switch
        {
            WeatherIntensity.Light => 1.0f,
            WeatherIntensity.Moderate => 2.0f,
            WeatherIntensity.Heavy => 4.0f,
            WeatherIntensity.Extreme => 8.0f,
            _ => 0.0f
        };

    /// <summary>
    /// 返回当前天气的游戏效果摘要，用于 HUD 实时显示。
    /// 只包含与默认值不同的效果，避免晴天时显示一堆 0。
    /// </summary>
    public string GetEffectsDescription()
    {
        var parts = new System.Collections.Generic.List<string>();

        float speedMod = GetZombieSpeedModifier();
        if (!Mathf.IsEqualApprox(speedMod, 1.0f))
        {
            int pct = Mathf.RoundToInt((speedMod - 1.0f) * 100);
            parts.Add($"僵尸速{pct:+#;-#}%");
        }

        float dmgMod = GetPlayerDamageModifier();
        if (!Mathf.IsEqualApprox(dmgMod, 1.0f))
        {
            int pct = Mathf.RoundToInt((dmgMod - 1.0f) * 100);
            parts.Add($"伤害{pct:+#;-#}%");
        }

        float stealth = GetZombieStealthBonus();
        if (stealth > 0.01f)
            parts.Add($"隐匿+{Mathf.RoundToInt(stealth * 100)}%");

        float vis = GetVisibilityFactor();
        if (vis < 0.95f)
            parts.Add($"能见度{Mathf.RoundToInt(vis * 100)}%");

        return string.Join(" ", parts);
    }

    // ═══════════════════════════════════════════════════
    // Process Loop
    // ═══════════════════════════════════════════════════

    public override void _Process(double delta)
    {
        // 模态硬约束：非战斗状态（卡片/暂停/GameOver）不推进天气，避免玩家在 UI 页被酸雨和闪电击杀
        if (GameServices.Game.CurrentState != GameState.Combat)
            return;
        float d = (float)delta;

        // Weather-specific per-tick logic
        _tickTimer += d;
        if (_tickTimer >= _tickInterval)
        {
            _tickTimer -= _tickInterval;
            _OnWeatherTick();
        }

        // Lightning
        if (CurrentWeather == WeatherType.Storm)
        {
            _lightningTimer += d;
            if (_lightningTimer >= _lightningCooldown / IntensityMultipliers[(int)Intensity])
            {
                _lightningTimer = 0.0f;
                _TriggerLightningStrike();
            }
        }

        // 持续推送当前天气到 HUD，让玩家随时能看到（特别酸雨持续掉血时）——
        // 区别于变化时的 ShowWeatherNotification 一次性弹窗。
        _hudUpdateTimer += d;
        if (_hudUpdateTimer >= _hudUpdateInterval)
        {
            _hudUpdateTimer = 0.0f;
            bool isHazard = IsPlayerTakingHazardDamage();
            int dmg = isHazard ? (int)GetAcidRainDamagePerTick() : 0;
            string effects = GetEffectsDescription();
            GetTree().CallGroup(
                "ui_manager", "update_weather_hud",
                _GetWeatherName(CurrentWeather),
                _GetWeatherColor(CurrentWeather),
                isHazard,
                dmg,
                effects
            );
        }
    }

    private void _OnWeatherTick()
    {
        // Acid rain: damage player over time
        if (CurrentWeather == WeatherType.AcidRain)
        {
            var player = GetTree().GetFirstNodeInGroup("player") as Player;
            if (player != null)
            {
                float dmg = GetAcidRainDamagePerTick();
                if (dmg > 0)
                {
                    player.TakeDamage((int)dmg);
                    VFXManager.SpawnFloatingLabel(
                        player.GetParent(), player.GlobalPosition,
                        $"-{dmg:F0}", new Color(0.8f, 1.0f, 0.1f), 12);
                }
            }
        }

        // Storm: spawn additional zombies periodically
        if (CurrentWeather == WeatherType.Storm && Intensity >= WeatherIntensity.Moderate)
        {
            if (GD.Randf() < 0.15f)
            {
                GetTree().CallGroup("game_manager", "spawn_extra_zombie");
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // Weather Effect Rendering
    // ═══════════════════════════════════════════════════

    private void _StartWeatherEffects(WeatherType type, WeatherIntensity intensity)
    {
        int i = (int)intensity;
        float mult = IntensityMultipliers[i];

        switch (type)
        {
            case WeatherType.Rain:
                _StartRain(intensity);
                _ApplyWorldAtmosphere(RainAtmosphere with { Strength = mult });
                _SetVignetteIntensity(0.05f * mult);
                break;

            case WeatherType.AcidRain:
                _StartAcidRain(intensity);
                _ApplyWorldAtmosphere(AcidAtmosphere);
                _SetVignetteIntensity(0.08f * mult);
                break;

            case WeatherType.Fog:
                _StartFog(intensity);
                _ApplyWorldAtmosphere(FogAtmosphere with { Strength = mult });
                _SetVignetteIntensity(0.1f * mult);
                break;

            case WeatherType.Night:
                _ApplyWorldAtmosphere(NightAtmosphere with { Strength = mult });
                _SetVignetteIntensity(0.2f * mult);
                break;

            case WeatherType.BloodMoon:
                _ApplyWorldAtmosphere(BloodMoonAtmosphere);
                _SetVignetteIntensity(0.05f);
                break;

            case WeatherType.Storm:
                _StartRain(intensity);
                _ApplyWorldAtmosphere(StormAtmosphere with { Strength = mult });
                _SetVignetteIntensity(0.15f * mult);
                _lightningCooldown = 4.0f / mult;
                break;
        }
    }

    private void _StopAllWeatherEffects()
    {
        if (_rainParticles != null)
        {
            _rainParticles.Emitting = false;
            _rainParticles.Visible = false;
        }
        if (_acidParticles != null)
        {
            _acidParticles.Emitting = false;
            _acidParticles.Visible = false;
        }
        if (_fogParticles != null)
        {
            _fogParticles.Emitting = false;
            _fogParticles.Visible = false;
        }
        if (_lightningTimerNode != null)
            _lightningTimerNode.Stop();
        _ApplyWorldAtmosphere(ClearAtmosphere);
        _SetVignetteIntensity(0.0f);
    }

    private void _StartRain(WeatherIntensity intensity)
    {
        if (_rainParticles == null) return;

        _rainParticles.Visible = true;
        _rainParticles.Emitting = true;
        _rainParticles.Amount = (int)(200 * IntensityMultipliers[(int)intensity]);
    }

    private void _StartAcidRain(WeatherIntensity intensity)
    {
        if (_acidParticles == null) return;

        _acidParticles.Visible = true;
        _acidParticles.Emitting = true;
        _acidParticles.Amount = (int)(150 * IntensityMultipliers[(int)intensity]);
    }

    private void _StartFog(WeatherIntensity intensity)
    {
        if (_fogParticles == null) return;

        _fogParticles.Visible = true;
        _fogParticles.Emitting = true;
        _fogParticles.Amount = (int)(80 * IntensityMultipliers[(int)intensity]);

        var mat = (ParticleProcessMaterial)_fogParticles.ProcessMaterial;
        mat.Color = new Color(0.8f, 0.8f, 0.85f, 0.25f * IntensityMultipliers[(int)intensity]);
    }

    // ═══════════════════════════════════════════════════
    // Lightning Strike
    // ═══════════════════════════════════════════════════

    private void _TriggerLightningStrike()
    {
        if (_lightningActive) return;
        _lightningActive = true;

        // Flash the screen white
        _FlashLightning();

        // Kill zombies in random area
        var screen = GetViewport().GetVisibleRect().Size;
        Vector2 strikePos = new Vector2(
            (float)GD.RandRange(50, screen.X - 50),
            (float)GD.RandRange(GameConfig.TopUiMargin + 50, screen.Y - 50)
        );

        // Spawn visual lightning bolt
        _SpawnLightningBolt(strikePos);

        // Damage zombies in blast radius
        int blastRadius = (int)(80 * IntensityMultipliers[(int)Intensity]);
        int damage = 50 + (int)(IntensityMultipliers[(int)Intensity] * 30);

        var snapshot = new System.Collections.Generic.List<ZombieWars.Enemy.Zombie>();
        foreach (var z in ZombieRegistry.All)
            snapshot.Add(z);
        foreach (var z in snapshot)
        {
            if (IsInstanceValid(z) && strikePos.DistanceTo(z.GlobalPosition) <= blastRadius)
            {
                z.TakeDamage(damage);
            }
        }

        // Also deal damage to player if nearby
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player != null && strikePos.DistanceTo(player.GlobalPosition) <= blastRadius)
        {
            player.TakeDamage(damage / 2);
        }

        // Sound
        GameServices.Audio.PlayLightning();

        GameServices.UI.ShakeScreen(8.0f);

        _lightningTimerNode?.Start();
    }

    private void _FlashLightning()
    {
        if (_vignetteRect == null) return;

        var mat = (ShaderMaterial)_vignetteRect.Material;
        var origAlpha = mat.GetShaderParameter("intensity");
        mat.SetShaderParameter("intensity", 1.0f);

        var tw = CreateTween();
        tw.TweenInterval(0.15f);
        tw.TweenCallback(Callable.From(() =>
        {
            if (IsInstanceValid(_vignetteRect))
                mat.SetShaderParameter("intensity", origAlpha);
        }));
    }

    private void _SpawnLightningBolt(Vector2 pos)
    {
        var parent = GetTree().CurrentScene;
        if (parent == null) return;

        // Draw jagged lightning line from top of screen to pos
        var line = new Line2D
        {
            Width = 3.0f,
            DefaultColor = new Color(0.9f, 0.95f, 1.0f),
            ZIndex = 110
        };

        Vector2 top = new Vector2(pos.X, 0);
        int segments = 8;
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float xJitter = (float)GD.RandRange(-30, 30);
            float yJitter = (float)GD.RandRange(-20, 20);
            line.AddPoint(top.Lerp(pos, t) + new Vector2(xJitter, yJitter));
        }

        // Glow layer
        var glow = new Line2D
        {
            Points = line.Points,
            Width = 10.0f,
            DefaultColor = new Color(0.6f, 0.8f, 1.0f, 0.4f),
            ZIndex = 109
        };

        parent.AddChild(line);
        parent.AddChild(glow);

        var tw = CreateTween();
        tw.TweenProperty(line, Node2D.PropertyName.Modulate.ToString() + ":a", 0.0f, 0.25f);
        tw.Parallel().TweenProperty(glow, Node2D.PropertyName.Modulate.ToString() + ":a", 0.0f, 0.25f);
        tw.TweenCallback(Callable.From(line.QueueFree));
        tw.TweenCallback(Callable.From(glow.QueueFree));

        // Spawn impact particles
        VFXManager.SpawnParticleBurst(parent, pos,
            new Color(0.7f, 0.9f, 1.0f), 20, 0.5f, 50, 120,
            new Vector3(0, -80, 0), 1.0f, 2.0f);
    }

    private void _EndLightningStrike()
    {
        _lightningActive = false;
    }

    // ═══════════════════════════════════════════════════
    // Transition
    // ═══════════════════════════════════════════════════
    // World Atmosphere — world-integrated, no full-screen overlay
    // ═══════════════════════════════════════════════════

    private void _ApplyWorldAtmosphere(Atmosphere atmosphere)
    {
        _currentAtmosphere = atmosphere;
        _targetAtmosphereStrength = atmosphere.Strength;
        _atmosphereBlendTimer = 0.0f;

        var bg = GetTree().GetFirstNodeInGroup("background") as Background;
        bg?.SetAtmosphereTint(atmosphere.TintR, atmosphere.TintG, atmosphere.TintB, atmosphere.FogBrightMult, atmosphere.FireLightMult);
    }

    private void _SetVignetteIntensity(float intensity)
    {
        if (_vignetteRect?.Material is ShaderMaterial mat)
            mat.SetShaderParameter("intensity", intensity);
    }

    // ═══════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════

    private static string _GetWeatherName(WeatherType type) =>
        type switch
        {
            WeatherType.Clear => "晴朗",
            WeatherType.Rain => "下雨",
            WeatherType.AcidRain => "酸雨",
            WeatherType.Fog => "大雾",
            WeatherType.Night => "夜晚",
            WeatherType.BloodMoon => "血月",
            WeatherType.Storm => "暴风雨",
            _ => "未知"
        };

    private static Color _GetWeatherColor(WeatherType type) =>
        type switch
        {
            WeatherType.Clear => new Color(1.0f, 0.9f, 0.5f),
            WeatherType.Rain => new Color(0.5f, 0.6f, 0.9f),
            WeatherType.AcidRain => new Color(0.6f, 1.0f, 0.1f),
            WeatherType.Fog => new Color(0.7f, 0.7f, 0.75f),
            WeatherType.Night => new Color(0.2f, 0.2f, 0.5f),
            WeatherType.BloodMoon => new Color(0.9f, 0.1f, 0.1f),
            WeatherType.Storm => new Color(0.3f, 0.3f, 0.8f),
            _ => Colors.White
        };
}
