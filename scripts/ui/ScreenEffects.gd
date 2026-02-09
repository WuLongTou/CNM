class_name ScreenEffects
extends Control
## 屏幕效果组件
## 负责：屏幕震动、伤害闪红、低血量晕影、攻击闪光、伤害数字、连击显示

# 屏幕震动
var shake_intensity: float = 0.0
var _original_pos: Vector2 = Vector2.ZERO

# 伤害闪红
var damage_flash: ColorRect
# 低血晕影
var low_hp_vignette: ColorRect
# 攻击闪光
var attack_flash: ColorRect

# 连击
var combo_label: Label
var combo_count: int = 0
var combo_timer: float = 0.0

func _ready():
	_create_damage_flash()
	_create_low_hp_vignette()
	_create_attack_flash()

func process_effects(delta: float):
	# 屏幕震动衰减
	if shake_intensity > 0:
		shake_intensity = max(0, shake_intensity - GameConfig.SHAKE_DECAY * delta)
		var offset = Vector2(
			randf_range(-shake_intensity, shake_intensity),
			randf_range(-shake_intensity, shake_intensity))
		get_parent().position = _original_pos + offset
	elif get_parent().position != _original_pos:
		get_parent().position = _original_pos

	# 连击衰减
	if combo_timer > 0:
		combo_timer -= delta
		if combo_timer <= 0:
			combo_count = 0

# ═══════════════════════════════════════════════
# 屏幕震动
# ═══════════════════════════════════════════════

func shake_screen(intensity: float):
	shake_intensity = max(shake_intensity, intensity)

# ═══════════════════════════════════════════════
# 伤害闪红
# ═══════════════════════════════════════════════

func _create_damage_flash():
	damage_flash = ColorRect.new()
	damage_flash.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	damage_flash.color = Color(0, 0, 0, 0)
	damage_flash.mouse_filter = Control.MOUSE_FILTER_IGNORE
	damage_flash.z_index = 40
	add_child(damage_flash)

func flash_damage():
	if not damage_flash:
		return
	var tw = create_tween()
	damage_flash.color = Color(0.8, 0.1, 0.1, 0.25)
	tw.tween_property(damage_flash, "color:a", 0.0, 0.5)

# ═══════════════════════════════════════════════
# 低血量晕影
# ═══════════════════════════════════════════════

func _create_low_hp_vignette():
	low_hp_vignette = ColorRect.new()
	low_hp_vignette.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	low_hp_vignette.mouse_filter = Control.MOUSE_FILTER_IGNORE
	low_hp_vignette.z_index = 38
	low_hp_vignette.visible = false

	var shader_code = """
shader_type canvas_item;
uniform float intensity : hint_range(0.0, 1.0) = 0.0;
void fragment() {
	vec2 uv = UV;
	float dist = distance(uv, vec2(0.5));
	float vignette = smoothstep(0.25, 0.7, dist);
	COLOR = vec4(0.5, 0.0, 0.0, vignette * intensity * 0.6);
}
"""
	var shader = Shader.new()
	shader.code = shader_code
	var mat = ShaderMaterial.new()
	mat.shader = shader
	low_hp_vignette.material = mat
	add_child(low_hp_vignette)

func update_low_hp_vignette(health_percent: float):
	if not low_hp_vignette:
		return
	if health_percent < GameConfig.HEALTH_MED_THRESHOLD:
		low_hp_vignette.visible = true
		var intensity = (GameConfig.HEALTH_MED_THRESHOLD - health_percent) / GameConfig.HEALTH_MED_THRESHOLD
		(low_hp_vignette.material as ShaderMaterial).set_shader_parameter("intensity", intensity)
	else:
		low_hp_vignette.visible = false

# ═══════════════════════════════════════════════
# 攻击闪光
# ═══════════════════════════════════════════════

func _create_attack_flash():
	attack_flash = ColorRect.new()
	attack_flash.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	attack_flash.color = Color(0, 0, 0, 0)
	attack_flash.mouse_filter = Control.MOUSE_FILTER_IGNORE
	attack_flash.z_index = 39
	add_child(attack_flash)

func flash_attack_color(color: Color):
	if not attack_flash:
		return
	attack_flash.color = Color(color.r, color.g, color.b, 0.12)
	var tw = create_tween()
	tw.tween_property(attack_flash, "color:a", 0.0, 0.15)

# ═══════════════════════════════════════════════
# 伤害数字
# ═══════════════════════════════════════════════

func show_damage_number(pos: Vector2, amount: int, is_critical: bool = false):
	var font_size: int
	var color: Color
	var text: String

	if amount < 0:  # 治疗
		color = Color(0.3, 1.0, 0.4)
		font_size = 16
		text = "+" + str(abs(amount))
	elif is_critical:
		color = Color(1.0, 0.85, 0.15)
		font_size = 28
		text = str(amount) + "!"
	else:
		color = Color(1.0, 1.0, 1.0)
		font_size = 14
		text = str(amount)

	var label = Label.new()
	label.global_position = pos + Vector2(randf_range(-15, 15), randf_range(-25, -15))
	label.z_index = 20
	label.text = text
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", color)
	label.add_theme_constant_override("outline_size", 3)
	label.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.9))
	add_child(label)

	var tw = create_tween()
	if is_critical:
		label.scale = Vector2(0.5, 0.5)
		tw.tween_property(label, "scale", Vector2(1.5, 1.5), 0.08).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
		tw.tween_property(label, "scale", Vector2(1.0, 1.0), 0.1)
		tw.parallel().tween_property(label, "position:y", label.position.y - 55, 0.6).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
		tw.parallel().tween_property(label, "modulate:a", 0.0, 0.6).set_delay(0.25)
	elif amount < 0:
		tw.parallel().tween_property(label, "position:y", label.position.y - 35, 0.6).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
		tw.parallel().tween_property(label, "modulate:a", 0.0, 0.6).set_delay(0.2)
	else:
		tw.parallel().tween_property(label, "position:y", label.position.y - 40, 0.55).set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)
		tw.parallel().tween_property(label, "modulate:a", 0.0, 0.55).set_delay(0.12)
	tw.tween_callback(label.queue_free)

# ═══════════════════════════════════════════════
# 连击系统
# ═══════════════════════════════════════════════

func add_combo():
	combo_count += 1
	combo_timer = GameConfig.COMBO_DECAY_TIME
	_update_combo_display()

func _update_combo_display():
	if combo_count < 3:
		if combo_label and is_instance_valid(combo_label):
			combo_label.queue_free()
			combo_label = null
		return

	if not combo_label or not is_instance_valid(combo_label):
		combo_label = Label.new()
		combo_label.z_index = 25
		combo_label.add_theme_constant_override("outline_size", 3)
		combo_label.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.8))
		add_child(combo_label)

	combo_label.text = str(combo_count) + " COMBO!"
	combo_label.add_theme_font_size_override("font_size", 20 + min(combo_count, 10))

	var color: Color
	if combo_count >= 10:
		color = Color(1.0, 0.3, 0.1)
	elif combo_count >= 7:
		color = Color(1.0, 0.6, 0.1)
	elif combo_count >= 5:
		color = Color(1.0, 0.85, 0.15)
	else:
		color = Color(1.0, 1.0, 1.0)
	combo_label.add_theme_color_override("font_color", color)

	var screen = get_viewport().get_visible_rect().size
	combo_label.position = Vector2(screen.x - 200, GameConfig.HUD_HEIGHT + 6)

	combo_label.scale = Vector2(0.5, 0.5)
	var tw = create_tween()
	tw.tween_property(combo_label, "scale", Vector2(1.2, 1.2), 0.08).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	tw.tween_property(combo_label, "scale", Vector2(1.0, 1.0), 0.1)

# ═══════════════════════════════════════════════
# 击杀得分
# ═══════════════════════════════════════════════

func show_kill_effect(pos: Vector2, points: int):
	VFXManager.spawn_floating_label(self, pos, "+" + str(points), Color(1.0, 0.85, 0.2), 16)

# ═══════════════════════════════════════════════
# 屏幕边缘指示器
# ═══════════════════════════════════════════════

func show_offscreen_indicator(direction: Vector2, danger_level: float):
	var screen = get_viewport().get_visible_rect().size
	var indicator = ColorRect.new()
	indicator.mouse_filter = Control.MOUSE_FILTER_IGNORE
	indicator.z_index = 35
	indicator.color = Color(0.8, 0.1, 0.1, clamp(danger_level * 0.3, 0.05, 0.3))

	var thickness = clamp(danger_level * 8, 3, 12)
	if abs(direction.x) > abs(direction.y):
		indicator.size = Vector2(thickness, screen.y * 0.3)
		if direction.x > 0:
			indicator.position = Vector2(screen.x - thickness, screen.y * 0.35)
		else:
			indicator.position = Vector2(0, screen.y * 0.35)
	else:
		indicator.size = Vector2(screen.x * 0.3, thickness)
		if direction.y > 0:
			indicator.position = Vector2(screen.x * 0.35, screen.y - thickness)
		else:
			indicator.position = Vector2(screen.x * 0.35, GameConfig.HUD_HEIGHT)
	add_child(indicator)

	var tw = create_tween()
	tw.tween_property(indicator, "modulate:a", 0.0, 0.5)
	tw.tween_callback(indicator.queue_free)

# ═══════════════════════════════════════════════
# Boss 氛围
# ═══════════════════════════════════════════════

func trigger_boss_atmosphere():
	var overlay = ColorRect.new()
	overlay.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	overlay.color = Color(0.5, 0.05, 0.05, 0.2)
	overlay.mouse_filter = Control.MOUSE_FILTER_IGNORE
	overlay.z_index = 36
	add_child(overlay)

	var tw = create_tween()
	tw.tween_property(overlay, "color:a", 0.0, 2.0)
	tw.tween_callback(overlay.queue_free)
