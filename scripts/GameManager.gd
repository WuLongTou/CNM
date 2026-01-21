class_name GameManager
extends Node
## 游戏管理器
## 负责管理游戏状态、回合流程、僵尸生成和难度缩放

signal round_started(round_number: int)
signal round_ended(round_number: int)
signal weapon_selection_started
signal weapon_selection_ended
signal game_over

## 游戏状态枚举
enum GameState {
	WEAPON_SELECTION,  ## 武器选择阶段
	COMBAT,            ## 战斗阶段
	ROUND_TRANSITION,  ## 回合过渡
	GAME_OVER          ## 游戏结束
}

# ===== 平衡常量 =====
# 僵尸数量
@export var zombies_per_round_base: int = 4  # 减少初始数量
@export var zombies_increase_per_round: int = 2  # 每回合增加
@export var zombies_max_per_round: int = 50  # 单回合上限

# 生成间隔
@export var spawn_interval: float = 2.5  # 初始间隔更长
@export var spawn_interval_min: float = 0.6
@export var spawn_interval_decay: float = 0.08  # 减缓衰减

# 难度缩放
@export var round_multiplier_base: float = 0.05  # 略微降低
@export var elite_chance_base: float = 0.03
@export var elite_chance_per_round: float = 0.012
@export var elite_chance_max: float = 0.30

# 回合间恢复
@export var heal_between_rounds: bool = true
@export var heal_percent: float = 0.15  # 回合结束恢复15%生命

var current_state: GameState = GameState.WEAPON_SELECTION
var current_round: int = 0
var zombies_to_spawn: int = 0
var zombies_spawned: int = 0
var zombies_alive: int = 0
var spawn_timer: float = 0.0
var current_spawn_interval: float = 2.0

var player: Player
var weapon_deck: WeaponDeck
var zombie_scene: PackedScene
var spawn_points: Array[Vector2] = []

var ui_manager: UIManager

var score: int = 0
var high_score: int = 0

func _ready():
	if ResourceLoader.exists("res://scenes/Zombie.tscn"):
		zombie_scene = preload("res://scenes/Zombie.tscn")
	
	_setup_spawn_points()
	
	get_viewport().size_changed.connect(_on_viewport_size_changed)
	
	await get_tree().process_frame
	
	if ui_manager:
		while ui_manager.welcome_visible:
			await get_tree().process_frame
	
	if SaveManager and SaveManager.instance:
		high_score = SaveManager.get_high_score()
		SaveManager.add_game_played()
		print("最高分: " + str(high_score))
	
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
			spawn_timer = current_spawn_interval
	
	# 检查回合是否结束
	if zombies_spawned >= zombies_to_spawn and zombies_alive <= 0:
		end_round()

func _setup_spawn_points():
	_update_spawn_points()

func _update_spawn_points():
	spawn_points.clear()
	var screen_size = get_viewport().get_visible_rect().size
	var margin = 30.0
	var top_margin = 110.0
	if ui_manager and ui_manager.hud_panel:
		top_margin = ui_manager.hud_panel.size.y + 10.0
	
	var play_height = max(0.0, screen_size.y - top_margin)
	var top_edge = top_margin + margin
	var bottom_edge = top_margin + play_height - margin
	var left_edge = margin
	var right_edge = screen_size.x - margin
	
	# 上边 - 5个点（边界内）
	for i in range(5):
		spawn_points.append(Vector2(i * screen_size.x / 4, top_edge))
	
	# 下边 - 5个点
	for i in range(5):
		spawn_points.append(Vector2(i * screen_size.x / 4, bottom_edge))
	
	# 左边 - 4个点
	for i in range(4):
		spawn_points.append(Vector2(left_edge, top_margin + i * play_height / 3))
	
	# 右边 - 4个点
	for i in range(4):
		spawn_points.append(Vector2(right_edge, top_margin + i * play_height / 3))

func start_new_round():
	current_round += 1
	current_state = GameState.WEAPON_SELECTION
	
	# 计算本回合僵尸数量 (平滑增长曲线)
	var round_factor = sqrt(float(current_round)) * 1.5
	zombies_to_spawn = zombies_per_round_base + int(round_factor) + (current_round - 1) * zombies_increase_per_round
	zombies_to_spawn = min(zombies_to_spawn, zombies_max_per_round)  # 限制上限
	zombies_spawned = 0
	zombies_alive = 0
	
	# 生成间隔衰减 (更平滑)
	current_spawn_interval = max(spawn_interval_min, spawn_interval * pow(0.94, max(0, current_round - 1)))
	spawn_timer = current_spawn_interval
	
	# 回合间恢复
	if heal_between_rounds and player and current_round > 1:
		var heal_amount = int(player.max_health * heal_percent)
		player.heal(heal_amount)
		if ui_manager:
			ui_manager.show_damage_number(player.global_position, -heal_amount, false)
		print("回合间恢复 " + str(heal_amount) + " 生命")
	
	round_started.emit(current_round)
	weapon_selection_started.emit()
	
	# 立即更新UI显示正确的僵尸数量
	if ui_manager:
		ui_manager.update_round_display(current_round)
		ui_manager.update_zombies_display(_get_remaining_zombies(), zombies_to_spawn)
		ui_manager.show_weapon_selection()
		if player:
			ui_manager.update_health_display(player.current_health, player.max_health)
	
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

func on_upgrade_skipped():
	if current_state != GameState.WEAPON_SELECTION:
		return
	
	current_state = GameState.COMBAT
	weapon_selection_ended.emit()
	
	if ui_manager:
		ui_manager.hide_weapon_selection()
	
	print("跳过升级！直接开始战斗！")

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
	
	# 精英僵尸概率
	var elite_chance = min(elite_chance_max, elite_chance_base + elite_chance_per_round * max(0, current_round - 1))
	if randf() < elite_chance:
		zombie_instance.set_elite(true)
	
	# 根据回合数增加僵尸属性
	var round_multiplier = 1.0 + log(1.0 + float(current_round)) * round_multiplier_base * 2.0
	zombie_instance.apply_round_scaling(current_round, round_multiplier)
	
	# 随机选择生成点
	if spawn_points.is_empty():
		_update_spawn_points()
	var spawn_point = spawn_points[randi() % spawn_points.size()]
	var offset = Vector2(randf_range(-20, 20), randf_range(-20, 20))
	zombie_instance.global_position = spawn_point + offset
	
	# 设置玩家目标
	zombie_instance.set_player_target(player)
	
	# 连接死亡信号
	zombie_instance.died.connect(_on_zombie_died)
	
	# 添加到场景
	get_tree().current_scene.add_child(zombie_instance)
	
	zombies_spawned += 1
	zombies_alive += 1
	
	if ui_manager:
		ui_manager.update_zombies_display(_get_remaining_zombies(), zombies_to_spawn)
	
	print("生成僵尸 " + str(zombies_spawned) + "/" + str(zombies_to_spawn) + 
		" (类型: " + _get_zombie_type_name(zombie_type) + 
		", 强度: x" + str(round_multiplier) + ")")

func _get_random_zombie_type() -> Zombie.ZombieType:
	var rand = randf()
	
	# 根据回合数调整僵尸类型概率（更平滑的难度曲线）
	if current_round <= 2:
		return Zombie.ZombieType.BASIC
	elif current_round <= 4:
		if rand < 0.75:
			return Zombie.ZombieType.BASIC
		else:
			return Zombie.ZombieType.FAST
	elif current_round <= 6:
		if rand < 0.45:
			return Zombie.ZombieType.BASIC
		elif rand < 0.75:
			return Zombie.ZombieType.FAST
		elif rand < 0.9:
			return Zombie.ZombieType.TANK
		else:
			return Zombie.ZombieType.EXPLODER
	elif current_round <= 10:
		if rand < 0.25:
			return Zombie.ZombieType.BASIC
		elif rand < 0.45:
			return Zombie.ZombieType.FAST
		elif rand < 0.65:
			return Zombie.ZombieType.TANK
		elif rand < 0.8:
			return Zombie.ZombieType.EXPLODER
		elif rand < 0.9:
			return Zombie.ZombieType.INVISIBLE
		else:
			return Zombie.ZombieType.SPITTER
	else:
		# 高回合数，所有类型均可能出现，包括治疗僵尸
		if rand < 0.15:
			return Zombie.ZombieType.BASIC
		elif rand < 0.30:
			return Zombie.ZombieType.FAST
		elif rand < 0.50:
			return Zombie.ZombieType.TANK
		elif rand < 0.65:
			return Zombie.ZombieType.EXPLODER
		elif rand < 0.78:
			return Zombie.ZombieType.INVISIBLE
		elif rand < 0.90:
			return Zombie.ZombieType.SPITTER
		else:
			return Zombie.ZombieType.HEALER

func _get_zombie_type_name(type: int) -> String:
	match type:
		Zombie.ZombieType.BASIC:
			return "基础"
		Zombie.ZombieType.FAST:
			return "快速"
		Zombie.ZombieType.TANK:
			return "坦克"
		Zombie.ZombieType.EXPLODER:
			return "爆炸"
		Zombie.ZombieType.INVISIBLE:
			return "隐身"
		Zombie.ZombieType.SPITTER:
			return "喷毒"
		Zombie.ZombieType.HEALER:
			return "治疗"
		_:
			return "未知"

func _on_zombie_died(_zombie: Zombie):
	zombies_alive -= 1
	
	# 根据僵尸类型计算得分
	var zombie_points = _get_zombie_points(_zombie.zombie_type)
	if _zombie.is_elite:
		zombie_points = int(zombie_points * 1.5)  # 精英僵尸加成
	
	score += zombie_points
	print("僵尸死亡，剩余: " + str(zombies_alive) + " | 本回合得分: " + str(score))
	if SaveManager and SaveManager.instance:
		SaveManager.add_kill()
	
	# 更新UI显示
	if ui_manager:
		ui_manager.update_zombies_display(_get_remaining_zombies(), zombies_to_spawn)
		ui_manager.update_score_display(score)
		ui_manager.add_combo()
		ui_manager.show_kill_effect(_zombie.global_position, zombie_points)

func _get_zombie_points(zombie_type: Zombie.ZombieType) -> int:
	match zombie_type:
		Zombie.ZombieType.BASIC:
			return 10
		Zombie.ZombieType.FAST:
			return 15
		Zombie.ZombieType.TANK:
			return 25
		Zombie.ZombieType.EXPLODER:
			return 30
		Zombie.ZombieType.INVISIBLE:
			return 35
		Zombie.ZombieType.SPITTER:
			return 40
		Zombie.ZombieType.HEALER:
			return 50  # 优先击杀目标，给予更高分数
		_:
			return 10

func end_round():
	current_state = GameState.ROUND_TRANSITION
	round_ended.emit(current_round)
	
	print("第 " + str(current_round) + " 回合结束！")
	if ui_manager:
		ui_manager.show_round_banner("回合完成")
	
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
	
	print("游戏结束！坚持了 " + str(current_round) + " 回合，得分: " + str(score))
	
	if SaveManager and SaveManager.instance:
		SaveManager.save_high_score(score)
		SaveManager.add_rounds_survived(current_round)

func get_current_round() -> int:
	return current_round

func get_zombies_remaining() -> int:
	return zombies_alive

func _get_remaining_zombies() -> int:
	return max(0, zombies_to_spawn - zombies_spawned + zombies_alive)
