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

# ═══════════════════════════════════════════════
# 导出参数 — 可在编辑器调节
# ═══════════════════════════════════════════════
@export var zombies_per_round_base: int = 3
@export var zombies_increase_per_round: int = 1
@export var zombies_max_per_round: int = 40

@export var spawn_interval: float = 2.0
@export var spawn_interval_min: float = 0.8
@export var spawn_interval_decay: float = 0.06

@export var round_multiplier_base: float = 0.04
@export var elite_chance_base: float = 0.02
@export var elite_chance_per_round: float = 0.010
@export var elite_chance_max: float = 0.25

@export var heal_between_rounds: bool = true
@export var heal_percent: float = 0.20

# ═══════════════════════════════════════════════
# 运行时状态
# ═══════════════════════════════════════════════
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

var _edge_indicator_timer: float = 0.0

# 僵尸类型名称（日志用）
const _TYPE_NAMES: Dictionary = {
	0: "基础", 1: "快速", 2: "坦克", 3: "爆炸",
	4: "隐身", 5: "喷毒", 6: "治疗"
}

# 僵尸计分表
const _TYPE_SCORE: Dictionary = {
	0: 10, 1: 15, 2: 25, 3: 30,
	4: 35, 5: 40, 6: 50
}

# 僵尸出场概率表 — 按回合阶段分层
const _SPAWN_TABLE: Array = [
	{ "max_round": 2,  "weights": { 0: 1.0 } },
	{ "max_round": 4,  "weights": { 0: 0.75, 1: 0.25 } },
	{ "max_round": 6,  "weights": { 0: 0.45, 1: 0.30, 2: 0.15, 3: 0.10 } },
	{ "max_round": 10, "weights": { 0: 0.25, 1: 0.20, 2: 0.20, 3: 0.15, 4: 0.10, 5: 0.10 } },
	{ "max_round": 9999, "weights": { 0: 0.15, 1: 0.15, 2: 0.20, 3: 0.15, 4: 0.13, 5: 0.12, 6: 0.10 } },
]

# ═══════════════════════════════════════════════
# 生命周期
# ═══════════════════════════════════════════════

func _ready():
	if ResourceLoader.exists("res://scenes/Zombie.tscn"):
		zombie_scene = preload("res://scenes/Zombie.tscn")

	_update_spawn_points()
	get_viewport().size_changed.connect(_on_viewport_size_changed)

	await get_tree().process_frame

	if ui_manager:
		while ui_manager.welcome_visible:
			await get_tree().process_frame

	if SaveManager and SaveManager.instance:
		high_score = SaveManager.get_high_score()
		SaveManager.add_game_played()
		print("最高分: %d" % high_score)

	start_new_round()

func _process(delta):
	if current_state == GameState.COMBAT:
		_handle_combat(delta)

# ═══════════════════════════════════════════════
# 战斗循环
# ═══════════════════════════════════════════════

func _handle_combat(delta: float):
	# 生成僵尸
	if zombies_spawned < zombies_to_spawn:
		spawn_timer -= delta
		if spawn_timer <= 0:
			spawn_zombie()
			spawn_timer = current_spawn_interval

	# 屏幕外指示器
	_edge_indicator_timer += delta
	if _edge_indicator_timer >= GameConfig.EDGE_INDICATOR_INTERVAL and player and ui_manager:
		_edge_indicator_timer = 0.0
		_update_edge_indicators()

	# 检查回合结束
	if zombies_spawned >= zombies_to_spawn and zombies_alive <= 0:
		end_round()

func _update_edge_indicators():
	var screen_size := get_viewport().get_visible_rect().size
	var screen_rect := Rect2(Vector2.ZERO, screen_size).grow(-20)
	var offscreen_dir := Vector2.ZERO
	var offscreen_count := 0

	for zombie in get_tree().get_nodes_in_group("zombies"):
		if zombie is Zombie and is_instance_valid(zombie):
			if not screen_rect.has_point(zombie.global_position):
				offscreen_dir += (zombie.global_position - player.global_position).normalized()
				offscreen_count += 1

	if offscreen_count > GameConfig.EDGE_INDICATOR_MIN_COUNT and offscreen_dir.length() > 0.5:
		var danger := clampf(float(offscreen_count) / 10.0, 0.0, 1.0)
		ui_manager.show_offscreen_indicator(offscreen_dir.normalized(), danger)

# ═══════════════════════════════════════════════
# 回合管理
# ═══════════════════════════════════════════════

func start_new_round():
	current_round += 1
	current_state = GameState.WEAPON_SELECTION

	# 计算本回合僵尸数量（平滑增长）
	var round_factor := sqrt(float(current_round)) * 1.5
	zombies_to_spawn = zombies_per_round_base + int(round_factor) + (current_round - 1) * zombies_increase_per_round
	zombies_to_spawn = min(zombies_to_spawn, zombies_max_per_round)
	zombies_spawned = 0
	zombies_alive = 0

	# 生成间隔衰减
	current_spawn_interval = max(spawn_interval_min, spawn_interval * pow(0.94, max(0, current_round - 1)))
	spawn_timer = current_spawn_interval

	# 回合间恢复
	if heal_between_rounds and player and current_round > 1:
		var heal_amount := int(player.max_health * heal_percent)
		player.heal(heal_amount)
		if ui_manager:
			ui_manager.show_damage_number(player.global_position, -heal_amount, false)

	round_started.emit(current_round)
	weapon_selection_started.emit()

	# 更新 UI
	if ui_manager:
		ui_manager.update_round_display(current_round)
		ui_manager.update_zombies_display(_get_remaining_zombies(), zombies_to_spawn)
		ui_manager.show_weapon_selection()
		if player:
			ui_manager.update_health_display(player.current_health, player.max_health)

	# 背景紧张度
	var bg := get_tree().current_scene.get_node_or_null("Background")
	if bg and bg.has_method("set_tension"):
		bg.set_tension(clampf(float(current_round - 1) / 15.0, 0.0, 1.0))
	if current_round % 5 == 0 and ui_manager:
		ui_manager.trigger_boss_atmosphere()

	print("第 %d 回合开始！需要消灭 %d 只僵尸" % [current_round, zombies_to_spawn])

func end_round():
	current_state = GameState.ROUND_TRANSITION
	round_ended.emit(current_round)

	print("第 %d 回合结束！" % current_round)
	if ui_manager:
		ui_manager.show_round_banner("回合完成")

	await get_tree().create_timer(GameConfig.ROUND_TRANSITION_DELAY).timeout
	start_new_round()

# ═══════════════════════════════════════════════
# 武器选择
# ═══════════════════════════════════════════════

func on_weapon_selected(weapon: WeaponCard):
	if current_state != GameState.WEAPON_SELECTION:
		return
	if player:
		player.apply_weapon_effect(weapon)
		if ui_manager:
			ui_manager.update_health_display(player.current_health, player.max_health)
			ui_manager.update_player_status(player)

	current_state = GameState.COMBAT
	weapon_selection_ended.emit()
	if ui_manager:
		ui_manager.hide_weapon_selection()
	print("选择了武器: %s，效果已生效！" % weapon.card_name)

func on_upgrade_skipped():
	if current_state != GameState.WEAPON_SELECTION:
		return
	current_state = GameState.COMBAT
	weapon_selection_ended.emit()
	if ui_manager:
		ui_manager.hide_weapon_selection()
	print("跳过升级！直接战斗！")

# ═══════════════════════════════════════════════
# 僵尸生成
# ═══════════════════════════════════════════════

func spawn_zombie():
	if not zombie_scene:
		push_warning("僵尸场景未加载")
		return

	var zombie_instance := zombie_scene.instantiate() as Zombie
	if not zombie_instance:
		push_warning("无法创建僵尸实例")
		return

	# 类型选择（数据驱动）
	var zombie_type := _pick_zombie_type()
	zombie_instance.zombie_type = zombie_type

	# 精英概率
	var elite_chance: float = min(elite_chance_max, elite_chance_base + elite_chance_per_round * float(max(0, current_round - 1)))
	if randf() < elite_chance:
		zombie_instance.set_elite(true)

	# 回合难度缩放
	var round_mult := 1.0 + log(1.0 + float(current_round)) * round_multiplier_base * 2.0
	zombie_instance.apply_round_scaling(current_round, round_mult)

	# 随机生成位置
	if spawn_points.is_empty():
		_update_spawn_points()
	var spawn_pos := spawn_points[randi() % spawn_points.size()]
	var offset := Vector2(randf_range(-GameConfig.SPAWN_OFFSET_RANGE, GameConfig.SPAWN_OFFSET_RANGE),
		randf_range(-GameConfig.SPAWN_OFFSET_RANGE, GameConfig.SPAWN_OFFSET_RANGE))
	zombie_instance.global_position = spawn_pos + offset

	zombie_instance.set_player_target(player)
	zombie_instance.died.connect(_on_zombie_died)
	get_tree().current_scene.add_child(zombie_instance)

	zombies_spawned += 1
	zombies_alive += 1

	if ui_manager:
		ui_manager.update_zombies_display(_get_remaining_zombies(), zombies_to_spawn)

	var type_name: String = _TYPE_NAMES.get(zombie_type, "未知")
	print("生成僵尸 %d/%d (类型: %s, 强度: x%.2f)" % [zombies_spawned, zombies_to_spawn, type_name, round_mult])

func _pick_zombie_type() -> Zombie.ZombieType:
	## 从 _SPAWN_TABLE 中按当前回合数选择概率表，加权随机选取
	var weights: Dictionary = {}
	for entry in _SPAWN_TABLE:
		if current_round <= entry["max_round"]:
			weights = entry["weights"]
			break

	if weights.is_empty():
		return Zombie.ZombieType.BASIC

	# 归一化权重累加
	var total := 0.0
	for w in weights.values():
		total += w

	var roll := randf() * total
	var cumulative := 0.0
	for type_id in weights:
		cumulative += weights[type_id]
		if roll <= cumulative:
			return type_id as Zombie.ZombieType

	# 兜底
	return Zombie.ZombieType.BASIC

# ═══════════════════════════════════════════════
# 僵尸死亡回调
# ═══════════════════════════════════════════════

func _on_zombie_died(_zombie: Zombie):
	zombies_alive -= 1

	var base_pts: int = int(_TYPE_SCORE.get(_zombie.zombie_type, 10))
	var pts: int = int(base_pts * GameConfig.ELITE_SCORE_MULT) if _zombie.is_elite else base_pts
	score += pts

	if SaveManager and SaveManager.instance:
		SaveManager.add_kill()

	if ui_manager:
		ui_manager.update_zombies_display(_get_remaining_zombies(), zombies_to_spawn)
		ui_manager.update_score_display(score)
		ui_manager.add_combo()
		ui_manager.show_kill_effect(_zombie.global_position, pts)

# ═══════════════════════════════════════════════
# 玩家死亡
# ═══════════════════════════════════════════════

func _on_player_died():
	current_state = GameState.GAME_OVER
	game_over.emit()

	# 冻结所有僵尸
	for zombie in get_tree().get_nodes_in_group("zombies"):
		if is_instance_valid(zombie):
			zombie.set_physics_process(false)
			zombie.set_process(false)

	# 停止生成
	spawn_timer = 999999.0
	zombies_to_spawn = 0

	print("游戏结束！坚持了 %d 回合，得分: %d" % [current_round, score])

	if SaveManager and SaveManager.instance:
		SaveManager.save_high_score(score)
		SaveManager.add_rounds_survived(current_round)

# ═══════════════════════════════════════════════
# 生成点
# ═══════════════════════════════════════════════

func _update_spawn_points():
	spawn_points.clear()
	var screen_size := get_viewport().get_visible_rect().size
	var margin: float = GameConfig.SPAWN_MARGIN
	var top_margin: float = GameConfig.SPAWN_TOP_MARGIN_FALLBACK
	if ui_manager and ui_manager.hud_panel:
		top_margin = ui_manager.hud_panel.size.y + 10.0

	var play_height: float = max(0.0, screen_size.y - top_margin)
	var top_edge: float = top_margin + margin
	var bottom_edge: float = top_margin + play_height - margin
	var left_edge: float = margin
	var right_edge: float = screen_size.x - margin

	# 上边 5 点
	for i in range(5):
		spawn_points.append(Vector2(i * screen_size.x / 4, top_edge))
	# 下边 5 点
	for i in range(5):
		spawn_points.append(Vector2(i * screen_size.x / 4, bottom_edge))
	# 左边 4 点
	for i in range(4):
		spawn_points.append(Vector2(left_edge, top_margin + i * play_height / 3))
	# 右边 4 点
	for i in range(4):
		spawn_points.append(Vector2(right_edge, top_margin + i * play_height / 3))

func _on_viewport_size_changed():
	_update_spawn_points()

# ═══════════════════════════════════════════════
# 公共 API
# ═══════════════════════════════════════════════

func set_player(player_node: Player):
	player = player_node
	player.died.connect(_on_player_died)

func set_weapon_deck(deck: WeaponDeck):
	weapon_deck = deck

func get_current_round() -> int:
	return current_round

func get_zombies_remaining() -> int:
	return zombies_alive

func _get_remaining_zombies() -> int:
	return max(0, zombies_to_spawn - zombies_spawned + zombies_alive)
