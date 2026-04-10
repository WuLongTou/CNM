class_name Projectile
extends Area2D

var velocity: Vector2
var damage: float
var lifetime: float = 3.0
var trajectory_damage_ratio: float = 0.0
var piercing_count: int = 0
var is_returning: bool = false
var shooter: Node
var bounces_left: int = 0
var bounds_padding: float = 8.0
var top_ui_margin: float = 110.0

var damaged_enemies: Array = []

func _ready():
	body_entered.connect(_on_body_entered)
	area_entered.connect(_on_area_entered)
	
	var timer = Timer.new()
	timer.wait_time = lifetime
	timer.one_shot = true
	timer.timeout.connect(queue_free)
	add_child(timer)
	timer.start()

func _physics_process(delta):
	global_position += velocity * delta
	
	if velocity.length() > 0:
		rotation = velocity.angle()
	
	_handle_bounds_bounce()
	
	# 路径伤害检测
	if trajectory_damage_ratio > 0:
		_check_trajectory_collision()

func _check_trajectory_collision():
	var zombies = get_tree().get_nodes_in_group("zombies")
	for zombie in zombies:
		if zombie is Zombie and not damaged_enemies.has(zombie):
			var distance = global_position.distance_to(zombie.global_position)
			if distance < 30:
				var trajectory_damage = int(damage * trajectory_damage_ratio)
				if trajectory_damage > 0:
					zombie.take_damage(trajectory_damage)
					damaged_enemies.append(zombie)
					if shooter is Player:
						(shooter as Player).register_damage_dealt(zombie, trajectory_damage)
					get_tree().call_group("ui_manager", "show_damage_number", zombie.global_position, trajectory_damage, false)

func _on_body_entered(body):
	if body is Zombie and not damaged_enemies.has(body):
		body.take_damage(int(damage))
		_apply_shooter_effects(body)
		damaged_enemies.append(body)
		_create_hit_effect()
		
		if piercing_count > 0:
			piercing_count -= 1
		else:
			if is_returning:
				_bounce_from_target(body)
			else:
				queue_free()

func _on_area_entered(area):
	if area.has_method("take_damage"):
		if not damaged_enemies.has(area):
			area.take_damage(int(damage))
			_apply_shooter_effects(area)
			damaged_enemies.append(area)
			_create_hit_effect()
			
			if piercing_count <= 0:
				if is_returning:
					_bounce_from_target(area)
				else:
					queue_free()

func _destroy_or_return():
	if is_returning:
		_bounce_from_target(null)
	else:
		queue_free()

func _bounce_from_target(target):
	if bounces_left <= 0:
		queue_free()
		return
	
	var normal = Vector2.RIGHT
	if target and target is Node2D:
		normal = (global_position - target.global_position).normalized()
	
	velocity = velocity.bounce(normal)
	bounces_left -= 1
	damaged_enemies.clear()

func _handle_bounds_bounce():
	if not is_returning:
		return
	var screen_size = get_viewport().get_visible_rect().size
	var min_pos = Vector2(bounds_padding, top_ui_margin + bounds_padding)
	var max_pos = Vector2(screen_size.x - bounds_padding, screen_size.y - bounds_padding)
	var bounced = false
	
	if global_position.x <= min_pos.x or global_position.x >= max_pos.x:
		velocity.x = -velocity.x
		bounced = true
	if global_position.y <= min_pos.y or global_position.y >= max_pos.y:
		velocity.y = -velocity.y
		bounced = true
	
	if bounced:
		global_position = global_position.clamp(min_pos, max_pos)
		_bounce_from_target(null)

func _apply_shooter_effects(target):
	if shooter and shooter is Player:
		var player = shooter as Player
		if target is Zombie:
			player.register_damage_dealt(target, damage)
			if player.freeze_duration > 0 and randf() < (player.freeze_duration / 100.0):
				target.apply_freeze(0.8)
			if player.poison_damage > 0 and randf() < (player.poison_damage / 100.0):
				var dps = 3.0 + player.poison_damage * 0.2
				target.apply_poison(dps, 3.0)

func _create_hit_effect():
	# 命中粒子爆发
	var hit_particles = GPUParticles2D.new()
	hit_particles.global_position = global_position
	hit_particles.emitting = true
	hit_particles.amount = 20
	hit_particles.lifetime = 0.4
	hit_particles.one_shot = true

	var particle_material = ParticleProcessMaterial.new()
	particle_material.direction = Vector3(0, 0, 0)
	particle_material.initial_velocity_min = 40.0
	particle_material.initial_velocity_max = 100.0
	particle_material.gravity = Vector3(0, 60, 0)
	particle_material.scale_min = 0.5
	particle_material.scale_max = 1.2
	particle_material.color = Color(1.0, 0.85, 0.3, 0.9)
	hit_particles.process_material = particle_material
	get_parent().add_child(hit_particles)

	# 命中冲击圈
	var ring = Line2D.new()
	var pts = 16
	for i in range(pts + 1):
		var a = i * 2.0 * PI / pts
		ring.add_point(Vector2(cos(a), sin(a)) * 8.0)
	ring.width = 2.0
	ring.default_color = Color(1.0, 0.9, 0.4, 0.8)
	var ring_node = Node2D.new()
	ring_node.global_position = global_position
	ring_node.add_child(ring)
	get_parent().add_child(ring_node)

	var ring_tw = ring_node.create_tween()
	ring_tw.set_parallel(true)
	ring_tw.tween_property(ring_node, "scale", Vector2(3.0, 3.0), 0.25).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	ring_tw.tween_property(ring_node, "modulate:a", 0.0, 0.25)
	ring_tw.set_parallel(false)
	ring_tw.tween_callback(ring_node.queue_free)

	var timer = Timer.new()
	timer.wait_time = 0.8
	timer.one_shot = true
	timer.timeout.connect(hit_particles.queue_free)
	hit_particles.add_child(timer)
	timer.start()
