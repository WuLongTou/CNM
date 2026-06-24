namespace ZombieWars.World;

using Godot;
using ZombieWars.Core;

public partial class ForgeStation : Area2D
{
    // 锻造台 3D mesh（Blender .glb）
    private static readonly Mesh? _mesh =
        ModelLoader.LoadMesh("res://assets/models/props/forge_station.glb");

    private bool _playerNear;
    private Label? _hint;

    public override void _Ready()
    {
        AddToGroup("mm_resource");
        CollisionLayer = 0;
        CollisionMask = GameConfig.CollisionLayerPlayer;
        BodyEntered += _ => _playerNear = true;
        BodyExited += _ => _playerNear = false;

        var col = new CollisionShape2D { Shape = new CircleShape2D { Radius = 44 } };
        AddChild(col);
        ZIndex = 1;

        // 主视觉：MeshInstance2D 渲染 .glb
        if (_mesh != null)
            AddChild(new MeshInstance2D { Mesh = _mesh });

        _hint = new Label { Text = "按 F 锻造", Position = new Vector2(-36, -56), ZIndex = 8 };
        _hint.AddThemeFontSizeOverride("font_size", 14);
        _hint.AddThemeColorOverride("font_color", new Color(1f, 0.7f, 0.3f));
        _hint.AddThemeColorOverride("font_outline_color", Colors.Black);
        _hint.AddThemeConstantOverride("outline_size", 3);
        _hint.Visible = false;
        AddChild(_hint);
    }

    public override void _Input(InputEvent @event)
    {
        if (!_playerNear) return;
        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.F && !GameServices.UI.IsForgeOpen())
        {
            GameServices.UI.OpenForge();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_hint != null)
            _hint.Visible = _playerNear;

        if (_playerNear)
        {
            if (_hint != null)
            {
                double t = Time.GetTicksMsec() / 1000.0;
                _hint.Position = new Vector2(-36, -56 + Mathf.Sin((float)t * 3f) * 3f);
            }
            if (Input.IsKeyPressed(Key.F) && !GameServices.UI.IsForgeOpen())
            {
                GameServices.UI.OpenForge();
            }
        }
        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_playerNear) return;
        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.F && !GameServices.UI.IsForgeOpen())
        {
            GameServices.UI.OpenForge();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Draw()
    {
        // 交互光圈 + 火花（玩家靠近时）—— 静态视觉由 MeshInstance2D 提供
        if (_playerNear)
        {
            double t = Time.GetTicksMsec() / 1000.0;
            // 火花
            float sparkle = 0.05f + 0.10f * Mathf.Sin((float)t * 8f);
            DrawRect(new Rect2(-8, -18, 4, 3), new Color(1f, 0.7f, 0.2f, sparkle), true);
            DrawRect(new Rect2(4, -20, 3, 3), new Color(1f, 0.6f, 0.1f, sparkle * 0.7f), true);
            // 交互光圈
            float pulse = 0.4f + 0.3f * Mathf.Sin((float)t * 4f);
            DrawArc(Vector2.Zero, 40f, 0, Mathf.Tau, 24, new Color(1f, 0.7f, 0.3f, pulse), 2f);
        }
    }
}
