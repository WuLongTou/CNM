class_name UIManager
extends Control
## UI管理器
## 负责所有UI元素的创建、更新和交互
## 包括HUD、武器选择面板、tooltip、暂停界面等

signal weapon_card_selected(weapon)
signal upgrade_skipped

# ═══ 组件引用 ═══
var screen_fx: ScreenEffects
var game_over_ui: GameOverUI

# ═══ HUD ═══
var weapon_selection_panel: Panel
var weapon_buttons: Array[Button] = []
var hud_panel: Panel
var health_label: Label
var round_label: Label
var zombies_label: Label
var damage_label: Label
var crit_label: Label
var lifesteal_label: Label
var attack_interval_label: Label
var attack_type_label: Label
var target_priority_label: Label
var dash_cooldown_label: Label
var speed_label: Label
var controls_panel: Panel
var controls_visible: bool = false
var welcome_panel: Panel
var welcome_visible: bool = true
var score_label: Label
var health_bar: ProgressBar
var boss_health_bar: ProgressBar
var boss_health_label: Label
var _boss_ui_visible: bool = false
var _boss_ui_health: int = -1
var _boss_ui_max_health: int = -1

# ═══ 武器选择 ═══
var current_weapon_options: Array = []
var game_manager: GameManager
var weapon_deck: WeaponDeck

# ═══ Tooltip ═══
var tooltip_panel: Panel
var tooltip_label: RichTextLabel
var tooltip_hide_timer: Timer
var tooltip_max_width: float = GameConfig.TOOLTIP_MAX_WIDTH
var tooltip_offset: Vector2 = GameConfig.TOOLTIP_OFFSET
var pause_panel: Panel
var pause_label: Label
var pause_locked_by_upgrade: bool = false
var user_paused: bool = false
var current_weapon_label: Label

var round_banner: Label
var low_health_tween: Tween

# ═══ 效果/Buff 显示 ═══
var effects_container: HBoxContainer
var effect_icons: Dictionary = {}

func _ready():
	add_to_group("ui_manager")
	process_mode = Node.PROCESS_MODE_ALWAYS

	# 初始化子组件
	screen_fx = ScreenEffects.new()
	screen_fx.name = "ScreenEffects"
	add_child(screen_fx)

	game_over_ui = GameOverUI.new()
	game_over_ui.name = "GameOverUI"
	add_child(game_over_ui)

	_create_ui_elements()
	_setup_connections()
	_create_tooltip_panel()
	_create_pause_panel()
	get_viewport().size_changed.connect(_on_ui_viewport_size_changed)
	_on_ui_viewport_size_changed()
	set_process(true)

func _create_ui_elements():
	# 创建HUD面板 - 使用响应式布局
	_create_hud_panel()
	
	# 创建武器选择面板
	_create_weapon_selection_panel()
	
	# 创建操作说明面板
	_create_controls_panel()
	
	# 创建欢迎界面
	_create_welcome_panel()
	
	# 创建回合提示
	_create_round_banner()

func _create_hud_panel():
	hud_panel = Panel.new()
	hud_panel.set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	hud_panel.custom_minimum_size.y = 52
	hud_panel.offset_bottom = 52

	var hud_style = StyleBoxFlat.new()
	hud_style.bg_color = Color(0.04, 0.04, 0.06, 0.90)
	hud_style.border_width_bottom = 1
	hud_style.border_color = Color(0.25, 0.18, 0.12, 0.4)
	hud_panel.add_theme_stylebox_override("panel", hud_style)
	add_child(hud_panel)

	var margin = MarginContainer.new()
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	margin.add_theme_constant_override("margin_left", 10)
	margin.add_theme_constant_override("margin_right", 10)
	margin.add_theme_constant_override("margin_top", 4)
	margin.add_theme_constant_override("margin_bottom", 4)
	hud_panel.add_child(margin)

	var main_vbox = VBoxContainer.new()
	main_vbox.add_theme_constant_override("separation", 3)
	margin.add_child(main_vbox)

	# ═══ 第一行：血条(左) | 回合(中) | 得分(右) ═══
	var row1 = HBoxContainer.new()
	row1.add_theme_constant_override("separation", 12)
	main_vbox.add_child(row1)

	# --- 左：血条区域（固定宽度，不扩展） ---
	var hp_wrapper = HBoxContainer.new()
	hp_wrapper.add_theme_constant_override("separation", 6)
	hp_wrapper.custom_minimum_size.x = 240
	hp_wrapper.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	row1.add_child(hp_wrapper)

	health_bar = ProgressBar.new()
	health_bar.custom_minimum_size = Vector2(180, 14)
	health_bar.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	health_bar.show_percentage = false
	var hp_bg = StyleBoxFlat.new()
	hp_bg.bg_color = Color(0.12, 0.03, 0.03)
	hp_bg.corner_radius_top_left = 2
	hp_bg.corner_radius_bottom_left = 2
	hp_bg.corner_radius_top_right = 2
	hp_bg.corner_radius_bottom_right = 2
	hp_bg.border_width_top = 1
	hp_bg.border_width_bottom = 1
	hp_bg.border_width_left = 1
	hp_bg.border_width_right = 1
	hp_bg.border_color = Color(0.30, 0.10, 0.08, 0.4)
	var hp_fill = StyleBoxFlat.new()
	hp_fill.bg_color = Color(0.65, 0.18, 0.14)
	hp_fill.corner_radius_top_left = 2
	hp_fill.corner_radius_bottom_left = 2
	hp_fill.corner_radius_top_right = 2
	hp_fill.corner_radius_bottom_right = 2
	health_bar.add_theme_stylebox_override("background", hp_bg)
	health_bar.add_theme_stylebox_override("fill", hp_fill)
	hp_wrapper.add_child(health_bar)

	health_label = Label.new()
	health_label.text = "120/120"
	health_label.add_theme_font_size_override("font_size", 11)
	health_label.add_theme_color_override("font_color", Color(0.9, 0.85, 0.8))
	health_label.add_theme_constant_override("outline_size", 2)
	health_label.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.9))
	health_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	hp_wrapper.add_child(health_label)

	# --- 中：回合+僵尸（占据中间空间） ---
	var center_box = HBoxContainer.new()
	center_box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	center_box.alignment = BoxContainer.ALIGNMENT_CENTER
	center_box.add_theme_constant_override("separation", 16)
	row1.add_child(center_box)

	round_label = Label.new()
	round_label.text = "第 1 波"
	round_label.add_theme_font_size_override("font_size", 14)
	round_label.add_theme_color_override("font_color", Color(0.90, 0.78, 0.35))
	round_label.add_theme_constant_override("outline_size", 2)
	round_label.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.6))
	center_box.add_child(round_label)

	zombies_label = Label.new()
	zombies_label.text = "剩余 0"
	zombies_label.add_theme_font_size_override("font_size", 12)
	zombies_label.add_theme_color_override("font_color", Color(0.75, 0.75, 0.75))
	center_box.add_child(zombies_label)

	# --- 右：得分 ---
	score_label = Label.new()
	score_label.text = "0"
	score_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	score_label.custom_minimum_size.x = 80
	score_label.size_flags_horizontal = Control.SIZE_SHRINK_END
	score_label.add_theme_font_size_override("font_size", 14)
	score_label.add_theme_color_override("font_color", Color(1.0, 0.85, 0.25))
	score_label.add_theme_constant_override("outline_size", 2)
	score_label.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.6))
	row1.add_child(score_label)

	# ═══ 第二行：属性 + 效果图标 ═══
	var row2 = HBoxContainer.new()
	row2.add_theme_constant_override("separation", 10)
	main_vbox.add_child(row2)

	damage_label = _make_stat_label("攻击 10", Color(1.0, 0.65, 0.25))
	row2.add_child(damage_label)
	attack_interval_label = _make_stat_label("间隔 0.50s", Color(0.8, 0.8, 0.45))
	row2.add_child(attack_interval_label)
	crit_label = _make_stat_label("暴击 0%", Color(0.85, 0.65, 1.0))
	row2.add_child(crit_label)
	lifesteal_label = _make_stat_label("吸血 0%", Color(0.85, 0.40, 0.40))
	row2.add_child(lifesteal_label)
	attack_type_label = _make_stat_label("近战", Color(0.50, 0.85, 0.85))
	row2.add_child(attack_type_label)
	target_priority_label = _make_stat_label("最近", Color(0.55, 0.75, 0.75))
	row2.add_child(target_priority_label)
	dash_cooldown_label = _make_stat_label("闪避 就绪", Color(0.55, 0.85, 0.55))
	row2.add_child(dash_cooldown_label)
	speed_label = _make_stat_label("速度 0", Color(0.55, 0.75, 0.65))
	row2.add_child(speed_label)

	# 效果图标容器
	effects_container = HBoxContainer.new()
	effects_container.add_theme_constant_override("separation", 3)
	effects_container.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row2.add_child(effects_container)

	var help_btn = Button.new()
	help_btn.text = "?"
	help_btn.custom_minimum_size = Vector2(20, 20)
	help_btn.focus_mode = Control.FOCUS_NONE
	help_btn.pressed.connect(_toggle_controls_panel)
	row2.add_child(help_btn)

	# ═══ Boss 血条 ═══
	boss_health_bar = ProgressBar.new()
	boss_health_bar.set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	boss_health_bar.offset_top = 66
	boss_health_bar.offset_bottom = 78
	boss_health_bar.offset_left = 16
	boss_health_bar.offset_right = -16
	boss_health_bar.custom_minimum_size.y = 12
	boss_health_bar.visible = false
	boss_health_bar.z_index = 5
	boss_health_bar.show_percentage = false
	var boss_bg = StyleBoxFlat.new()
	boss_bg.bg_color = Color(0.15, 0.05, 0.05, 0.9)
	boss_bg.corner_radius_top_left = 4
	boss_bg.corner_radius_top_right = 4
	boss_bg.corner_radius_bottom_left = 4
	boss_bg.corner_radius_bottom_right = 4
	boss_health_bar.add_theme_stylebox_override("background", boss_bg)
	var boss_fill = StyleBoxFlat.new()
	boss_fill.bg_color = Color(0.85, 0.15, 0.1)
	boss_fill.corner_radius_top_left = 4
	boss_fill.corner_radius_top_right = 4
	boss_fill.corner_radius_bottom_left = 4
	boss_fill.corner_radius_bottom_right = 4
	boss_health_bar.add_theme_stylebox_override("fill", boss_fill)
	add_child(boss_health_bar)

	boss_health_label = Label.new()
	boss_health_label.set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	boss_health_label.offset_top = 66
	boss_health_label.offset_bottom = 84
	boss_health_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	boss_health_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	boss_health_label.add_theme_font_size_override("font_size", 12)
	boss_health_label.add_theme_color_override("font_color", Color(1.0, 0.3, 0.3))
	boss_health_label.add_theme_constant_override("outline_size", 2)
	boss_health_label.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.7))
	boss_health_label.visible = false
	boss_health_label.z_index = 6
	add_child(boss_health_label)

func _make_stat_label(text: String, color: Color) -> Label:
	var lbl = Label.new()
	lbl.text = text
	lbl.add_theme_font_size_override("font_size", 10)
	lbl.add_theme_color_override("font_color", color)
	lbl.add_theme_constant_override("outline_size", 1)
	lbl.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.5))
	return lbl

func _create_weapon_selection_panel():
	weapon_selection_panel = Panel.new()
	weapon_selection_panel.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	weapon_selection_panel.visible = false
	weapon_selection_panel.process_mode = Node.PROCESS_MODE_ALWAYS
	# 设置武器选择背景 - 渐变暗色
	var weapon_style = StyleBoxFlat.new()
	weapon_style.bg_color = Color(0.02, 0.02, 0.08, 0.92)
	weapon_selection_panel.add_theme_stylebox_override("panel", weapon_style)
	add_child(weapon_selection_panel)
	
	# 创建居中容器
	var center_container = CenterContainer.new()
	center_container.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	weapon_selection_panel.add_child(center_container)
	
	# 创建主容器
	var main_vbox = VBoxContainer.new()
	main_vbox.add_theme_constant_override("separation", 30)
	center_container.add_child(main_vbox)
	
	# 武器选择标题
	var title_label = Label.new()
	title_label.text = "⚔️ 选择本回合强化 ⚔️"
	title_label.add_theme_font_size_override("font_size", 32)
	title_label.add_theme_color_override("font_color", Color.GOLD)
	title_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	main_vbox.add_child(title_label)

	current_weapon_label = Label.new()
	current_weapon_label.text = "当前武器: 无"
	current_weapon_label.add_theme_font_size_override("font_size", 16)
	current_weapon_label.add_theme_color_override("font_color", Color(0.8, 0.8, 0.9))
	current_weapon_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	current_weapon_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	current_weapon_label.custom_minimum_size.x = 720
	main_vbox.add_child(current_weapon_label)

	# 说明文字
	var desc_label = Label.new()
	desc_label.text = "你可能抽到新武器、装备，或对已有装备的强化卡"
	desc_label.add_theme_font_size_override("font_size", 18)
	desc_label.add_theme_color_override("font_color", Color.WHITE)
	desc_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	desc_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	desc_label.custom_minimum_size.x = 720
	main_vbox.add_child(desc_label)
	
	# 创建武器按钮容器
	var button_container = HBoxContainer.new()
	button_container.add_theme_constant_override("separation", 20)
	button_container.alignment = BoxContainer.ALIGNMENT_CENTER
	main_vbox.add_child(button_container)
	
	# 创建三个武器按钮（简化卡片设计）
	for i in range(3):
		var button = Button.new()
		button.custom_minimum_size = Vector2(200, 280)
		button.pressed.connect(_on_weapon_button_pressed.bind(i))
		# 整个卡片都触发tooltip
		button.mouse_entered.connect(_on_card_mouse_entered.bind(i))
		button.mouse_exited.connect(_on_card_mouse_exited)
		button_container.add_child(button)
		weapon_buttons.append(button)
		
		var card_vbox = VBoxContainer.new()
		card_vbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		card_vbox.mouse_filter = Control.MOUSE_FILTER_IGNORE
		card_vbox.add_theme_constant_override("separation", 10)
		var margin = MarginContainer.new()
		margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		margin.add_theme_constant_override("margin_top", 15)
		margin.add_theme_constant_override("margin_bottom", 15)
		margin.add_theme_constant_override("margin_left", 15)
		margin.add_theme_constant_override("margin_right", 15)
		margin.mouse_filter = Control.MOUSE_FILTER_IGNORE
		button.add_child(margin)
		margin.add_child(card_vbox)

		# 1. 升级类型/等级
		var category_label = Label.new()
		category_label.name = "Category"
		category_label.text = "【类型】"
		category_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		category_label.add_theme_font_size_override("font_size", 12)
		category_label.add_theme_color_override("font_color", Color.LIGHT_GRAY)
		card_vbox.add_child(category_label)
		
		# 2. 名称（大字）
		var title = Label.new()
		title.name = "Title"
		title.text = "武器名称"
		title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		title.add_theme_font_size_override("font_size", 22)
		title.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		card_vbox.add_child(title)
		
		# 分隔线
		var sep1 = HSeparator.new()
		sep1.mouse_filter = Control.MOUSE_FILTER_IGNORE
		card_vbox.add_child(sep1)

		# 3. 图标（大）
		var icon_placeholder = Control.new()
		icon_placeholder.name = "IconContainer"
		icon_placeholder.custom_minimum_size.y = 70
		icon_placeholder.mouse_filter = Control.MOUSE_FILTER_IGNORE
		card_vbox.add_child(icon_placeholder)
		
		var icon_label = Label.new()
		icon_label.name = "Icon"
		icon_label.text = "⚔️"
		icon_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		icon_label.add_theme_font_size_override("font_size", 55)
		icon_placeholder.add_child(icon_label)
		icon_label.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	
		# 4. 类型文字
		var type_label = Label.new()
		type_label.name = "Type"
		type_label.text = "类型"
		type_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		type_label.add_theme_font_size_override("font_size", 14)
		type_label.add_theme_color_override("font_color", Color.LIGHT_GRAY)
		card_vbox.add_child(type_label)

		# 5. 简要描述（只显示关键词）
		var desc = Label.new()
		desc.name = "Desc"
		desc.text = ""
		desc.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		desc.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		desc.add_theme_font_size_override("font_size", 13)
		desc.add_theme_color_override("font_color", Color(0.8, 0.9, 1.0))
		card_vbox.add_child(desc)
		
		# 占位撑开
		var spacer = Control.new()
		spacer.size_flags_vertical = Control.SIZE_EXPAND_FILL
		spacer.mouse_filter = Control.MOUSE_FILTER_IGNORE
		card_vbox.add_child(spacer)
		
		# 6. 稀有度
		var rarity = Label.new()
		rarity.name = "Rarity"
		rarity.text = "稀有度"
		rarity.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		rarity.add_theme_font_size_override("font_size", 14)
		card_vbox.add_child(rarity)
		
		# 7. 悬停提示
		var hint_label = Label.new()
		hint_label.name = "Hint"
		hint_label.text = "悬停查看详情"
		hint_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		hint_label.add_theme_font_size_override("font_size", 11)
		hint_label.add_theme_color_override("font_color", Color(0.5, 0.5, 0.6))
		card_vbox.add_child(hint_label)
	
	# 跳过按钮
	var skip_container = CenterContainer.new()
	main_vbox.add_child(skip_container)
	
	var skip_button = Button.new()
	skip_button.text = "跳过升级"
	skip_button.add_theme_font_size_override("font_size", 16)
	skip_button.custom_minimum_size = Vector2(150, 40)
	skip_button.pressed.connect(_on_skip_pressed)
	skip_container.add_child(skip_button)

func _create_tooltip_panel():
	# 使用RichTextLabel实现更好的文本渲染
	tooltip_panel = Panel.new()
	tooltip_panel.visible = false
	tooltip_panel.z_index = 100
	tooltip_panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tooltip_panel.process_mode = Node.PROCESS_MODE_ALWAYS
	
	# 现代化tooltip样式 - 深色半透明背景
	var tooltip_style = StyleBoxFlat.new()
	tooltip_style.bg_color = Color(0.05, 0.05, 0.12, 0.96)
	tooltip_style.border_width_left = 2
	tooltip_style.border_width_right = 2
	tooltip_style.border_width_top = 2
	tooltip_style.border_width_bottom = 2
	tooltip_style.border_color = Color(0.35, 0.55, 0.95, 0.9)
	tooltip_style.corner_radius_top_left = 8
	tooltip_style.corner_radius_top_right = 8
	tooltip_style.corner_radius_bottom_left = 8
	tooltip_style.corner_radius_bottom_right = 8
	tooltip_style.shadow_size = 10
	tooltip_style.shadow_color = Color(0, 0, 0, 0.6)
	tooltip_style.shadow_offset = Vector2(3, 3)
	tooltip_panel.add_theme_stylebox_override("panel", tooltip_style)
	
	# 使用RichTextLabel以获得更好的文本布局
	tooltip_label = RichTextLabel.new()
	tooltip_label.bbcode_enabled = true
	tooltip_label.fit_content = true  # 关键：自动适应内容高度
	tooltip_label.scroll_active = false
	tooltip_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tooltip_label.add_theme_font_size_override("normal_font_size", 14)
	tooltip_label.add_theme_font_size_override("bold_font_size", 15)
	tooltip_label.add_theme_color_override("default_color", Color.WHITE)
	tooltip_label.custom_minimum_size = Vector2(320, 0)  # 最小宽度
	
	# 添加边距
	var margin = MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 16)
	margin.add_theme_constant_override("margin_right", 16)
	margin.add_theme_constant_override("margin_top", 12)
	margin.add_theme_constant_override("margin_bottom", 12)
	margin.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tooltip_panel.add_child(margin)
	margin.add_child(tooltip_label)
	
	add_child(tooltip_panel)
	
	tooltip_hide_timer = Timer.new()
	tooltip_hide_timer.one_shot = true
	tooltip_hide_timer.wait_time = 0.2
	tooltip_hide_timer.timeout.connect(_hide_tooltip_panel)
	add_child(tooltip_hide_timer)

func _create_pause_panel():
	pause_panel = Panel.new()
	pause_panel.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	pause_panel.visible = false
	pause_panel.z_index = 90
	pause_panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	pause_panel.process_mode = Node.PROCESS_MODE_ALWAYS
	
	var style_box = StyleBoxFlat.new()
	style_box.bg_color = Color(0, 0, 0, 0.6)
	pause_panel.add_theme_stylebox_override("panel", style_box)
	add_child(pause_panel)
	
	var center = CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	pause_panel.add_child(center)
	
	pause_label = Label.new()
	pause_label.text = "⏸ 暂停中\n按 ESC 继续"
	pause_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	pause_label.add_theme_font_size_override("font_size", 28)
	pause_label.add_theme_color_override("font_color", Color.WHITE)
	center.add_child(pause_label)

func _create_round_banner():
	round_banner = Label.new()
	round_banner.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	var viewport_size := get_viewport().get_visible_rect().size
	round_banner.custom_minimum_size = Vector2(min(720.0, viewport_size.x - 40.0), 80)
	round_banner.text = ""
	round_banner.visible = false
	round_banner.z_index = 50
	round_banner.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	round_banner.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	round_banner.add_theme_font_size_override("font_size", 28)
	round_banner.add_theme_color_override("font_color", Color.GOLD)
	round_banner.add_theme_constant_override("outline_size", 6)
	round_banner.add_theme_color_override("font_outline_color", Color.BLACK)
	add_child(round_banner)


func _create_controls_panel():
	controls_panel = Panel.new()
	controls_panel.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
	_on_ui_viewport_size_changed()
	controls_panel.visible = false
	controls_panel.focus_mode = Control.FOCUS_NONE
	
	# 设置面板样式 - 半透明深色背景
	var style_box = StyleBoxFlat.new()
	style_box.bg_color = Color(0.1, 0.1, 0.2, 0.95)
	style_box.border_width_left = 3
	style_box.border_width_top = 3
	style_box.border_width_right = 3
	style_box.border_width_bottom = 3
	style_box.border_color = Color(0.4, 0.6, 1.0, 0.8)
	style_box.corner_radius_top_left = 10
	style_box.corner_radius_top_right = 10
	style_box.corner_radius_bottom_left = 10
	style_box.corner_radius_bottom_right = 10
	controls_panel.add_theme_stylebox_override("panel", style_box)
	
	add_child(controls_panel)
	
	# 创建滚动容器
	var scroll_container = ScrollContainer.new()
	scroll_container.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	scroll_container.set_offsets_preset(Control.PRESET_FULL_RECT, Control.PRESET_MODE_MINSIZE, 10)
	controls_panel.add_child(scroll_container)
	
	# 内容容器
	var container = VBoxContainer.new()
	container.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll_container.add_child(container)
	
	# 标题
	var title_label = Label.new()
	title_label.text = "🎮 操作指南"
	title_label.add_theme_font_size_override("font_size", 20)
	title_label.add_theme_color_override("font_color", Color(0.4, 0.8, 1.0))
	title_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	container.add_child(title_label)
	
	# 分隔线
	var separator = HSeparator.new()
	separator.add_theme_color_override("separator", Color(0.4, 0.6, 1.0, 0.5))
	container.add_child(separator)
	
	# 创建信息卡片
	_create_info_card(container, "🏃 移动", "WASD 或 方向键\n自由移动角色", Color(0.3, 0.8, 0.3))
	_create_info_card(container, "⚔️ 攻击", "按住空格进行攻击\n魔法类自动锁定", Color(0.8, 0.3, 0.3))
	_create_info_card(container, "🗡️ 武器类型", "近战: 扇形斩击\n远程: 发射子弹\n魔法: 追踪导弹\n范围: 爆炸攻击", Color(0.8, 0.6, 0.2))
	_create_info_card(container, "🎯 回合目标", "消灭所有僵尸\n回合开始时选择强化\n可跳过本次强化", Color(0.6, 0.3, 0.8))
	_create_info_card(container, "💨 冲刺", "按 Shift 快速闪避\n有冷却时间", Color(0.4, 0.7, 1.0))
	_create_info_card(container, "🔭 锁定", "按 Tab 切换目标优先级\n最近/最远/密集", Color(0.5, 0.8, 0.9))
	_create_info_card(container, "⏸ 暂停", "按 ESC 暂停/继续游戏\n强化阶段无法手动暂停", Color(0.8, 0.8, 0.5))
	
	# 关闭提示
	var close_hint = Label.new()
	close_hint.text = "💡 按 H 键切换显示"
	close_hint.add_theme_font_size_override("font_size", 12)
	close_hint.add_theme_color_override("font_color", Color(0.7, 0.7, 0.7))
	close_hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	container.add_child(close_hint)

func _create_info_card(parent: VBoxContainer, title: String, content: String, accent_color: Color):
	# 卡片容器
	var card = Panel.new()
	card.custom_minimum_size = Vector2(0, 80)
	
	# 卡片样式
	var card_style = StyleBoxFlat.new()
	card_style.bg_color = Color(0.15, 0.15, 0.25, 0.8)
	card_style.border_width_left = 2
	card_style.border_color = accent_color
	card_style.corner_radius_top_left = 5
	card_style.corner_radius_top_right = 5
	card_style.corner_radius_bottom_left = 5
	card_style.corner_radius_bottom_right = 5
	card.add_theme_stylebox_override("panel", card_style)
	
	parent.add_child(card)
	
	# 卡片内容
	var card_container = VBoxContainer.new()
	card_container.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	card_container.set_offsets_preset(Control.PRESET_FULL_RECT, Control.PRESET_MODE_MINSIZE, 8)
	card.add_child(card_container)
	
	# 标题
	var title_label = Label.new()
	title_label.text = title
	title_label.add_theme_font_size_override("font_size", 14)
	title_label.add_theme_color_override("font_color", accent_color)
	card_container.add_child(title_label)
	
	# 内容
	var content_label = Label.new()
	content_label.text = content
	content_label.add_theme_font_size_override("font_size", 12)
	content_label.add_theme_color_override("font_color", Color.WHITE)
	content_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	card_container.add_child(content_label)
	
	# 间距
	var spacer = Control.new()
	spacer.custom_minimum_size = Vector2(0, 5)
	parent.add_child(spacer)

func _create_welcome_panel():
	welcome_panel = Panel.new()
	welcome_panel.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var style_box = StyleBoxFlat.new()
	style_box.bg_color = Color(0, 0, 0, 0.8)
	welcome_panel.add_theme_stylebox_override("panel", style_box)
	add_child(welcome_panel)
	
	# 添加边距容器
	var margin_container = MarginContainer.new()
	margin_container.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	margin_container.add_theme_constant_override("margin_left", 50)
	margin_container.add_theme_constant_override("margin_right", 50)
	margin_container.add_theme_constant_override("margin_top", 30)
	margin_container.add_theme_constant_override("margin_bottom", 30)
	welcome_panel.add_child(margin_container)
	
	# 创建居中容器
	var center_container = CenterContainer.new()
	center_container.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	margin_container.add_child(center_container)
	
	var vbox = VBoxContainer.new()
	vbox.add_theme_constant_override("separation", 25)
	center_container.add_child(vbox)
	
	# 游戏标题
	var title = Label.new()
	title.text = "🧟 僵尸大战 🧟"
	title.add_theme_font_size_override("font_size", 36)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	vbox.add_child(title)
	
	# 副标题
	var subtitle = Label.new()
	subtitle.text = "Zombie vs Survivors"
	subtitle.add_theme_font_size_override("font_size", 18)
	subtitle.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	vbox.add_child(subtitle)
	
	# 游戏介绍
	var intro_text = [
		"欢迎来到僵尸大战！",
		"",
		"在这个生存游戏中，你需要：",
		"• 每回合选择武器卡牌强化自己",
		"• 消灭不断涌现的僵尸",
		"• 尽可能坚持更多回合",
		"",
		"准备好迎接挑战了吗？"
	]
	
	for text in intro_text:
		var label = Label.new()
		label.text = text
		if text == "":
			label.custom_minimum_size.y = 8
		else:
			label.add_theme_font_size_override("font_size", 16)
		label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		vbox.add_child(label)
	
	# 开始游戏按钮
	var start_button = Button.new()
	start_button.text = "开始游戏"
	start_button.add_theme_font_size_override("font_size", 22)
	start_button.custom_minimum_size = Vector2(180, 50)
	start_button.pressed.connect(_start_game)
	vbox.add_child(start_button)
	
	# 操作说明按钮
	var help_button = Button.new()
	help_button.text = "操作说明"
	help_button.add_theme_font_size_override("font_size", 16)
	help_button.custom_minimum_size = Vector2(120, 40)
	help_button.pressed.connect(_toggle_controls_panel)
	vbox.add_child(help_button)

func _start_game():
	welcome_panel.visible = false
	welcome_visible = false

func _setup_connections():
	# 连接游戏管理器信号
	if game_manager:
		game_manager.weapon_selection_started.connect(_on_weapon_selection_started)
		game_manager.round_started.connect(_on_round_started)

func _input(event):
	if event is InputEventKey and event.pressed:
		if event.keycode == KEY_H:
			_toggle_controls_panel()

func _unhandled_input(event):
	if event.is_action_pressed("pause"):
		if is_upgrade_active():
			return
		set_user_paused(not user_paused)
		get_viewport().set_input_as_handled()

func _process(delta):
	if tooltip_panel and tooltip_panel.visible:
		_position_tooltip()
	if game_manager and game_manager.player:
		_update_dash_display(game_manager.player)
		_update_attack_interval_display(game_manager.player)
	# Boss 血条更新
	if game_manager and game_manager.boss_zombie and is_instance_valid(game_manager.boss_zombie):
		var boss = game_manager.boss_zombie
		if not _boss_ui_visible:
			_boss_ui_visible = true
			if boss_health_bar:
				boss_health_bar.visible = true
			if boss_health_label:
				boss_health_label.visible = true
		if boss.current_health != _boss_ui_health or boss.max_health != _boss_ui_max_health:
			_boss_ui_health = boss.current_health
			_boss_ui_max_health = boss.max_health
			if boss_health_bar:
				boss_health_bar.max_value = boss.max_health
				boss_health_bar.value = boss.current_health
			if boss_health_label:
				boss_health_label.text = "BOSS  " + str(boss.current_health) + "/" + str(boss.max_health)
	else:
		if _boss_ui_visible:
			_boss_ui_visible = false
			_boss_ui_health = -1
			_boss_ui_max_health = -1
			if boss_health_bar:
				boss_health_bar.visible = false
			if boss_health_label:
				boss_health_label.visible = false
	if screen_fx:
		screen_fx.process_effects(delta)

func _update_dash_display(player: Player):
	if not dash_cooldown_label:
		return
	if player.can_dash:
		dash_cooldown_label.text = "闪避 就绪"
		dash_cooldown_label.tooltip_text = "闪避已就绪，按 Shift 使用"
	else:
		var remaining = player.dash_cooldown_timer.time_left if player.dash_cooldown_timer else 0.0
		dash_cooldown_label.text = "闪避 " + str(snapped(remaining, 0.1)) + "s"
		dash_cooldown_label.tooltip_text = "闪避冷却中，还需 " + str(snapped(remaining, 0.1)) + "s"

func _update_attack_interval_display(player: Player):
	if not attack_interval_label:
		return
	var interval_text = str(snapped(player.attack_cooldown, 0.01)) + "s"
	var cooldown_text = "就绪"
	if player.combat and player.combat.attack_timer and not player.combat.can_attack:
		cooldown_text = str(snapped(player.combat.attack_timer.time_left, 0.1)) + "s"
	attack_interval_label.text = "间隔 " + interval_text + " | 冷却 " + cooldown_text
	attack_interval_label.tooltip_text = "攻击间隔: " + interval_text + "\n下次可攻击: " + cooldown_text

func set_user_paused(paused: bool):
	user_paused = paused
	pause_panel.visible = paused
	if paused:
		get_tree().paused = true
	elif not pause_locked_by_upgrade:
		get_tree().paused = false

func is_upgrade_active() -> bool:
	return weapon_selection_panel.visible

func _toggle_controls_panel():
	# 如果武器选择面板正在显示，不允许打开操作说明
	if weapon_selection_panel.visible and !controls_visible:
		return
		
	controls_visible = !controls_visible
	controls_panel.visible = controls_visible

func show_weapon_selection():
	if not weapon_deck:
		print("错误：武器牌堆未设置")
		return
	pause_locked_by_upgrade = true
	get_tree().paused = true
	if pause_panel:
		pause_panel.visible = false
	
	# 隐藏操作说明面板
	if controls_visible:
		_toggle_controls_panel()
	
	if game_manager and game_manager.player:
		_update_current_weapon_label(game_manager.player)
		current_weapon_options = weapon_deck.draw_three_cards(game_manager.player)
	else:
		current_weapon_options = weapon_deck.draw_three_cards()
	
	# 更新按钮显示（简化版 - 详细信息在tooltip中）
	for i in range(3):
		if i < current_weapon_options.size():
			var weapon = current_weapon_options[i]
			var button = weapon_buttons[i]
			button.visible = true

			if weapon is UpgradeCard:
				_render_upgrade_card(button, weapon)
				continue

			# 获取内部控件
			var vbox = button.get_child(0).get_child(0) # Button -> Margin -> VBox
			var category_node = vbox.get_node("Category")
			var title_node = vbox.get_node("Title")
			var type_node = vbox.get_node("Type")
			var icon_node = vbox.get_node("IconContainer/Icon")
			var desc_node = vbox.get_node("Desc")
			var rarity_node = vbox.get_node("Rarity")
			
			# 名称
			title_node.text = weapon.card_name
			
			# 类别和等级
			var upgrade_category = weapon.upgrade_category
			if upgrade_category == "":
				upgrade_category = "武器" if weapon.category == WeaponCard.WeaponCategory.WEAPON else "装备"
			category_node.text = "【" + upgrade_category + " Lv." + str(weapon.tier) + "】"
			
			# 类型
			if weapon.category == WeaponCard.WeaponCategory.WEAPON:
				type_node.text = weapon.get_weapon_type_text()
			else:
				type_node.text = "装备"
			
			# 简要描述（只显示关键词，不显示数值）
			var keywords = []
			if weapon.damage_modifier > 1.0:
				keywords.append("伤害↑")
			elif weapon.damage_modifier < 1.0:
				keywords.append("伤害↓")
			if weapon.attack_speed_modifier > 1.0:
				keywords.append("攻速↑")
			elif weapon.attack_speed_modifier < 1.0:
				keywords.append("攻速↓")
			if weapon.health_modifier > 0:
				keywords.append("生命↑")
			elif weapon.health_modifier < 0:
				keywords.append("生命↓")
			if weapon.speed_modifier > 0:
				keywords.append("速度↑")
			if weapon.critical_bonus > 0:
				keywords.append("暴击")
			if weapon.life_steal_bonus > 0:
				keywords.append("吸血")
			if weapon.freeze_bonus > 0:
				keywords.append("冰冻")
			if weapon.poison_bonus > 0:
				keywords.append("中毒")
			if weapon.grants_aura:
				keywords.append("护盾")
			if weapon.health_cost > 0:
				keywords.append("⚠消耗生命")
			
			# 特效关键词
			if weapon.melee_effects.size() > 0 or weapon.ranged_effects.size() > 0 or weapon.magic_effects.size() > 0:
				keywords.append("特效")
			
			desc_node.text = " ".join(keywords) if keywords.size() > 0 else "基础"
			
			# 稀有度
			rarity_node.text = _get_rarity_text(weapon.rarity)
			
			# 图标
			icon_node.text = weapon.icon
			
			# 设置样式颜色
			var base_color: Color
			var border_color: Color
			
			match weapon.rarity:
				1: # 普通 (灰/白)
					base_color = Color(0.25, 0.25, 0.30, 0.95)
					border_color = Color(0.5, 0.5, 0.55)
					title_node.add_theme_color_override("font_color", Color.WHITE)
					rarity_node.add_theme_color_override("font_color", Color.LIGHT_GRAY)
				2: # 稀有 (蓝)
					base_color = Color(0.12, 0.18, 0.32, 0.95)
					border_color = Color(0.3, 0.55, 0.95)
					title_node.add_theme_color_override("font_color", Color(0.6, 0.8, 1.0))
					rarity_node.add_theme_color_override("font_color", Color(0.4, 0.7, 1.0))
				3: # 史诗 (紫)
					base_color = Color(0.22, 0.12, 0.32, 0.95)
					border_color = Color(0.7, 0.35, 0.95)
					title_node.add_theme_color_override("font_color", Color(0.9, 0.6, 1.0))
					rarity_node.add_theme_color_override("font_color", Color(0.8, 0.5, 1.0))
				4: # 传说 (金)
					base_color = Color(0.32, 0.26, 0.12, 0.95)
					border_color = Color(1.0, 0.8, 0.25)
					title_node.add_theme_color_override("font_color", Color(1.0, 0.9, 0.4))
					rarity_node.add_theme_color_override("font_color", Color.GOLD)
				_:
					base_color = Color(0.25, 0.25, 0.30, 0.95)
					border_color = Color(0.5, 0.5, 0.55)
					title_node.add_theme_color_override("font_color", Color.WHITE)
					rarity_node.add_theme_color_override("font_color", Color.LIGHT_GRAY)

			# 应用样式
			var style = StyleBoxFlat.new()
			style.bg_color = base_color
			style.border_width_left = 3
			style.border_width_right = 3
			style.border_width_top = 3
			style.border_width_bottom = 3
			style.border_color = border_color
			style.corner_radius_top_left = 10
			style.corner_radius_top_right = 10
			style.corner_radius_bottom_left = 10
			style.corner_radius_bottom_right = 10
			style.shadow_size = 8
			style.shadow_color = Color(0, 0, 0, 0.4)
			
			button.add_theme_stylebox_override("normal", style)
			
			# Hover 样式
			var hover_style = style.duplicate()
			hover_style.bg_color = base_color.lightened(0.15)
			hover_style.border_color = border_color.lightened(0.25)
			hover_style.shadow_size = 12
			button.add_theme_stylebox_override("hover", hover_style)
			
		else:
			weapon_buttons[i].visible = false
	
	weapon_selection_panel.visible = true

func _update_current_weapon_label(player: Player):
	if not current_weapon_label:
		return
	var weapon_name = "无"
	var weapon_type_text = ""
	var effects = []
	for effect in player.active_effects:
		if effect.category == WeaponCard.WeaponCategory.WEAPON:
			weapon_name = effect.card_name
			weapon_type_text = effect.get_weapon_type_text()
			for m in effect.melee_effects:
				var level = effect.melee_effect_levels.get(m, 1)
				effects.append(effect.get_melee_effect_desc(m, level))
			for r in effect.ranged_effects:
				var level = effect.ranged_effect_levels.get(r, 1)
				effects.append(effect.get_ranged_effect_desc(r, level))
			for g in effect.magic_effects:
				var level = effect.magic_effect_levels.get(g, 1)
				effects.append(effect.get_magic_effect_desc(g, level))
			break
	
	var effect_text = " | ".join(effects) if effects.size() > 0 else "无特效"
	effect_text = _truncate_text(effect_text, 36)
	if weapon_name == "无":
		current_weapon_label.text = "当前武器: 无"
	else:
		current_weapon_label.text = "当前武器: " + weapon_name + " (" + weapon_type_text + ") | " + effect_text

func _truncate_text(text: String, max_len: int) -> String:
	if text.length() <= max_len:
		return text
	return text.substr(0, max_len - 1) + "…"

func hide_weapon_selection():
	weapon_selection_panel.visible = false
	_hide_tooltip()  # 隐藏tooltip
	pause_locked_by_upgrade = false
	if not user_paused:
		get_tree().paused = false
	elif pause_panel:
		pause_panel.visible = true

func _get_rarity_text(rarity: int) -> String:
	return WeaponFormatter.get_rarity_text(rarity)

func _on_weapon_button_pressed(index: int):
	if index < current_weapon_options.size():
		var selected_weapon = current_weapon_options[index]
		weapon_card_selected.emit(selected_weapon)
		hide_weapon_selection()
		_hide_tooltip()
		
		if SoundManager.instance:
			SoundManager.play_weapon_select()

func _on_skip_pressed():
	upgrade_skipped.emit()
	hide_weapon_selection()
	_hide_tooltip()
	print("跳过升级")

func _on_card_mouse_entered(index: int):
	# 整张卡片悬停时显示详细tooltip
	if index < current_weapon_options.size():
		var weapon = current_weapon_options[index]
		if weapon is UpgradeCard:
			_show_tooltip(_get_upgrade_tooltip_text(weapon))
		else:
			_show_tooltip(_get_weapon_tooltip_text(weapon))

func _on_card_mouse_exited():
	if tooltip_hide_timer:
		tooltip_hide_timer.start()

func _hide_tooltip():
	if tooltip_panel:
		tooltip_panel.visible = false
	if tooltip_hide_timer and not tooltip_hide_timer.is_stopped():
		tooltip_hide_timer.stop()

func _show_tooltip(text: String):
	if not tooltip_panel or not tooltip_label:
		return
	
	if tooltip_hide_timer and not tooltip_hide_timer.is_stopped():
		tooltip_hide_timer.stop()
	
	# 设置BBCode文本
	tooltip_label.text = text
	
	# 强制更新布局
	tooltip_label.reset_size()
	
	# 计算尺寸 - 使用固定宽度，高度自适应
	var screen_size = get_viewport().get_visible_rect().size
	var content_width = min(tooltip_max_width, max(260.0, screen_size.x * 0.32))
	var content_height = tooltip_label.get_content_height()
	
	# 确保最小高度
	content_height = max(content_height, 100)
	
	# 面板尺寸 = 内容 + 边距
	var panel_width = content_width + 32  # 16*2 边距
	var panel_height = content_height + 24  # 12*2 边距
	
	tooltip_label.custom_minimum_size = Vector2(content_width, content_height)
	tooltip_panel.custom_minimum_size = Vector2(panel_width, panel_height)
	tooltip_panel.size = Vector2(panel_width, panel_height)
	
	# 定位后显示
	_position_tooltip()
	tooltip_panel.visible = true

func _position_tooltip():
	var mouse_pos = get_viewport().get_mouse_position()
	var panel_size = tooltip_panel.get_combined_minimum_size()
	var screen_size = get_viewport().get_visible_rect().size
	
	var candidates = [
		mouse_pos + tooltip_offset,
		mouse_pos + Vector2(tooltip_offset.x, -panel_size.y - tooltip_offset.y),
		mouse_pos + Vector2(-panel_size.x - tooltip_offset.x, tooltip_offset.y),
		mouse_pos + Vector2(-panel_size.x - tooltip_offset.x, -panel_size.y - tooltip_offset.y)
	]
	
	var best_pos = mouse_pos + tooltip_offset
	var best_dist = INF
	for pos in candidates:
		var inside = pos.x >= 0 and pos.y >= 0 and pos.x + panel_size.x <= screen_size.x and pos.y + panel_size.y <= screen_size.y
		if inside:
			var dist = pos.distance_to(mouse_pos)
			if dist < best_dist:
				best_dist = dist
				best_pos = pos
	
	if best_dist < INF:
		tooltip_panel.position = best_pos
		return
	
	tooltip_panel.position = Vector2(
		clamp(mouse_pos.x + tooltip_offset.x, 10, screen_size.x - panel_size.x - 10),
		clamp(mouse_pos.y + tooltip_offset.y, 10, screen_size.y - panel_size.y - 10)
	)

func _render_upgrade_card(button: Button, upgrade: UpgradeCard):
	var vbox = button.get_child(0).get_child(0)
	var category_node = vbox.get_node("Category")
	var title_node = vbox.get_node("Title")
	var type_node = vbox.get_node("Type")
	var icon_node = vbox.get_node("IconContainer/Icon")
	var desc_node = vbox.get_node("Desc")
	var rarity_node = vbox.get_node("Rarity")

	category_node.text = "【强化卡】"
	title_node.text = upgrade.display_name
	type_node.text = "强化已有装备"
	icon_node.text = upgrade.icon
	desc_node.text = upgrade.description
	rarity_node.text = "升级"

	title_node.add_theme_color_override("font_color", Color(0.55, 1.0, 0.65))
	rarity_node.add_theme_color_override("font_color", Color(0.4, 0.95, 0.55))

	var style = StyleBoxFlat.new()
	style.bg_color = Color(0.10, 0.22, 0.12, 0.95)
	style.border_width_left = 3
	style.border_width_right = 3
	style.border_width_top = 3
	style.border_width_bottom = 3
	style.border_color = Color(0.3, 0.95, 0.45)
	style.corner_radius_top_left = 10
	style.corner_radius_top_right = 10
	style.corner_radius_bottom_left = 10
	style.corner_radius_bottom_right = 10
	style.shadow_size = 8
	style.shadow_color = Color(0, 0, 0, 0.4)
	button.add_theme_stylebox_override("normal", style)

	var hover_style = style.duplicate()
	hover_style.bg_color = style.bg_color.lightened(0.15)
	hover_style.border_color = style.border_color.lightened(0.2)
	hover_style.shadow_size = 12
	button.add_theme_stylebox_override("hover", hover_style)

func _get_upgrade_tooltip_text(upgrade: UpgradeCard) -> String:
	var bb := ""
	var target_name := upgrade.target_ref.card_name if upgrade.target_ref else "未知"
	var count := upgrade.target_ref.upgrade_count if upgrade.target_ref else 0
	bb += "[center][b][color=#66ff99]" + upgrade.display_name + "[/color][/b][/center]\n"
	bb += "[center][color=#aaaaaa]强化目标: " + target_name + " · 已强化 " + str(count) + " 次[/color][/center]\n"
	bb += "[center]━━━━━━━━━━━━━━━━━━━━[/center]\n\n"
	bb += "[color=#88ff88]【强化效果】[/color]\n"
	if upgrade.damage_delta > 0:
		bb += "• 伤害 [color=#88ff88]+" + str(int(upgrade.damage_delta * 100)) + "%[/color]\n"
	if upgrade.attack_speed_delta > 0:
		bb += "• 攻速 [color=#88ff88]+" + str(int(upgrade.attack_speed_delta * 100)) + "%[/color]\n"
	if upgrade.health_delta > 0:
		bb += "• 生命 [color=#88ff88]+" + str(int(upgrade.health_delta)) + "[/color]\n"
	if upgrade.speed_delta > 0:
		bb += "• 速度 [color=#88ff88]+" + str(int(upgrade.speed_delta)) + "[/color]\n"
	if upgrade.crit_delta > 0:
		bb += "• 暴击 [color=#88ff88]+" + str(int(upgrade.crit_delta)) + "%[/color]\n"
	if upgrade.life_steal_delta > 0:
		bb += "• 吸血 [color=#88ff88]+" + str(int(upgrade.life_steal_delta)) + "%[/color]\n"
	if upgrade.freeze_delta > 0:
		bb += "• 冰冻 [color=#88ff88]+" + str(int(upgrade.freeze_delta)) + "%[/color]\n"
	if upgrade.poison_delta > 0:
		bb += "• 中毒 [color=#88ff88]+" + str(int(upgrade.poison_delta)) + "%[/color]\n"
	if not upgrade.effect_upgrades.is_empty():
		bb += "• [color=#88ff88]特效等级 +1[/color]\n"
	if upgrade.tradeoff_type != UpgradeCard.TradeoffType.NONE:
		bb += "\n[color=#ff8888]【副作用】[/color]\n"
		match upgrade.tradeoff_type:
			UpgradeCard.TradeoffType.HEALTH_COST:
				bb += "• 每次攻击额外消耗 [color=#ff8888]+" + str(int(upgrade.tradeoff_value)) + "[/color] 生命\n"
			UpgradeCard.TradeoffType.SPEED:
				bb += "• 速度 [color=#ff8888]" + str(int(upgrade.tradeoff_value)) + "[/color]\n"
			UpgradeCard.TradeoffType.HEALTH_MAX:
				bb += "• 生命上限 [color=#ff8888]" + str(int(upgrade.tradeoff_value)) + "[/color]\n"
	bb += "\n[color=#aaaaaa]选择后将直接强化你已有的装备。[/color]"
	return bb

func _get_weapon_tooltip_text(weapon: WeaponCard) -> String:
	# 使用BBCode格式化tooltip内容
	var bb = ""
	
	# ===== 标题区 =====
	var rarity_color = _get_rarity_color(weapon.rarity)
	bb += "[center][b][color=" + rarity_color + "]" + weapon.card_name + "[/color][/b][/center]\n"
	
	# 类型和稀有度
	var type_text = ""
	if weapon.category == WeaponCard.WeaponCategory.EQUIPMENT:
		type_text = "装备"
	else:
		type_text = weapon.get_weapon_type_text() + "武器"
	type_text += " · Lv." + str(weapon.tier) + " · " + _get_rarity_text(weapon.rarity)
	bb += "[center][color=#aaaaaa]" + type_text + "[/color][/center]\n"
	bb += "[center]━━━━━━━━━━━━━━━━━━━━[/center]\n\n"
	
	# ===== 获取玩家当前数据 =====
	var player: Player = null
	if game_manager:
		player = game_manager.player
	
	# ===== 属性变化区 =====
	bb += "[color=#ffdd88]【属性效果】[/color]\n"
	var has_stats = false
	
	# 伤害
	if weapon.damage_modifier != 1.0:
		var mod = int((weapon.damage_modifier - 1.0) * 100)
		var color = "#88ff88" if mod > 0 else "#ff8888"
		bb += "  [color=" + color + "]● 伤害: " + ("+" if mod > 0 else "") + str(mod) + "%[/color]"
		if player:
			var current_val = int(player.current_damage)
			var new_val = int(player.current_damage * weapon.damage_modifier)
			bb += "  [color=#888888](" + str(current_val) + " → " + str(new_val) + ")[/color]"
		bb += "\n"
		has_stats = true
	
	# 攻速
	if weapon.attack_speed_modifier != 1.0:
		var mod = int((weapon.attack_speed_modifier - 1.0) * 100)
		var color = "#88ff88" if mod > 0 else "#ff8888"
		bb += "  [color=" + color + "]● 攻速: " + ("+" if mod > 0 else "") + str(mod) + "%[/color]\n"
		has_stats = true
	
	# 攻击间隔（武器类型才显示）
	if weapon.category == WeaponCard.WeaponCategory.WEAPON:
		var attack_interval = 0.5 / weapon.attack_speed_modifier
		if weapon.cooldown > 0:
			attack_interval = max(attack_interval, weapon.cooldown)
		bb += "  [color=#88ccff]● 攻击间隔: " + str(snapped(attack_interval, 0.01)) + "秒[/color]"
		if player:
			bb += "  [color=#888888](当前: " + str(snapped(player.attack_cooldown, 0.01)) + "秒)[/color]"
		bb += "\n"
		has_stats = true
	
	# 生命
	if weapon.health_modifier != 0:
		var color = "#88ff88" if weapon.health_modifier > 0 else "#ff8888"
		bb += "  [color=" + color + "]● 生命上限: " + ("+" if weapon.health_modifier > 0 else "") + str(int(weapon.health_modifier)) + "[/color]"
		if player:
			var current_val = player.max_health
			var new_val = current_val + int(weapon.health_modifier)
			bb += "  [color=#888888](" + str(current_val) + " → " + str(new_val) + ")[/color]"
		bb += "\n"
		has_stats = true
	
	# 速度
	if weapon.speed_modifier != 0:
		var color = "#88ff88" if weapon.speed_modifier > 0 else "#ff8888"
		bb += "  [color=" + color + "]● 移动速度: " + ("+" if weapon.speed_modifier > 0 else "") + str(int(weapon.speed_modifier)) + "[/color]"
		if player:
			var current_val = int(player.current_speed)
			var new_val = current_val + int(weapon.speed_modifier)
			bb += "  [color=#888888](" + str(current_val) + " → " + str(new_val) + ")[/color]"
		bb += "\n"
		has_stats = true
	
	# 暴击
	if weapon.critical_bonus > 0:
		bb += "  [color=#ffaa44]● 暴击几率: +" + str(int(weapon.critical_bonus)) + "%[/color]"
		if player:
			var current_val = int(player.critical_chance)
			var new_val = current_val + int(weapon.critical_bonus)
			bb += "  [color=#888888](" + str(current_val) + "% → " + str(new_val) + "%)[/color]"
		bb += "\n"
		has_stats = true
	
	# 吸血
	if weapon.life_steal_bonus > 0:
		bb += "  [color=#ff88cc]● 吸血比例: +" + str(int(weapon.life_steal_bonus)) + "%[/color]"
		if player:
			var current_val = int(player.life_steal_percent)
			var new_val = current_val + int(weapon.life_steal_bonus)
			bb += "  [color=#888888](" + str(current_val) + "% → " + str(new_val) + "%)[/color]"
		bb += "\n"
		has_stats = true
	
	# 冰冻
	if weapon.freeze_bonus > 0:
		bb += "  [color=#88ddff]● 冰冻几率: +" + str(int(weapon.freeze_bonus)) + "%[/color]"
		if player:
			var current_val = int(player.freeze_duration)
			var new_val = current_val + int(weapon.freeze_bonus)
			bb += "  [color=#888888](" + str(current_val) + "% → " + str(new_val) + "%)[/color]"
		bb += "\n"
		has_stats = true
	
	# 中毒
	if weapon.poison_bonus > 0:
		bb += "  [color=#88ff44]● 中毒几率: +" + str(int(weapon.poison_bonus)) + "%[/color]"
		if player:
			var current_val = int(player.poison_damage)
			var new_val = current_val + int(weapon.poison_bonus)
			bb += "  [color=#888888](" + str(current_val) + "% → " + str(new_val) + "%)[/color]"
		bb += "\n"
		has_stats = true
	
	# 护盾领域
	if weapon.grants_aura:
		if player and player.aura.active:
			bb += "  [color=#aaddff]● 护盾领域: 升级 (伤害+3, 范围+15)[/color]\n"
		else:
			bb += "  [color=#aaddff]● 护盾领域: 启用[/color]\n"
		has_stats = true
	
	if not has_stats:
		bb += "  [color=#888888]● 无属性变化[/color]\n"
	
	bb += "\n"
	
	# ===== 特效区 =====
	var has_effects = weapon.melee_effects.size() > 0 or weapon.ranged_effects.size() > 0 or weapon.magic_effects.size() > 0
	
	if has_effects:
		bb += "[b][color=#aa88ff]【特殊效果】[/color][/b]\n"
		for effect in weapon.melee_effects:
			var level = weapon.melee_effect_levels.get(effect, 1)
			bb += "  [color=#ddaaff]★ " + weapon.get_melee_effect_desc(effect, level) + "[/color]\n"
			var detail = weapon.get_effect_detail(WeaponCard.MeleeEffect.keys()[effect], "melee", level)
			if detail != "":
				bb += "     [color=#999999]" + detail + "[/color]\n"
		for effect in weapon.ranged_effects:
			var level = weapon.ranged_effect_levels.get(effect, 1)
			bb += "  [color=#ddaaff]★ " + weapon.get_ranged_effect_desc(effect, level) + "[/color]\n"
			var detail = weapon.get_effect_detail(WeaponCard.RangedEffect.keys()[effect], "ranged", level)
			if detail != "":
				bb += "     [color=#999999]" + detail + "[/color]\n"
		for effect in weapon.magic_effects:
			var level = weapon.magic_effect_levels.get(effect, 1)
			bb += "  [color=#ddaaff]★ " + weapon.get_magic_effect_desc(effect, level) + "[/color]\n"
			var detail = weapon.get_effect_detail(WeaponCard.MagicEffect.keys()[effect], "magic", level)
			if detail != "":
				bb += "     [color=#999999]" + detail + "[/color]\n"
		bb += "\n"
	
	# ===== 风险提示区 =====
	var has_risk = weapon.health_cost > 0 or weapon.health_modifier < 0 or weapon.speed_modifier < 0 or weapon.damage_modifier < 1.0 or weapon.attack_speed_modifier < 1.0
	if has_risk:
		bb += "[color=#ff6666]【⚠ 风险提示】[/color]\n"
		if weapon.health_cost > 0:
			bb += "  [color=#ff8888]● 每次攻击消耗 " + str(weapon.health_cost) + " 点生命[/color]\n"
		if weapon.health_modifier < 0:
			bb += "  [color=#ff8888]● 生命上限降低 " + str(abs(int(weapon.health_modifier))) + " 点[/color]\n"
		if weapon.speed_modifier < 0:
			bb += "  [color=#ff8888]● 移动速度降低 " + str(abs(int(weapon.speed_modifier))) + " 点[/color]\n"
		if weapon.damage_modifier < 1.0:
			bb += "  [color=#ff8888]● 伤害降低 " + str(int((1.0 - weapon.damage_modifier) * 100)) + "%[/color]\n"
		if weapon.attack_speed_modifier < 1.0:
			bb += "  [color=#ff8888]● 攻击速度降低 " + str(int((1.0 - weapon.attack_speed_modifier) * 100)) + "%[/color]\n"
		bb += "\n"
	
	# ===== 替换提示 =====
	if weapon.category == WeaponCard.WeaponCategory.WEAPON:
		var current_weapon_name = ""
		if player:
			for eff in player.active_effects:
				if eff.category == WeaponCard.WeaponCategory.WEAPON:
					current_weapon_name = eff.card_name
					break
		if current_weapon_name != "" and current_weapon_name != weapon.card_name:
			bb += "[color=#ffaa44]⚔ 将替换当前武器: " + current_weapon_name + "[/color]\n"
	
	return bb

func _get_rarity_color(rarity: int) -> String:
	return WeaponFormatter.get_rarity_color(rarity)

func _hide_tooltip_panel():
	if tooltip_panel:
		tooltip_panel.visible = false

func _hide_round_banner(banner: Control):
	if banner:
		banner.visible = false

func _on_ui_viewport_size_changed():
	var viewport_size := get_viewport().get_visible_rect().size
	if controls_panel:
		controls_panel.size = Vector2(min(340.0, viewport_size.x * 0.36), min(520.0, viewport_size.y - 40.0))
		controls_panel.position = Vector2(-controls_panel.size.x - 20.0, 20)
	if round_banner:
		round_banner.custom_minimum_size = Vector2(min(720.0, viewport_size.x - 40.0), 80)

func _on_weapon_selection_started():
	show_weapon_selection()

func _on_round_started(round_number: int):
	update_round_display(round_number)
	show_round_banner("第 " + str(round_number) + " 回合开始")

func update_round_display(round_number: int):
	round_label.text = "🏆 回合 " + str(round_number)
	round_label.tooltip_text = "当前回合: " + str(round_number)

func show_round_banner(text: String):
	if not round_banner:
		return
	round_banner.text = text
	round_banner.visible = true
	round_banner.modulate.a = 0.0
	round_banner.scale = Vector2(0.8, 0.8)
	
	# 入场动画
	var tween = create_tween()
	tween.parallel().tween_property(round_banner, "modulate:a", 1.0, 0.3).set_trans(Tween.TRANS_CUBIC)
	tween.parallel().tween_property(round_banner, "scale", Vector2(1.1, 1.1), 0.3).set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	tween.tween_property(round_banner, "scale", Vector2(1.0, 1.0), 0.15)
	
	# 停留
	tween.tween_interval(0.8)
	
	# 退场动画
	tween.parallel().tween_property(round_banner, "modulate:a", 0.0, 0.4)
	tween.parallel().tween_property(round_banner, "scale", Vector2(1.15, 1.15), 0.4)
	tween.tween_callback(_hide_round_banner.bind(round_banner))

func update_score_display(new_score: int):
	if score_label:
		score_label.text = str(new_score)

func update_health_display(current_health: int, max_health: int):
	if health_bar:
		health_bar.max_value = max_health
		health_bar.value = current_health
		var health_percent = float(current_health) / float(max_health)
		var fill_style = health_bar.get_theme_stylebox("fill")
		if fill_style:
			fill_style = fill_style.duplicate()
			if health_percent > 0.6:
				fill_style.bg_color = Color(0.22, 0.55, 0.22)  # 暗绿
			elif health_percent > 0.3:
				fill_style.bg_color = Color(0.65, 0.60, 0.15)  # 暗黄
			else:
				fill_style.bg_color = Color(0.70, 0.15, 0.12)  # 暗红
			health_bar.add_theme_stylebox_override("fill", fill_style)

		# 更新血量数值标签
		if health_label:
			health_label.text = str(current_health) + "/" + str(max_health)

		# 低血量暗角效果
		update_low_hp_vignette(health_percent)

		if health_percent <= 0.3:
			if not low_health_tween or not low_health_tween.is_running():
				low_health_tween = create_tween()
				low_health_tween.set_loops()
				low_health_tween.tween_property(health_bar, "modulate:a", 0.6, 0.35)
				low_health_tween.tween_property(health_bar, "modulate:a", 1.0, 0.35)
		else:
			if low_health_tween and low_health_tween.is_running():
				low_health_tween.kill()
			health_bar.modulate.a = 1.0

func update_zombies_display(zombies_remaining: int, total_zombies: int):
	if zombies_label:
		zombies_label.text = "剩余: " + str(zombies_remaining) + "/" + str(total_zombies)

func update_player_status(player: Player):
	if damage_label:
		damage_label.text = "攻击 " + str(int(player.current_damage))
	if crit_label:
		crit_label.text = "暴击 " + str(int(player.critical_chance)) + "%"
	if lifesteal_label:
		lifesteal_label.text = "吸血 " + str(int(player.life_steal_percent)) + "%"
	if speed_label:
		speed_label.text = "速度 " + str(int(player.current_speed))
	if attack_type_label:
		match player.current_attack_type:
			Player.AttackType.MELEE:
				attack_type_label.text = "近战"
			Player.AttackType.RANGED:
				attack_type_label.text = "远程"
			Player.AttackType.AREA:
				attack_type_label.text = "范围"
			Player.AttackType.MAGIC:
				attack_type_label.text = "魔法"
	
	if target_priority_label:
		match player.target_priority:
			0:
				target_priority_label.text = "锁定: 最近"
			1:
				target_priority_label.text = "锁定: 最远"
			2:
				target_priority_label.text = "锁定: 密集"

	
	# 更新效果图标显示
	_update_effects_display(player)
	_update_status_tooltips(player)

func _update_status_tooltips(player: Player):
	if damage_label:
		damage_label.tooltip_text = "攻击力: " + str(int(player.current_damage)) + "\n基础伤害与倍率已计入"
	if crit_label:
		crit_label.tooltip_text = "暴击概率: " + str(int(player.critical_chance)) + "%\n暴击伤害倍率: x" + str(snapped(player.crit_multiplier, 0.1))
	if lifesteal_label:
		lifesteal_label.tooltip_text = "吸血比例: " + str(int(player.life_steal_percent)) + "%\n命中后回复对应比例生命"
	if speed_label:
		speed_label.tooltip_text = "移动速度: " + str(int(player.current_speed)) + "\n受装备、升级和减速效果影响"
	if target_priority_label:
		var priority_text = "最近"
		if player.target_priority == 1:
			priority_text = "最远"
		elif player.target_priority == 2:
			priority_text = "密集"
		target_priority_label.tooltip_text = "目标优先级: " + priority_text + "\n按 Tab 切换"
	if attack_type_label:
		attack_type_label.tooltip_text = WeaponFormatter.build_attack_effects_tooltip(player)

func _update_effects_display(player: Player):
	if not effects_container:
		return
	
	# 清除旧图标
	for child in effects_container.get_children():
		child.queue_free()
	effect_icons.clear()
	
	# 收集所有激活的效果
	var active_items: Array[Dictionary] = []
	
	# 1. 当前武器
	for effect in player.active_effects:
		if effect.category == WeaponCard.WeaponCategory.WEAPON:
			active_items.append({
				"icon": effect.icon,
				"name": effect.card_name,
				"type": "weapon",
				"color": _get_rarity_color_value(effect.rarity),
				"tooltip": WeaponFormatter.get_effect_tooltip(effect)
			})
			break

	# 2. 装备
	for effect in player.active_effects:
		if effect.category == WeaponCard.WeaponCategory.EQUIPMENT:
			active_items.append({
				"icon": effect.icon,
				"name": effect.card_name,
				"type": "equipment",
				"color": _get_rarity_color_value(effect.rarity),
				"tooltip": WeaponFormatter.get_effect_tooltip(effect)
			})
	
	# 3. 护盾 (如果激活)
	if player.aura.active:
		active_items.append({
			"icon": "❄️",
			"name": "冰霜护盾",
			"type": "buff",
			"color": Color(0.3, 0.8, 1.0),
			"tooltip": "【冰霜护盾】\n伤害: " + str(int(player.aura.damage)) + "\n范围: " + str(int(player.aura.radius)) + "\n效果: 环绕的冰晶会伤害并减速接触的敌人"
		})
	
	# 4. 特殊属性
	if player.critical_chance > 0:
		active_items.append({
			"icon": "💥",
			"name": "暴击",
			"type": "stat",
			"color": Color(1.0, 0.6, 0.2),
			"tooltip": "【暴击】\n几率: " + str(int(player.critical_chance)) + "%\n伤害: x2.0"
		})
	
	if player.life_steal_percent > 0:
		active_items.append({
			"icon": "🩸",
			"name": "吸血",
			"type": "stat",
			"color": Color(0.9, 0.3, 0.3),
			"tooltip": "【吸血】\n比例: " + str(int(player.life_steal_percent)) + "%\n效果: 造成伤害时回复生命"
		})
	
	if player.freeze_duration > 0:
		active_items.append({
			"icon": "🧊",
			"name": "冰冻",
			"type": "stat",
			"color": Color(0.4, 0.8, 1.0),
			"tooltip": "【冰冻】\n几率: " + str(int(player.freeze_duration)) + "%\n效果: 攻击有几率冻结敌人"
		})
	
	if player.poison_damage > 0:
		active_items.append({
			"icon": "☠️",
			"name": "中毒",
			"type": "stat",
			"color": Color(0.4, 0.9, 0.3),
			"tooltip": "【中毒】\n几率: " + str(int(player.poison_damage)) + "%\n效果: 攻击有几率使敌人中毒"
		})
	
	if player.shield_amount > 0:
		active_items.append({
			"icon": "🛡️",
			"name": "护盾",
			"type": "buff",
			"color": Color(0.6, 0.8, 1.0),
			"tooltip": "【护盾】\n剩余: " + str(player.shield_amount) + "\n效果: 吸收伤害"
		})
	
	# 创建图标按钮
	for item in active_items:
		var btn = Button.new()
		btn.text = item["icon"]
		btn.custom_minimum_size = Vector2(28, 24)
		btn.add_theme_font_size_override("font_size", 14)
		btn.focus_mode = Control.FOCUS_NONE
		btn.flat = true
		
		# 设置背景色
		var style = StyleBoxFlat.new()
		style.bg_color = item["color"].darkened(0.6)
		style.bg_color.a = 0.7
		style.border_width_bottom = 2
		style.border_color = item["color"]
		style.corner_radius_top_left = 4
		style.corner_radius_top_right = 4
		style.corner_radius_bottom_left = 4
		style.corner_radius_bottom_right = 4
		btn.add_theme_stylebox_override("normal", style)
		
		var hover_style = style.duplicate()
		hover_style.bg_color = item["color"].darkened(0.4)
		btn.add_theme_stylebox_override("hover", hover_style)
		
		# 连接鼠标事件显示自定义tooltip
		var effect_tooltip = item["tooltip"]
		btn.mouse_entered.connect(_on_effect_icon_entered.bind(effect_tooltip))
		btn.mouse_exited.connect(_on_effect_icon_exited)
		
		effects_container.add_child(btn)
		effect_icons[item["name"]] = btn
	
	# 如果没有任何效果，显示提示
	if active_items.is_empty():
		var hint = Label.new()
		hint.text = "无"
		hint.add_theme_font_size_override("font_size", 11)
		hint.add_theme_color_override("font_color", Color(0.5, 0.5, 0.6))
		effects_container.add_child(hint)

func _on_effect_icon_entered(effect_tip: String):
	_show_tooltip(effect_tip)

func _on_effect_icon_exited():
	if tooltip_hide_timer:
		tooltip_hide_timer.start()

func _get_rarity_color_value(rarity: int) -> Color:
	return WeaponFormatter.get_rarity_color_value(rarity)

func set_game_manager(manager: GameManager):
	game_manager = manager
	_setup_connections()

func set_weapon_deck(deck: WeaponDeck):
	weapon_deck = deck

# ═══════════════════════════════════════════════
# 委托方法 — 保持 call_group("ui_manager", ...) 兼容
# ═══════════════════════════════════════════════

func show_game_over():
	game_over_ui.setup(game_manager)
	game_over_ui.show_game_over(hud_panel)

func show_damage_number(pos: Vector2, amount: int, is_critical: bool = false):
	if screen_fx: screen_fx.show_damage_number(pos, amount, is_critical)

func flash_damage():
	if screen_fx: screen_fx.flash_damage()

func shake_screen(intensity: float):
	if screen_fx: screen_fx.shake_screen(intensity)

func flash_attack_color(attack_color: Color):
	if screen_fx: screen_fx.flash_attack_color(attack_color)

func add_combo():
	if screen_fx: screen_fx.add_combo()

func show_kill_effect(pos: Vector2, points: int):
	if screen_fx: screen_fx.show_kill_effect(pos, points)

func update_low_hp_vignette(health_percent: float):
	if screen_fx: screen_fx.update_low_hp_vignette(health_percent)

func show_offscreen_indicator(direction: Vector2, danger_level: float):
	if screen_fx: screen_fx.show_offscreen_indicator(direction, danger_level)

func trigger_boss_atmosphere():
	if screen_fx: screen_fx.trigger_boss_atmosphere()
