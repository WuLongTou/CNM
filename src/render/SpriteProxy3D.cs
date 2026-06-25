namespace ZombieWars.Render;

using Godot;
using ZombieWars.Core;

/// <summary>
/// 2D 精灵的 3D 代理(Sprite3D):把玩家/僵尸的 Sprite2D 搬进 3D 渲染层,
/// 与 3D 模型统一 Z 序(阶段 3)。
///
/// 平躺(rotation -90° X)贴地,顶视相机看正面;PixelSize=1 让 1 纹理像素=1 世界单位
/// (与 3D 相机 size 配合,显示尺寸与原 2D sprite 一致)。
/// 位置由绑定的 2D 节点每帧同步(见 <see cref="Coord"/>)。
/// Texture/FlipH/Modulate/Frame 等 Sprite3D 均支持,原 Sprite2D 操作逻辑无需改。
/// </summary>
public partial class SpriteProxy3D : Sprite3D
{
    private Node2D? _source;

    /// <summary>绑定 2D 玩法节点:每帧同步其 GlobalPosition 到本代理 3D 位置。</summary>
    public void BindTo2D(Node2D source) => _source = source;

    public override void _Ready()
    {
        RotationDegrees = new Vector3(-90f, 0f, 0f); // 平躺贴地,顶视看正面
        PixelSize = 1.0f;                             // 1 纹理像素 = 1 世界单位
        NoDepthTest = true;                           // 不参与深度测试,用 Y 高度排序即可
    }

    private bool _autoAttached;

    public override void _Process(double delta)
    {
        // 自动 reparent 到 3D 渲染层(Player._Ready 早于 RenderViewport 创建,需延迟挂载)
        if (!_autoAttached)
        {
            var rv = RenderViewport.Instance;
            if (rv != null && GodotObject.IsInstanceValid(rv))
            {
                if (GetParent() != rv.RenderLayer)
                {
                    GetParent()?.RemoveChild(this);
                    rv.AddProxy(this);
                }
                _autoAttached = true;
            }
        }
        if (_source != null && GodotObject.IsInstanceValid(_source))
            GlobalPosition = Coord.World2DTo3D(_source.GlobalPosition);
    }
}
