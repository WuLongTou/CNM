class_name WeaponDeck
extends Node
## 武器牌组管理
## 负责：武器库初始化、卡牌抽取逻辑（智能推荐升级路线）

var all_weapons: Array[WeaponCard] = []
var prerequisites: Dictionary = {}

func _ready():
	all_weapons = WeaponFactory.build_all()
	prerequisites = WeaponFactory.get_prerequisites()
	print("武器库初始化完成: " + str(all_weapons.size()) + " 种武器")

# ═══════════════════════════════════════════════
# 抽卡系统
# ═══════════════════════════════════════════════

func draw_three_cards(player: Player = null) -> Array:
	var pool: Array = []

	if player == null:
		pool = all_weapons.duplicate()
	else:
		pool = _build_smart_pool(player)

	if pool.is_empty():
		pool = all_weapons.duplicate()

	pool.shuffle()

	# 去重抽取
	var drawn: Array = []
	var names: Array[String] = []
	for card in pool:
		if drawn.size() >= 3:
			break
		var card_id: String = card.card_name
		if not names.has(card_id):
			drawn.append(card)
			names.append(card_id)

	# 如果池子太小，从全库补充
	if drawn.size() < 3:
		var backup = all_weapons.duplicate()
		backup.shuffle()
		for card in backup:
			if drawn.size() >= 3:
				break
			if not names.has(card.card_name):
				drawn.append(card)
				names.append(card.card_name)

	return drawn

func get_random_weapon() -> WeaponCard:
	return all_weapons.pick_random() if all_weapons.size() > 0 else null

# ═══════════════════════════════════════════════
# 智能池构建
# ═══════════════════════════════════════════════

func _build_smart_pool(player: Player) -> Array:
	var inventory_names: Array[String] = []
	var current_type = null
	var current_tier: int = 0
	var player_fx := { "melee": {}, "ranged": {}, "magic": {} }

	for w in player.active_effects:
		inventory_names.append(w.card_name)
		if w.category == WeaponCard.WeaponCategory.WEAPON:
			current_type = w.weapon_type
			current_tier = max(current_tier, w.tier)
		_collect_effects(w, player_fx)

	var upgrade_pool: Array[WeaponCard] = []
	var sidegrade_pool: Array[WeaponCard] = []
	var off_type_pool: Array[WeaponCard] = []
	var equipment_pool: Array[WeaponCard] = []

	for w in all_weapons:
		if w.category == WeaponCard.WeaponCategory.EQUIPMENT:
			if not inventory_names.has(w.card_name) and _meets_prerequisite(w, inventory_names):
				equipment_pool.append(w)
			continue

		if current_type != null and w.weapon_type == current_type:
			if _is_valid_upgrade(w, inventory_names, player_fx, current_tier):
				if w.tier == current_tier + 1 and w.is_upgrade_for(inventory_names):
					upgrade_pool.append(w)
				elif w.tier == current_tier:
					sidegrade_pool.append(w)
		else:
			if w.rarity <= 2 and not inventory_names.has(w.card_name) and _meets_prerequisite(w, inventory_names):
				off_type_pool.append(w)

	# 权重组合
	var pool: Array = []
	for w in upgrade_pool:
		pool.append(w)
		pool.append(w)  # 升级双倍权重
	for w in sidegrade_pool:
		pool.append(w)
	for w in off_type_pool:
		pool.append(w)
	for w in equipment_pool:
		if randf() < 0.35:
			pool.append(w)

	# 升级卡池
	var effect_count := player.active_effects.size()
	var upgrade_weight := 0.25
	if effect_count >= 4:
		upgrade_weight = 0.55
	elif effect_count >= 2:
		upgrade_weight = 0.40

	var added_targets: Array = []
	for effect in player.active_effects:
		if effect is WeaponCard and not added_targets.has(effect):
			if randf() < upgrade_weight:
				pool.append(UpgradeGenerator.generate(effect))
				added_targets.append(effect)

	return pool

func _collect_effects(w: WeaponCard, fx: Dictionary):
	for e in w.melee_effects:
		var lv = w.melee_effect_levels.get(e, 0)
		if fx["melee"].get(e, 0) < lv:
			fx["melee"][e] = lv
	for e in w.ranged_effects:
		var lv = w.ranged_effect_levels.get(e, 0)
		if fx["ranged"].get(e, 0) < lv:
			fx["ranged"][e] = lv
	for e in w.magic_effects:
		var lv = w.magic_effect_levels.get(e, 0)
		if fx["magic"].get(e, 0) < lv:
			fx["magic"][e] = lv

func _is_valid_upgrade(w: WeaponCard, inventory: Array[String], player_fx: Dictionary, current_tier: int) -> bool:
	if not _meets_prerequisite(w, inventory):
		return false
	if w.tier > current_tier:
		return true

	# 检查是否包含更高级别的效果
	var fx_key: String
	var fx_list: Array
	var fx_levels: Dictionary
	match w.weapon_type:
		WeaponCard.WeaponType.MELEE:
			fx_key = "melee"
			fx_list = w.melee_effects
			fx_levels = w.melee_effect_levels
		WeaponCard.WeaponType.RANGED:
			fx_key = "ranged"
			fx_list = w.ranged_effects
			fx_levels = w.ranged_effect_levels
		_:
			fx_key = "magic"
			fx_list = w.magic_effects
			fx_levels = w.magic_effect_levels

	for effect in fx_list:
		var new_lv = fx_levels.get(effect, 0)
		if new_lv > player_fx[fx_key].get(effect, 0):
			return true
	return false

func _meets_prerequisite(card: WeaponCard, inventory: Array[String]) -> bool:
	if prerequisites.has(card.card_name):
		return inventory.has(prerequisites[card.card_name])
	if card.tags.size() == 0:
		return true
	for tag in card.tags:
		if inventory.has(tag):
			return true
	return false
