class_name WeaponFormatter
## 武器/效果格式化工具类
## 集中管理稀有度显示、武器描述、效果摘要等格式化逻辑

# ═══════════════════════════════════════════════
# 稀有度工具
# ═══════════════════════════════════════════════

static func get_rarity_text(rarity: int) -> String:
	match rarity:
		1: return "[普通]"
		2: return "[稀有]"
		3: return "[史诗]"
		4: return "[传说]"
		_: return "[未知]"

static func get_rarity_color(rarity: int) -> String:
	match rarity:
		1: return "#cccccc"
		2: return "#4499ff"
		3: return "#cc66ff"
		4: return "#ffcc00"
		_: return "#ffffff"

static func get_rarity_color_value(rarity: int) -> Color:
	match rarity:
		1: return Color(0.7, 0.7, 0.7)
		2: return Color(0.3, 0.6, 1.0)
		3: return Color(0.7, 0.3, 0.9)
		4: return Color(1.0, 0.8, 0.2)
		_: return Color.WHITE

# ═══════════════════════════════════════════════
# 效果摘要
# ═══════════════════════════════════════════════

static func get_effect_level_summary(player: Player) -> Array[String]:
	var melee_levels = {}
	var ranged_levels = {}
	var magic_levels = {}
	for weapon in player.active_effects:
		if weapon.weapon_type == WeaponCard.WeaponType.MELEE:
			for effect in weapon.melee_effects:
				var level = weapon.melee_effect_levels.get(effect, 1)
				if melee_levels.get(effect, 0) < level:
					melee_levels[effect] = level
		elif weapon.weapon_type == WeaponCard.WeaponType.RANGED:
			for effect in weapon.ranged_effects:
				var level = weapon.ranged_effect_levels.get(effect, 1)
				if ranged_levels.get(effect, 0) < level:
					ranged_levels[effect] = level
		elif weapon.weapon_type == WeaponCard.WeaponType.MAGIC:
			for effect in weapon.magic_effects:
				var level = weapon.magic_effect_levels.get(effect, 1)
				if magic_levels.get(effect, 0) < level:
					magic_levels[effect] = level

	var result: Array[String] = []
	var temp_card = WeaponCard.new()
	for effect in melee_levels.keys():
		result.append(temp_card.get_melee_effect_desc(effect, melee_levels[effect]))
	for effect in ranged_levels.keys():
		result.append(temp_card.get_ranged_effect_desc(effect, ranged_levels[effect]))
	for effect in magic_levels.keys():
		result.append(temp_card.get_magic_effect_desc(effect, magic_levels[effect]))
	return result

static func get_effect_tooltip(weapon: WeaponCard) -> String:
	var text = "【" + weapon.card_name + "】\n"
	text += "类型: " + ("装备" if weapon.category == WeaponCard.WeaponCategory.EQUIPMENT else weapon.get_weapon_type_text()) + "\n"
	text += "等级: Lv." + str(weapon.tier) + "\n"
	text += "稀有度: " + get_rarity_text(weapon.rarity) + "\n\n"

	if weapon.damage_modifier != 1.0:
		var mod = int((weapon.damage_modifier - 1.0) * 100)
		text += "伤害: " + ("+" if mod > 0 else "") + str(mod) + "%\n"
	if weapon.health_modifier != 0:
		text += "生命: " + ("+" if weapon.health_modifier > 0 else "") + str(int(weapon.health_modifier)) + "\n"
	if weapon.speed_modifier != 0:
		text += "速度: " + ("+" if weapon.speed_modifier > 0 else "") + str(int(weapon.speed_modifier)) + "\n"
	if weapon.critical_bonus > 0:
		text += "暴击: +" + str(int(weapon.critical_bonus)) + "%\n"
	if weapon.life_steal_bonus > 0:
		text += "吸血: +" + str(int(weapon.life_steal_bonus)) + "%\n"
	return text

static func build_active_effects_summary(player: Player) -> String:
	if player.active_effects.is_empty():
		return ""
	var current_weapon = "无"
	var effects = get_effect_level_summary(player)
	for effect in player.active_effects:
		if effect.category == WeaponCard.WeaponCategory.WEAPON:
			current_weapon = effect.card_name
			break
	if effects.is_empty():
		return "🎒 " + current_weapon
	return "🎒 " + current_weapon + " | " + " | ".join(effects)

static func build_active_effects_tooltip(player: Player) -> String:
	if player.active_effects.is_empty():
		return "已获得: 无"
	var current_weapon = "无"
	for effect in player.active_effects:
		if effect.category == WeaponCard.WeaponCategory.WEAPON:
			current_weapon = effect.card_name
			break
	var effects = get_effect_level_summary(player)
	if effects.is_empty():
		return "当前武器: " + current_weapon + "\n特效: 无"
	return "当前武器: " + current_weapon + "\n特效:\n- " + "\n- ".join(effects)

static func build_attack_effects_tooltip(player: Player) -> String:
	var lines = []
	var type_text = "近战"
	if player.current_attack_type == Player.AttackType.RANGED:
		type_text = "远程"
	elif player.current_attack_type == Player.AttackType.MAGIC:
		type_text = "魔法"
	elif player.current_attack_type == Player.AttackType.AREA:
		type_text = "范围"
	var weapon_name = "无"
	for effect in player.active_effects:
		if effect.category == WeaponCard.WeaponCategory.WEAPON:
			weapon_name = effect.card_name
			break
	lines.append("当前武器: " + weapon_name)
	lines.append("攻击类型: " + type_text)
	lines.append("基础伤害: " + str(int(player.current_damage)))

	var melee_levels = {}
	var ranged_levels = {}
	var magic_levels = {}
	for weapon in player.active_effects:
		if weapon.weapon_type == WeaponCard.WeaponType.MELEE:
			for effect in weapon.melee_effects:
				var level = weapon.melee_effect_levels.get(effect, 1)
				if melee_levels.get(effect, 0) < level:
					melee_levels[effect] = level
		elif weapon.weapon_type == WeaponCard.WeaponType.RANGED:
			for effect in weapon.ranged_effects:
				var level = weapon.ranged_effect_levels.get(effect, 1)
				if ranged_levels.get(effect, 0) < level:
					ranged_levels[effect] = level
		elif weapon.weapon_type == WeaponCard.WeaponType.MAGIC:
			for effect in weapon.magic_effects:
				var level = weapon.magic_effect_levels.get(effect, 1)
				if magic_levels.get(effect, 0) < level:
					magic_levels[effect] = level

	var effect_lines = []
	var temp_card = WeaponCard.new()
	if player.current_attack_type == Player.AttackType.MELEE:
		for effect in melee_levels.keys():
			effect_lines.append("• " + temp_card.get_melee_effect_desc(effect, melee_levels[effect]))
	elif player.current_attack_type == Player.AttackType.RANGED:
		for effect in ranged_levels.keys():
			effect_lines.append("• " + temp_card.get_ranged_effect_desc(effect, ranged_levels[effect]))
		if player.returning_projectiles and player.bounce_count > 0:
			effect_lines.append("• 反弹次数: " + str(player.bounce_count))
	elif player.current_attack_type == Player.AttackType.MAGIC:
		for effect in magic_levels.keys():
			effect_lines.append("• " + temp_card.get_magic_effect_desc(effect, magic_levels[effect]))
	elif player.current_attack_type == Player.AttackType.AREA:
		effect_lines.append("• 范围爆炸")

	if player.critical_chance > 0:
		effect_lines.append("• 暴击率 +" + str(int(player.critical_chance)) + "%")
	if player.life_steal_percent > 0:
		effect_lines.append("• 吸血 +" + str(int(player.life_steal_percent)) + "%")
	if player.freeze_duration > 0:
		effect_lines.append("• 冰冻几率 +" + str(int(player.freeze_duration)) + "%")
	if player.poison_damage > 0:
		effect_lines.append("• 中毒几率 +" + str(int(player.poison_damage)) + "%")
	if player.aura.active:
		effect_lines.append("• 护盾领域: 启用")

	if effect_lines.is_empty():
		effect_lines.append("• 无特效")
	lines.append("特效:")
	lines.append("\n".join(effect_lines))
	return "\n".join(lines)
