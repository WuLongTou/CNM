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
    private ColorRect? _backgroundRect;
    private readonly Godot.Collections.Array<TextureRect> _fogLayers = new();

    public float Tension { get; set; } = 0.0f;

    public override void _Ready()
    {
        ZIndex = -10;
        _CreateBaseBackground();
        if (!_TryLoadGroundPng())
        {
            _CreateGroundTexture();
        }
        _CreateFogPatches();
        _CreateFireLights();
        _CreateAshParticles();
        _CreateEmberParticles();
        _CreateGroundDecals();
        SetProcess(false);
    }

    private Vector2 _WorldSize => GameConfig.WorldSize;

    private bool _TryLoadGroundPng()
    {
        string[] paths = {
            "res://assets/sprites/backgrounds/ground_stone.jpg",
            "res://assets/sprites/backgrounds/ground_graveyard.jpg",
            "res://assets/sprites/backgrounds/ground_dungeon.jpg",
            "res://assets/sprites/backgrounds/ground_cemetery.jpg",
            "res://assets/sprites/backgrounds/ground_ruins.jpg"
        };

        foreach (string p in paths)
        {
            if (!FileAccess.FileExists(p)) continue;
            var tex = GD.Load<Texture2D>(p);
            if (tex == null) continue;

            var rect = new TextureRect
            {
                Texture = tex,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                Size = _WorldSize,
                ZIndex = -5,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            AddChild(rect);
            return true;
        }
        return false;
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
        _backgroundRect.AddChild(vignette);
    }

    private void _CreateGroundTexture()
    {
        int w = 640;
        int h = 360;
        using var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float noiseVal = _SimpleHash(x, y);
                float baseGrey = 0.11f + noiseVal * 0.04f;

                int blockX = x / 64;
                int blockY = y / 64;
                float blockShift = _SimpleHash(blockX * 7 + 3, blockY * 13 + 7) * 0.025f - 0.012f;
                baseGrey += blockShift;

                float r = baseGrey + noiseVal * 0.008f;
                float g = baseGrey + noiseVal * 0.005f;
                float b = baseGrey - noiseVal * 0.006f;
                img.SetPixel(x, y, new Color(r, g, b, 1.0f));
            }
        }

        int tileW = 80;
        int tileH = 60;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int yMod = y % tileH;
                if (yMod <= 1)
                {
                    Color c = img.GetPixel(x, y);
                    img.SetPixel(x, y, c.Darkened(0.25f + _SimpleHash(x, y * 3) * 0.15f));
                }

                int row = y / tileH;
                int ofs = (row % 2) * (tileW / 2);
                int xMod = (x + ofs) % tileW;
                if (xMod <= 1)
                {
                    Color c = img.GetPixel(x, y);
                    img.SetPixel(x, y, c.Darkened(0.20f + _SimpleHash(x * 5, y) * 0.15f));
                }
            }
        }

        for (int i = 0; i < 12; i++)
        {
            int cx = (int)GD.RandRange(20, w - 20);
            int cy = (int)GD.RandRange(20, h - 20);
            float angle = (float)GD.RandRange(0.0f, Mathf.Tau);
            int length = (int)GD.RandRange(30, 100);
            for (int step = 0; step < length; step++)
            {
                int px = cx + (int)(Mathf.Cos(angle) * step + GD.RandRange(-1.0f, 1.0f));
                int py = cy + (int)(Mathf.Sin(angle) * step + GD.RandRange(-1.0f, 1.0f));
                if (px >= 0 && px < w && py >= 0 && py < h)
                {
                    Color c = img.GetPixel(px, py);
                    img.SetPixel(px, py, c.Darkened(0.35f));
                }
                angle += (float)GD.RandRange(-0.15f, 0.15f);
            }
        }

        for (int i = 0; i < 20; i++)
        {
            int sx = (int)GD.RandRange(10, w - 10);
            int sy = (int)GD.RandRange(10, h - 10);
            int radius = (int)GD.RandRange(8, 25);
            int stainType = (int)GD.RandRange(0, 2);
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= radius)
                    {
                        int px = sx + dx;
                        int py = sy + dy;
                        if (px >= 0 && px < w && py >= 0 && py < h)
                        {
                            float falloff = 1.0f - d / radius;
                            Color c = img.GetPixel(px, py);
                            switch (stainType)
                            {
                                case 0:
                                    c = c.Darkened(0.15f * falloff);
                                    break;
                                case 1:
                                    c = c.Lerp(new Color(0.08f, 0.12f, 0.06f), 0.20f * falloff);
                                    break;
                                case 2:
                                    c = c.Lerp(new Color(0.15f, 0.04f, 0.03f), 0.18f * falloff);
                                    break;
                            }
                            img.SetPixel(px, py, c);
                        }
                    }
                }
            }
        }

        var tex = ImageTexture.CreateFromImage(img);
        var groundSprite = new TextureRect
        {
            Texture = tex,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            Size = _WorldSize,
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _backgroundRect!.AddChild(groundSprite);
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
            new() { { "pos", new Vector2(0.30f, 0.45f) }, { "size", 420 }, { "color", new Color(0.11f, 0.08f, 0.06f, 0.04f) } },
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

            var drift = CreateTween().SetLoops();
            float dx = (float)GD.RandRange(20.0f, 50.0f);
            float dt = (float)GD.RandRange(12.0f, 25.0f);
            drift.TweenProperty(fog, "position:x", fog.Position.X + dx, dt)
                 .SetTrans(Tween.TransitionType.Sine);
            drift.TweenProperty(fog, "position:x", fog.Position.X - dx, dt)
                 .SetTrans(Tween.TransitionType.Sine);

            var alphaTw = CreateTween().SetLoops();
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
            new() { { "pos", new Vector2(0.22f, 0.85f) }, { "color", new Color(0.90f, 0.50f, 0.12f) }, { "alpha", 0.09f }, { "size", 160 } },
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
                    new Color(c.R * 0.6f, c.G * 0.3f, c.B * 0.1f, a * 0.3f),
                    Colors.Transparent
                },
                Offsets = new[] { 0.0f, 0.45f, 1.0f }
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

            var flicker = CreateTween().SetLoops();
            flicker.TweenProperty(light, "modulate:a", (float)GD.RandRange(0.5f, 0.7f), (float)GD.RandRange(0.3f, 0.7f));
            flicker.TweenProperty(light, "modulate:a", 1.0f, (float)GD.RandRange(0.2f, 0.4f));
            flicker.TweenProperty(light, "modulate:a", (float)GD.RandRange(0.6f, 0.85f), (float)GD.RandRange(0.4f, 0.9f));
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
            Direction = new Vector3(0.2f, -1, 0),
            Spread = 30.0f,
            InitialVelocityMin = 3.0f,
            InitialVelocityMax = 12.0f,
            Gravity = new Vector3(0, -2, 0),
            ScaleMin = 0.6f,
            ScaleMax = 1.8f,
            Color = new Color(0.55f, 0.50f, 0.42f, 0.35f)
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
            Spread = 25.0f,
            InitialVelocityMin = 8.0f,
            InitialVelocityMax = 22.0f,
            Gravity = new Vector3(0, -5, 0),
            ScaleMin = 0.8f,
            ScaleMax = 2.5f
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

        for (int i = 0; i < 12; i++)
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

    public void UpdateBackgroundSize()
    {
        if (_backgroundRect != null)
            _backgroundRect.Size = GameConfig.WorldSize;
    }

    public void SetTension(float value)
    {
        Tension = Mathf.Clamp(value, 0.0f, 1.0f);
        foreach (TextureRect fog in _fogLayers)
        {
            if (fog != null && IsInstanceValid(fog))
                fog.Modulate = fog.Modulate with { A = Mathf.Lerp(0.7f, 1.5f, Tension) };
        }
    }

    public void SetAtmosphereTint(float r, float g, float b, float fogBrightMult, float fireLightMult)
    {
        if (_backgroundRect != null)
            _backgroundRect.Modulate = new Color(r, g, b, 1.0f);

        foreach (TextureRect fog in _fogLayers)
        {
            if (fog != null && IsInstanceValid(fog))
                fog.Modulate = fog.Modulate with
                {
                    R = Mathf.Lerp(fog.Modulate.R, r, fogBrightMult),
                    G = Mathf.Lerp(fog.Modulate.G, g, fogBrightMult),
                    B = Mathf.Lerp(fog.Modulate.B, b, fogBrightMult)
                };
        }
    }
}
