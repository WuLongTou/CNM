class_name UpgradeGenerator
## 随机升级卡生成器
## 为玩家已有的 WeaponCard 生成 UpgradeCard

const MAX_EFFECT_LEVEL: int = 5

# 升级维度: [属性名, 最小值, 最大值]
const _DIMENSIONS: Array = [
	["damage",       0.05, 0.15],
	["attack_speed", 0.05, 0.12],
	["health",       3.0,  8.0],
	["speed",        3.0,  8.0],
	["crit",         2.0,  5.0],
	["life_steal",   1.0,  3.0],
	["freeze",       2.0,  5.0],
	["poison",       2.0,  5.0],
]

# 副作用表: [类型, 最小值, 最大值]
const _TRADEOFFS: Array = [
	[UpgradeCard.TradeoffType.HEALTH_COST, 1.0, 3.0],
	[UpgradeCard.TradeoffType.SPEED,       -2.0, -5.0],
	[UpgradeCard.TradeoffType.HEALTH_MAX,  -3.0, -8.0],
]

## 为已有 WeaponCard 生成一张随机升级卡
static func generate(target: WeaponCard) -> UpgradeCard:
	var card := UpgradeCard.new()
	card.target_ref = target
	card.display_name = "强化·" + target.card_name

	# 决定升级维度数量: 1-2 个
	var dim_count := 1 if randf() < 0.6 else 2

	# 40% 概率改为提升特效等级（如果目标有特效）
	if randf() < 0.4 and _has_effects(target):
		_upgrade_random_effect(target, card)
		dim_count = max(1, dim_count - 1)  # 减少一个数值维度

	var chosen_dims := _pick_dimensions(target, dim_count)

	# 应用数值提升
	for dim in chosen_dims:
		_apply_dimension(card, dim)

	# 30% 概率添加副作用
	if randf() < 0.3:
		_apply_tradeoff(card)

	# 确定主导方向
	card.upgrade_direction = _determine_direction(card)

	# 稀有度根据增量大小
	card.rarity = 2 if dim_count >= 2 else 1

	return card

# ═══════════════════════════════════════════════
# 内部方法
# ═══════════════════════════════════════════════

static func _pick_dimensions(target: WeaponCard, count: int) -> Array:
	# 构建权重 — 偏向目标已有优势方向
	var weighted := []
	for dim in _DIMENSIONS:
		var stat_name: String = dim[0]
		var weight := 1.0
		# 根据目标已有属性增强权重
		match stat_name:
			"damage":
				if target.damage_modifier > 1.0: weight = 2.0
			"attack_speed":
				if target.attack_speed_modifier > 1.0: weight = 2.0
			"crit":
				if target.critical_bonus > 0: weight = 2.0
			"life_steal":
				if target.life_steal_bonus > 0: weight = 2.0
			"freeze":
				if target.freeze_bonus > 0: weight = 2.0
			"poison":
				if target.poison_bonus > 0: weight = 2.0
		weighted.append({"dim": dim, "weight": weight})

	var chosen := []
	for i in range(count):
		var pick := _weighted_pick(weighted)
		chosen.append(pick)
		# 移除已选的，避免重复
		for j in range(weighted.size()):
			if weighted[j]["dim"] == pick:
				weighted.remove_at(j)
				break
	return chosen

static func _apply_dimension(card: UpgradeCard, dim: Array) -> void:
	var stat_name: String = dim[0]
	var min_val: float = dim[1]
	var max_val: float = dim[2]
	var val := randf_range(min_val, max_val)

	match stat_name:
		"damage":
			card.damage_delta = val
		"attack_speed":
			card.attack_speed_delta = val
		"health":
			card.health_delta = val
		"speed":
			card.speed_delta = val
		"crit":
			card.crit_delta = val
		"life_steal":
			card.life_steal_delta = val
		"freeze":
			card.freeze_delta = val
		"poison":
			card.poison_delta = val

static func _has_effects(target: WeaponCard) -> bool:
	return target.melee_effects.size() > 0 or target.ranged_effects.size() > 0 or target.magic_effects.size() > 0

static func _upgrade_random_effect(target: WeaponCard, card: UpgradeCard) -> void:
	# 收集所有可升级的特效
	var candidates: Array = []
	var effect_levels: Dictionary = {}

	for e in target.melee_effects:
		var lv: int = target.melee_effect_levels.get(e, 0)
		if lv < MAX_EFFECT_LEVEL:
			candidates.append({"effect": e, "type": "melee", "current": lv})
	for e in target.ranged_effects:
		var lv: int = target.ranged_effect_levels.get(e, 0)
		if lv < MAX_EFFECT_LEVEL:
			candidates.append({"effect": e, "type": "ranged", "current": lv})
	for e in target.magic_effects:
		var lv: int = target.magic_effect_levels.get(e, 0)
		if lv < MAX_EFFECT_LEVEL:
			candidates.append({"effect": e, "type": "magic", "current": lv})

	if candidates.is_empty():
		return

	var pick = candidates[randi() % candidates.size()]
	card.effect_upgrades[pick["effect"]] = 1

static func _apply_tradeoff(card: UpgradeCard) -> void:
	var t = _TRADEOFFS[randi() % _TRADEOFFS.size()]
	card.tradeoff_type = t[0]
	card.tradeoff_value = randf_range(t[1], t[2])

static func _determine_direction(card: UpgradeCard) -> UpgradeCard.UpgradeDirection:
	if card.damage_delta > 0:
		return UpgradeCard.UpgradeDirection.DAMAGE
	if card.attack_speed_delta > 0 or card.speed_delta > 0:
		return UpgradeCard.UpgradeDirection.SPEED
	if card.crit_delta > 0:
		return UpgradeCard.UpgradeDirection.CRIT
	if card.life_steal_delta > 0:
		return UpgradeCard.UpgradeDirection.LEECH
	if card.health_delta > 0:
		return UpgradeCard.UpgradeDirection.DEFENSE
	if not card.effect_upgrades.is_empty() or card.freeze_delta > 0 or card.poison_delta > 0:
		return UpgradeCard.UpgradeDirection.EFFECT
	return UpgradeCard.UpgradeDirection.DAMAGE

static func _weighted_pick(items: Array) -> Array:
	var total := 0.0
	for item in items:
		total += item["weight"]
	var roll := randf() * total
	var cumulative := 0.0
	for item in items:
		cumulative += item["weight"]
		if roll <= cumulative:
			return item["dim"]
	return items[-1]["dim"]
