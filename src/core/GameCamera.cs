namespace ZombieWars.Core;

using Godot;

/// <summary>
/// 游戏摄像机：平滑跟随 + 移动方向前瞻 + 冲击 zoom punch + 真·摄像机震动 + 冲刺 zoom out。
/// 替代原来挂在玩家下的硬跟随 Camera2D，让镜头有生命力和打击感。
/// 物理坐标完全不变（Zoom/Offset 是纯渲染变换）。
/// </summary>
public partial class GameCamera : Camera2D
{
    private ZombieWars.Player.Player _target = null!;
    private Vector2 _currentPos;
    private bool _initialized;

    private float _shake;       // 当前震动幅度
    private float _zoomPunch;   // 当前 zoom punch（正=拉近）

    public void Init(ZombieWars.Player.Player target)
    {
        _target = target;
        AddToGroup("game_camera");
        Zoom = GameConfig.CameraBaseZoom;
        PositionSmoothingEnabled = false; // 自己实现阻尼跟随
    }

    public void UpdateLimits(Vector2 worldSize)
    {
        LimitLeft = 0;
        LimitTop = 0;
        LimitRight = (int)worldSize.X;
        LimitBottom = (int)(worldSize.Y - GameConfig.BottomUiMargin); // 底部 HUD 区不算游戏区域
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsInstanceValid(_target)) return;
        float d = (float)delta;

        // ── 平滑跟随 + 移动方向前瞻（露出前方视野）──
        var vel = _target.Velocity;
        float lookLen = Mathf.Min(vel.Length() * GameConfig.CameraLookaheadFactor,
            GameConfig.CameraLookaheadMax);
        var lookOffset = vel.Length() > 1f ? vel.Normalized() * lookLen : Vector2.Zero;
        var desired = _target.GlobalPosition + lookOffset;

        if (!_initialized)
        {
            // 开场瞬移到目标，避免镜头从原点飞过来
            _currentPos = _target.GlobalPosition;
            _initialized = true;
        }
        else
        {
            float t = 1f - Mathf.Exp(-GameConfig.CameraSmoothing * d);
            _currentPos = _currentPos.Lerp(desired, t);
        }
        GlobalPosition = _currentPos;

        // ── 震动（叠加到 Offset，不影响跟随位置）──
        Vector2 shakeOff = Vector2.Zero;
        if (_shake > 0f)
        {
            _shake = Mathf.Max(0f, _shake - GameConfig.CameraShakeDecay * d);
            shakeOff = new Vector2(
                (float)GD.RandRange(-_shake, _shake),
                (float)GD.RandRange(-_shake, _shake));
        }

        // ── zoom punch 衰减 ──
        if (_zoomPunch > 0f)
            _zoomPunch = Mathf.Max(0f, _zoomPunch - GameConfig.CameraZoomPunchDecay * d);

        // ── 冲刺 zoom out（相对缩放，乘到 baseZoom 两轴以保持斜视比例）──
        float dashZ = _target.IsDashing ? -GameConfig.CameraDashZoomOut : 0f;
        float dynRel = _zoomPunch + dashZ;
        Zoom = GameConfig.CameraBaseZoom * (1f + dynRel);

        Offset = shakeOff;
    }

    /// <summary>外部触发：摄像机震动（自动关联一次轻微 zoom punch）。</summary>
    public void AddShake(float intensity)
    {
        _shake = Mathf.Max(_shake, intensity);
        _zoomPunch = Mathf.Max(_zoomPunch, intensity * GameConfig.CameraShakeToZoom);
    }

    /// <summary>外部触发：纯 zoom punch（攻击命中拉近弹回）。</summary>
    public void PunchZoom(float amount)
    {
        _zoomPunch = Mathf.Max(_zoomPunch, amount);
    }
}
