class_name ZombieVFX
extends Node
## 僵尸视觉效果组件
## 负责：精灵生成、动画、状态标签、伤害弹出、死亡/治疗特效

# 类型视觉数据 { color, size }
const TYPE_VISUALS: Dictionary = {
	Zombie.ZombieType.BASIC:     [Color(0.40, 0.55, 0.30), 36],
	Zombie.ZombieType.FAST:      [Color(0.75, 0.60, 0.25), 28],
	Zombie.ZombieType.TANK:      [Color(0.65, 0.20, 0.18), 52],
	Zombie.ZombieType.EXPLODER:  [Color(0.70, 0.25, 0.60), 34],
	Zombie.ZombieType.INVISIBLE: [Color(0.35, 0.55, 0.65), 34],
	Zombie.ZombieType.SPITTER:   [Color(0.25, 0.70, 0.20), 40],
	Zombie.ZombieType.HEALER:    [Color(0.25, 0.80, 0.40), 38],
}

# 死亡粒子颜色
const DEATH_COLORS: Dictionary = {
	Zombie.ZombieType.BASIC:     Color(0.4, 0.55, 0.3),
	Zombie.ZombieType.FAST:      Color(0.8, 0.7, 0.3),
	Zombie.ZombieType.TANK:      Color(0.6, 0.25, 0.2),
	Zombie.ZombieType.EXPLODER:  Color(1.0, 0.5, 0.1),
	Zombie.ZombieType.INVISIBLE: Color(0.4, 0.7, 0.8),
	Zombie.ZombieType.SPITTER:   Color(0.2, 0.8, 0.2),
	Zombie.ZombieType.HEALER:    Color(0.3, 0.9, 0.5),
}

var zombie: Zombie

# 节点引用
var sprite: Sprite2D
var shadow: Sprite2D
var health_label: Label
var attack_label: Label
var elite_ring: Line2D

func setup(z: Zombie):
	zombie = z

func create_visuals():
	_create_shadow()
	sprite = Sprite2D.new()
	zombie.add_child(sprite)
	_draw_zombie_sprite()
	_create_status_labels()
	if zombie.zombie_type == Zombie.ZombieType.INVISIBLE:
		sprite.modulate.a = 0.3

func _create_shadow():
	shadow = Sprite2D.new()
	var w := int(GameConfig.ZOMBIE_SHADOW_SIZE.x)
	var h := int(GameConfig.ZOMBIE_SHADOW_SIZE.y)
	shadow.texture = _make_ellipse_texture(w, h, GameConfig.ZOMBIE_SHADOW_COLOR)
	shadow.position = GameConfig.ZOMBIE_SHADOW_OFFSET
	zombie.add_child(shadow)

# ═══════════════════════════════════════════════
# 精灵生成
# ═══════════════════════════════════════════════

func _draw_zombie_sprite():
	var vis: Array = TYPE_VISUALS.get(zombie.zombie_type, TYPE_VISUALS[Zombie.ZombieType.BASIC])
	var base_color: Color = vis[0]
	var size: int = vis[1]

	var image := Image.create(size, size, false, Image.FORMAT_RGBA8)
	var cx := size / 2.0
	var cy := size / 2.0

	for y in range(size):
		for x in range(size):
			var color := _compute_pixel(x, y, cx, cy, size, base_color)
			color = _apply_type_overlay(x, y, cx, cy, size, base_color, color)
			image.set_pixel(x, y, color)

	var texture := ImageTexture.new()
	texture.set_image(image)
	sprite.texture = texture

	_start_idle_animation()

	if zombie.is_elite:
		_create_elite_ring(size * 0.7, base_color.lightened(0.4))
		sprite.scale = GameConfig.ZOMBIE_ELITE_SCALE

func _compute_pixel(x: int, y: int, cx: float, cy: float, size: int, base_color: Color) -> Color:
	var color := Color.TRANSPARENT
	var head_r := size * 0.22
	var head_cy := cy * 0.52

	# 头部
	var hd := Vector2(x - cx, y - head_cy).length()
	if hd <= head_r:
		color = base_color
		if hd > head_r * 0.65 and ((x + y) % 3 == 0):
			color = base_color.darkened(0.35)
		# 眼睛
		var le := Vector2(cx - size * 0.09, head_cy)
		var re := Vector2(cx + size * 0.09, head_cy)
		if Vector2(x - le.x, y - le.y).length() <= size * 0.045:
			color = Color(1.0, 0.15, 0.10)
		elif Vector2(x - re.x, y - re.y).length() <= size * 0.045:
			color = Color(1.0, 0.15, 0.10)
		# 嘴
		if y >= head_cy + head_r * 0.4 and y <= head_cy + head_r * 0.7:
			if abs(x - cx) <= head_r * 0.5:
				color = Color(0.08, 0.0, 0.0)

	# 身体
	elif y >= cy * 0.78 and y <= cy * 1.4:
		var bw: float = size * 0.20 - abs(y - cy * 1.1) * 0.03
		if abs(x - cx) <= bw:
			color = base_color.darkened(0.15)
			if int(y) % 4 == 0 and abs(x - cx) > bw * 0.5:
				color = Color(0.20, 0.18, 0.15)
			if y >= cy * 0.9 and y <= cy * 1.15 and int(y) % 3 == 0 and abs(x - cx) <= bw * 0.6:
				color = base_color.darkened(0.4)

	# 手臂
	elif y >= cy * 0.85 and y <= cy * 1.5:
		var la := cx - size * 0.34 + sin(y * 0.25) * 2
		var ra := cx + size * 0.32 + sin(y * 0.25 + PI) * 2
		if abs(x - la) <= size * 0.055:
			color = base_color.darkened(0.2)
			if int(y) % 5 == 0:
				color = base_color.darkened(0.45)
		elif abs(x - ra) <= size * 0.055:
			color = base_color.darkened(0.2)
			if int(y) % 5 == 0:
				color = base_color.darkened(0.45)

	# 腿部
	elif y >= cy * 1.4:
		var llx := cx - size * 0.11 + (y - cy * 1.4) * 0.07
		var rlx := cx + size * 0.10 - (y - cy * 1.4) * 0.05
		if abs(x - llx) <= size * 0.07:
			color = base_color.darkened(0.30)
		elif abs(x - rlx) <= size * 0.07:
			color = base_color.darkened(0.30)

	return color

func _apply_type_overlay(x: int, y: int, cx: float, cy: float, size: int, base_color: Color, color: Color) -> Color:
	match zombie.zombie_type:
		Zombie.ZombieType.TANK:
			if color != Color.TRANSPARENT:
				if y < size * 0.15 or y > size * 0.88:
					color = Color(0.35, 0.30, 0.28)
				if x < size * 0.12 or x > size * 0.88:
					color = Color(0.35, 0.30, 0.28)
			if ((x < size * 0.16 or x > size * 0.84) and int(y) % 7 == 3):
				if color != Color.TRANSPARENT:
					color = Color(0.50, 0.48, 0.45)
			if y >= cy * 0.6 and y <= cy * 0.8:
				if (abs(x - (cx - size * 0.3)) <= 4 or abs(x - (cx + size * 0.3)) <= 4):
					color = Color(0.40, 0.35, 0.30)

		Zombie.ZombieType.FAST:
			if color != Color.TRANSPARENT:
				color = color.lightened(0.15)
			if (x <= 2 or x >= size - 3) and int(y) % 2 == 0 and y > size * 0.3 and y < size * 0.7:
				color = Color(0.9, 0.8, 0.3, 0.5)

		Zombie.ZombieType.EXPLODER:
			var core_d := Vector2(x - cx, y - cy * 1.15).length()
			if core_d <= size * 0.10:
				color = Color(1.0, 0.6, 0.1, 0.95)
			elif core_d <= size * 0.15 and core_d > size * 0.10:
				if color != Color.TRANSPARENT:
					color = base_color.lightened(0.4)

		Zombie.ZombieType.INVISIBLE:
			if color != Color.TRANSPARENT:
				color.a = 0.55
			var outline_d := Vector2(x - cx, y - cy).length()
			if outline_d >= size * 0.38 and outline_d <= size * 0.42:
				color = Color(0.4, 0.8, 0.9, 0.25)

		Zombie.ZombieType.SPITTER:
			var cheek_l := Vector2(x - (cx - size * 0.15), y - (cy * 0.55)).length()
			var cheek_r := Vector2(x - (cx + size * 0.15), y - (cy * 0.55)).length()
			if cheek_l <= size * 0.08 or cheek_r <= size * 0.08:
				color = Color(0.15, 0.85, 0.15, 0.9)
			if abs(x - cx) <= 1 and y >= cy * 0.7 and y <= cy * 0.85:
				color = Color(0.10, 0.75, 0.10, 0.7)

		Zombie.ZombieType.HEALER:
			if y >= cy * 0.85 and y <= cy * 1.15:
				if abs(x - cx) <= 2:
					color = Color.WHITE
			if abs(y - cy) <= 2 and y >= cy * 0.9 and y <= cy * 1.1:
				if x >= cx - size * 0.1 and x <= cx + size * 0.1:
					color = Color.WHITE
			var hd2 := Vector2(x - cx, y - cy).length()
			if hd2 >= size * 0.42 and hd2 <= size * 0.45:
				if int(x + y) % 4 == 0:
					color = Color(0.3, 1.0, 0.5, 0.4)

	return color

# ═══════════════════════════════════════════════
# 动画
# ═══════════════════════════════════════════════

func _start_idle_animation():
	match zombie.zombie_type:
		Zombie.ZombieType.EXPLODER:
			var tw := zombie.create_tween()
			tw.set_loops()
			tw.tween_property(sprite, "scale", Vector2(1.1, 1.1), 1.5).set_trans(Tween.TRANS_SINE)
			tw.tween_property(sprite, "scale", Vector2(0.9, 0.9), 1.5).set_trans(Tween.TRANS_SINE)

		Zombie.ZombieType.FAST:
			var tw := zombie.create_tween()
			tw.set_loops()
			tw.tween_property(sprite, "position:x", 2, 0.1)
			tw.tween_property(sprite, "position:x", -2, 0.1)
			tw.tween_property(sprite, "position:x", 0, 0.1)

		Zombie.ZombieType.TANK:
			var tw := zombie.create_tween()
			tw.set_loops()
			tw.tween_property(sprite, "position:y", 2, 2.0).set_trans(Tween.TRANS_SINE)
			tw.tween_property(sprite, "position:y", 0, 2.0).set_trans(Tween.TRANS_SINE)

		Zombie.ZombieType.HEALER:
			var tw := zombie.create_tween()
			tw.set_loops()
			tw.tween_property(sprite, "rotation", PI * 2, 4.0)

		_:
			var tw := zombie.create_tween()
			tw.set_loops()
			tw.tween_property(sprite, "rotation", deg_to_rad(5), 3.0).set_trans(Tween.TRANS_SINE)
			tw.tween_property(sprite, "rotation", deg_to_rad(-5), 3.0).set_trans(Tween.TRANS_SINE)

func _create_elite_ring(radius: float, _color: Color):
	elite_ring = Line2D.new()
	var pts := 24
	for i in range(pts + 1):
		var angle := i * 2.0 * PI / pts
		elite_ring.add_point(Vector2(cos(angle), sin(angle)) * radius)
	elite_ring.width = 2.5
	elite_ring.default_color = GameConfig.ZOMBIE_ELITE_RING_COLOR
	elite_ring.z_index = -1
	zombie.add_child(elite_ring)
	var tw := zombie.create_tween()
	tw.set_loops()
	tw.tween_property(elite_ring, "rotation", PI * 2, 3.0)

# ═══════════════════════════════════════════════
# 状态标签
# ═══════════════════════════════════════════════

func _create_status_labels():
	health_label = Label.new()
	health_label.add_theme_font_size_override("font_size", GameConfig.ZOMBIE_STATUS_FONT_SIZE)
	health_label.add_theme_color_override("font_color", Color.RED)
	health_label.position = GameConfig.ZOMBIE_STATUS_HP_OFFSET
	health_label.z_index = 10
	zombie.add_child(health_label)

	attack_label = Label.new()
	attack_label.add_theme_font_size_override("font_size", GameConfig.ZOMBIE_STATUS_FONT_SIZE)
	attack_label.add_theme_color_override("font_color", Color.ORANGE)
	attack_label.position = GameConfig.ZOMBIE_STATUS_ATK_OFFSET
	attack_label.z_index = 10
	zombie.add_child(attack_label)

	update_status_labels()

func update_status_labels():
	if health_label:
		health_label.text = "❤️" + str(zombie.current_health) + "/" + str(zombie.max_health)
	if attack_label:
		attack_label.text = "⚔️" + str(zombie.damage)

# ═══════════════════════════════════════════════
# 受击特效
# ═══════════════════════════════════════════════

func flash_hit():
	if not sprite:
		return
	var tw := zombie.create_tween()
	tw.tween_property(sprite, "modulate", Color(2.0, 2.0, 2.0), GameConfig.ZOMBIE_HIT_FLASH_IN)
	tw.tween_property(sprite, "modulate", Color.WHITE, GameConfig.ZOMBIE_HIT_FLASH_OUT)
	# 击退微动
	var kb := Vector2(randf_range(-1, 1), randf_range(-1, 1)).normalized() * GameConfig.ZOMBIE_KNOCKBACK_DIST
	var pos_tw := zombie.create_tween()
	pos_tw.tween_property(zombie, "position", zombie.position + kb, GameConfig.ZOMBIE_HIT_FLASH_IN)
	pos_tw.tween_property(zombie, "position", zombie.position, GameConfig.ZOMBIE_HIT_FLASH_OUT)

func show_damage_popup(amount: int):
	var popup := Label.new()
	popup.text = str(amount)
	popup.global_position = zombie.global_position + Vector2(randf_range(-10, 10), -25)
	popup.add_theme_font_size_override("font_size", GameConfig.ZOMBIE_DAMAGE_POPUP_SIZE)
	popup.add_theme_color_override("font_color", Color.WHITE)
	popup.add_theme_constant_override("outline_size", 2)
	popup.add_theme_color_override("font_outline_color", Color.BLACK)
	popup.z_index = 15
	zombie.get_parent().add_child(popup)

	var tw := popup.create_tween()
	tw.parallel().tween_property(popup, "position:y", popup.position.y - 30, 0.5)
	tw.parallel().tween_property(popup, "modulate:a", 0.0, 0.5).set_delay(0.2)
	tw.tween_callback(popup.queue_free)

# ═══════════════════════════════════════════════
# 状态效果视觉
# ═══════════════════════════════════════════════

func show_frozen():
	if sprite:
		sprite.modulate = Color.CYAN

func show_stunned():
	if sprite:
		sprite.modulate = Color(0.8, 0.8, 1.0)

func show_poisoned():
	if sprite:
		sprite.modulate = Color.PURPLE

func show_charmed():
	if sprite:
		sprite.modulate = Color.MAGENTA

func show_invisible(revealed: bool):
	if sprite:
		sprite.modulate.a = 1.0 if revealed else 0.3

func clear_effect():
	if sprite:
		sprite.modulate = Color.WHITE

# ═══════════════════════════════════════════════
# 死亡特效
# ═══════════════════════════════════════════════

func spawn_death_effects():
	_spawn_corpse()
	_spawn_death_particles()
	_spawn_blood_stain()

func _spawn_corpse():
	if not sprite or not sprite.texture:
		return
	var corpse := Sprite2D.new()
	corpse.texture = sprite.texture
	corpse.global_position = zombie.global_position
	corpse.modulate = Color(0.6, 0.4, 0.3, 0.8)
	corpse.z_index = -1
	zombie.get_parent().add_child(corpse)
	var tw := corpse.create_tween()
	tw.set_parallel(true)
	tw.tween_property(corpse, "scale", Vector2(0.1, 0.1), GameConfig.ZOMBIE_CORPSE_FADE).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_IN)
	tw.tween_property(corpse, "rotation", randf_range(-PI, PI), GameConfig.ZOMBIE_CORPSE_FADE)
	tw.tween_property(corpse, "modulate:a", 0.0, GameConfig.ZOMBIE_CORPSE_FADE)
	tw.set_parallel(false)
	tw.tween_callback(corpse.queue_free)

func _spawn_death_particles():
	var death_color: Color = DEATH_COLORS.get(zombie.zombie_type, Color(0.4, 0.55, 0.3))
	var particle_count := GameConfig.ZOMBIE_DEATH_PARTICLES_DEFAULT
	if zombie.zombie_type == Zombie.ZombieType.TANK:
		particle_count = GameConfig.ZOMBIE_DEATH_PARTICLES_TANK
	elif zombie.zombie_type == Zombie.ZombieType.EXPLODER:
		particle_count = GameConfig.ZOMBIE_DEATH_PARTICLES_EXPLODER

	var particles := GPUParticles2D.new()
	particles.global_position = zombie.global_position
	particles.emitting = true
	particles.amount = particle_count
	particles.lifetime = 0.7
	particles.one_shot = true

	var mat := ParticleProcessMaterial.new()
	mat.direction = Vector3(0, -1, 0)
	mat.initial_velocity_min = 40.0
	mat.initial_velocity_max = 140.0
	mat.gravity = Vector3(0, 180, 0)
	mat.scale_min = 1.5
	mat.scale_max = 4.0
	mat.color = death_color
	particles.process_material = mat
	zombie.get_parent().add_child(particles)

	# 爆炸僵尸冲击波
	if zombie.zombie_type == Zombie.ZombieType.EXPLODER:
		_spawn_explosion_ring()

	var timer := Timer.new()
	timer.wait_time = 1.2
	timer.one_shot = true
	timer.timeout.connect(particles.queue_free)
	particles.add_child(timer)
	timer.start()

func _spawn_explosion_ring():
	var ring := Line2D.new()
	var pts := 24
	for i in range(pts + 1):
		var a := i * 2.0 * PI / pts
		ring.add_point(Vector2(cos(a), sin(a)) * 15.0)
	ring.width = 3.0
	ring.default_color = Color(1.0, 0.5, 0.1, 0.9)
	var ring_node := Node2D.new()
	ring_node.global_position = zombie.global_position
	ring_node.add_child(ring)
	zombie.get_parent().add_child(ring_node)
	var tw := ring_node.create_tween()
	tw.set_parallel(true)
	tw.tween_property(ring_node, "scale", Vector2(6, 6), 0.4).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	tw.tween_property(ring_node, "modulate:a", 0.0, 0.4)
	tw.set_parallel(false)
	tw.tween_callback(ring_node.queue_free)

func _spawn_blood_stain():
	var blood := ColorRect.new()
	blood.size = Vector2(randf_range(12, 24), randf_range(6, 14))
	blood.global_position = zombie.global_position - blood.size * 0.5
	blood.color = Color(0.35, 0.06, 0.04, 0.4)
	blood.rotation = randf_range(-0.5, 0.5)
	blood.z_index = -2
	zombie.get_parent().add_child(blood)
	var tw := blood.create_tween()
	tw.tween_property(blood, "modulate:a", 0.0, GameConfig.ZOMBIE_BLOOD_FADE)
	tw.tween_callback(blood.queue_free)

# ═══════════════════════════════════════════════
# 治疗特效
# ═══════════════════════════════════════════════

func show_heal_effect(target_pos: Vector2, amount: int):
	var heal_label := Label.new()
	heal_label.text = "+" + str(amount)
	heal_label.global_position = target_pos + Vector2(randf_range(-10, 10), -20)
	heal_label.add_theme_font_size_override("font_size", 12)
	heal_label.add_theme_color_override("font_color", Color(0.3, 1.0, 0.5))
	heal_label.add_theme_constant_override("outline_size", 2)
	heal_label.add_theme_color_override("font_outline_color", Color.BLACK)
	heal_label.z_index = 15
	zombie.get_parent().add_child(heal_label)

	var tw := heal_label.create_tween()
	tw.parallel().tween_property(heal_label, "position:y", heal_label.position.y - 25, 0.5)
	tw.parallel().tween_property(heal_label, "modulate:a", 0.0, 0.5).set_delay(0.2)
	tw.tween_callback(heal_label.queue_free)

func show_heal_pulse(heal_range: float):
	var ring := Node2D.new()
	ring.global_position = zombie.global_position

	var line := Line2D.new()
	var pts := 20
	for i in range(pts + 1):
		var angle := i * 2.0 * PI / pts
		line.add_point(Vector2(cos(angle), sin(angle)) * 20.0)
	line.width = 2.0
	line.default_color = Color(0.3, 1.0, 0.5, 0.8)
	ring.add_child(line)
	zombie.get_parent().add_child(ring)

	var tw := ring.create_tween()
	tw.parallel().tween_property(ring, "scale", Vector2(heal_range / 20.0, heal_range / 20.0), 0.4)
	tw.parallel().tween_property(ring, "modulate:a", 0.0, 0.4)
	tw.tween_callback(ring.queue_free)

# ═══════════════════════════════════════════════
# 工具
# ═══════════════════════════════════════════════

func _make_ellipse_texture(width: int, height: int, color: Color) -> Texture2D:
	var image := Image.create(width, height, false, Image.FORMAT_RGBA8)
	var cx := width / 2.0
	var cy := height / 2.0
	for y in range(height):
		for x in range(width):
			var dx := (x - cx) / (width * 0.5)
			var dy := (y - cy) / (height * 0.5)
			if dx * dx + dy * dy <= 1.0:
				image.set_pixel(x, y, color)
	var texture := ImageTexture.new()
	texture.set_image(image)
	return texture
