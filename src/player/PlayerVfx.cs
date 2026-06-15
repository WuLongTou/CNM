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
    private float _breatheTimer = 0.0f;
    private AttackType _visualAttackType = AttackType.Melee;

    /// <summary>
    /// 当前角色朝向（true=朝左，false=朝右）。
    /// 这是唯一事实源——任何依赖朝向做决策的代码（攻击方向/特效生成点等）
    /// 都应该读这个属性，不要直接读 Sprite.FlipH 或 Scale.x。
    /// </summary>
    public bool FacingLeft => _facingLeft;

    private float _trailTimer = 0.0f;
    private float _footstepTimer = 0.0f;
    private const float FootstepInterval = 0.28f;

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
            _CreatePlayerSprite(AttackType.Melee);
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
        Player.Sprite.Scale = Vector2.One;
        Player.Sprite.FlipH = _facingLeft;

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
        _LoadWarriorSprites();
        _LoadRangerSprites();
        _LoadMageSprites();
        // 所有精灵加载完成后再标记，避免加载失败时误判
        _allSpritesLoaded = true;
    }

    /// <summary>
    /// 从投影分割返回的帧列表中按等距分布取 N 帧，
    /// 均匀覆盖整个动画序列（投影分割帧顺序可能不固定）。
    /// </summary>
    private static void _TakeFramesEvenly(
        Godot.Collections.Array<Texture2D> src,
        Godot.Collections.Array<Texture2D> dst,
        int want)
    {
        dst.Clear();
        if (src == null || src.Count == 0 || want <= 0) return;
        int total = src.Count;
        if (total <= want)
        {
            for (int i = 0; i < total; i++) dst.Add(src[i]);
            return;
        }
        // 等距取样：保证首尾都覆盖
        for (int i = 0; i < want; i++)
        {
            int idx = (int)System.Math.Round((double)i * (total - 1) / (want - 1));
            if (idx >= total) idx = total - 1;
            dst.Add(src[idx]);
        }
    }

    private static void _LoadWarriorSprites()
    {
        _warriorWalkFrames.Clear();
        _warriorAttackFrames.Clear();
        _warriorIdleFrames.Clear();

        var allWalk = Core.TextureUtils.LoadAndSplitSpriteSheet("res://assets/sprites/player/sheets/warrior_walk.png");
        _TakeFramesEvenly(allWalk, _warriorWalkFrames, 8);

        var allAttack = Core.TextureUtils.LoadAndSplitSpriteSheet("res://assets/sprites/player/sheets/warrior_attack.png");
        _TakeFramesEvenly(allAttack, _warriorAttackFrames, 6);

        var allIdle = Core.TextureUtils.LoadSpriteSheetGridFrames("res://assets/sprites/player/sheets/warrior_idle.png", cols: 2, rows: 2);
        _TakeFramesEvenly(allIdle, _warriorIdleFrames, 4);

        _warriorIdle = _warriorIdleFrames.Count > 0 ? _warriorIdleFrames[0] : null;
        _warriorBase = _warriorWalkFrames.Count > 0 ? _warriorWalkFrames[0] : null;
    }

    private static void _LoadRangerSprites()
    {
        _rangerWalkFrames.Clear();
        _rangerAttackFrames.Clear();
        _rangerIdleFrames.Clear();

        var allWalk = Core.TextureUtils.LoadAndSplitSpriteSheet("res://assets/sprites/player/sheets/ranger_walk.png");
        _TakeFramesEvenly(allWalk, _rangerWalkFrames, 8);

        var allAttack = Core.TextureUtils.LoadAndSplitSpriteSheet("res://assets/sprites/player/sheets/ranger_attack.png");
        _TakeFramesEvenly(allAttack, _rangerAttackFrames, 6);

        var allIdle = Core.TextureUtils.LoadSpriteSheetGridFrames("res://assets/sprites/player/sheets/ranger_idle.png", cols: 2, rows: 2);
        _TakeFramesEvenly(allIdle, _rangerIdleFrames, 4);

        _rangerIdle = _rangerIdleFrames.Count > 0 ? _rangerIdleFrames[0] : null;
        _rangerBase = _rangerWalkFrames.Count > 0 ? _rangerWalkFrames[0] : null;
    }

    private static void _LoadMageSprites()
    {
        _mageWalkFrames.Clear();
        _mageAttackFrames.Clear();
        _mageIdleFrames.Clear();

        var allWalk = Core.TextureUtils.LoadAndSplitSpriteSheet("res://assets/sprites/player/sheets/mage_walk.png");
        _TakeFramesEvenly(allWalk, _mageWalkFrames, 8);

        var allAttack = Core.TextureUtils.LoadAndSplitSpriteSheet("res://assets/sprites/player/sheets/mage_attack.png");
        _TakeFramesEvenly(allAttack, _mageAttackFrames, 6);

        var allIdle = Core.TextureUtils.LoadSpriteSheetGridFrames("res://assets/sprites/player/sheets/mage_idle.png", cols: 2, rows: 2);
        _TakeFramesEvenly(allIdle, _mageIdleFrames, 4);

        _mageIdle = _mageIdleFrames.Count > 0 ? _mageIdleFrames[0] : null;
        _mageBase = _mageWalkFrames.Count > 0 ? _mageWalkFrames[0] : null;
    }

    /// Switch active sprites based on current weapon type. Call when weapon changes.
    public void SwitchWeaponSprites(AttackType attackType)
    {
        _visualAttackType = attackType;
        if (attackType == _lastAttackType) return;
        _lastAttackType = attackType;
        _ApplyWeaponSprites();
        UpdateStatusDisplays();
        GameServices.UI.UpdatePlayerStatus(Player!);
    }

    private void _ApplyWeaponSprites()
    {
        _walkFrames.Clear();
        _attackFrames.Clear();
        _idleFrames.Clear();
        _idleFrame = null;
        _baseTexture = null;

        // 优先使用 _ReloadStaticSprites 加载的 PNG sprite（精确拼图后的 spritesheet）
        if (_warriorIdleFrames.Count > 0 || _rangerIdleFrames.Count > 0 || _mageIdleFrames.Count > 0)
        {
            Godot.Collections.Array<Texture2D> walkSrc, atkSrc, idleSrc;
            Texture2D? idleFirstSrc, baseSrc;
            switch (_lastAttackType)
            {
                case AttackType.Ranged:
                    walkSrc = _rangerWalkFrames; atkSrc = _rangerAttackFrames; idleSrc = _rangerIdleFrames;
                    idleFirstSrc = _rangerIdle; baseSrc = _rangerBase; break;
                case AttackType.Magic:
                case AttackType.Area:
                    walkSrc = _mageWalkFrames; atkSrc = _mageAttackFrames; idleSrc = _mageIdleFrames;
                    idleFirstSrc = _mageIdle; baseSrc = _mageBase; break;
                default:
                    walkSrc = _warriorWalkFrames; atkSrc = _warriorAttackFrames; idleSrc = _warriorIdleFrames;
                    idleFirstSrc = _warriorIdle; baseSrc = _warriorBase; break;
            }
            foreach (var f in walkSrc) _walkFrames.Add(f);
            foreach (var f in atkSrc) _attackFrames.Add(f);
            foreach (var f in idleSrc) _idleFrames.Add(f);
            _idleFrame = idleFirstSrc;
            _baseTexture = baseSrc;
        }
        else
        {
            // 兜底：程序生成 chibi 角色
            _CreatePlayerSprite(_lastAttackType);
        }

        if (_idleFrame != null && Player?.Sprite != null)
            Player.Sprite.Texture = _idleFrame;
        else if (_walkFrames.Count > 0 && Player?.Sprite != null)
            Player.Sprite.Texture = _walkFrames[0];

        if (Player?.Sprite != null)
        {
            Player.Sprite.Scale = Vector2.One;
            Player.Sprite.FlipH = _facingLeft;
        }

        if (_walkFrames.Count > 0 && Player?.Sprite != null)
            CallDeferred(MethodName._GenerateStateVariantsDeferred);
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
    public void PlayDashAnim() { _animState = AnimState.Dash; _stateTimer = 0.15f; GameServices.Audio.PlayDash(); _ApplyStateFrame(); }

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
            Position = new Vector2(6, 18)
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

    // Color palette per class type
    private readonly record struct StickPalette(
        Color Body, Color BodyDark, Color Head, Color Hair,
        Color Limb, Color Weapon, Color WeaponAccent, Color Eye);

    private static readonly StickPalette MeleePalette = new(
        Body: new Color(0.45f, 0.38f, 0.30f), // brown leather armor
        BodyDark: new Color(0.30f, 0.25f, 0.20f),
        Head: new Color(0.78f, 0.62f, 0.52f),
        Hair: new Color(0.30f, 0.22f, 0.12f),  // dark brown hair
        Limb: new Color(0.35f, 0.30f, 0.25f),
        Weapon: new Color(0.85f, 0.85f, 0.90f),  // silver blade
        WeaponAccent: new Color(0.60f, 0.50f, 0.30f), // wooden handle
        Eye: new Color(0.10f, 0.10f, 0.10f)
    );

    private static readonly StickPalette RangerPalette = new(
        Body: new Color(0.25f, 0.40f, 0.25f), // forest green tunic
        BodyDark: new Color(0.18f, 0.30f, 0.18f),
        Head: new Color(0.78f, 0.62f, 0.52f),
        Hair: new Color(0.55f, 0.40f, 0.20f),  // auburn
        Limb: new Color(0.22f, 0.32f, 0.22f),
        Weapon: new Color(0.65f, 0.45f, 0.25f),  // wooden bow
        WeaponAccent: new Color(0.90f, 0.90f, 0.85f), // arrow/string
        Eye: new Color(0.10f, 0.10f, 0.10f)
    );

    private static readonly StickPalette MagicPalette = new(
        Body: new Color(0.35f, 0.25f, 0.55f), // purple robe
        BodyDark: new Color(0.25f, 0.18f, 0.40f),
        Head: new Color(0.78f, 0.62f, 0.52f),
        Hair: new Color(0.65f, 0.60f, 0.55f),  // silver-white
        Limb: new Color(0.30f, 0.22f, 0.45f),
        Weapon: new Color(0.50f, 0.35f, 0.70f),  // magical staff
        WeaponAccent: new Color(0.70f, 0.50f, 1.0f), // glowing orb
        Eye: new Color(0.40f, 0.25f, 0.70f)   // purple glowing eyes
    );

    private void _CreatePlayerSprite(AttackType type)
    {
        var p = type switch
        {
            AttackType.Ranged => RangerPalette,
            AttackType.Magic or AttackType.Area => MagicPalette,
            _ => MeleePalette
        };

        int size = 48;
        float cx = size / 2.0f;
        float cy = size / 2.0f;

        _walkFrames.Clear();
        _idleFrames.Clear();
        _attackFrames.Clear();

        // --- Walk frames (16) ---
        for (int fi = 0; fi < 16; fi++)
        {
            using var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            float phase = fi / 16.0f;
            float bob = -Mathf.Abs(Mathf.Sin(phase * Mathf.Tau)) * 2.0f;
            float legSwing = Mathf.Sin(phase * Mathf.Tau) * 4.0f;
            float armSwing = Mathf.Sin(phase * Mathf.Tau) * 3.0f;

            _DrawShadow(img, cx, size);
            _DrawBody(img, cx, cy, bob, p);
            _DrawHead(img, cx, cy, bob, p, type);
            _DrawEyes(img, cx, cy, bob, p);
            _DrawArms(img, cx, cy, bob, armSwing, p);
            _DrawLegs(img, cx, cy, bob, legSwing, p);
            _DrawClassAccessory(img, cx, cy, bob, type, p);
            _walkFrames.Add(ImageTexture.CreateFromImage(img));
        }

        // --- Idle frames (4, breathing) ---
        for (int fi = 0; fi < 4; fi++)
        {
            using var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            float breathBob = Mathf.Sin(fi / 4.0f * Mathf.Tau) * 0.8f;

            _DrawShadow(img, cx, size);
            _DrawBody(img, cx, cy, breathBob, p);
            _DrawHead(img, cx, cy, breathBob, p, type);
            _DrawEyes(img, cx, cy, breathBob, p);
            _DrawArms(img, cx, cy, breathBob, 0, p);
            _DrawLegs(img, cx, cy, breathBob, 0, p);
            _DrawClassAccessory(img, cx, cy, breathBob, type, p);
            _idleFrames.Add(ImageTexture.CreateFromImage(img));
        }
        _idleFrame = _idleFrames.Count > 0 ? _idleFrames[0] : null;

        // --- Attack frames (8) ---
        for (int fi = 0; fi < 8; fi++)
        {
            using var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            float atkPhase = fi / 8.0f;
            float lean = Mathf.Sin(atkPhase * Mathf.Pi) * 1.5f;

            _DrawShadow(img, cx, size);
            _DrawBodyLean(img, cx, cy, lean, p);
            _DrawHeadLean(img, cx, cy, lean, p, type);
            _DrawEyesLean(img, cx, cy, lean, p);
            _DrawLegsStable(img, cx, cy, p);
            _DrawWeaponAttack(img, cx, cy, atkPhase, type, p);
            _attackFrames.Add(ImageTexture.CreateFromImage(img));
        }

        if (Player!.Sprite != null && _idleFrame != null)
            Player.Sprite.Texture = _idleFrame;
    }

    // --- Common drawing primitives ---

    private static void _DrawShadow(Image img, float cx, int size)
    {
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                if (Mathf.Abs(y - size * 0.88f) < 3 && Mathf.Abs(x - cx) < 10)
                    img.SetPixel(x, y, new Color(0, 0, 0, 0.15f));
    }

    private static void _DrawBody(Image img, float cx, float cy, float bob, StickPalette p)
    {
        float top = cy * 0.48f + bob, bot = cy * 1.35f + bob;
        int w = img.GetWidth();
        for (int x = 0; x < w; x++)
            for (int y = 0; y < img.GetHeight(); y++)
                if (y >= top && y <= bot)
                {
                    float bw = w * 0.18f - Mathf.Abs(y - cy - bob) * 0.03f;
                    if (Mathf.Abs(x - cx) < bw)
                        img.SetPixel(x, y, y < top + (bot - top) * 0.3f ? p.Body : p.BodyDark);
                }
    }

    private static void _DrawBodyLean(Image img, float cx, float cy, float lean, StickPalette p)
    {
        int w = img.GetWidth();
        float top = cy * 0.48f, bot = cy * 1.35f;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < img.GetHeight(); y++)
                if (y >= top && y <= bot)
                {
                    float lo = lean * (y - cy * 0.5f) / (w * 0.5f);
                    float bw = w * 0.18f - Mathf.Abs(y - cy) * 0.03f;
                    if (Mathf.Abs(x - cx - lo) < bw)
                        img.SetPixel(x, y, y < top + (bot - top) * 0.3f ? p.Body : p.BodyDark);
                }
    }

    private static void _DrawHead(Image img, float cx, float cy, float bob, StickPalette p, AttackType type)
    {
        float headR = 48 * 0.16f;
        float headCy = cy * 0.38f + bob;
        for (int x = 0; x < img.GetWidth(); x++)
            for (int y = 0; y < img.GetHeight(); y++)
            {
                float hd = new Vector2(x - cx, y - headCy).Length();
                if (hd < headR)
                    img.SetPixel(x, y, hd > headR * 0.65f ? p.Hair : p.Head);
            }

        // Mage: pointed wizard hat
        if (type == AttackType.Magic || type == AttackType.Area)
        {
            float hatBaseY = headCy - headR * 0.5f;
            float hatTipY = hatBaseY - headR * 1.8f;
            for (int y = (int)hatTipY; y <= (int)hatBaseY; y++)
            {
                float t = (hatBaseY - y) / (hatBaseY - hatTipY);
                float halfW = headR * 0.7f * (1.0f - t * 0.85f);
                for (int x = 0; x < img.GetWidth(); x++)
                    if (Mathf.Abs(x - cx) < halfW && y >= 0 && y < img.GetHeight())
                        img.SetPixel(x, y, new Color(p.BodyDark.R * 0.7f, p.BodyDark.G * 0.7f, p.BodyDark.B * 0.7f));
            }
            // Hat brim
            int brimY = (int)hatBaseY;
            if (brimY >= 0 && brimY < img.GetHeight())
                for (int x = 0; x < img.GetWidth(); x++)
                    if (Mathf.Abs(x - cx) < headR * 0.9f)
                        img.SetPixel(x, brimY, new Color(p.BodyDark.R * 0.8f, p.BodyDark.G * 0.8f, p.BodyDark.B * 0.8f));
        }

        // Ranger: hood
        if (type == AttackType.Ranged)
        {
            float hoodCy = headCy - headR * 0.15f;
            float hoodR = headR * 1.25f;
            for (int x = 0; x < img.GetWidth(); x++)
                for (int y = 0; y < img.GetHeight(); y++)
                {
                    float d = new Vector2(x - cx, y - hoodCy).Length();
                    if (d < hoodR && d > headR * 0.7f && y < headCy + headR * 0.2f)
                        img.SetPixel(x, y, p.Body);
                }
        }

        // Melee: small helmet crest
        if (type == AttackType.Melee || type == AttackType.Mine)
        {
            int crestY = (int)(headCy - headR * 0.9f);
            for (int dx = -1; dx <= 1; dx++)
            {
                int px = (int)(cx + dx);
                if (px >= 0 && px < img.GetWidth() && crestY >= 0 && crestY < img.GetHeight())
                    img.SetPixel(px, crestY, new Color(0.75f, 0.65f, 0.25f)); // gold accent
            }
        }
    }

    private static void _DrawHeadLean(Image img, float cx, float cy, float lean, StickPalette p, AttackType type)
    {
        float headR = 48 * 0.16f;
        float headCy = cy * 0.38f;
        for (int x = 0; x < img.GetWidth(); x++)
            for (int y = 0; y < img.GetHeight(); y++)
            {
                float lo = lean * (y - cy * 0.3f) / (48 * 0.3f);
                float hd = new Vector2(x - cx - lo, y - headCy).Length();
                if (hd < headR)
                    img.SetPixel(x, y, hd > headR * 0.65f ? p.Hair : p.Head);
            }

        if (type == AttackType.Magic || type == AttackType.Area)
        {
            float hatBaseY = headCy - headR * 0.5f;
            float hatTipY = hatBaseY - headR * 1.8f;
            for (int y = (int)hatTipY; y <= (int)hatBaseY; y++)
            {
                float t = (hatBaseY - y) / (hatBaseY - hatTipY);
                float halfW = headR * 0.7f * (1.0f - t * 0.85f);
                for (int x = 0; x < img.GetWidth(); x++)
                    if (Mathf.Abs(x - cx) < halfW && y >= 0 && y < img.GetHeight())
                        img.SetPixel(x, y, new Color(p.BodyDark.R * 0.7f, p.BodyDark.G * 0.7f, p.BodyDark.B * 0.7f));
            }
        }
    }

    private static void _DrawEyes(Image img, float cx, float cy, float bob, StickPalette p)
    {
        int eyeY = (int)(cy * 0.34f + bob);
        foreach (int dx in new[] { -1, 1 })
        {
            int ex = (int)(cx + dx * 48 * 0.055f);
            if (ex >= 0 && ex < img.GetWidth() && eyeY >= 0 && eyeY < img.GetHeight())
            {
                img.SetPixel(ex, eyeY, p.Eye);
                if (eyeY - 1 >= 0) img.SetPixel(ex, eyeY - 1, new Color(0.95f, 0.95f, 0.95f));
            }
        }
    }

    private static void _DrawEyesLean(Image img, float cx, float cy, float lean, StickPalette p)
    {
        int eyeY = (int)(cy * 0.34f);
        foreach (int dx in new[] { -1, 1 })
        {
            int ex = (int)(cx + dx * 48 * 0.055f + lean);
            if (ex >= 0 && ex < img.GetWidth() && eyeY >= 0 && eyeY < img.GetHeight())
            {
                img.SetPixel(ex, eyeY, p.Eye);
                if (eyeY - 1 >= 0) img.SetPixel(ex, eyeY - 1, new Color(0.95f, 0.95f, 0.95f));
            }
        }
    }

    private static void _DrawArms(Image img, float cx, float cy, float bob, float armSwing, StickPalette p)
    {
        float top = cy * 0.5f + bob, bot = cy * 1.1f + bob;
        for (int x = 0; x < img.GetWidth(); x++)
            for (int y = 0; y < img.GetHeight(); y++)
                if (y >= top && y <= bot)
                    foreach (int side in new[] { -1, 1 })
                    {
                        float armX = cx + side * (48 * 0.22f + side * armSwing);
                        if (Mathf.Abs(x - armX) < 2.5f)
                            img.SetPixel(x, y, p.Limb);
                    }
    }

    private static void _DrawLegs(Image img, float cx, float cy, float bob, float legSwing, StickPalette p)
    {
        for (int y = (int)(cy * 1.2f + bob); y <= (int)(cy * 1.75f + bob); y++)
            foreach (int side in new[] { -1, 1 })
            {
                int lx = (int)(cx + side * 48 * 0.08f + side * legSwing);
                if (lx >= 0 && lx < img.GetWidth() && y >= 0 && y < img.GetHeight())
                {
                    img.SetPixel(lx, y, p.Limb);
                    if (lx + 1 < img.GetWidth()) img.SetPixel(lx + 1, y, p.Limb);
                }
            }
    }

    private static void _DrawLegsStable(Image img, float cx, float cy, StickPalette p)
    {
        for (int y = (int)(cy * 1.2f); y <= (int)(cy * 1.75f); y++)
            foreach (int side in new[] { -1, 1 })
            {
                int lx = (int)(cx + side * 48 * 0.1f);
                if (lx >= 0 && lx < img.GetWidth() && y >= 0 && y < img.GetHeight())
                {
                    img.SetPixel(lx, y, p.Limb);
                    if (lx + 1 < img.GetWidth()) img.SetPixel(lx + 1, y, p.Limb);
                }
            }
    }

    /// Class-specific accessory on walk/idle frames (weapon on back, quiver, staff, etc.)
    private static void _DrawClassAccessory(Image img, float cx, float cy, float bob, AttackType type, StickPalette p)
    {
        if (type == AttackType.Ranged)
        {
            // Quiver on back (left side)
            float qX = cx - 48 * 0.28f;
            for (int y = (int)(cy * 0.45f + bob); y <= (int)(cy * 0.95f + bob); y++)
            {
                if (y >= 0 && y < img.GetHeight())
                {
                    int px = (int)qX;
                    if (px >= 0 && px < img.GetWidth()) img.SetPixel(px, y, new Color(0.55f, 0.40f, 0.25f));
                    if (px + 1 < img.GetWidth()) img.SetPixel(px + 1, y, new Color(0.55f, 0.40f, 0.25f));
                }
            }
            // Arrow tips
            for (int i = 0; i < 2; i++)
            {
                int tipY = (int)(cy * 0.42f + bob) - i * 3;
                if (tipY >= 0 && tipY < img.GetHeight())
                {
                    int px = (int)qX;
                    if (px >= 0 && px < img.GetWidth())
                        img.SetPixel(px, tipY, new Color(0.85f, 0.85f, 0.80f));
                }
            }
        }

        if (type == AttackType.Magic || type == AttackType.Area)
        {
            // Glowing orb floating near right hand
            float orbX = cx + 48 * 0.32f;
            float orbY = cy * 0.75f + bob;
            for (int x = 0; x < img.GetWidth(); x++)
                for (int y = 0; y < img.GetHeight(); y++)
                {
                    float d = new Vector2(x - orbX, y - orbY).Length();
                    if (d < 3.0f)
                        img.SetPixel(x, y, d < 1.5f ? p.WeaponAccent : new Color(p.WeaponAccent.R, p.WeaponAccent.G, p.WeaponAccent.B, 0.6f));
                }
        }

        if (type == AttackType.Melee || type == AttackType.Mine)
        {
            // Sword hilt visible at hip
            float hiltX = cx + 48 * 0.18f;
            for (int y = (int)(cy * 0.8f + bob); y <= (int)(cy * 1.1f + bob); y++)
            {
                if (y >= 0 && y < img.GetHeight())
                {
                    int px = (int)hiltX;
                    if (px >= 0 && px < img.GetWidth())
                        img.SetPixel(px, y, p.WeaponAccent);
                }
            }
            // Crossguard
            int guardY = (int)(cy * 0.78f + bob);
            if (guardY >= 0 && guardY < img.GetHeight())
                for (int dx = -1; dx <= 1; dx++)
                {
                    int px = (int)hiltX + dx;
                    if (px >= 0 && px < img.GetWidth())
                        img.SetPixel(px, guardY, new Color(0.75f, 0.65f, 0.25f));
                }
        }
    }

    /// Draw weapon animation during attack frames, class-specific.
    private static void _DrawWeaponAttack(Image img, float cx, float cy, float phase, AttackType type, StickPalette p)
    {
        switch (type)
        {
            case AttackType.Ranged:
                _DrawBowAttack(img, cx, cy, phase, p);
                break;
            case AttackType.Magic:
            case AttackType.Area:
                _DrawStaffAttack(img, cx, cy, phase, p);
                break;
            default:
                _DrawSwordAttack(img, cx, cy, phase, p);
                break;
        }
    }

    private static void _DrawSwordAttack(Image img, float cx, float cy, float phase, StickPalette p)
    {
        float angle = Mathf.Lerp(-0.8f, 0.8f, phase);
        float ox = cx + 48 * 0.2f;
        float oy = cy * 0.6f;
        float len = 48 * 0.45f;
        float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
        for (int x = 0; x < img.GetWidth(); x++)
            for (int y = 0; y < img.GetHeight(); y++)
            {
                float dx = x - ox, dy = y - oy;
                float rx = dx * cos - dy * sin, ry = dx * sin + dy * cos;
                if (rx >= 0 && rx <= len && Mathf.Abs(ry) < 2.5f)
                    img.SetPixel(x, y, rx / len < 0.15f ? p.WeaponAccent : p.Weapon);
            }
    }

    private static void _DrawBowAttack(Image img, float cx, float cy, float phase, StickPalette p)
    {
        float drawBack = Mathf.Sin(phase * Mathf.Pi) * 6.0f;
        float bowX = cx + 48 * 0.22f;
        float bowTop = cy * 0.3f;
        float bowBot = cy * 1.0f;

        // Bow arc
        for (int y = (int)bowTop; y <= (int)bowBot; y++)
        {
            float t = (float)(y - bowTop) / (bowBot - bowTop);
            float curve = Mathf.Sin(t * Mathf.Pi) * 5.0f;
            int px = (int)(bowX + curve);
            if (px >= 0 && px < img.GetWidth() && y >= 0 && y < img.GetHeight())
            {
                img.SetPixel(px, y, p.Weapon);
                if (px + 1 < img.GetWidth()) img.SetPixel(px + 1, y, p.Weapon);
            }
        }

        // Bowstring
        float stringX = bowX - drawBack;
        for (int y = (int)bowTop; y <= (int)bowBot; y++)
        {
            int px = (int)(stringX);
            if (px >= 0 && px < img.GetWidth() && y >= 0 && y < img.GetHeight())
                img.SetPixel(px, y, p.WeaponAccent);
        }

        // Arrow (appears mid-animation)
        if (phase > 0.2f && phase < 0.7f)
        {
            float arrowX = stringX + 2;
            float arrowY = (bowTop + bowBot) / 2;
            for (int i = 0; i < 8; i++)
            {
                int px = (int)(arrowX + i);
                if (px >= 0 && px < img.GetWidth() && (int)arrowY >= 0 && (int)arrowY < img.GetHeight())
                    img.SetPixel(px, (int)arrowY, p.WeaponAccent);
            }
        }
    }

    private static void _DrawStaffAttack(Image img, float cx, float cy, float phase, StickPalette p)
    {
        float angle = Mathf.Lerp(-0.5f, 0.5f, phase);
        float ox = cx + 48 * 0.15f;
        float oy = cy * 0.3f;
        float len = 48 * 0.5f;
        float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);

        // Staff body
        for (int x = 0; x < img.GetWidth(); x++)
            for (int y = 0; y < img.GetHeight(); y++)
            {
                float dx = x - ox, dy = y - oy;
                float rx = dx * cos - dy * sin, ry = dx * sin + dy * cos;
                if (rx >= 0 && rx <= len && Mathf.Abs(ry) < 2.0f)
                    img.SetPixel(x, y, p.Weapon);
            }

        // Glowing orb at tip, pulsing with attack phase
        float glow = 0.5f + Mathf.Sin(phase * Mathf.Pi) * 0.5f;
        float tipX = ox + cos * len;
        float tipY = oy + sin * len;
        for (int x = 0; x < img.GetWidth(); x++)
            for (int y = 0; y < img.GetHeight(); y++)
            {
                float d = new Vector2(x - tipX, y - tipY).Length();
                if (d < 5.0f * glow + 2.0f)
                {
                    float intensity = 1.0f - d / (5.0f * glow + 2.0f);
                    var c = p.WeaponAccent;
                    img.SetPixel(x, y, new Color(c.R, c.G, c.B, intensity * 0.9f));
                }
            }
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
                // 朝向锁定为 _facingLeft 状态，避免 idle 时重置朝向
                Player.Sprite.Scale = Vector2.One;
                Player.Sprite.FlipH = _facingLeft;
            }
            _ApplyWeaponVisualTint();
            return;
        }

        _isMoving = Player!.Velocity.Length() > 10;

        if (_isMoving)
        {
            _footstepTimer += delta;
            if (_footstepTimer >= FootstepInterval)
            {
                _footstepTimer -= FootstepInterval; // Use modulo-style to avoid burst on resume
                GameServices.Audio.PlayFootstep();
            }
        }
        else
        {
            _footstepTimer = 0.0f;
        }

        if (_isMoving && _walkFrames.Count > 0)
        {
            _animState = AnimState.Walk;
            _breatheTimer = 0.0f;
            Player.Sprite.Scale = Vector2.One;
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

        // Breathing animation for idle（朝向只由 FlipH 控制，避免和负 scale 双重翻转）
        if (_animState == AnimState.Idle && _idleFrame != null)
        {
            _breatheTimer += delta;
            float breathe = Mathf.Sin(_breatheTimer * Mathf.Tau / Core.GameConfig.IdleBreathSpeed) * 0.02f;
            Player.Sprite.Scale = new Vector2(
                1.0f + breathe * 0.8f,
                1.0f - breathe * 0.8f
            );
            Player.Sprite.FlipH = _facingLeft;
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

        _ApplyWeaponVisualTint();
    }

    private void _ApplyWeaponVisualTint()
    {
        if (Player?.Sprite == null)
            return;

        Player.Sprite.Modulate = _visualAttackType switch
        {
            AttackType.Ranged => new Color(0.92f, 1.0f, 0.96f),
            AttackType.Magic or AttackType.Area => new Color(0.94f, 0.92f, 1.0f),
            _ => Colors.White
        };
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
        switch (attackType)
        {
            case AttackType.Melee:
                _ShowMeleeSlash(targetPos);
                break;
            case AttackType.Ranged:
                break;
            case AttackType.Area:
                _ShowAreaRings();
                break;
            case AttackType.Magic:
                _ShowMagicCircle(targetPos);
                break;
        }
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
                AttackType.Melee => "近战",
                AttackType.Ranged => "远程",
                AttackType.Area => "范围",
                AttackType.Magic => "魔法",
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
