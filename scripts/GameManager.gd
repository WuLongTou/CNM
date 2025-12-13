class_name GameManager
extends Node

signal round_started(round_number: int)
signal round_ended(round_number: int)
signal weapon_selection_started
signal weapon_selection_ended
signal game_over

enum GameState {
	WEAPON_SELECTION,
	COMBAT,
	ROUND_TRANSITION,
	GAME_OVER
}

@export var zombies_per_round_base: int = 5
@export var zombies_increase_per_round: int = 2
@export var spawn_interval: float = 2.0

var current_state: GameState = GameState.WEAPON_SELECTION
var current_round: int = 0
var zombies_to_spawn: int = 0
var zombies_spawned: int = 0
var zombies_alive: int = 0
var spawn_timer: float = 0.0

var player: Player
var weapon_deck: WeaponDeck
var zombie_scene: PackedScene
var spawn_points: Array[Vector2] = []

var ui_manager: UIManager

func _ready():
	# 预加载僵尸场景
	if ResourceLoader.exists("res://scenes/Zombie.tscn"):
		zombie_scene = preload("res://scenes/Zombie.tscn")
	
	# 设置生成点（围绕屏幕边缘）
	_setup_spawn_points()
	
	# 连接窗口大小改变信号
	get_viewport().size_changed.connect(_on_viewport_size_changed)
	
	# 等待一帧让所有节点初始化完成
	await get_tree().process_frame
	
	# 等待欢迎界面关闭后再开始游戏
	if ui_manager:
		while ui_manager.welcome_visible:
			await get_tree().process_frame
	
	# 开始第一回合
	start_new_round()

func _on_viewport_size_changed():
	# 窗口大小改变时更新生成点
	_update_spawn_points()

func _process(delta):
	match current_state:
		GameState.COMBAT:
			_handle_combat_state(delta)

func _handle_combat_state(delta):
	# 处理僵尸生成
	if zombies_spawned < zombies_to_spawn:
		spawn_timer -= delta
		if spawn_timer <= 0:
			spawn_zombie()
			spawn_timer = spawn_interval
	
	# 检查回合是否结束
	if zombies_spawned >= zombies_to_spawn and zombies_alive <= 0:
		end_round()

func _setup_spawn_points():
	_update_spawn_points()

func _update_spawn_points():
	spawn_points.clear()
	var screen_size = get_viewport().get_visible_rect().size
	var margin = 100.0
	
	# 上边 - 5个点
	for i in range(5):
		spawn_points.append(Vector2(i * screen_size.x / 4, -margin))
	
	# 下边 - 5个点
	for i in range(5):
		spawn_points.append(Vector2(i * screen_size.x / 4, screen_size.y + margin))
	
	# 左边 - 4个点
	for i in range(4):
		spawn_points.append(Vector2(-margin, i * screen_size.y / 3))
	
	# 右边 - 4个点
	for i in range(4):
		spawn_points.append(Vector2(screen_size.x + margin, i * screen_size.y / 3))

func start_new_round():
	current_round += 1
	current_state = GameState.WEAPON_SELECTION
	
	# 计算本回合僵尸数量
	zombies_to_spawn = zombies_per_round_base + (current_round - 1) * zombies_increase_per_round
	zombies_spawned = 0
	zombies_alive = zombies_to_spawn  # 立即设置为将要生成的僵尸数量
	spawn_timer = spawn_interval
	
	round_started.emit(current_round)
	weapon_selection_started.emit()
	
	# 立即更新UI显示正确的僵尸数量
	if ui_manager:
		ui_manager.update_round_display(current_round)
		ui_manager.update_zombies_display(zombies_alive)
		ui_manager.show_weapon_selection()
	
	print("第 " + str(current_round) + " 回合开始！需要消灭 " + str(zombies_to_spawn) + " 只僵尸")

func on_weapon_selected(weapon: WeaponCard):
	if current_state != GameState.WEAPON_SELECTION:
		return
	
	# 立即应用武器效果到玩家
	if player:
		player.apply_weapon_effect(weapon)
		# 立即更新UI显示玩家状态
		if ui_manager:
			ui_manager.update_health_display(player.current_health, player.max_health)
			ui_manager.update_player_status(player)
	
	# 开始战斗阶段
	current_state = GameState.COMBAT
	weapon_selection_ended.emit()
	
	if ui_manager:
		ui_manager.hide_weapon_selection()
	
	print("选择了武器: " + weapon.card_name + "，效果已立即生效！开始战斗！")

func spawn_zombie():
	if not zombie_scene:
		print("错误：僵尸场景未加载")
		return
	
	var zombie_instance = zombie_scene.instantiate() as Zombie
	if not zombie_instance:
		print("错误：无法创建僵尸实例")
		return
	
	# 随机选择僵尸类型（根据回合数增加高级僵尸的概率）
	var zombie_type = _get_random_zombie_type()
	zombie_instance.zombie_type = zombie_type
	
	# 根据回合数增加僵尸血量（难度递增）
	var health_multiplier = 1.0 + (current_round - 1) * 0.3  # 每回合增加30%血量
	zombie_instance.apply_round_scaling(current_round, health_multiplier)
	
	# 随机选择生成点
	var spawn_point = spawn_points[randi() % spawn_points.size()]
	zombie_instance.global_position = spawn_point
	
	# 设置玩家目标
	zombie_instance.set_player_target(player)
	
	# 连接死亡信号
	zombie_instance.died.connect(_on_zombie_died)
	
	# 添加到场景
	get_tree().current_scene.add_child(zombie_instance)
	
	zombies_spawned += 1
	# 注意：不要在这里增加zombies_alive，因为它已经在start_new_round中设置了
	
	print("生成僵尸 " + str(zombies_spawned) + "/" + str(zombies_to_spawn) + "（血量倍数: " + str(health_multiplier) + "）")

func _get_random_zombie_type() -> Zombie.ZombieType:
	var rand = randf()
	
	# 根据回合数调整僵尸类型概率
	if current_round <= 2:
		# 前两回合只有基础僵尸
		return Zombie.ZombieType.BASIC
	elif current_round <= 5:
		# 3-5回合添加快速僵尸
		if rand < 0.7:
			return Zombie.ZombieType.BASIC
		else:
			return Zombie.ZombieType.FAST
	elif current_round <= 8:
		# 6-8回合添加坦克僵尸
		if rand < 0.5:
			return Zombie.ZombieType.BASIC
		elif rand < 0.8:
			return Zombie.ZombieType.FAST
		else:
			return Zombie.ZombieType.TANK
	else:
		# 9回合以后添加爆炸僵尸
		if rand < 0.3:
			return Zombie.ZombieType.BASIC
		elif rand < 0.6:
			return Zombie.ZombieType.FAST
		elif rand < 0.85:
			return Zombie.ZombieType.TANK
		else:
			return Zombie.ZombieType.EXPLODER

func _on_zombie_died(_zombie: Zombie):
	zombies_alive -= 1
	print("僵尸死亡，剩余: " + str(zombies_alive))
	
	# 更新UI显示
	if ui_manager:
		ui_manager.update_zombies_display(zombies_alive)

func end_round():
	current_state = GameState.ROUND_TRANSITION
	round_ended.emit(current_round)
	
	print("第 " + str(current_round) + " 回合结束！")
	
	# 短暂延迟后开始下一回合
	await get_tree().create_timer(2.0).timeout
	start_new_round()

func set_player(player_node: Player):
	player = player_node
	player.died.connect(_on_player_died)

func set_weapon_deck(deck: WeaponDeck):
	weapon_deck = deck

func _on_player_died():
	current_state = GameState.GAME_OVER
	game_over.emit()
	print("游戏结束！坚持了 " + str(current_round) + " 回合")

func get_current_round() -> int:
	return current_round

func get_zombies_remaining() -> int:
	return zombies_alive
