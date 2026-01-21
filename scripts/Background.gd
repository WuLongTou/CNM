class_name Background
extends CanvasLayer

var background_rect: ColorRect
var grid_container: Control
var grid_lines: Array[Line2D] = []
var grid_size: int = 60
var grid_scroll_speed: Vector2 = Vector2(12, 6)
var grid_offset: Vector2 = Vector2.ZERO

func _ready():
	layer = -10  # 确保背景在最底层
	_create_background()
	_create_grid()
	set_process(true)

func _create_background():
	# 创建多层渐变背景
	background_rect = ColorRect.new()
	background_rect.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	
	# 创建复杂渐变背景 - 从深空紫到午夜蓝
	var gradient = Gradient.new()
	gradient.add_point(0.0, Color(0.05, 0.02, 0.12))      # 深空紫
	gradient.add_point(0.2, Color(0.08, 0.04, 0.18))      # 深紫
	gradient.add_point(0.5, Color(0.12, 0.06, 0.22))      # 中紫
	gradient.add_point(0.8, Color(0.08, 0.08, 0.25))      # 紫蓝
	gradient.add_point(1.0, Color(0.03, 0.03, 0.15))      # 午夜蓝
	
	var gradient_texture = GradientTexture2D.new()
	gradient_texture.gradient = gradient
	gradient_texture.fill_from = Vector2(0, 0)
	gradient_texture.fill_to = Vector2(0, 1)
	gradient_texture.width = 1280
	gradient_texture.height = 720
	
	background_rect.color = Color(0.08, 0.05, 0.18)
	add_child(background_rect)
	
	# 添加渐变覆盖层
	_create_gradient_overlay()
	
	# 添加多层次星空效果
	_create_stars()
	
	# 添加星云效果
	_create_nebula()
	
	# 添加远处光晕
	_create_ambient_glow()

func _create_gradient_overlay():
	# 创建渐变覆盖层使用TextureRect
	var gradient_overlay = TextureRect.new()
	gradient_overlay.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	
	# 创建渐变纹理
	var gradient = Gradient.new()
	gradient.add_point(0.0, Color(0.15, 0.1, 0.25, 0.8))  # 半透明深紫色
	gradient.add_point(0.5, Color(0.1, 0.05, 0.2, 0.9))   # 更深的紫色
	gradient.add_point(1.0, Color(0.05, 0.02, 0.1, 1.0))  # 完全不透明黑色
	
	var gradient_texture = GradientTexture2D.new()
	gradient_texture.gradient = gradient
	gradient_texture.fill_from = Vector2(0, 0)
	gradient_texture.fill_to = Vector2(0, 1)
	gradient_texture.width = 1280
	gradient_texture.height = 720
	
	gradient_overlay.texture = gradient_texture
	gradient_overlay.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
	
	background_rect.add_child(gradient_overlay)

func _create_stars():
	var screen_size = get_viewport().get_visible_rect().size
	
	# 创建多层次星空
	# 远景星星 - 小而多
	for i in range(150):
		var star = ColorRect.new()
		star.size = Vector2(1, 1)
		star.color = Color(0.8, 0.8, 1.0, randf_range(0.2, 0.6))
		star.position = Vector2(
			randf_range(0, screen_size.x),
			randf_range(0, screen_size.y)
		)
		star.set_meta("layer", "far")
		background_rect.add_child(star)
		
		# 缓慢闪烁
		var tween = create_tween()
		tween.set_loops()
		tween.tween_property(star, "modulate:a", 0.1, randf_range(3.0, 5.0))
		tween.tween_property(star, "modulate:a", 0.6, randf_range(3.0, 5.0))
	
	# 中景星星 - 中等大小
	for i in range(80):
		var star = ColorRect.new()
		star.size = Vector2(randf_range(1.5, 2.5), randf_range(1.5, 2.5))
		star.color = Color(1.0, 0.95, 0.8, randf_range(0.4, 0.9))
		star.position = Vector2(
			randf_range(0, screen_size.x),
			randf_range(0, screen_size.y)
		)
		star.set_meta("layer", "mid")
		background_rect.add_child(star)
		
		# 中等速度闪烁
		var tween = create_tween()
		tween.set_loops()
		tween.tween_property(star, "modulate:a", 0.3, randf_range(1.5, 3.0))
		tween.tween_property(star, "modulate:a", 0.9, randf_range(1.5, 3.0))
	
	# 近景星星 - 大而亮
	for i in range(30):
		var star = ColorRect.new()
		star.size = Vector2(randf_range(2, 4), randf_range(2, 4))
		star.color = Color(1.0, 1.0, 1.0, randf_range(0.7, 1.0))
		star.position = Vector2(
			randf_range(0, screen_size.x),
			randf_range(0, screen_size.y)
		)
		star.set_meta("layer", "near")
		background_rect.add_child(star)
		
		# 快速闪烁，更明显
		var tween = create_tween()
		tween.set_loops()
		tween.tween_property(star, "modulate:a", 0.4, randf_range(0.8, 2.0))
		tween.tween_property(star, "modulate:a", 1.0, randf_range(0.8, 2.0))
	
	# 添加彩色星星 (稀少)
	for i in range(15):
		var star = ColorRect.new()
		star.size = Vector2(3, 3)
		var colors = [Color.CYAN, Color.MAGENTA, Color.YELLOW, Color.LIGHT_GREEN]
		star.color = colors[randi() % colors.size()]
		star.color.a = randf_range(0.6, 0.9)
		star.position = Vector2(
			randf_range(0, screen_size.x),
			randf_range(0, screen_size.y)
		)
		star.set_meta("layer", "special")
		background_rect.add_child(star)
		
		# 彩色星星的特殊闪烁
		var tween = create_tween()
		tween.set_loops()
		tween.tween_property(star, "modulate:a", 0.2, 1.0).set_trans(Tween.TRANS_SINE)
		tween.tween_property(star, "modulate:a", 1.0, 1.0).set_trans(Tween.TRANS_SINE)
		tween.tween_property(star, "modulate:a", 0.2, 1.0).set_trans(Tween.TRANS_SINE)

func _create_nebula():
	# 创建星云效果
	var nebula_container = Node2D.new()
	nebula_container.name = "NebulaContainer"
	add_child(nebula_container)
	
	var screen_size = get_viewport().get_visible_rect().size
	
	# 创建多个星云层
	for i in range(3):
		var nebula = ColorRect.new()
		nebula.size = screen_size * randf_range(0.3, 0.6)
		nebula.position = Vector2(
			randf_range(-nebula.size.x * 0.5, screen_size.x - nebula.size.x * 0.5),
			randf_range(-nebula.size.y * 0.5, screen_size.y - nebula.size.y * 0.5)
		)
		
		# 星云颜色 - 紫色、蓝色、粉色系列
		var nebula_colors = [
			Color(0.4, 0.2, 0.6, 0.15),  # 紫色星云
			Color(0.2, 0.4, 0.7, 0.12),  # 蓝色星云
			Color(0.6, 0.3, 0.5, 0.1)   # 粉色星云
		]
		nebula.color = nebula_colors[i % nebula_colors.size()]
		
		# 添加模糊效果
		var material = CanvasItemMaterial.new()
		material.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
		nebula.material = material
		
		nebula_container.add_child(nebula)
		
		# 缓慢漂移动画
		var drift_tween = create_tween()
		drift_tween.set_loops()
		drift_tween.tween_property(nebula, "position:x", nebula.position.x + 50, 30.0)
		drift_tween.tween_property(nebula, "position:x", nebula.position.x - 50, 30.0)
		
		# 旋转效果
		var rotate_tween = create_tween()
		rotate_tween.set_loops()
		rotate_tween.tween_property(nebula, "rotation", PI * 2, 60.0)

func _create_ambient_glow():
	# 创建环境光晕
	var glow_container = Node2D.new()
	glow_container.name = "GlowContainer"
	add_child(glow_container)
	
	var screen_size = get_viewport().get_visible_rect().size
	
	# 在关键位置添加光晕
	var glow_positions = [
		Vector2(screen_size.x * 0.1, screen_size.y * 0.2),
		Vector2(screen_size.x * 0.8, screen_size.y * 0.3),
		Vector2(screen_size.x * 0.6, screen_size.y * 0.7),
		Vector2(screen_size.x * 0.2, screen_size.y * 0.8)
	]
	
	for i in range(glow_positions.size()):
		var glow = ColorRect.new()
		glow.size = Vector2(200, 200)
		glow.position = glow_positions[i] - Vector2(100, 100)
		
		# 渐变光晕
		var glow_gradient = Gradient.new()
		glow_gradient.add_point(0.0, Color(0.6, 0.4, 0.8, 0.1))
		glow_gradient.add_point(0.5, Color(0.4, 0.6, 1.0, 0.05))
		glow_gradient.add_point(1.0, Color.TRANSPARENT)
		
		var glow_texture = GradientTexture2D.new()
		glow_texture.gradient = glow_gradient
		glow_texture.fill_from = Vector2(0.5, 0.5)
		glow_texture.fill_to = Vector2(0, 0)
		glow_texture.width = 200
		glow_texture.height = 200
		
		var texture_rect = TextureRect.new()
		texture_rect.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		texture_rect.texture = glow_texture
		texture_rect.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_COVERED
		glow.add_child(texture_rect)
		
		# 添加混合模式
		var material = CanvasItemMaterial.new()
		material.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
		glow.material = material
		
		glow_container.add_child(glow)
		
		# 呼吸效果
		var pulse_tween = create_tween()
		pulse_tween.set_loops()
		var base_size = Vector2(200, 200)
		pulse_tween.tween_property(glow, "size", base_size * 1.2, 4.0).set_trans(Tween.TRANS_SINE)
		pulse_tween.tween_property(glow, "size", base_size * 0.8, 4.0).set_trans(Tween.TRANS_SINE)

func _create_grid():
	grid_container = Control.new()
	grid_container.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(grid_container)

	_rebuild_grid()

func _rebuild_grid():
	for line in grid_lines:
		if is_instance_valid(line):
			line.queue_free()
	grid_lines.clear()
	
	var screen_size = get_viewport().get_visible_rect().size
	
	# 创建垂直线
	for x in range(0, int(screen_size.x + grid_size), grid_size):
		var line = Line2D.new()
		line.add_point(Vector2(x, 0))
		line.add_point(Vector2(x, screen_size.y + grid_size))
		line.width = 1.0
		line.default_color = Color(0.3, 0.4, 0.6, 0.2)
		grid_container.add_child(line)
		grid_lines.append(line)
	
	# 创建水平线
	for y in range(0, int(screen_size.y + grid_size), grid_size):
		var line = Line2D.new()
		line.add_point(Vector2(0, y))
		line.add_point(Vector2(screen_size.x + grid_size, y))
		line.width = 1.0
		line.default_color = Color(0.3, 0.4, 0.6, 0.2)
		grid_container.add_child(line)
		grid_lines.append(line)

func _process(delta):
	if grid_container:
		grid_offset += grid_scroll_speed * delta
		grid_offset.x = fmod(grid_offset.x, grid_size)
		grid_offset.y = fmod(grid_offset.y, grid_size)
		grid_container.position = Vector2(-grid_offset.x, -grid_offset.y)

func update_background_size():
	# 当窗口大小改变时更新背景
	if background_rect:
		background_rect.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	if grid_container:
		grid_container.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		_rebuild_grid()
