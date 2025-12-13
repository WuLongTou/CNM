class_name MagicMissile
extends Area2D

var target: Zombie
var damage: float
var speed: float = 300.0
var homing_strength: float = 5.0
var lifetime: float = 5.0

func _ready():
	# 连接碰撞信号
	body_entered.connect(_on_body_entered)
	
	# 设置生命周期
	var timer = Timer.new()
	timer.wait_time = lifetime
	timer.one_shot = true
	timer.timeout.connect(queue_free)
	add_child(timer)
	timer.start()

func _physics_process(delta):
	if target and is_instance_valid(target):
		# 追踪目标
		var direction = (target.global_position - global_position).normalized()
		var current_velocity = Vector2(cos(rotation), sin(rotation)) * speed
		var new_velocity = current_velocity.lerp(direction * speed, homing_strength * delta)
		
		global_position += new_velocity * delta
		rotation = new_velocity.angle()
	else:
		# 目标丢失，直线飞行
		global_position += Vector2(cos(rotation), sin(rotation)) * speed * delta

func _on_body_entered(body):
	if body is Zombie:
		# 对僵尸造成伤害
		body.take_damage(int(damage))
		
		# 创建魔法爆炸特效
		_create_magic_explosion()
		
		queue_free()

func _create_magic_explosion():
	# 魔法爆炸特效
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
	
	# 1秒后删除
	var timer = Timer.new()
	timer.wait_time = 1.0
	timer.one_shot = true
	timer.timeout.connect(explosion.queue_free)
	explosion.add_child(timer)
	timer.start()

