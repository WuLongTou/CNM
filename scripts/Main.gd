extends Node2D

@onready var background: Background = $Background
@onready var player: Player = $Player
@onready var game_manager: GameManager = $GameManager
@onready var weapon_deck: WeaponDeck = $WeaponDeck
@onready var ui_manager: UIManager = $UILayer/UIManager

func _ready():
	# 设置游戏管理器的UI管理器引用
	game_manager.ui_manager = ui_manager
	
	# 设置游戏管理器
	game_manager.set_player(player)
	game_manager.set_weapon_deck(weapon_deck)
	
	# 设置UI管理器
	ui_manager.set_game_manager(game_manager)
	ui_manager.set_weapon_deck(weapon_deck)
	
	# 连接信号
	ui_manager.weapon_card_selected.connect(game_manager.on_weapon_selected)
	player.health_changed.connect(_on_player_health_changed)
	game_manager.game_over.connect(ui_manager.show_game_over)
	
	# 设置玩家初始位置
	var screen_size = get_viewport().get_visible_rect().size
	player.global_position = screen_size / 2
	
	# 连接窗口大小改变信号
	get_viewport().size_changed.connect(_on_viewport_size_changed)
	
	print("僵尸大战游戏启动！")
	print("使用方向键移动玩家")
	print("每回合开始时选择一张武器卡牌来增强你的能力")
	print("消灭所有僵尸进入下一回合！")

func _on_player_health_changed(new_health: int):
	ui_manager.update_health_display(new_health, player.max_health)

func _on_viewport_size_changed():
	# 更新背景
	if background:
		background.update_background_size()
