class_name Player
extends CharacterBody2D
## 玩家角色 — 精简 Facade，核心逻辑委托给组件
##
## 组件：
##   combat: PlayerCombat — 攻击、目标选择、投射物、武器效果
##   aura:   PlayerAura   — 冰霜护盾光环系统
##   vfx:    PlayerVFX    — 视觉效果、精灵、动画

signal health_changed(new_health: int)
signal died
@warning_ignore("unused_signal")
signal effect_applied(effect_name: String)

# ═══════════════════════════════════════════════
# 攻击类型
# ═══════════════════════════════════════════════
enum AttackType { MELEE, RANGED, AREA, MAGIC }

# ═══════════════════════════════════════════════
# 基础属性
# ═══════════════════════════════════════════════
@export var max_health: int = GameConfig.PLAYER_MAX_HEALTH
@export var base_damage: float = GameConfig.PLAYER_BASE_DAMAGE
@export var base_speed: float = GameConfig.PLAYER_BASE_SPEED
var base_max_health: int = GameConfig.PLAYER_MAX_HEALTH

var current_health: int
var current_damage: float
var current_speed: float
var active_effects: Array[WeaponCard] = []

# ═══════════════════════════════════════════════
# 冲刺
# ═══════════════════════════════════════════════
var can_dash: bool = true
var is_dashing: bool = false
var dash_timer: Timer
var dash_cooldown_timer: Timer

# ═══════════════════════════════════════════════
# 目标 & 攻击属性（由 PlayerCombat 管理，但需要其他脚本读取）
# ═══════════════════════════════════════════════
var target_priority: int = 0
var current_attack_type: AttackType = AttackType.MELEE
var attack_cooldown: float = GameConfig.BASE_ATTACK_COOLDOWN
var attack_speed_multiplier: float = 1.0
var weapon_attack_speed_modifier: float = 1.0
var weapon_cooldown: float = 0.0
var weapon_range_modifier: float = 1.0

# ═══════════════════════════════════════════════
# 近战特效参数
# ═══════════════════════════════════════════════
var splash_radius: float = 0.0
var melee_target_count: int = 1
var kill_explosion_damage: int = 0
var kill_shield_amount: int = 0
var knockback_force: float = 0.0

# ═══════════════════════════════════════════════
# 远程特效参数
# ═══════════════════════════════════════════════
var trajectory_damage_ratio: float = 0.0
var returning_projectiles: bool = false
var chain_count: int = 0
var bounce_count: int = 0
var multi_shot_count: int = 1
var piercing_count: int = 0

# ═══════════════════════════════════════════════
# 魔法特效参数
# ═══════════════════════════════════════════════
var charm_duration: float = 0.0
var push_force: float = 0.0
var stun_duration: float = 0.0
var dot_damage: float = 0.0

# ═══════════════════════════════════════════════
# 武器效果累积值
# ═══════════════════════════════════════════════
var damage_bonus: float = 0.0
var speed_multiplier: float = 1.0
var area_damage: float = 0.0
var life_steal_percent: float = 0.0
var critical_chance: float = 0.0
var freeze_duration: float = 0.0
var poison_damage: float = 0.0
var crit_multiplier: float = GameConfig.CRIT_MULTIPLIER
var health_cost_per_attack: int = 0

# ═══════════════════════════════════════════════
# 护盾 & 无敌
# ═══════════════════════════════════════════════
var shield_amount: int = 0
var invincible_timer: float = 0.0
var invincible_duration: float = GameConfig.INVINCIBLE_DURATION

# ═══════════════════════════════════════════════
# 中毒状态
# ═══════════════════════════════════════════════
var poisoned_timer: float = 0.0
var poisoned_tick_timer: float = 0.0
var poisoned_dps: float = 0.0

# ═══════════════════════════════════════════════
# 视觉节点（由 PlayerVFX 创建，但需要 Player 持有引用）
# ═══════════════════════════════════════════════
var sprite: Sprite2D
var shadow: Sprite2D
var collision: CollisionShape2D
var health_display: Label
var attack_type_display: Label
var melee_range_indicator: Node2D
var show_melee_range: bool = true
var is_dead: bool = false

# ═══════════════════════════════════════════════
# 组件引用
# ═══════════════════════════════════════════════
var combat: PlayerCombat
var aura: PlayerAura
var vfx: PlayerVFX

# ═══════════════════════════════════════════════
# 初始化
# ═══════════════════════════════════════════════

func _ready():
	base_max_health = max_health
	current_health = max_health
	current_damage = base_damage
	current_speed = base_speed

	# 精灵（VFX 组件会设置 texture）
	sprite = Sprite2D.new()
	add_child(sprite)

	# 碰撞
	collision = CollisionShape2D.new()
	var shape = RectangleShape2D.new()
	shape.size = GameConfig.PLAYER_COLLISION_SIZE
	collision.shape = shape
	add_child(collision)

	# 初始化组件
	vfx = PlayerVFX.new()
	vfx.name = "VFX"
	add_child(vfx)
	vfx.setup(self)

	combat = PlayerCombat.new()
	combat.name = "Combat"
	add_child(combat)
	combat.setup(self)

	aura = PlayerAura.new()
	aura.name = "Aura"
	add_child(aura)
	aura.setup(self)

	# 冲刺系统
	_setup_dash()

# ═══════════════════════════════════════════════
# 主循环
# ═══════════════════════════════════════════════

func _physics_process(delta):
	if is_dashing:
		vfx.spawn_dash_trail(delta)
		move_and_slide()
		return

	_handle_movement()
	vfx.update_walk_animation(delta)
	_handle_dash_input()
	_handle_target_cycle()
	_handle_status_effects(delta)
	_handle_invincibility(delta)
	aura.handle(delta)
	combat.handle_auto_attack(delta)
	combat.handle_manual_attack()
	_handle_collision_repulsion()
	move_and_slide()
	_clamp_to_screen()

func _input(event):
	if event is InputEventKey and event.pressed:
		if event.keycode == KEY_TAB:
			target_priority = (target_priority + 1) % 3
			get_tree().call_group("ui_manager", "update_player_status", self)

# ═══════════════════════════════════════════════
# 移动
# ═══════════════════════════════════════════════

func _handle_movement():
	var input_vec = Vector2.ZERO
	if Input.is_action_pressed("move_right"): input_vec.x += 1
	if Input.is_action_pressed("move_left"):  input_vec.x -= 1
	if Input.is_action_pressed("move_down"):  input_vec.y += 1
	if Input.is_action_pressed("move_up"):    input_vec.y -= 1
	velocity = input_vec.normalized() * current_speed if input_vec != Vector2.ZERO else Vector2.ZERO

func _clamp_to_screen():
	var screen = get_viewport().get_visible_rect().size
	global_position = global_position.clamp(
		Vector2(GameConfig.BOUNDS_PADDING, GameConfig.TOP_UI_MARGIN),
		Vector2(screen.x - GameConfig.BOUNDS_PADDING, screen.y - GameConfig.BOUNDS_PADDING))

func _handle_collision_repulsion():
	for z in get_tree().get_nodes_in_group("zombies"):
		if z is Zombie:
			var dist = global_position.distance_to(z.global_position)
			if dist < GameConfig.REPULSION_DISTANCE and dist > 0:
				var dir = (z.global_position - global_position).normalized()
				var factor = (GameConfig.REPULSION_DISTANCE - dist) / GameConfig.REPULSION_DISTANCE
				velocity += dir * factor * GameConfig.REPULSION_FORCE * 0.016

# ═══════════════════════════════════════════════
# 冲刺
# ═══════════════════════════════════════════════

func _setup_dash():
	dash_timer = Timer.new()
	dash_timer.wait_time = GameConfig.DASH_DURATION
	dash_timer.one_shot = true
	dash_timer.timeout.connect(_on_dash_finished)
	add_child(dash_timer)

	dash_cooldown_timer = Timer.new()
	dash_cooldown_timer.wait_time = GameConfig.DASH_COOLDOWN
	dash_cooldown_timer.one_shot = true
	dash_cooldown_timer.timeout.connect(func(): can_dash = true)
	add_child(dash_cooldown_timer)

func _handle_dash_input():
	if Input.is_action_just_pressed("dash") and can_dash and velocity != Vector2.ZERO:
		is_dashing = true
		can_dash = false
		vfx.reset_dash_trail()
		velocity = velocity.normalized() * current_speed * GameConfig.DASH_SPEED_MULT
		collision.disabled = true
		dash_timer.start()
		if sprite:
			var tw = create_tween()
			tw.tween_property(sprite, "modulate", Color(0.5, 0.8, 1.0, 0.5), 0.05)

func _on_dash_finished():
	is_dashing = false
	velocity = Vector2.ZERO
	collision.disabled = false
	if sprite:
		sprite.modulate = Color.WHITE
	dash_cooldown_timer.start()

func _handle_target_cycle():
	if Input.is_action_just_pressed("cycle_target"):
		target_priority = (target_priority + 1) % 3
		get_tree().call_group("ui_manager", "update_player_status", self)

# ═══════════════════════════════════════════════
# 状态效果
# ═══════════════════════════════════════════════

func _handle_status_effects(delta):
	if poisoned_timer > 0:
		poisoned_timer -= delta
		poisoned_tick_timer -= delta
		if poisoned_tick_timer <= 0:
			poisoned_tick_timer = GameConfig.ZOMBIE_POISON_TICK
			var dmg = int(poisoned_dps)
			if dmg > 0:
				take_damage(dmg)
		if poisoned_timer <= 0 and sprite:
			sprite.modulate = Color.WHITE

func _handle_invincibility(delta):
	if invincible_timer > 0:
		invincible_timer -= delta
		if invincible_timer <= 0 and sprite:
			sprite.modulate = Color.WHITE

func apply_poison(damage_per_second: float, duration: float):
	poisoned_dps = max(poisoned_dps, damage_per_second)
	poisoned_timer = max(poisoned_timer, duration)
	poisoned_tick_timer = min(poisoned_tick_timer, 0.1)
	if sprite:
		sprite.modulate = Color(0.7, 0.3, 0.8)

# ═══════════════════════════════════════════════
# 生命系统
# ═══════════════════════════════════════════════

func take_damage(amount: int):
	if is_dead or invincible_timer > 0:
		return
	if shield_amount > 0:
		var blocked = min(shield_amount, amount)
		shield_amount -= blocked
		amount -= blocked
		get_tree().call_group("ui_manager", "show_damage_number", global_position, -blocked, false)
		VFXManager.spawn_floating_label(get_parent(), global_position, "护盾吸收", Color.CYAN)
	if amount > 0:
		current_health = max(0, current_health - amount)
		health_changed.emit(current_health)
		get_tree().call_group("ui_manager", "show_damage_number", global_position, amount, true)
		get_tree().call_group("ui_manager", "flash_damage")
		get_tree().call_group("ui_manager", "shake_screen", 5.0)
		invincible_timer = invincible_duration
		VFXManager.flash_hurt(self, sprite)
		if SoundManager.instance:
			SoundManager.play_player_hurt()
		vfx.update_status_displays()
		if float(current_health) / float(max_health) <= GameConfig.HEALTH_LOW_THRESHOLD:
			VFXManager.spawn_floating_label(get_parent(), global_position, "危险！", Color.RED)
		if current_health <= 0:
			die()

func heal(amount: int):
	current_health = min(max_health, current_health + amount)
	health_changed.emit(current_health)

func die():
	if is_dead:
		return
	is_dead = true
	set_physics_process(false)
	set_process(false)
	if collision: collision.disabled = true
	if dash_timer: dash_timer.stop()
	if dash_cooldown_timer: dash_cooldown_timer.stop()
	if health_display: health_display.visible = false
	if attack_type_display: attack_type_display.visible = false
	if melee_range_indicator: melee_range_indicator.visible = false
	if aura: aura.visible = false

	# 死亡视觉
	if sprite:
		var tw = create_tween()
		tw.tween_property(sprite, "modulate", Color(1.0, 0.2, 0.2, 1.0), 0.15)
		tw.tween_property(sprite, "modulate", Color(0.5, 0.1, 0.1, 0.5), 0.4)
		tw.parallel().tween_property(sprite, "scale", Vector2(0.3, 0.3), 0.4).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_IN)
		tw.parallel().tween_property(sprite, "rotation", randf_range(-1.0, 1.0), 0.4)
		tw.tween_property(sprite, "modulate:a", 0.0, 0.3)

	VFXManager.spawn_particle_burst(get_parent(), global_position, Color(0.8, 0.15, 0.1, 0.9),
		30, 0.8, 40.0, 120.0, Vector3(0, 150, 0), 1.5, 3.5)

	if SoundManager.instance:
		SoundManager.play_game_over()
	died.emit()

# ═══════════════════════════════════════════════
# Facade 方法 — 保持外部 API 兼容
# ═══════════════════════════════════════════════

func apply_weapon_effect(weapon: WeaponCard):
	combat.apply_weapon_effect(weapon)

func activate_aura():
	aura.activate()

func upgrade_aura(extra_damage: float = 3.0, extra_radius: float = 15.0):
	aura.upgrade(extra_damage, extra_radius)

func reset_for_new_round():
	var permanent: Array[WeaponCard] = []
	for e in active_effects:
		if e.duration == -1:
			permanent.append(e)
	active_effects = permanent
	combat.recalculate_stats()
