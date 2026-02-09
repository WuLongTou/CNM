class_name PlayerVFX
extends Node
## 玩家视觉效果组件
## 负责：精灵创建、动画、攻击特效、冲刺残影

var player: Player

# 动画状态
var walk_frames: Array[Texture2D] = []
var idle_frame: Texture2D
var current_frame: int = 0
var anim_timer: float = 0.0
var is_moving: bool = false
var facing_left: bool = false

func setup(p: Player):
	player = p
	_create_player_sprite()
	_create_shadow()
	_create_status_displays()
	_create_melee_range_indicator()

# ═══════════════════════════════════════════════
# 精灵创建
# ═══════════════════════════════════════════════

func _create_shadow():
	player.shadow = Sprite2D.new()
	player.shadow.texture = _make_shadow_texture(32, 12, Color(0, 0, 0, 0.5))
	player.shadow.position = Vector2(0, 18)
	player.add_child(player.shadow)

func _make_shadow_texture(w: int, h: int, color: Color) -> Texture2D:
	var img = Image.create(w, h, false, Image.FORMAT_RGBA8)
	var cx = w / 2.0
	var cy = h / 2.0
	var rx = w / 2.0
	var ry = h / 2.0
	for x in range(w):
		for y in range(h):
			var dx = (x - cx) / rx
			var dy = (y - cy) / ry
			if dx * dx + dy * dy <= 1.0:
				var dist = sqrt(dx * dx + dy * dy)
				img.set_pixel(x, y, Color(color.r, color.g, color.b, color.a * (1.0 - dist)))
	return ImageTexture.create_from_image(img)

func _create_player_sprite():
	var size = 40
	var frames_data: Array[Image] = []
	for frame_idx in range(4):
		var img = Image.create(size, size, false, Image.FORMAT_RGBA8)
		var cx = size / 2.0
		var cy = size / 2.0
		var bob = 0.0
		if frame_idx == 1: bob = -1.5
		elif frame_idx == 3: bob = 1.0

		# 身体
		for x in range(size):
			for y in range(size):
				var body_top = cy * 0.55 + bob
				var body_bot = cy * 1.5 + bob
				if y >= body_top and y <= body_bot:
					var body_w = size * 0.22 - abs(y - cy * 1.0 - bob) * 0.04
					if abs(x - cx) < body_w:
						img.set_pixel(x, y, Color(0.25, 0.30, 0.22))
		# 头
		for x in range(size):
			for y in range(size):
				var head_r = size * 0.18
				var head_cy = cy * 0.42 + bob
				var hd = Vector2(x - cx, y - head_cy).length()
				if hd < head_r:
					if hd > head_r * 0.65:
						img.set_pixel(x, y, Color(0.3, 0.35, 0.25))
					else:
						img.set_pixel(x, y, Color(0.75, 0.6, 0.5))
		# 眼睛
		var eye_y = int(cy * 0.38 + bob)
		for dx in [-1, 1]:
			var ex = int(cx + dx * size * 0.06)
			if ex >= 0 and ex < size and eye_y >= 0 and eye_y < size:
				img.set_pixel(ex, eye_y, Color(0.9, 0.9, 0.9))

		# 腿（行走帧差异）
		var leg_offset = 0.0
		if frame_idx == 1: leg_offset = 3.0
		elif frame_idx == 3: leg_offset = -3.0
		for ly in range(int(cy * 1.5 + bob), int(cy * 1.85 + bob)):
			for side in [-1, 1]:
				var lx = int(cx + side * size * 0.08 + side * leg_offset * (1 if side == 1 else -1))
				if lx >= 0 and lx < size and ly >= 0 and ly < size:
					img.set_pixel(lx, ly, Color(0.2, 0.22, 0.18))
					if lx + 1 < size:
						img.set_pixel(lx + 1, ly, Color(0.2, 0.22, 0.18))

		frames_data.append(img)

	# 生成纹理
	walk_frames.clear()
	for img in frames_data:
		walk_frames.append(ImageTexture.create_from_image(img))
	idle_frame = walk_frames[0] if walk_frames.size() > 0 else null
	if player.sprite and idle_frame:
		player.sprite.texture = idle_frame

# ═══════════════════════════════════════════════
# 行走动画
# ═══════════════════════════════════════════════

func update_walk_animation(delta: float):
	if walk_frames.is_empty() or not player.sprite:
		return
	is_moving = player.velocity.length() > 10
	if is_moving:
		anim_timer += delta
		if anim_timer >= GameConfig.WALK_ANIM_SPEED:
			anim_timer -= GameConfig.WALK_ANIM_SPEED
			current_frame = (current_frame + 1) % walk_frames.size()
			player.sprite.texture = walk_frames[current_frame]
	elif idle_frame:
		current_frame = 0
		anim_timer = 0.0
		player.sprite.texture = idle_frame

	# 朝向
	if player.velocity.x < -10:
		facing_left = true
		player.sprite.flip_h = true
	elif player.velocity.x > 10:
		facing_left = false
		player.sprite.flip_h = false

# ═══════════════════════════════════════════════
# 冲刺残影
# ═══════════════════════════════════════════════

var _trail_timer: float = 0.0

func spawn_dash_trail(delta: float):
	_trail_timer += delta
	if _trail_timer < GameConfig.DASH_TRAIL_INTERVAL:
		return
	_trail_timer -= GameConfig.DASH_TRAIL_INTERVAL
	VFXManager.spawn_ghost_trail(player.get_parent(), player.sprite, player.global_position)

func reset_dash_trail():
	_trail_timer = 0.0

# ═══════════════════════════════════════════════
# 攻击视觉效果
# ═══════════════════════════════════════════════

func show_attack_effect(target_pos: Vector2, attack_type: Player.AttackType):
	var flash_color = Color.WHITE
	match attack_type:
		Player.AttackType.MELEE:
			flash_color = Color(1.0, 0.6, 0.2)
			_show_melee_slash(target_pos)
		Player.AttackType.RANGED:
			flash_color = Color(1.0, 0.9, 0.4)
		Player.AttackType.AREA:
			flash_color = Color(1.0, 0.4, 0.1)
			_show_area_rings()
		Player.AttackType.MAGIC:
			flash_color = Color(0.6, 0.3, 1.0)
			_show_magic_circle(target_pos)
	player.get_tree().call_group("ui_manager", "flash_attack_color", flash_color)

func _show_melee_slash(target_pos: Vector2):
	var parent = player.get_parent()
	var base_dir = (target_pos - player.global_position).normalized()
	var start_angle = base_dir.angle() - PI / 6

	# 斩击弧线
	for arc in range(2):
		var line = Line2D.new()
		var arc_r = 55 + arc * 15
		for i in range(15):
			var a = start_angle + float(i) / 14.0 * (PI / 3)
			line.add_point(player.global_position + Vector2(cos(a), sin(a)) * arc_r)
		line.width = 6.0 - arc * 2.0
		line.default_color = Color(1.0, 0.8 - arc * 0.15, 0.2, 0.85 - arc * 0.2)
		line.joint_mode = Line2D.LINE_JOINT_ROUND
		line.begin_cap_mode = Line2D.LINE_CAP_ROUND
		line.end_cap_mode = Line2D.LINE_CAP_ROUND
		parent.add_child(line)
		var tw = line.create_tween()
		tw.tween_property(line, "modulate:a", 0.0, 0.3)
		tw.tween_callback(line.queue_free)

	# 冲击波
	VFXManager.spawn_impact_ring(parent, target_pos, 25.0, Color.ORANGE)
	player.get_tree().call_group("ui_manager", "shake_screen", 3.0)

func _show_area_rings():
	var parent = player.get_parent()
	for i in range(3):
		VFXManager.spawn_impact_ring(parent, player.global_position, 30.0 + i * 20, Color(1.0, 0.5, 0.0, 0.7 - i * 0.15), 3.0)

func _show_magic_circle(target_pos: Vector2):
	var parent = player.get_parent()
	var circle = _make_circle_ring(player.global_position, 50.0, Color.MAGENTA, 2.0)
	var target_ring = _make_circle_ring(target_pos, 35.0, Color(1.0, 0.5, 1.0, 0.8), 2.0)
	parent.add_child(circle)
	parent.add_child(target_ring)

	var tw = player.create_tween()
	tw.parallel().tween_property(circle, "rotation", TAU, 0.5)
	tw.parallel().tween_property(target_ring, "rotation", -TAU, 0.5)
	tw.parallel().tween_property(circle, "modulate:a", 0.0, 0.45)
	tw.parallel().tween_property(target_ring, "modulate:a", 0.0, 0.45)
	tw.tween_callback(circle.queue_free)
	tw.tween_callback(target_ring.queue_free)

func _make_circle_ring(pos: Vector2, radius: float, color: Color, width: float = 2.0) -> Node2D:
	var node = Node2D.new()
	node.global_position = pos
	var line = Line2D.new()
	var pts = 24
	for i in range(pts + 1):
		var a = i * TAU / pts
		line.add_point(Vector2(cos(a), sin(a)) * radius)
	line.width = width
	line.default_color = color
	node.add_child(line)
	return node

# ═══════════════════════════════════════════════
# 枪口闪光
# ═══════════════════════════════════════════════

func create_muzzle_flash(_direction: Vector2):
	var flash = Node2D.new()
	flash.global_position = player.global_position
	for i in range(6):
		var angle = i * TAU / 6
		var ray_len = randf_range(15, 35)
		var ray = Line2D.new()
		ray.add_point(Vector2.ZERO)
		ray.add_point(Vector2(cos(angle), sin(angle)) * ray_len)
		ray.width = randf_range(2, 5)
		ray.default_color = Color(1.0, 0.9, 0.5, randf_range(0.6, 1.0))
		flash.add_child(ray)
	player.get_parent().add_child(flash)
	var tw = flash.create_tween()
	tw.tween_property(flash, "modulate:a", 0.0, 0.15)
	tw.tween_callback(flash.queue_free)

# ═══════════════════════════════════════════════
# 升级效果
# ═══════════════════════════════════════════════

func show_upgrade_effect():
	var flash = ColorRect.new()
	flash.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	flash.color = Color(1.0, 0.9, 0.3, 0.3)
	flash.mouse_filter = Control.MOUSE_FILTER_IGNORE
	flash.z_index = 50
	player.get_tree().current_scene.add_child(flash)

	var tw = flash.create_tween()
	tw.tween_property(flash, "color:a", 0.0, 0.4)
	tw.tween_callback(flash.queue_free)

	if player.sprite:
		var glow = player.create_tween()
		glow.tween_property(player.sprite, "modulate", Color(1.5, 1.5, 1.0), 0.2)
		glow.tween_property(player.sprite, "modulate", Color.WHITE, 0.3)

# ═══════════════════════════════════════════════
# 状态显示（玩家头顶标签）
# ═══════════════════════════════════════════════

func _create_status_displays():
	player.health_display = Label.new()
	player.health_display.add_theme_font_size_override("font_size", 12)
	player.health_display.add_theme_color_override("font_color", Color.RED)
	player.health_display.position = Vector2(-20, -55)
	player.health_display.z_index = 10
	player.add_child(player.health_display)

	player.attack_type_display = Label.new()
	player.attack_type_display.add_theme_font_size_override("font_size", 12)
	player.attack_type_display.add_theme_color_override("font_color", Color.CYAN)
	player.attack_type_display.position = Vector2(-20, -35)
	player.attack_type_display.z_index = 10
	player.add_child(player.attack_type_display)

	update_status_displays()

func update_status_displays():
	if player.health_display:
		player.health_display.text = str(player.current_health) + "/" + str(player.max_health)
	if player.attack_type_display:
		var labels = {
			Player.AttackType.MELEE: "近战",
			Player.AttackType.RANGED: "远程",
			Player.AttackType.AREA: "范围",
			Player.AttackType.MAGIC: "魔法"
		}
		player.attack_type_display.text = labels.get(player.current_attack_type, "")
	_update_melee_range_indicator()

# ═══════════════════════════════════════════════
# 近战范围指示器
# ═══════════════════════════════════════════════

func _create_melee_range_indicator():
	player.melee_range_indicator = Node2D.new()
	player.melee_range_indicator.z_index = -1
	player.add_child(player.melee_range_indicator)

	var sprite = Sprite2D.new()
	sprite.name = "RangeSprite"
	var s = int(GameConfig.BASE_ATTACK_RANGE * player.weapon_range_modifier * 2)
	sprite.texture = _make_range_texture(s)
	sprite.modulate.a = 0.3
	player.melee_range_indicator.add_child(sprite)

func _update_melee_range_indicator():
	if not player.melee_range_indicator:
		return
	var should_show = player.show_melee_range and player.current_attack_type == Player.AttackType.MELEE
	player.melee_range_indicator.visible = should_show
	if should_show:
		var rs = player.melee_range_indicator.get_node_or_null("RangeSprite") as Sprite2D
		if rs and rs.texture:
			var expected = GameConfig.BASE_ATTACK_RANGE * player.weapon_range_modifier * 2
			if abs(rs.texture.get_size().x - expected) > 5:
				rs.texture = _make_range_texture(int(expected))

func _make_range_texture(size: int) -> Texture2D:
	var img = Image.create(size, size, false, Image.FORMAT_RGBA8)
	@warning_ignore("integer_division")
	var center = size / 2
	var radius = center - 2
	for x in range(size):
		for y in range(size):
			var dist = Vector2(x - center, y - center).length()
			if dist >= radius - 2 and dist <= radius:
				img.set_pixel(x, y, Color(0.3, 0.8, 1.0, 0.6))
			elif dist < radius - 2:
				img.set_pixel(x, y, Color(0.2, 0.6, 0.9, 0.1))
	return ImageTexture.create_from_image(img)
