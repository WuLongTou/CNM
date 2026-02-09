class_name PlayerCombat
extends Node
## 玩家战斗系统组件
## 负责：攻击处理、目标选择、投射物创建、武器效果、属性计算

var player: Player

# 攻击系统节点
var attack_area: Area2D
var attack_timer: Timer
var can_attack: bool = true

func setup(p: Player):
	player = p
	_setup_attack_area()
	_setup_attack_timer()

# ═══════════════════════════════════════════════
# 初始化
# ═══════════════════════════════════════════════

func _setup_attack_area():
	attack_area = Area2D.new()
	player.add_child(attack_area)
	var col = CollisionShape2D.new()
	var shape = CircleShape2D.new()
	shape.radius = GameConfig.BASE_ATTACK_RANGE
	col.shape = shape
	attack_area.add_child(col)

func _setup_attack_timer():
	attack_timer = Timer.new()
	attack_timer.wait_time = player.attack_cooldown
	attack_timer.one_shot = true
	attack_timer.timeout.connect(func(): can_attack = true)
	player.add_child(attack_timer)

# ═══════════════════════════════════════════════
# 攻击输入
# ═══════════════════════════════════════════════

func handle_manual_attack():
	if player.current_attack_type == Player.AttackType.MAGIC:
		return
	if Input.is_action_pressed("attack") and can_attack:
		perform_attack()
		return
	if Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT):
		var mouse_pos = player.get_viewport().get_mouse_position()
		if mouse_pos.y > GameConfig.TOP_UI_MARGIN and can_attack:
			perform_attack()

func handle_auto_attack(_delta):
	if player.current_attack_type != Player.AttackType.MAGIC:
		return
	if not can_attack:
		return
	var target = _find_target_zombie(GameConfig.MAGIC_ATTACK_RANGE)
	if target:
		_cast_magic_missile(target)

# ═══════════════════════════════════════════════
# 攻击执行
# ═══════════════════════════════════════════════

func perform_attack():
	match player.current_attack_type:
		Player.AttackType.MELEE:
			if SoundManager.instance:
				SoundManager.play_melee_attack()
			_perform_melee_attack()
		Player.AttackType.RANGED:
			if SoundManager.instance:
				SoundManager.play_ranged_attack()
			_perform_ranged_attack()
		Player.AttackType.AREA:
			if SoundManager.instance:
				SoundManager.play_ranged_attack()
			_perform_area_attack()
		Player.AttackType.MAGIC:
			if SoundManager.instance:
				SoundManager.play_magic_attack()
			_perform_magic_attack()

func _perform_melee_attack():
	if not _can_pay_health_cost():
		return
	_pay_health_cost()
	var bodies = attack_area.get_overlapping_bodies()
	var targets: Array[Zombie] = []
	for body in bodies:
		if body is Zombie:
			targets.append(body)
			if targets.size() >= player.melee_target_count:
				break
	for zombie in targets:
		attack_zombie(zombie)
		player.vfx.show_attack_effect(zombie.global_position, player.current_attack_type)

func _perform_ranged_attack():
	if not _can_pay_health_cost():
		return
	_pay_health_cost()
	_notify_ui_status()
	var target = _find_target_zombie(GameConfig.RANGED_ATTACK_RANGE)
	if not target:
		return
	if player.multi_shot_count > 1:
		var targets = _get_nearest_zombies(GameConfig.RANGED_ATTACK_RANGE, player.multi_shot_count)
		for z in targets:
			_fire_projectile(z.global_position)
		var extra = player.multi_shot_count - targets.size()
		for i in range(extra):
			_fire_projectile(target.global_position + Vector2(randf_range(-25, 25), randf_range(-25, 25)))
	else:
		_fire_projectile(target.global_position)
	player.vfx.show_attack_effect(target.global_position, player.current_attack_type)

func _perform_area_attack():
	if not _can_pay_health_cost():
		return
	_pay_health_cost()
	_notify_ui_status()
	_create_area_explosion()
	player.vfx.show_attack_effect(player.global_position, player.current_attack_type)
	can_attack = false
	attack_timer.start()

func _perform_magic_attack():
	if not _can_pay_health_cost():
		return
	_pay_health_cost()
	var target = _find_target_zombie(GameConfig.MAGIC_ATTACK_RANGE)
	if target:
		_cast_magic_missile(target)
		player.vfx.show_attack_effect(target.global_position, player.current_attack_type)

# ═══════════════════════════════════════════════
# 僵尸攻击核心逻辑
# ═══════════════════════════════════════════════

func attack_zombie(zombie: Zombie):
	if not can_attack:
		return
	can_attack = false
	attack_timer.start()

	var damage = player.current_damage
	var is_critical = randf() < (player.critical_chance / 100.0)
	if is_critical:
		damage *= player.crit_multiplier

	var hit_pos = zombie.global_position
	zombie.take_damage(int(damage))
	player.get_tree().call_group("ui_manager", "show_damage_number", hit_pos, int(damage), is_critical)

	# 打击反馈
	var shake = 8.0 if is_critical else 3.0
	player.get_tree().call_group("ui_manager", "shake_screen", shake)
	VFXManager.flash_sprite(player, player.sprite)

	if not is_instance_valid(zombie):
		_on_enemy_killed(hit_pos, damage)
		return

	if SoundManager.instance:
		SoundManager.play_zombie_hit()

	# 击退
	if player.knockback_force > 0:
		var push_dir = (zombie.global_position - player.global_position).normalized()
		zombie.velocity += push_dir * player.knockback_force

	# 冰冻（几率）
	if player.freeze_duration > 0 and randf() < (player.freeze_duration / 100.0):
		zombie.apply_freeze(GameConfig.FREEZE_DURATION_DEFAULT)

	# 中毒（几率）
	if player.poison_damage > 0 and randf() < (player.poison_damage / 100.0):
		var dps = 3.0 + player.poison_damage * 0.2
		zombie.apply_poison(dps, GameConfig.POISON_DURATION_DEFAULT)

	# 溅射
	if player.splash_radius > 0:
		for other in player.get_tree().get_nodes_in_group("zombies"):
			if other is Zombie and other != zombie:
				if hit_pos.distance_to(other.global_position) <= player.splash_radius:
					var splash_dmg = int(damage * GameConfig.SPLASH_DAMAGE_MULT)
					other.take_damage(splash_dmg)
					player.get_tree().call_group("ui_manager", "show_damage_number", other.global_position, splash_dmg, false)

	# 检查延迟击杀
	await player.get_tree().process_frame
	if not is_instance_valid(zombie):
		_on_enemy_killed(hit_pos, damage)

	# 生命偷取
	if player.life_steal_percent > 0:
		var heal_amt = int(damage * player.life_steal_percent / 100.0)
		player.heal(heal_amt)

func _on_enemy_killed(pos: Vector2, damage: float = 0):
	if SoundManager.instance:
		SoundManager.play_zombie_die()
		# 通知旋律系统：击杀加速连击
		SoundManager.instance._combo_count += 2
		SoundManager.instance._combo_timer = 2.0

	if player.kill_explosion_damage > 0:
		for z in player.get_tree().get_nodes_in_group("zombies"):
			if z is Zombie and pos.distance_to(z.global_position) <= GameConfig.KILL_EXPLOSION_RADIUS:
				z.take_damage(player.kill_explosion_damage)
				player.get_tree().call_group("ui_manager", "show_damage_number", z.global_position, player.kill_explosion_damage, false)

	if player.kill_shield_amount > 0:
		player.shield_amount = min(player.shield_amount + player.kill_shield_amount, GameConfig.MAX_SHIELD)

	if player.life_steal_percent > 0 and damage > 0:
		player.heal(int(damage * player.life_steal_percent / 100.0))

# ═══════════════════════════════════════════════
# 目标选择
# ═══════════════════════════════════════════════

func _find_target_zombie(max_range: float) -> Zombie:
	var zombies = player.get_tree().get_nodes_in_group("zombies")
	if zombies.is_empty():
		return null
	var result: Zombie = null
	match player.target_priority:
		0:  # 最近
			var best_dist = max_range
			for z in zombies:
				if z is Zombie:
					var d = player.global_position.distance_to(z.global_position)
					if d < best_dist:
						best_dist = d
						result = z
		1:  # 最远
			var best_dist = 0.0
			for z in zombies:
				if z is Zombie:
					var d = player.global_position.distance_to(z.global_position)
					if d > best_dist and d <= max_range:
						best_dist = d
						result = z
		2:  # 最密集
			var max_neighbors: int = -1
			var candidates: Array[Zombie] = []
			for z in zombies:
				if z is Zombie:
					var d: float = player.global_position.distance_to(z.global_position)
					if d > max_range:
						continue
					var count: int = 0
					for other in zombies:
						if other is Zombie and other != z:
							if z.global_position.distance_to(other.global_position) < 150:
								count += 1
					if count > max_neighbors:
						max_neighbors = count
						candidates.clear()
						candidates.append(z)
					elif count == max_neighbors:
						candidates.append(z)
			if not candidates.is_empty():
				result = candidates[randi() % candidates.size()]
	return result

func _get_nearest_zombies(max_range: float, count: int) -> Array[Zombie]:
	var zombies = player.get_tree().get_nodes_in_group("zombies")
	var candidates: Array[Zombie] = []
	for z in zombies:
		if z is Zombie and player.global_position.distance_to(z.global_position) <= max_range:
			candidates.append(z)
	candidates.sort_custom(func(a, b):
		return player.global_position.distance_to(a.global_position) < player.global_position.distance_to(b.global_position)
	)
	return candidates.slice(0, count)

# ═══════════════════════════════════════════════
# 投射物
# ═══════════════════════════════════════════════

func _fire_projectile(target_pos: Vector2):
	can_attack = false
	attack_timer.start()
	var base_dir = (target_pos - player.global_position).normalized()
	var direction = base_dir.rotated(deg_to_rad(randf_range(-2.0, 2.0)))

	var bullet = _create_bullet()
	bullet.global_position = player.global_position
	bullet.velocity = direction * GameConfig.BULLET_VELOCITY
	bullet.damage = player.current_damage
	bullet.trajectory_damage_ratio = player.trajectory_damage_ratio
	bullet.piercing_count = player.piercing_count
	bullet.is_returning = player.returning_projectiles
	bullet.bounces_left = player.bounce_count
	bullet.shooter = player
	player.get_parent().add_child(bullet)
	player.vfx.create_muzzle_flash(direction)

func _create_bullet() -> Projectile:
	var bullet = Projectile.new()
	var bw = 14
	var bh = 6
	var img = Image.create(bw, bh, false, Image.FORMAT_RGBA8)
	# 箭矢外观
	for by in range(bh):
		for bx in range(bw):
			# 三角形箭头
			if bx > bw - 5:
				var center_dist = abs(by - bh / 2.0)
				var max_dist = (bw - bx) * (bh / 2.0) / 5.0
				if center_dist <= max_dist:
					img.set_pixel(bx, by, Color(0.9, 0.85, 0.7))
			# 箭杆
			elif by >= (bh >> 1) - 1 and by <= (bh >> 1):
				img.set_pixel(bx, by, Color(0.6, 0.45, 0.3))
			# 尾羽
			if bx < 3:
				if by == 0 or by == bh - 1:
					img.set_pixel(bx, by, Color(0.7, 0.3, 0.2, 0.8))

	var tex = ImageTexture.create_from_image(img)
	var sprite = Sprite2D.new()
	sprite.texture = tex
	bullet.add_child(sprite)

	# 拖尾粒子
	var trail = GPUParticles2D.new()
	trail.emitting = true
	trail.amount = 8
	trail.lifetime = 0.3
	var tmat = ParticleProcessMaterial.new()
	tmat.direction = Vector3(-1, 0, 0)
	tmat.initial_velocity_min = 10.0
	tmat.initial_velocity_max = 30.0
	tmat.gravity = Vector3.ZERO
	tmat.scale_min = 0.5
	tmat.scale_max = 1.5
	tmat.color = Color(0.8, 0.6, 0.3, 0.6)
	trail.process_material = tmat
	bullet.add_child(trail)
	return bullet

func _cast_magic_missile(target: Zombie):
	can_attack = false
	attack_timer.start()

	var missile = _create_magic_missile()
	missile.global_position = player.global_position
	missile.target = target
	missile.damage = player.current_damage
	missile.charm_duration = player.charm_duration
	missile.push_force = player.push_force
	missile.stun_duration = player.stun_duration
	missile.dot_damage = player.dot_damage
	missile.chain_count = player.chain_count
	missile.shooter = player
	player.get_parent().add_child(missile)

	if SoundManager.instance:
		SoundManager.play_magic_attack()

func _create_magic_missile() -> MagicMissile:
	var missile = MagicMissile.new()
	var size = 12
	var img = Image.create(size, size, false, Image.FORMAT_RGBA8)
	for x in range(size):
		for y in range(size):
			var dist = Vector2(x - size / 2.0, y - size / 2.0).length()
			if dist < 2:
				img.set_pixel(x, y, Color(1.0, 1.0, 1.0, 0.95))
			elif dist < 4:
				var t = (dist - 2) / 2.0
				img.set_pixel(x, y, Color(0.8 - t * 0.3, 0.5 - t * 0.2, 1.0, 0.9 - t * 0.3))
			elif dist < 6:
				var t = (dist - 4) / 2.0
				img.set_pixel(x, y, Color(0.4, 0.2, 0.8, 0.4 * (1.0 - t)))
	var tex = ImageTexture.create_from_image(img)
	var sprite = Sprite2D.new()
	sprite.texture = tex
	missile.add_child(sprite)

	# 碰撞形状 — 没有这个 body_entered 永远不会触发
	var col = CollisionShape2D.new()
	var col_shape = CircleShape2D.new()
	col_shape.radius = 8.0
	col.shape = col_shape
	missile.add_child(col)

	# 螺旋拖尾
	var trail = GPUParticles2D.new()
	trail.emitting = true
	trail.amount = 12
	trail.lifetime = 0.4
	var tmat = ParticleProcessMaterial.new()
	tmat.direction = Vector3(0, 0, 0)
	tmat.initial_velocity_min = 5.0
	tmat.initial_velocity_max = 20.0
	tmat.gravity = Vector3.ZERO
	tmat.scale_min = 1.0
	tmat.scale_max = 2.5
	tmat.color = Color(0.6, 0.3, 1.0, 0.5)
	trail.process_material = tmat
	missile.add_child(trail)
	return missile

func _create_area_explosion():
	var pos = player.global_position
	var bodies = attack_area.get_overlapping_bodies()
	for body in bodies:
		if body is Zombie:
			var damage = player.current_damage * GameConfig.AREA_DAMAGE_MULT
			body.take_damage(int(damage))

	VFXManager.spawn_particle_burst(player.get_parent(), pos, Color(1.0, 0.5, 0.0),
		20, 0.5, 60.0, 150.0, Vector3(0, 80, 0), 1.5, 3.0)
	VFXManager.spawn_impact_ring(player.get_parent(), pos, 40.0, Color(1.0, 0.5, 0.0), 3.0)

# ═══════════════════════════════════════════════
# 武器效果
# ═══════════════════════════════════════════════

func apply_weapon_effect(weapon: WeaponCard):
	if weapon.category == WeaponCard.WeaponCategory.WEAPON:
		var kept: Array[WeaponCard] = []
		for e in player.active_effects:
			if e.category != WeaponCard.WeaponCategory.WEAPON:
				kept.append(e)
		player.active_effects = kept
	player.active_effects.append(weapon)
	recalculate_stats()

	# 视觉反馈
	player.vfx.show_upgrade_effect()
	player.effect_applied.emit(weapon.card_name)
	var color = Color.GOLD
	match weapon.rarity:
		2: color = Color(0.4, 0.7, 1.0)
		3: color = Color(0.8, 0.4, 1.0)
		4: color = Color(1.0, 0.8, 0.2)
	VFXManager.spawn_floating_label(player.get_parent(), player.global_position, "+" + weapon.card_name, color)
	_notify_ui_status()

func recalculate_stats():
	# 重置
	player.max_health = player.base_max_health
	player.current_speed = player.base_speed
	player.damage_bonus = 0.0
	player.speed_multiplier = 1.0
	player.multi_shot_count = 1
	player.piercing_count = 0
	player.area_damage = 0.0
	player.life_steal_percent = 0.0
	player.critical_chance = 0.0
	player.freeze_duration = 0.0
	player.poison_damage = 0.0
	player.bounce_count = 0
	player.health_cost_per_attack = 0
	player.attack_speed_multiplier = 1.0
	player.weapon_attack_speed_modifier = 1.0
	player.weapon_cooldown = 0.0
	player.weapon_range_modifier = 1.0
	player.splash_radius = 0.0
	player.melee_target_count = 1
	player.kill_explosion_damage = 0
	player.kill_shield_amount = 0
	player.knockback_force = 0.0
	player.trajectory_damage_ratio = 0.0
	player.returning_projectiles = false
	player.chain_count = 0
	player.charm_duration = 0.0
	player.push_force = 0.0
	player.stun_duration = 0.0
	player.dot_damage = 0.0

	# 确定武器类型
	var current_weapon: WeaponCard = null
	for e in player.active_effects:
		if e.category == WeaponCard.WeaponCategory.WEAPON:
			current_weapon = e
			break

	if current_weapon:
		match current_weapon.weapon_type:
			WeaponCard.WeaponType.MELEE:
				player.current_attack_type = Player.AttackType.MELEE
			WeaponCard.WeaponType.RANGED:
				player.current_attack_type = Player.AttackType.RANGED
			WeaponCard.WeaponType.MAGIC:
				player.current_attack_type = Player.AttackType.MAGIC
		player.target_priority = current_weapon.target_priority
	else:
		player.current_attack_type = Player.AttackType.MELEE

	# 应用所有活跃效果
	for effect in player.active_effects:
		_apply_effect_values(effect)

	player.current_damage = player.base_damage * max(GameConfig.MIN_DAMAGE_MULT, 1.0 + player.damage_bonus)
	var total_speed = max(0.1, player.weapon_attack_speed_modifier * player.attack_speed_multiplier)
	player.attack_cooldown = max(GameConfig.MIN_ATTACK_COOLDOWN, GameConfig.BASE_ATTACK_COOLDOWN / total_speed, player.weapon_cooldown)
	attack_timer.wait_time = player.attack_cooldown
	_update_attack_range(player.weapon_range_modifier)
	player.current_health = clamp(player.current_health, 0, player.max_health)

func _apply_effect_values(weapon: WeaponCard):
	if weapon.damage_modifier != 1.0:
		player.damage_bonus += (weapon.damage_modifier - 1.0)
	player.current_speed = player.base_speed + weapon.speed_modifier

	if weapon.health_modifier != 0:
		player.max_health += int(weapon.health_modifier)
		player.current_health += int(weapon.health_modifier)
		player.current_health = clamp(player.current_health, 0, player.max_health)
		player.health_changed.emit(player.current_health)

	if weapon.critical_bonus > 0:
		player.critical_chance += weapon.critical_bonus
	if weapon.life_steal_bonus > 0:
		player.life_steal_percent += weapon.life_steal_bonus
	if weapon.freeze_bonus > 0:
		player.freeze_duration = max(player.freeze_duration, weapon.freeze_bonus)
	if weapon.poison_bonus > 0:
		player.poison_damage = max(player.poison_damage, weapon.poison_bonus)
	if weapon.grants_aura:
		player.aura.activate()

	if weapon.category == WeaponCard.WeaponCategory.WEAPON:
		player.weapon_attack_speed_modifier = weapon.attack_speed_modifier
		player.weapon_cooldown = weapon.cooldown
		player.weapon_range_modifier = weapon.range_modifier
		if weapon.health_cost > 0:
			player.health_cost_per_attack = weapon.health_cost
	elif weapon.attack_speed_modifier != 1.0:
		player.attack_speed_multiplier *= weapon.attack_speed_modifier

	# 类型匹配的特效
	if player.current_attack_type == Player.AttackType.MELEE and weapon.weapon_type == WeaponCard.WeaponType.MELEE:
		for effect in weapon.melee_effects:
			_apply_melee_effect(effect, weapon.melee_effect_levels.get(effect, 1))

	if player.current_attack_type == Player.AttackType.RANGED and weapon.weapon_type == WeaponCard.WeaponType.RANGED:
		for effect in weapon.ranged_effects:
			_apply_ranged_effect(effect, weapon.ranged_effect_levels.get(effect, 1))

	if player.current_attack_type == Player.AttackType.MAGIC and weapon.weapon_type == WeaponCard.WeaponType.MAGIC:
		for effect in weapon.magic_effects:
			_apply_magic_effect(effect, weapon.magic_effect_levels.get(effect, 1))

func _apply_melee_effect(effect: int, level: int):
	match effect:
		WeaponCard.MeleeEffect.SPLASH:
			player.splash_radius = 30.0 + level * 10.0
		WeaponCard.MeleeEffect.MULTI_TARGET:
			player.melee_target_count = max(player.melee_target_count, 1 + level)
		WeaponCard.MeleeEffect.KILL_EXPLOSION:
			player.kill_explosion_damage += level * 50
		WeaponCard.MeleeEffect.KILL_SHIELD:
			player.kill_shield_amount += level * 20
		WeaponCard.MeleeEffect.KNOCKBACK:
			player.knockback_force += level * 50

func _apply_ranged_effect(effect: int, level: int):
	match effect:
		WeaponCard.RangedEffect.MULTI_SHOT:
			player.multi_shot_count = max(player.multi_shot_count, 1 + level)
		WeaponCard.RangedEffect.TRAJECTORY_DAMAGE:
			player.trajectory_damage_ratio = max(player.trajectory_damage_ratio, level * 0.3)
		WeaponCard.RangedEffect.RETURNING:
			player.returning_projectiles = true
			player.bounce_count = max(player.bounce_count, level)
		WeaponCard.RangedEffect.PIERCING:
			player.piercing_count += level

func _apply_magic_effect(effect: int, level: int):
	match effect:
		WeaponCard.MagicEffect.CHARM:
			player.charm_duration = max(player.charm_duration, level * 5.0)
		WeaponCard.MagicEffect.PUSH:
			player.push_force += level * 80
		WeaponCard.MagicEffect.STUN:
			player.stun_duration = max(player.stun_duration, level * 1.5)
		WeaponCard.MagicEffect.CHAIN:
			player.chain_count = max(player.chain_count, level)
		WeaponCard.MagicEffect.DOT:
			player.dot_damage += level * 5

# ═══════════════════════════════════════════════
# 工具
# ═══════════════════════════════════════════════

func _update_attack_range(range_mod: float):
	if not attack_area:
		return
	var col = attack_area.get_child(0) as CollisionShape2D
	if col and col.shape is CircleShape2D:
		(col.shape as CircleShape2D).radius = GameConfig.BASE_ATTACK_RANGE * max(GameConfig.MIN_RANGE_MULT, range_mod)

func _can_pay_health_cost() -> bool:
	return player.health_cost_per_attack <= 0 or player.current_health > player.health_cost_per_attack

func _pay_health_cost():
	if player.health_cost_per_attack > 0:
		player.take_damage(player.health_cost_per_attack)

func _notify_ui_status():
	player.get_tree().call_group("ui_manager", "update_player_status", player)
