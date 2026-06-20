namespace ZombieWars.World;

using System;
using Godot;
using Godot.Collections;
using ZombieWars.Core;

/// <summary>
/// World-space background — renders ground, fog, fire lights, particles, and decals
/// positioned in world coordinates so the camera scrolls over them.
/// </summary>
public partial class Background : Node2D
{
    private const string AlchemyAtmospherePath = "res://assets/sprites/backgrounds/atmosphere_blood_moon2.jpg";

    private ColorRect? _backgroundRect;
    private TextureRect? _groundLayer;
    private TextureRect? _groundOverlay;
    private TextureRect? _atmosphereLayer;
    private readonly Godot.Collections.Array<TextureRect> _fogLayers = new();
    private MapData? _currentMap;

    public float Tension { get; set; } = 0.0f;

    public override void _Ready()
    {
        ZIndex = -10;
        // 尝试获取当前地图（MapManager._Ready 已设置默认）
        _currentMap = (GetTree().GetFirstNodeInGroup("map_manager") as MapManager)?.Current;
        _CreateBaseBackground();
        _BakeGroundTexture();
        _TryLoadGroundOverlay();
        _CreateAtmosphereOverlay();
        SetProcess(false);
        // 非核心视觉（雾/火光/粒子/地标/装饰物）延迟到下一帧创建，避免阻塞启动
        CallDeferred(nameof(_DeferredVisualSetup));
    }

    private void _DeferredVisualSetup()
    {
        if (!IsInstanceValid(this)) return;
        _CreateFogPatches();
        _CreateFireLights();
        _CreateAshParticles();
        _CreateEmberParticles();
        _CreateAlchemyLandmarks();
        _CreateGroundDecals();
        _CreateSceneProps();
    }

    private Vector2 _WorldSize => MapContext.CurrentWorldSize;

    private void _BakeGroundTexture()
    {
        var img = ProceduralGround.Bake();
        var tex = ImageTexture.CreateFromImage(img);
        var groundSprite = new TextureRect
        {
            Texture = tex,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Size = _WorldSize,
            ZIndex = -5,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _backgroundRect!.AddChild(groundSprite);
        _backgroundRect.MoveChild(groundSprite, 0);
        _groundLayer = groundSprite;
    }

    private void _CreateAtmosphereOverlay()
    {
        string path = _currentMap?.AtmosphereTexturePath ?? AlchemyAtmospherePath;
        if (!FileAccess.FileExists(path))
            return;

        var tex = TextureUtils.LoadTexture(path);
        if (tex == null)
            return;

        var tint = _currentMap?.AtmosphereTint ?? new Color(1.0f, 0.82f, 0.72f);
        float alpha = _currentMap?.AtmosphereAlpha ?? 0.22f;

        _atmosphereLayer = new TextureRect
        {
            Texture = tex,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            Size = _WorldSize,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            Modulate = new Color(tint.R, tint.G, tint.B, alpha),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        _backgroundRect!.AddChild(_atmosphereLayer);
        _backgroundRect.MoveChild(_atmosphereLayer, _groundLayer != null ? 1 : 0);
    }

    private void _CreateBaseBackground()
    {
        _backgroundRect = new ColorRect
        {
            Color = new Color(0.065f, 0.060f, 0.055f),
            Size = _WorldSize,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        AddChild(_backgroundRect);
        _CreateVignette();
    }

    private void _CreateVignette()
    {
        var vignette = new TextureRect
        {
            Size = _WorldSize,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        var vigGradient = new Gradient();
        vigGradient.SetColor(0, new Color(0.10f, 0.09f, 0.08f, 0.0f));
        vigGradient.SetColor(1, new Color(0.02f, 0.02f, 0.015f, 0.65f));
        vigGradient.SetOffset(0, 0.0f);
        vigGradient.SetOffset(1, 1.0f);

        var vigTex = new GradientTexture2D
        {
            Gradient = vigGradient,
            Width = 512,
            Height = 512
        };
        vignette.Texture = vigTex;
        vignette.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
        _backgroundRect!.AddChild(vignette);
    }

    private void _TryLoadGroundOverlay()
    {
        string? path = _currentMap?.GroundTexturePath;
        if (path == null || !FileAccess.FileExists(path))
            return;
        var tex = TextureUtils.LoadTexture(path);
        if (tex == null)
            return;

        var tint = _currentMap?.GroundTint ?? new Color(1.0f, 0.95f, 0.90f);
        _groundOverlay = new TextureRect
        {
            Texture = tex,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            Size = _WorldSize,
            ZIndex = -4,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            Modulate = new Color(tint.R, tint.G, tint.B, 0.16f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _backgroundRect!.AddChild(_groundOverlay);
    }

    private void _CreateSceneProps()
    {
        ScenePropPlacer.Populate(this);
    }

    private float _SimpleHash(int x, int y)
    {
        int n = x * 374761393 + y * 668265263;
        n = (n ^ (n >> 13)) * 1274126177;
        n = n ^ (n >> 16);
        return (int)(Math.Abs(n) % 10000) / 10000.0f;
    }

    private void _CreateFogPatches()
    {
        var ws = _WorldSize;

        var configs = new Godot.Collections.Array<Godot.Collections.Dictionary>
        {
            new() { { "pos", new Vector2(0.15f, 0.25f) }, { "size", 450 }, { "color", new Color(0.12f, 0.10f, 0.08f, 0.06f) } },
            new() { { "pos", new Vector2(0.70f, 0.60f) }, { "size", 500 }, { "color", new Color(0.10f, 0.08f, 0.06f, 0.05f) } },
            new() { { "pos", new Vector2(0.40f, 0.80f) }, { "size", 380 }, { "color", new Color(0.08f, 0.10f, 0.07f, 0.05f) } },
            new() { { "pos", new Vector2(0.85f, 0.20f) }, { "size", 350 }, { "color", new Color(0.10f, 0.09f, 0.06f, 0.04f) } },
        };

        foreach (var cfg in configs)
        {
            int s = (int)cfg["size"];
            Color c = (Color)cfg["color"];
            Vector2 pos = (Vector2)cfg["pos"];
            Vector2 scaledPos = new(ws.X * pos.X, ws.Y * pos.Y);

            var grad = new Gradient();
            grad.SetColor(0, new Color(c.R, c.G, c.B, c.A));
            grad.SetColor(1, new Color(c.R, c.G, c.B, 0.0f));
            grad.SetOffset(0, 0.0f);
            grad.SetOffset(1, 1.0f);

            var gradTex = new GradientTexture2D { Gradient = grad, Width = s, Height = s };

            var fog = new TextureRect
            {
                Texture = gradTex,
                Size = new Vector2(s, s),
                Position = scaledPos - new Vector2(s * 0.5f, s * 0.5f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };

            var mat = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
            fog.Material = mat;
            _backgroundRect!.AddChild(fog);
            _fogLayers.Add(fog);

            var drift = CreateTween().SetLoops(999999);
            float dx = (float)GD.RandRange(20.0f, 50.0f);
            float dt = (float)GD.RandRange(12.0f, 25.0f);
            drift.TweenProperty(fog, "position:x", fog.Position.X + dx, dt)
                 .SetTrans(Tween.TransitionType.Sine);
            drift.TweenProperty(fog, "position:x", fog.Position.X - dx, dt)
                 .SetTrans(Tween.TransitionType.Sine);

            var alphaTw = CreateTween().SetLoops(999999);
            alphaTw.TweenProperty(fog, "modulate:a", (float)GD.RandRange(0.5f, 0.75f), (float)GD.RandRange(6.0f, 12.0f))
                   .SetTrans(Tween.TransitionType.Sine);
            alphaTw.TweenProperty(fog, "modulate:a", (float)GD.RandRange(1.0f, 1.3f), (float)GD.RandRange(6.0f, 12.0f))
                   .SetTrans(Tween.TransitionType.Sine);
        }
    }

    private void _CreateFireLights()
    {
        var ws = _WorldSize;

        var configs = new Godot.Collections.Array<Godot.Collections.Dictionary>
        {
            new() { { "pos", new Vector2(0.08f, 0.30f) }, { "color", new Color(0.95f, 0.45f, 0.10f) }, { "alpha", 0.10f }, { "size", 180 } },
            new() { { "pos", new Vector2(0.88f, 0.25f) }, { "color", new Color(0.85f, 0.35f, 0.08f) }, { "alpha", 0.08f }, { "size", 140 } },
            new() { { "pos", new Vector2(0.55f, 0.80f) }, { "color", new Color(0.80f, 0.25f, 0.05f) }, { "alpha", 0.07f }, { "size", 120 } },
        };

        foreach (var cfg in configs)
        {
            int s = (int)cfg["size"];
            Color c = (Color)cfg["color"];
            float a = (float)cfg["alpha"];
            Vector2 pos = (Vector2)cfg["pos"];
            Vector2 scaledPos = new(ws.X * pos.X, ws.Y * pos.Y);

            var grad = new Gradient
            {
                Colors = new[]
                {
                    new Color(c.R, c.G, c.B, a),
                    new Color(c.R * 0.7f, c.G * 0.4f, c.B * 0.15f, a * 0.6f),
                    new Color(c.R * 0.4f, c.G * 0.2f, c.B * 0.05f, a * 0.25f),
                    Colors.Transparent
                },
                Offsets = new[] { 0.0f, 0.35f, 0.65f, 1.0f }
            };

            var gradTex = new GradientTexture2D { Gradient = grad, Width = s, Height = s };

            var light = new TextureRect
            {
                Texture = gradTex,
                Size = new Vector2(s, s),
                Position = scaledPos - new Vector2(s * 0.5f, s * 0.5f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };

            var mat = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
            light.Material = mat;
            _backgroundRect!.AddChild(light);

            var flicker = CreateTween().SetLoops(999999);
            flicker.TweenProperty(light, "modulate:a", (float)GD.RandRange(0.5f, 0.7f), (float)GD.RandRange(0.3f, 0.7f));
            flicker.TweenProperty(light, "modulate:a", 1.0f, (float)GD.RandRange(0.2f, 0.4f));
            flicker.TweenProperty(light, "modulate:a", (float)GD.RandRange(0.6f, 0.85f), (float)GD.RandRange(0.4f, 0.9f));

            // 真 PointLight2D：给障碍物/角色打暖色光，5 阶径向渐变实现软边缘过渡
            var lightGrad = new Gradient
            {
                Colors = new[] {
                    new Color(1f, 0.85f, 0.5f),   // 中心暖黄
                    new Color(0.8f, 0.55f, 0.2f), // 内圈暖橙
                    new Color(0.5f, 0.25f, 0.1f), // 中圈深琥珀
                    new Color(0.12f, 0.06f, 0.02f, 0.4f), // 外圈过渡半透明
                    Colors.Transparent             // 边缘完全透明
                },
                Offsets = new[] { 0f, 0.25f, 0.5f, 0.78f, 1f }
            };
            var lightTex = new GradientTexture2D { Gradient = lightGrad, Width = 128, Height = 128 };
            var pl = new PointLight2D
            {
                Position = scaledPos,
                Texture = lightTex,
                Color = c,
                Energy = 0.7f,
                TextureScale = Mathf.Max(1f, s / 48f),
                ShadowEnabled = false,
            };
            AddChild(pl);
            var plFlicker = CreateTween().SetLoops(999999);
            plFlicker.TweenProperty(pl, "energy", 0.5f, (float)GD.RandRange(0.3f, 0.7f));
            plFlicker.TweenProperty(pl, "energy", 0.9f, (float)GD.RandRange(0.2f, 0.4f));
        }
    }

    private void _CreateAshParticles()
    {
        var ws = _WorldSize;

        var ash = new GpuParticles2D
        {
            Amount = 60,
            Lifetime = 8.0f,
            Preprocess = 4.0f,
            Position = ws * 0.5f
        };

        var mat = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(ws.X * 0.55f, ws.Y * 0.55f, 0),
            Direction = new Vector3(0.1f, -1, 0),
            Spread = 90.0f,
            InitialVelocityMin = 1.0f,
            InitialVelocityMax = 4.0f,
            Gravity = new Vector3(0, -0.5f, 0),
            ScaleMin = 0.6f,
            ScaleMax = 1.8f,
            Color = new Color(0.55f, 0.50f, 0.42f, 0.35f),
            TurbulenceEnabled = true,
            TurbulenceInfluenceMin = 0.3f,
            TurbulenceInfluenceMax = 0.7f
        };

        var colorRamp = new Gradient
        {
            Colors = new[]
            {
                new Color(0.6f, 0.55f, 0.48f, 0.0f),
                new Color(0.55f, 0.50f, 0.42f, 0.35f),
                new Color(0.50f, 0.45f, 0.38f, 0.0f)
            },
            Offsets = new[] { 0.0f, 0.3f, 1.0f }
        };

        mat.ColorRamp = new GradientTexture1D { Gradient = colorRamp };
        ash.ProcessMaterial = mat;
        _backgroundRect!.AddChild(ash);
    }

    private void _CreateEmberParticles()
    {
        var ws = _WorldSize;

        var embers = new GpuParticles2D
        {
            Amount = 18,
            Lifetime = 4.0f,
            Preprocess = 2.0f,
            Position = ws * 0.5f
        };

        var mat = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(ws.X * 0.55f, ws.Y * 0.55f, 0),
            Direction = new Vector3(0.1f, -1, 0),
            Spread = 70.0f,
            InitialVelocityMin = 3.0f,
            InitialVelocityMax = 8.0f,
            Gravity = new Vector3(0, -2.0f, 0),
            ScaleMin = 0.8f,
            ScaleMax = 2.5f,
            TurbulenceEnabled = true,
            TurbulenceInfluenceMin = 0.4f,
            TurbulenceInfluenceMax = 0.9f
        };

        var colorRamp = new Gradient
        {
            Colors = new[]
            {
                new Color(1.0f, 0.7f, 0.2f, 0.0f),
                new Color(1.0f, 0.5f, 0.15f, 0.70f),
                new Color(0.8f, 0.25f, 0.05f, 0.50f),
                new Color(0.4f, 0.1f, 0.02f, 0.0f)
            },
            Offsets = new[] { 0.0f, 0.15f, 0.6f, 1.0f }
        };

        mat.ColorRamp = new GradientTexture1D { Gradient = colorRamp };
        embers.ProcessMaterial = mat;
        _backgroundRect!.AddChild(embers);
    }

    private void _CreateGroundDecals()
    {
        var ws = _WorldSize;

        for (int i = 0; i < 8; i++)
        {
            int s = (int)GD.RandRange(20, 50);
            var decal = new TextureRect
            {
                Size = new Vector2(s, s),
                Position = new Vector2((float)GD.RandRange(30, ws.X - 30), (float)GD.RandRange(30, ws.Y - 30)),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Rotation = (float)GD.RandRange(0.0f, Mathf.Tau)
            };

            using var img = Image.CreateEmpty(s, s, false, Image.Format.Rgba8);
            float center = s / 2.0f;
            bool isBlood = GD.Randf() < 0.6f;

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float d = new Vector2(x - center, y - center).Length();
                    if (d <= center)
                    {
                        float falloff = 1.0f - (d / center);
                        falloff = falloff * falloff;
                        float noise = _SimpleHash(x * 17 + i, y * 31 + i) * 0.3f;
                        float alpha = falloff * (0.15f + noise * 0.1f);
                        Color c;
                        if (isBlood)
                            c = new Color(0.30f + noise * 0.1f, 0.04f, 0.03f, alpha);
                        else
                            c = new Color(0.06f, 0.05f, 0.04f, alpha * 1.5f);
                        img.SetPixel(x, y, c);
                    }
                }
            }

            decal.Texture = ImageTexture.CreateFromImage(img);
            _backgroundRect!.AddChild(decal);
        }
    }

    private void _CreateAlchemyLandmarks()
    {
        _CreatePathRibbon();
        _CreateRuneCircles();
        _CreateRuinMarkers();
    }

    private void _CreatePathRibbon()
    {
        var ws = _WorldSize;
        var path = new Line2D
        {
            Width = 58.0f,
            DefaultColor = new Color(0.16f, 0.12f, 0.08f, 0.28f),
            JointMode = Line2D.LineJointMode.Round,
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round,
            ZIndex = -3
        };

        path.AddPoint(new Vector2(ws.X * 0.08f, ws.Y * 0.74f));
        path.AddPoint(new Vector2(ws.X * 0.28f, ws.Y * 0.60f));
        path.AddPoint(new Vector2(ws.X * 0.48f, ws.Y * 0.62f));
        path.AddPoint(new Vector2(ws.X * 0.70f, ws.Y * 0.44f));
        path.AddPoint(new Vector2(ws.X * 0.92f, ws.Y * 0.38f));
        _backgroundRect!.AddChild(path);

        var edge = new Line2D
        {
            Width = 3.0f,
            DefaultColor = new Color(0.72f, 0.38f, 0.15f, 0.20f),
            JointMode = Line2D.LineJointMode.Round,
            ZIndex = -2
        };
        foreach (var point in path.Points)
            edge.AddPoint(point);
        _backgroundRect.AddChild(edge);
    }

    private void _CreateRuneCircles()
    {
        var ws = _WorldSize;
        Vector2[] centers =
        {
            new(ws.X * 0.28f, ws.Y * 0.34f),
            new(ws.X * 0.58f, ws.Y * 0.70f),
            new(ws.X * 0.80f, ws.Y * 0.22f),
        };

        for (int i = 0; i < centers.Length; i++)
        {
            var rune = new Node2D
            {
                Position = centers[i],
                Rotation = (float)GD.RandRange(0.0f, Mathf.Tau),
                ZIndex = -1,
                Modulate = new Color(1.0f, 0.72f, 0.32f, 0.30f)
            };
            _backgroundRect!.AddChild(rune);

            float radius = 54.0f + i * 16.0f;
            for (int ring = 0; ring < 2; ring++)
            {
                var circle = new Line2D
                {
                    Width = 1.4f,
                    DefaultColor = new Color(0.95f, 0.62f, 0.24f, 0.34f),
                    Closed = true
                };
                int points = 32;
                float r = radius + ring * 18.0f;
                for (int p = 0; p < points; p++)
                {
                    float angle = p * Mathf.Tau / points;
                    circle.AddPoint(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r);
                }
                rune.AddChild(circle);
            }

            var spokes = new Line2D
            {
                Width = 1.0f,
                DefaultColor = new Color(0.95f, 0.62f, 0.24f, 0.20f)
            };
            for (int s = 0; s < 6; s++)
            {
                float angle = s * Mathf.Tau / 6.0f;
                spokes.AddPoint(Vector2.Zero);
                spokes.AddPoint(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius + 24.0f));
            }
            rune.AddChild(spokes);

            var pulse = CreateTween().SetLoops(999999);
            pulse.TweenProperty(rune, "modulate:a", 0.46f, 2.4f + i * 0.5f).SetTrans(Tween.TransitionType.Sine);
            pulse.TweenProperty(rune, "modulate:a", 0.20f, 2.8f + i * 0.4f).SetTrans(Tween.TransitionType.Sine);
        }
    }

    private void _CreateRuinMarkers()
    {
        var ws = _WorldSize;
        for (int i = 0; i < 10; i++)
        {
            var marker = new ColorRect
            {
                Size = new Vector2((float)GD.RandRange(8, 22), (float)GD.RandRange(24, 58)),
                Position = new Vector2((float)GD.RandRange(80, ws.X - 80), (float)GD.RandRange(90, ws.Y - 90)),
                Rotation = (float)GD.RandRange(-0.45f, 0.45f),
                Color = GD.Randf() < 0.45f
                    ? new Color(0.10f, 0.10f, 0.09f, 0.34f)
                    : new Color(0.19f, 0.13f, 0.10f, 0.28f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = -2
            };
            _backgroundRect!.AddChild(marker);
        }
    }

    public void UpdateBackgroundSize()
    {
        if (_backgroundRect != null)
            _backgroundRect.Size = MapContext.CurrentWorldSize;
        if (_groundLayer != null)
            _groundLayer.Size = MapContext.CurrentWorldSize;
        if (_atmosphereLayer != null)
            _atmosphereLayer.Size = MapContext.CurrentWorldSize;
        if (_groundOverlay != null)
            _groundOverlay.Size = MapContext.CurrentWorldSize;
    }

    /// <summary>切换地图时重建全部视觉层（地面/装饰/氛围粒子/灯），避免旧地图主题残留。</summary>
    public async void LoadMap(MapData map)
    {
        _currentMap = map;
        // 释放所有视觉层（_backgroundRect 的子节点：vignette/地面/装饰/粒子/灯）
        if (_backgroundRect != null)
        {
            foreach (var child in _backgroundRect.GetChildren())
                if (child is Node n) n.QueueFree();
        }
        _groundLayer = null;
        _groundOverlay = null;
        _atmosphereLayer = null;
        _fogLayers.Clear();
        // 等一帧让旧节点真正释放，再重建（避免 MoveChild 与待释放节点索引冲突）
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _CreateVignette();
        _BakeGroundTexture();
        _TryLoadGroundOverlay();
        _CreateAtmosphereOverlay();
        UpdateBackgroundSize();
        // 非核心视觉延迟到下一帧
        CallDeferred(nameof(_DeferredVisualSetup));
    }

    public void SetTension(float value)
    {
        Tension = Mathf.Clamp(value, 0.0f, 1.0f);
        if (_atmosphereLayer != null)
            _atmosphereLayer.Modulate = new Color(
                Mathf.Lerp(0.90f, 1.20f, Tension),
                Mathf.Lerp(0.78f, 0.48f, Tension),
                Mathf.Lerp(0.70f, 0.38f, Tension),
                Mathf.Lerp(0.18f, 0.38f, Tension));
        foreach (TextureRect fog in _fogLayers)
        {
            if (IsInstanceValid(fog))
                fog.Modulate = fog.Modulate with { A = Mathf.Lerp(0.7f, 1.5f, Tension) };
        }
    }

    public void SetAtmosphereTint(float r, float g, float b, float fogBrightMult, float fireLightMult)
    {
        if (_backgroundRect != null)
            _backgroundRect.Modulate = new Color(r, g, b, 1.0f);

        foreach (TextureRect fog in _fogLayers)
        {
            if (IsInstanceValid(fog))
                fog.Modulate = fog.Modulate with
                {
                    R = Mathf.Lerp(fog.Modulate.R, r, fogBrightMult),
                    G = Mathf.Lerp(fog.Modulate.G, g, fogBrightMult),
                    B = Mathf.Lerp(fog.Modulate.B, b, fogBrightMult)
                };
        }
    }
}
