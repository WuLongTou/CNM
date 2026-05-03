namespace ZombieWars.Player;

using Godot;
using ZombieWars.Core;
using ZombieWars.UI;
using ZombieWars.Vfx;
using static ZombieWars.Core.GameEnums;
using static ZombieWars.UI.HandDrawnTheme.Colors;

public partial class PlayerVfx : Node
{
    public Player? Player { get; set; }

    private readonly Godot.Collections.Array<Texture2D> _walkFrames = new();
    private readonly Godot.Collections.Array<Texture2D> _attackFrames = new();
    private readonly Godot.Collections.Array<Texture2D> _idleFrames = new();
    private Texture2D? _idleFrame;
    private int _currentFrame = 0;
    private int _attackFrameIdx = 0;
    private float _animTimer = 0.0f;
    private bool _isMoving = false;
    private bool _facingLeft = false;

    private float _trailTimer = 0.0f;

    private enum AnimState { Idle, Walk, Attack, Hurt, Dash }
    private AnimState _animState = AnimState.Idle;
    private float _stateTimer = 0.0f;

    private Texture2D? _baseTexture;
    private Texture2D? _attackFrame;
    private Texture2D? _hurtFrame;
    private Texture2D? _dashFrame;
    private Texture2D? _hurtFallback;
    private Texture2D? _dashFallback;
    private Texture2D? _attackFallback;
    private Sprite2D? _outlineSprite;

    public void Setup(Player p)
    {
        Player = p;
        _TryLoadPngSprites();
        if (_idleFrame == null)
        {
            _CreatePlayerSprite();
        }
        _CreateShadow();
        _CreateStatusDisplays();
        _CreateMeleeRangeIndicator();
        if (_baseTexture != null)
            CallDeferred(MethodName._GenerateStateVariantsDeferred);

        _ApplySketchOutline();
    }

    private void _ApplySketchOutline()
    {
        // Disabled: outline sprite caused rendering artifacts
        // Re-enable if needed with proper ZIndex handling
    }

    private void _SyncOutlineTexture()
    {
        if (_outlineSprite != null && Player?.Sprite != null)
            _outlineSprite.Texture = Player.Sprite.Texture;
    }

    private void _GenerateStateVariantsDeferred()
    {
        if (_baseTexture == null || Player?.Sprite == null) return;
        var baseImg = _baseTexture.GetImage();
        if (baseImg == null) return;

        // Work on a copy so we don't mutate the original texture
        var img = baseImg.Duplicate() as Image;
        if (img == null) return;

        // Use the processed (potentially resized) image for all states
        _idleFrame = ImageTexture.CreateFromImage(img);
        if (_walkFrames.Count == 0)
            Player.Sprite.Texture = _idleFrame;
        Player.Sprite.Scale = new Vector2(1.0f, 1.0f);

        var attackImg = img.Duplicate() as Image;
        _TintImage(attackImg, new Color(1.3f, 1.1f, 0.7f));
        _attackFrame = ImageTexture.CreateFromImage(attackImg);

        var hurtImg = img.Duplicate() as Image;
        _TintImage(hurtImg, new Color(1.5f, 0.4f, 0.3f));
        _hurtFrame = ImageTexture.CreateFromImage(hurtImg);

        var dashImg = img.Duplicate() as Image;
        _TintImage(dashImg, new Color(0.2f, 0.1f, 0.1f, 0.6f));
        _dashFrame = ImageTexture.CreateFromImage(dashImg);
    }

    // Per-weapon-type sprite caches
    private static readonly Godot.Collections.Array<Texture2D> _warriorWalkFrames = new();
    private static readonly Godot.Collections.Array<Texture2D> _rangerWalkFrames = new();
    private static readonly Godot.Collections.Array<Texture2D> _mageWalkFrames = new();
    private static readonly Godot.Collections.Array<Texture2D> _warriorAttackFrames = new();
    private static readonly Godot.Collections.Array<Texture2D> _rangerAttackFrames = new();
    private static readonly Godot.Collections.Array<Texture2D> _mageAttackFrames = new();
    private static readonly Godot.Collections.Array<Texture2D> _warriorIdleFrames = new();
    private static readonly Godot.Collections.Array<Texture2D> _rangerIdleFrames = new();
    private static readonly Godot.Collections.Array<Texture2D> _mageIdleFrames = new();
    private static Texture2D? _warriorIdle;
    private static Texture2D? _rangerIdle;
    private static Texture2D? _mageIdle;
    private static Texture2D? _warriorBase;
    private static Texture2D? _rangerBase;
    private static Texture2D? _mageBase;
    private static bool _allSpritesLoaded = false;

    public static void ResetSpriteCache()
    {
        _warriorWalkFrames.Clear();
        _rangerWalkFrames.Clear();
        _mageWalkFrames.Clear();
        _warriorAttackFrames.Clear();
        _rangerAttackFrames.Clear();
        _mageAttackFrames.Clear();
        _warriorIdleFrames.Clear();
        _rangerIdleFrames.Clear();
        _mageIdleFrames.Clear();
        _warriorIdle = null;
        _rangerIdle = null;
        _mageIdle = null;
        _warriorBase = null;
        _rangerBase = null;
        _mageBase = null;
        _allSpritesLoaded = false;
        // Also clear the global texture cache so re-loaded PNGs get fresh processing
        Core.TextureUtils.ClearCache();
    }

    private AttackType _lastAttackType = AttackType.Melee;

    private void _TryLoadPngSprites()
    {
        _ReloadStaticSprites();
        _ApplyWeaponSprites();
    }

    private void _ReloadStaticSprites()
    {
        if (_allSpritesLoaded && _warriorIdle != null)
            return;

        // Invalidate texture cache so re-loaded PNGs get fresh processing
        Core.TextureUtils.ClearCache();
        _allSpritesLoaded = true;
        _LoadWarriorSprites();
        _LoadRangerSprites();
        _LoadMageSprites();
    }

    private static readonly string[] _WARRIOR_WALK = {
        "warrior_walk_06", "warrior_walk_07",
        "warrior_idle_02", "warrior_idle_02",
        "warrior_idle_04", "warrior_idle_04",
        "warrior_idle_06", "warrior_idle_06",
    };
    private static readonly string[] _WARRIOR_ATTACK = {
        "warrior_attack_01", "warrior_attack_03",
        "warrior_attack_04", "warrior_attack_04",
        "warrior_attack_06", "warrior_attack_05",
    };
    private static readonly string[] _WARRIOR_IDLE = {
        "warrior_idle_02", "warrior_idle_04",
        "warrior_idle_06", "warrior_idle_08",
    };

    private static readonly string[] _RANGER_WALK = {
        "ranger_walk_02", "ranger_walk_07",
        "ranger_idle_01", "ranger_idle_01",
        "ranger_idle_04", "ranger_idle_04",
        "ranger_walk_07", "ranger_walk_02",
    };
    private static readonly string[] _RANGER_ATTACK = {
        "ranger_attack_03", "ranger_attack_03",
        "ranger_attack_05", "ranger_attack_05",
        "ranger_attack_03", "ranger_attack_05",
    };
    private static readonly string[] _RANGER_IDLE = {
        "ranger_idle_01", "ranger_idle_04",
        "ranger_idle_01", "ranger_idle_04",
    };

    private static readonly string[] _MAGE_WALK = {
        "mage_walk_01", "mage_walk_02",
        "mage_walk_03", "mage_walk_04",
        "mage_walk_05", "mage_walk_06",
        "mage_walk_07", "mage_walk_08",
    };
    private static readonly string[] _MAGE_ATTACK = {
        "mage_idle_02", "mage_idle_02",
        "mage_idle_02", "mage_idle_02",
        "mage_idle_02", "mage_idle_02",
    };
    private static readonly string[] _MAGE_IDLE = {
        "mage_idle_01", "mage_idle_02",
        "mage_idle_03", "mage_idle_04",
    };

    private static void _LoadWarriorSprites()
    {
        _LoadSpritesFromFiles("warrior", _WARRIOR_WALK, _WARRIOR_ATTACK, _WARRIOR_IDLE,
            _warriorWalkFrames, _warriorAttackFrames, _warriorIdleFrames,
            out _warriorBase, out _warriorIdle);
    }

    private static void _LoadRangerSprites()
    {
        _LoadSpritesFromFiles("ranger", _RANGER_WALK, _RANGER_ATTACK, _RANGER_IDLE,
            _rangerWalkFrames, _rangerAttackFrames, _rangerIdleFrames,
            out _rangerBase, out _rangerIdle);
    }

    private static void _LoadMageSprites()
    {
        _LoadSpritesFromFiles("mage", _MAGE_WALK, _MAGE_ATTACK, _MAGE_IDLE,
            _mageWalkFrames, _mageAttackFrames, _mageIdleFrames,
            out _mageBase, out _mageIdle);
    }

    private static void _LoadSpritesFromFiles(string charName,
        string[] walkFiles, string[] attackFiles, string[] idleFiles,
        Godot.Collections.Array<Texture2D> walkOut,
        Godot.Collections.Array<Texture2D> attackOut,
        Godot.Collections.Array<Texture2D> idleOut,
        out Texture2D? idleFirst, out Texture2D? baseOut)
    {
        walkOut.Clear();
        attackOut.Clear();
        idleOut.Clear();
        idleFirst = null;
        baseOut = null;

        string dir = $"res://assets/sprites/player/{charName}";
        Texture2D? firstWalk = null;
        Texture2D? firstAtk = null;
        Texture2D? firstIdle = null;

        foreach (string f in walkFiles)
        {
            var frame = TextureUtils.LoadNormalizedSprite($"{dir}/{f}.png");
            if (frame != null)
            {
                walkOut.Add(frame);
                if (firstWalk == null) firstWalk = frame;
            }
        }

        foreach (string f in attackFiles)
        {
            var frame = TextureUtils.LoadNormalizedSprite($"{dir}/{f}.png");
            if (frame != null)
            {
                attackOut.Add(frame);
                if (firstAtk == null) firstAtk = frame;
            }
        }

        foreach (string f in idleFiles)
        {
            var frame = TextureUtils.LoadNormalizedSprite($"{dir}/{f}.png");
            if (frame != null)
            {
                idleOut.Add(frame);
                if (firstIdle == null) firstIdle = frame;
            }
        }

        idleFirst = firstIdle;
        baseOut = firstWalk ?? firstAtk ?? firstIdle;
    }

    private static void _LoadCharacterSprites(string charName,
        Godot.Collections.Array<Texture2D> walkOut,
        Godot.Collections.Array<Texture2D> attackOut,
        Godot.Collections.Array<Texture2D> idleOut,
        out Texture2D? idleFirst, out Texture2D? baseOut)
    {
        walkOut.Clear();
        attackOut.Clear();
        idleOut.Clear();
        idleFirst = null;
        baseOut = null;

        // Priority 1: Processed sprites (white bg stripped, centered, individual PNGs in subdirectory)
        // Layout: processed/{charName}/warrior_idle_01.png ... warrior_walk_01.png ... warrior_attack_01.png
        string processedDir = $"res://assets/sprites/player/{charName}";
        bool hasProcessed = _HasProcessedSprites(processedDir, charName);

        if (hasProcessed)
        {
            for (int i = 1; i <= 8; i++)
            {
                string path = $"{processedDir}/{charName}_walk_{i:D2}.png";
                var frame = TextureUtils.LoadNormalizedSprite(path);
                if (frame != null) walkOut.Add(frame);
            }

            for (int i = 1; i <= 6; i++)
            {
                string path = $"{processedDir}/{charName}_attack_{i:D2}.png";
                var frame = TextureUtils.LoadNormalizedSprite(path);
                if (frame != null) attackOut.Add(frame);
            }

            // Load idle frames (idle_01 through idle_08, or fallback to less frames)
            Texture2D? firstIdle = null;
            int maxIdleFrames = 8; // warrior has 8, others have 4
            for (int i = 1; i <= maxIdleFrames; i++)
            {
                string path = $"{processedDir}/{charName}_idle_{i:D2}.png";
                var frame = TextureUtils.LoadNormalizedSprite(path);
                if (frame != null)
                {
                    if (firstIdle == null)
                    {
                        firstIdle = frame;
                        idleOut.Add(frame);
                    }
                    else
                    {
                        bool firstFacesLeft = Core.TextureUtils.FacesLeft(firstIdle);
                        bool thisFacesLeft = Core.TextureUtils.FacesLeft(frame);
                        if (firstFacesLeft != thisFacesLeft)
                            frame = Core.TextureUtils.FlipHorizontal(frame);
                        idleOut.Add(frame);
                    }
                }
                if (i == 1) baseOut = frame; // Use frame 1 as base
            }
            idleFirst = firstIdle;

            // Fallback: try single idle.png if no multi-frame
            if (idleFirst == null)
            {
                string singlePath = $"{processedDir}/{charName}_idle.png";
                idleFirst = TextureUtils.LoadNormalizedSprite(singlePath);
                baseOut = idleFirst;
                if (idleFirst != null) idleOut.Add(idleFirst);
            }
            return;
        }

        // Priority 2: Original individual frame files (may have white bg)
        _LoadOriginalIndividualFrames(charName, walkOut, attackOut, idleOut, out idleFirst, out baseOut);
        if (walkOut.Count >= 8)
            return;

        // Priority 3: Atlas fallback (4x4 grid)
        _LoadFromAtlas(charName, walkOut, attackOut, idleOut, out idleFirst, out baseOut);
    }

    private static bool _HasProcessedSprites(string dir, string charName)
    {
        // Check if processed walk frame 01 exists
        string testPath = $"{dir}/{charName}_walk_01.png";
        if (!FileAccess.FileExists(testPath))
            return false;
        // Also check attack frame 01
        string atkPath = $"{dir}/{charName}_attack_01.png";
        return FileAccess.FileExists(atkPath);
    }

    private static void _LoadOriginalIndividualFrames(string charName,
        Godot.Collections.Array<Texture2D> walkOut,
        Godot.Collections.Array<Texture2D> attackOut,
        Godot.Collections.Array<Texture2D> idleOut,
        out Texture2D? idleFirst, out Texture2D? baseOut)
    {
        walkOut.Clear();
        attackOut.Clear();
        idleOut.Clear();
        idleFirst = null;
        baseOut = null;

        for (int i = 1; i <= 8; i++)
        {
            string pngPath = $"res://assets/sprites/player/{charName}_walk_{i}.png";
            string jpgPath = $"res://assets/sprites/player/{charName}_walk_{i}.jpg";

            string path = FileAccess.FileExists(pngPath) ? pngPath :
                          FileAccess.FileExists(jpgPath) ? jpgPath : "";
            if (string.IsNullOrEmpty(path)) continue;

            var frame = TextureUtils.LoadNormalizedSprite(path);
            if (frame != null)
                walkOut.Add(frame);
        }

        string idlePng = $"res://assets/sprites/player/{charName}_idle.png";
        string idleJpg = $"res://assets/sprites/player/{charName}_idle.jpg";
        string idlePath = FileAccess.FileExists(idlePng) ? idlePng :
                          FileAccess.FileExists(idleJpg) ? idleJpg : "";
        if (!string.IsNullOrEmpty(idlePath))
        {
            idleFirst = TextureUtils.LoadNormalizedSprite(idlePath);
            if (idleFirst != null)
            {
                idleOut.Add(idleFirst);
                baseOut = idleFirst;
            }
        }

        if (idleFirst == null && walkOut.Count > 0)
        {
            idleFirst = walkOut[0];
            baseOut = idleFirst;
        }

        for (int i = 1; i <= 6; i++)
        {
            string pngPath = $"res://assets/sprites/player/{charName}_attack_{i}.png";
            string jpgPath = $"res://assets/sprites/player/{charName}_attack_{i}.jpg";

            string path = FileAccess.FileExists(pngPath) ? pngPath :
                          FileAccess.FileExists(jpgPath) ? jpgPath : "";
            if (string.IsNullOrEmpty(path)) continue;

            var frame = TextureUtils.LoadNormalizedSprite(path);
            if (frame != null)
                attackOut.Add(frame);
        }
    }

    private static void _LoadFromAtlas(string charName,
        Godot.Collections.Array<Texture2D> walkOut,
        Godot.Collections.Array<Texture2D> attackOut,
        Godot.Collections.Array<Texture2D> idleOut,
        out Texture2D? idleFirst, out Texture2D? baseOut)
    {
        walkOut.Clear();
        attackOut.Clear();
        idleOut.Clear();
        idleFirst = null;
        baseOut = null;

        string atlasPath = $"res://assets/sprites/player/{charName}_atlas.png";
        if (!FileAccess.FileExists(atlasPath))
            return;

        var atlasFrames = TextureUtils.LoadAtlasFrames(atlasPath, 4, 4);
        if (atlasFrames.Count < 8)
            return;

        // Atlas layout: row0 = idle(2), row1-2 = walk(8), row2-3 = attack(6)
        // index 0-1: idle, 2-9: walk (8), 10-15: attack (6)
        int idx = 0;
        if (atlasFrames.Count > 0)
        {
            idleFirst = atlasFrames[0];
            idleOut.Add(idleFirst);
            baseOut = idleFirst;
        }
        idx = 2;
        for (int i = 0; i < 8 && idx < atlasFrames.Count; i++, idx++)
            walkOut.Add(atlasFrames[idx]);
        for (int i = 0; i < 6 && idx < atlasFrames.Count; i++, idx++)
            attackOut.Add(atlasFrames[idx]);
    }

    /// Switch active sprites based on current weapon type. Call when weapon changes.
    public void SwitchWeaponSprites(AttackType attackType)
    {
        if (attackType == _lastAttackType) return;
        _lastAttackType = attackType;
        _ApplyWeaponSprites();
    }

    private void _ApplyWeaponSprites()
    {
        // Re-load from disk if static caches were cleared (e.g. scene reload)
        if (!_allSpritesLoaded || _warriorIdle == null)
            _ReloadStaticSprites();

        _walkFrames.Clear();
        _attackFrames.Clear();
        _idleFrames.Clear();
        _idleFrame = null;
        _baseTexture = null;

        switch (_lastAttackType)
        {
            case AttackType.Ranged:
                _walkFrames.AddRange(_rangerWalkFrames);
                _attackFrames.AddRange(_rangerAttackFrames);
                _idleFrames.AddRange(_rangerIdleFrames);
                _idleFrame = _rangerIdle;
                _baseTexture = _rangerBase;
                break;
            case AttackType.Magic:
            case AttackType.Area:
                _walkFrames.AddRange(_mageWalkFrames);
                _attackFrames.AddRange(_mageAttackFrames);
                _idleFrames.AddRange(_mageIdleFrames);
                _idleFrame = _mageIdle;
                _baseTexture = _mageBase;
                break;
            default: // Melee, Mine
                _walkFrames.AddRange(_warriorWalkFrames);
                _attackFrames.AddRange(_warriorAttackFrames);
                _idleFrames.AddRange(_warriorIdleFrames);
                _idleFrame = _warriorIdle;
                _baseTexture = _warriorBase;
                break;
        }

        if (_idleFrame != null && Player?.Sprite != null)
            Player.Sprite.Texture = _idleFrame;
        else if (_walkFrames.Count > 0 && Player?.Sprite != null)
            Player.Sprite.Texture = _walkFrames[0];

        if (Player?.Sprite != null)
            Player.Sprite.Scale = new Vector2(1.0f, 1.0f);

        if (_baseTexture != null && Player?.Sprite != null)
        {
            // Use _idleFrame (a processed single-frame texture) as the sprite base,
            // not the raw walk frame from _LoadSpritesFromFiles — avoids aliasing
            // the shared Image buffer that _GenerateStateVariantsDeferred modifies.
            Player.Sprite.Texture = _idleFrame ?? _baseTexture;
            CallDeferred(MethodName._GenerateStateVariantsDeferred);
        }

        if (_walkFrames.Count == 0 && _idleFrame == null && _baseTexture == null)
            _CreatePlayerSprite();
    }

    private static Texture2D? _LoadWithAlpha(string path)
    {
        var tex = GD.Load<Texture2D>(path);
        if (tex == null) return null;
        var img = tex.GetImage();
        if (img == null) return tex;
        // Duplicate before mutating — tex.GetImage() returns a shared reference
        img = img.Duplicate() as Image ?? img;
        if (img.GetFormat() != Image.Format.Rgba8)
            img.Convert(Image.Format.Rgba8);
        _StripWhiteBackground(img, 0.82f);
        return ImageTexture.CreateFromImage(img);
    }

    private static void _StripWhiteBackground(Image img, float threshold)
    {
        int w = img.GetWidth();
        int h = img.GetHeight();
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var c = img.GetPixel(x, y);
                float brightness = (c.R + c.G + c.B) / 3.0f;
                if (brightness > threshold && c.R > threshold && c.G > threshold && c.B > threshold)
                {
                    float alpha = Mathf.Clamp((1.0f - brightness) / (1.0f - threshold), 0.0f, 1.0f);
                    img.SetPixel(x, y, new Color(c.R, c.G, c.B, alpha * c.A));
                }
            }
        }
    }

    private static void _TintImage(Image? img, Color tint)
    {
        if (img == null) return;
        int w = img.GetWidth();
        int h = img.GetHeight();
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var c = img.GetPixel(x, y);
                if (c.A < 0.05f) continue;
                img.SetPixel(x, y, new Color(
                    Mathf.Clamp(c.R * tint.R, 0.0f, 1.0f),
                    Mathf.Clamp(c.G * tint.G, 0.0f, 1.0f),
                    Mathf.Clamp(c.B * tint.B, 0.0f, 1.0f),
                    c.A * tint.A));
            }
        }
    }

    public void PlayDeathAnimation()
    {
        if (Player == null) return;
        Node? parent = Player.GetParent();
        if (parent == null) return;

        var flash = new ColorRect();
        flash.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        flash.Color = new Color(BloodRed, 0.3f);
        flash.MouseFilter = Control.MouseFilterEnum.Ignore;
        flash.ZIndex = 50;
        Player.GetTree().CurrentScene.AddChild(flash);

        var flashTw = flash.CreateTween();
        flashTw.TweenProperty(flash, "color:a", 0.0f, 0.6f);
        flashTw.TweenCallback(Callable.From(flash.QueueFree));

        if (Player.Sprite != null)
        {
            float offsetX = (float)GD.RandRange(-6f, 6f);
            float offsetY = (float)GD.RandRange(-6f, 6f);
            var tw = Player.CreateTween();
            tw.Parallel().TweenProperty(Player.Sprite, "modulate:a", 0.0f, 0.8f);
            tw.Parallel().TweenProperty(Player.Sprite, "position", new Vector2(offsetX, offsetY), 0.8f)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        }

        Vector2 center = Player.GlobalPosition;
        int count = (int)GD.RandRange(8, 13);
        for (int i = 0; i < count; i++)
        {
            float angle = (float)GD.RandRange(0, Mathf.Tau);
            float length = (float)GD.RandRange(8, 28);
            var ink = new Node2D();
            ink.GlobalPosition = center;
            var line = new Line2D();
            line.Width = (float)GD.RandRange(0.3f, 0.7f);
            line.DefaultColor = GD.Randf() > 0.5f ? BloodRed : BloodBright;
            line.AddPoint(Vector2.Zero);
            line.AddPoint(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length);
            ink.AddChild(line);
            parent.AddChild(ink);

            var inkTw = ink.CreateTween();
            inkTw.TweenProperty(ink, "modulate:a", 0.0f, 0.6f);
            inkTw.TweenCallback(Callable.From(ink.QueueFree));
        }
    }

    public void PlayAttackAnim()
    {
        _animState = AnimState.Attack;
        _stateTimer = 0.12f;
        _attackFrameIdx = 0;
        _ApplyStateFrame();
    }
    public void PlayHurtAnim() { _animState = AnimState.Hurt; _stateTimer = 0.2f; _ApplyStateFrame(); }
    public void PlayDashAnim() { _animState = AnimState.Dash; _stateTimer = 0.15f; _ApplyStateFrame(); }

    private void _ApplyStateFrame()
    {
        if (Player?.Sprite == null) return;
        Texture2D? tex = null;
        switch (_animState)
        {
            case AnimState.Attack:
                if (_attackFrames.Count > 0)
                {
                    tex = _attackFrames[_attackFrameIdx % _attackFrames.Count];
                }
                else
                {
                    tex = _attackFrame;
                    // Cycle through attack frames
                    if (_stateTimer <= 0)
                    {
                        _attackFrameIdx++;
                        _stateTimer = 0.1f;
                    }
                }
                break;
            case AnimState.Hurt:
                tex = _hurtFrame;
                break;
            case AnimState.Dash:
                tex = _dashFrame;
                break;
            default:
                tex = _idleFrame;
                break;
        }
        Player.Sprite.Texture = tex ?? _idleFrame;
    }

    private void _CreateShadow()
    {
        Player!.Shadow = new Sprite2D
        {
            Texture = _MakeShadowTexture(32, 12, new Color(0, 0, 0, 0.5f)),
            Position = new Vector2(0, 18)
        };
        Player.AddChild(Player.Shadow);
    }

    private Texture2D _MakeShadowTexture(int w, int h, Color color)
    {
        using var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        float cx = w / 2.0f;
        float cy = h / 2.0f;
        float rx = w / 2.0f;
        float ry = h / 2.0f;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                float dx = (x - cx) / rx;
                float dy = (y - cy) / ry;
                if (dx * dx + dy * dy <= 1.0f)
                {
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    img.SetPixel(x, y, new Color(color.R, color.G, color.B, color.A * (1.0f - dist)));
                }
            }
        }
        return ImageTexture.CreateFromImage(img);
    }

    private void _CreatePlayerSprite()
    {
        int size = 48;
        var framesData = new Godot.Collections.Array<Image>();

        // Generate 16 walk frames for smooth animation
        for (int frameIdx = 0; frameIdx < 16; frameIdx++)
        {
            using var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            float cx = size / 2.0f;
            float cy = size / 2.0f;

            float walkPhase = frameIdx / 16.0f;
            float legSwing = Mathf.Sin(walkPhase * Mathf.Tau) * 4.0f;
            float bodyBob = -Mathf.Abs(Mathf.Sin(walkPhase * Mathf.Tau)) * 2.0f;
            float armSwing = Mathf.Sin(walkPhase * Mathf.Tau) * 3.0f;

            // Shadow under feet
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float shadowY = size * 0.88f;
                    float shadowDist = Mathf.Abs(y - shadowY);
                    if (shadowDist < 3 && Mathf.Abs(x - cx) < 10)
                    {
                        img.SetPixel(x, y, new Color(0, 0, 0, 0.15f));
                    }
                }
            }

            // Body
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float bodyTop = cy * 0.48f + bodyBob;
                    float bodyBot = cy * 1.35f + bodyBob;
                    if (y >= bodyTop && y <= bodyBot)
                    {
                        float bodyW = size * 0.18f - Mathf.Abs(y - cy - bodyBob) * 0.03f;
                        if (Mathf.Abs(x - cx) < bodyW)
                        {
                            float shade = 0.25f + (y - bodyTop) / (bodyBot - bodyTop) * 0.05f;
                            img.SetPixel(x, y, new Color(shade * 0.8f, shade * 0.9f, shade * 0.7f));
                        }
                    }
                }
            }

            // Head
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float headR = size * 0.16f;
                    float headCy = cy * 0.38f + bodyBob;
                    float hd = new Vector2(x - cx, y - headCy).Length();
                    if (hd < headR)
                    {
                        if (hd > headR * 0.7f)
                            img.SetPixel(x, y, new Color(0.28f, 0.32f, 0.22f));
                        else if (hd > headR * 0.3f)
                            img.SetPixel(x, y, new Color(0.72f, 0.58f, 0.48f));
                        else
                            img.SetPixel(x, y, new Color(0.78f, 0.62f, 0.52f));
                    }
                }
            }

            // Eyes
            int eyeY = (int)(cy * 0.34f + bodyBob);
            foreach (int dx in new[] { -1, 1 })
            {
                int ex = (int)(cx + dx * size * 0.055f);
                if (ex >= 0 && ex < size && eyeY >= 0 && eyeY < size)
                {
                    img.SetPixel(ex, eyeY, new Color(0.1f, 0.1f, 0.1f));
                    if (eyeY - 1 >= 0) img.SetPixel(ex, eyeY - 1, new Color(0.95f, 0.95f, 0.95f));
                    if (eyeY + 1 < size) img.SetPixel(ex, eyeY + 1, new Color(0.2f, 0.2f, 0.2f));
                }
            }

            // Arms
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float armTop = cy * 0.5f + bodyBob;
                    float armBot = cy * 1.1f + bodyBob;
                    if (y >= armTop && y <= armBot)
                    {
                        foreach (int side in new[] { -1, 1 })
                        {
                            float armCenterX = cx + side * (size * 0.22f + side * armSwing);
                            if (Mathf.Abs(x - armCenterX) < 2.5f)
                            {
                                img.SetPixel(x, y, new Color(0.28f, 0.32f, 0.22f));
                            }
                        }
                    }
                }
            }

            // Legs with walking animation
            for (int y = (int)(cy * 1.2f + bodyBob); y <= (int)(cy * 1.75f + bodyBob); y++)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    float legOffsetX = side * legSwing;
                    int lx = (int)(cx + side * size * 0.08f + legOffsetX);
                    if (lx >= 0 && lx < size && y >= 0 && y < size)
                    {
                        img.SetPixel(lx, y, new Color(0.18f, 0.2f, 0.15f));
                        if (lx + 1 < size) img.SetPixel(lx + 1, y, new Color(0.18f, 0.2f, 0.15f));
                    }
                }
            }

            framesData.Add(img);
        }

        _walkFrames.Clear();
        foreach (var img in framesData)
            _walkFrames.Add(ImageTexture.CreateFromImage(img));

        // Generate idle frame with subtle breathing
        using var idleImg = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float idleCx = size / 2.0f;
        float idleCy = size / 2.0f;
        // Mid-breath position (unused in static idle frame)

        // Shadow
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float shadowY = size * 0.88f;
                if (Mathf.Abs(y - shadowY) < 3 && Mathf.Abs(x - idleCx) < 10)
                {
                    idleImg.SetPixel(x, y, new Color(0, 0, 0, 0.18f));
                }
            }
        }

        // Body
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float bodyTop = idleCy * 0.48f;
                float bodyBot = idleCy * 1.35f;
                if (y >= bodyTop && y <= bodyBot)
                {
                    float bodyW = size * 0.18f - Mathf.Abs(y - idleCy) * 0.03f;
                    if (Mathf.Abs(x - idleCx) < bodyW)
                    {
                        float shade = 0.25f + (y - bodyTop) / (bodyBot - bodyTop) * 0.05f;
                        idleImg.SetPixel(x, y, new Color(shade * 0.8f, shade * 0.9f, shade * 0.7f));
                    }
                }
            }
        }

        // Head
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float headR = size * 0.16f;
                float headCy = idleCy * 0.38f;
                float hd = new Vector2(x - idleCx, y - headCy).Length();
                if (hd < headR)
                {
                    if (hd > headR * 0.7f)
                        idleImg.SetPixel(x, y, new Color(0.28f, 0.32f, 0.22f));
                    else if (hd > headR * 0.3f)
                        idleImg.SetPixel(x, y, new Color(0.72f, 0.58f, 0.48f));
                    else
                        idleImg.SetPixel(x, y, new Color(0.78f, 0.62f, 0.52f));
                }
            }
        }

        // Eyes
        int idleEyeY = (int)(idleCy * 0.34f);
        foreach (int dx in new[] { -1, 1 })
        {
            int ex = (int)(idleCx + dx * size * 0.055f);
            if (ex >= 0 && ex < size && idleEyeY >= 0 && idleEyeY < size)
            {
                idleImg.SetPixel(ex, idleEyeY, new Color(0.1f, 0.1f, 0.1f));
                if (idleEyeY - 1 >= 0) idleImg.SetPixel(ex, idleEyeY - 1, new Color(0.95f, 0.95f, 0.95f));
            }
        }

        _idleFrame = ImageTexture.CreateFromImage(idleImg);

        // Generate attack frames (8 frames showing weapon swing)
        _attackFrames.Clear();
        for (int attackFrame = 0; attackFrame < 8; attackFrame++)
        {
            using var attackImg = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            float attackCx = size / 2.0f;
            float attackCy = size / 2.0f;
            float attackPhase = attackFrame / 8.0f;
            float weaponAngle = Mathf.Lerp(-0.8f, 0.8f, attackPhase);
            float bodyLean = Mathf.Sin(attackPhase * Mathf.Pi) * 1.5f;

            // Shadow
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float shadowY = size * 0.88f;
                    if (Mathf.Abs(y - shadowY) < 3 && Mathf.Abs(x - attackCx) < 10)
                    {
                        attackImg.SetPixel(x, y, new Color(0, 0, 0, 0.15f));
                    }
                }
            }

            // Body with lean
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float leanOffset = bodyLean * (y - attackCy * 0.5f) / (size * 0.5f);
                    float bodyTop = attackCy * 0.48f;
                    float bodyBot = attackCy * 1.35f;
                    if (y >= bodyTop && y <= bodyBot)
                    {
                        float bodyW = size * 0.18f - Mathf.Abs(y - attackCy) * 0.03f;
                        if (Mathf.Abs(x - attackCx - leanOffset) < bodyW)
                        {
                            float shade = 0.25f + (y - bodyTop) / (bodyBot - bodyTop) * 0.05f;
                            attackImg.SetPixel(x, y, new Color(shade * 0.9f, shade * 0.8f, shade * 0.6f));
                        }
                    }
                }
            }

            // Head
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float leanOffset = bodyLean * (y - attackCy * 0.3f) / (size * 0.3f);
                    float headR = size * 0.16f;
                    float headCy = attackCy * 0.38f;
                    float hd = new Vector2(x - attackCx - leanOffset, y - headCy).Length();
                    if (hd < headR)
                    {
                        if (hd > headR * 0.7f)
                            attackImg.SetPixel(x, y, new Color(0.3f, 0.34f, 0.24f));
                        else if (hd > headR * 0.3f)
                            attackImg.SetPixel(x, y, new Color(0.72f, 0.58f, 0.48f));
                        else
                            attackImg.SetPixel(x, y, new Color(0.78f, 0.62f, 0.52f));
                    }
                }
            }

            // Weapon (sword)
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float weaponOriginX = attackCx + size * 0.2f;
                    float weaponOriginY = attackCy * 0.6f;
                    float weaponLen = size * 0.45f;
                    float weaponWidth = 2.5f;

                    // Rotate point around origin
                    float cos = Mathf.Cos(weaponAngle);
                    float sin = Mathf.Sin(weaponAngle);
                    float dx = x - weaponOriginX;
                    float dy = y - weaponOriginY;
                    float rotX = dx * cos - dy * sin;
                    float rotY = dx * sin + dy * cos;

                    if (rotX >= 0 && rotX <= weaponLen && Mathf.Abs(rotY) < weaponWidth)
                    {
                        float t = rotX / weaponLen;
                        if (t < 0.15f)
                            attackImg.SetPixel(x, y, new Color(0.6f, 0.5f, 0.3f)); // Handle
                        else
                            attackImg.SetPixel(x, y, new Color(0.85f, 0.85f, 0.9f)); // Blade
                    }
                }
            }

            // Legs (stable stance)
            for (int y = (int)(attackCy * 1.2f); y <= (int)(attackCy * 1.75f); y++)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    int lx = (int)(attackCx + side * size * 0.1f);
                    if (lx >= 0 && lx < size && y >= 0 && y < size)
                    {
                        attackImg.SetPixel(lx, y, new Color(0.18f, 0.2f, 0.15f));
                        if (lx + 1 < size) attackImg.SetPixel(lx + 1, y, new Color(0.18f, 0.2f, 0.15f));
                    }
                }
            }

            _attackFrames.Add(ImageTexture.CreateFromImage(attackImg));
        }

        if (Player!.Sprite != null && _idleFrame != null)
            Player.Sprite.Texture = _idleFrame;
    }

    public void UpdateWalkAnimation(float delta)
    {
        if (Player?.Sprite == null)
            return;

        // Handle state transitions (attack, hurt, dash)
        if (_stateTimer > 0)
        {
            _stateTimer -= delta;
            if (_stateTimer <= 0)
            {
                _animState = AnimState.Idle;
                Player.Sprite.Texture = _idleFrame ?? (_walkFrames.Count > 0 ? _walkFrames[0] : null);
                Player.Sprite.Scale = new Vector2(1.0f, 1.0f);
            }
            return;
        }

        _isMoving = Player!.Velocity.Length() > 10;

        if (_isMoving && _walkFrames.Count > 0)
        {
            _animState = AnimState.Walk;
            _animTimer += delta;
            float frameInterval = Core.GameConfig.WalkAnimSpeed;
            if (_animTimer >= frameInterval)
            {
                _animTimer -= frameInterval;
                _currentFrame = (_currentFrame + 1) % _walkFrames.Count;
                Player.Sprite.Texture = _walkFrames[_currentFrame];
            }
        }
        else if (!_isMoving)
        {
            _animState = AnimState.Idle;
            // Use idle frames if available, otherwise fall back to walk first frame
            if (_idleFrames.Count > 0)
            {
                _animTimer += delta;
                float frameInterval = Core.GameConfig.IdleBreathSpeed;
                if (_animTimer >= frameInterval)
                {
                    _animTimer -= frameInterval;
                    _currentFrame = (_currentFrame + 1) % _idleFrames.Count;
                    Player.Sprite.Texture = _idleFrames[_currentFrame];
                }
            }
            else if (_currentFrame != 0 && _walkFrames.Count > 0)
            {
                _currentFrame = 0;
                Player.Sprite.Texture = _walkFrames[0];
                _animTimer = 0.0f;
            }
        }

        // Sync outline texture
        _SyncOutlineTexture();

        // Subtle walk brightness pulse
        if (_isMoving)
        {
            float b = 0.95f + Mathf.Abs(Mathf.Sin(_animTimer * 4.0f)) * 0.1f;
            Player.Sprite.SelfModulate = new Color(b, b, b, 1.0f);
        }
        else
        {
            Player.Sprite.SelfModulate = Colors.White;
        }

        // Breathing animation for idle
        if (_animState == AnimState.Idle && _idleFrame != null)
        {
            _animTimer += delta;
            float breathe = Mathf.Sin(_animTimer * Mathf.Tau / Core.GameConfig.IdleBreathSpeed) * 0.02f;
            Player.Sprite.Scale = new Vector2(1.0f + breathe * 0.8f, 1.0f - breathe * 0.8f);
        }

        if (Player!.Velocity.X < -10)
        {
            _facingLeft = true;
            Player.Sprite.FlipH = true;
        }
        else if (Player.Velocity.X > 10)
        {
            _facingLeft = false;
            Player.Sprite.FlipH = false;
        }
    }

    public void SpawnDashTrail(float delta)
    {
        _trailTimer += delta;
        if (_trailTimer < Core.GameConfig.DashTrailInterval)
            return;
        _trailTimer -= Core.GameConfig.DashTrailInterval;
        VFXManager.SpawnGhostTrail(Player!.GetParent(), Player.Sprite!, Player.GlobalPosition, 0.3f);
    }

    public void ResetDashTrail()
    {
        _trailTimer = 0.0f;
    }

    public void ShowAttackEffect(Vector2 targetPos, AttackType attackType)
    {
        PlayAttackAnim();
        Color flashColor = Colors.White;
        bool doFlash = false;
        switch (attackType)
        {
            case AttackType.Melee:
                flashColor = new Color(0.9f, 0.3f, 0.2f, 0.5f);
                _ShowMeleeSlash(targetPos);
                doFlash = true;
                break;
            case AttackType.Ranged:
                flashColor = new Color(0.9f, 0.3f, 0.2f, 0.4f);
                break;
            case AttackType.Area:
                flashColor = new Color(0.9f, 0.3f, 0.2f, 0.5f);
                _ShowAreaRings();
                doFlash = true;
                break;
            case AttackType.Magic:
                flashColor = new Color(0.9f, 0.3f, 0.2f, 0.5f);
                _ShowMagicCircle(targetPos);
                doFlash = true;
                break;
        }
        if (doFlash)
            GameServices.UI.FlashAttackColor(flashColor);
    }

    private void _ShowMeleeSlash(Vector2 targetPos)
    {
        Node? parent = Player!.GetParent();
        Vector2 baseDir = (targetPos - Player.GlobalPosition).Normalized();
        float startAngle = baseDir.Angle() - Mathf.Pi / 6;

        for (int arc = 0; arc < 2; arc++)
        {
            var line = new Line2D();
            float arcR = 55 + arc * 15;
            for (int i = 0; i < 15; i++)
            {
                float a = startAngle + (float)i / 14.0f * (Mathf.Pi / 3);
                line.AddPoint(Player.GlobalPosition + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * arcR);
            }
            line.Width = 6.0f - arc * 2.0f;
            line.DefaultColor = new Color(PencilGray, 0.75f - arc * 0.15f);
            line.JointMode = Line2D.LineJointMode.Round;
            line.BeginCapMode = Line2D.LineCapMode.Round;
            line.EndCapMode = Line2D.LineCapMode.Round;
            parent!.AddChild(line);

            var tw = line.CreateTween();
            tw.TweenProperty(line, "modulate:a", 0.0f, 0.3f);
            tw.TweenCallback(Callable.From(line.QueueFree));
        }

        VFXManager.SpawnImpactRing(parent, targetPos, 25.0f, BloodRed);
        GameServices.UI.ShakeScreen(3.0f);
    }

    private void _ShowAreaRings()
    {
        Node? parent = Player!.GetParent();
        for (int i = 0; i < 3; i++)
        {
            VFXManager.SpawnImpactRing(parent, Player.GlobalPosition, 30.0f + i * 20,
                new Color(1.0f, 0.5f, 0.0f, 0.7f - i * 0.15f), 3.0f);
        }
    }

    private void _ShowMagicCircle(Vector2 targetPos)
    {
        Node? parent = Player!.GetParent();
        var circle = _MakeCircleRing(Player!.GlobalPosition, 50.0f, new Color(BloodRed, 0.7f), 2.0f);
        var targetRing = _MakeCircleRing(targetPos, 35.0f, new Color(BloodBright, 0.7f), 2.0f);
        parent!.AddChild(circle);
        parent.AddChild(targetRing);

        var tw = Player.CreateTween();
        tw.Parallel().TweenProperty(circle, "rotation", Mathf.Tau, 0.5f);
        tw.Parallel().TweenProperty(targetRing, "rotation", -Mathf.Tau, 0.5f);
        tw.Parallel().TweenProperty(circle, "modulate:a", 0.0f, 0.45f);
        tw.Parallel().TweenProperty(targetRing, "modulate:a", 0.0f, 0.45f);
        tw.TweenCallback(Callable.From(circle.QueueFree));
        tw.TweenCallback(Callable.From(targetRing.QueueFree));
    }

    private Node2D _MakeCircleRing(Vector2 pos, float radius, Color color, float width = 2.0f)
    {
        var node = new Node2D { GlobalPosition = pos };
        var line = new Line2D { Width = width, DefaultColor = color };
        const int pts = 24;
        for (int i = 0; i <= pts; i++)
        {
            float a = i * Mathf.Tau / pts;
            line.AddPoint(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
        }
        node.AddChild(line);
        return node;
    }

    public void CreateMuzzleFlash(Vector2 _direction)
    {
        var flash = new Node2D { GlobalPosition = Player!.GlobalPosition };
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.Tau / 6;
            float rayLen = (float)GD.RandRange(15, 35);
            var ray = new Line2D
            {
                Width = (float)GD.RandRange(2, 5),
                DefaultColor = new Color(BloodBright, (float)GD.RandRange(0.6, 1.0))
            };
            ray.AddPoint(Vector2.Zero);
            ray.AddPoint(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * rayLen);
            flash.AddChild(ray);
        }
        Player.GetParent().AddChild(flash);
        var tw = flash.CreateTween();
        tw.TweenProperty(flash, "modulate:a", 0.0f, 0.15f);
        tw.TweenCallback(Callable.From(flash.QueueFree));
    }

    public void ShowUpgradeEffect()
    {
        var flash = new ColorRect();
        flash.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        flash.Color = new Color(BloodRed, 0.3f);
        flash.MouseFilter = Control.MouseFilterEnum.Ignore;
        flash.ZIndex = 50;
        Player!.GetTree().CurrentScene.AddChild(flash);

        var tw = flash.CreateTween();
        tw.TweenProperty(flash, "color:a", 0.0f, 0.4f);
        tw.TweenCallback(Callable.From(flash.QueueFree));

        if (Player.Sprite != null)
        {
            var glow = Player.CreateTween();
            glow.TweenProperty(Player.Sprite, "modulate", new Color(0.9f, 0.3f, 0.2f), 0.2f);
            glow.TweenProperty(Player.Sprite, "modulate", Colors.White, 0.3f);
        }
    }

    private void _CreateStatusDisplays()
    {
        Player!.HealthDisplay = new Label();
        Player.HealthDisplay.AddThemeFontSizeOverride("font_size", 12);
        Player.HealthDisplay.AddThemeColorOverride("font_color", Colors.Red);
        Player.HealthDisplay.Position = new Vector2(-20, -55);
        Player.HealthDisplay.ZIndex = 10;
        Player.AddChild(Player.HealthDisplay);

        Player.AttackTypeDisplay = new Label();
        Player.AttackTypeDisplay.AddThemeFontSizeOverride("font_size", 12);
        Player.AttackTypeDisplay.AddThemeColorOverride("font_color", Colors.Cyan);
        Player.AttackTypeDisplay.Position = new Vector2(-20, -35);
        Player.AttackTypeDisplay.ZIndex = 10;
        Player.AddChild(Player.AttackTypeDisplay);

        UpdateStatusDisplays();
    }

    public void UpdateStatusDisplays()
    {
        if (Player!.HealthDisplay != null)
            Player.HealthDisplay.Text = Player.CurrentHealth + "/" + Player.MaxHealth;

        if (Player.AttackTypeDisplay != null)
        {
            Player.AttackTypeDisplay.Text = Player.CurrentAttackType switch
            {
                AttackType.Melee  => "近战",
                AttackType.Ranged => "远程",
                AttackType.Area   => "范围",
                AttackType.Magic  => "魔法",
                _ => ""
            };
        }
        _UpdateMeleeRangeIndicator();
    }

    private void _CreateMeleeRangeIndicator()
    {
        Player!.MeleeRangeIndicator = new Node2D { ZIndex = -1 };
        Player.AddChild(Player.MeleeRangeIndicator);

        var sprite = new Sprite2D { Name = "RangeSprite" };
        int s = (int)(Core.GameConfig.BaseAttackRange * Player.WeaponRangeModifier * 2);
        sprite.Texture = _MakeRangeTexture(s);
        sprite.Modulate = sprite.Modulate with { A = 0.3f };
        Player.MeleeRangeIndicator.AddChild(sprite);
    }

    private void _UpdateMeleeRangeIndicator()
    {
        if (Player?.MeleeRangeIndicator == null)
            return;

        bool shouldShow = Player.ShowMeleeRange && Player.CurrentAttackType == AttackType.Melee;
        Player.MeleeRangeIndicator.Visible = shouldShow;

        if (shouldShow)
        {
            var rs = Player.MeleeRangeIndicator.GetNodeOrNull("RangeSprite") as Sprite2D;
            if (rs?.Texture != null)
            {
                float expected = Core.GameConfig.BaseAttackRange * Player.WeaponRangeModifier * 2;
                if (Mathf.Abs(rs.Texture.GetSize().X - expected) > 5)
                    rs.Texture = _MakeRangeTexture((int)expected);
            }
        }
    }

    private Texture2D _MakeRangeTexture(int size)
    {
        using var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        int center = size / 2;
        int radius = center - 2;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float dist = new Vector2(x - center, y - center).Length();
                if (dist >= radius - 2 && dist <= radius)
                    img.SetPixel(x, y, new Color(0.3f, 0.8f, 1.0f, 0.6f));
                else if (dist < radius - 2)
                    img.SetPixel(x, y, new Color(0.2f, 0.6f, 0.9f, 0.1f));
            }
        }
        return ImageTexture.CreateFromImage(img);
    }
}
