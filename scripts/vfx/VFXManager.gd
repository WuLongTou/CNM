class_name VFXManager
extends Node
## 集中管理所有视觉效果，消除多个文件中的重复效果创建代码
## 使用方法：VFXManager.spawn_*(parent_node, params)

static var instance: VFXManager

func _ready():
	instance = self

func _exit_tree():
	if instance == self:
		instance = null

# ═══════════════════════════════════════════════
# 冲击环
# ═══════════════════════════════════════════════

## 在指定位置创建扩散冲击环
static func spawn_impact_ring(parent: Node, pos: Vector2, radius: float, color: Color, line_width: float = 2.0) -> Node2D:
	var ring = Node2D.new()
	ring.global_position = pos

	var line = Line2D.new()
	var pts = GameConfig.VFX_IMPACT_RING_SEGMENTS
	for i in range(pts + 1):
		var angle = i * TAU / pts
		line.add_point(Vector2(cos(angle), sin(angle)) * radius)
	line.width = line_width
	line.default_color = color
	ring.add_child(line)

	parent.add_child(ring)

	var tw = ring.create_tween()
	tw.parallel().tween_property(ring, "scale", GameConfig.VFX_RING_EXPAND_SCALE, GameConfig.VFX_RING_EXPAND_DURATION) \
		.set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	tw.parallel().tween_property(ring, "modulate:a", 0.0, GameConfig.VFX_RING_EXPAND_DURATION)
	tw.tween_callback(ring.queue_free)
	return ring

# ═══════════════════════════════════════════════
# 粒子爆发
# ═══════════════════════════════════════════════

## 在指定位置创建一次性粒子爆发
static func spawn_particle_burst(parent: Node, pos: Vector2, color: Color,
		count: int = 12, lifetime: float = 0.4, vel_min: float = 40.0,
		vel_max: float = 100.0, gravity := Vector3.ZERO,
		scale_min: float = 0.8, scale_max: float = 1.5) -> GPUParticles2D:
	var particles = GPUParticles2D.new()
	particles.global_position = pos
	particles.emitting = true
	particles.amount = count
	particles.lifetime = lifetime
	particles.one_shot = true

	var mat = ParticleProcessMaterial.new()
	mat.color = color
	mat.gravity = gravity
	mat.initial_velocity_min = vel_min
	mat.initial_velocity_max = vel_max
	mat.scale_min = scale_min
	mat.scale_max = scale_max
	particles.process_material = mat

	parent.add_child(particles)

	# 自动清理
	_auto_free(parent, particles, lifetime + 0.5)
	return particles

# ═══════════════════════════════════════════════
# 命中效果（粒子 + 冲击环组合）
# ═══════════════════════════════════════════════

## 命中反馈效果
static func spawn_hit_effect(parent: Node, pos: Vector2, color: Color = Color.CYAN, intensity: float = 1.0):
	spawn_particle_burst(parent, pos, color,
		int(GameConfig.VFX_HIT_PARTICLE_COUNT * intensity),
		GameConfig.VFX_HIT_LIFETIME * intensity,
		20.0 * intensity, 60.0 * intensity)
	spawn_impact_ring(parent, pos, 20.0 * intensity, color)

# ═══════════════════════════════════════════════
# 浮动文字
# ═══════════════════════════════════════════════

## 通用浮动文字弹出（伤害数字、状态提示等）
static func spawn_floating_label(parent: Node, pos: Vector2, text: String,
		color: Color, font_size: int = 14, float_dist: float = -1,
		duration: float = -1, delay_fade: float = 0.3):
	if float_dist < 0:
		float_dist = GameConfig.VFX_POPUP_FLOAT_DISTANCE
	if duration < 0:
		duration = GameConfig.VFX_POPUP_DURATION

	var label = Label.new()
	label.text = text
	label.global_position = pos + Vector2(randf_range(-15, 15), randf_range(-25, -10))
	label.z_index = 30
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", color)
	label.add_theme_constant_override("outline_size", 3)
	label.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.9))
	parent.add_child(label)

	var tw = label.create_tween()
	tw.parallel().tween_property(label, "position:y", label.position.y - float_dist, duration) \
		.set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	tw.parallel().tween_property(label, "modulate:a", 0.0, duration).set_delay(delay_fade)
	tw.tween_callback(label.queue_free)

# ═══════════════════════════════════════════════
# 闪电链效果
# ═══════════════════════════════════════════════

## 两点间的锯齿闪电效果
static func spawn_chain_lightning(parent: Node, from: Vector2, to: Vector2,
		color: Color = Color(0.6, 0.8, 1.0), glow_color: Color = Color(0.3, 0.5, 0.8, 0.4)):
	var dist = from.distance_to(to)
	var segments = max(4, int(dist / 15.0))
	var direction = (to - from)

	# 主闪电线
	var line = Line2D.new()
	for i in range(segments + 1):
		var t = float(i) / segments
		var base_point = from + direction * t
		var jitter = randf_range(-8, 8) * (1.0 - abs(t - 0.5) * 2)
		var perp = direction.normalized().rotated(PI / 2)
		line.add_point(base_point + perp * jitter)
	line.width = 3.0
	line.default_color = color
	parent.add_child(line)

	# 外发光层
	var glow = Line2D.new()
	glow.points = line.points
	glow.width = 8.0
	glow.default_color = glow_color
	parent.add_child(glow)

	# 淡出
	var tw = line.create_tween()
	tw.parallel().tween_property(line, "modulate:a", 0.0, 0.25)
	tw.parallel().tween_property(glow, "modulate:a", 0.0, 0.25)
	tw.tween_callback(line.queue_free)
	tw.tween_callback(glow.queue_free)

# ═══════════════════════════════════════════════
# 精灵闪烁
# ═══════════════════════════════════════════════

## 让 sprite 短暂闪白再恢复
static func flash_sprite(node: Node, sprite: Node2D, flash_color: Color = Color(1.8, 1.8, 1.8),
		flash_duration: float = 0.03, restore_duration: float = 0.07):
	if not sprite:
		return
	var tw = node.create_tween()
	tw.tween_property(sprite, "modulate", flash_color, flash_duration)
	tw.tween_property(sprite, "modulate", Color.WHITE, restore_duration)

## 受伤闪红
static func flash_hurt(node: Node, sprite: Node2D):
	if not sprite:
		return
	var tw = node.create_tween()
	tw.tween_property(sprite, "modulate", Color(1.0, 0.3, 0.3), 0.05)
	tw.tween_property(sprite, "modulate", Color(1.0, 1.0, 1.0, 0.6), 0.1)

# ═══════════════════════════════════════════════
# 残影
# ═══════════════════════════════════════════════

## 创建半透明残影精灵
static func spawn_ghost_trail(parent: Node, source_sprite: Sprite2D, pos: Vector2,
		color: Color = Color(0.3, 0.6, 0.9, 0.5), fade_time: float = 0.25):
	if not source_sprite or not source_sprite.texture:
		return
	var ghost = Sprite2D.new()
	ghost.texture = source_sprite.texture
	ghost.global_position = pos
	ghost.flip_h = source_sprite.flip_h
	ghost.modulate = color
	ghost.z_index = source_sprite.z_index - 1
	parent.add_child(ghost)

	var tw = ghost.create_tween()
	tw.tween_property(ghost, "modulate:a", 0.0, fade_time)
	tw.tween_callback(ghost.queue_free)

# ═══════════════════════════════════════════════
# 工具
# ═══════════════════════════════════════════════

static func _auto_free(parent: Node, target: Node, delay: float):
	var timer = parent.get_tree().create_timer(delay)
	timer.timeout.connect(func():
		if is_instance_valid(target):
			target.queue_free()
	)
