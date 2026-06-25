namespace ZombieWars;

using Godot;
using System;
using ZombieWars.Core;
using ZombieWars.UI;
using ZombieWars.Vfx;
using ZombieWars.Audio;
using ZombieWars.World;
using ZombieWars.Render;
using PlayerClass = ZombieWars.Player.Player;
using WeaponDeck = ZombieWars.Weapons.WeaponDeck;

/// <summary>
/// Main game entry point - wires together all subsystems.
/// Uses GameServices for type-safe service access throughout the codebase.
/// </summary>
public partial class Main : Node2D
{
    // ═══ NodePath 导出（编辑器中可拖拽修改；运行时按节点名查找子节点）══
    [Export] public NodePath BackgroundPath { get; set; } = "Background";
    [Export] public NodePath PlayerPath { get; set; } = "Player";
    [Export] public NodePath GameManagerPath { get; set; } = "GameManager";
    [Export] public NodePath WeaponDeckPath { get; set; } = "WeaponDeck";
    [Export] public NodePath UIManagerPath { get; set; } = "UILayer/UIManager";

    private Background _background = null!;
    private PlayerClass _player = null!;
    private GameManager _gameManager = null!;
    private WeaponDeck _weaponDeck = null!;
    private UIManager _uiManager = null!;
    private WeatherSystem? _weatherSystem;
    private AutoTestRunner? _autoTestRunner;
    private GameCamera _camera = null!;
    private RenderViewport _renderViewport = null!;
    private TextureRect _renderRect = null!;
    private MapManager _mapManager = null!;
    private MapBuilder _mapBuilder = null!;

    private Vector2 _screenSize;

    public override void _Ready()
    {
        _screenSize = GetViewport().GetVisibleRect().Size;

        // Instantiate and add manager singletons as children.
        var soundManagerNode = new SoundManager { Name = "SoundManager" };
        AddChild(soundManagerNode);

        var saveManagerNode = new SaveManager { Name = "SaveManager" };
        AddChild(saveManagerNode);

        var vfxManagerNode = new VFXManager { Name = "VFXManager" };
        AddChild(vfxManagerNode);

        var achievementSystemNode = new AchievementSystem { Name = "AchievementSystem" };
        AddChild(achievementSystemNode);

        // Weather system - start it now so timer begins
        _weatherSystem = new WeatherSystem { Name = "WeatherSystem" };
        AddChild(_weatherSystem);
        _weatherSystem.StartWeatherCycle();

        TextureUtils.ClearCache();
        ZombieRegistry.Clear();
        GameServices.Clear();

        // 创建地图系统（多地图 + 障碍物 + 导航），需先于 Background 重建
        _mapManager = new MapManager { Name = "MapManager" };
        AddChild(_mapManager);
        _mapBuilder = new MapBuilder { Name = "MapBuilder" };
        AddChild(_mapBuilder);
        _mapManager.SelectInitialRandom();

        // Cache typed references to autoloaded/child nodes.
        _background = GetNode<Background>(BackgroundPath);
        _player = GetNode<PlayerClass>(PlayerPath);
        _gameManager = GetNode<GameManager>(GameManagerPath);
        _weaponDeck = GetNode<WeaponDeck>(WeaponDeckPath);
        _uiManager = GetNode<UIManager>(UIManagerPath);

        // 用选定地图构建可交互场景（Background._Ready 已用默认构建氛围，这里按选定地图重建）
        _background.LoadMap(_mapManager.Current);
        _CreateRenderViewport(); // 必须在 Build 前:世界节点 _Ready 时挂 3D 代理
        _mapBuilder.Build(_mapManager.Current);
        _mapManager.MapChanged += OnMapChanged;

        // Setup camera
        _SetupCamera();

        // Register all services with GameServices for type-safe access
        GameServices.Register(_uiManager);
        GameServices.Register(_gameManager);
        GameServices.Register(saveManagerNode);
        GameServices.Register(vfxManagerNode);
        GameServices.Register(soundManagerNode);

        // Wire up cross-references.
        _gameManager.SetUIManager(_uiManager);
        _gameManager.SetPlayer(_player);
        _gameManager.SetWeaponDeck(_weaponDeck);

        _uiManager.SetGameManager(_gameManager);
        _uiManager.SetWeaponDeck(_weaponDeck);

        // Connect UI signals → game manager handlers.
        _uiManager.WeaponCardSelected += _gameManager.OnWeaponSelected;
        _uiManager.UpgradeSkipped += _gameManager.OnUpgradeSkipped;
        _player.HealthChanged += OnPlayerHealthChanged;
        _gameManager.GameOver += _uiManager.ShowGameOver;

        // Center the player in the world.
        _player.GlobalPosition = MapContext.CurrentWorldSize / 2;

        // Respond to viewport resizes to keep background covering the screen.
        GetViewport().SizeChanged += OnViewportSizeChanged;

        // Welcome banner.
        LogUtils.Print("僵尸大战游戏启动！");
        LogUtils.Print("使用方向键或 WASD 移动玩家");
        LogUtils.Print("按住空格攻击，Shift 冲刺");
        LogUtils.Print("每回合开始时选择一张武器卡牌来增强你的能力");
        LogUtils.Print("消灭所有僵尸进入下一回合！");

        GameServices.Audio.StartMusic();

        var args = OS.GetCmdlineUserArgs();
        if (Array.Exists(args, arg => arg == "--autotest"))
        {
            LogUtils.Print("[AUTOTEST] enabled");
            GameConfig.IsAutotestMode = true;
            _autoTestRunner = new AutoTestRunner { Name = "AutoTestRunner" };
            AddChild(_autoTestRunner);
        }
        else if (Array.Exists(args, arg => arg == "--perftest"))
        {
            LogUtils.Print("[PERFTEST] enabled");
            AddChild(new PerformanceRunner { Name = "PerformanceRunner" });
        }
        else if (Array.Exists(args, arg => arg == "--evotest"))
        {
            LogUtils.Print("[EVOTEST] enabled");
            AddChild(new EvoTestRunner { Name = "EvoTestRunner" });
        }
    }

    private void _SetupCamera()
    {
        _camera = new GameCamera { Name = "Camera" };
        _camera.UpdateLimits(MapContext.CurrentWorldSize);
        _camera.ProcessMode = Node.ProcessModeEnum.Always;
        _camera.Init(_player);
        // 独立节点：由 GameCamera 自己做平滑跟随，不再硬挂在玩家下
        AddChild(_camera);

        // 视差远景：大场景滚动时远山慢移，产生纵深层次感
        AddChild(new ZombieWars.World.ParallaxBackdrop());

        // 小地图：大场景导航，显示玩家/敌人/宝箱/祭坛位置
        var miniMap = new ZombieWars.UI.MiniMap();
        AddChild(miniMap);
        miniMap.Setup(_player);
    }

    /// <summary>创建 3D 渲染桥(2.5D)。必须在地图构建前调用,
    /// 使世界节点 _Ready 时可通过 RenderViewport.Instance 挂载 3D 代理。</summary>
    private void _CreateRenderViewport()
    {
        _renderViewport = new RenderViewport { Name = "RenderViewport" };
        AddChild(_renderViewport);
        _renderViewport.SetupCamera(
            new Vector2I((int)_screenSize.X, (int)_screenSize.Y),
            MapContext.CurrentWorldSize / 2f);
        _renderViewport.InitCamera(_player);
        _renderViewport.UpdateCameraLimits(MapContext.CurrentWorldSize);

        _renderRect = new TextureRect { Name = "RenderRect" };
        _renderRect.Position = Vector2.Zero;
        _renderRect.Size = _screenSize;
        _renderRect.Texture = _renderViewport.GetTexture();
        _renderRect.ZIndex = 0; // 阶段1 微调(Background 之上、玩家 Sprite2D 之下)
        AddChild(_renderRect);
    }

    public override void _Process(double delta)
    {
        // TextureRect(3D 层)跟随 2D 相机位置+震动,保持屏幕固定。
        // 它在 default canvas 里会被 Camera2D 拖到屏幕外,必须每帧补偿回屏幕中心。
        // zoom 由 Camera2D 的 Zoom 自然缩放 TextureRect(3D 内容随之缩放,单次 zoom)。
        if (_renderRect != null && _camera != null)
            _renderRect.GlobalPosition = _camera.GlobalPosition + _camera.Offset;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
    }

    private void OnPlayerHealthChanged(int newHealth)
    {
        _uiManager.UpdateHealthDisplay(newHealth, _player.MaxHealth);
    }

    private void OnViewportSizeChanged()
    {
        _screenSize = GetViewport().GetVisibleRect().Size;
        if (_background != null)
            _background.UpdateBackgroundSize();
        // 同步 3D 渲染桥的渲染分辨率、正交视野与显示尺寸
        if (_renderViewport != null)
        {
            _renderViewport.Size = new Vector2I((int)_screenSize.X, (int)_screenSize.Y);
            _renderViewport.Camera.Size = _screenSize.Y;
        }
        if (_renderRect != null)
            _renderRect.Size = _screenSize;
    }

    private void OnMapChanged(MapData map)
    {
        _background.LoadMap(map);
        _mapBuilder.Build(map);
        // 玩家回到新地图中心，避免卡入障碍
        _player.GlobalPosition = MapContext.CurrentWorldSize / 2;
        // 刷新摄像机边界到新地图尺寸
        _camera.UpdateLimits(MapContext.CurrentWorldSize);
        _renderViewport.UpdateCameraLimits(MapContext.CurrentWorldSize);
        LogUtils.Print($"[Main] 地图已切换：{map.Name}");
    }

    public void ToggleHelp()
    {
        _uiManager.ToggleHelp();
    }
}
