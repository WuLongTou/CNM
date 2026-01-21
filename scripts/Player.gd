class_name Player
extends CharacterBody2D

signal health_changed(new_health: int)
signal died
signal effect_applied(effect_name: String)  # 新增：特效应用信号

# ===== 常量定义 =====
const DEFAULT_MAX_HEALTH: int = 120  # 增加初始血量，提高容错率
const DEFAULT_BASE_DAMAGE: float = 12.0  # 略微提高基础伤害
const DEFAULT_BASE_SPEED: float = 220.0  # 提高移速，增加操作感
const DASH_SPEED_MULTIPLIER: float = 3.5  # 提高冲刺倍率，增加躲避能力
const DASH_DURATION: float = 0.25  # 略微延长冲刺时间
const DASH_COOLDOWN: float = 0.8  # 减少冲刺冷却，提高使用频率
const INVINCIBLE_DURATION: float = 0.5  # 延长无敌帧，提高生存能力
const CRIT_MULTIPLIER: float = 2.5  # 提高暴击倍率，增加爆发潜力
const AURA_TICK_INTERVAL: float = 0.5
const AURA_ORB_COUNT: int = 3

@export var max_health: int = DEFAULT_MAX_HEALTH
@export var base_damage: float = DEFAULT_BASE_DAMAGE
@export var base_speed: float = DEFAULT_BASE_SPEED
@export var base_max_health: int = DEFAULT_MAX_HEALTH

var current_health: int
var current_damage: float
var current_speed: float
var active_effects: Array[WeaponCard] = []

# 冲刺系统
var can_dash: bool = true
var is_dashing: bool = false
var dash_speed_multiplier: float = DASH_SPEED_MULTIPLIER
var dash_duration: float = DASH_DURATION
var dash_cooldown: float = DASH_COOLDOWN
var dash_timer: Timer
var dash_cooldown_timer: Timer

# 视觉反馈系统
var status_effect_icons: Dictionary = {}
var combo_counter: int = 0
var combo_timer: float = 0.0
var last_damage_time: float = 0.0

# 目标选择
var target_priority: int = 0  # 0=最近, 1=最远, 2=敌人最多

# 近战特效变量
var splash_radius: float = 0.0
var melee_target_count: int = 1
var kill_explosion_damage: int = 0
var kill_shield_amount: int = 0
var knockback_force: float = 0.0

# 远程特效变量
var trajectory_damage_ratio: float = 0.0
var returning_projectiles: bool = false
var chain_count: int = 0
var bounce_count: int = 0

# 魔法特效变量
var charm_duration: float = 0.0
var push_force: float = 0.0
var stun_duration: float = 0.0
var dot_damage: float = 0.0

# 护盾
var shield_amount: int = 0

# 环绕武器系统 (Aura)
var aura_active: bool = false
var aura_damage: float = 5.0
var aura_radius: float = 80.0
var aura_tick_timer: float = 0.0
var aura_tick_interval: float = AURA_TICK_INTERVAL
var aura_visuals: Node2D
var aura_hitboxes: Array[Area2D] = []
var aura_orb_count: int = AURA_ORB_COUNT
var aura_orb_radius: float = 8.0

# 武器效果的累积值
var damage_bonus: float = 0.0
var speed_multiplier: float = 1.0
var multi_shot_count: int = 1
var piercing_count: int = 0
var area_damage: float = 0.0
var life_steal_percent: float = 0.0
var critical_chance: float = 0.0
var freeze_duration: float = 0.0
var poison_damage: float = 0.0

var sprite: Sprite2D
var shadow: Sprite2D
var collision: CollisionShape2D
var attack_area: Area2D
var attack_timer: Timer
var can_attack: bool = true
var base_attack_cooldown: float = 0.45  # 略微缩短攻击间隔
var attack_cooldown: float = 0.5
var base_attack_range: float = 70.0  # 增加攻击范围
var bounds_padding: float = 22.0
var top_ui_margin: float = 110.0
var attack_speed_multiplier: float = 1.0
var weapon_attack_speed_modifier: float = 1.0
var weapon_cooldown: float = 0.0
var weapon_range_modifier: float = 1.0

# 每次攻击消耗
var health_cost_per_attack: int = 0
var crit_multiplier: float = CRIT_MULTIPLIER
var invincible_timer: float = 0.0
var invincible_duration: float = INVINCIBLE_DURATION

# 玩家中毒状态
var poisoned_timer: float = 0.0
var poisoned_tick_timer: float = 0.0
var poisoned_dps: float = 0.0

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

# 状态显示标签
var health_display: Label
var attack_type_display: Label

# 近战攻击范围指示器
var melee_range_indicator: Node2D
var show_melee_range: bool = true

func _ready():
	base_max_health = max_health
	current_health = max_health
	current_damage = base_damage
	current_speed = base_speed
	
	# 创建阴影
	shadow = Sprite2D.new()
	shadow.texture = _create_shadow_texture(32, 12, Color(0, 0, 0, 0.5))
	shadow.position = Vector2(0, 18)
	add_child(shadow)
	
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
	
	# 创建近战攻击范围指示器
	_create_melee_range_indicator()
	
	# 创建状态显示标签
	_create_status_displays()

func _physics_process(delta):
	if is_dashing:
		move_and_slide()
		return

	handle_movement()
	handle_dash()
	handle_target_cycle()
	handle_status_effects(delta)
	_handle_invincibility(delta)
	handle_aura(delta)
	handle_auto_attack(delta)
	handle_manual_attack()
	handle_collision_repulsion()
	move_and_slide()
	_clamp_to_screen()

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
		
		# 直接使用距离检测所有僵尸
		var zombies = get_tree().get_nodes_in_group("zombies")
		for zombie in zombies:
			if not is_instance_valid(zombie) or not zombie is Zombie:
				continue
			
			# 检查每个护盾球的位置是否接触到僵尸
			for hitbox in aura_hitboxes:
				if not is_instance_valid(hitbox):
					continue
				var orb_global_pos = hitbox.global_position
				var dist = orb_global_pos.distance_to(zombie.global_position)
				# 如果距离小于护盾球半径+僵尸半径，造成伤害
				if dist < aura_orb_radius * 2 + 12:  # 12是僵尸的大致半径
					zombie.take_damage(int(aura_damage))
					get_tree().call_group("ui_manager", "show_damage_number", zombie.global_position, int(aura_damage), false)
					zombie.apply_freeze(1.0)  # 冰霜护盾附带减速
					_create_hit_effect(zombie.global_position)
					break  # 每个僵尸每次只受一次伤害

func handle_status_effects(delta):
	if poisoned_timer > 0:
		poisoned_timer -= delta
		poisoned_tick_timer -= delta
		if poisoned_tick_timer <= 0:
			poisoned_tick_timer = 1.0
			var damage = int(poisoned_dps)
			if damage > 0:
				take_damage(damage)
		if poisoned_timer <= 0 and sprite:
			sprite.modulate = Color.WHITE

func _handle_invincibility(delta):
	if invincible_timer > 0:
		invincible_timer -= delta
		if invincible_timer <= 0 and sprite:
			sprite.modulate = Color.WHITE

func _create_hit_effect(pos: Vector2, color: Color = Color.CYAN, intensity: float = 1.0):
	# 主粒子效果
	var particle = GPUParticles2D.new()
	particle.global_position = pos
	particle.emitting = true
	particle.amount = int(5 * intensity)
	particle.lifetime = 0.3 * intensity
	particle.one_shot = true
	var mat = ParticleProcessMaterial.new()
	mat.color = color
	mat.gravity = Vector3(0, 0, 0)
	mat.scale_min = 2.0 * intensity
	mat.scale_max = 3.0 * intensity
	mat.initial_velocity_min = 20.0 * intensity
	mat.initial_velocity_max = 60.0 * intensity
	particle.process_material = mat
	get_parent().add_child(particle)
	
	# 添加冲击波效果
	var ring = _create_impact_ring(pos, 20.0 * intensity, color)
	get_parent().add_child(ring)
	
	var timer = Timer.new()
	timer.wait_time = 0.5
	timer.one_shot = true
	timer.timeout.connect(particle.queue_free)
	timer.start()
	
	get_parent().add_child(timer)
	get_parent().add_child(particle)

func _create_impact_ring(pos: Vector2, radius: float, color: Color) -> Node2D:
	var ring = Node2D.new()
	ring.global_position = pos
	
	var line = Line2D.new()
	var points = 16
	for i in range(points + 1):
		var angle = i * 2.0 * PI / points
		var point = Vector2(cos(angle), sin(angle)) * radius
		line.add_point(point)
	line.width = 2.0
	line.default_color = color
	ring.add_child(line)
	
	# 扩散动画
	var tween = ring.create_tween()
	tween.parallel().tween_property(ring, "scale", Vector2(2.5, 2.5), 0.3).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	tween.parallel().tween_property(ring, "modulate:a", 0.0, 0.3)
	tween.tween_callback(ring.queue_free)
	
	return ring

func _show_status_effect_popup(effect_name: String, color: Color):
	# 显示状态效果弹出文字
	var popup = Label.new()
	popup.text = effect_name
	popup.global_position = global_position + Vector2(randf_range(-30, 30), -60)
	popup.add_theme_font_size_override("font_size", 14)
	popup.add_theme_color_override("font_color", color)
	popup.add_theme_constant_override("outline_size", 3)
	popup.add_theme_color_override("font_outline_color", Color.BLACK)
	popup.z_index = 30
	get_parent().add_child(popup)
	
	var tween = popup.create_tween()
	tween.parallel().tween_property(popup, "position:y", popup.position.y - 40, 0.8).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	tween.parallel().tween_property(popup, "modulate:a", 0.0, 0.8).set_delay(0.3)
	tween.tween_callback(popup.queue_free)

func _create_upgrade_effect():
	# 升级时的视觉效果
	var flash = ColorRect.new()
	flash.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	flash.color = Color(1.0, 0.9, 0.3, 0.3)
	flash.mouse_filter = Control.MOUSE_FILTER_IGNORE
	flash.z_index = 50
	get_tree().current_scene.add_child(flash)
	
	var tween = flash.create_tween()
	tween.tween_property(flash, "color:a", 0.0, 0.4)
	tween.tween_callback(flash.queue_free)
	
	# 玩家发光效果
	if sprite:
		var glow_tween = create_tween()
		glow_tween.tween_property(sprite, "modulate", Color(1.5, 1.5, 1.0), 0.2)
		glow_tween.tween_property(sprite, "modulate", Color.WHITE, 0.3)

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
	if Input.is_action_just_pressed("dash"):
		if can_dash and velocity != Vector2.ZERO:
			perform_dash()

func handle_target_cycle():
	if Input.is_action_just_pressed("cycle_target"):
		target_priority = (target_priority + 1) % 3
		get_tree().call_group("ui_manager", "update_player_status", self)

func _input(event):
	if event is InputEventKey and event.pressed:
		if event.keycode == KEY_TAB:
			target_priority = (target_priority + 1) % 3
			get_tree().call_group("ui_manager", "update_player_status", self)

func perform_dash():
	is_dashing = true
	can_dash = false
	
	# 冲刺方向为当前移动方向
	velocity = velocity.normalized() * current_speed * dash_speed_multiplier
	
	# 开启无敌帧
	collision.disabled = true
	
	dash_timer.start()
	
	# 视觉效果
	var tween = create_tween()
	tween.tween_property(sprite, "modulate:a", 0.5, 0.1)
	
	print("冲刺！")

func _on_dash_finished():
	is_dashing = false
	velocity = Vector2.ZERO
	collision.disabled = false
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
	attack_shape.radius = base_attack_range  # 攻击范围
	attack_collision.shape = attack_shape
	attack_area.add_child(attack_collision)
	
	# 创建攻击计时器
	attack_timer = Timer.new()
	attack_timer.wait_time = attack_cooldown
	attack_timer.one_shot = true
	attack_timer.timeout.connect(_on_attack_cooldown_finished)
	add_child(attack_timer)

func _update_attack_range(range_modifier: float):
	if not attack_area:
		return
	var attack_collision = attack_area.get_child(0)
	if attack_collision and attack_collision.shape is CircleShape2D:
		var circle_shape = attack_collision.shape as CircleShape2D
		circle_shape.radius = base_attack_range * max(0.6, range_modifier)

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
	var target_zombie = _find_target_zombie(500.0)
	if target_zombie:
		_cast_magic_missile(target_zombie)

func attack_zombie(zombie: Zombie):
	if not can_attack:
		return
	
	can_attack = false
	attack_timer.start()
	
	var damage = current_damage
	var is_critical = randf() < (critical_chance / 100.0)
	if is_critical:
		damage *= 2.0
	
	var hit_pos = zombie.global_position
	zombie.take_damage(int(damage))
	get_tree().call_group("ui_manager", "show_damage_number", hit_pos, int(damage), is_critical)
	
	if not is_instance_valid(zombie):
		_on_enemy_killed(hit_pos)
		if SoundManager.instance:
			SoundManager.play_zombie_die()
		if life_steal_percent > 0:
			var heal_amount = int(damage * life_steal_percent / 100.0)
			heal(heal_amount)
		_show_attack_effect(hit_pos)
		return
	
	if SoundManager.instance:
		SoundManager.play_zombie_hit()
	
	# 击退效果
	if knockback_force > 0:
		var push_dir = (zombie.global_position - global_position).normalized()
		zombie.velocity += push_dir * knockback_force

	# 冰冻效果（几率）
	if freeze_duration > 0 and randf() < (freeze_duration / 100.0):
		zombie.apply_freeze(0.8)

	# 中毒效果（几率）
	if poison_damage > 0 and randf() < (poison_damage / 100.0):
		var dps = 3.0 + poison_damage * 0.2
		zombie.apply_poison(dps, 3.0)
	
	# 溅射效果
	if splash_radius > 0:
		var zombies = get_tree().get_nodes_in_group("zombies")
		for other_zombie in zombies:
			if other_zombie is Zombie and other_zombie != zombie:
				if hit_pos.distance_to(other_zombie.global_position) <= splash_radius:
					var splash_damage = int(damage * 0.5)
					other_zombie.take_damage(splash_damage)
					get_tree().call_group("ui_manager", "show_damage_number", other_zombie.global_position, splash_damage, false)
	
	# 检查是否击杀
	await get_tree().process_frame
	if not is_instance_valid(zombie):
		_on_enemy_killed(hit_pos)
		if SoundManager.instance:
			SoundManager.play_zombie_die()
	
	# 生命偷取
	if life_steal_percent > 0:
		var heal_amount = int(damage * life_steal_percent / 100.0)
		heal(heal_amount)
	
	_show_attack_effect(hit_pos)

func _on_enemy_killed(pos: Vector2):
	if kill_explosion_damage > 0:
		var zombies = get_tree().get_nodes_in_group("zombies")
		for zombie in zombies:
			if zombie is Zombie and pos.distance_to(zombie.global_position) <= 100:
				zombie.take_damage(kill_explosion_damage)
				get_tree().call_group("ui_manager", "show_damage_number", zombie.global_position, kill_explosion_damage, false)
	
	if kill_shield_amount > 0:
		shield_amount += kill_shield_amount
		if shield_amount > 50:
			shield_amount = 50
		print("获得护盾: " + str(shield_amount))

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
	# 增强版近战攻击 - 华丽斩击效果
	var slash_effect = Node2D.new()
	slash_effect.global_position = global_position
	get_parent().add_child(slash_effect)
	
	# 创建多层斩击轨迹
	for arc in range(3):  # 三层斩击弧
		var arc_container = Node2D.new()
		slash_effect.add_child(arc_container)
		
		var arc_points = []
		var segments = 20
		var arc_radius = 60 + arc * 15
		var arc_angle = PI / 3  # 60度斩击范围
		
		var base_direction = (target_pos - global_position).normalized()
		var start_angle = base_direction.angle() - arc_angle / 2
		
		for i in range(segments + 1):
			var angle = start_angle + (float(i) / segments) * arc_angle
			var radius = arc_radius * (1.0 + sin(float(i) / segments * PI) * 0.2)  # 波浪形
			var point = Vector2(cos(angle), sin(angle)) * radius
			arc_points.append(point)
		
		var slash_line = Line2D.new()
		slash_line.points = arc_points
		slash_line.width = 8.0 - arc * 2.0
		slash_line.default_color = Color(1.0, 0.8 - arc * 0.1, 0.2 - arc * 0.05, 0.9 - arc * 0.2)
		slash_line.joint_mode = Line2D.LINE_JOINT_ROUND
		slash_line.begin_cap_mode = Line2D.LINE_CAP_ROUND
		slash_line.end_cap_mode = Line2D.LINE_CAP_ROUND
		arc_container.add_child(slash_line)
		
		# 添加能量粒子
		for i in range(8 + arc * 3):
			var particle_color = Color.GOLD if arc == 0 else (Color.ORANGE if arc == 1 else Color.YELLOW)
			var particle = _create_energy_particle(global_position + base_direction.rotated(randf() - 0.5) * arc_radius, particle_color, 0.5 + arc * 0.1)
			get_parent().add_child(particle)
	
	# 剑光轨迹
	var sword_trail = _create_sword_trail(global_position, target_pos)
	get_parent().add_child(sword_trail)
	
	# 多重冲击波
	for i in range(3):
		var delay = i * 0.1
		var circle_effect = _create_expanding_circle(target_pos, 30.0 + i * 15, 
			Color.ORANGE.lightened(i * 0.2))
		circle_effect.modulate.a = 0.0
		get_parent().add_child(circle_effect)
		
		_animate_circle_effect(circle_effect, delay)
	
	# 屏幕震动
	get_tree().call_group("ui_manager", "shake_screen", 3.0)
	
	# 斩击音效增强
	if SoundManager.instance:
		SoundManager.play_melee_attack()
	
	# 动画清理
	var cleanup_tween = create_tween()
	cleanup_tween.tween_interval(1.0)
	cleanup_tween.tween_callback(slash_effect.queue_free)
	cleanup_tween.tween_callback(sword_trail.queue_free)

func _show_ranged_effect(_target_pos: Vector2):
	# 增强版远程攻击 - 华丽射击效果
	var muzzle_flash = _create_enhanced_muzzle_flash()
	get_parent().add_child(muzzle_flash)
	
	# 创建弹道轨迹
	var bullet_trail = _create_bullet_trail(global_position, _target_pos)
	get_parent().add_child(bullet_trail)
	
	# 命中爆炸效果
	var impact = _create_bullet_impact(_target_pos)
	get_parent().add_child(impact)
	
	# 清理定时器
	var cleanup_timer = Timer.new()
	cleanup_timer.wait_time = 1.0
	cleanup_timer.one_shot = true
	cleanup_timer.timeout.connect(_cleanup_effects.bind([muzzle_flash, bullet_trail, impact, cleanup_timer]))
	get_parent().add_child(cleanup_timer)
	cleanup_timer.start()

func _show_area_effect():
	# 范围攻击 - 爆炸冲击波
	var explosion_rings = []
	for i in range(3):
		var ring = _create_explosion_ring(global_position, 30.0 + i * 25, Color(1.0, 0.5, 0.0, 0.7))
		get_parent().add_child(ring)
		explosion_rings.append(ring)
		
		# 延迟出现每个环
		var delay_tween = create_tween()
		delay_tween.tween_interval(i * 0.1)
		delay_tween.tween_callback(_animate_explosion_ring.bind(ring))

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

# 新增特效函数
func _create_energy_particle(pos: Vector2, color: Color, lifetime: float = 0.6) -> GPUParticles2D:
	var particle = GPUParticles2D.new()
	particle.emitting = true
	particle.amount = 10
	particle.lifetime = lifetime
	particle.one_shot = true
	particle.modulate = color
	
	var mat = ParticleProcessMaterial.new()
	mat.direction = Vector3(0, -1, 0)
	mat.initial_velocity_min = 20.0
	mat.initial_velocity_max = 80.0
	mat.gravity = Vector3(0, 50, 0)
	mat.scale_min = 1.0
	mat.scale_max = 3.0
	particle.process_material = mat
	
	return particle

func _create_sword_trail(start_pos: Vector2, end_pos: Vector2) -> Node2D:
	var trail = Node2D.new()
	trail.global_position = start_pos
	
	# 创建多层剑光轨迹
	for i in range(5):
		var trail_line = Line2D.new()
		trail_line.add_point(Vector2.ZERO)
		trail_line.add_point(end_pos - start_pos)
		trail_line.width = 12.0 - i * 2.0
		trail_line.default_color = Color(1.0, 0.9 - i * 0.1, 0.3 - i * 0.05, 0.8 - i * 0.15)
		trail_line.joint_mode = Line2D.LINE_JOINT_ROUND
		trail_line.end_cap_mode = Line2D.LINE_CAP_ROUND
		trail.add_child(trail_line)
		
		# 轨迹淡出动画
		var fade_tween = trail.create_tween()
		fade_tween.tween_interval(i * 0.02)
		fade_tween.tween_property(trail_line, "modulate:a", 0.0, 0.3 - i * 0.05)
	
	# 添加光晕
	var glow = Line2D.new()
	glow.add_point(Vector2.ZERO)
	glow.add_point(end_pos - start_pos)
	glow.width = 20.0
	glow.default_color = Color(1.0, 0.8, 0.2, 0.3)
	glow.joint_mode = Line2D.LINE_JOINT_ROUND
	trail.add_child(glow)
	
	return trail

func _create_enhanced_muzzle_flash() -> Node2D:
	var flash = Node2D.new()
	flash.global_position = global_position
	
	# 多层闪光效果
	for i in range(8):
		var ray_length = randf_range(30, 60)
		var angle = i * PI / 4
		var direction = Vector2(cos(angle), sin(angle))
		
		var ray = Line2D.new()
		ray.add_point(Vector2.ZERO)
		ray.add_point(direction * ray_length)
		ray.width = randf_range(2, 6)
		ray.default_color = Color(1.0, 0.9, 0.5, randf_range(0.7, 1.0))
		ray.joint_mode = Line2D.LINE_JOINT_ROUND
		ray.end_cap_mode = Line2D.LINE_CAP_ROUND
		flash.add_child(ray)
		
		# 射线消失动画
		var ray_tween = flash.create_tween()
		ray_tween.tween_property(ray, "modulate:a", 0.0, 0.15)
	
	# 中心光环
	var center_glow = GPUParticles2D.new()
	center_glow.emitting = true
	center_glow.amount = 20
	center_glow.lifetime = 0.3
	center_glow.one_shot = true
	center_glow.modulate = Color.YELLOW
	
	var glow_mat = ParticleProcessMaterial.new()
	glow_mat.direction = Vector3(0, 0, 1)
	glow_mat.initial_velocity_min = 10.0
	glow_mat.initial_velocity_max = 30.0
	glow_mat.scale_min = 2.0
	glow_mat.scale_max = 5.0
	center_glow.process_material = glow_mat
	
	flash.add_child(center_glow)
	
	# 自动清理
	var timer = Timer.new()
	timer.wait_time = 0.5
	timer.one_shot = true
	timer.timeout.connect(flash.queue_free)
	flash.add_child(timer)
	timer.start()
	
	return flash

func _create_bullet_trail(start_pos: Vector2, end_pos: Vector2) -> Node2D:
	var trail = Node2D.new()
	
	# 子弹轨迹主线
	var main_trail = Line2D.new()
	main_trail.add_point(Vector2.ZERO)
	main_trail.add_point(end_pos - start_pos)
	main_trail.width = 3.0
	main_trail.default_color = Color.CYAN
	main_trail.joint_mode = Line2D.LINE_JOINT_ROUND
	trail.add_child(main_trail)
	
	# 能量光晕
	var energy_glow = Line2D.new()
	energy_glow.add_point(Vector2.ZERO)
	energy_glow.add_point(end_pos - start_pos)
	energy_glow.width = 8.0
	energy_glow.default_color = Color(0.3, 0.8, 1.0, 0.5)
	energy_glow.joint_mode = Line2D.LINE_JOINT_ROUND
	trail.add_child(energy_glow)
	
	# 轨迹粒子
	var trail_particles = GPUParticles2D.new()
	trail_particles.emitting = true
	trail_particles.amount = 30
	trail_particles.lifetime = 0.8
	trail_particles.one_shot = true
	trail_particles.modulate = Color.CYAN
	
	var trail_mat = ParticleProcessMaterial.new()
	trail_mat.direction = Vector3(0, -1, 0)
	trail_mat.initial_velocity_min = 20.0
	trail_mat.initial_velocity_max = 60.0
	trail_mat.scale_min = 0.5
	trail_mat.scale_max = 2.0
	trail_particles.process_material = trail_mat
	
	trail.add_child(trail_particles)
	
	return trail

func _create_bullet_impact(pos: Vector2) -> Node2D:
	var impact = Node2D.new()
	impact.global_position = pos
	
	# 爆炸粒子
	var explosion = GPUParticles2D.new()
	explosion.emitting = true
	explosion.amount = 25
	explosion.lifetime = 0.6
	explosion.one_shot = true
	explosion.modulate = Color.CYAN
	
	var explosion_mat = ParticleProcessMaterial.new()
	explosion_mat.direction = Vector3(0, 1, 0)
	explosion_mat.initial_velocity_min = 50.0
	explosion_mat.initial_velocity_max = 150.0
	explosion_mat.gravity = Vector3(0, -100, 0)
	explosion_mat.scale_min = 1.0
	explosion_mat.scale_max = 4.0
	explosion.process_material = explosion_mat
	
	impact.add_child(explosion)
	
	# 扩散光环
	for i in range(3):
		var ring = _create_expanding_circle(Vector2.ZERO, 10.0 + i * 8, Color.CYAN.lightened(i * 0.2))
		impact.add_child(ring)
		
		var ring_tween = impact.create_tween()
		ring_tween.tween_interval(i * 0.05)
		ring_tween.tween_property(ring, "scale", Vector2(3, 3), 0.4)
		ring_tween.parallel().tween_property(ring, "modulate:a", 0.0, 0.4)
	
	return impact

func _create_expanding_circle(pos: Vector2, radius: float, color: Color) -> Node2D:
	var circle = Node2D.new()
	circle.position = pos
	
	var line = Line2D.new()
	var points = 24
	for i in range(points + 1):
		var angle = i * 2.0 * PI / points
		var point = Vector2(cos(angle), sin(angle)) * radius
		line.add_point(point)
	line.width = 2.0
	line.default_color = color
	line.joint_mode = Line2D.LINE_JOINT_ROUND
	circle.add_child(line)
	
	return circle

func _cleanup_effects(effects: Array):
	for effect in effects:
		if is_instance_valid(effect):
			effect.queue_free()

func _animate_circle_effect(circle: Node2D, delay: float):
	await get_tree().create_timer(delay).timeout
	
	if not is_instance_valid(circle):
		return
	
	# 淡入动画
	var fade_in_tween = circle.create_tween()
	fade_in_tween.tween_property(circle, "modulate:a", 0.8, 0.1)
	
	# 等待淡入完成
	await fade_in_tween.finished
	
	if not is_instance_valid(circle):
		return
	
	# 淡出动画
	var fade_out_tween = circle.create_tween()
	fade_out_tween.tween_property(circle, "modulate:a", 0.0, 0.3)
	
	# 等待淡出完成，然后删除
	await fade_out_tween.finished
	
	if is_instance_valid(circle):
		circle.queue_free()

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

func _can_pay_health_cost() -> bool:
	if health_cost_per_attack <= 0:
		return true
	return current_health > health_cost_per_attack

func _pay_health_cost():
	if health_cost_per_attack > 0:
		take_damage(health_cost_per_attack)

func _find_target_zombie(max_range: float) -> Zombie:
	var zombies = get_tree().get_nodes_in_group("zombies")
	if zombies.is_empty():
		return null
	
	var target_zombie: Zombie = null
	
	match target_priority:
		0:  # 最近
			var nearest_distance = max_range
			for zombie in zombies:
				if zombie is Zombie:
					var distance = global_position.distance_to(zombie.global_position)
					if distance < nearest_distance:
						nearest_distance = distance
						target_zombie = zombie
		
		1:  # 最远
			var farthest_distance = 0.0
			for zombie in zombies:
				if zombie is Zombie:
					var distance = global_position.distance_to(zombie.global_position)
					if distance > farthest_distance and distance <= max_range:
						farthest_distance = distance
						target_zombie = zombie
		
		2:  # 敌人最多的地方
			var max_neighbor_count = 0
			for zombie in zombies:
				if zombie is Zombie:
					var neighbor_count = 0
					var z_pos = zombie.global_position
					for other_zombie in zombies:
						if other_zombie is Zombie and other_zombie != zombie:
							if z_pos.distance_to(other_zombie.global_position) < 150:
								neighbor_count += 1
					if neighbor_count > max_neighbor_count:
						max_neighbor_count = neighbor_count
						target_zombie = zombie
	
	return target_zombie

func _find_nearest_zombie(max_range: float) -> Zombie:
	return _find_target_zombie(max_range)

func _get_nearest_zombies(max_range: float, count: int) -> Array[Zombie]:
	var zombies = get_tree().get_nodes_in_group("zombies")
	var candidates: Array[Zombie] = []
	for zombie in zombies:
		if zombie is Zombie:
			var distance = global_position.distance_to(zombie.global_position)
			if distance <= max_range:
				candidates.append(zombie)
	candidates.sort_custom(func(a, b):
		return global_position.distance_to(a.global_position) < global_position.distance_to(b.global_position)
	)
	var result: Array[Zombie] = []
	for zombie in candidates:
		result.append(zombie)
		if result.size() >= count:
			break
	return result

func _fire_projectile(target_pos: Vector2):
	can_attack = false
	attack_timer.start()
	
	var base_direction = (target_pos - global_position).normalized()
	var direction = base_direction.rotated(deg_to_rad(randf_range(-2.0, 2.0)))
	
	var bullet = _create_bullet()
	bullet.global_position = global_position
	bullet.velocity = direction * 500.0
	bullet.damage = current_damage
	bullet.trajectory_damage_ratio = trajectory_damage_ratio
	bullet.piercing_count = piercing_count
	bullet.is_returning = returning_projectiles
	bullet.bounces_left = bounce_count
	bullet.shooter = self
	
	get_parent().add_child(bullet)
	_create_muzzle_flash(direction)

func _create_bullet() -> Projectile:
	var bullet = Projectile.new()
	
	var bullet_sprite = Sprite2D.new()
	var bullet_image = Image.create(8, 3, false, Image.FORMAT_RGB8)
	bullet_image.fill(Color.YELLOW)
	var bullet_texture = ImageTexture.new()
	bullet_texture.set_image(bullet_image)
	bullet_sprite.texture = bullet_texture
	bullet.add_child(bullet_sprite)
	
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
	
	var missile = _create_magic_missile()
	missile.global_position = global_position
	missile.target = target
	missile.damage = current_damage * 1.2
	missile.shooter = self
	missile.charm_duration = charm_duration
	missile.push_force = push_force
	missile.stun_duration = stun_duration
	missile.dot_damage = dot_damage
	missile.chain_count = chain_count
	
	get_parent().add_child(missile)

func _create_magic_missile() -> MagicMissile:
	var missile = MagicMissile.new()
	
	var missile_sprite = Sprite2D.new()
	var missile_image = Image.create(12, 12, false, Image.FORMAT_RGB8)
	missile_image.fill(Color.MAGENTA)
	var missile_texture = ImageTexture.new()
	missile_texture.set_image(missile_image)
	missile_sprite.texture = missile_texture
	missile.add_child(missile_sprite)
	
	var missile_collision = CollisionShape2D.new()
	var missile_shape = CircleShape2D.new()
	missile_shape.radius = 6.0
	missile_collision.shape = missile_shape
	missile.add_child(missile_collision)
	
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
	# 创建更精美详细的玩家角色
	var image = Image.create(48, 56, false, Image.FORMAT_RGBA8)
	
	# 绘制玩家角色 - 精致的战士版本
	for y in range(56):
		for x in range(48):
			var color = Color.TRANSPARENT
			var center_x = 24
			var center_y = 28
			
			# 头部盔甲 (圆形，更精致)
			if _is_in_circle(x, y, center_x, center_y - 12, 10):
				# 金属反光效果
				var dist = Vector2(x - center_x, y - (center_y - 12)).length()
				if dist < 6:
					color = Color(0.7, 0.8, 0.9)  # 银色高光
				else:
					color = Color(0.4, 0.5, 0.6)  # 钢色盔甲
			
			# 头盔面罩
			elif y >= center_y - 8 and y <= center_y - 2 and x >= center_x - 8 and x <= center_x + 8:
				if y >= center_y - 6 and y <= center_y - 4 and (x == center_x - 6 or x == center_x + 6):
					color = Color.CYAN  # 眼镜光效
				else:
					color = Color(0.3, 0.4, 0.5)  # 面罩
			
			# 身体盔甲 (六边形设计)
			elif y >= center_y and y <= center_y + 18:
				var body_width = 14 - abs(y - center_y - 9) * 0.3
				if x >= center_x - body_width and x <= center_x + body_width:
					# 盔甲主体
					if y >= center_y + 2 and y <= center_y + 14:
						# 中心徽章
						if y >= center_y + 6 and y <= center_y + 10 and x >= center_x - 4 and x <= center_x + 4:
							if y >= center_y + 7 and y <= center_y + 9 and x >= center_x - 2 and x <= center_x + 2:
								color = Color.GOLD  # 徽章核心
							else:
								color = Color(0.8, 0.6, 0.2)  # 徽章边框
						else:
							# 盔甲反光条纹
							var stripe_pos = (x - center_x + body_width) / (body_width * 2)
							if stripe_pos > 0.3 and stripe_pos < 0.4:
								color = Color(0.6, 0.7, 0.8)  # 反光
							else:
								color = Color(0.2, 0.4, 0.7)  # 蓝色盔甲
					else:
						color = Color(0.25, 0.35, 0.6)  # 护肩
			
			# 手臂 (动态姿态)
			elif (x >= center_x - 18 and x <= center_x - 10 and y >= center_y + 4 and y <= center_y + 20):
				# 左臂举盾
				if y >= center_y + 4 and y <= center_y + 12:
					if x >= center_x - 16 and x <= center_x - 12:
						color = Color(0.6, 0.3, 0.8)  # 能量盾
					elif x >= center_x - 18 and x <= center_x - 16:
						color = Color(0.4, 0.2, 0.6)  # 盾边
				else:
					color = Color(0.3, 0.4, 0.5)  # 手臂盔甲
			elif (x >= center_x + 10 and x <= center_x + 16 and y >= center_y + 2 and y <= center_y + 18):
				# 右臂持武器
				color = Color(0.3, 0.4, 0.5)  # 手臂盔甲
			
			# 武器 (能量剑)
			elif x >= center_x + 16 and x <= center_x + 20:
				if y >= center_y - 8 and y <= center_y + 14:
					# 剑身光效
					if x >= center_x + 17 and x <= center_x + 19:
						var blade_pos = (y - (center_y - 8)) / 22.0
						if blade_pos > 0.3 and blade_pos < 0.7:
							color = Color.CYAN  # 剑身光
						else:
							color = Color(0.2, 0.8, 1.0)  # 青色剑身
					else:
						color = Color(0.1, 0.6, 0.8)  # 剑边
			# 剑柄
			elif x >= center_x + 15 and x <= center_x + 21 and y >= center_y + 14 and y <= center_y + 18:
				if y >= center_y + 15 and y <= center_y + 17:
					color = Color(0.7, 0.4, 0.2)  # 柄身
				else:
					color = Color(0.5, 0.3, 0.1)  # 柄端
			
			# 腿部 (装甲裤)
			elif y >= center_y + 18:
				# 左腿
				if x >= center_x - 8 and x <= center_x - 4 and y <= center_y + 28:
					# 膝盖护甲
					if y >= center_y + 22 and y <= center_y + 24:
						color = Color(0.5, 0.5, 0.6)  # 护膝
					else:
						color = Color(0.2, 0.3, 0.5)  # 腿甲
				# 右腿
				elif x >= center_x + 4 and x <= center_x + 8 and y <= center_y + 28:
					# 膝盖护甲
					if y >= center_y + 22 and y <= center_y + 24:
						color = Color(0.5, 0.5, 0.6)  # 护膝
					else:
						color = Color(0.2, 0.3, 0.5)  # 腿甲
				
				# 靴子
				elif ((x >= center_x - 9 and x <= center_x - 3) or (x >= center_x + 3 and x <= center_x + 9)) and y >= center_y + 26:
					color = Color(0.15, 0.15, 0.25)  # 深色靴子
			
			# 斗篷/披风
			elif y >= center_y - 4 and y <= center_y + 24:
				if (x >= center_x - 22 and x <= center_x - 18) or (x >= center_x + 18 and x <= center_x + 22):
					# 披风渐变
					var cape_alpha = 1.0 - abs(y - center_y - 10) / 14.0
					if x >= center_x + 18:
						color = Color(0.3, 0.1, 0.1, cape_alpha * 0.8)  # 红色披风
					else:
						color = Color(0.1, 0.1, 0.2, cape_alpha * 0.8)  # 深色内衬
			
			image.set_pixel(x, y, color)
	
	var texture = ImageTexture.new()
	texture.set_image(image)
	sprite.texture = texture
	
	# 添加呼吸动画
	_create_breathing_animation()

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

func _is_in_circle(x: int, y: int, center_x: int, center_y: int, radius: int) -> bool:
	var dx = x - center_x
	var dy = y - center_y
	return dx * dx + dy * dy <= radius * radius

func _create_breathing_animation():
	# 创建呼吸动画效果
	var tween = create_tween()
	tween.set_loops()
	tween.tween_property(sprite, "scale", Vector2(1.05, 1.05), 2.0).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	tween.tween_property(sprite, "scale", Vector2(0.95, 0.95), 2.0).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)

func _create_walking_animation():
	# 创建行走动画（当移动时）
	if not sprite:
		return
		
	# 简单的左右摇摆动画
	var sway_tween = create_tween()
	sway_tween.set_loops()
	sway_tween.tween_property(sprite, "rotation", deg_to_rad(3), 0.3).set_trans(Tween.TRANS_SINE)
	sway_tween.tween_property(sprite, "rotation", deg_to_rad(-3), 0.6).set_trans(Tween.TRANS_SINE)
	sway_tween.tween_property(sprite, "rotation", 0.0, 0.3).set_trans(Tween.TRANS_SINE)

func handle_movement():
	var input_vector = Vector2.ZERO
	
	if Input.is_action_pressed("move_right"):
		input_vector.x += 1
	if Input.is_action_pressed("move_left"):
		input_vector.x -= 1
	if Input.is_action_pressed("move_down"):
		input_vector.y += 1
	if Input.is_action_pressed("move_up"):
		input_vector.y -= 1
	
	if input_vector != Vector2.ZERO:
		input_vector = input_vector.normalized()
		velocity = input_vector * current_speed
	else:
		velocity = Vector2.ZERO

func _clamp_to_screen():
	var screen_size = get_viewport().get_visible_rect().size
	var min_pos = Vector2(bounds_padding, top_ui_margin)
	var max_pos = Vector2(screen_size.x - bounds_padding, screen_size.y - bounds_padding)
	global_position = global_position.clamp(min_pos, max_pos)

func apply_weapon_effect(weapon: WeaponCard):
	if weapon.category == WeaponCard.WeaponCategory.WEAPON:
		var kept_effects: Array[WeaponCard] = []
		for effect in active_effects:
			if effect.category != WeaponCard.WeaponCategory.WEAPON:
				kept_effects.append(effect)
		active_effects = kept_effects
	
	active_effects.append(weapon)
	_recalculate_stats()
	
	# 视觉反馈
	_create_upgrade_effect()
	effect_applied.emit(weapon.card_name)
	
	# 显示升级提示
	var effect_color = Color.GOLD
	match weapon.rarity:
		2: effect_color = Color(0.4, 0.7, 1.0)  # 稀有-蓝
		3: effect_color = Color(0.8, 0.4, 1.0)  # 史诗-紫
		4: effect_color = Color(1.0, 0.8, 0.2)  # 传说-金
	_show_status_effect_popup("+" + weapon.card_name, effect_color)
	
	print("✅ " + weapon.card_name + " - " + weapon.get_weapon_type_text())
	print("   " + weapon.get_effect_description())
	_update_status_displays()
	get_tree().call_group("ui_manager", "update_player_status", self)

func _apply_effect_values(weapon: WeaponCard):
	# 基础属性修改
	if weapon.damage_modifier != 1.0:
		damage_bonus += (weapon.damage_modifier - 1.0)
	current_speed = base_speed + weapon.speed_modifier
	
	if weapon.health_modifier != 0:
		max_health += int(weapon.health_modifier)
		current_health += int(weapon.health_modifier)
		current_health = clamp(current_health, 0, max_health)
		health_changed.emit(current_health)
	
	if weapon.critical_bonus > 0:
		critical_chance += weapon.critical_bonus
	if weapon.life_steal_bonus > 0:
		life_steal_percent += weapon.life_steal_bonus
	if weapon.freeze_bonus > 0:
		freeze_duration = max(freeze_duration, weapon.freeze_bonus)
	if weapon.poison_bonus > 0:
		poison_damage = max(poison_damage, weapon.poison_bonus)
	if weapon.grants_aura:
		activate_aura()
	
	# 武器专属参数
	if weapon.category == WeaponCard.WeaponCategory.WEAPON:
		weapon_attack_speed_modifier = weapon.attack_speed_modifier
		weapon_cooldown = weapon.cooldown
		weapon_range_modifier = weapon.range_modifier
		if weapon.health_cost > 0:
			health_cost_per_attack = weapon.health_cost
	elif weapon.attack_speed_modifier != 1.0:
		attack_speed_multiplier *= weapon.attack_speed_modifier
	
	# 特效只在武器类型匹配时生效
	if current_attack_type == AttackType.MELEE and weapon.weapon_type == WeaponCard.WeaponType.MELEE:
		for effect in weapon.melee_effects:
			var level = weapon.melee_effect_levels.get(effect, 1)
			_apply_melee_effect(effect, level)
	
	if current_attack_type == AttackType.RANGED and weapon.weapon_type == WeaponCard.WeaponType.RANGED:
		for effect in weapon.ranged_effects:
			var level = weapon.ranged_effect_levels.get(effect, 1)
			_apply_ranged_effect(effect, level)
	
	if current_attack_type == AttackType.MAGIC and weapon.weapon_type == WeaponCard.WeaponType.MAGIC:
		for effect in weapon.magic_effects:
			var level = weapon.magic_effect_levels.get(effect, 1)
			_apply_magic_effect(effect, level)

func _apply_melee_effect(effect: int, level: int):
	match effect:
		WeaponCard.MeleeEffect.SPLASH:
			splash_radius = 30.0 + level * 10.0
		WeaponCard.MeleeEffect.MULTI_TARGET:
			melee_target_count = max(melee_target_count, 1 + level)
		WeaponCard.MeleeEffect.KILL_EXPLOSION:
			kill_explosion_damage += level * 50
		WeaponCard.MeleeEffect.KILL_SHIELD:
			kill_shield_amount += level * 20
		WeaponCard.MeleeEffect.KNOCKBACK:
			knockback_force += level * 50

func _apply_ranged_effect(effect: int, level: int):
	match effect:
		WeaponCard.RangedEffect.MULTI_SHOT:
			multi_shot_count = max(multi_shot_count, 1 + level)
		WeaponCard.RangedEffect.TRAJECTORY_DAMAGE:
			trajectory_damage_ratio = max(trajectory_damage_ratio, level * 0.3)
		WeaponCard.RangedEffect.RETURNING:
			returning_projectiles = true
			bounce_count = max(bounce_count, level)
		WeaponCard.RangedEffect.PIERCING:
			piercing_count += level

func _apply_magic_effect(effect: int, level: int):
	match effect:
		WeaponCard.MagicEffect.CHARM:
			charm_duration = max(charm_duration, level * 5.0)
		WeaponCard.MagicEffect.PUSH:
			push_force += level * 80
		WeaponCard.MagicEffect.STUN:
			stun_duration = max(stun_duration, level * 1.5)
		WeaponCard.MagicEffect.CHAIN:
			chain_count = max(chain_count, level)
		WeaponCard.MagicEffect.DOT:
			dot_damage += level * 5

func take_damage(amount: int):
	if invincible_timer > 0:
		return
	if shield_amount > 0:
		var blocked = min(shield_amount, amount)
		shield_amount -= blocked
		amount -= blocked
		get_tree().call_group("ui_manager", "show_damage_number", global_position, -blocked, false)
		_show_status_effect_popup("护盾吸收", Color.CYAN)
	
	if amount > 0:
		current_health -= amount
		current_health = max(0, current_health)
		health_changed.emit(current_health)
		get_tree().call_group("ui_manager", "show_damage_number", global_position, amount, true)
		get_tree().call_group("ui_manager", "flash_damage")
		get_tree().call_group("ui_manager", "shake_screen", 5.0)  # 屏幕震动
		invincible_timer = invincible_duration
		
		# 增强的受击闪烁效果
		if sprite:
			var hurt_tween = create_tween()
			hurt_tween.tween_property(sprite, "modulate", Color(1.0, 0.3, 0.3), 0.05)
			hurt_tween.tween_property(sprite, "modulate", Color(1.0, 1.0, 1.0, 0.6), 0.1)
		
		if SoundManager.instance:
			SoundManager.play_player_hurt()
		
		_update_status_displays()
		
		# 低血量警告
		var health_percent = float(current_health) / float(max_health)
		if health_percent <= 0.25:
			_show_status_effect_popup("危险！", Color.RED)
		
		if current_health <= 0:
			die()

func apply_poison(damage_per_second: float, duration: float):
	poisoned_dps = max(poisoned_dps, damage_per_second)
	poisoned_timer = max(poisoned_timer, duration)
	poisoned_tick_timer = min(poisoned_tick_timer, 0.1)
	if sprite:
		sprite.modulate = Color(0.7, 0.3, 0.8)

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
	max_health = base_max_health
	current_speed = base_speed
	damage_bonus = 0.0
	speed_multiplier = 1.0
	multi_shot_count = 1
	piercing_count = 0
	area_damage = 0.0
	life_steal_percent = 0.0
	critical_chance = 0.0
	freeze_duration = 0.0
	poison_damage = 0.0
	bounce_count = 0
	health_cost_per_attack = 0
	attack_speed_multiplier = 1.0
	weapon_attack_speed_modifier = 1.0
	weapon_cooldown = 0.0
	weapon_range_modifier = 1.0
	
	var current_weapon: WeaponCard = null
	for effect in active_effects:
		if effect.category == WeaponCard.WeaponCategory.WEAPON:
			current_weapon = effect
			break
	
	if current_weapon:
		match current_weapon.weapon_type:
			WeaponCard.WeaponType.MELEE:
				current_attack_type = AttackType.MELEE
			WeaponCard.WeaponType.RANGED:
				current_attack_type = AttackType.RANGED
			WeaponCard.WeaponType.MAGIC:
				current_attack_type = AttackType.MAGIC
		target_priority = current_weapon.target_priority
	else:
		current_attack_type = AttackType.MELEE
	
	# 重新应用所有活跃效果
	for effect in active_effects:
		_apply_effect_values(effect)
	
	current_damage = base_damage * max(0.2, 1.0 + damage_bonus)
	
	var total_speed = max(0.1, weapon_attack_speed_modifier * attack_speed_multiplier)
	attack_cooldown = max(0.12, base_attack_cooldown / total_speed, weapon_cooldown)
	attack_timer.wait_time = attack_cooldown
	_update_attack_range(weapon_range_modifier)
	
	current_health = clamp(current_health, 0, max_health)

func _create_status_displays():
	# 创建血量显示
	health_display = Label.new()
	health_display.add_theme_font_size_override("font_size", 12)
	health_display.add_theme_color_override("font_color", Color.RED)
	health_display.position = Vector2(-20, -55)
	health_display.z_index = 10
	add_child(health_display)
	
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
	
	# 更新近战范围指示器可见性
	_update_melee_range_indicator()

func _create_melee_range_indicator():
	melee_range_indicator = Node2D.new()
	melee_range_indicator.z_index = -1  # 在玩家下方显示
	add_child(melee_range_indicator)
	_setup_inline_range_indicator()

func _setup_inline_range_indicator():
	# 使用简单的圆形精灵作为范围指示器
	var indicator_sprite = Sprite2D.new()
	indicator_sprite.name = "RangeSprite"
	var size = int(base_attack_range * weapon_range_modifier * 2)
	var img = Image.create(size, size, false, Image.FORMAT_RGBA8)
	_draw_range_circle(img, size)
	var tex = ImageTexture.create_from_image(img)
	indicator_sprite.texture = tex
	indicator_sprite.modulate.a = 0.3
	melee_range_indicator.add_child(indicator_sprite)

func _draw_range_circle(img: Image, size: int):
	@warning_ignore("integer_division")
	var center: int = size / 2
	var radius: int = center - 2
	for x in range(size):
		for y in range(size):
			var dist = Vector2(x - center, y - center).length()
			if dist >= radius - 2 and dist <= radius:
				# 边缘线
				img.set_pixel(x, y, Color(0.3, 0.8, 1.0, 0.6))
			elif dist < radius - 2:
				# 内部填充
				img.set_pixel(x, y, Color(0.2, 0.6, 0.9, 0.1))

func _update_melee_range_indicator():
	if not melee_range_indicator:
		return
	
	# 只在近战模式下显示
	var should_show = show_melee_range and current_attack_type == AttackType.MELEE
	melee_range_indicator.visible = should_show
	
	if should_show:
		# 更新范围大小
		var range_sprite = melee_range_indicator.get_node_or_null("RangeSprite")
		if range_sprite:
			var current_range = base_attack_range * weapon_range_modifier
			var expected_size = current_range * 2
			if range_sprite.texture:
				var tex_size = range_sprite.texture.get_size().x
				if abs(tex_size - expected_size) > 5:
					# 重新创建纹理
					var size = int(expected_size)
					var img = Image.create(size, size, false, Image.FORMAT_RGBA8)
					_draw_range_circle(img, size)
					var tex = ImageTexture.create_from_image(img)
					range_sprite.texture = tex

func handle_manual_attack():
	if current_attack_type == AttackType.MAGIC:
		return
	
	# 键盘空格键攻击
	if Input.is_action_pressed("attack"):
		if can_attack:
			perform_attack()
		return
	
	# 鼠标左键攻击（只在游戏区域内有效）
	if Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT):
		var mouse_pos = get_viewport().get_mouse_position()
		# 检查鼠标是否在游戏区域（排除顶部UI区域）
		if mouse_pos.y > top_ui_margin:
			if can_attack:
				perform_attack()

func handle_auto_attack(_delta):
	if current_attack_type != AttackType.MAGIC:
		return
	
	if not can_attack:
		return
	
	var target_zombie = _find_target_zombie(600.0)
	if target_zombie:
		_cast_magic_missile(target_zombie)

func perform_attack():
	match current_attack_type:
		AttackType.MELEE:
			if SoundManager.instance:
				SoundManager.play_melee_attack()
			_perform_melee_attack()
		AttackType.RANGED:
			if SoundManager.instance:
				SoundManager.play_ranged_attack()
			_perform_ranged_attack()
		AttackType.AREA:
			if SoundManager.instance:
				SoundManager.play_ranged_attack()
			_perform_area_attack()
		AttackType.MAGIC:
			if SoundManager.instance:
				SoundManager.play_magic_attack()
			_perform_magic_attack()

func handle_collision_repulsion():
	var zombies = get_tree().get_nodes_in_group("zombies")
	var repulsion_distance = 30.0
	var repulsion_force = 200.0
	
	for zombie in zombies:
		if zombie is Zombie:
			var distance = global_position.distance_to(zombie.global_position)
			if distance < repulsion_distance and distance > 0:
				var direction = (zombie.global_position - global_position).normalized()
				var push_factor = (repulsion_distance - distance) / repulsion_distance
				velocity += direction * push_factor * repulsion_force * 0.016

func _perform_melee_attack():
	if not _can_pay_health_cost():
		return
	_pay_health_cost()
	
	var zombies_in_range = attack_area.get_overlapping_bodies()
	var targets = []
	
	for zombie in zombies_in_range:
		if zombie is Zombie:
			targets.append(zombie)
			if targets.size() >= melee_target_count:
				break
	
	for zombie in targets:
		attack_zombie(zombie)
		_show_attack_effect(zombie.global_position)

func _perform_ranged_attack():
	if not _can_pay_health_cost():
		return
	_pay_health_cost()
	_update_status_displays()
	get_tree().call_group("ui_manager", "update_player_status", self)
	
	var target_zombie = _find_target_zombie(600.0)
	if not target_zombie:
		return
	
	if multi_shot_count > 1:
		var targets = _get_nearest_zombies(600.0, multi_shot_count)
		for zombie in targets:
			_fire_projectile(zombie.global_position)
		
		if targets.size() < multi_shot_count:
			var extra = multi_shot_count - targets.size()
			for i in range(extra):
				var offset = Vector2(randf_range(-25, 25), randf_range(-25, 25))
				_fire_projectile(target_zombie.global_position + offset)
	else:
		_fire_projectile(target_zombie.global_position)
	
	_show_attack_effect(target_zombie.global_position)

func _perform_area_attack():
	# 范围攻击消耗更多子弹
	if not _can_pay_health_cost():
		return
	_pay_health_cost()
	_update_status_displays()
	get_tree().call_group("ui_manager", "update_player_status", self)
	
	_create_area_explosion()
	_show_attack_effect(global_position)
	
	# 设置攻击冷却
	can_attack = false
	attack_timer.start()

func _perform_magic_attack():
	if not _can_pay_health_cost():
		return
	_pay_health_cost()
	var target_zombie = _find_target_zombie(600.0)
	if target_zombie:
		_cast_magic_missile(target_zombie)
		_show_attack_effect(target_zombie.global_position)

func activate_aura():
	# 如果已激活，不做任何事（只能通过升级卡片来升级）
	if aura_active:
		return
		
	aura_active = true
	
	# 创建视觉效果
	aura_visuals = Node2D.new()
	add_child(aura_visuals)
	aura_hitboxes.clear()
	for i in range(aura_orb_count):
		_create_aura_orb(i)
	_update_aura_orbs()

func upgrade_aura(extra_damage: float = 3.0, extra_radius: float = 15.0):
	# 只能通过升级卡片调用此函数来升级护盾
	if not aura_active:
		activate_aura()
		return
	aura_damage += extra_damage
	aura_radius += extra_radius
	_update_aura_orbs()

func _create_aura_orb(index: int):
	var orb_area = Area2D.new()
	orb_area.monitoring = true
	orb_area.monitorable = false
	orb_area.collision_layer = 0
	orb_area.collision_mask = 1  # 检测层1上的物体（僵尸）
	
	var orb_collision = CollisionShape2D.new()
	var orb_shape = CircleShape2D.new()
	orb_shape.radius = aura_orb_radius * 2  # 增大碰撞检测范围
	orb_collision.shape = orb_shape
	orb_area.add_child(orb_collision)
	
	# 创建更明显的视觉效果
	var orb_sprite = Sprite2D.new()
	var img = Image.create(16, 16, false, Image.FORMAT_RGBA8)
	for x in range(16):
		for y in range(16):
			var dist = Vector2(x - 8, y - 8).length()
			if dist < 7:
				var alpha = 1.0 - dist / 7.0
				img.set_pixel(x, y, Color(0.3, 0.8, 1.0, alpha * 0.9))
	var tex = ImageTexture.create_from_image(img)
	orb_sprite.texture = tex
	orb_area.add_child(orb_sprite)
	
	orb_area.set_meta("aura_index", index)
	aura_visuals.add_child(orb_area)
	aura_hitboxes.append(orb_area)

func _update_aura_orbs():
	if not aura_visuals:
		return
	for orb_area in aura_hitboxes:
		if not is_instance_valid(orb_area):
			continue
		var index = int(orb_area.get_meta("aura_index"))
		var angle = index * (PI * 2 / max(1, aura_orb_count))
		orb_area.position = Vector2(cos(angle), sin(angle)) * aura_radius
