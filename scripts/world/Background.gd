class_name Background
extends CanvasLayer

var background_rect: ColorRect
var fog_layers: Array[TextureRect] = []

# 场景紧张度（外部可调，0.0~1.0）
var tension: float = 0.0

func _ready():
	layer = -10
	_create_base_background()
	_create_ground_texture()
	_create_fog_patches()
	_create_fire_lights()
	_create_ash_particles()
	_create_ember_particles()
	_create_ground_decals()
	set_process(false)  # 不需要每帧更新，粒子和Tween自行运行

# ─── 基础背景层 ───

func _create_base_background():
	background_rect = ColorRect.new()
	background_rect.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	background_rect.color = Color(0.065, 0.060, 0.055)
	add_child(background_rect)

	# 大气渐变：中心稍亮、四周暗沉
	var vignette = TextureRect.new()
	vignette.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var vig_gradient = Gradient.new()
	vig_gradient.colors = [Color(0.10, 0.09, 0.08, 0.0), Color(0.02, 0.02, 0.015, 0.65)]
	vig_gradient.offsets = [0.0, 1.0]
	var vig_tex = GradientTexture2D.new()
	vig_tex.gradient = vig_gradient
	vig_tex.fill = GradientTexture2D.FILL_RADIAL
	vig_tex.fill_from = Vector2(0.5, 0.5)
	vig_tex.fill_to = Vector2(0.0, 0.0)
	vig_tex.width = 512
	vig_tex.height = 512
	vignette.texture = vig_tex
	vignette.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	background_rect.add_child(vignette)

# ─── 程序化地面纹理 ───

func _create_ground_texture():
	# 生成一张整屏的水泥/沥青纹理，包含噪点、色差和裂缝
	var w = 640  # 半分辨率生成后拉伸，节省性能
	var h = 360
	var img = Image.create(w, h, false, Image.FORMAT_RGBA8)

	# 基底：带噪点的灰色水泥
	for y in range(h):
		for x in range(w):
			var noise_val = _simple_hash(x, y) # 0.0~1.0 伪随机
			var base_grey = 0.11 + noise_val * 0.04
			# 大块色调变化（模拟水泥板不均匀）
			@warning_ignore("integer_division")
			var block_x: int = x / 64
			@warning_ignore("integer_division")
			var block_y: int = y / 64
			var block_shift = _simple_hash(block_x * 7 + 3, block_y * 13 + 7) * 0.025 - 0.012
			base_grey += block_shift
			# 微绿/微褐的色彩偏移
			var r = base_grey + noise_val * 0.008
			var g = base_grey + noise_val * 0.005
			var b = base_grey - noise_val * 0.006
			img.set_pixel(x, y, Color(r, g, b, 1.0))

	# 砖缝/裂缝线
	var tile_w = 80
	var tile_h = 60
	for y in range(h):
		for x in range(w):
			# 水平缝隙
			var y_mod = y % tile_h
			if y_mod <= 1:
				var c = img.get_pixel(x, y)
				img.set_pixel(x, y, c.darkened(0.25 + _simple_hash(x, y * 3) * 0.15))
			# 垂直缝隙（交错砖块）
			@warning_ignore("integer_division")
			var row: int = y / tile_h
			@warning_ignore("integer_division")
			var ofs: int = (row % 2) * (tile_w / 2)
			var x_mod: int = (x + ofs) % tile_w
			if x_mod <= 1:
				var c = img.get_pixel(x, y)
				img.set_pixel(x, y, c.darkened(0.20 + _simple_hash(x * 5, y) * 0.15))

	# 随机裂缝
	for i in range(12):
		var cx = randi_range(20, w - 20)
		var cy = randi_range(20, h - 20)
		var angle = randf_range(0, TAU)
		var length = randi_range(30, 100)
		for step in range(length):
			var px = cx + int(cos(angle) * step + randf_range(-1, 1))
			var py = cy + int(sin(angle) * step + randf_range(-1, 1))
			if px >= 0 and px < w and py >= 0 and py < h:
				var c = img.get_pixel(px, py)
				img.set_pixel(px, py, c.darkened(0.35))
			# 裂缝轻微弯曲
			angle += randf_range(-0.15, 0.15)

	# 随机污渍/苔藓斑块
	for i in range(20):
		var sx = randi_range(10, w - 10)
		var sy = randi_range(10, h - 10)
		var radius = randi_range(8, 25)
		var stain_type = randi() % 3
		for dy in range(-radius, radius + 1):
			for dx in range(-radius, radius + 1):
				var d = sqrt(dx * dx + dy * dy)
				if d <= radius:
					var px = sx + dx
					var py = sy + dy
					if px >= 0 and px < w and py >= 0 and py < h:
						var falloff = 1.0 - d / radius
						var c = img.get_pixel(px, py)
						match stain_type:
							0:  # 深色油渍
								c = c.darkened(0.15 * falloff)
							1:  # 微绿苔藓
								c = c.lerp(Color(0.08, 0.12, 0.06), 0.20 * falloff)
							2:  # 暗红锈渍/血渍
								c = c.lerp(Color(0.15, 0.04, 0.03), 0.18 * falloff)
						img.set_pixel(px, py, c)

	var tex = ImageTexture.create_from_image(img)
	var ground_sprite = TextureRect.new()
	ground_sprite.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	ground_sprite.texture = tex
	ground_sprite.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	# 拉伸时使用最近邻采样保留像素质感
	ground_sprite.texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR
	background_rect.add_child(ground_sprite)

# 简易伪随机哈希（确定性，不用 FastNoiseLite）
func _simple_hash(x: int, y: int) -> float:
	var n = x * 374761393 + y * 668265263
	n = (n ^ (n >> 13)) * 1274126177
	n = n ^ (n >> 16)
	return float(absi(n) % 10000) / 10000.0

# ─── 径向渐变雾气 ───

func _create_fog_patches():
	var screen = get_viewport().get_visible_rect().size
	var fog_configs = [
		{"pos": Vector2(0.15, 0.25), "size": 450, "color": Color(0.12, 0.10, 0.08, 0.06)},
		{"pos": Vector2(0.70, 0.60), "size": 500, "color": Color(0.10, 0.08, 0.06, 0.05)},
		{"pos": Vector2(0.40, 0.80), "size": 380, "color": Color(0.08, 0.10, 0.07, 0.05)},
		{"pos": Vector2(0.85, 0.20), "size": 350, "color": Color(0.10, 0.09, 0.06, 0.04)},
		{"pos": Vector2(0.30, 0.45), "size": 420, "color": Color(0.11, 0.08, 0.06, 0.04)},
	]
	for cfg in fog_configs:
		var s: int = cfg["size"]
		var c: Color = cfg["color"]

		var grad = Gradient.new()
		grad.colors = [Color(c.r, c.g, c.b, c.a), Color(c.r, c.g, c.b, 0.0)]
		grad.offsets = [0.0, 1.0]
		var grad_tex = GradientTexture2D.new()
		grad_tex.gradient = grad
		grad_tex.fill = GradientTexture2D.FILL_RADIAL
		grad_tex.fill_from = Vector2(0.5, 0.5)
		grad_tex.fill_to = Vector2(0.0, 0.5)
		grad_tex.width = s
		grad_tex.height = s

		var fog = TextureRect.new()
		fog.texture = grad_tex
		fog.size = Vector2(s, s)
		fog.position = Vector2(screen.x * cfg["pos"].x, screen.y * cfg["pos"].y) - Vector2(s * 0.5, s * 0.5)
		fog.mouse_filter = Control.MOUSE_FILTER_IGNORE

		var mat = CanvasItemMaterial.new()
		mat.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
		fog.material = mat
		background_rect.add_child(fog)
		fog_layers.append(fog)

		# 缓慢飘动 + 透明度呼吸
		var drift = create_tween().set_loops()
		var dx = randf_range(20, 50)
		var dt = randf_range(12.0, 25.0)
		drift.tween_property(fog, "position:x", fog.position.x + dx, dt).set_trans(Tween.TRANS_SINE)
		drift.tween_property(fog, "position:x", fog.position.x - dx, dt).set_trans(Tween.TRANS_SINE)

		var alpha_tw = create_tween().set_loops()
		alpha_tw.tween_property(fog, "modulate:a", randf_range(0.5, 0.75), randf_range(6.0, 12.0)).set_trans(Tween.TRANS_SINE)
		alpha_tw.tween_property(fog, "modulate:a", randf_range(1.0, 1.3), randf_range(6.0, 12.0)).set_trans(Tween.TRANS_SINE)

# ─── 火光 ───

func _create_fire_lights():
	var screen = get_viewport().get_visible_rect().size
	var lights = [
		{"pos": Vector2(0.08, 0.30), "color": Color(0.95, 0.45, 0.10), "alpha": 0.10, "size": 180},
		{"pos": Vector2(0.88, 0.25), "color": Color(0.85, 0.35, 0.08), "alpha": 0.08, "size": 140},
		{"pos": Vector2(0.55, 0.80), "color": Color(0.80, 0.25, 0.05), "alpha": 0.07, "size": 120},
		{"pos": Vector2(0.22, 0.85), "color": Color(0.90, 0.50, 0.12), "alpha": 0.09, "size": 160},
	]
	for cfg in lights:
		var s: int = cfg["size"]
		var c: Color = cfg["color"]
		var a: float = cfg["alpha"]

		var grad = Gradient.new()
		grad.colors = [Color(c.r, c.g, c.b, a), Color(c.r * 0.6, c.g * 0.3, c.b * 0.1, a * 0.3), Color.TRANSPARENT]
		grad.offsets = [0.0, 0.45, 1.0]
		var grad_tex = GradientTexture2D.new()
		grad_tex.gradient = grad
		grad_tex.fill = GradientTexture2D.FILL_RADIAL
		grad_tex.fill_from = Vector2(0.5, 0.5)
		grad_tex.fill_to = Vector2(0.0, 0.5)
		grad_tex.width = s
		grad_tex.height = s

		var light = TextureRect.new()
		light.texture = grad_tex
		light.size = Vector2(s, s)
		light.position = Vector2(screen.x * cfg["pos"].x, screen.y * cfg["pos"].y) - Vector2(s * 0.5, s * 0.5)
		light.mouse_filter = Control.MOUSE_FILTER_IGNORE

		var mat = CanvasItemMaterial.new()
		mat.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
		light.material = mat
		background_rect.add_child(light)

		# 火焰闪烁
		var flicker = create_tween().set_loops()
		flicker.tween_property(light, "modulate:a", randf_range(0.5, 0.7), randf_range(0.3, 0.7))
		flicker.tween_property(light, "modulate:a", 1.0, randf_range(0.2, 0.4))
		flicker.tween_property(light, "modulate:a", randf_range(0.6, 0.85), randf_range(0.4, 0.9))

# ─── GPUParticles2D 灰烬 ───

func _create_ash_particles():
	var screen = get_viewport().get_visible_rect().size

	var ash = GPUParticles2D.new()
	ash.amount = 60
	ash.lifetime = 8.0
	ash.preprocess = 4.0  # 预热，开局就有粒子
	ash.position = Vector2(screen.x * 0.5, screen.y * 0.5)

	var mat = ParticleProcessMaterial.new()
	mat.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_BOX
	mat.emission_box_extents = Vector3(screen.x * 0.55, screen.y * 0.55, 0)
	mat.direction = Vector3(0.2, -1, 0)
	mat.spread = 30.0
	mat.initial_velocity_min = 3.0
	mat.initial_velocity_max = 12.0
	mat.gravity = Vector3(0, -2, 0)  # 微弱上升
	mat.scale_min = 0.6
	mat.scale_max = 1.8
	mat.color = Color(0.55, 0.50, 0.42, 0.35)

	# 颜色渐变：从灰白到透明
	var color_ramp = Gradient.new()
	color_ramp.colors = [Color(0.6, 0.55, 0.48, 0.0), Color(0.55, 0.50, 0.42, 0.35), Color(0.50, 0.45, 0.38, 0.0)]
	color_ramp.offsets = [0.0, 0.3, 1.0]
	var color_tex = GradientTexture1D.new()
	color_tex.gradient = color_ramp
	mat.color_ramp = color_tex

	ash.process_material = mat
	background_rect.add_child(ash)

# ─── GPUParticles2D 火星/余烬 ───

func _create_ember_particles():
	var screen = get_viewport().get_visible_rect().size

	var embers = GPUParticles2D.new()
	embers.amount = 18
	embers.lifetime = 4.0
	embers.preprocess = 2.0
	embers.position = Vector2(screen.x * 0.5, screen.y * 0.5)

	var mat = ParticleProcessMaterial.new()
	mat.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_BOX
	mat.emission_box_extents = Vector3(screen.x * 0.55, screen.y * 0.55, 0)
	mat.direction = Vector3(0.1, -1, 0)
	mat.spread = 25.0
	mat.initial_velocity_min = 8.0
	mat.initial_velocity_max = 22.0
	mat.gravity = Vector3(0, -5, 0)
	mat.scale_min = 0.8
	mat.scale_max = 2.5

	# 余烬颜色：橙红到暗红渐隐
	var color_ramp = Gradient.new()
	color_ramp.colors = [
		Color(1.0, 0.7, 0.2, 0.0),
		Color(1.0, 0.5, 0.15, 0.70),
		Color(0.8, 0.25, 0.05, 0.50),
		Color(0.4, 0.1, 0.02, 0.0)
	]
	color_ramp.offsets = [0.0, 0.15, 0.6, 1.0]
	var color_tex = GradientTexture1D.new()
	color_tex.gradient = color_ramp
	mat.color_ramp = color_tex

	embers.process_material = mat
	background_rect.add_child(embers)

# ─── 地面标记（少量血迹、弹坑） ───

func _create_ground_decals():
	var screen = get_viewport().get_visible_rect().size

	for i in range(8):
		var s = randi_range(20, 50)
		var decal = TextureRect.new()
		decal.size = Vector2(s, s)
		decal.position = Vector2(randf_range(30, screen.x - 30), randf_range(30, screen.y - 30))
		decal.mouse_filter = Control.MOUSE_FILTER_IGNORE
		decal.rotation = randf_range(0, TAU)

		# 生成圆形血迹/弹坑纹理
		var img = Image.create(s, s, false, Image.FORMAT_RGBA8)
		var center = s / 2.0
		var is_blood = randf() < 0.6
		for y in range(s):
			for x in range(s):
				var d = Vector2(x - center, y - center).length()
				if d <= center:
					var falloff = 1.0 - (d / center)
					falloff = falloff * falloff  # 二次衰减
					var noise = _simple_hash(x * 17 + i, y * 31 + i) * 0.3
					var alpha = falloff * (0.15 + noise * 0.1)
					var c: Color
					if is_blood:
						c = Color(0.30 + noise * 0.1, 0.04, 0.03, alpha)
					else:
						c = Color(0.06, 0.05, 0.04, alpha * 1.5)  # 弹坑：更暗
					img.set_pixel(x, y, c)
		var tex = ImageTexture.create_from_image(img)
		decal.texture = tex
		background_rect.add_child(decal)

# ─── 外部接口 ───

func update_background_size():
	if background_rect:
		background_rect.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)

func set_tension(value: float):
	tension = clampf(value, 0.0, 1.0)
	for fog in fog_layers:
		if is_instance_valid(fog):
			fog.modulate.a = lerpf(0.7, 1.5, tension)
