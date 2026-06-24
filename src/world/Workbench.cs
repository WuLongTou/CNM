namespace ZombieWars.World;

using Godot;
using ZombieWars.Core;

/// <summary>
/// 工作台（饥荒式合成）：地图固定放置，玩家靠近按 E 打开合成面板。
/// Area2D + CollisionMask=Player 范式（复用 Chest/Altar 交互模式）。
///
/// 视觉：Blender 3D 模型（.glb）经 MeshInstance2D 渲染。
/// 保留 _Draw 画玩家靠近时的交互光圈脉动。
/// </summary>
public partial class Workbench : Area2D
{
    // 工作台 3D mesh（Blender .glb）
    private static readonly Mesh? _mesh =
        ModelLoader.LoadMesh("res://assets/models/props/workbench.glb");

    private bool _playerNear;
    private Label? _hint;

    public override void _Ready()
    {
        AddToGroup("mm_resource"); // 小地图标记
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

        _hint = new Label { Text = "按 E 合成", Position = new Vector2(-36, -56), ZIndex = 8 };
        _hint.AddThemeFontSizeOverride("font_size", 14);
        _hint.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.4f));
        _hint.AddThemeColorOverride("font_outline_color", Colors.Black);
        _hint.AddThemeConstantOverride("outline_size", 3);
        _hint.Visible = false;
        AddChild(_hint);
    }

    public override void _Input(InputEvent @event)
    {
        if (!_playerNear) return;
        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.E && !GameServices.UI.IsCraftingOpen())
        {
            GameServices.UI.OpenCrafting();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_playerNear && _hint != null)
        {
            // 提示文字轻微浮动
            double t = Time.GetTicksMsec() / 1000.0;
            _hint.Position = new Vector2(-36, -56 + Mathf.Sin((float)t * 3f) * 3f);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        // 交互光圈（玩家靠近时脉动）—— 静态视觉由 MeshInstance2D 提供
        if (_playerNear)
        {
            double t = Time.GetTicksMsec() / 1000.0;
            float pulse = 0.4f + 0.3f * Mathf.Sin((float)t * 4f);
            DrawArc(Vector2.Zero, 40f, 0, Mathf.Tau, 24, new Color(1f, 0.85f, 0.35f, pulse), 2f);
        }
    }
}
