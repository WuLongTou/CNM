class_name GameOverUI
extends Control
## 游戏结束画面组件
## 负责：死亡画面渐入、战绩统计、重新开始/退出

var game_manager: GameManager

func setup(gm: GameManager):
	game_manager = gm

func show_game_over(hud_panel: Panel = null):
	if hud_panel:
		hud_panel.visible = false

	# 暗色背景（渐入）
	var bg = ColorRect.new()
	bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	bg.color = Color(0, 0, 0, 0)
	bg.z_index = 50
	add_child(bg)

	var red_tint = ColorRect.new()
	red_tint.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	red_tint.color = Color(0.3, 0.02, 0.02, 0)
	red_tint.z_index = 51
	add_child(red_tint)

	var bg_tw = create_tween()
	bg_tw.tween_property(bg, "color:a", 0.75, 0.8).set_trans(Tween.TRANS_CUBIC)
	bg_tw.parallel().tween_property(red_tint, "color:a", 0.25, 1.0).set_trans(Tween.TRANS_CUBIC)

	# 内容面板
	var content = Panel.new()
	content.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	content.custom_minimum_size = Vector2(380, 360)
	content.position -= content.custom_minimum_size / 2
	content.z_index = 52
	content.modulate.a = 0

	var style = StyleBoxFlat.new()
	style.bg_color = Color(0.06, 0.04, 0.04, 0.92)
	style.corner_radius_top_left = 6
	style.corner_radius_top_right = 6
	style.corner_radius_bottom_left = 6
	style.corner_radius_bottom_right = 6
	style.border_width_top = 2
	style.border_width_bottom = 2
	style.border_width_left = 2
	style.border_width_right = 2
	style.border_color = Color(0.5, 0.1, 0.08, 0.5)
	style.shadow_size = 12
	style.shadow_color = Color(0.2, 0, 0, 0.6)
	content.add_theme_stylebox_override("panel", style)
	add_child(content)

	var margin = MarginContainer.new()
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	margin.add_theme_constant_override("margin_left", 30)
	margin.add_theme_constant_override("margin_right", 30)
	margin.add_theme_constant_override("margin_top", 25)
	margin.add_theme_constant_override("margin_bottom", 25)
	content.add_child(margin)

	var vbox = VBoxContainer.new()
	vbox.add_theme_constant_override("separation", 14)
	vbox.alignment = BoxContainer.ALIGNMENT_CENTER
	margin.add_child(vbox)

	# 标题
	var title = Label.new()
	title.text = "YOU DIED"
	title.add_theme_font_size_override("font_size", 38)
	title.add_theme_color_override("font_color", Color(0.8, 0.12, 0.08))
	title.add_theme_constant_override("outline_size", 3)
	title.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.8))
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	vbox.add_child(title)

	# 分隔线
	var sep = ColorRect.new()
	sep.custom_minimum_size = Vector2(0, 1)
	sep.color = Color(0.5, 0.15, 0.1, 0.4)
	vbox.add_child(sep)

	# 战绩
	var stats_box = VBoxContainer.new()
	stats_box.add_theme_constant_override("separation", 8)
	vbox.add_child(stats_box)

	var round_count = game_manager.get_current_round() if game_manager else 0
	var total_score = game_manager.score if game_manager else 0
	var killed = game_manager.zombies_spawned if game_manager else 0

	_add_stat_row(stats_box, "存活波数", str(round_count), Color(0.9, 0.78, 0.35))
	_add_stat_row(stats_box, "消灭数量", str(killed), Color(0.75, 0.75, 0.75))
	_add_stat_row(stats_box, "最终得分", str(total_score), Color(1.0, 0.85, 0.2))

	var high = SaveManager.get_high_score() if SaveManager.instance else 0
	if total_score >= high and total_score > 0:
		_add_stat_row(stats_box, "新纪录!", str(total_score), Color(1.0, 0.6, 0.1))
	else:
		_add_stat_row(stats_box, "最高分", str(high), Color(0.6, 0.6, 0.55))

	# 按钮
	var spacer = Control.new()
	spacer.custom_minimum_size.y = 10
	vbox.add_child(spacer)

	var btn_box = HBoxContainer.new()
	btn_box.add_theme_constant_override("separation", 16)
	btn_box.alignment = BoxContainer.ALIGNMENT_CENTER
	vbox.add_child(btn_box)

	var restart_btn = _make_button("重新开始", Color(0.18, 0.55, 0.25))
	restart_btn.pressed.connect(func(): get_tree().reload_current_scene())
	btn_box.add_child(restart_btn)

	var quit_btn = _make_button("退出游戏", Color(0.5, 0.15, 0.12))
	quit_btn.pressed.connect(func(): get_tree().quit())
	btn_box.add_child(quit_btn)

	# 动画
	var panel_tw = create_tween()
	panel_tw.tween_interval(0.6)
	panel_tw.tween_property(content, "modulate:a", 1.0, 0.5).set_trans(Tween.TRANS_CUBIC)

	var title_tw = create_tween().set_loops()
	title_tw.tween_property(title, "modulate", Color(1.0, 0.85, 0.85), 1.2).set_trans(Tween.TRANS_SINE)
	title_tw.tween_property(title, "modulate", Color(1.0, 1.0, 1.0), 1.2).set_trans(Tween.TRANS_SINE)

	if SoundManager.instance:
		SoundManager.play_game_over()

func _add_stat_row(parent: VBoxContainer, label_text: String, value_text: String, color: Color):
	var row = HBoxContainer.new()
	row.add_theme_constant_override("separation", 8)
	parent.add_child(row)

	var lbl = Label.new()
	lbl.text = label_text
	lbl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	lbl.add_theme_font_size_override("font_size", 15)
	lbl.add_theme_color_override("font_color", Color(0.65, 0.65, 0.6))
	row.add_child(lbl)

	var val = Label.new()
	val.text = value_text
	val.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	val.add_theme_font_size_override("font_size", 16)
	val.add_theme_color_override("font_color", color)
	val.add_theme_constant_override("outline_size", 1)
	val.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.6))
	row.add_child(val)

func _make_button(text: String, bg_color: Color) -> Button:
	var btn = Button.new()
	btn.text = text
	btn.custom_minimum_size = Vector2(140, 40)
	btn.add_theme_font_size_override("font_size", 15)
	btn.focus_mode = Control.FOCUS_NONE
	var s = StyleBoxFlat.new()
	s.bg_color = bg_color
	s.corner_radius_top_left = 4
	s.corner_radius_top_right = 4
	s.corner_radius_bottom_left = 4
	s.corner_radius_bottom_right = 4
	s.border_width_top = 1
	s.border_width_bottom = 1
	s.border_width_left = 1
	s.border_width_right = 1
	s.border_color = Color(1, 1, 1, 0.1)
	btn.add_theme_stylebox_override("normal", s)
	var h = s.duplicate()
	h.bg_color = bg_color.lightened(0.2)
	btn.add_theme_stylebox_override("hover", h)
	var p = s.duplicate()
	p.bg_color = bg_color.darkened(0.15)
	btn.add_theme_stylebox_override("pressed", p)
	return btn
