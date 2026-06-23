namespace ZombieWars;

using Godot;
using System;
using ZombieWars.Core;
using ZombieWars.UI;
using ZombieWars.Vfx;
using ZombieWars.Audio;
using ZombieWars.World;
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
    }

    private void OnMapChanged(MapData map)
    {
        _background.LoadMap(map);
        _mapBuilder.Build(map);
        // 玩家回到新地图中心，避免卡入障碍
        _player.GlobalPosition = MapContext.CurrentWorldSize / 2;
        // 刷新摄像机边界到新地图尺寸
        _camera.UpdateLimits(MapContext.CurrentWorldSize);
        LogUtils.Print($"[Main] 地图已切换：{map.Name}");
    }

    public void ToggleHelp()
    {
        _uiManager.ToggleHelp();
    }
}
