class_name Zombie
extends CharacterBody2D

signal died(zombie: Zombie)

enum ZombieType {
	BASIC,     # 基础僵尸
	FAST,      # 快速僵尸
	TANK,      # 坦克僵尸
	EXPLODER   # 爆炸僵尸
}

@export var zombie_type: ZombieType = ZombieType.BASIC
@export var max_health: int = 30
@export var damage: int = 10
@export var speed: float = 50.0
@export var attack_range: float = 40.0

var current_health: int
var player: Player
var is_frozen: bool = false
var freeze_timer: float = 0.0
var poison_timer: float = 0.0
var poison_damage: float = 0.0

var sprite: Sprite2D
var collision: CollisionShape2D
var health_label: Label
var attack_label: Label
@onready var attack_timer: Timer = $AttackTimer

func _ready():
	current_health = max_health
	
	# 添加到僵尸组
	add_to_group("zombies")
	
	# 根据僵尸类型设置属性
	_setup_zombie_type()
	
	# 创建僵尸精灵
	sprite = Sprite2D.new()
	add_child(sprite)
	_create_zombie_sprite()
	
	# 创建碰撞形状
	collision = CollisionShape2D.new()
	var shape = RectangleShape2D.new()
	shape.size = Vector2(24, 24)
	collision.shape = shape
	add_child(collision)
	
	# 连接攻击计时器信号（AttackTimer已在场景中存在）
	if attack_timer:
		attack_timer.wait_time = 1.0
		attack_timer.timeout.connect(_on_attack_timer_timeout)
	
	# 创建血量和攻击力显示标签
	_create_status_labels()

func _setup_zombie_type():
	match zombie_type:
		ZombieType.BASIC:
			max_health = 30
			damage = 10
			speed = 50.0
		ZombieType.FAST:
			max_health = 20
			damage = 8
			speed = 100.0
		ZombieType.TANK:
			max_health = 80
			damage = 15
			speed = 25.0
		ZombieType.EXPLODER:
			max_health = 25
			damage = 20
			speed = 75.0
	
	current_health = max_health

func apply_round_scaling(round_number: int, health_multiplier: float):
	"""根据回合数应用难度缩放"""
	# 应用血量倍数
	max_health = int(max_health * health_multiplier)
	current_health = max_health
	
	# 根据回合数增加伤害（每回合增加10%）
	var damage_multiplier = 1.0 + (round_number - 1) * 0.1
	damage = int(damage * damage_multiplier)
	
	# 稍微增加移动速度（每回合增加5%，但有上限）
	var speed_multiplier = 1.0 + min((round_number - 1) * 0.05, 0.5)  # 最多增加50%
	speed = speed * speed_multiplier
	
	print("僵尸缩放 - 回合: " + str(round_number) + ", 血量: " + str(max_health) + ", 伤害: " + str(damage) + ", 速度: " + str(speed))
	
	# 更新状态标签
	_update_status_labels()

func _create_status_labels():
	# 创建血量标签
	health_label = Label.new()
	health_label.add_theme_font_size_override("font_size", 10)
	health_label.add_theme_color_override("font_color", Color.RED)
	health_label.position = Vector2(-15, -45)  # 僵尸头部上方
	health_label.z_index = 10
	add_child(health_label)
	
	# 创建攻击力标签
	attack_label = Label.new()
	attack_label.add_theme_font_size_override("font_size", 10)
	attack_label.add_theme_color_override("font_color", Color.ORANGE)
	attack_label.position = Vector2(-15, -35)  # 血量标签下方
	attack_label.z_index = 10
	add_child(attack_label)
	
	_update_status_labels()

func _update_status_labels():
	if health_label:
		health_label.text = "❤️" + str(current_health) + "/" + str(max_health)
	if attack_label:
		attack_label.text = "⚔️" + str(damage)

func _create_zombie_sprite():
	var base_color = Color.GREEN
	var size = 30
	
	match zombie_type:
		ZombieType.BASIC:
			base_color = Color(0.4, 0.7, 0.3)  # 绿色
			size = 30
		ZombieType.FAST:
			base_color = Color(0.9, 0.9, 0.3)  # 黄色
			size = 25
		ZombieType.TANK:
			base_color = Color(0.7, 0.3, 0.3)  # 红色
			size = 40
		ZombieType.EXPLODER:
			base_color = Color(0.7, 0.3, 0.7)  # 紫色
			size = 28
	
	var image = Image.create(size, size, false, Image.FORMAT_RGBA8)
	
	# 绘制僵尸角色
	for y in range(size):
		for x in range(size):
			var color = Color.TRANSPARENT
			var center_x = size / 2.0
			var center_y = size / 2.0
			
			# 头部 (圆形)
			if _is_zombie_in_circle(x, y, center_x, center_y * 0.6, size * 0.25):
				color = base_color
			
			# 身体 (椭圆)
			elif _is_zombie_in_ellipse(x, y, center_x, center_y * 1.2, size * 0.2, size * 0.3):
				color = base_color.darkened(0.2)
			
			# 手臂 (简化)
			elif (x >= center_x - size * 0.4 and x <= center_x - size * 0.3 and y >= center_y * 0.8 and y <= center_y * 1.4) or \
				 (x >= center_x + size * 0.3 and x <= center_x + size * 0.4 and y >= center_y * 0.8 and y <= center_y * 1.4):
				color = base_color.darkened(0.1)
			
			# 腿部
			elif (x >= center_x - size * 0.15 and x <= center_x - size * 0.05 and y >= center_y * 1.5 and y <= size - 2) or \
				 (x >= center_x + size * 0.05 and x <= center_x + size * 0.15 and y >= center_y * 1.5 and y <= size - 2):
				color = base_color.darkened(0.3)
			
			# 眼睛 (红色)
			elif (x == int(center_x - size * 0.1) or x == int(center_x + size * 0.1)) and y == int(center_y * 0.5):
				color = Color.RED
			
			# 嘴巴 (黑色)
			elif x >= center_x - size * 0.08 and x <= center_x + size * 0.08 and y == int(center_y * 0.7):
				color = Color.BLACK
			
			# 特殊效果
			if zombie_type == ZombieType.EXPLODER and _is_zombie_in_circle(x, y, center_x, center_y, size * 0.1):
				color = Color.ORANGE  # 爆炸核心
			elif zombie_type == ZombieType.TANK and (x == 0 or x == size-1 or y == 0 or y == size-1):
				if color != Color.TRANSPARENT:
					color = Color.GRAY  # 装甲边缘
			
			image.set_pixel(x, y, color)
	
	var texture = ImageTexture.new()
	texture.set_image(image)
	sprite.texture = texture

func _is_zombie_in_circle(x: int, y: int, center_x: float, center_y: float, radius: float) -> bool:
	var dx = x - center_x
	var dy = y - center_y
	return dx * dx + dy * dy <= radius * radius

func _is_zombie_in_ellipse(x: int, y: int, center_x: float, center_y: float, radius_x: float, radius_y: float) -> bool:
	var dx = (x - center_x) / radius_x
	var dy = (y - center_y) / radius_y
	return dx * dx + dy * dy <= 1.0

func _physics_process(delta):
	if is_frozen:
		freeze_timer -= delta
		if freeze_timer <= 0:
			is_frozen = false
		return
	
	if poison_timer > 0:
		poison_timer -= delta
		if poison_timer <= 0:
			take_damage(int(poison_damage))
			poison_timer = 1.0  # 每秒造成一次毒伤害
	
	if player:
		_move_towards_player(delta)
		_check_attack_range()

func _move_towards_player(_delta):
	var direction = (player.global_position - global_position).normalized()
	velocity = direction * speed
	move_and_slide()

func _check_attack_range():
	if player and global_position.distance_to(player.global_position) <= attack_range:
		if attack_timer.is_stopped():
			attack_timer.start()

func _on_attack_timer_timeout():
	if player and global_position.distance_to(player.global_position) <= attack_range:
		attack_player()

func attack_player():
	if player:
		player.take_damage(damage)
		print("僵尸攻击玩家，造成 " + str(damage) + " 点伤害")

func take_damage(amount: int):
	current_health -= amount
	current_health = max(0, current_health)
	
	# 更新血量显示
	_update_status_labels()
	
	# 简单的受伤效果
	if sprite:
		var tween = create_tween()
		tween.tween_property(sprite, "modulate", Color.RED, 0.1)
		tween.tween_property(sprite, "modulate", Color.WHITE, 0.1)
	
	if current_health <= 0:
		die()

func die():
	if zombie_type == ZombieType.EXPLODER:
		_explode()
	
	died.emit(self)
	queue_free()

func _explode():
	# 爆炸僵尸死亡时造成范围伤害
	var explosion_range = 100.0
	if player and global_position.distance_to(player.global_position) <= explosion_range:
		player.take_damage(damage * 2)
		print("爆炸僵尸爆炸！对玩家造成 " + str(damage * 2) + " 点伤害")

func apply_freeze(duration: float):
	is_frozen = true
	freeze_timer = duration
	if sprite:
		sprite.modulate = Color.CYAN

func apply_poison(damage_per_second: float, duration: float):
	poison_damage = damage_per_second
	poison_timer = duration
	if sprite:
		sprite.modulate = Color.PURPLE

func set_player_target(target_player: Player):
	player = target_player
