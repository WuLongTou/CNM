namespace ZombieWars.Render;

using Godot;
using ZombieWars.Core;
using ZombieWars.Player;

/// <summary>
/// 3D 正交相机(2.5D 渲染桥),只负责【位置跟随】玩家 + Limit 边界。
///
/// zoom / 震动 全部交给 2D GameCamera:TextureRect 每帧跟随 2D 相机的
/// GlobalPosition + Offset(屏幕固定),并随其 Zoom 缩放。这样 3D 内容的
/// 镜头动感(拉近/震动)由 2D 相机统一驱动,避免 2D/3D 双重 zoom/震动叠加。
///
/// 纯顶视(rotation -90° X),坐标与 2D 玩法层 1:1(见 <see cref="Coord"/>)。
/// </summary>
public partial class GameCamera3D : Camera3D
{
    private Player _target = null!;
    private Vector3 _currentPos;
    private bool _initialized;
    private float _baseSize = 720f;     // 正交视野高度(= 视口像素高,固定不 zoom)
    private float _aspect = 16f / 9f;
    private Vector2 _worldSize;          // 2D 世界尺寸(X, Y→Z)

    /// <summary>绑定跟随目标(2D 玩家)。</summary>
    public void Init(Player target)
    {
        _target = target;
    }

    /// <summary>设置视口尺寸:正交 Size = 视口高(固定),aspect 用于 Limit。</summary>
    public void SetViewport(Vector2I viewportSize)
    {
        _baseSize = viewportSize.Y;
        _aspect = viewportSize.X / (float)viewportSize.Y;
        Size = _baseSize;
    }

    /// <summary>更新世界边界(地图切换时)。</summary>
    public void UpdateLimits(Vector2 worldSize) => _worldSize = worldSize;

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_target)) return;
        float d = (float)delta;

        // 平滑跟随 + 速度前瞻(2D player → 3D)
        var vel = _target.Velocity;
        float lookLen = Mathf.Min(vel.Length() * GameConfig.CameraLookaheadFactor,
            GameConfig.CameraLookaheadMax);
        var lookOffset = vel.Length() > 1f ? vel.Normalized() * lookLen : Vector2.Zero;
        var desired = Coord.World2DTo3D(_target.GlobalPosition + lookOffset);

        if (!_initialized)
        {
            _currentPos = Coord.World2DTo3D(_target.GlobalPosition);
            _initialized = true;
        }
        else
        {
            float t = 1f - Mathf.Exp(-GameConfig.CameraSmoothing * d);
            _currentPos = _currentPos.Lerp(desired, t);
        }

        // Limit 钳制:相机视野不超出世界边界
        float halfH = _baseSize * 0.5f;
        float halfW = halfH * _aspect;
        _currentPos.X = Mathf.Clamp(_currentPos.X,
            Mathf.Min(halfW, _worldSize.X * 0.5f),
            Mathf.Max(_worldSize.X - halfW, _worldSize.X * 0.5f));
        _currentPos.Z = Mathf.Clamp(_currentPos.Z,
            Mathf.Min(halfH, _worldSize.Y * 0.5f),
            Mathf.Max(_worldSize.Y - halfH, _worldSize.Y * 0.5f));

        GlobalPosition = _currentPos + new Vector3(0f, 1000f, 0f);
    }
}
