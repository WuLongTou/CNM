namespace ZombieWars.UI;

using Godot;
using ZombieWars.Core;
using ZombieWars.Enemy;
using ZombieWars.Player;

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

    public override void _Ready()
    {
        Layer = 95;
        Name = "MiniMap";
        _drawer.MiniMap = this;
        _drawer.MouseFilter = Control.MouseFilterEnum.Ignore;
        _drawer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_drawer);
    }

    public void Render(Control drawer)
    {
        if (_player == null || !GodotObject.IsInstanceValid(_player)) return;
        var vp = drawer.GetViewport().GetVisibleRect();
        // 右下角，避开底部 HUD（上调留空，避免与右下按钮重叠）
        var origin = new Vector2(vp.Size.X - MapSize - 20, vp.Size.Y - MapSize - 160);
        var ws = MapContext.CurrentWorldSize;
        float sc = MapSize / Mathf.Max(ws.X, ws.Y);
        Vector2 Map(Vector2 world) => origin + new Vector2(world.X * sc, world.Y * sc);

        // 背景 + 外发光 + 边框
        drawer.DrawRect(new Rect2(origin, MapSize, MapSize), new Color(0.05f, 0.04f, 0.03f, 0.60f), filled: true);
        drawer.DrawRect(new Rect2(origin - Vector2.One, MapSize + 2, MapSize + 2), new Color(0.55f, 0.42f, 0.18f, 0.15f), filled: false, width: 3f);
        drawer.DrawRect(new Rect2(origin, MapSize, MapSize), new Color(0.65f, 0.50f, 0.25f, 0.85f), filled: false, width: 2f);

        // 敌人（红点）
        foreach (var z in ZombieRegistry.All)
        {
            if (GodotObject.IsInstanceValid(z))
                drawer.DrawCircle(Map(z.GlobalPosition), 1.4f, new Color(0.9f, 0.22f, 0.18f, 0.85f));
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
        // 玩家（白点 + 朝向短线）
        var pp = Map(_player.GlobalPosition);
        drawer.DrawCircle(pp, 3.2f, Colors.White);
        bool faceLeft = _player.Vfx?.FacingLeft ?? (_player.Velocity.X < 0);
        float dir = faceLeft ? Mathf.Pi : 0f;
        var tip = pp + new Vector2(Mathf.Cos(dir), Mathf.Sin(dir)) * 9f;
        drawer.DrawLine(pp, tip, Colors.White, 2f);
    }

    private partial class MapDrawer : Control
    {
        public MiniMap? MiniMap;
        public override void _Process(double delta) => QueueRedraw();
        public override void _Draw() => MiniMap?.Render(this);
    }
}
