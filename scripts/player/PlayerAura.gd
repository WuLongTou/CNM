class_name PlayerAura
extends Node2D
## 冰霜护盾/光环系统组件
## 负责：光球创建、旋转、碰撞检测、伤害

var player: Player

var active: bool = false
var damage: float = GameConfig.AURA_BASE_DAMAGE
var radius: float = GameConfig.AURA_BASE_RADIUS
var tick_timer: float = 0.0
var tick_interval: float = GameConfig.AURA_TICK_INTERVAL
var orb_count: int = GameConfig.AURA_ORB_COUNT
var orb_radius: float = GameConfig.AURA_ORB_RADIUS
var hitboxes: Array[Area2D] = []
var _hit_cooldowns: Dictionary = {}  # zombie_instance_id -> remaining_cooldown

func setup(p: Player):
	player = p

# ═══════════════════════════════════════════════
# 每帧更新
# ═══════════════════════════════════════════════

func handle(delta: float):
	if not active:
		return

	# 旋转
	rotation += GameConfig.AURA_ROTATION_SPEED * delta

	# 冷却衰减
	var expired: Array = []
	for key in _hit_cooldowns:
		_hit_cooldowns[key] -= delta
		if _hit_cooldowns[key] <= 0:
			expired.append(key)
	for key in expired:
		_hit_cooldowns.erase(key)

	# 高频碰撞检测
	tick_timer -= delta
	if tick_timer > 0:
		return
	tick_timer = tick_interval

	var zombies = get_tree().get_nodes_in_group("zombies")
	var detect_range = orb_radius * GameConfig.AURA_DETECT_RADIUS_MULT + GameConfig.AURA_DETECT_EXTRA

	for zombie in zombies:
		if not is_instance_valid(zombie) or not zombie is Zombie:
			continue
		var zid = zombie.get_instance_id()
		if _hit_cooldowns.has(zid):
			continue

		for hitbox in hitboxes:
			if not is_instance_valid(hitbox):
				continue
			if hitbox.global_position.distance_to(zombie.global_position) < detect_range:
				zombie.take_damage(int(damage))
				if player:
					player.register_damage_dealt(zombie, damage)
				get_tree().call_group("ui_manager", "show_damage_number", zombie.global_position, int(damage), false)
				zombie.apply_freeze(1.0)
				VFXManager.spawn_hit_effect(player.get_parent(), zombie.global_position)
				_hit_cooldowns[zid] = GameConfig.AURA_HIT_COOLDOWN
				break

# ═══════════════════════════════════════════════
# 激活/升级
# ═══════════════════════════════════════════════

func activate():
	if active:
		return
	active = true
	hitboxes.clear()
	for i in range(orb_count):
		_create_orb(i)
	_update_orb_positions()

func upgrade(extra_damage: float = 3.0, extra_radius: float = 15.0):
	if not active:
		activate()
	damage += extra_damage
	radius += extra_radius
	_update_orb_positions()

# ═══════════════════════════════════════════════
# 光球创建
# ═══════════════════════════════════════════════

func _create_orb(index: int):
	var orb = Area2D.new()
	orb.monitoring = true
	orb.monitorable = false
	orb.collision_layer = 0
	orb.collision_mask = 1

	var col = CollisionShape2D.new()
	var shape = CircleShape2D.new()
	shape.radius = orb_radius * 2
	col.shape = shape
	orb.add_child(col)

	# 视觉
	var sprite = Sprite2D.new()
	var size = 16
	var img = Image.create(size, size, false, Image.FORMAT_RGBA8)
	var center = size / 2.0
	for x in range(size):
		for y in range(size):
			var dist = Vector2(x - center, y - center).length()
			if dist < 7:
				var alpha = 1.0 - dist / 7.0
				img.set_pixel(x, y, Color(0.3, 0.8, 1.0, alpha * 0.9))
	sprite.texture = ImageTexture.create_from_image(img)
	orb.add_child(sprite)

	orb.set_meta("aura_index", index)
	add_child(orb)
	hitboxes.append(orb)

func _update_orb_positions():
	for orb in hitboxes:
		if not is_instance_valid(orb):
			continue
		var idx = int(orb.get_meta("aura_index"))
		var angle = idx * TAU / max(1, orb_count)
		orb.position = Vector2(cos(angle), sin(angle)) * radius
