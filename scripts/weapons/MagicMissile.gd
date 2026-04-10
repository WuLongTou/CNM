class_name MagicMissile
extends Area2D

var target: Zombie
var damage: float
var speed: float = 300.0
var homing_strength: float = 5.0
var lifetime: float = 5.0
var shooter: Node

var charm_duration: float = 0.0
var push_force: float = 0.0
var stun_duration: float = 0.0
var dot_damage: float = 0.0
var chain_count: int = 0

var chain_targets: Array = []

func _ready():
	body_entered.connect(_on_body_entered)
	
	var timer = Timer.new()
	timer.wait_time = lifetime
	timer.one_shot = true
	timer.timeout.connect(queue_free)
	add_child(timer)
	timer.start()

func _physics_process(delta):
	if target and is_instance_valid(target):
		var to_target = target.global_position - global_position
		var dist = to_target.length()
		var direction = to_target.normalized()

		# 距离越近转向力越强，避免绕圈
		var adaptive_strength = homing_strength + 20.0 / maxf(dist, 10.0) * 100.0

		# 足够近时直冲目标
		if dist < 30.0:
			global_position += direction * speed * delta
		else:
			var current_velocity = Vector2(cos(rotation), sin(rotation)) * speed
			var blend = minf(adaptive_strength * delta, 1.0)
			var new_velocity = current_velocity.lerp(direction * speed, blend)
			global_position += new_velocity * delta
			rotation = new_velocity.angle()
	else:
		global_position += Vector2(cos(rotation), sin(rotation)) * speed * delta

func _on_body_entered(body):
	if body is Zombie and not chain_targets.has(body):
		_deal_damage(body)
		
		if chain_count > 0 and chain_targets.size() < chain_count:
			_chain_lightning(body)
		else:
			_create_magic_explosion()
			queue_free()

func _deal_damage(zombie: Zombie):
	zombie.take_damage(int(damage))
	if shooter is Player:
		(shooter as Player).register_damage_dealt(zombie, damage)

	# 魅惑 - 让僵尸攻击其他僵尸
	if charm_duration > 0:
		zombie.apply_charm(charm_duration)
	
	# 击退
	if push_force > 0:
		var push_dir = (zombie.global_position - global_position).normalized()
		zombie.velocity += push_dir * push_force
	
	# 定身
	if stun_duration > 0:
		zombie.apply_stun(stun_duration)
	
	# 持续伤害
	if dot_damage > 0:
		zombie.apply_poison(dot_damage, 5.0)
	
	chain_targets.append(zombie)

func _chain_lightning(current_target: Zombie):
	chain_targets.append(current_target)
	
	var zombies = get_tree().get_nodes_in_group("zombies")
	var next_target = null
	var min_distance = 150.0
	
	for zombie in zombies:
		if zombie is Zombie and not chain_targets.has(zombie):
			var distance = current_target.global_position.distance_to(zombie.global_position)
			if distance < min_distance:
				min_distance = distance
				next_target = zombie
	
	if next_target:
		target = next_target
		chain_count -= 1
		damage *= 0.8
		_create_chain_effect(current_target.global_position, next_target.global_position)
	else:
		_create_magic_explosion()
		queue_free()

func _create_chain_effect(from: Vector2, to: Vector2):
	# 锯齿闪电效果
	var lightning = Line2D.new()
	var dist = from.distance_to(to)
	var segments = max(4, int(dist / 15.0))
	var dir = (to - from).normalized()
	var perp = Vector2(-dir.y, dir.x)
	lightning.add_point(from)
	for i in range(1, segments):
		var t = float(i) / segments
		var base = from.lerp(to, t)
		var offset = perp * randf_range(-8, 8) * (1.0 - abs(t - 0.5) * 2)
		lightning.add_point(base + offset)
	lightning.add_point(to)
	lightning.width = 3.0
	lightning.default_color = Color(0.5, 0.7, 1.0, 0.9)
	get_parent().add_child(lightning)

	# 外层光晕
	var glow = lightning.duplicate() as Line2D
	glow.width = 8.0
	glow.default_color = Color(0.4, 0.5, 1.0, 0.3)
	get_parent().add_child(glow)

	var tween = create_tween()
	tween.set_parallel(true)
	tween.tween_property(lightning, "modulate:a", 0.0, 0.25)
	tween.tween_property(glow, "modulate:a", 0.0, 0.25)
	tween.set_parallel(false)
	tween.tween_callback(lightning.queue_free)
	tween.tween_callback(glow.queue_free)

func _create_magic_explosion():
	# 粒子爆发
	var explosion = GPUParticles2D.new()
	explosion.global_position = global_position
	explosion.emitting = true
	explosion.amount = 35
	explosion.lifetime = 0.6
	explosion.one_shot = true

	var particle_material = ParticleProcessMaterial.new()
	particle_material.direction = Vector3(0, 0, 0)
	particle_material.initial_velocity_min = 50.0
	particle_material.initial_velocity_max = 160.0
	particle_material.gravity = Vector3.ZERO
	particle_material.scale_min = 0.8
	particle_material.scale_max = 2.0
	particle_material.color = Color(0.7, 0.3, 1.0, 0.9)
	explosion.process_material = particle_material
	get_parent().add_child(explosion)

	# 爆炸光圈
	var ring = Line2D.new()
	var pts = 20
	for i in range(pts + 1):
		var a = i * 2.0 * PI / pts
		ring.add_point(Vector2(cos(a), sin(a)) * 10.0)
	ring.width = 3.0
	ring.default_color = Color(0.8, 0.4, 1.0, 0.9)
	var ring_node = Node2D.new()
	ring_node.global_position = global_position
	ring_node.add_child(ring)
	get_parent().add_child(ring_node)

	var ring_tw = ring_node.create_tween()
	ring_tw.set_parallel(true)
	ring_tw.tween_property(ring_node, "scale", Vector2(4, 4), 0.3).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
	ring_tw.tween_property(ring_node, "modulate:a", 0.0, 0.3)
	ring_tw.set_parallel(false)
	ring_tw.tween_callback(ring_node.queue_free)

	var timer = Timer.new()
	timer.wait_time = 1.0
	timer.one_shot = true
	timer.timeout.connect(explosion.queue_free)
	explosion.add_child(timer)
	timer.start()
