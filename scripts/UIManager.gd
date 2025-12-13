class_name UIManager
extends Control

signal weapon_card_selected(weapon: WeaponCard)

var weapon_selection_panel: Panel
var weapon_buttons: Array[Button] = []
var hud_panel: Panel
var health_label: Label
var round_label: Label
var zombies_label: Label
var damage_label: Label
var ammo_label: Label
var attack_type_label: Label
var owned_weapons_label: Label
var controls_panel: Panel
var controls_visible: bool = false
var welcome_panel: Panel
var welcome_visible: bool = true

var current_weapon_options: Array[WeaponCard] = []
var game_manager: GameManager
var weapon_deck: WeaponDeck

func _ready():
	_create_ui_elements()
	_setup_connections()

func _create_ui_elements():
	# 创建HUD面板 - 使用响应式布局
	_create_hud_panel()
	
	# 创建武器选择面板
	_create_weapon_selection_panel()
	
	# 创建操作说明面板
	_create_controls_panel()
	
	# 创建欢迎界面
	_create_welcome_panel()

func _create_hud_panel():
	hud_panel = Panel.new()
	hud_panel.set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	hud_panel.size.y = 120  # 增加高度以容纳更多信息
	# 设置HUD背景样式
	var hud_style = StyleBoxFlat.new()
	hud_style.bg_color = Color(0.1, 0.1, 0.1, 0.9)
	hud_style.border_width_bottom = 2
	hud_style.border_color = Color(0.3, 0.3, 0.3)
	hud_panel.add_theme_stylebox_override("panel", hud_style)
	add_child(hud_panel)
	
	# 创建主容器
	var main_container = VBoxContainer.new()
	main_container.position = Vector2(20, 10)
	main_container.add_theme_constant_override("separation", 5)
	hud_panel.add_child(main_container)
	
	# 第一行：基础信息
	var top_container = HBoxContainer.new()
	top_container.add_theme_constant_override("separation", 30)
	main_container.add_child(top_container)
	
	# 生命值标签
	health_label = Label.new()
	health_label.text = "❤️ 100/100"
	health_label.add_theme_font_size_override("font_size", 20)
	health_label.add_theme_color_override("font_color", Color.RED)
	top_container.add_child(health_label)
	
	# 回合标签
	round_label = Label.new()
	round_label.text = "🏆 回合 1"
	round_label.add_theme_font_size_override("font_size", 20)
	round_label.add_theme_color_override("font_color", Color.GOLD)
	top_container.add_child(round_label)
	
	# 僵尸数量标签
	zombies_label = Label.new()
	zombies_label.text = "🧟 剩余 0"
	zombies_label.add_theme_font_size_override("font_size", 20)
	zombies_label.add_theme_color_override("font_color", Color.GREEN)
	top_container.add_child(zombies_label)
	
	# 第二行：战斗信息
	var battle_container = HBoxContainer.new()
	battle_container.add_theme_constant_override("separation", 30)
	main_container.add_child(battle_container)
	
	# 攻击力标签
	damage_label = Label.new()
	damage_label.text = "⚔️ 攻击 10"
	damage_label.add_theme_font_size_override("font_size", 18)
	damage_label.add_theme_color_override("font_color", Color.ORANGE)
	battle_container.add_child(damage_label)
	
	# 子弹标签
	ammo_label = Label.new()
	ammo_label.text = "🔫 10/10"
	ammo_label.add_theme_font_size_override("font_size", 18)
	ammo_label.add_theme_color_override("font_color", Color.YELLOW)
	battle_container.add_child(ammo_label)
	
	# 攻击类型标签
	attack_type_label = Label.new()
	attack_type_label.text = "⚔️ 近战"
	attack_type_label.add_theme_font_size_override("font_size", 18)
	attack_type_label.add_theme_color_override("font_color", Color.CYAN)
	battle_container.add_child(attack_type_label)
	
	# 第三行：已拥有武器
	var weapons_container = HBoxContainer.new()
	main_container.add_child(weapons_container)
	
	owned_weapons_label = Label.new()
	owned_weapons_label.text = "🎒 武器: 无"
	owned_weapons_label.add_theme_font_size_override("font_size", 16)
	owned_weapons_label.add_theme_color_override("font_color", Color.LIGHT_GRAY)
	weapons_container.add_child(owned_weapons_label)
	
	# 操作说明按钮 - 右上角
	var controls_button = Button.new()
	controls_button.text = "❓ 帮助"
	controls_button.add_theme_font_size_override("font_size", 18)
	controls_button.custom_minimum_size = Vector2(100, 40)
	controls_button.position = Vector2(get_viewport().get_visible_rect().size.x - 120, 15)
	controls_button.pressed.connect(_toggle_controls_panel)
	hud_panel.add_child(controls_button)

func _create_weapon_selection_panel():
	weapon_selection_panel = Panel.new()
	weapon_selection_panel.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	weapon_selection_panel.visible = false
	# 设置武器选择背景
	var weapon_style = StyleBoxFlat.new()
	weapon_style.bg_color = Color(0, 0, 0, 0.85)
	weapon_selection_panel.add_theme_stylebox_override("panel", weapon_style)
	add_child(weapon_selection_panel)
	
	# 创建居中容器
	var center_container = CenterContainer.new()
	center_container.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	weapon_selection_panel.add_child(center_container)
	
	# 创建主容器
	var main_vbox = VBoxContainer.new()
	main_vbox.add_theme_constant_override("separation", 40)
	center_container.add_child(main_vbox)
	
	# 武器选择标题
	var title_label = Label.new()
	title_label.text = "⚔️ 选择武器强化 ⚔️"
	title_label.add_theme_font_size_override("font_size", 42)
	title_label.add_theme_color_override("font_color", Color.GOLD)
	title_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	main_vbox.add_child(title_label)
	
	# 说明文字
	var desc_label = Label.new()
	desc_label.text = "选择一张武器卡牌来增强你的战斗能力"
	desc_label.add_theme_font_size_override("font_size", 22)
	desc_label.add_theme_color_override("font_color", Color.WHITE)
	desc_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	main_vbox.add_child(desc_label)
	
	# 创建武器按钮容器
	var button_container = HBoxContainer.new()
	button_container.add_theme_constant_override("separation", 30)
	button_container.alignment = BoxContainer.ALIGNMENT_CENTER
	main_vbox.add_child(button_container)
	
	# 创建三个武器按钮
	for i in range(3):
		var button = Button.new()
		button.custom_minimum_size = Vector2(280, 200)
		button.add_theme_font_size_override("font_size", 16)
		# 设置按钮样式
		var button_style = StyleBoxFlat.new()
		button_style.bg_color = Color(0.2, 0.2, 0.3, 0.9)
		button_style.border_width_left = 3
		button_style.border_width_right = 3
		button_style.border_width_top = 3
		button_style.border_width_bottom = 3
		button_style.border_color = Color.WHITE
		button_style.corner_radius_top_left = 10
		button_style.corner_radius_top_right = 10
		button_style.corner_radius_bottom_left = 10
		button_style.corner_radius_bottom_right = 10
		button.add_theme_stylebox_override("normal", button_style)
		button.pressed.connect(_on_weapon_button_pressed.bind(i))
		button_container.add_child(button)
		weapon_buttons.append(button)

func _create_controls_panel():
	# 创建侧边栏样式的控制面板
	controls_panel = Panel.new()
	controls_panel.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
	controls_panel.size = Vector2(350, 500)
	controls_panel.position = Vector2(-370, 20)  # 右侧边距20px
	controls_panel.visible = false
	
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
	title_label.add_theme_font_size_override("font_size", 24)
	title_label.add_theme_color_override("font_color", Color(0.4, 0.8, 1.0))
	title_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	container.add_child(title_label)
	
	# 分隔线
	var separator = HSeparator.new()
	separator.add_theme_color_override("separator", Color(0.4, 0.6, 1.0, 0.5))
	container.add_child(separator)
	
	# 创建信息卡片
	_create_info_card(container, "🏃 移动", "WASD 或 方向键\n自由移动角色", Color(0.3, 0.8, 0.3))
	_create_info_card(container, "⚔️ 攻击", "自动攻击附近僵尸\n攻击类型取决于武器", Color(0.8, 0.3, 0.3))
	_create_info_card(container, "🗡️ 武器类型", "近战: 扇形斩击\n远程: 发射子弹\n魔法: 追踪导弹\n范围: 爆炸攻击", Color(0.8, 0.6, 0.2))
	_create_info_card(container, "🎯 目标", "消灭所有僵尸\n选择武器卡牌\n生存更多回合", Color(0.6, 0.3, 0.8))
	
	# 关闭提示
	var close_hint = Label.new()
	close_hint.text = "💡 按 H 键切换显示"
	close_hint.add_theme_font_size_override("font_size", 14)
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
	title_label.add_theme_font_size_override("font_size", 16)
	title_label.add_theme_color_override("font_color", accent_color)
	card_container.add_child(title_label)
	
	# 内容
	var content_label = Label.new()
	content_label.text = content
	content_label.add_theme_font_size_override("font_size", 13)
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
	# 创建半透明黑色背景
	var style_box = StyleBoxFlat.new()
	style_box.bg_color = Color(0, 0, 0, 0.8)
	welcome_panel.add_theme_stylebox_override("panel", style_box)
	add_child(welcome_panel)
	
	# 创建欢迎内容容器
	var center_container = CenterContainer.new()
	center_container.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	welcome_panel.add_child(center_container)
	
	var vbox = VBoxContainer.new()
	vbox.add_theme_constant_override("separation", 30)
	center_container.add_child(vbox)
	
	# 游戏标题
	var title = Label.new()
	title.text = "🧟 僵尸大战 🧟"
	title.add_theme_font_size_override("font_size", 48)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	vbox.add_child(title)
	
	# 副标题
	var subtitle = Label.new()
	subtitle.text = "Zombie vs Survivors"
	subtitle.add_theme_font_size_override("font_size", 24)
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
			label.custom_minimum_size.y = 10
		else:
			label.add_theme_font_size_override("font_size", 20)
		label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		vbox.add_child(label)
	
	# 开始游戏按钮
	var start_button = Button.new()
	start_button.text = "开始游戏"
	start_button.add_theme_font_size_override("font_size", 28)
	start_button.custom_minimum_size = Vector2(200, 60)
	start_button.pressed.connect(_start_game)
	vbox.add_child(start_button)
	
	# 操作说明按钮
	var help_button = Button.new()
	help_button.text = "操作说明"
	help_button.add_theme_font_size_override("font_size", 20)
	help_button.custom_minimum_size = Vector2(150, 50)
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
	
	# 隐藏操作说明面板
	if controls_visible:
		_toggle_controls_panel()
	
	if game_manager and game_manager.player:
		current_weapon_options = weapon_deck.draw_three_cards(game_manager.player)
	else:
		current_weapon_options = weapon_deck.draw_three_cards()
	
	# 更新按钮显示
	for i in range(3):
		if i < current_weapon_options.size():
			var weapon = current_weapon_options[i]
			var button = weapon_buttons[i]
			
			# 设置按钮文本 - 更好的格式化
			var button_text = "【" + weapon.card_name + "】\n"
			button_text += _get_weapon_type_text(weapon.weapon_type) + "\n\n"
			button_text += weapon.get_effect_description() + "\n\n"
			button_text += _get_rarity_text(weapon.rarity)
			
			button.text = button_text
			button.visible = true
			
			# 根据稀有度设置颜色和样式
			match weapon.rarity:
				1:  # 普通
					button.modulate = Color(0.9, 0.9, 0.9)
				2:  # 稀有
					button.modulate = Color(0.5, 0.8, 1.0)
				3:  # 史诗
					button.modulate = Color(0.8, 0.5, 1.0)
				4:  # 传说
					button.modulate = Color(1.0, 0.8, 0.3)
		else:
			weapon_buttons[i].visible = false
	
	weapon_selection_panel.visible = true

func hide_weapon_selection():
	weapon_selection_panel.visible = false

func _get_rarity_text(rarity: int) -> String:
	match rarity:
		1:
			return "[普通]"
		2:
			return "[稀有]"
		3:
			return "[史诗]"
		4:
			return "[传说]"
		_:
			return "[未知]"

func _get_weapon_type_text(weapon_type: WeaponCard.WeaponType) -> String:
	match weapon_type:
		WeaponCard.WeaponType.MELEE:
			return "⚔️ 近战武器"
		WeaponCard.WeaponType.RANGED:
			return "🏹 远程武器"
		WeaponCard.WeaponType.MAGIC:
			return "✨ 魔法武器"
		WeaponCard.WeaponType.SUPPORT:
			return "🛡️ 辅助武器"
		_:
			return "❓ 未知类型"

func _on_weapon_button_pressed(index: int):
	if index < current_weapon_options.size():
		var selected_weapon = current_weapon_options[index]
		weapon_card_selected.emit(selected_weapon)
		hide_weapon_selection()

func _on_weapon_selection_started():
	show_weapon_selection()

func _on_round_started(round_number: int):
	update_round_display(round_number)

func update_round_display(round_number: int):
	round_label.text = "🏆 回合 " + str(round_number)

func update_health_display(current_health: int, max_health: int):
	health_label.text = "❤️ " + str(current_health) + "/" + str(max_health)
	# 根据生命值改变颜色
	var health_percent = float(current_health) / float(max_health)
	if health_percent > 0.6:
		health_label.add_theme_color_override("font_color", Color.GREEN)
	elif health_percent > 0.3:
		health_label.add_theme_color_override("font_color", Color.YELLOW)
	else:
		health_label.add_theme_color_override("font_color", Color.RED)

func update_zombies_display(zombies_remaining: int):
	zombies_label.text = "🧟 剩余 " + str(zombies_remaining)

func update_player_status(player: Player):
	# 更新攻击力
	if damage_label:
		damage_label.text = "⚔️ 攻击 " + str(int(player.current_damage))
	
	# 更新子弹信息
	if ammo_label:
		if player.is_reloading:
			var reload_progress = int((player.reload_time - player.reload_timer) / player.reload_time * 100)
			ammo_label.text = "🔄 " + str(reload_progress) + "%"
			ammo_label.add_theme_color_override("font_color", Color.ORANGE)
		else:
			ammo_label.text = "🔫 " + str(player.current_ammo) + "/" + str(player.max_ammo)
			ammo_label.add_theme_color_override("font_color", Color.YELLOW)
	
	# 更新攻击类型
	if attack_type_label:
		match player.current_attack_type:
			Player.AttackType.MELEE:
				attack_type_label.text = "⚔️ 近战"
			Player.AttackType.RANGED:
				attack_type_label.text = "🏹 远程"
			Player.AttackType.AREA:
				attack_type_label.text = "💥 范围"
			Player.AttackType.MAGIC:
				attack_type_label.text = "✨ 魔法"
	
	# 更新已拥有武器
	if owned_weapons_label:
		if player.active_effects.size() > 0:
			var weapon_names = []
			for weapon in player.active_effects:
				weapon_names.append(weapon.card_name)
			owned_weapons_label.text = "🎒 武器: " + ", ".join(weapon_names)
		else:
			owned_weapons_label.text = "🎒 武器: 无"

func set_game_manager(manager: GameManager):
	game_manager = manager
	_setup_connections()

func set_weapon_deck(deck: WeaponDeck):
	weapon_deck = deck

func show_game_over():
	var game_over_panel = Panel.new()
	game_over_panel.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	game_over_panel.size = Vector2(400, 200)
	add_child(game_over_panel)
	
	var game_over_label = Label.new()
	game_over_label.text = "游戏结束!"
	game_over_label.add_theme_font_size_override("font_size", 32)
	game_over_label.position = Vector2(150, 50)
	game_over_panel.add_child(game_over_label)
	
	var round_info_label = Label.new()
	if game_manager:
		round_info_label.text = "坚持了 " + str(game_manager.get_current_round()) + " 回合"
	else:
		round_info_label.text = "坚持了 0 回合"
	round_info_label.add_theme_font_size_override("font_size", 16)
	round_info_label.position = Vector2(150, 100)
	game_over_panel.add_child(round_info_label)
	
	var restart_button = Button.new()
	restart_button.text = "重新开始"
	restart_button.size = Vector2(120, 40)
	restart_button.position = Vector2(140, 140)
	restart_button.pressed.connect(_on_restart_pressed)
	game_over_panel.add_child(restart_button)

func _on_restart_pressed():
	get_tree().reload_current_scene()
