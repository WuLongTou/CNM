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
	private Background _background = null!;
	private PlayerClass _player = null!;
	private GameManager _gameManager = null!;
	private WeaponDeck _weaponDeck = null!;
	private UIManager _uiManager = null!;
	private WeatherSystem? _weatherSystem;
	private AutoTestRunner? _autoTestRunner;
	private Camera2D _camera = null!;

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

		// Cache typed references to autoloaded/child nodes.
		_background   = GetNode<Background>("Background");
		_player       = GetNode<PlayerClass>("Player");
		_gameManager  = GetNode<GameManager>("GameManager");
		_weaponDeck   = GetNode<WeaponDeck>("WeaponDeck");
		_uiManager    = GetNode<UIManager>("UILayer/UIManager");

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
		_uiManager.UpgradeSkipped    += _gameManager.OnUpgradeSkipped;
		_player.HealthChanged         += OnPlayerHealthChanged;
		_gameManager.GameOver         += _uiManager.ShowGameOver;

		// Center the player in the world.
		_player.GlobalPosition = GameConfig.WorldSize / 2;

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
			_autoTestRunner = new AutoTestRunner { Name = "AutoTestRunner" };
			AddChild(_autoTestRunner);
		}
	}

	private void _SetupCamera()
	{
		_camera = new Camera2D { Name = "Camera" };
		_camera.LimitLeft = 0;
		_camera.LimitTop = 0;
		_camera.LimitRight = (int)GameConfig.WorldSize.X;
		_camera.LimitBottom = (int)GameConfig.WorldSize.Y;
		_camera.ProcessMode = Node.ProcessModeEnum.Always;
		// Add directly to player so it follows automatically
		_player.AddChild(_camera);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("pause"))
		{
			if (_uiManager.IsUpgradeActive())
				return;
			GetTree().Paused = !GetTree().Paused;
		}
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
}
