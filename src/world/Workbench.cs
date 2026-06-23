namespace ZombieWars.World;

using Godot;
using ZombieWars.Core;

/// <summary>
/// 工作台（饥荒式合成）：地图固定放置，玩家靠近按 E 打开合成面板。
/// Area2D + CollisionMask=Player 范式（复用 Chest/Altar 交互模式）。
/// </summary>
public partial class Workbench : Area2D
{
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
        // 阴影
        DrawCircle(new Vector2(4, 12), 30, new Color(0, 0, 0, 0.25f));
        // 桌面（厚木板）
        DrawRect(new Rect2(-32, -10, 64, 26), new Color(0.36f, 0.23f, 0.13f), true);
        DrawRect(new Rect2(-32, -10, 64, 4), new Color(0.46f, 0.31f, 0.18f), true);
        DrawRect(new Rect2(-32, -10, 64, 26), new Color(0.20f, 0.12f, 0.06f), false, 2f);
        // 桌腿
        DrawLine(new Vector2(-24, 16), new Vector2(-24, 32), new Color(0.25f, 0.16f, 0.08f), 5f);
        DrawLine(new Vector2(24, 16), new Vector2(24, 32), new Color(0.25f, 0.16f, 0.08f), 5f);
        // 铁砧
        DrawRect(new Rect2(-12, -22, 24, 4), new Color(0.42f, 0.42f, 0.45f), true);
        DrawRect(new Rect2(-9, -18, 18, 12), new Color(0.32f, 0.32f, 0.35f), true);
        DrawRect(new Rect2(-12, -6, 24, 5), new Color(0.38f, 0.38f, 0.40f), true);
        // 锤子（斜放）
        DrawLine(new Vector2(8, -20), new Vector2(20, -32), new Color(0.45f, 0.30f, 0.15f), 3f);
        DrawRect(new Rect2(16, -36, 10, 8), new Color(0.40f, 0.40f, 0.43f), true);
        // 交互光圈（玩家靠近时脉动）
        if (_playerNear)
        {
            double t = Time.GetTicksMsec() / 1000.0;
            float pulse = 0.4f + 0.3f * Mathf.Sin((float)t * 4f);
            DrawArc(Vector2.Zero, 40f, 0, Mathf.Tau, 24, new Color(1f, 0.85f, 0.35f, pulse), 2f);
        }
    }
}
