namespace ZombieWars.UI;

using Godot;
using ZombieWars.Core;
using ZombieWars.Enemy;
using ZombieWars.Player;
using static ZombieWars.Core.GameEnums;

/// <summary>
/// 小地图（右下角）：缩略显示整个大世界，标注玩家/敌人/宝箱/祭坛位置。
/// 大场景导航核心——玩家能看到全局，产生"去远处那个金点看看"的探索动机。
/// </summary>
public partial class MiniMap : CanvasLayer
{
    private const float MapSize = 180f;
    private Player? _player;
    private readonly MapDrawer _drawer = new();

    public void Setup(Player player) => _player = player;

    public override void _Process(double delta)
    {
        // 进入战斗后才显示小地图
        var game = Core.GameServices.GameOrNull;
        if (game != null)
            Visible = game.CurrentState == GameState.Combat;
    }

    public override void _Ready()
    {
        Layer = 95;
        Name = "MiniMap";
        Visible = false; // 游戏开始前隐藏
        _drawer.MiniMap = this;
        _drawer.MouseFilter = Control.MouseFilterEnum.Ignore;
        _drawer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_drawer);
    }

    public void Render(Control drawer)
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player)) return;
        var vp = drawer.GetViewport().GetVisibleRect();
        var ws = MapContext.CurrentWorldSize;
        // 按世界宽高比缩放，让小地图铺满世界区域而非留大片空白
        float sc = MapSize / Mathf.Max(ws.X, ws.Y);
        float mapW = ws.X * sc;
        float mapH = ws.Y * sc;
        // 右下角，水平居中于 MapSize 框内
        var origin = new Vector2(vp.Size.X - MapSize - 20, vp.Size.Y - mapH - 160);
        Vector2 Map(Vector2 world) => origin + new Vector2(world.X * sc, world.Y * sc);

        // 背景 + 边框（按世界实际比例绘制，不再画正方形留空白）
        drawer.DrawRect(new Rect2(origin - Vector2.One, mapW + 2, mapH + 2),
            new Color(0.15f, 0.1f, 0.08f, 0.35f), filled: false, width: 1.5f);
        drawer.DrawRect(new Rect2(origin, mapW, mapH),
            new Color(0.05f, 0.04f, 0.03f, 0.60f), filled: true);
        drawer.DrawRect(new Rect2(origin, mapW, mapH),
            new Color(0.25f, 0.18f, 0.12f, 0.50f), filled: false, width: 1f);

        // 敌人（红点）
        foreach (var z in ZombieRegistry.All)
        {
            if (GodotObject.IsInstanceValid(z))
                drawer.DrawCircle(Map(z.GlobalPosition), 2.2f, new Color(0.9f, 0.22f, 0.18f, 0.85f));
        }
        // 宝箱（金）
        foreach (var n in drawer.GetTree().GetNodesInGroup("mm_chest"))
        {
            if (n is Node2D nd && GodotObject.IsInstanceValid(nd))
                drawer.DrawCircle(Map(nd.GlobalPosition), 2.6f, new Color(1f, 0.84f, 0.2f));
        }
        // 祭坛（青）
        foreach (var n in drawer.GetTree().GetNodesInGroup("mm_altar"))
        {
            if (n is Node2D nd && GodotObject.IsInstanceValid(nd))
                drawer.DrawCircle(Map(nd.GlobalPosition), 2.6f, new Color(0.4f, 0.8f, 1f));
        }
        // 玩家（白点 + 朝向短线，跟随实际移动方向）
        var pp = Map(_player.GlobalPosition);
        drawer.DrawCircle(pp, 3.2f, Colors.White);
        var vel = _player.Velocity;
        float dir;
        if (vel.LengthSquared() > 4f)
            dir = Mathf.Atan2(vel.Y, vel.X);  // 移动中：指向移动方向
        else
            dir = (_player.Vfx?.FacingLeft ?? false) ? Mathf.Pi : 0f;  // 静止：指向朝向
        var tip = pp + new Vector2(Mathf.Cos(dir), Mathf.Sin(dir)) * 9f;
        drawer.DrawLine(pp, tip, Colors.White, 2f);
    }

    private partial class MapDrawer : Control
    {
        public MiniMap? MiniMap;
        public override void _Process(double delta) => QueueRedraw();
        public override void _Draw()
        {
            // 游戏未开始（非 Combat 状态）时不绘制
            var game = Core.GameServices.GameOrNull;
            if (game == null) return;
            if (game.CurrentState == GameState.WeaponSelection && game.CurrentRound <= 1)
            {
                // 第 1 回合选卡期间也不显示（欢迎画面刚关闭）
                if (!MiniMap?.Visible ?? true) return;
            }
            MiniMap?.Render(this);
        }
    }
}
