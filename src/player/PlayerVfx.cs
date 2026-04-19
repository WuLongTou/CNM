namespace ZombieWars.Player;

using Godot;
using ZombieWars.Core;
using ZombieWars.Vfx;
using static ZombieWars.Core.GameEnums;

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
    }

    private void _GenerateStateVariantsDeferred()
    {
        if (_baseTexture == null || Player?.Sprite == null) return;
        var baseImg = _baseTexture.GetImage();
        if (baseImg == null) return;

        // Work on a copy so we don't mutate the original texture
        var img = baseImg.Duplicate() as Image;
        if (img == null) return;

        // Downscale to 128px max for consistent display size across all textures
        int maxDim = 128;
        if (img.GetWidth() > maxDim || img.GetHeight() > maxDim)
        {
            float ratio = Mathf.Min((float)maxDim / img.GetWidth(), (float)maxDim / img.GetHeight());
            img.Resize((int)(img.GetWidth() * ratio), (int)(img.GetHeight() * ratio));
        }

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
        _TintImage(dashImg, new Color(0.5f, 0.8f, 1.3f, 0.7f));
        _dashFrame = ImageTexture.CreateFromImage(dashImg);
    }

    private void _TryLoadPngSprites()
    {
        string playerSprite = "res://assets/sprites/player/player_warrior.jpg";
        string idleSprite = "res://assets/sprites/player/player_idle.jpg";

        // Load walk frames using normalized texture loader
        for (int i = 1; i <= 4; i++)
        {
            string path = $"res://assets/sprites/player/player_walk_{i}.jpg";
            var tex = TextureUtils.LoadNormalizedSprite(path);
            if (tex != null)
                _walkFrames.Add(tex);
        }

        // Load idle frames
        var idleTex = TextureUtils.LoadNormalizedSprite(idleSprite);
        if (idleTex != null)
            _idleFrames.Add(idleTex);

        // Fallback: use player_walk_1 as idle if no dedicated idle sprite
        if (_idleFrames.Count == 0 && _walkFrames.Count > 0)
            _idleFrames.Add(_walkFrames[0]);

        // Load attack frames
        for (int i = 1; i <= 3; i++)
        {
            string path = $"res://assets/sprites/player/player_attack_{i}.jpg";
            var tex = TextureUtils.LoadNormalizedSprite(path);
            if (tex != null)
                _attackFrames.Add(tex);
        }

        // Load base warrior sprite for state variant generation
        _baseTexture = TextureUtils.LoadNormalizedSprite(playerSprite);
        if (_baseTexture != null)
        {
            Player!.Sprite!.Texture = _baseTexture;
            CallDeferred(MethodName._GenerateStateVariantsDeferred);
        }

        // Set idle frame from loaded frames
        if (_idleFrames.Count > 0)
        {
            _idleFrame = _idleFrames[0];
            if (Player!.Sprite != null && Player.Sprite.Texture == null)
                Player.Sprite.Texture = _idleFrame;
        }

        // Set sprite scale to 1.0 for normalized textures
        if (Player?.Sprite != null && (_walkFrames.Count > 0 || _idleFrames.Count > 0))
            Player.Sprite.Scale = new Vector2(1.0f, 1.0f);

        // If no textures loaded, generate fallback sprites
        if (_walkFrames.Count == 0 && _idleFrames.Count == 0 && _baseTexture == null)
            _CreatePlayerSprite();
    }

    private static Texture2D? _LoadWithAlpha(string path)
    {
        var tex = GD.Load<Texture2D>(path);
        if (tex == null) return null;
        var img = tex.GetImage();
        if (img == null) return tex;
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
        using var img = Image.Create(w, h, false, Image.Format.Rgba8);
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
        int size = 40;
        var framesData = new Godot.Collections.Array<Image>();

        for (int frameIdx = 0; frameIdx < 4; frameIdx++)
        {
            using var img = Image.Create(size, size, false, Image.Format.Rgba8);
            float cx = size / 2.0f;
            float cy = size / 2.0f;
            float bob = 0.0f;
            if (frameIdx == 1) bob = -1.5f;
            else if (frameIdx == 3) bob = 1.0f;

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float bodyTop = cy * 0.55f + bob;
                    float bodyBot = cy * 1.5f + bob;
                    if (y >= bodyTop && y <= bodyBot)
                    {
                        float bodyW = size * 0.22f - Mathf.Abs(y - cy * 1.0f - bob) * 0.04f;
                        if (Mathf.Abs(x - cx) < bodyW)
                            img.SetPixel(x, y, new Color(0.25f, 0.30f, 0.22f));
                    }
                }
            }

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float headR = size * 0.18f;
                    float headCy = cy * 0.42f + bob;
                    float hd = new Vector2(x - cx, y - headCy).Length();
                    if (hd < headR)
                    {
                        if (hd > headR * 0.65f)
                            img.SetPixel(x, y, new Color(0.3f, 0.35f, 0.25f));
                        else
                            img.SetPixel(x, y, new Color(0.75f, 0.6f, 0.5f));
                    }
                }
            }

            int eyeY = (int)(cy * 0.38f + bob);
            foreach (int dx in new[] { -1, 1 })
            {
                int ex = (int)(cx + dx * size * 0.06f);
                if (ex >= 0 && ex < size && eyeY >= 0 && eyeY < size)
                    img.SetPixel(ex, eyeY, new Color(0.9f, 0.9f, 0.9f));
            }

            float legOffset = 0.0f;
            if (frameIdx == 1) legOffset = 3.0f;
            else if (frameIdx == 3) legOffset = -3.0f;

            for (int ly = (int)(cy * 1.5f + bob); ly <= (int)(cy * 1.85f + bob); ly++)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    int lx = (int)(cx + side * size * 0.08f + side * legOffset * (side == 1 ? 1 : -1));
                    if (lx >= 0 && lx < size && ly >= 0 && ly < size)
                    {
                        img.SetPixel(lx, ly, new Color(0.2f, 0.22f, 0.18f));
                        if (lx + 1 < size)
                            img.SetPixel(lx + 1, ly, new Color(0.2f, 0.22f, 0.18f));
                    }
                }
            }

            framesData.Add(img);
        }

        _walkFrames.Clear();
        foreach (var img in framesData)
            _walkFrames.Add(ImageTexture.CreateFromImage(img));

        _idleFrame = _walkFrames.Count > 0 ? _walkFrames[0] : null;
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
        else if (_isMoving && _idleFrame != null)
        {
            _animState = AnimState.Walk;
            if (Player.Sprite.Texture != _idleFrame)
                Player.Sprite.Texture = _idleFrame;
        }
        else
        {
            _animState = AnimState.Idle;
            if (_currentFrame != 0 && _walkFrames.Count > 0)
            {
                _currentFrame = 0;
                Player.Sprite.Texture = _walkFrames[0];
            }
            _animTimer = 0.0f;
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
        VFXManager.SpawnGhostTrail(Player!.GetParent(), Player.Sprite, Player.GlobalPosition, new Color(0.5f, 0.8f, 1.0f, 0.5f));
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
                flashColor = new Color(1.0f, 0.6f, 0.2f);
                _ShowMeleeSlash(targetPos);
                doFlash = true;
                break;
            case AttackType.Ranged:
                flashColor = new Color(1.0f, 0.9f, 0.4f);
                break;
            case AttackType.Area:
                flashColor = new Color(1.0f, 0.4f, 0.1f);
                _ShowAreaRings();
                doFlash = true;
                break;
            case AttackType.Magic:
                flashColor = new Color(0.6f, 0.3f, 1.0f);
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
            line.DefaultColor = new Color(1.0f, 0.8f - arc * 0.15f, 0.2f, 0.85f - arc * 0.2f);
            line.JointMode = Line2D.LineJointMode.Round;
            line.BeginCapMode = Line2D.LineCapMode.Round;
            line.EndCapMode = Line2D.LineCapMode.Round;
            parent!.AddChild(line);

            var tw = line.CreateTween();
            tw.TweenProperty(line, "modulate:a", 0.0f, 0.3f);
            tw.TweenCallback(Callable.From(line.QueueFree));
        }

        VFXManager.SpawnImpactRing(parent, targetPos, 25.0f, Colors.Orange);
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
        var circle = _MakeCircleRing(Player!.GlobalPosition, 50.0f, Colors.Magenta, 2.0f);
        var targetRing = _MakeCircleRing(targetPos, 35.0f, new Color(1.0f, 0.5f, 1.0f, 0.8f), 2.0f);
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
                DefaultColor = new Color(1.0f, 0.9f, 0.5f, (float)GD.RandRange(0.6, 1.0))
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
        flash.Color = new Color(1.0f, 0.9f, 0.3f, 0.3f);
        flash.MouseFilter = Control.MouseFilterEnum.Ignore;
        flash.ZIndex = 50;
        Player!.GetTree().CurrentScene.AddChild(flash);

        var tw = flash.CreateTween();
        tw.TweenProperty(flash, "color:a", 0.0f, 0.4f);
        tw.TweenCallback(Callable.From(flash.QueueFree));

        if (Player.Sprite != null)
        {
            var glow = Player.CreateTween();
            glow.TweenProperty(Player.Sprite, "modulate", new Color(1.5f, 1.5f, 1.0f), 0.2f);
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
        using var img = Image.Create(size, size, false, Image.Format.Rgba8);
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
