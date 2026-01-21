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
	# 创建渐变背景
	background_rect = ColorRect.new()
	background_rect.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	
	# 创建渐变材质
	var gradient = Gradient.new()
	gradient.add_point(0.0, Color(0.15, 0.1, 0.25))  # 深紫色
	gradient.add_point(0.5, Color(0.1, 0.05, 0.2))   # 更深的紫色
	gradient.add_point(1.0, Color(0.05, 0.02, 0.1))  # 几乎黑色
	
	var gradient_texture = GradientTexture2D.new()
	gradient_texture.gradient = gradient
	gradient_texture.fill_from = Vector2(0, 0)
	gradient_texture.fill_to = Vector2(0, 1)
	gradient_texture.width = 1280
	gradient_texture.height = 720
	
	# 使用CanvasItemMaterial来应用渐变纹理
	var material = CanvasItemMaterial.new()
	material.blend_mode = CanvasItemMaterial.BLEND_MODE_MIX
	background_rect.material = material
	
	# 直接设置颜色作为基础，然后叠加纹理效果
	background_rect.color = Color(0.1, 0.05, 0.2)  # 基础深色
	
	add_child(background_rect)
	
	# 添加渐变覆盖层
	_create_gradient_overlay()
	
	# 添加星空效果
	_create_stars()

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
	
	for i in range(100):
		var star = ColorRect.new()
		star.size = Vector2(2, 2)
		star.color = Color(1, 1, 1, randf_range(0.3, 0.8))
		star.position = Vector2(
			randf_range(0, screen_size.x),
			randf_range(0, screen_size.y)
		)
		background_rect.add_child(star)
		
		# 添加闪烁动画
		var tween = create_tween()
		tween.set_loops()
		tween.tween_property(star, "modulate:a", 0.2, randf_range(1.0, 3.0))
		tween.tween_property(star, "modulate:a", 0.8, randf_range(1.0, 3.0))

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
