class_name Player
extends CharacterBody2D

signal health_changed(new_health: int)
signal died

@export var max_health: int = 100
@export var base_damage: float = 10.0
@export var base_speed: float = 200.0

var current_health: int
var current_damage: float
var current_speed: float
var active_effects: Array[WeaponCard] = []

# 冲刺系统
var can_dash: bool = true
var is_dashing: bool = false
var dash_speed_multiplier: float = 3.0
var dash_duration: float = 0.2
var dash_cooldown: float = 1.0
var dash_timer: Timer
var dash_cooldown_timer: Timer

# 环绕武器系统 (Aura)
var aura_active: bool = false
var aura_damage: float = 5.0
var aura_radius: float = 80.0
var aura_tick_timer: float = 0.0
var aura_tick_interval: float = 0.5
var aura_visuals: Node2D

# 武器效果的累积值
var damage_multiplier: float = 1.0
var speed_multiplier: float = 1.0
var multi_shot_count: int = 1
var piercing_count: int = 0
var area_damage: float = 0.0
var life_steal_percent: float = 0.0
var critical_chance: float = 0.0
var freeze_duration: float = 0.0
var poison_damage: float = 0.0

var sprite: Sprite2D
var collision: CollisionShape2D
var attack_area: Area2D
var attack_timer: Timer
var can_attack: bool = true
var attack_cooldown: float = 0.5

# 攻击类型枚举
enum AttackType {
	MELEE,      # 近战攻击
	RANGED,     # 远程射击
	AREA,       # 范围攻击
	MAGIC       # 魔法攻击
}

var current_attack_type: AttackType = AttackType.MELEE
var projectile_scene: PackedScene
var magic_particles: GPUParticles2D

# 子弹系统
var max_ammo: int = 10
var current_ammo: int = 10
var reload_time: float = 2.0
var is_reloading: bool = false
var reload_timer: float = 0.0

# 状态显示标签
var health_display: Label
var ammo_display: Label
var attack_type_display: Label

func _ready():
	current_health = max_health
	current_damage = base_damage
	current_speed = base_speed
	
	# 创建玩家精灵
	sprite = Sprite2D.new()
	add_child(sprite)
	_create_player_sprite()
	
	# 创建碰撞形状
	collision = CollisionShape2D.new()
	var shape = RectangleShape2D.new()
	shape.size = Vector2(32, 32)
	collision.shape = shape
	add_child(collision)
	
	# 创建攻击区域
	_setup_attack_system()
	
	# 设置冲刺系统
	_setup_dash_system()
	
	# 创建状态显示标签
	_create_status_displays()

func _physics_process(delta):
	if is_dashing:
		move_and_slide()
		return

	handle_movement()
	handle_dash()
	handle_aura(delta)
	handle_reload(delta)
	handle_manual_attack()
	move_and_slide()

func handle_aura(delta):
	if not aura_active:
		return
		
	# 旋转视觉效果
	if aura_visuals:
		aura_visuals.rotation += 2.0 * delta
		
	# 处理伤害
	aura_tick_timer -= delta
	if aura_tick_timer <= 0:
		aura_tick_timer = aura_tick_interval
		
		# 查找范围内的敌人
		var zombies = get_tree().get_nodes_in_group("zombies")
		for zombie in zombies:
			if zombie is Zombie:
				if global_position.distance_to(zombie.global_position) <= aura_radius:
					zombie.take_damage(int(aura_damage))
					zombie.apply_freeze(1.0) # 冰霜护盾附带减速
					_create_hit_effect(zombie.global_position)

func _create_hit_effect(pos: Vector2):
	var particle = GPUParticles2D.new()
	particle.global_position = pos
	particle.emitting = true
	particle.amount = 5
	particle.lifetime = 0.3
	particle.one_shot = true
	var mat = ParticleProcessMaterial.new()
	mat.color = Color.CYAN
	mat.gravity = Vector3(0,0,0)
	mat.scale_min = 2.0
	mat.scale_max = 3.0
	particle.process_material = mat
	get_parent().add_child(particle)
	
	var timer = Timer.new()
	timer.wait_time = 0.3
	timer.one_shot = true
	timer.timeout.connect(particle.queue_free)
	particle.add_child(timer)
	timer.start()

func _setup_dash_system():
	dash_timer = Timer.new()
	dash_timer.wait_time = dash_duration
	dash_timer.one_shot = true
	dash_timer.timeout.connect(_on_dash_finished)
	add_child(dash_timer)
	
	dash_cooldown_timer = Timer.new()
	dash_cooldown_timer.wait_time = dash_cooldown
	dash_cooldown_timer.one_shot = true
	dash_cooldown_timer.timeout.connect(_on_dash_cooldown_finished)
	add_child(dash_cooldown_timer)

func handle_dash():
	if Input.is_action_just_pressed("ui_focus_next") or Input.is_key_pressed(KEY_SHIFT):
		if can_dash and velocity != Vector2.ZERO:
			perform_dash()

func perform_dash():
	is_dashing = true
	can_dash = false
	
	# 冲刺方向为当前移动方向
	velocity = velocity.normalized() * current_speed * dash_speed_multiplier
	
	# 开启无敌帧（可选）
	# collision.disabled = true
	
	dash_timer.start()
	
	# 视觉效果
	var tween = create_tween()
	tween.tween_property(sprite, "modulate:a", 0.5, 0.1)
	
	print("冲刺！")

func _on_dash_finished():
	is_dashing = false
	velocity = Vector2.ZERO
	# collision.disabled = false
	sprite.modulate.a = 1.0
	dash_cooldown_timer.start()

func _on_dash_cooldown_finished():
	can_dash = true

func _setup_attack_system():
	# 创建攻击区域
	attack_area = Area2D.new()
	add_child(attack_area)
	
	# 创建攻击区域的碰撞形状
	var attack_collision = CollisionShape2D.new()
	var attack_shape = CircleShape2D.new()
	attack_shape.radius = 60.0  # 攻击范围
	attack_collision.shape = attack_shape
	attack_area.add_child(attack_collision)
	
	# 创建攻击计时器
	attack_timer = Timer.new()
	attack_timer.wait_time = attack_cooldown
	attack_timer.one_shot = true
	attack_timer.timeout.connect(_on_attack_cooldown_finished)
	add_child(attack_timer)

func handle_attack():
	if not can_attack:
		return
		
	match current_attack_type:
		AttackType.MELEE:
			_handle_melee_attack()
		AttackType.RANGED:
			_handle_ranged_attack()
		AttackType.AREA:
			_handle_area_attack()
		AttackType.MAGIC:
			_handle_magic_attack()

func _handle_melee_attack():
	# 近战攻击 - 攻击范围内的僵尸
	var zombies_in_range = attack_area.get_overlapping_bodies()
	for body in zombies_in_range:
		if body is Zombie:
			attack_zombie(body)
			break

func _handle_ranged_attack():
	# 远程攻击 - 发射子弹
	var nearest_zombie = _find_nearest_zombie(300.0)  # 300像素射程
	if nearest_zombie:
		_fire_projectile(nearest_zombie.global_position)

func _handle_area_attack():
	# 范围攻击 - 攻击所有范围内的僵尸
	var zombies_in_range = attack_area.get_overlapping_bodies()
	if zombies_in_range.size() > 0:
		can_attack = false
		attack_timer.start()
		_create_area_explosion()
		for body in zombies_in_range:
			if body is Zombie:
				var damage = current_damage * 0.7  # 范围攻击伤害稍低
				body.take_damage(int(damage))

func _handle_magic_attack():
	# 魔法攻击 - 追踪弹
	var nearest_zombie = _find_nearest_zombie(400.0)  # 400像素魔法射程
	if nearest_zombie:
		_cast_magic_missile(nearest_zombie)

func attack_zombie(zombie: Zombie):
	if not can_attack:
		return
		
	can_attack = false
	attack_timer.start()
	
	# 计算伤害
	var damage = current_damage
	var is_critical = randf() < (critical_chance / 100.0)
	if is_critical:
		damage *= 2.0
		print("暴击！造成 " + str(damage) + " 点伤害")
	
	# 对僵尸造成伤害
	zombie.take_damage(int(damage))
	
	# 应用特殊效果
	if freeze_duration > 0:
		zombie.apply_freeze(freeze_duration)
	if poison_damage > 0:
		zombie.apply_poison(poison_damage, 5.0)
	
	# 生命偷取
	if life_steal_percent > 0:
		var heal_amount = int(damage * life_steal_percent / 100.0)
		heal(heal_amount)
	
	# 视觉反馈
	_show_attack_effect(zombie.global_position)

func _show_attack_effect(target_pos: Vector2):
	match current_attack_type:
		AttackType.MELEE:
			_show_melee_effect(target_pos)
		AttackType.RANGED:
			_show_ranged_effect(target_pos)
		AttackType.AREA:
			_show_area_effect()
		AttackType.MAGIC:
			_show_magic_effect(target_pos)

func _show_melee_effect(target_pos: Vector2):
	# 近战攻击 - 扇形斩击效果
	var slash_effect = Node2D.new()
	slash_effect.global_position = global_position
	get_parent().add_child(slash_effect)
	
	# 创建多条斩击线
	for i in range(5):
		var angle_offset = (i - 2) * 0.2  # 扇形展开
		var direction = (target_pos - global_position).normalized()
		var rotated_dir = direction.rotated(angle_offset)
		var end_pos = global_position + rotated_dir * 80
		
		var line = Line2D.new()
		line.add_point(Vector2.ZERO)
		line.add_point(end_pos - global_position)
		line.width = 6.0 - i * 0.5  # 中间粗，两边细
		line.default_color = Color(1.0, 0.8, 0.0, 0.8 - i * 0.1)  # 金色渐变
		slash_effect.add_child(line)
	
	# 添加冲击波圆圈
	var circle_effect = _create_impact_circle(target_pos, 40.0, Color.ORANGE)
	get_parent().add_child(circle_effect)
	
	# 动画效果
	var tween = create_tween()
	tween.parallel().tween_property(slash_effect, "modulate:a", 0.0, 0.3)
	tween.parallel().tween_property(circle_effect, "modulate:a", 0.0, 0.4)
	tween.tween_callback(slash_effect.queue_free)
	tween.tween_callback(circle_effect.queue_free)

func _show_ranged_effect(_target_pos: Vector2):
	# 远程攻击已经有子弹轨迹，这里添加射击光效
	var muzzle_flash = _create_muzzle_flash_enhanced()
	get_parent().add_child(muzzle_flash)

func _show_area_effect():
	# 范围攻击 - 爆炸冲击波
	var explosion_rings = []
	for i in range(3):
		var ring = _create_explosion_ring(global_position, 30.0 + i * 25, Color(1.0, 0.5, 0.0, 0.7))
		get_parent().add_child(ring)
		explosion_rings.append(ring)
		
		# 延迟出现每个环
		var delay_tween = create_tween()
		delay_tween.tween_delay(i * 0.1)
		delay_tween.tween_callback(func(): _animate_explosion_ring(ring))

func _show_magic_effect(target_pos: Vector2):
	# 魔法攻击 - 魔法阵效果
	var magic_circle = _create_magic_circle(global_position, 60.0)
	var target_circle = _create_magic_circle(target_pos, 40.0)
	get_parent().add_child(magic_circle)
	get_parent().add_child(target_circle)
	
	# 连接线
	var connection = Line2D.new()
	connection.add_point(global_position)
	connection.add_point(target_pos)
	connection.width = 4.0
	connection.default_color = Color.MAGENTA
	get_parent().add_child(connection)
	
	# 动画
	var tween = create_tween()
	tween.parallel().tween_property(magic_circle, "rotation", PI * 2, 0.5)
	tween.parallel().tween_property(target_circle, "rotation", -PI * 2, 0.5)
	tween.parallel().tween_property(connection, "modulate:a", 0.0, 0.3)
	tween.tween_callback(magic_circle.queue_free)
	tween.tween_callback(target_circle.queue_free)
	tween.tween_callback(connection.queue_free)

func _create_impact_circle(pos: Vector2, radius: float, color: Color) -> Node2D:
	var circle = Node2D.new()
	circle.global_position = pos
	
	var line = Line2D.new()
	var points = 32
	for i in range(points + 1):
		var angle = i * 2.0 * PI / points
		var point = Vector2(cos(angle), sin(angle)) * radius
		line.add_point(point)
	line.width = 3.0
	line.default_color = color
	circle.add_child(line)
	
	return circle

func _create_muzzle_flash_enhanced() -> Node2D:
	var flash = Node2D.new()
	flash.global_position = global_position
	
	# 创建多个闪光线条
	for i in range(8):
		var line = Line2D.new()
		var angle = i * PI / 4
		var length = randf_range(15, 30)
		line.add_point(Vector2.ZERO)
		line.add_point(Vector2(cos(angle), sin(angle)) * length)
		line.width = randf_range(2, 5)
		line.default_color = Color(1.0, 1.0, 0.5, randf_range(0.6, 1.0))
		flash.add_child(line)
	
	# 快速消失
	var tween = create_tween()
	tween.tween_property(flash, "modulate:a", 0.0, 0.15)
	tween.tween_callback(flash.queue_free)
	
	return flash

func _create_explosion_ring(pos: Vector2, radius: float, color: Color) -> Node2D:
	var ring = Node2D.new()
	ring.global_position = pos
	
	var line = Line2D.new()
	var points = 24
	for i in range(points + 1):
		var angle = i * 2.0 * PI / points
		var point = Vector2(cos(angle), sin(angle)) * radius
		line.add_point(point)
	line.width = 4.0
	line.default_color = color
	ring.add_child(line)
	
	return ring

func _animate_explosion_ring(ring: Node2D):
	var tween = create_tween()
	tween.parallel().tween_property(ring, "scale", Vector2(2.0, 2.0), 0.4)
	tween.parallel().tween_property(ring, "modulate:a", 0.0, 0.4)
	tween.tween_callback(ring.queue_free)

func _create_magic_circle(pos: Vector2, radius: float) -> Node2D:
	var circle = Node2D.new()
	circle.global_position = pos
	
	# 外圈
	var outer_line = Line2D.new()
	var points = 32
	for i in range(points + 1):
		var angle = i * 2.0 * PI / points
		var point = Vector2(cos(angle), sin(angle)) * radius
		outer_line.add_point(point)
	outer_line.width = 3.0
	outer_line.default_color = Color.MAGENTA
	circle.add_child(outer_line)
	
	# 内圈符文
	var inner_line = Line2D.new()
	for i in range(points + 1):
		var angle = i * 2.0 * PI / points
		var point = Vector2(cos(angle), sin(angle)) * radius * 0.6
		inner_line.add_point(point)
	inner_line.width = 2.0
	inner_line.default_color = Color(1.0, 0.5, 1.0, 0.8)
	circle.add_child(inner_line)
	
	return circle

func _on_attack_cooldown_finished():
	can_attack = true

func _find_nearest_zombie(max_range: float) -> Zombie:
	var nearest_zombie: Zombie = null
	var nearest_distance: float = max_range
	
	# 获取所有僵尸
	var zombies = get_tree().get_nodes_in_group("zombies")
	for zombie in zombies:
		if zombie is Zombie:
			var distance = global_position.distance_to(zombie.global_position)
			if distance < nearest_distance:
				nearest_distance = distance
				nearest_zombie = zombie
	
	return nearest_zombie

func _fire_projectile(target_pos: Vector2):
	can_attack = false
	attack_timer.start()
	
	var base_direction = (target_pos - global_position).normalized()
	var total_spread = PI / 4 # 45度扇形
	
	# 根据 multi_shot_count 发射多发子弹
	var shot_count = multi_shot_count
	if current_attack_type == AttackType.RANGED and active_effects.any(func(w): return w.card_name == "霰弹枪"): # 特殊判断
		shot_count = max(shot_count, 5)

	for i in range(shot_count):
		var angle_offset = 0.0
		if shot_count > 1:
			# 计算每个子弹的偏移角度，使其均匀分布
			angle_offset = -total_spread / 2.0 + (total_spread / (shot_count - 1)) * i
		
		var direction = base_direction.rotated(angle_offset)
		
		# 创建子弹
		var bullet = _create_bullet()
		bullet.global_position = global_position
		bullet.velocity = direction * 500.0
		bullet.damage = current_damage
		
		# 如果是多重射击，稍微降低单个子弹伤害
		if shot_count > 1:
			bullet.damage *= 0.8
			
		get_parent().add_child(bullet)
		_create_muzzle_flash(direction)

func _create_bullet() -> Area2D:
	var bullet = Area2D.new()
	bullet.set_script(preload("res://scripts/Projectile.gd"))
	
	# 子弹精灵
	var bullet_sprite = Sprite2D.new()
	var bullet_image = Image.create(8, 3, false, Image.FORMAT_RGB8)
	bullet_image.fill(Color.YELLOW)
	var bullet_texture = ImageTexture.new()
	bullet_texture.set_image(bullet_image)
	bullet_sprite.texture = bullet_texture
	bullet.add_child(bullet_sprite)
	
	# 子弹碰撞
	var bullet_collision = CollisionShape2D.new()
	var bullet_shape = RectangleShape2D.new()
	bullet_shape.size = Vector2(8, 3)
	bullet_collision.shape = bullet_shape
	bullet.add_child(bullet_collision)
	
	return bullet

func _create_area_explosion():
	# 创建爆炸特效
	var explosion = GPUParticles2D.new()
	explosion.global_position = global_position
	explosion.emitting = true
	explosion.amount = 50
	explosion.lifetime = 1.0
	
	# 配置粒子材质
	var particle_material = ParticleProcessMaterial.new()
	particle_material.direction = Vector3(0, -1, 0)
	particle_material.initial_velocity_min = 100.0
	particle_material.initial_velocity_max = 200.0
	particle_material.gravity = Vector3(0, 98, 0)
	particle_material.scale_min = 0.5
	particle_material.scale_max = 1.5
	explosion.process_material = particle_material
	
	get_parent().add_child(explosion)
	
	# 1秒后删除
	var timer = Timer.new()
	timer.wait_time = 1.0
	timer.one_shot = true
	timer.timeout.connect(explosion.queue_free)
	explosion.add_child(timer)
	timer.start()

func _cast_magic_missile(target: Zombie):
	can_attack = false
	attack_timer.start()
	
	# 创建魔法弹
	var missile = _create_magic_missile()
	missile.global_position = global_position
	missile.target = target
	missile.damage = current_damage * 1.2  # 魔法攻击伤害更高
	
	get_parent().add_child(missile)

func _create_magic_missile() -> Area2D:
	var missile = Area2D.new()
	missile.set_script(preload("res://scripts/MagicMissile.gd"))
	
	# 魔法弹精灵
	var missile_sprite = Sprite2D.new()
	var missile_image = Image.create(12, 12, false, Image.FORMAT_RGB8)
	missile_image.fill(Color.MAGENTA)
	var missile_texture = ImageTexture.new()
	missile_texture.set_image(missile_image)
	missile_sprite.texture = missile_texture
	missile.add_child(missile_sprite)
	
	# 魔法弹碰撞
	var missile_collision = CollisionShape2D.new()
	var missile_shape = CircleShape2D.new()
	missile_shape.radius = 6.0
	missile_collision.shape = missile_shape
	missile.add_child(missile_collision)
	
	# 魔法粒子特效
	var particles = GPUParticles2D.new()
	particles.emitting = true
	particles.amount = 20
	var particle_material = ParticleProcessMaterial.new()
	particle_material.scale_min = 0.2
	particle_material.scale_max = 0.5
	particles.process_material = particle_material
	missile.add_child(particles)
	
	return missile

func _create_muzzle_flash(direction: Vector2):
	# 枪口闪光特效
	var flash = GPUParticles2D.new()
	flash.global_position = global_position + direction * 20
	flash.emitting = true
	flash.amount = 10
	flash.lifetime = 0.2
	
	var flash_material = ParticleProcessMaterial.new()
	flash_material.direction = Vector3(direction.x, direction.y, 0)
	flash_material.initial_velocity_min = 50.0
	flash_material.initial_velocity_max = 100.0
	flash.process_material = flash_material
	
	get_parent().add_child(flash)
	
	# 0.3秒后删除
	var timer = Timer.new()
	timer.wait_time = 0.3
	timer.one_shot = true
	timer.timeout.connect(flash.queue_free)
	flash.add_child(timer)
	timer.start()

func _create_player_sprite():
	# 创建更美观的玩家角色
	var image = Image.create(40, 40, false, Image.FORMAT_RGBA8)
	
	# 绘制玩家角色 - 一个战士的简化版本
	for y in range(40):
		for x in range(40):
			var color = Color.TRANSPARENT
			
			# 头部 (圆形)
			if _is_in_circle(x, y, 20, 12, 8):
				color = Color(1.0, 0.8, 0.6)  # 肤色
			
			# 身体 (矩形)
			elif x >= 15 and x <= 25 and y >= 20 and y <= 35:
				color = Color(0.2, 0.4, 0.8)  # 蓝色盔甲
			
			# 手臂
			elif (x >= 10 and x <= 14 and y >= 22 and y <= 30) or (x >= 26 and x <= 30 and y >= 22 and y <= 30):
				color = Color(1.0, 0.8, 0.6)  # 肤色
			
			# 腿部
			elif (x >= 16 and x <= 19 and y >= 35 and y <= 39) or (x >= 21 and x <= 24 and y >= 35 and y <= 39):
				color = Color(0.3, 0.2, 0.1)  # 棕色裤子
			
			# 武器 (剑)
			elif x >= 31 and x <= 33 and y >= 15 and y <= 25:
				color = Color.GRAY  # 剑身
			elif x >= 30 and x <= 34 and y >= 14 and y <= 16:
				color = Color(0.6, 0.4, 0.2)  # 剑柄
			
			# 眼睛
			elif (x == 18 or x == 22) and y == 10:
				color = Color.BLACK
			
			image.set_pixel(x, y, color)
	
	var texture = ImageTexture.new()
	texture.set_image(image)
	sprite.texture = texture

func _is_in_circle(x: int, y: int, center_x: int, center_y: int, radius: int) -> bool:
	var dx = x - center_x
	var dy = y - center_y
	return dx * dx + dy * dy <= radius * radius

func handle_movement():
	var input_vector = Vector2.ZERO
	
	if Input.is_action_pressed("ui_right"):
		input_vector.x += 1
	if Input.is_action_pressed("ui_left"):
		input_vector.x -= 1
	if Input.is_action_pressed("ui_down"):
		input_vector.y += 1
	if Input.is_action_pressed("ui_up"):
		input_vector.y -= 1
	
	if input_vector != Vector2.ZERO:
		input_vector = input_vector.normalized()
		velocity = input_vector * current_speed
	else:
		velocity = Vector2.ZERO

func apply_weapon_effect(weapon: WeaponCard):
	active_effects.append(weapon)
	
	# 根据武器类型切换攻击方式
	match weapon.weapon_type:
		WeaponCard.WeaponType.MELEE:
			current_attack_type = AttackType.MELEE
		WeaponCard.WeaponType.RANGED:
			current_attack_type = AttackType.RANGED
		WeaponCard.WeaponType.MAGIC:
			current_attack_type = AttackType.MAGIC
		WeaponCard.WeaponType.SUPPORT:
			# 辅助武器不改变攻击类型，但某些特殊辅助武器可以
			if weapon.effect_type == WeaponCard.EffectType.AREA_DAMAGE:
				current_attack_type = AttackType.AREA
	
	# 特殊武器检查
	if weapon.card_name == "冰霜护盾":
		activate_aura()

	# 立即应用武器效果
	match weapon.effect_type:
		WeaponCard.EffectType.DAMAGE_BOOST:
			current_damage += weapon.effect_value
			damage_multiplier = current_damage / base_damage
		WeaponCard.EffectType.HEALTH_BOOST:
			max_health += int(weapon.effect_value)
			current_health += int(weapon.effect_value)
			# 确保血量不超过最大值
			current_health = min(current_health, max_health)
			health_changed.emit(current_health)
		WeaponCard.EffectType.SPEED_BOOST:
			speed_multiplier += weapon.effect_value / 100.0
			current_speed = base_speed * speed_multiplier
		WeaponCard.EffectType.MULTI_SHOT:
			multi_shot_count = max(multi_shot_count, int(weapon.effect_value))
		WeaponCard.EffectType.PIERCING:
			piercing_count = max(piercing_count, int(weapon.effect_value))
		WeaponCard.EffectType.AREA_DAMAGE:
			area_damage += weapon.effect_value
		WeaponCard.EffectType.LIFE_STEAL:
			life_steal_percent += weapon.effect_value
		WeaponCard.EffectType.CRITICAL_CHANCE:
			critical_chance += weapon.effect_value
		WeaponCard.EffectType.FREEZE:
			freeze_duration = max(freeze_duration, weapon.effect_value)
		WeaponCard.EffectType.POISON:
			poison_damage += weapon.effect_value
	
	print("✅ 武器效果已立即生效: " + weapon.card_name + " - " + weapon.get_effect_description())
	print("当前属性 - 伤害: " + str(current_damage) + ", 血量: " + str(current_health) + "/" + str(max_health) + ", 速度: " + str(current_speed))
	
	# 更新状态显示
	_update_status_displays()

func take_damage(amount: int):
	current_health -= amount
	current_health = max(0, current_health)
	health_changed.emit(current_health)
	
	# 更新状态显示
	_update_status_displays()
	
	if current_health <= 0:
		die()

func heal(amount: int):
	current_health += amount
	current_health = min(max_health, current_health)
	health_changed.emit(current_health)

func die():
	died.emit()
	print("玩家死亡!")

func reset_for_new_round():
	# 重置临时效果，保留永久效果
	var permanent_effects = []
	for effect in active_effects:
		if effect.duration == -1:  # 永久效果
			permanent_effects.append(effect)
	
	active_effects = permanent_effects
	_recalculate_stats()

func _recalculate_stats():
	# 重置所有属性到基础值
	current_damage = base_damage
	current_speed = base_speed
	damage_multiplier = 1.0
	speed_multiplier = 1.0
	multi_shot_count = 1
	piercing_count = 0
	area_damage = 0.0
	life_steal_percent = 0.0
	critical_chance = 0.0
	freeze_duration = 0.0
	poison_damage = 0.0
	
	# 重新应用所有活跃效果
	for effect in active_effects:
		apply_weapon_effect(effect)

func _create_status_displays():
	# 创建血量显示
	health_display = Label.new()
	health_display.add_theme_font_size_override("font_size", 12)
	health_display.add_theme_color_override("font_color", Color.RED)
	health_display.position = Vector2(-20, -55)
	health_display.z_index = 10
	add_child(health_display)
	
	# 创建子弹显示
	ammo_display = Label.new()
	ammo_display.add_theme_font_size_override("font_size", 12)
	ammo_display.add_theme_color_override("font_color", Color.YELLOW)
	ammo_display.position = Vector2(-20, -45)
	ammo_display.z_index = 10
	add_child(ammo_display)
	
	# 创建攻击类型显示
	attack_type_display = Label.new()
	attack_type_display.add_theme_font_size_override("font_size", 12)
	attack_type_display.add_theme_color_override("font_color", Color.CYAN)
	attack_type_display.position = Vector2(-20, -35)
	attack_type_display.z_index = 10
	add_child(attack_type_display)
	
	_update_status_displays()

func _update_status_displays():
	if health_display:
		health_display.text = "❤️" + str(current_health) + "/" + str(max_health)
	
	if ammo_display:
		if is_reloading:
			var reload_progress = int((reload_time - reload_timer) / reload_time * 100)
			ammo_display.text = "🔄" + str(reload_progress) + "%"
			ammo_display.add_theme_color_override("font_color", Color.ORANGE)
		else:
			ammo_display.text = "🔫" + str(current_ammo) + "/" + str(max_ammo)
			ammo_display.add_theme_color_override("font_color", Color.YELLOW)
	
	if attack_type_display:
		match current_attack_type:
			AttackType.MELEE:
				attack_type_display.text = "⚔️近战"
			AttackType.RANGED:
				attack_type_display.text = "🏹远程"
			AttackType.AREA:
				attack_type_display.text = "💥范围"
			AttackType.MAGIC:
				attack_type_display.text = "✨魔法"

func handle_reload(delta):
	if is_reloading:
		reload_timer -= delta
		_update_status_displays()
		if reload_timer <= 0:
			is_reloading = false
			current_ammo = max_ammo
			_update_status_displays()
			print("重装完成！")

func handle_manual_attack():
	# 检查空格键输入
	if Input.is_action_just_pressed("ui_accept"):  # 空格键
		if can_attack and not is_reloading:
			perform_attack()

func perform_attack():
	match current_attack_type:
		AttackType.MELEE:
			_perform_melee_attack()
		AttackType.RANGED:
			_perform_ranged_attack()
		AttackType.AREA:
			_perform_area_attack()
		AttackType.MAGIC:
			_perform_magic_attack()

func _perform_melee_attack():
	# 近战攻击不需要子弹
	var zombies_in_range = attack_area.get_overlapping_bodies()
	for zombie in zombies_in_range:
		if zombie.is_in_group("zombies"):
			attack_zombie(zombie)
			_show_attack_effect(zombie.global_position)
	
	# 设置攻击冷却
	can_attack = false
	attack_timer.start()

func _perform_ranged_attack():
	# 远程攻击需要子弹
	if current_ammo <= 0:
		_start_reload()
		return
	
	current_ammo -= 1
	_update_status_displays()
	
	var nearest_zombie = _find_nearest_zombie(500.0)  # 500像素范围内寻找僵尸
	if nearest_zombie:
		_fire_projectile(nearest_zombie.global_position)
		_show_attack_effect(nearest_zombie.global_position)
	
	# 设置攻击冷却
	can_attack = false
	attack_timer.start()

func _perform_area_attack():
	# 范围攻击消耗更多子弹
	var ammo_cost = 3
	if current_ammo < ammo_cost:
		_start_reload()
		return
	
	current_ammo -= ammo_cost
	_update_status_displays()
	
	_create_area_explosion()
	_show_attack_effect(global_position)
	
	# 设置攻击冷却
	can_attack = false
	attack_timer.start()

func _perform_magic_attack():
	# 魔法攻击消耗2发子弹
	var ammo_cost = 2
	if current_ammo < ammo_cost:
		_start_reload()
		return
	
	current_ammo -= ammo_cost
	_update_status_displays()
	
	var nearest_zombie = _find_nearest_zombie(600.0)  # 魔法攻击范围更远
	if nearest_zombie:
 		_cast_magic_missile(nearest_zombie.global_position)
		_show_attack_effect(nearest_zombie.global_position)
	
	# 设置攻击冷却
	can_attack = false
	attack_timer.start()

func activate_aura():
	if aura_active:
		# 升级：增加范围和伤害
		aura_damage += 5.0
		aura_radius += 20.0
		if aura_visuals:
			aura_visuals.scale *= 1.2
		return
		
	aura_active = true
	
	# 创建视觉效果
	aura_visuals = Node2D.new()
	add_child(aura_visuals)
	
	var count = 3
	for i in range(count):
		var orb = Sprite2D.new()
		var img = Image.create(8, 8, false, Image.FORMAT_RGBA8)
		img.fill(Color.CYAN)
		var tex = ImageTexture.new()
		tex.set_image(img)
		orb.texture = tex
		
		var angle = i * (PI * 2 / count)
		orb.position = Vector2(cos(angle), sin(angle)) * aura_radius
		aura_visuals.add_child(orb)

func _start_reload():
	if not is_reloading:
		is_reloading = true
		reload_timer = reload_time
		_update_status_displays()
		print("开始重装弹药...")
