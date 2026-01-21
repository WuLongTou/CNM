extends Node2D

@onready var background: Background = $Background
@onready var player: Player = $Player
@onready var game_manager: GameManager = $GameManager
@onready var weapon_deck: WeaponDeck = $WeaponDeck
@onready var ui_manager = $UILayer/UIManager
var sound_manager: SoundManager

func _ready():
	var sound_manager_node = SoundManager.new()
	sound_manager_node.name = "SoundManager"
	add_child(sound_manager_node)
	sound_manager = sound_manager_node
	
	var save_manager_node = SaveManager.new()
	save_manager_node.name = "SaveManager"
	add_child(save_manager_node)
	
	game_manager.ui_manager = ui_manager
	
	game_manager.set_player(player)
	game_manager.set_weapon_deck(weapon_deck)
	
	ui_manager.set_game_manager(game_manager)
	ui_manager.set_weapon_deck(weapon_deck)
	
	ui_manager.weapon_card_selected.connect(game_manager.on_weapon_selected)
	ui_manager.upgrade_skipped.connect(game_manager.on_upgrade_skipped)
	player.health_changed.connect(_on_player_health_changed)
	game_manager.game_over.connect(ui_manager.show_game_over)
	
	var screen_size = get_viewport().get_visible_rect().size
	player.global_position = screen_size / 2
	
	get_viewport().size_changed.connect(_on_viewport_size_changed)
	
	print("僵尸大战游戏启动！")
	print("使用方向键或 WASD 移动玩家")
	print("按住空格攻击，Shift 冲刺")
	print("每回合开始时选择一张武器卡牌来增强你的能力")
	print("消灭所有僵尸进入下一回合！")

func _unhandled_input(event):
	if event.is_action_pressed("pause"):
		if ui_manager and ui_manager.is_upgrade_active():
			return
		if ui_manager:
			ui_manager.set_user_paused(not ui_manager.user_paused)

func _on_player_health_changed(new_health: int):
	ui_manager.update_health_display(new_health, player.max_health)

func _on_viewport_size_changed():
	# 更新背景
	if background:
		background.update_background_size()
