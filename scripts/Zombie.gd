class_name Zombie
extends CharacterBody2D
## 僵尸基类
## 处理僵尸的移动、攻击、状态效果和类型特殊能力

signal died(zombie: Zombie)

## 僵尸类型枚举 - 每种类型有独特的属性和能力
enum ZombieType {
	BASIC,     ## 基础僵尸 - 平衡型，适合新手
	FAST,      ## 快速僵尸 - 低血高速，需要精准打击
	TANK,      ## 坦克僵尸 - 高血高伤低速，需要持续输出
	EXPLODER,  ## 爆炸僵尸 - 死后爆炸，需要保持距离
	INVISIBLE, ## 隐身僵尸 - 周期性隐身，难以预判
	SPITTER,   ## 喷毒僵尸 - 远程中毒攻击，需要优先击杀
	HEALER     ## 治疗僵尸 - 治疗周围僵尸，高优先级目标
}

# ===== 默认常量 =====
const DEFAULT_ATTACK_RANGE: float = 40.0
const POISON_TICK_INTERVAL: float = 1.0
const INVISIBLE_REVEAL_MIN: float = 2.0
const INVISIBLE_REVEAL_MAX: float = 4.0

@export var zombie_type: ZombieType = ZombieType.BASIC
@export var max_health: int = 30
@export var damage: int = 10
@export var speed: float = 50.0
@export var attack_range: float = DEFAULT_ATTACK_RANGE

var current_health: int
var player: Player
var is_frozen: bool = false
var freeze_timer: float = 0.0
var is_stunned: bool = false
var stun_timer: float = 0.0
var slow_multiplier: float = 1.0
var poison_timer: float = 0.0
var poison_damage: float = 0.0

# 魅惑效果
var is_charmed: bool = false
var charm_timer: float = 0.0
var original_player: Player  # 保存原始玩家引用

var invisible_timer: float = 0.0
var is_revealed: bool = false
var spitter_cooldown: float = 0.0
var spitter_cooldown_base: float = 3.0

# 治疗僵尸
var healer_cooldown: float = 0.0
var healer_cooldown_base: float = 2.5
var healer_range: float = 120.0
var healer_amount: int = 8

# 精英状态
var is_elite: bool = false
var elite_multiplier: float = 1.25
var elite_ring: Line2D

var sprite: Sprite2D
var shadow: Sprite2D
var collision: CollisionShape2D
var health_label: Label
var attack_label: Label
@onready var attack_timer: Timer = $AttackTimer

func _ready():
	current_health = max_health

	if is_elite:
		max_health = int(max_health * elite_multiplier)
		current_health = max_health
		damage = int(damage * elite_multiplier)
		speed = speed * 1.05
	
	# 添加到僵尸组
	add_to_group("zombies")
	
	# 根据僵尸类型设置属性
	_setup_zombie_type()
	
	# 创建阴影
	shadow = Sprite2D.new()
	shadow.texture = _create_shadow_texture(28, 10, Color(0, 0, 0, 0.5))
	shadow.position = Vector2(0, 14)
	add_child(shadow)
	
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
	
	# 隐身僵尸初始半透明
	if zombie_type == ZombieType.INVISIBLE:
		if sprite:
			sprite.modulate.a = 0.3

func _setup_zombie_type():
	# 更平衡的僵尸属性设计
	# 设计原则：每种僵尸有明确的特色和对应的应对策略
	match zombie_type:
		ZombieType.BASIC:
			# 基础僵尸：平衡型，适合新手熟悉游戏
			max_health = 25
			damage = 8
			speed = 45.0
		ZombieType.FAST:
			# 快速僵尸：脆皮高速，需要精准打击
			max_health = 15
			damage = 6
			speed = 90.0
		ZombieType.TANK:
			# 坦克僵尸：高血高伤低速，需要持续输出
			max_health = 100
			damage = 18
			speed = 22.0
		ZombieType.EXPLODER:
			# 爆炸僵尸：中等属性，死后爆炸需要保持距离
			max_health = 20
			damage = 12
			speed = 55.0
		ZombieType.INVISIBLE:
			# 隐身僵尸：中等属性，难以预判
			max_health = 30
			damage = 10
			speed = 55.0
		ZombieType.SPITTER:
			# 喷毒僵尸：远程攻击，需要优先击杀
			max_health = 35
			damage = 8
			speed = 35.0
		ZombieType.HEALER:
			# 治疗僵尸：会治疗附近僵尸，需要优先击杀
			max_health = 45
			damage = 5
			speed = 30.0
	
	current_health = max_health

func apply_round_scaling(round_number: int, multiplier: float):
	"""根据回合数应用难度缩放"""
	# 应用属性倍数（血量、伤害、速度）
	max_health = int(float(max_health) * multiplier)
	current_health = max_health
	damage = int(float(damage) * multiplier)
	var speed_multiplier = 1.0 + (multiplier - 1.0) * 0.6
	speed = speed * speed_multiplier
	
	if zombie_type == ZombieType.SPITTER:
		spitter_cooldown_base = max(1.5, 3.0 - round_number * 0.03)
	
	print("僵尸升级 - 回合: " + str(round_number) + 
		", 血量: " + str(max_health) + 
		", 伤害: " + str(damage) + 
		", 速度: " + str(speed) + 
		", 倍数: x" + str(multiplier))
	
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
	var size = 36  # 增大基础尺寸
	
	match zombie_type:
		ZombieType.BASIC:
			base_color = Color(0.4, 0.7, 0.3)  # 腐烂绿色
			size = 36
		ZombieType.FAST:
			base_color = Color(0.9, 0.7, 0.2)  # 暗黄色
			size = 30
		ZombieType.TANK:
			base_color = Color(0.8, 0.2, 0.2)  # 血红色
			size = 48
		ZombieType.EXPLODER:
			base_color = Color(0.8, 0.2, 0.8)  # 紫红色
			size = 34
		ZombieType.INVISIBLE:
			base_color = Color(0.3, 0.6, 0.7)  # 鬼魂青色
			size = 36
		ZombieType.SPITTER:
			base_color = Color(0.2, 0.8, 0.2)  # 剧毒绿色
			size = 40
		ZombieType.HEALER:
			base_color = Color(0.2, 0.9, 0.4)  # 荧光绿色
			size = 38
	
	var image = Image.create(size, size, false, Image.FORMAT_RGBA8)
	
	# 绘制增强版僵尸角色
	for y in range(size):
		for x in range(size):
			var color = Color.TRANSPARENT
			var center_x = size / 2.0
			var center_y = size / 2.0
			
			# 头部 (不规则圆形，表示腐烂)
			var head_dist = Vector2(x - center_x, y - center_y * 0.55).length()
			var head_radius = size * 0.22
			if head_dist <= head_radius:
				# 头部主体
				color = base_color
				# 添加腐烂痕迹
				if head_dist > head_radius * 0.6:
					if randf() < 0.3:  # 随机腐烂点
						color = base_color.darkened(0.4)
				
				# 眼睛位置
				var left_eye = Vector2(center_x - size * 0.08, center_y * 0.5)
				var right_eye = Vector2(center_x + size * 0.08, center_y * 0.5)
				if Vector2(x - left_eye.x, y - left_eye.y).length() <= size * 0.04:
					# 邪恶的红眼睛
					color = Color.RED
					if Vector2(x - left_eye.x, y - left_eye.y).length() <= size * 0.02:
						color = Color(1.0, 0.2, 0.2)  # 眼睛中心
				elif Vector2(x - right_eye.x, y - right_eye.y).length() <= size * 0.04:
					color = Color.RED
					if Vector2(x - right_eye.x, y - right_eye.y).length() <= size * 0.02:
						color = Color(1.0, 0.2, 0.2)
				
				# 嘴巴 (邪恶的笑容)
				if y >= center_y * 0.65 and y <= center_y * 0.75:
					var mouth_width = size * 0.15
					if x >= center_x - mouth_width and x <= center_x + mouth_width:
						if y == int(center_y * 0.7):
							color = Color.BLACK
						elif abs(x - center_x) <= mouth_width * 0.6:
							color = Color(0.1, 0, 0)  # 深红口腔
			
			# 身体 (瘦长，怪异姿态)
			elif y >= center_y * 0.8 and y <= center_y * 1.4:
				var body_width = size * 0.18 - abs(y - center_y * 1.1) * 0.02
				if x >= center_x - body_width and x <= center_x + body_width:
					color = base_color.darkened(0.15)
					# 添加肋骨效果
					if y >= center_y * 0.9 and y <= center_y * 1.2:
						if abs(x - center_x) <= body_width * 0.8 and int(y) % 3 == 0:
							color = base_color.darkened(0.35)
			
			# 手臂 (骨瘦如柴，不规则)
			elif (y >= center_y * 0.9 and y <= center_y * 1.5):
				# 左臂
				var left_arm_x = center_x - size * 0.35 + sin(y * 0.3) * 2
				if abs(x - left_arm_x) <= size * 0.06:
					color = base_color.darkened(0.25)
					# 骨节突出
					if int(y) % 4 == 0:
						color = base_color.darkened(0.4)
				
				# 右臂
				var right_arm_x = center_x + size * 0.32 + sin(y * 0.3 + PI) * 2
				if abs(x - right_arm_x) <= size * 0.06:
					color = base_color.darkened(0.25)
					if int(y) % 4 == 0:
						color = base_color.darkened(0.4)
			
			# 腿部 (扭曲变形)
			elif y >= center_y * 1.4:
				# 左腿 (倾斜)
				var left_leg_x = center_x - size * 0.12 + (y - center_y * 1.4) * 0.08
				if abs(x - left_leg_x) <= size * 0.08:
					color = base_color.darkened(0.3)
					# 膝盖骨节
					if y >= center_y * 1.65 and y <= center_y * 1.7:
						color = base_color.darkened(0.5)
				
				# 右腿 (反向倾斜)
				var right_leg_x = center_x + size * 0.1 - (y - center_y * 1.4) * 0.06
				if abs(x - right_leg_x) <= size * 0.08:
					color = base_color.darkened(0.3)
					if y >= center_y * 1.65 and y <= center_y * 1.7:
						color = base_color.darkened(0.5)
			
			# 特殊类型效果
			match zombie_type:
				ZombieType.EXPLODER:
					# 爆炸核心 (发光)
					var core_dist = Vector2(x - center_x, y - center_y * 1.2).length()
					if core_dist <= size * 0.08:
						var pulse = sin(Time.get_ticks_msec() * 0.01) * 0.3 + 0.7
						color = Color.ORANGE * pulse
					elif core_dist <= size * 0.12:
						color = base_color.lightened(0.3)
				
				ZombieType.TANK:
					# 重装甲板
					if y <= size * 0.15 or y >= size * 0.85 or x <= size * 0.1 or x >= size * 0.9:
						if color != Color.TRANSPARENT:
							color = Color.GRAY.darkened(0.2)
					# 金属铆钉
					if ((x <= size * 0.15 or x >= size * 0.85) and int(y) % 6 == 3) or \
					   ((y <= size * 0.15 or y >= size * 0.85) and int(x) % 6 == 3):
						color = Color.DARK_GRAY
				
				ZombieType.FAST:
					# 流线型身体
					if color != Color.TRANSPARENT:
						var speed_factor = 1.0 + sin(Time.get_ticks_msec() * 0.02) * 0.2
						color = color.lightened(speed_factor * 0.2)
					# 速度线条
					if x <= size * 0.05 or x >= size * 0.95:
						if y % 3 == 0:
							color = Color.YELLOW.lightened(0.3)
				
				ZombieType.INVISIBLE:
					# 半透明鬼魂效果
					if color != Color.TRANSPARENT:
						var ghost_alpha = 0.6 + sin(Time.get_ticks_msec() * 0.005) * 0.2
						color.a *= ghost_alpha
					# 鬼魂光环
					var aura_dist = Vector2(x - center_x, y - center_y).length()
					if aura_dist >= size * 0.4 and aura_dist <= size * 0.45:
						color = Color.CYAN * 0.3
				
				ZombieType.SPITTER:
					# 毒液袋 (绿色脓包)
					var sac_dist = Vector2(x - center_x, y - center_y * 1.3).length()
					if sac_dist <= size * 0.06:
						color = Color(0.2, 1.0, 0.2)
					# 毒液管道
					elif abs(x - center_x) <= size * 0.02 and y >= center_y * 1.2 and y <= center_y * 1.4:
						color = Color(0.1, 0.8, 0.1)
				
				ZombieType.HEALER:
					# 治疗光环
					var heal_dist = Vector2(x - center_x, y - center_y).length()
					if heal_dist <= size * 0.5:
						var heal_pulse = sin(Time.get_ticks_msec() * 0.008) * 0.2 + 0.8
						if color != Color.TRANSPARENT:
							color = color.lightened(heal_pulse * 0.3)
					# 十字标记
					if abs(x - center_x) <= size * 0.02 and y >= center_y * 0.2 and y <= center_y * 0.4:
						color = Color.WHITE
					elif abs(y - center_y * 0.3) <= size * 0.02 and x >= center_x * 0.8 and x <= center_x:
						color = Color.WHITE
			
			image.set_pixel(x, y, color)
	
	var texture = ImageTexture.new()
	texture.set_image(image)
	sprite.texture = texture
	
	# 添加僵尸类型特有的动画
	_add_zombie_animation()
	
	if is_elite:
		_create_elite_ring(size * 0.7, base_color.lightened(0.4))

func _add_zombie_animation():
	# 根据僵尸类型添加特定动画
	match zombie_type:
		ZombieType.EXPLODER:
			# 爆炸僵尸的脉动效果
			var pulse_tween = create_tween()
			pulse_tween.set_loops()
			pulse_tween.tween_property(sprite, "scale", Vector2(1.1, 1.1), 1.5).set_trans(Tween.TRANS_SINE)
			pulse_tween.tween_property(sprite, "scale", Vector2(0.9, 0.9), 1.5).set_trans(Tween.TRANS_SINE)
		
		ZombieType.FAST:
			# 快速僵尸的抖动效果
			var shake_tween = create_tween()
			shake_tween.set_loops()
			shake_tween.tween_property(sprite, "position:x", 2, 0.1)
			shake_tween.tween_property(sprite, "position:x", -2, 0.1)
			shake_tween.tween_property(sprite, "position:x", 0, 0.1)
		
		ZombieType.TANK:
			# 坦克僵尸的重压效果
			var weight_tween = create_tween()
			weight_tween.set_loops()
			weight_tween.tween_property(sprite, "position:y", 2, 2.0).set_trans(Tween.TRANS_SINE)
			weight_tween.tween_property(sprite, "position:y", 0, 2.0).set_trans(Tween.TRANS_SINE)
		
		ZombieType.HEALER:
			# 治疗僵尸的旋转光环
			var heal_tween = create_tween()
			heal_tween.set_loops()
			heal_tween.tween_property(sprite, "rotation", PI * 2, 4.0)
		
		_:  # BASIC, INVISIBLE, SPITTER
			# 基础僵尸的摇摆效果
			var sway_tween = create_tween()
			sway_tween.set_loops()
			sway_tween.tween_property(sprite, "rotation", deg_to_rad(5), 3.0).set_trans(Tween.TRANS_SINE)
			sway_tween.tween_property(sprite, "rotation", deg_to_rad(-5), 3.0).set_trans(Tween.TRANS_SINE)

func _create_elite_ring(radius: float, color: Color):
	elite_ring = Line2D.new()
	var points = 24
	for i in range(points + 1):
		var angle = i * 2.0 * PI / points
		elite_ring.add_point(Vector2(cos(angle), sin(angle)) * radius)
	elite_ring.width = 2.0
	elite_ring.default_color = color
	elite_ring.z_index = -1
	add_child(elite_ring)

func _create_shadow_texture(width: int, height: int, color: Color) -> Texture2D:
	var image = Image.create(width, height, false, Image.FORMAT_RGBA8)
	var cx = width / 2.0
	var cy = height / 2.0
	for y in range(height):
		for x in range(width):
			var dx = (x - cx) / (width * 0.5)
			var dy = (y - cy) / (height * 0.5)
			if dx * dx + dy * dy <= 1.0:
				image.set_pixel(x, y, color)
	var texture = ImageTexture.new()
	texture.set_image(image)
	return texture

func _is_zombie_in_circle(x: int, y: int, center_x: float, center_y: float, radius: float) -> bool:
	var dx = x - center_x
	var dy = y - center_y
	return dx * dx + dy * dy <= radius * radius

func _is_zombie_in_ellipse(x: int, y: int, center_x: float, center_y: float, radius_x: float, radius_y: float) -> bool:
	var dx = (x - center_x) / radius_x
	var dy = (y - center_y) / radius_y
	return dx * dx + dy * dy <= 1.0

func _physics_process(delta):
	if is_stunned:
		stun_timer -= delta
		if stun_timer <= 0:
			is_stunned = false
			if sprite:
				sprite.modulate = Color.WHITE
		return
	
	if is_frozen:
		freeze_timer -= delta
		if freeze_timer <= 0:
			is_frozen = false
			slow_multiplier = 1.0
			if sprite:
				sprite.modulate = Color.WHITE
	
	if is_charmed:
		charm_timer -= delta
		if charm_timer <= 0:
			is_charmed = false
			player = original_player  # 恢复攻击玩家
	
	if poison_timer > 0:
		poison_timer -= delta
		if poison_timer <= 0:
			take_damage(int(poison_damage))
			poison_timer = 1.0
	
	# 隐身僵尸逻辑
	if zombie_type == ZombieType.INVISIBLE:
		_handle_invisible_zombie(delta)
	
	# 喷毒僵尸逻辑
	if zombie_type == ZombieType.SPITTER:
		_handle_spitter_zombie(delta)
	
	# 治疗僵尸逻辑
	if zombie_type == ZombieType.HEALER:
		_handle_healer_zombie(delta)
	
	if player:
		_move_towards_player(delta)
		_check_attack_range()

func _move_towards_player(_delta):
	if is_charmed:
		# 魅惑时攻击其他僵尸
		var nearest_zombie = _find_nearest_zombie(600.0)
		if nearest_zombie:
			var direction = (nearest_zombie.global_position - global_position).normalized()
			velocity = direction * speed
		else:
			velocity = Vector2.ZERO
	else:
		var direction = (player.global_position - global_position).normalized()
		velocity = direction * speed * slow_multiplier
	move_and_slide()

func _handle_invisible_zombie(delta):
	# 隐身僵尸周期性显形
	invisible_timer -= delta
	if invisible_timer <= 0:
		invisible_timer = randf_range(INVISIBLE_REVEAL_MIN, INVISIBLE_REVEAL_MAX)
		is_revealed = !is_revealed
		if sprite:
			sprite.modulate.a = 1.0 if is_revealed else 0.3

func _handle_spitter_zombie(delta):
	# 喷毒僵尸远程攻击
	spitter_cooldown -= delta
	if spitter_cooldown <= 0:
		spitter_cooldown = spitter_cooldown_base
		if player and global_position.distance_to(player.global_position) <= 200:
			_spitt_poison(player)

func _spitt_poison(target):
	# 创建毒液弹
	var poison_projectile = Sprite2D.new()
	var poison_image = Image.create(10, 10, false, Image.FORMAT_RGBA8)
	poison_image.fill(Color.GREEN)
	var poison_texture = ImageTexture.new()
	poison_texture.set_image(poison_image)
	poison_projectile.texture = poison_texture
	poison_projectile.global_position = global_position
	get_parent().add_child(poison_projectile)
	
	# 飞向玩家
	var _direction = (target.global_position - global_position).normalized()
	var move_tween = create_tween()
	move_tween.tween_property(poison_projectile, "global_position", target.global_position, 0.5)
	move_tween.tween_callback(_complete_poison_spit.bind(target, poison_projectile))

func _handle_healer_zombie(delta):
	healer_cooldown -= delta
	if healer_cooldown <= 0:
		healer_cooldown = healer_cooldown_base
		_heal_nearby_zombies()

func _heal_nearby_zombies():
	var zombies = get_tree().get_nodes_in_group("zombies")
	var healed_any = false
	
	for zombie in zombies:
		if zombie is Zombie and zombie != self:
			var dist = global_position.distance_to(zombie.global_position)
			if dist <= healer_range and zombie.current_health < zombie.max_health:
				zombie.current_health = min(zombie.max_health, zombie.current_health + healer_amount)
				zombie._update_status_labels()
				healed_any = true
				
				# 治疗视觉效果
				_create_heal_effect(zombie.global_position)
	
	if healed_any:
		# 治疗者自身的光环效果
		_create_heal_pulse()

func _create_heal_effect(target_pos: Vector2):
	var heal_label = Label.new()
	heal_label.text = "+" + str(healer_amount)
	heal_label.global_position = target_pos + Vector2(randf_range(-10, 10), -20)
	heal_label.add_theme_font_size_override("font_size", 12)
	heal_label.add_theme_color_override("font_color", Color(0.3, 1.0, 0.5))
	heal_label.add_theme_constant_override("outline_size", 2)
	heal_label.add_theme_color_override("font_outline_color", Color.BLACK)
	heal_label.z_index = 15
	get_parent().add_child(heal_label)
	
	var tween = heal_label.create_tween()
	tween.parallel().tween_property(heal_label, "position:y", heal_label.position.y - 25, 0.5)
	tween.parallel().tween_property(heal_label, "modulate:a", 0.0, 0.5).set_delay(0.2)
	tween.tween_callback(heal_label.queue_free)

func _create_heal_pulse():
	# 创建治疗脉冲圆环
	var ring = Node2D.new()
	ring.global_position = global_position
	
	var line = Line2D.new()
	var points = 20
	for i in range(points + 1):
		var angle = i * 2.0 * PI / points
		var point = Vector2(cos(angle), sin(angle)) * 20.0
		line.add_point(point)
	line.width = 2.0
	line.default_color = Color(0.3, 1.0, 0.5, 0.8)
	ring.add_child(line)
	get_parent().add_child(ring)
	
	var tween = ring.create_tween()
	tween.parallel().tween_property(ring, "scale", Vector2(healer_range / 20.0, healer_range / 20.0), 0.4)
	tween.parallel().tween_property(ring, "modulate:a", 0.0, 0.4)
	tween.tween_callback(ring.queue_free)

func _find_nearest_zombie(max_range: float) -> Zombie:
	var zombies = get_tree().get_nodes_in_group("zombies")
	var nearest: Zombie = null
	var nearest_dist = max_range
	
	for zombie in zombies:
		if zombie is Zombie and zombie != self:
			var dist = global_position.distance_to(zombie.global_position)
			if dist < nearest_dist:
				nearest_dist = dist
				nearest = zombie
	
	return nearest

func _check_attack_range():
	if is_charmed:
		# 魅惑时检查范围内是否有其他僵尸
		var nearest_zombie = _find_nearest_zombie(attack_range)
		if nearest_zombie:
			if attack_timer.is_stopped():
				attack_timer.start()
	elif player and global_position.distance_to(player.global_position) <= attack_range:
		if attack_timer.is_stopped():
			attack_timer.start()

func _on_attack_timer_timeout():
	if is_charmed:
		# 魅惑时检查最近的其他僵尸
		var nearest_zombie = _find_nearest_zombie(attack_range)
		if nearest_zombie:
			attack_zombie(nearest_zombie)
	elif player and global_position.distance_to(player.global_position) <= attack_range:
		attack_player()

func attack_zombie(target: Zombie):
	if target:
		target.take_damage(damage)
		print("魅惑僵尸攻击其他僵尸，造成 " + str(damage) + " 点伤害")

func attack_player():
	if player:
		player.take_damage(damage)
		print("僵尸攻击玩家，造成 " + str(damage) + " 点伤害")

func take_damage(amount: int):
	current_health -= amount
	current_health = max(0, current_health)
	
	# 更新血量显示
	_update_status_labels()
	
	# 增强的受伤效果
	if sprite:
		var tween = create_tween()
		tween.tween_property(sprite, "modulate", Color.RED, 0.05)
		tween.tween_property(sprite, "modulate", Color.WHITE, 0.15)
		
		# 轻微击退视觉效果
		var knockback_dir = Vector2(randf_range(-1, 1), randf_range(-1, 1)).normalized() * 3
		var pos_tween = create_tween()
		pos_tween.tween_property(self, "position", position + knockback_dir, 0.05)
		pos_tween.tween_property(self, "position", position, 0.1)
	
	# 显示伤害数字
	_show_damage_popup(amount)
	
	if SoundManager.instance:
		SoundManager.play_zombie_hit()
	
	if current_health <= 0:
		die()

func _show_damage_popup(amount: int):
	var popup = Label.new()
	popup.text = str(amount)
	popup.global_position = global_position + Vector2(randf_range(-10, 10), -25)
	popup.add_theme_font_size_override("font_size", 14)
	popup.add_theme_color_override("font_color", Color.WHITE)
	popup.add_theme_constant_override("outline_size", 2)
	popup.add_theme_color_override("font_outline_color", Color.BLACK)
	popup.z_index = 15
	get_parent().add_child(popup)
	
	var tween = popup.create_tween()
	tween.parallel().tween_property(popup, "position:y", popup.position.y - 30, 0.5)
	tween.parallel().tween_property(popup, "modulate:a", 0.0, 0.5).set_delay(0.2)
	tween.tween_callback(popup.queue_free)

func die():
	if zombie_type == ZombieType.EXPLODER:
		_explode()
		if SoundManager.instance:
			SoundManager.play_explosion()
	
	# 死亡特效
	_create_death_effect()
	
	died.emit(self)
	queue_free()

func _create_death_effect():
	# 创建死亡粒子效果
	var particles = GPUParticles2D.new()
	particles.global_position = global_position
	particles.emitting = true
	particles.amount = 20
	particles.lifetime = 0.6
	particles.one_shot = true
	
	var mat = ParticleProcessMaterial.new()
	mat.direction = Vector3(0, -1, 0)
	mat.initial_velocity_min = 50.0
	mat.initial_velocity_max = 120.0
	mat.gravity = Vector3(0, 200, 0)
	mat.scale_min = 2.0
	mat.scale_max = 4.0
	
	# 颜色根据僵尸类型
	match zombie_type:
		ZombieType.BASIC:
			mat.color = Color(0.4, 0.7, 0.3)
		ZombieType.FAST:
			mat.color = Color(0.9, 0.9, 0.3)
		ZombieType.TANK:
			mat.color = Color(0.7, 0.3, 0.3)
		ZombieType.EXPLODER:
			mat.color = Color(1.0, 0.5, 0.0)
		ZombieType.INVISIBLE:
			mat.color = Color(0.5, 0.8, 0.8)
		ZombieType.SPITTER:
			mat.color = Color(0.3, 0.9, 0.3)
	
	particles.process_material = mat
	get_parent().add_child(particles)
	
	# 自动清理
	var timer = Timer.new()
	timer.wait_time = 1.0
	timer.one_shot = true
	timer.timeout.connect(particles.queue_free)
	particles.add_child(timer)
	timer.start()

func _explode():
	# 爆炸僵尸死亡时造成范围伤害
	var explosion_range = 100.0
	if player and global_position.distance_to(player.global_position) <= explosion_range:
		player.take_damage(damage * 2)
		print("爆炸僵尸爆炸！对玩家造成 " + str(damage * 2) + " 点伤害")

func apply_freeze(duration: float):
	is_frozen = true
	freeze_timer = duration
	slow_multiplier = 0.4
	if sprite:
		sprite.modulate = Color.CYAN

func apply_stun(duration: float):
	is_stunned = true
	stun_timer = duration
	if sprite:
		sprite.modulate = Color(0.8, 0.8, 1.0)

func apply_poison(damage_per_second: float, duration: float):
	poison_damage = damage_per_second
	poison_timer = duration
	if sprite:
		sprite.modulate = Color.PURPLE

func apply_charm(duration: float):
	# 根据僵尸类型和回合数减少魅惑时间
	var zombie_level_bonus = 0
	match zombie_type:
		ZombieType.TANK:
			zombie_level_bonus = 0.4  # 坦克僵尸更难控制
		ZombieType.FAST:
			zombie_level_bonus = 0.2  # 快速僵尸稍难控制
	
	var actual_duration = duration * (1.0 - zombie_level_bonus)
	actual_duration = max(1.0, actual_duration)  # 至少1秒
	
	is_charmed = true
	charm_timer = actual_duration
	original_player = player
	
	if SoundManager.instance:
		SoundManager.play_charm()
	
	if sprite:
		sprite.modulate = Color.MAGENTA

func set_player_target(target_player: Player):
	player = target_player

func set_elite(enabled: bool):
	is_elite = enabled

func _complete_poison_spit(target: Node, projectile: Node):
	if is_instance_valid(target):
		target.apply_poison(5.0, 3.0)
	if is_instance_valid(projectile):
		projectile.queue_free()
