namespace ZombieWars.Enemy;

using System.Collections.Generic;
using Godot;
using ZombieWars.Core;
using ZombieWars.Render;
using ZombieWars.UI;
using static ZombieWars.Core.GameEnums;
using static ZombieWars.UI.HandDrawnTheme.Colors;

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
        { ZombieType.Ship,     new Color(0.20f, 0.40f, 0.70f) }, // Deep blue for spaceship
        { ZombieType.Mech,     new Color(0.35f, 0.35f, 0.40f) }, // Gunmetal for mech
        { ZombieType.Knight,   new Color(0.15f, 0.25f, 0.55f) }, // Royal blue for knight
        { ZombieType.Dragon,   new Color(0.50f, 0.15f, 0.60f) }, // Purple for dragon
        { ZombieType.Swarm,    new Color(0.60f, 0.30f, 0.15f) }, // Amber for swarm
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
        { ZombieType.Ship,     44 },
        { ZombieType.Mech,     54 },
        { ZombieType.Knight,   42 },
        { ZombieType.Dragon,   48 },
        { ZombieType.Swarm,    26 },
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
        { ZombieType.Ship,     new Color(0.3f, 0.6f, 0.9f) },
        { ZombieType.Mech,     new Color(0.5f, 0.5f, 0.6f) },
        { ZombieType.Knight,   new Color(0.3f, 0.4f, 0.8f) },
        { ZombieType.Dragon,   new Color(0.7f, 0.3f, 0.8f) },
        { ZombieType.Swarm,    new Color(0.8f, 0.5f, 0.3f) },
    };

    // Static texture cache to avoid regenerating sprites on every spawn
    private static readonly Dictionary<ZombieType, Texture2D?> _spriteCache = new();
    private static bool _cacheInitialized = false;

    private Zombie _zombie = null!;

    private SpriteProxy3D _sprite = null!;
    private Sprite2D? _outlineSprite;
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
    private bool _pngLoaded;

    public Sprite3D Sprite => _sprite;

    public void Setup(Zombie z)
    {
        _zombie = z;
    }

    private void _ApplySketchOutline()
    {
        // Disabled: outline sprite causes rendering artifacts
        // Re-enable if needed with proper ZIndex handling
    }

    private void _SyncOutlineTexture()
    {
        if (_outlineSprite != null)
            _outlineSprite.Texture = _sprite.Texture;
    }

    private void _TryLoadPngSprite(ZombieType type)
    {
        // Priority 1: spritesheet (cute cartoon chibi style)
        if (_TryLoadSpritesheet(type))
            return;

        // Priority 2: individual PNG/JPG files
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
                $"res://assets/sprites/zombies/{baseName}.png",
                $"res://assets/sprites/zombies/{baseName}.jpg",
            };
        }

        _LoadWalkFramesLegacy(baseName);

        foreach (string p in paths)
        {
            var tex = TextureUtils.LoadNormalizedSprite(p);
            if (tex != null)
            {
                _sprite.Texture = tex;
                _pngLoaded = true;
                if (_walkFrames.Count == 0)
                    _walkFrames.Add(tex);
                return;
            }
        }
    }

    private bool _TryLoadSpritesheet(ZombieType type)
    {
        string sheetName = _GetSpritesheetName(type);
        string sheetPath = $"res://assets/sprites/enemies/sheets/{sheetName}_walk.png";

        if (!FileAccess.FileExists(sheetPath))
            return false;

        // 敌人 2.5D 俯视 walk 图为「1行8列」瘦长站立全身像（角色约 1:7+）。
        // 保持比例会缩成细竖条，故用投影分割 + trim 后**强制横向拉伸**到 ~45% 画布宽，
        // 让角色清晰可辨（横向变形优于不可见的细条）。
        var frames = TextureUtils.LoadAndStretchSplitFrames(sheetPath, widthRatio: 0.45f);
        if (frames == null || frames.Count == 0)
            return false;

        // Ensure all sprites face right
        if (TextureUtils.FramesFaceLeft(frames))
        {
            // 复制数组后再翻转，避免直接修改缓存中的 frames
            var flipped = new Godot.Collections.Array<Texture2D>();
            for (int i = 0; i < frames.Count; i++)
                flipped.Add(TextureUtils.FlipHorizontal(frames[i]));
            frames = flipped;
        }

        _sprite.Texture = frames[0];
        _pngLoaded = true;
        _walkFrames.Clear();
        int walkCount = Mathf.Min(8, frames.Count);
        for (int i = 0; i < walkCount; i++)
        {
            int idx = walkCount <= 1
                ? 0
                : (int)System.Math.Round((double)i * (frames.Count - 1) / (walkCount - 1));
            if (idx >= frames.Count) idx = frames.Count - 1;
            _walkFrames.Add(frames[idx]);
        }

        return true;
    }

    private string _GetSpritesheetName(ZombieType type) => type switch
    {
        ZombieType.Basic => "zombie_basic",
        ZombieType.Fast => "zombie_fast",
        ZombieType.Tank => "zombie_tank",
        ZombieType.Exploder => "zombie_exploder",
        ZombieType.Invisible => "zombie_invisible",
        ZombieType.Spitter => "zombie_spitter",
        ZombieType.Healer => "zombie_healer",
        ZombieType.Summoner => "zombie_summoner",
        ZombieType.Archer => "zombie_archer",
        ZombieType.Ship => "ship",
        ZombieType.Mech => "mech",
        ZombieType.Knight => "knight",
        ZombieType.Dragon => "dragon",
        ZombieType.Swarm => "swarm",
        _ => "zombie_basic"
    };

    private void _LoadWalkFramesLegacy(string baseName)
    {
        for (int i = 1; i <= 6; i++)
        {
            var tex = TextureUtils.LoadNormalizedSprite($"res://assets/sprites/zombies/{baseName}_walk_{i}.png");
            if (tex == null)
                tex = TextureUtils.LoadNormalizedSprite($"res://assets/sprites/zombies/{baseName}_walk_{i}.jpg");
            if (tex != null)
                _walkFrames.Add(tex);
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
        ZombieType.Archer => "zombie_archer",
        ZombieType.Ship => "ship_01",
        ZombieType.Mech => "mech_01",
        ZombieType.Knight => "knight_01",
        ZombieType.Dragon => "flyer_01",
        ZombieType.Swarm => "creature_01",
        _ => "zombie_basic"
    };

    public void CreateVisuals()
    {
        _CreateShadow();
        _sprite = new SpriteProxy3D();
        _sprite.BindTo2D(_zombie);
        RenderViewport.Instance?.AddProxy(_sprite);

        // Priority: PNG assets > cached generated sprites > generate on the fly
        _pngLoaded = false;
        _TryLoadPngSprite(_zombie.ZombieType);
        if (_sprite.Texture == null)
        {
            _InitTextureCache();
            if (_spriteCache.TryGetValue(_zombie.ZombieType, out var cached) && cached != null)
            {
                _sprite.Texture = cached;
                _sprite.Scale = Vector3.One;
            }
            else
            {
                _GenerateAndCacheSprite(_zombie.ZombieType);
            }
        }

        // Only use cached walk frames if PNG loading failed or frames are insufficient
        if ((!_pngLoaded || _walkFrames.Count < 4) && _walkFramesCache.TryGetValue(_zombie.ZombieType, out var cachedFrames))
        {
            _walkFrames.Clear();
            _walkFrames.AddRange(cachedFrames);
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

        _ApplySketchOutline();
    }

    private void _CreateShadow()
    {
        _shadow = new Sprite2D();
        int w = (int)GameConfig.ZombieShadowSize.X;
        int h = (int)GameConfig.ZombieShadowSize.Y;
        _shadow.Texture = _MakeEllipseTexture(w, h, GameConfig.ZombieShadowColor);
        _shadow.Position = GameConfig.ZombieShadowOffset;
        _zombie.AddChild(_shadow);

        // 脚底光环（红色径向渐变，群战中快速辨识敌人位置）
        var glow = new Sprite2D
        {
            Texture = _MakeEllipseTexture(40, 40, new Color(0.85f, 0.15f, 0.1f, 0.30f)),
            ZIndex = -2
        };
        _zombie.AddChild(glow);
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

        // Generate idle sprite
        var image = _CreateZombieImage(type, baseColor, size, 0, 0, 0);
        var texture = ImageTexture.CreateFromImage(image);
        _spriteCache[type] = texture;

        // Generate walk frames for smoother animation
        if (!_walkFramesCache.ContainsKey(type))
        {
            var walkFrames = new List<Texture2D>();
            for (int frame = 0; frame < 12; frame++)
            {
                float walkPhase = frame / 12.0f;
                float legSwing = Mathf.Sin(walkPhase * Mathf.Tau) * 3.0f;
                float bodyBob = -Mathf.Abs(Mathf.Sin(walkPhase * Mathf.Tau)) * 1.5f;
                var walkImg = _CreateZombieImage(type, baseColor, size, legSwing, bodyBob, frame);
                walkFrames.Add(ImageTexture.CreateFromImage(walkImg));
            }
            _walkFramesCache[type] = walkFrames;
        }

        // If this is for our current zombie, apply it
        if (_zombie != null && _zombie.ZombieType == type && _sprite != null)
        {
            _sprite.Texture = texture;
            _StartIdleAnimation();
            if (_zombie.IsElite)
            {
                _CreateEliteRing(size * 0.7f, baseColor.Lightened(0.4f));
                _sprite.Scale = new Vector3(GameConfig.ZombieEliteScale.X, GameConfig.ZombieEliteScale.Y, 1f);
            }
        }
    }

    // Cache for walk frames by zombie type
    private static readonly Dictionary<ZombieType, List<Texture2D>> _walkFramesCache = new();

    public static void ResetSpriteCache()
    {
        _spriteCache.Clear();
        _walkFramesCache.Clear();
        _cacheInitialized = false;
        // 同步清除 TextureUtils 的帧/纹理缓存
        TextureUtils.ClearCache();
    }

    private Image _CreateZombieImage(ZombieType type, Color baseColor, int size, float legSwing, float bodyBob, int frame)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float cx = size / 2.0f;
        float cy = size / 2.0f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var color = _ComputePixelForType(x, y, cx, cy, size, baseColor, type, legSwing, bodyBob, frame);
                color = _ApplyTypeOverlayForType(x, y, cx, cy, size, baseColor, color, type);
                image.SetPixel(x, y, color);
            }
        }
        return image;
    }

    private Color _ComputePixelForType(int x, int y, float cx, float cy, int size, Color baseColor, ZombieType type, float legSwing = 0, float bodyBob = 0, int frame = 0)
    {
        // Simplified sprite generation for cache
        Color color = Color.Color8(0, 0, 0, 0);
        float headR = size * 0.22f;
        float headCy = cy * 0.52f + bodyBob;

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
        else if (y >= cy * 0.78f + bodyBob && y <= cy * 1.4f + bodyBob)
        {
            float bw = size * 0.20f - Mathf.Abs(y - cy * 1.1f - bodyBob) * 0.03f;
            if (Mathf.Abs(x - cx) <= bw)
            {
                color = baseColor.Darkened(0.15f);
                if (y % 4 == 0 && Mathf.Abs(x - cx) > bw * 0.5)
                {
                    color = new Color(0.20f, 0.18f, 0.15f);
                }
                if (y >= cy * 0.9f + bodyBob && y <= cy * 1.15f + bodyBob && y % 3 == 0 && Mathf.Abs(x - cx) <= bw * 0.6)
                {
                    color = baseColor.Darkened(0.4f);
                }
            }
        }
        else if (y >= cy * 0.85f + bodyBob && y <= cy * 1.5f + bodyBob)
        {
            // Arms with swing animation
            float armSwing = Mathf.Sin((frame / 12.0f) * Mathf.Tau) * 2.0f;
            float la = cx - size * 0.34f + (float)Mathf.Sin(y * 0.25f) * 2 + armSwing;
            float ra = cx + size * 0.32f + (float)Mathf.Sin(y * 0.25f + Mathf.Pi) * 2 - armSwing;
            if (Mathf.Abs(x - la) <= 4 || Mathf.Abs(x - ra) <= 4)
            {
                if (y > cy * 1.15f + bodyBob)
                {
                    color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
                }
                else
                {
                    color = baseColor.Darkened(0.25f);
                }
            }
        }

        // Animated legs
        if (y >= cy * 1.3f + bodyBob && y <= cy * 1.7f)
        {
            float leftLegX = cx - size * 0.12f + legSwing;
            float rightLegX = cx + size * 0.12f - legSwing;
            if (Mathf.Abs(x - leftLegX) <= 3.5f || Mathf.Abs(x - rightLegX) <= 3.5f)
            {
                color = baseColor.Darkened(0.3f);
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

            case ZombieType.Ship:
                // Engine glow on right side
                if (x > cx && y >= cy * 0.85f && y <= cy * 1.15f && Mathf.Abs(x - cx) <= size * 0.1f)
                {
                    color = new Color(0.2f, 0.5f, 1.0f);
                }
                break;

            case ZombieType.Mech:
                // Heavy armor lines
                if (color.A > 0 && (y % 6 == 0 || x % 6 == 0))
                {
                    color = new Color(0.25f, 0.28f, 0.32f);
                }
                break;

            case ZombieType.Dragon:
                // Wing pattern
                if (color.A > 0 && (x < cx - size * 0.3f || x > cx + size * 0.3f))
                {
                    if (y < cy * 0.75f || y > cy * 1.15f)
                    {
                        color = new Color(0.4f, 0.1f, 0.5f);
                    }
                }
                break;
        }

        return color;
    }


    private void _StartIdleAnimation()
    {
        var tw = _zombie.CreateTween();
        tw.SetLoops(999999);

        switch (_zombie.ZombieType)
        {
            case ZombieType.Exploder:
                // Pulsing scale for danger
                tw.TweenProperty(_sprite, "scale", new Vector2(1.08f, 1.08f), 1.2f).SetTrans(Tween.TransitionType.Sine);
                tw.TweenProperty(_sprite, "scale", new Vector2(0.92f, 0.92f), 1.2f).SetTrans(Tween.TransitionType.Sine);
                break;

            case ZombieType.Fast:
                // Quick jittering
                tw.TweenProperty(_sprite, "position:x", 1.5f, 0.08f);
                tw.TweenProperty(_sprite, "position:x", -1.5f, 0.08f);
                tw.TweenProperty(_sprite, "position:x", 0.0f, 0.08f);
                break;

            case ZombieType.Tank:
                // Slow heavy breathing
                tw.TweenProperty(_sprite, "scale", new Vector2(1.03f, 1.02f), 2.5f).SetTrans(Tween.TransitionType.Sine);
                tw.TweenProperty(_sprite, "scale", new Vector2(1.0f, 1.0f), 2.5f).SetTrans(Tween.TransitionType.Sine);
                break;

            case ZombieType.Healer:
                // Gentle floating
                tw.TweenProperty(_sprite, "position:y", -2.0f, 1.5f).SetTrans(Tween.TransitionType.Sine);
                tw.TweenProperty(_sprite, "position:y", 2.0f, 1.5f).SetTrans(Tween.TransitionType.Sine);
                break;

            case ZombieType.Invisible:
                // Flickering alpha
                tw.TweenProperty(_sprite, "modulate:a", 0.5f, 1.0f).SetTrans(Tween.TransitionType.Sine);
                tw.TweenProperty(_sprite, "modulate:a", 0.3f, 1.0f).SetTrans(Tween.TransitionType.Sine);
                break;

            case ZombieType.Ship:
                // Gentle hover bob
                tw.TweenProperty(_sprite, "position:y", -3.0f, 1.8f).SetTrans(Tween.TransitionType.Sine);
                tw.TweenProperty(_sprite, "position:y", 3.0f, 1.8f).SetTrans(Tween.TransitionType.Sine);
                break;

            case ZombieType.Dragon:
                // Wing flutter
                tw.TweenProperty(_sprite, "scale:x", 1.05f, 0.6f).SetTrans(Tween.TransitionType.Sine);
                tw.TweenProperty(_sprite, "scale:x", 0.95f, 0.6f).SetTrans(Tween.TransitionType.Sine);
                break;

            case ZombieType.Mech:
                // Heavy rumble
                tw.TweenProperty(_sprite, "position:y", 1.0f, 3.0f).SetTrans(Tween.TransitionType.Sine);
                tw.TweenProperty(_sprite, "position:y", -1.0f, 3.0f).SetTrans(Tween.TransitionType.Sine);
                break;

            default:
                // Normal breathing - subtle scale pulse
                tw.TweenProperty(_sprite, "scale", new Vector2(1.02f, 1.015f), 2.0f).SetTrans(Tween.TransitionType.Sine);
                tw.TweenProperty(_sprite, "scale", new Vector2(1.0f, 1.0f), 2.0f).SetTrans(Tween.TransitionType.Sine);
                break;
        }
    }

    /// <summary>
    /// Update walk animation. Call this from Zombie._PhysicsProcess.
    /// Uses smooth frame cycling for fluid animation with 12 frames.
    /// </summary>
    public void UpdateWalkAnimation(float delta)
    {
        if (_sprite == null)
            return;

        // If no walk frames loaded, use the sprite texture for idle bobbing
        if (_walkFrames.Count <= 1)
        {
            _animTimer += delta;
            return;
        }

        _isMoving = _zombie.Velocity.Length() > 5;

        if (_isMoving)
        {
            _animTimer += delta;
            // Use slightly faster frame interval for smoother feel
            float frameInterval = GameConfig.WalkAnimSpeed * 0.85f;
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

        // Sync outline texture
        _SyncOutlineTexture();

        // Subtle walk brightness pulse
        if (_isMoving)
        {
            float b = 0.95f + Mathf.Abs(Mathf.Sin(_animTimer * 4.0f)) * 0.1f;
            _sprite.Modulate = new Color(b, b, b, 1.0f);
        }
        else
        {
            _sprite.Modulate = Colors.White;
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
        tw.SetLoops(999999);
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
            _healthLabel.Text = $"心️{_zombie.CurrentHealth}/{_zombie.MaxHealth}";
        }
        if (_attackLabel != null)
        {
            // Add ranged indicator for archers
            string damageText = $"剑{_zombie.Damage}";
            if (_zombie.ZombieType == ZombieType.Archer)
                damageText += " 弓";
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
        if (_sprite != null)
        {
            // 保留原 alpha（Invisible 僵尸应保持半隐身），只覆盖色调
            _sprite.Modulate = new Color(Colors.Cyan.R, Colors.Cyan.G, Colors.Cyan.B, _sprite.Modulate.A);
            // Add ice particle effect
            _SpawnStatusParticles(new Color(0.5f, 0.8f, 1.0f), 5, 0.3f);
        }
    }

    public void ShowStunned()
    {
        if (_sprite != null)
        {
            _sprite.Modulate = new Color(0.8f, 0.8f, 1.0f, _sprite.Modulate.A);
            // Show stun stars
            _SpawnStunStars();
        }
    }

    public void ShowPoisoned()
    {
        if (_sprite != null)
        {
            _sprite.Modulate = new Color(Colors.Purple.R, Colors.Purple.G, Colors.Purple.B, _sprite.Modulate.A);
            // Add poison drip effect
            _SpawnStatusParticles(new Color(0.4f, 0.9f, 0.3f), 3, 0.5f);
        }
    }

    public void ShowCharmed()
    {
        if (_sprite != null)
        {
            _sprite.Modulate = new Color(Colors.Magenta.R, Colors.Magenta.G, Colors.Magenta.B, _sprite.Modulate.A);
            // Add heart particles
            _SpawnStatusParticles(new Color(1.0f, 0.5f, 0.7f), 4, 0.4f);
        }
    }

    private void _SpawnStatusParticles(Color color, int amount, float lifetime)
    {
        var particles = new GpuParticles2D
        {
            GlobalPosition = _zombie.GlobalPosition,
            Emitting = true,
            Amount = amount,
            Lifetime = lifetime,
            OneShot = true
        };

        var mat = new ParticleProcessMaterial
        {
            Direction = new Vector3(0, -1, 0),
            InitialVelocityMin = 20.0f,
            InitialVelocityMax = 60.0f,
            Gravity = new Vector3(0, -50, 0),
            ScaleMin = 0.5f,
            ScaleMax = 1.0f,
            Color = color
        };
        particles.ProcessMaterial = mat;
        _zombie.GetParent().AddChild(particles);

        var timer = new Timer { WaitTime = lifetime + 0.2f, OneShot = true };
        timer.Timeout += () => particles.QueueFree();
        particles.AddChild(timer);
        timer.Start();
    }

    private void _SpawnStunStars()
    {
        // Create rotating star effect for stun
        for (int i = 0; i < 3; i++)
        {
            var star = new Label
            {
                Text = "⭐",
                GlobalPosition = _zombie.GlobalPosition + new Vector2(
                    (float)Mathf.Cos(i * Mathf.Tau / 3) * 20,
                    -30 + (float)Mathf.Sin(i * Mathf.Tau / 3) * 10
                ),
                ZIndex = 15
            };
            star.AddThemeFontSizeOverride("font_size", 14);
            _zombie.GetParent().AddChild(star);

            var tw = star.CreateTween();
            tw.SetLoops(2);
            tw.TweenProperty(star, "position:y", star.Position.Y - 10, 0.3f);
            tw.TweenProperty(star, "position:y", star.Position.Y, 0.3f);
            tw.TweenCallback(Callable.From(() =>
            {
                if (IsInstanceValid(star))
                    star.QueueFree();
            }));

            // Auto-free after stun duration
            var autoFree = new Timer { WaitTime = 2.0f, OneShot = true };
            autoFree.Timeout += () =>
            {
                if (IsInstanceValid(star))
                    star.QueueFree();
            };
            star.AddChild(autoFree);
            autoFree.Start();
        }
    }

    public void ShowInvisible(bool revealed)
    {
        if (_sprite != null) _sprite.Modulate = _sprite.Modulate with { A = revealed ? 1.0f : 0.3f };
    }

    public void ClearEffect()
    {
        if (_sprite != null)
        {
            // 恢复默认颜色，但 Invisible 类型需保持半透明 alpha=0.3
            float alpha = (_zombie != null && _zombie.ZombieType == ZombieType.Invisible) ? 0.3f : 1.0f;
            _sprite.Modulate = Colors.White with { A = alpha };
        }
    }

    public void SpawnDeathEffects()
    {
        // Use parent node for effects since zombie will be QueueFree'd
        var parent = _zombie.GetParent();
        if (parent == null) return;

        // Spawn blood stain directly
        _SpawnBloodStain();

        // Spawn corpse and death particles directly (not via tween, since zombie is about to be freed)
        _SpawnCorpse();
        _SpawnDeathParticles();
        _SpawnDeathInkDots();
    }

    private void _SpawnDeathInkDots()
    {
        var particles = new GpuParticles2D
        {
            GlobalPosition = _zombie.GlobalPosition,
            Emitting = true,
            Amount = 8,
            Lifetime = 0.6f,
            OneShot = true
        };

        var mat = new ParticleProcessMaterial
        {
            Direction = new Vector3(0, -1, 0),
            InitialVelocityMin = 10.0f,
            InitialVelocityMax = 40.0f,
            Gravity = new Vector3(0, -20, 0),
            ScaleMin = 0.5f,
            ScaleMax = 1.5f,
            Color = new Color(BloodRed, 0.8f)
        };
        particles.ProcessMaterial = mat;
        _zombie.GetParent().AddChild(particles);

        var timer = new Timer { WaitTime = 1.0f, OneShot = true };
        timer.Timeout += () => particles.QueueFree();
        particles.AddChild(timer);
        timer.Start();
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

        // Use configurable particle counts based on zombie type
        int particleCount;
        if (_zombie.IsBoss)
            particleCount = GameConfig.VfxDeathParticlesBoss;
        else if (_zombie.ZombieType == ZombieType.Tank)
            particleCount = GameConfig.VfxDeathParticlesTank;
        else
            particleCount = GameConfig.VfxDeathParticlesDefault;

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

        if (_zombie.ZombieType == ZombieType.Exploder || _zombie.IsBoss)
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

    public void ShowStompShockwave()
    {
        var ring = new Node2D();
        ring.GlobalPosition = _zombie.GlobalPosition;

        var line = new Line2D();
        int pts = 32;
        for (int i = 0; i <= pts; i++)
        {
            float angle = i * 2.0f * Mathf.Pi / pts;
            line.AddPoint(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 15.0f);
        }
        line.Width = 4.0f;
        line.DefaultColor = new Color(0.6f, 0.6f, 0.7f, 0.9f);
        ring.AddChild(line);
        _zombie.GetParent().AddChild(ring);

        var tw = ring.CreateTween();
        tw.Parallel().TweenProperty(ring, "scale", new Vector2(5, 5), 0.5f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tw.Parallel().TweenProperty(ring, "modulate:a", 0.0f, 0.5f);
        tw.TweenCallback(Callable.From(ring.QueueFree));
        // 伤害判定已归位至 Zombie._HandleMech（VFX 只负责视觉）
    }

    public void ShowChargeEffect()
    {
        if (_sprite == null) return;
        var tw = _zombie.CreateTween();
        tw.TweenProperty(_sprite, "self_modulate:g", 1.5f, 0.2f);
        tw.TweenProperty(_sprite, "self_modulate:b", 1.5f, 0.2f);
        tw.TweenProperty(_sprite, "self_modulate", Colors.White, 0.4f);
    }

    public void ShowDiveEffect()
    {
        if (_sprite == null) return;
        _sprite.Modulate = new Color(0.8f, 0.3f, 0.3f);
        var tw = _zombie.CreateTween();
        tw.TweenProperty(_sprite, "modulate", Colors.White, 0.3f).SetDelay(0.5f);
    }

    public void ShowFlyUpEffect()
    {
        _SpawnStatusParticles(new Color(0.5f, 0.15f, 0.6f), 6, 0.4f);
    }

    private Texture2D _MakeEllipseTexture(int width, int height, Color color)
    {
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
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
