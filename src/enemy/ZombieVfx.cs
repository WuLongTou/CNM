namespace ZombieWars.Enemy;

using System.Collections.Generic;
using Godot;
using ZombieWars.Core;
using static ZombieWars.Core.GameEnums;

public partial class ZombieVfx : Node
{
    private static readonly Dictionary<ZombieType, Color> TypeColors = new()
    {
        { ZombieType.Basic,     new Color(0.40f, 0.55f, 0.30f) },
        { ZombieType.Fast,      new Color(0.75f, 0.60f, 0.25f) },
        { ZombieType.Tank,      new Color(0.65f, 0.20f, 0.18f) },
        { ZombieType.Exploder,  new Color(0.70f, 0.25f, 0.60f) },
        { ZombieType.Invisible, new Color(0.35f, 0.55f, 0.65f) },
        { ZombieType.Spitter,   new Color(0.25f, 0.70f, 0.20f) },
        { ZombieType.Healer,    new Color(0.25f, 0.80f, 0.40f) },
        { ZombieType.Summoner,  new Color(0.55f, 0.15f, 0.55f) },
        { ZombieType.Archer,    new Color(0.60f, 0.45f, 0.30f) }, // Tan/brown for archer
    };

    private static readonly Dictionary<ZombieType, int> TypeSizes = new()
    {
        { ZombieType.Basic,     36 },
        { ZombieType.Fast,      28 },
        { ZombieType.Tank,      52 },
        { ZombieType.Exploder,  34 },
        { ZombieType.Invisible, 34 },
        { ZombieType.Spitter,   40 },
        { ZombieType.Healer,    38 },
        { ZombieType.Summoner,  40 },
        { ZombieType.Archer,    36 },
    };

    private static readonly Dictionary<ZombieType, Color> DeathColors = new()
    {
        { ZombieType.Basic,     new Color(0.4f, 0.55f, 0.3f) },
        { ZombieType.Fast,      new Color(0.8f, 0.7f, 0.3f) },
        { ZombieType.Tank,      new Color(0.6f, 0.25f, 0.2f) },
        { ZombieType.Exploder,  new Color(1.0f, 0.5f, 0.1f) },
        { ZombieType.Invisible, new Color(0.4f, 0.7f, 0.8f) },
        { ZombieType.Spitter,   new Color(0.2f, 0.8f, 0.2f) },
        { ZombieType.Healer,    new Color(0.3f, 0.9f, 0.5f) },
        { ZombieType.Summoner,  new Color(0.6f, 0.2f, 0.6f) },
        { ZombieType.Archer,    new Color(0.7f, 0.5f, 0.3f) },
    };

    // Static texture cache to avoid regenerating sprites on every spawn
    private static readonly Dictionary<ZombieType, Texture2D?> _spriteCache = new();
    private static bool _cacheInitialized = false;

    private Zombie _zombie = null!;

    private Sprite2D _sprite = null!;
    private Sprite2D _shadow = null!;
    private Label _healthLabel = null!;
    private Label _attackLabel = null!;
    private Line2D _eliteRing = null!;

    // Animation support - using List<T> for better C# performance
    private readonly List<Texture2D> _walkFrames = new();
    private int _currentFrame = 0;
    private float _animTimer = 0.0f;
    private bool _isMoving = false;
    private bool _facingLeft = false;

    public Sprite2D Sprite => _sprite;

    public void Setup(Zombie z)
    {
        _zombie = z;
    }

    private void _TryLoadPngSprite(ZombieType type)
    {
        string[] paths;
        string baseName;

        if (_zombie.IsBoss && FileAccess.FileExists("res://assets/sprites/zombies/boss_zombie.jpg"))
        {
            paths = new[] { "res://assets/sprites/zombies/boss_zombie.jpg" };
            baseName = "boss_zombie";
        }
        else if (_zombie.IsElite && FileAccess.FileExists("res://assets/sprites/zombies/zombie_elite.jpg"))
        {
            paths = new[] { "res://assets/sprites/zombies/zombie_elite.jpg" };
            baseName = "zombie_elite";
        }
        else
        {
            baseName = _GetZombieAssetName(type);
            paths = new[]
            {
                $"res://assets/sprites/zombies/zombie_{baseName}.jpg",
                $"res://assets/sprites/zombies/{baseName}.jpg",
            };
        }

        // Load walk frames if available
        _LoadWalkFrames(baseName);

        foreach (string p in paths)
        {
            // Use TextureUtils for normalized loading
            var tex = TextureUtils.LoadNormalizedSprite(p);
            if (tex != null)
            {
                _sprite.Texture = tex;
                // If no walk frames loaded, use main sprite as first frame
                if (_walkFrames.Count == 0)
                    _walkFrames.Add(tex);
                return;
            }
        }
    }

    private void _LoadWalkFrames(string baseName)
    {
        // Try to load walk frames in various naming conventions

        // Pattern 1: zombie_walk_1.jpg (generic)
        for (int i = 1; i <= 4; i++)
        {
            string path = $"res://assets/sprites/zombies/zombie_walk_{i}.jpg";
            var tex = TextureUtils.LoadNormalizedSprite(path);
            if (tex != null)
                _walkFrames.Add(tex);
        }

        // Pattern 2: zombie_basic_walk_1.jpg (type-specific naming)
        if (_walkFrames.Count == 0)
        {
            for (int i = 1; i <= 4; i++)
            {
                string path = $"res://assets/sprites/zombies/{baseName}_walk_{i}.jpg";
                var tex = TextureUtils.LoadNormalizedSprite(path);
                if (tex != null)
                    _walkFrames.Add(tex);
            }
        }

        // Pattern 3: zombie_frame_1.jpg (frame-based)
        if (_walkFrames.Count == 0)
        {
            for (int i = 1; i <= 4; i++)
            {
                string path = $"res://assets/sprites/zombies/{baseName}_frame_{i}.jpg";
                var tex = TextureUtils.LoadNormalizedSprite(path);
                if (tex != null)
                    _walkFrames.Add(tex);
            }
        }
    }

    private string _GetZombieAssetName(ZombieType type) => type switch
    {
        ZombieType.Basic => "zombie_basic",
        ZombieType.Fast => "zombie_fast",
        ZombieType.Tank => "zombie_tank",
        ZombieType.Exploder => "zombie_exploder",
        ZombieType.Invisible => "zombie_invisible",
        ZombieType.Spitter => "zombie_spitter",
        ZombieType.Healer => "zombie_healer",
        ZombieType.Summoner => "zombie_summoner",
        _ => "zombie_basic"
    };

    public void CreateVisuals()
    {
        _CreateShadow();
        _sprite = new Sprite2D();
        _zombie.AddChild(_sprite);

        // Priority: PNG assets > cached generated sprites > generate on the fly
        _TryLoadPngSprite(_zombie.ZombieType);
        if (_sprite.Texture == null)
        {
            _InitTextureCache();
            if (_spriteCache.TryGetValue(_zombie.ZombieType, out var cached) && cached != null)
            {
                _sprite.Texture = cached;
                _sprite.Scale = new Godot.Vector2(1.0f, 1.0f);
            }
            else
            {
                _GenerateAndCacheSprite(_zombie.ZombieType);
            }
        }

        _CreateStatusLabels();

        if (_zombie.ZombieType == ZombieType.Invisible)
        {
            _sprite.Modulate = _sprite.Modulate with { A = 0.3f };
        }

        if (_zombie.IsBoss)
        {
            // Boss needs bigger scale since it's a larger enemy
            _zombie.Scale = new Godot.Vector2(1.8f, 1.8f);
        }
        else if (_zombie.IsElite)
        {
            _zombie.Scale = GameConfig.ZombieEliteScale;
        }
    }

    private void _CreateShadow()
    {
        _shadow = new Sprite2D();
        int w = (int)GameConfig.ZombieShadowSize.X;
        int h = (int)GameConfig.ZombieShadowSize.Y;
        _shadow.Texture = _MakeEllipseTexture(w, h, GameConfig.ZombieShadowColor);
        _shadow.Position = GameConfig.ZombieShadowOffset;
        _zombie.AddChild(_shadow);
    }

    private void _InitTextureCache()
    {
        if (_cacheInitialized) return;
        _cacheInitialized = true;

        // Pre-generate all zombie type sprites
        foreach (ZombieType type in System.Enum.GetValues(typeof(ZombieType)))
        {
            if (!_spriteCache.ContainsKey(type))
            {
                _GenerateAndCacheSprite(type);
            }
        }
    }

    private void _GenerateAndCacheSprite(ZombieType type)
    {
        var baseColor = TypeColors.TryGetValue(type, out var c) ? c : TypeColors[ZombieType.Basic];
        var size = TypeSizes.TryGetValue(type, out var s) ? s : TypeSizes[ZombieType.Basic];

        var image = Image.Create(size, size, false, Image.Format.Rgba8);
        float cx = size / 2.0f;
        float cy = size / 2.0f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var color = _ComputePixelForType(x, y, cx, cy, size, baseColor, type);
                color = _ApplyTypeOverlayForType(x, y, cx, cy, size, baseColor, color, type);
                image.SetPixel(x, y, color);
            }
        }

        var texture = ImageTexture.CreateFromImage(image);
        _spriteCache[type] = texture;

        // If this is for our current zombie, apply it
        if (_zombie != null && _zombie.ZombieType == type && _sprite != null)
        {
            _sprite.Texture = texture;
            _StartIdleAnimation();
            if (_zombie.IsElite)
            {
                _CreateEliteRing(size * 0.7f, baseColor.Lightened(0.4f));
                _sprite.Scale = GameConfig.ZombieEliteScale;
            }
        }
    }

    private Color _ComputePixelForType(int x, int y, float cx, float cy, int size, Color baseColor, ZombieType type)
    {
        // Simplified sprite generation for cache
        Color color = Color.Color8(0, 0, 0, 0);
        float headR = size * 0.22f;
        float headCy = cy * 0.52f;

        float hd = new Vector2(x - cx, y - headCy).Length();
        if (hd <= headR)
        {
            color = baseColor;
            if (hd > headR * 0.65 && ((x + y) % 3 == 0))
            {
                color = baseColor.Darkened(0.35f);
            }

            var le = new Vector2(cx - size * 0.09f, headCy);
            var re = new Vector2(cx + size * 0.09f, headCy);
            if (new Vector2(x - le.X, y - le.Y).Length() <= size * 0.045f)
            {
                color = new Color(1.0f, 0.15f, 0.10f);
            }
            else if (new Vector2(x - re.X, y - re.Y).Length() <= size * 0.045f)
            {
                color = new Color(1.0f, 0.15f, 0.10f);
            }

            if (y >= headCy + headR * 0.4 && y <= headCy + headR * 0.7)
            {
                if (Mathf.Abs(x - cx) <= headR * 0.5)
                {
                    color = new Color(0.08f, 0.0f, 0.0f);
                }
            }
        }
        else if (y >= cy * 0.78f && y <= cy * 1.4f)
        {
            float bw = size * 0.20f - Mathf.Abs(y - cy * 1.1f) * 0.03f;
            if (Mathf.Abs(x - cx) <= bw)
            {
                color = baseColor.Darkened(0.15f);
                if (y % 4 == 0 && Mathf.Abs(x - cx) > bw * 0.5)
                {
                    color = new Color(0.20f, 0.18f, 0.15f);
                }
                if (y >= cy * 0.9f && y <= cy * 1.15f && y % 3 == 0 && Mathf.Abs(x - cx) <= bw * 0.6)
                {
                    color = baseColor.Darkened(0.4f);
                }
            }
        }
        else if (y >= cy * 0.85f && y <= cy * 1.5f)
        {
            float la = cx - size * 0.34f + (float)Mathf.Sin(y * 0.25f) * 2;
            float ra = cx + size * 0.32f + (float)Mathf.Sin(y * 0.25f + Mathf.Pi) * 2;
            if (Mathf.Abs(x - la) <= 4 || Mathf.Abs(x - ra) <= 4)
            {
                if (y > cy * 1.15f)
                {
                    color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
                }
                else
                {
                    color = baseColor.Darkened(0.25f);
                }
            }
        }

        return color;
    }

    private Color _ApplyTypeOverlayForType(int x, int y, float cx, float cy, int size, Color baseColor, Color color, ZombieType type)
    {
        switch (type)
        {
            case ZombieType.Tank:
                if (color.A > 0)
                {
                    if (y < size * 0.15f || y > size * 0.88f)
                    {
                        color = new Color(0.35f, 0.30f, 0.28f);
                    }
                    if (x < size * 0.12f || x > size * 0.88f)
                    {
                        color = new Color(0.35f, 0.30f, 0.28f);
                    }
                }
                if ((x < size * 0.16f || x > size * 0.84f) && y % 7 == 3)
                {
                    if (color.A > 0)
                    {
                        color = new Color(0.50f, 0.48f, 0.45f);
                    }
                }
                break;

            case ZombieType.Fast:
                if (color.A > 0)
                {
                    color = color.Lightened(0.15f);
                }
                break;

            case ZombieType.Archer:
                // Crossbow indicator on back
                if (y >= cy * 0.7f && y <= cy * 1.3f && Mathf.Abs(x - cx) <= 2)
                {
                    color = new Color(0.5f, 0.35f, 0.15f);
                }
                break;

            case ZombieType.Spitter:
                // Green acid mouth
                if (y >= cy * 1.0f && y <= cy * 1.2f && Mathf.Abs(x - cx) <= size * 0.15f)
                {
                    color = new Color(0.2f, 0.9f, 0.2f);
                }
                break;
        }

        return color;
    }

    private void _DrawZombieSprite()
    {
        var baseColor = TypeColors.TryGetValue(_zombie.ZombieType, out var c)
            ? c
            : TypeColors[ZombieType.Basic];
        var size = TypeSizes.TryGetValue(_zombie.ZombieType, out var s)
            ? s
            : TypeSizes[ZombieType.Basic];

        var image = Image.Create(size, size, false, Image.Format.Rgba8);
        float cx = size / 2.0f;
        float cy = size / 2.0f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var color = _ComputePixel(x, y, cx, cy, size, baseColor);
                color = _ApplyTypeOverlay(x, y, cx, cy, size, baseColor, color);
                image.SetPixel(x, y, color);
            }
        }

        var texture = ImageTexture.CreateFromImage(image);
        _sprite.Texture = texture;

        _StartIdleAnimation();

        if (_zombie.IsElite)
        {
            _CreateEliteRing(size * 0.7f, baseColor.Lightened(0.4f));
            _sprite.Scale = GameConfig.ZombieEliteScale;
        }
    }

    private Color _ComputePixel(int x, int y, float cx, float cy, int size, Color baseColor)
    {
        Color color = Color.Color8(0, 0, 0, 0);
        float headR = size * 0.22f;
        float headCy = cy * 0.52f;

        float hd = new Vector2(x - cx, y - headCy).Length();
        if (hd <= headR)
        {
            color = baseColor;
            if (hd > headR * 0.65 && ((x + y) % 3 == 0))
            {
                color = baseColor.Darkened(0.35f);
            }

            var le = new Vector2(cx - size * 0.09f, headCy);
            var re = new Vector2(cx + size * 0.09f, headCy);
            if (new Vector2(x - le.X, y - le.Y).Length() <= size * 0.045f)
            {
                color = new Color(1.0f, 0.15f, 0.10f);
            }
            else if (new Vector2(x - re.X, y - re.Y).Length() <= size * 0.045f)
            {
                color = new Color(1.0f, 0.15f, 0.10f);
            }

            if (y >= headCy + headR * 0.4 && y <= headCy + headR * 0.7)
            {
                if (Mathf.Abs(x - cx) <= headR * 0.5)
                {
                    color = new Color(0.08f, 0.0f, 0.0f);
                }
            }
        }
        else if (y >= cy * 0.78f && y <= cy * 1.4f)
        {
            float bw = size * 0.20f - Mathf.Abs(y - cy * 1.1f) * 0.03f;
            if (Mathf.Abs(x - cx) <= bw)
            {
                color = baseColor.Darkened(0.15f);
                if (y % 4 == 0 && Mathf.Abs(x - cx) > bw * 0.5)
                {
                    color = new Color(0.20f, 0.18f, 0.15f);
                }
                if (y >= cy * 0.9f && y <= cy * 1.15f && y % 3 == 0 && Mathf.Abs(x - cx) <= bw * 0.6)
                {
                    color = baseColor.Darkened(0.4f);
                }
            }
        }
        else if (y >= cy * 0.85f && y <= cy * 1.5f)
        {
            float la = cx - size * 0.34f + (float)Mathf.Sin(y * 0.25f) * 2;
            float ra = cx + size * 0.32f + (float)Mathf.Sin(y * 0.25f + Mathf.Pi) * 2;
            if (Mathf.Abs(x - la) <= size * 0.055f)
            {
                color = baseColor.Darkened(0.2f);
                if (y % 5 == 0)
                {
                    color = baseColor.Darkened(0.45f);
                }
            }
            else if (Mathf.Abs(x - ra) <= size * 0.055f)
            {
                color = baseColor.Darkened(0.2f);
                if (y % 5 == 0)
                {
                    color = baseColor.Darkened(0.45f);
                }
            }
        }
        else if (y >= cy * 1.4f)
        {
            float llx = cx - size * 0.11f + (y - cy * 1.4f) * 0.07f;
            float rlx = cx + size * 0.10f - (y - cy * 1.4f) * 0.05f;
            if (Mathf.Abs(x - llx) <= size * 0.07f)
            {
                color = baseColor.Darkened(0.30f);
            }
            else if (Mathf.Abs(x - rlx) <= size * 0.07f)
            {
                color = baseColor.Darkened(0.30f);
            }
        }

        return color;
    }

    private Color _ApplyTypeOverlay(int x, int y, float cx, float cy, int size, Color baseColor, Color color)
    {
        switch (_zombie.ZombieType)
        {
            case ZombieType.Tank:
                if (color.A > 0)
                {
                    if (y < size * 0.15f || y > size * 0.88f)
                    {
                        color = new Color(0.35f, 0.30f, 0.28f);
                    }
                    if (x < size * 0.12f || x > size * 0.88f)
                    {
                        color = new Color(0.35f, 0.30f, 0.28f);
                    }
                }
                if ((x < size * 0.16f || x > size * 0.84f) && y % 7 == 3)
                {
                    if (color.A > 0)
                    {
                        color = new Color(0.50f, 0.48f, 0.45f);
                    }
                }
                if (y >= cy * 0.6f && y <= cy * 0.8f)
                {
                    if (Mathf.Abs(x - (cx - size * 0.3f)) <= 4 || Mathf.Abs(x - (cx + size * 0.3f)) <= 4)
                    {
                        color = new Color(0.40f, 0.35f, 0.30f);
                    }
                }
                break;

            case ZombieType.Fast:
                if (color.A > 0)
                {
                    color = color.Lightened(0.15f);
                }
                if ((x <= 2 || x >= size - 3) && y % 2 == 0 && y > size * 0.3f && y < size * 0.7f)
                {
                    color = new Color(0.9f, 0.8f, 0.3f, 0.5f);
                }
                break;

            case ZombieType.Exploder:
                float coreD = new Vector2(x - cx, y - cy * 1.15f).Length();
                if (coreD <= size * 0.10f)
                {
                    color = new Color(1.0f, 0.6f, 0.1f, 0.95f);
                }
                else if (coreD <= size * 0.15f && coreD > size * 0.10f)
                {
                    if (color.A > 0)
                    {
                        color = baseColor.Lightened(0.4f);
                    }
                }
                break;

            case ZombieType.Invisible:
                if (color.A > 0)
                {
                    color = color with { A = 0.55f };
                }
                float outlineD = new Vector2(x - cx, y - cy).Length();
                if (outlineD >= size * 0.38f && outlineD <= size * 0.42f)
                {
                    color = new Color(0.4f, 0.8f, 0.9f, 0.25f);
                }
                break;

            case ZombieType.Spitter:
                float cheekL = new Vector2(x - (cx - size * 0.15f), y - (cy * 0.55f)).Length();
                float cheekR = new Vector2(x - (cx + size * 0.15f), y - (cy * 0.55f)).Length();
                if (cheekL <= size * 0.08f || cheekR <= size * 0.08f)
                {
                    color = new Color(0.15f, 0.85f, 0.15f, 0.9f);
                }
                if (Mathf.Abs(x - cx) <= 1 && y >= cy * 0.7f && y <= cy * 0.85f)
                {
                    color = new Color(0.10f, 0.75f, 0.10f, 0.7f);
                }
                break;

            case ZombieType.Healer:
                if (y >= cy * 0.85f && y <= cy * 1.15f)
                {
                    if (Mathf.Abs(x - cx) <= 2)
                    {
                        color = Colors.White;
                    }
                }
                if (Mathf.Abs(y - cy) <= 2 && y >= cy * 0.9f && y <= cy * 1.1f)
                {
                    if (x >= cx - size * 0.1f && x <= cx + size * 0.1f)
                    {
                        color = Colors.White;
                    }
                }
                float hd2 = new Vector2(x - cx, y - cy).Length();
                if (hd2 >= size * 0.42f && hd2 <= size * 0.45f)
                {
                    if ((x + y) % 4 == 0)
                    {
                        color = new Color(0.3f, 1.0f, 0.5f, 0.4f);
                    }
                }
                break;
        }

        return color;
    }

    private void _StartIdleAnimation()
    {
        var tw = _zombie.CreateTween();
        tw.SetLoops();

        switch (_zombie.ZombieType)
        {
            case ZombieType.Exploder:
        tw.TweenProperty(_sprite, "scale", new Vector2(1.1f, 1.1f), 1.5f).SetTrans(Tween.TransitionType.Sine);
        tw.TweenProperty(_sprite, "scale", new Vector2(0.9f, 0.9f), 1.5f).SetTrans(Tween.TransitionType.Sine);
                break;

            case ZombieType.Fast:
        tw.TweenProperty(_sprite, "position:x", 2.0f, 0.1f);
        tw.TweenProperty(_sprite, "position:x", -2.0f, 0.1f);
        tw.TweenProperty(_sprite, "position:x", 0.0f, 0.1f);
                break;

            case ZombieType.Tank:
        tw.TweenProperty(_sprite, "position:y", 2.0f, 2.0f).SetTrans(Tween.TransitionType.Sine);
        tw.TweenProperty(_sprite, "position:y", 0.0f, 2.0f).SetTrans(Tween.TransitionType.Sine);
                break;

            case ZombieType.Healer:
        tw.TweenProperty(_sprite, "rotation", Mathf.Pi * 2, 4.0f);
        break;

        default:
            tw.TweenProperty(_sprite, "rotation", Mathf.DegToRad(5), 3.0f).SetTrans(Tween.TransitionType.Sine);
            tw.TweenProperty(_sprite, "rotation", Mathf.DegToRad(-5), 3.0f).SetTrans(Tween.TransitionType.Sine);
            break;
        }
    }

    /// <summary>
    /// Update walk animation. Call this from Zombie._PhysicsProcess.
    /// Uses smooth frame cycling for fluid animation.
    /// </summary>
    public void UpdateWalkAnimation(float delta)
    {
        if (_sprite == null)
            return;

        // If no walk frames loaded, use the sprite texture for idle bobbing
        if (_walkFrames.Count <= 1)
        {
            // Subtle idle animation even when standing still
            _animTimer += delta;
            return;
        }

        _isMoving = _zombie.Velocity.Length() > 5;

        if (_isMoving)
        {
            _animTimer += delta;
            float frameInterval = GameConfig.WalkAnimSpeed;
            if (_animTimer >= frameInterval)
            {
                _animTimer -= frameInterval;
                _currentFrame = (_currentFrame + 1) % _walkFrames.Count;
                _sprite.Texture = _walkFrames[_currentFrame];
            }
        }
        else
        {
            // Reset to first frame when stopped
            if (_currentFrame != 0)
            {
                _currentFrame = 0;
                _sprite.Texture = _walkFrames[0];
            }
            _animTimer = 0;
        }

        // Handle facing direction
        float velX = _zombie.Velocity.X;
        if (velX < -5)
        {
            _facingLeft = true;
            _sprite.FlipH = true;
        }
        else if (velX > 5)
        {
            _facingLeft = false;
            _sprite.FlipH = false;
        }
    }

    private void _CreateEliteRing(float radius, Color color)
    {
        _eliteRing = new Line2D();
        int pts = 24;
        for (int i = 0; i <= pts; i++)
        {
            float angle = i * 2.0f * Mathf.Pi / pts;
            _eliteRing.AddPoint(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
        _eliteRing.Width = 2.5f;
        _eliteRing.DefaultColor = GameConfig.ZombieEliteRingColor;
        _eliteRing.ZIndex = -1;
        _zombie.AddChild(_eliteRing);

        var tw = _zombie.CreateTween();
        tw.SetLoops();
        tw.TweenProperty(_eliteRing, "rotation", Mathf.Pi * 2, 3.0f);
    }

    private void _CreateStatusLabels()
    {
        _healthLabel = new Label();
        _healthLabel.AddThemeFontSizeOverride("font_size", GameConfig.ZombieStatusFontSize);
        _healthLabel.AddThemeColorOverride("font_color", Colors.Red);
        _healthLabel.Position = GameConfig.ZombieStatusHpOffset;
        _healthLabel.ZIndex = 10;
        _zombie.AddChild(_healthLabel);

        _attackLabel = new Label();
        _attackLabel.AddThemeFontSizeOverride("font_size", GameConfig.ZombieStatusFontSize);
        _attackLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.5f, 0.0f));
        _attackLabel.Position = GameConfig.ZombieStatusAtkOffset;
        _attackLabel.ZIndex = 10;
        _zombie.AddChild(_attackLabel);

        UpdateStatusLabels();
    }

    public void UpdateStatusLabels()
    {
        if (_healthLabel != null)
        {
            _healthLabel.Text = $"❤️{_zombie.CurrentHealth}/{_zombie.MaxHealth}";
        }
        if (_attackLabel != null)
        {
            // Add ranged indicator for archers
            string damageText = $"⚔️{_zombie.Damage}";
            if (_zombie.ZombieType == ZombieType.Archer)
                damageText += " 🏹";
            _attackLabel.Text = damageText;
        }
    }

    public void FlashHit()
    {
        if (_sprite == null) return;

        var tw = _zombie.CreateTween();
        tw.TweenProperty(_sprite, "modulate", new Color(2.0f, 2.0f, 2.0f), GameConfig.ZombieHitFlashIn);
        tw.TweenProperty(_sprite, "modulate", Colors.White, GameConfig.ZombieHitFlashOut);

        float kbDist = GameConfig.ZombieKnockbackDist;
        var kb = new Vector2((float)GD.RandRange(-1, 1), (float)GD.RandRange(-1, 1)).Normalized() * kbDist;
        var posTw = _zombie.CreateTween();
        posTw.TweenProperty(_zombie, "position", _zombie.Position + kb, GameConfig.ZombieHitFlashIn);
        posTw.TweenProperty(_zombie, "position", _zombie.Position, GameConfig.ZombieHitFlashOut);
    }

    public void ShowDamagePopup(int amount)
    {
        var popup = new Label();
        popup.Text = amount.ToString();
        popup.GlobalPosition = _zombie.GlobalPosition + new Vector2((float)GD.RandRange(-10, 10), -25);
        popup.AddThemeFontSizeOverride("font_size", GameConfig.ZombieDamagePopupSize);
        popup.AddThemeColorOverride("font_color", Colors.White);
        popup.AddThemeConstantOverride("outline_size", 2);
        popup.AddThemeColorOverride("font_outline_color", Colors.Black);
        popup.ZIndex = 15;
        _zombie.GetParent().AddChild(popup);

        var tw = popup.CreateTween();
        tw.Parallel().TweenProperty(popup, "position:y", popup.Position.Y - 30, 0.5f);
        tw.Parallel().TweenProperty(popup, "modulate:a", 0.0f, 0.5f).SetDelay(0.2f);
        tw.TweenCallback(Callable.From(popup.QueueFree));
    }

    public void ShowFrozen()
    {
        if (_sprite != null) _sprite.Modulate = Colors.Cyan;
    }

    public void ShowStunned()
    {
        if (_sprite != null) _sprite.Modulate = new Color(0.8f, 0.8f, 1.0f);
    }

    public void ShowPoisoned()
    {
        if (_sprite != null) _sprite.Modulate = Colors.Purple;
    }

    public void ShowCharmed()
    {
        if (_sprite != null) _sprite.Modulate = Colors.Magenta;
    }

    public void ShowInvisible(bool revealed)
    {
        if (_sprite != null) _sprite.Modulate = _sprite.Modulate with { A = revealed ? 1.0f : 0.3f };
    }

    public void ClearEffect()
    {
        if (_sprite != null) _sprite.Modulate = Colors.White;
    }

    public void SpawnDeathEffects()
    {
        _SpawnCorpse();
        _SpawnDeathParticles();
        _SpawnBloodStain();
    }

    private void _SpawnCorpse()
    {
        if (_sprite == null || _sprite.Texture == null) return;

        var corpse = new Sprite2D();
        corpse.Texture = _sprite.Texture;
        corpse.GlobalPosition = _zombie.GlobalPosition;
        corpse.Modulate = new Color(0.6f, 0.4f, 0.3f, 0.8f);
        corpse.ZIndex = -1;
        _zombie.GetParent().AddChild(corpse);

        var tw = corpse.CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(corpse, "scale", new Vector2(0.1f, 0.1f), GameConfig.ZombieCorpseFade)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
        tw.TweenProperty(corpse, "rotation", (float)GD.RandRange(-Mathf.Pi, Mathf.Pi), GameConfig.ZombieCorpseFade);
        tw.TweenProperty(corpse, "modulate:a", 0.0f, GameConfig.ZombieCorpseFade);
        tw.SetParallel(false);
        tw.TweenCallback(Callable.From(corpse.QueueFree));
    }

    private void _SpawnDeathParticles()
    {
        Color deathColor = DeathColors.TryGetValue(_zombie.ZombieType, out var dc) ? dc : new Color(0.4f, 0.55f, 0.3f);
        int particleCount = GameConfig.ZombieDeathParticlesDefault;
        if (_zombie.ZombieType == ZombieType.Tank) particleCount = GameConfig.ZombieDeathParticlesTank;
        else if (_zombie.ZombieType == ZombieType.Exploder) particleCount = GameConfig.ZombieDeathParticlesExploder;

        var particles = new GpuParticles2D();
        particles.GlobalPosition = _zombie.GlobalPosition;
        particles.Emitting = true;
        particles.Amount = particleCount;
        particles.Lifetime = 0.7f;
        particles.OneShot = true;

        var mat = new ParticleProcessMaterial();
        mat.Direction = new Vector3(0, -1, 0);
        mat.InitialVelocityMin = 40.0f;
        mat.InitialVelocityMax = 140.0f;
        mat.Gravity = new Vector3(0, 180, 0);
        mat.ScaleMin = 1.5f;
        mat.ScaleMax = 4.0f;
        mat.Color = deathColor;
        particles.ProcessMaterial = mat;
        _zombie.GetParent().AddChild(particles);

        if (_zombie.ZombieType == ZombieType.Exploder)
        {
            _SpawnExplosionRing();
        }

        var timer = new Timer();
        timer.WaitTime = 1.2f;
        timer.OneShot = true;
        timer.Timeout += () => particles.QueueFree();
        particles.AddChild(timer);
        timer.Start();
    }

    private void _SpawnExplosionRing()
    {
        var ring = new Line2D();
        int pts = 24;
        for (int i = 0; i <= pts; i++)
        {
            float a = i * 2.0f * Mathf.Pi / pts;
            ring.AddPoint(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 15.0f);
        }
        ring.Width = 3.0f;
        ring.DefaultColor = new Color(1.0f, 0.5f, 0.1f, 0.9f);

        var ringNode = new Node2D();
        ringNode.GlobalPosition = _zombie.GlobalPosition;
        ringNode.AddChild(ring);
        _zombie.GetParent().AddChild(ringNode);

        var tw = ringNode.CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(ringNode, "scale", new Vector2(6, 6), 0.4f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(ringNode, "modulate:a", 0.0f, 0.4f);
        tw.SetParallel(false);
        tw.TweenCallback(Callable.From(ringNode.QueueFree));
    }

    private void _SpawnBloodStain()
    {
        var blood = new ColorRect();
        blood.Size = new Vector2((float)GD.RandRange(12, 24), (float)GD.RandRange(6, 14));
        blood.GlobalPosition = _zombie.GlobalPosition - blood.Size * 0.5f;
        blood.Color = new Color(0.35f, 0.06f, 0.04f, 0.4f);
        blood.Rotation = (float)GD.RandRange(-0.5f, 0.5f);
        blood.ZIndex = -2;
        _zombie.GetParent().AddChild(blood);

        var tw = blood.CreateTween();
        tw.TweenProperty(blood, "modulate:a", 0.0f, GameConfig.ZombieBloodFade);
        tw.TweenCallback(Callable.From(blood.QueueFree));
    }

    public void ShowHealEffect(Vector2 targetPos, int amount)
    {
        var healLabel = new Label();
        healLabel.Text = $"+{amount}";
        healLabel.GlobalPosition = targetPos + new Vector2((float)GD.RandRange(-10, 10), -20);
        healLabel.AddThemeFontSizeOverride("font_size", 12);
        healLabel.AddThemeColorOverride("font_color", new Color(0.3f, 1.0f, 0.5f));
        healLabel.AddThemeConstantOverride("outline_size", 2);
        healLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        healLabel.ZIndex = 15;
        _zombie.GetParent().AddChild(healLabel);

        var tw = healLabel.CreateTween();
        tw.Parallel().TweenProperty(healLabel, "position:y", healLabel.Position.Y - 25, 0.5f);
        tw.Parallel().TweenProperty(healLabel, "modulate:a", 0.0f, 0.5f).SetDelay(0.2f);
        tw.TweenCallback(Callable.From(healLabel.QueueFree));
    }

    public void ShowHealPulse(float healRange)
    {
        var ring = new Node2D();
        ring.GlobalPosition = _zombie.GlobalPosition;

        var line = new Line2D();
        int pts = 20;
        for (int i = 0; i <= pts; i++)
        {
            float angle = i * 2.0f * Mathf.Pi / pts;
            line.AddPoint(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 20.0f);
        }
        line.Width = 2.0f;
        line.DefaultColor = new Color(0.3f, 1.0f, 0.5f, 0.8f);
        ring.AddChild(line);
        _zombie.GetParent().AddChild(ring);

        var tw = ring.CreateTween();
        tw.Parallel().TweenProperty(ring, "scale", new Vector2(healRange / 20.0f, healRange / 20.0f), 0.4f);
        tw.Parallel().TweenProperty(ring, "modulate:a", 0.0f, 0.4f);
        tw.TweenCallback(Callable.From(ring.QueueFree));
    }

    private Texture2D _MakeEllipseTexture(int width, int height, Color color)
    {
        var image = Image.Create(width, height, false, Image.Format.Rgba8);
        float cx = width / 2.0f;
        float cy = height / 2.0f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = (x - cx) / (width * 0.5f);
                float dy = (y - cy) / (height * 0.5f);
                if (dx * dx + dy * dy <= 1.0f)
                {
                    image.SetPixel(x, y, color);
                }
            }
        }

        return ImageTexture.CreateFromImage(image);
    }
}
