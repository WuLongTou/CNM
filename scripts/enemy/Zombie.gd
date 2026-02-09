class_name Zombie
extends CharacterBody2D
## 僵尸基类 — 精简 Facade
## 核心逻辑保留（移动、攻击、状态效果、类型能力），视觉效果委托给 ZombieVFX

signal died(zombie: Zombie)

## 僵尸类型枚举
enum ZombieType {
	BASIC,     ## 基础僵尸 - 平衡型
	FAST,      ## 快速僵尸 - 低血高速
	TANK,      ## 坦克僵尸 - 高血高伤低速
	EXPLODER,  ## 爆炸僵尸 - 死后爆炸
	INVISIBLE, ## 隐身僵尸 - 周期性隐身
	SPITTER,   ## 喷毒僵尸 - 远程中毒
	HEALER     ## 治疗僵尸 - 治疗周围僵尸
}

@export var zombie_type: ZombieType = ZombieType.BASIC
@export var max_health: int = 30
@export var damage: int = 10
@export var speed: float = 50.0
@export var attack_range: float = GameConfig.ZOMBIE_DEFAULT_ATTACK_RANGE

# 类型属性表 { hp, dmg, spd }
const TYPE_STATS: Dictionary = {
	ZombieType.BASIC:     [25,  8,  45.0],
	ZombieType.FAST:      [15,  6,  90.0],
	ZombieType.TANK:      [100, 18, 22.0],
	ZombieType.EXPLODER:  [20,  12, 55.0],
	ZombieType.INVISIBLE: [30,  10, 55.0],
	ZombieType.SPITTER:   [35,  8,  35.0],
	ZombieType.HEALER:    [45,  5,  30.0],
}

var current_health: int
var player: Player

# 状态效果
var is_frozen: bool = false
var freeze_timer: float = 0.0
var is_stunned: bool = false
var stun_timer: float = 0.0
var slow_multiplier: float = 1.0
var poison_timer: float = 0.0
var poison_damage: float = 0.0

# 魅惑
var is_charmed: bool = false
var charm_timer: float = 0.0
var original_player: Player

# 类型专属状态
var invisible_timer: float = 0.0
var is_revealed: bool = false
var spitter_cooldown: float = 0.0
var spitter_cooldown_base: float = GameConfig.SPITTER_COOLDOWN

# 治疗僵尸
var healer_cooldown: float = 0.0
var healer_cooldown_base: float = GameConfig.HEALER_COOLDOWN
var healer_range: float = GameConfig.HEALER_RANGE
var healer_amount: int = GameConfig.HEALER_AMOUNT

# 精英
var is_elite: bool = false
var elite_multiplier: float = GameConfig.ZOMBIE_ELITE_STAT_MULT

# 节点引用（便于外部访问）
var sprite: Sprite2D:
	get: return vfx.sprite if vfx else null
var collision: CollisionShape2D

# 组件
var vfx: ZombieVFX

@onready var attack_timer: Timer = $AttackTimer

# ═══════════════════════════════════════════════
# 初始化
# ═══════════════════════════════════════════════

func _ready():
	# 精英加成
	if is_elite:
		max_health = int(max_health * elite_multiplier)
		damage = int(damage * elite_multiplier)
		speed = speed * GameConfig.ZOMBIE_ELITE_SPEED_MULT

	# 根据类型覆写属性
	_apply_type_stats()

	current_health = max_health
	add_to_group("zombies")

	# 碰撞
	collision = CollisionShape2D.new()
	var shape := RectangleShape2D.new()
	shape.size = GameConfig.ZOMBIE_COLLISION_SIZE
	collision.shape = shape
	add_child(collision)

	# 初始化 VFX 组件
	vfx = ZombieVFX.new()
	vfx.name = "VFX"
	add_child(vfx)
	vfx.setup(self)
	vfx.create_visuals()

	# 攻击计时器
	if attack_timer:
		attack_timer.wait_time = GameConfig.ZOMBIE_ATTACK_COOLDOWN
		attack_timer.timeout.connect(_on_attack_timer_timeout)

func _apply_type_stats():
	if TYPE_STATS.has(zombie_type):
		var s: Array = TYPE_STATS[zombie_type]
		max_health = s[0]
		damage = s[1]
		speed = s[2]

# ═══════════════════════════════════════════════
# 回合缩放
# ═══════════════════════════════════════════════

func apply_round_scaling(round_number: int, multiplier: float):
	max_health = int(float(max_health) * multiplier)
	current_health = max_health
	damage = int(float(damage) * multiplier)
	var speed_mult := 1.0 + (multiplier - 1.0) * GameConfig.ROUND_SPEED_SCALE_FACTOR
	speed = speed * speed_mult

	if zombie_type == ZombieType.SPITTER:
		spitter_cooldown_base = max(GameConfig.SPITTER_MIN_COOLDOWN, GameConfig.SPITTER_COOLDOWN - round_number * GameConfig.SPITTER_COOLDOWN_DECAY)

	print("僵尸升级 - 回合: %d, 血量: %d, 伤害: %d, 速度: %.1f, 倍数: x%.2f" % [round_number, max_health, damage, speed, multiplier])

	if vfx:
		vfx.update_status_labels()

# ═══════════════════════════════════════════════
# 物理循环
# ═══════════════════════════════════════════════

func _physics_process(delta):
	if is_stunned:
		stun_timer -= delta
		if stun_timer <= 0:
			is_stunned = false
			if vfx:
				vfx.clear_effect()
		return

	if is_frozen:
		freeze_timer -= delta
		if freeze_timer <= 0:
			is_frozen = false
			slow_multiplier = 1.0
			if vfx:
				vfx.clear_effect()

	if is_charmed:
		charm_timer -= delta
		if charm_timer <= 0:
			is_charmed = false
			player = original_player

	if poison_timer > 0:
		poison_timer -= delta
		if poison_timer <= 0:
			take_damage(int(poison_damage))
			poison_timer = GameConfig.ZOMBIE_POISON_TICK

	# 类型专属逻辑
	if zombie_type == ZombieType.INVISIBLE:
		_handle_invisible(delta)
	if zombie_type == ZombieType.SPITTER:
		_handle_spitter(delta)
	if zombie_type == ZombieType.HEALER:
		_handle_healer(delta)

	if player:
		_move_towards_player()
		_check_attack_range()

# ═══════════════════════════════════════════════
# 移动
# ═══════════════════════════════════════════════

func _move_towards_player():
	if is_charmed:
		var nearest := _find_nearest_zombie(GameConfig.CHARM_SEARCH_RANGE)
		if nearest:
			velocity = (nearest.global_position - global_position).normalized() * speed
		else:
			velocity = Vector2.ZERO
	else:
		velocity = (player.global_position - global_position).normalized() * speed * slow_multiplier
	move_and_slide()

# ═══════════════════════════════════════════════
# 类型专属行为
# ═══════════════════════════════════════════════

func _handle_invisible(delta: float):
	invisible_timer -= delta
	if invisible_timer <= 0:
		invisible_timer = randf_range(GameConfig.ZOMBIE_INVISIBLE_REVEAL_MIN, GameConfig.ZOMBIE_INVISIBLE_REVEAL_MAX)
		is_revealed = !is_revealed
		if vfx:
			vfx.show_invisible(is_revealed)

func _handle_spitter(delta: float):
	spitter_cooldown -= delta
	if spitter_cooldown <= 0:
		spitter_cooldown = spitter_cooldown_base
		if player and global_position.distance_to(player.global_position) <= GameConfig.SPITTER_RANGE:
			_spit_poison(player)

func _spit_poison(target: Node):
	var proj := Sprite2D.new()
	var img := Image.create(10, 10, false, Image.FORMAT_RGBA8)
	img.fill(Color.GREEN)
	var tex := ImageTexture.new()
	tex.set_image(img)
	proj.texture = tex
	proj.global_position = global_position
	get_parent().add_child(proj)

	var tw := create_tween()
	tw.tween_property(proj, "global_position", target.global_position, 0.5)
	tw.tween_callback(_complete_poison_spit.bind(target, proj))

func _handle_healer(delta: float):
	healer_cooldown -= delta
	if healer_cooldown <= 0:
		healer_cooldown = healer_cooldown_base
		_heal_nearby_zombies()

func _heal_nearby_zombies():
	var zombies := get_tree().get_nodes_in_group("zombies")
	var healed_any := false

	for z in zombies:
		if z is Zombie and z != self:
			if global_position.distance_to(z.global_position) <= healer_range and z.current_health < z.max_health:
				z.current_health = min(z.max_health, z.current_health + healer_amount)
				if z.vfx:
					z.vfx.update_status_labels()
				healed_any = true
				if vfx:
					vfx.show_heal_effect(z.global_position, healer_amount)

	if healed_any and vfx:
		vfx.show_heal_pulse(healer_range)

# ═══════════════════════════════════════════════
# 攻击
# ═══════════════════════════════════════════════

func _check_attack_range():
	if is_charmed:
		var nearest := _find_nearest_zombie(attack_range)
		if nearest and attack_timer.is_stopped():
			attack_timer.start()
	elif player and global_position.distance_to(player.global_position) <= attack_range:
		if attack_timer.is_stopped():
			attack_timer.start()

func _on_attack_timer_timeout():
	if is_charmed:
		var nearest := _find_nearest_zombie(attack_range)
		if nearest:
			attack_zombie(nearest)
	elif player and global_position.distance_to(player.global_position) <= attack_range:
		attack_player()

func attack_zombie(target: Zombie):
	if target:
		target.take_damage(damage)

func attack_player():
	if player:
		player.take_damage(damage)

# ═══════════════════════════════════════════════
# 受伤 & 死亡
# ═══════════════════════════════════════════════

func take_damage(amount: int):
	current_health -= amount
	current_health = max(0, current_health)

	if vfx:
		vfx.update_status_labels()
		vfx.flash_hit()
		vfx.show_damage_popup(amount)

	if SoundManager.instance:
		SoundManager.play_zombie_hit()

	if current_health <= 0:
		die()

func die():
	if zombie_type == ZombieType.EXPLODER:
		_explode()
		if SoundManager.instance:
			SoundManager.play_explosion()

	if vfx:
		vfx.spawn_death_effects()

	died.emit(self)
	queue_free()

func _explode():
	if player and global_position.distance_to(player.global_position) <= GameConfig.EXPLODER_RANGE:
		player.take_damage(damage * int(GameConfig.EXPLODER_DAMAGE_MULT))

# ═══════════════════════════════════════════════
# 状态效果 API
# ═══════════════════════════════════════════════

func apply_freeze(duration: float):
	is_frozen = true
	freeze_timer = duration
	slow_multiplier = GameConfig.FREEZE_SLOW_MULT
	if vfx:
		vfx.show_frozen()

func apply_stun(duration: float):
	is_stunned = true
	stun_timer = duration
	if vfx:
		vfx.show_stunned()

func apply_poison(damage_per_second: float, duration: float):
	poison_damage = damage_per_second
	poison_timer = duration
	if vfx:
		vfx.show_poisoned()

func apply_charm(duration: float):
	var resist := 0.0
	match zombie_type:
		ZombieType.TANK:
			resist = GameConfig.CHARM_RESIST_TANK
		ZombieType.FAST:
			resist = GameConfig.CHARM_RESIST_FAST

	var actual: float = maxf(GameConfig.CHARM_MIN_DURATION, duration * (1.0 - resist))
	is_charmed = true
	charm_timer = actual
	original_player = player

	if SoundManager.instance:
		SoundManager.play_charm()
	if vfx:
		vfx.show_charmed()

func set_player_target(target_player: Player):
	player = target_player

func set_elite(enabled: bool):
	is_elite = enabled

# ═══════════════════════════════════════════════
# 辅助
# ═══════════════════════════════════════════════

func _find_nearest_zombie(max_range: float) -> Zombie:
	var zombies := get_tree().get_nodes_in_group("zombies")
	var nearest: Zombie = null
	var nearest_dist := max_range
	for z in zombies:
		if z is Zombie and z != self:
			var dist := global_position.distance_to(z.global_position)
			if dist < nearest_dist:
				nearest_dist = dist
				nearest = z
	return nearest

func _complete_poison_spit(target: Node, projectile: Node):
	if is_instance_valid(target):
		target.apply_poison(GameConfig.SPITTER_POISON_DPS, GameConfig.SPITTER_POISON_DURATION)
	if is_instance_valid(projectile):
		projectile.queue_free()
