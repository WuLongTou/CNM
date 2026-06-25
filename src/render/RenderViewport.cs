namespace ZombieWars.Render;

using Godot;
using ZombieWars.Core;

/// <summary>
/// 3D 渲染桥(SubViewport):内部装正交 Camera3D + 灯光 + RenderLayer(Node3D 容器)。
///
/// 把 2D 玩法层的世界用真正的 3D 渲染(模型 MeshInstance3D 原材质正常显色,
/// 解决 MeshInstance2D 白方块问题),其纹理供 2D 主场景 TextureRect 显示。
/// 坐标与 2D 层 1:1 同步(见 <see cref="Coord"/>),Camera3D 朝 -Y 正交俯视。
///
/// 阶段 0:静态相机看世界中心;阶段 2 由 GameCamera3D 接管跟随玩家。
/// </summary>
public partial class RenderViewport : SubViewport
{
    private Camera3D _camera = null!;
    private Node3D _renderLayer = null!;

    /// <summary>3D 代理容器:模型/角色代理(ModelProxy3D)挂在这里统一渲染。</summary>
    public Node3D RenderLayer => _renderLayer;

    /// <summary>全局访问点(供世界节点 AddProxy 挂载 3D 代理)。Main 在地图构建前创建。</summary>
    public static RenderViewport? Instance { get; private set; }

    /// <summary>正交俯视相机(阶段 2 的 GameCamera3D 会接管跟随/震动/zoom)。</summary>
    public Camera3D Camera => _camera;

    public override void _Ready()
    {
        Instance = this;
        // 独立 3D 世界(主场景是纯 2D,无 World3D)
        World3D = new World3D();

        // SubViewport 渲染设置:每帧更新 + 透明背景(叠在 2D 场景之上)
        RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        TransparentBg = true;

        // 3D 代理容器
        _renderLayer = new Node3D { Name = "RenderLayer" };
        AddChild(_renderLayer);

        // 正交俯视相机(朝 -Y),与 2D 玩法层坐标 1:1
        _camera = new GameCamera3D
        {
            Name = "Camera3D",
            Projection = Camera3D.ProjectionType.Orthogonal,
            Current = true,
            RotationDegrees = new Vector3(-90f, 0f, 0f),
            Near = 0.1f,
            Far = 4000f,
        };
        AddChild(_camera);

        // 主光:暖色斜射,与 2D SunLight 氛围一致,制造立体阴影
        AddChild(new DirectionalLight3D
        {
            Name = "KeyLight",
            LightColor = new Color(1.0f, 0.93f, 0.78f),
            LightEnergy = 1.3f,
            RotationDegrees = new Vector3(45f, 35f, 0f),
            ShadowEnabled = true,
        });

        // 补光:冷蓝,提亮暗面
        AddChild(new DirectionalLight3D
        {
            Name = "FillLight",
            LightColor = new Color(0.6f, 0.7f, 0.9f),
            LightEnergy = 0.5f,
            RotationDegrees = new Vector3(-30f, -20f, 0f),
        });

        // 环境:透明背景 + 微弱环境光兜底(避免暗面全黑)
        AddChild(new WorldEnvironment
        {
            Name = "Env3D",
            Environment = new Environment
            {
                BackgroundMode = Environment.BGMode.Color,
                BackgroundColor = new Color(0f, 0f, 0f, 0f),
                AmbientLightSource = Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.55f, 0.55f, 0.6f),
                AmbientLightEnergy = 0.35f,
            },
        });
    }

    /// <summary>设置渲染分辨率(SubViewport.Size)与视口尺寸(GameCamera3D 求 aspect)。</summary>
    public void SetupCamera(Vector2I viewportSize, Vector2 center2D)
    {
        Size = viewportSize;
        if (_camera is GameCamera3D g)
            g.SetViewport(viewportSize);
    }

    /// <summary>初始化 3D 相机跟随目标(阶段 2)。</summary>
    public void InitCamera(ZombieWars.Player.Player player)
    {
        if (_camera is GameCamera3D g)
            g.Init(player);
    }

    /// <summary>更新 3D 相机边界(地图切换时)。</summary>
    public void UpdateCameraLimits(Vector2 worldSize)
    {
        if (_camera is GameCamera3D g)
            g.UpdateLimits(worldSize);
    }

    /// <summary>挂载 3D 代理到渲染层。</summary>
    public void AddProxy(Node3D proxy) => _renderLayer.AddChild(proxy);

    /// <summary>移除 3D 代理。</summary>
    public void RemoveProxy(Node3D proxy) => _renderLayer.RemoveChild(proxy);
}
