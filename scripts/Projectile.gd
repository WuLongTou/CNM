class_name Projectile
extends Area2D

var velocity: Vector2
var damage: float
var lifetime: float = 3.0
var piercing_count: int = 0

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
	global_position += velocity * delta
	
	# 根据速度旋转子弹
	if velocity.length() > 0:
		rotation = velocity.angle()

func _on_body_entered(body):
	if body is Zombie:
		# 对僵尸造成伤害
		body.take_damage(int(damage))
		
		# 创建击中特效
		_create_hit_effect()
		
		# 检查穿透
		if piercing_count > 0:
			piercing_count -= 1
		else:
			queue_free()

func _create_hit_effect():
	# 击中特效
	var hit_particles = GPUParticles2D.new()
	hit_particles.global_position = global_position
	hit_particles.emitting = true
	hit_particles.amount = 15
	hit_particles.lifetime = 0.5
	
	var particle_material = ParticleProcessMaterial.new()
	particle_material.direction = Vector3(0, 0, 0)
	particle_material.initial_velocity_min = 30.0
	particle_material.initial_velocity_max = 80.0
	particle_material.gravity = Vector3(0, 50, 0)
	particle_material.scale_min = 0.3
	particle_material.scale_max = 0.8
	hit_particles.process_material = particle_material
	
	get_parent().add_child(hit_particles)
	
	# 0.6秒后删除
	var timer = Timer.new()
	timer.wait_time = 0.6
	timer.one_shot = true
	timer.timeout.connect(hit_particles.queue_free)
	hit_particles.add_child(timer)
	timer.start()
