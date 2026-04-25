namespace ZombieWars.Vfx;

using Godot;
using Godot.Collections;
using ZombieWars.Core;
using ZombieWars.UI;
using static ZombieWars.UI.HandDrawnTheme.Colors;

/// <summary>
/// Central VFX manager — eliminates duplicate effect creation across the codebase.
/// All effects are spawned via VFXManager.Spawn*(parent, params).
/// </summary>
public partial class VFXManager : Node
{
	public static VFXManager? Instance { get; private set; }

	// Pre-computed property paths to avoid GC pressure in hot paths
	private static readonly NodePath ScalePath = new(Node2D.PropertyName.Scale);
	private static readonly NodePath ModulatePath = new(Node2D.PropertyName.Modulate);
	private static readonly NodePath PositionPath = new(Node2D.PropertyName.Position);
	private static readonly NodePath ModulateAPath = new(Node2D.PropertyName.Modulate + ":a");
	private static readonly NodePath PositionYPath = new(Node2D.PropertyName.Position + ":y");

	// ══ Generated Effect Sprite Cache ══
	private static readonly string[] EffectSpritePaths = {
		"res://assets/sprites/effects/effect_fire.jpg",
		"res://assets/sprites/effects/effect_ice.jpg",
		"res://assets/sprites/effects/effect_poison.jpg",
		"res://assets/sprites/effects/effect_heal.jpg",
		"res://assets/sprites/effects/effect_lightning.jpg",
		"res://assets/sprites/effects/effect_charm.jpg",
		"res://assets/sprites/effects/effect_death.jpg",
		"res://assets/sprites/effects/effect_explosion.jpg",
		"res://assets/sprites/effects/effect_aura.jpg",
		"res://assets/sprites/effects/effect_shield.jpg",
		"res://assets/sprites/effects/effect_smoke.jpg",
		"res://assets/sprites/effects/effect_stun.jpg",
		"res://assets/sprites/effects/effect_freeze.jpg",
		"res://assets/sprites/effects/effect_crit.jpg",
		"res://assets/sprites/effects/effect_dash.jpg",
		"res://assets/sprites/effects/effect_upgrade.jpg",
	};
	private static Texture2D?[] _effectCache = new Texture2D?[16];
	private static bool _cacheInitialized = false;

	public override void _Ready()
	{
		Instance = this;
		_InitEffectCache();
	}

	private void _InitEffectCache()
	{
		if (_cacheInitialized) return;
		_cacheInitialized = true;
		for (int i = 0; i < EffectSpritePaths.Length; i++)
		{
			if (FileAccess.FileExists(EffectSpritePaths[i]))
				_effectCache[i] = GD.Load<Texture2D>(EffectSpritePaths[i]);
		}
	}

	/// <summary>Load a generated effect sprite. Returns null if not found.</summary>
	public static Texture2D? GetEffectSprite(int index)
	{
		if (index < 0 || index >= _effectCache.Length) return null;
		return _effectCache[index];
	}

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;
	}

	// ══ Impact Ring ══

	public static Node2D SpawnImpactRing(Node parent, Vector2 pos, float radius, Color color, float lineWidth = 2.0f)
	{
		var ring = new Node2D { GlobalPosition = pos };

		var line = new Line2D();
		int pts = GameConfig.VfxImpactRingSegments;
		for (int i = 0; i <= pts; i++)
		{
			float angle = i * Mathf.Tau / pts;
			float jitter = 0.8f + (float)GD.Randf() * 0.5f;
			line.AddPoint(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * jitter);
		}
		line.Width = lineWidth;
		line.DefaultColor = color;
		line.JointMode = Line2D.LineJointMode.Round;
		line.BeginCapMode = Line2D.LineCapMode.Round;
		line.EndCapMode = Line2D.LineCapMode.Round;
		ring.AddChild(line);

		parent.AddChild(ring);

		var tw = ring.CreateTween();
		tw.Parallel().TweenProperty(ring, ScalePath, GameConfig.VfxRingExpandScale, GameConfig.VfxRingExpandDuration)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		tw.Parallel().TweenProperty(ring, ModulateAPath, 0.0f, GameConfig.VfxRingExpandDuration);
		tw.TweenCallback(Callable.From(ring.QueueFree));
		return ring;
	}

	// ══ Particle Burst ══

	public static GpuParticles2D SpawnParticleBurst(Node parent, Vector2 pos, Color color,
		int count = 12, float lifetime = 0.4f, float velMin = 40.0f,
		float velMax = 100.0f, Vector3 gravity = default,
		float scaleMin = 0.8f, float scaleMax = 1.5f)
	{
		// Cap particle count for performance
		count = Mathf.Min(count, GameConfig.VfxExplosionParticles);

		var particles = new GpuParticles2D
		{
			GlobalPosition = pos,
			Emitting = true,
			Amount = count,
			Lifetime = lifetime,
			OneShot = true
		};

		var mat = new ParticleProcessMaterial
		{
			Color = color,
			Gravity = gravity,
			InitialVelocityMin = velMin,
			InitialVelocityMax = velMax,
			ScaleMin = scaleMin,
			ScaleMax = scaleMax
		};
		particles.ProcessMaterial = mat;

		parent.AddChild(particles);
		_AutoFree(parent, particles, lifetime + 0.5f);
		return particles;
	}

	// ══ Hit Effect ══

	public static void SpawnHitEffect(Node parent, Vector2 pos, Color color, float intensity = 1.0f)
	{
		SpawnParticleBurst(parent, pos, color,
			(int)(GameConfig.VfxHitParticleCount * intensity),
			GameConfig.VfxHitLifetime * intensity,
			20.0f * intensity, 60.0f * intensity);
		SpawnImpactRing(parent, pos, 20.0f * intensity, color);
	}

	// ══ Floating Label ══

	public static void SpawnFloatingLabel(Node parent, Vector2 pos, string text,
		Color color, int fontSize = 14, float floatDist = -1,
		float duration = -1, float delayFade = 0.3f)
	{
		if (floatDist < 0) floatDist = GameConfig.VfxPopupFloatDistance;
		if (duration < 0) duration = GameConfig.VfxPopupDuration;

		float xOffset = (float)(GD.Randf() * 30 - 15);
		float yOffset = (float)(GD.Randf() * 15 - 25);
		var label = new Label
		{
			Text = text,
			GlobalPosition = pos + new Vector2(xOffset, yOffset),
			ZIndex = 30
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeConstantOverride("outline_size", 3);
		label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
		parent.AddChild(label);

		var tw = label.CreateTween();
		tw.Parallel().TweenProperty(label, PositionYPath, label.Position.Y - floatDist, duration)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		tw.Parallel().TweenProperty(label, ModulateAPath, 0.0f, duration)
			.SetDelay(delayFade);
		tw.TweenCallback(Callable.From(label.QueueFree));
	}

	// ══ Chain Lightning ══

	public static void SpawnChainLightning(Node parent, Vector2 from, Vector2 to,
		Color color, Color glowColor)
	{
		float dist = from.DistanceTo(to);
		int segments = Mathf.Max(4, (int)(dist / 15.0f));
		var direction = to - from;

		var line = new Line2D { Width = 2.0f, DefaultColor = color };
		for (int i = 0; i <= segments; i++)
		{
			float t = (float)i / segments;
			var basePoint = from + direction * t;
			float jitter = (float)(GD.Randf() * 24 - 12) * (1.0f - Mathf.Abs(t - 0.5f) * 2);
			var perp = direction.Normalized().Rotated(Mathf.Pi / 2);
			line.AddPoint(basePoint + perp * jitter);
		}
		parent.AddChild(line);

		var tw = line.CreateTween();
		tw.Parallel().TweenProperty(line, ModulateAPath, 0.0f, 0.3f);
		tw.TweenCallback(Callable.From(line.QueueFree));
	}

	// ══ Sprite Flash ══

	public static void FlashSprite(Node node, Sprite2D sprite, Color flashColor, float flashDuration = 0.03f, float restoreDuration = 0.07f)
	{
		if (sprite == null) return;
		var tw = node.CreateTween();
		tw.TweenProperty(sprite, ModulatePath, flashColor, flashDuration);
		tw.TweenProperty(sprite, ModulatePath, Colors.White, restoreDuration);
	}

	public static void FlashHurt(Node node, Sprite2D sprite)
	{
		if (sprite == null) return;
		var tw = node.CreateTween();
		tw.TweenProperty(sprite, ModulatePath, new Color(0.9f, 0.3f, 0.2f), 0.05f);
		tw.TweenProperty(sprite, ModulatePath, new Color(1, 1, 1, 0.6f), 0.1f);
	}

	// ══ Ghost Trail ══

	public static void SpawnGhostTrail(Node parent, Sprite2D sourceSprite, Vector2 pos, Color color, float fadeTime = 0.25f)
	{
		if (sourceSprite == null) return;
		var group = new Node2D
		{
			GlobalPosition = pos,
			ZIndex = sourceSprite.ZIndex - 1
		};
		int lineCount = (int)GD.RandRange(3, 6);
		for (int i = 0; i < lineCount; i++)
		{
			var line = new Line2D
			{
				Width = (float)GD.RandRange(1.0f, 2.5f),
				DefaultColor = color
			};
			float len = (float)GD.RandRange(4.0f, 12.0f);
			float angle = (float)(GD.Randf() * Mathf.Tau);
			var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
			line.AddPoint(Vector2.Zero);
			line.AddPoint(dir * len);
			line.Position = new Vector2((float)GD.RandRange(-10f, 10f), (float)GD.RandRange(-10f, 10f));
			group.AddChild(line);
		}
		parent.AddChild(group);

		var tw = group.CreateTween();
		tw.TweenProperty(group, ModulateAPath, 0.0f, fadeTime);
		tw.TweenCallback(Callable.From(group.QueueFree));
	}

	// ══ Auto-Free Utility ══

	private static void _AutoFree(Node parent, Node target, float delay)
	{
		var timer = parent.GetTree().CreateTimer(delay);
		timer.Timeout += () =>
		{
			if (IsInstanceValid(target))
				target.QueueFree();
		};
	}
}
