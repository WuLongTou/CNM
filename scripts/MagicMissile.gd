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
		var direction = (target.global_position - global_position).normalized()
		var current_velocity = Vector2(cos(rotation), sin(rotation)) * speed
		var new_velocity = current_velocity.lerp(direction * speed, homing_strength * delta)
		
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
	var lightning = Line2D.new()
	lightning.add_point(from)
	lightning.add_point(to)
	lightning.width = 2.0
	lightning.default_color = Color.CYAN
	get_parent().add_child(lightning)
	
	var tween = create_tween()
	tween.tween_property(lightning, "modulate:a", 0.0, 0.2)
	tween.tween_callback(lightning.queue_free)

func _create_magic_explosion():
	var explosion = GPUParticles2D.new()
	explosion.global_position = global_position
	explosion.emitting = true
	explosion.amount = 30
	explosion.lifetime = 0.8
	
	var particle_material = ParticleProcessMaterial.new()
	particle_material.direction = Vector3(0, 0, 0)
	particle_material.initial_velocity_min = 50.0
	particle_material.initial_velocity_max = 150.0
	particle_material.gravity = Vector3(0, 0, 0)
	particle_material.scale_min = 0.5
	particle_material.scale_max = 1.2
	particle_material.color = Color.MAGENTA
	explosion.process_material = particle_material
	
	get_parent().add_child(explosion)
	
	var timer = Timer.new()
	timer.wait_time = 1.0
	timer.one_shot = true
	timer.timeout.connect(explosion.queue_free)
	explosion.add_child(timer)
	timer.start()
