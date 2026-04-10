class_name WeaponFactory
## 数据驱动武器工厂
## 用紧凑的数据表替代 580 行重复的 WeaponCard.new() 代码

# 缩写映射，保持数据表紧凑
const W = WeaponCard.WeaponCategory.WEAPON
const E = WeaponCard.WeaponCategory.EQUIPMENT
const ME = WeaponCard.WeaponType.MELEE
const RA = WeaponCard.WeaponType.RANGED
const MA = WeaponCard.WeaponType.MAGIC

## 构建整个武器库
static func build_all() -> Array[WeaponCard]:
	var cards: Array[WeaponCard] = []
	for def in _WEAPON_DATA:
		cards.append(_build_card(def))
	_auto_assign_rarity(cards)
	return cards

## 前置条件表
static func get_prerequisites() -> Dictionary:
	return {
		"战斧": "铁剑", "双刀": "铁剑",
		"淬毒短刀": "双刀",
		"巨斧": "战斧", "长枪": "巨斧", "死神镰刀": "长枪",
		"重弩": "弓箭", "散弹枪": "弓箭",
		"狙击弩": "重弩", "轨迹弩": "重弩",
		"回旋镖": "散弹枪",
		"火球法杖": "法杖", "魅惑法杖": "法杖",
		"力场法杖": "法杖",
		"冰霜法杖": "火球法杖", "雷电法杖": "火球法杖",
		"死神法杖": "雷电法杖",
	}

# ═══════════════════════════════════════════════
# 数据表
# 字段:  name, desc, type, cat, tier, upcat, icon, [tags]
# 可选:  dmg, spd, asp, cd, hp, spd_mod, crit, ls, frz, psn, aura, hcost
# 效果:  melee_fx, ranged_fx, magic_fx  (dict: effect_enum -> level)
# ═══════════════════════════════════════════════

const _WEAPON_DATA = [
	# ═══ 近战武器 ═══
	{ "name": "铁剑", "desc": "平衡的近战武器，适合新手",
	  "type": ME, "cat": W, "tier": 1, "upcat": "武器", "icon": "⚔️", "rarity": 1 },

	{ "name": "战斧", "desc": "高伤害低攻速",
	  "type": ME, "cat": W, "tier": 2, "upcat": "武器", "icon": "🪓", "rarity": 2,
	  "tags": ["铁剑"], "dmg": 1.5, "asp": 0.7,
	  "melee_fx": { "SPLASH": 1 } },

	{ "name": "双刀", "desc": "高攻速多目标",
	  "type": ME, "cat": W, "tier": 2, "upcat": "武器", "icon": "🗡️", "rarity": 2,
	  "tags": ["铁剑"], "dmg": 0.7, "asp": 1.5, "hp": -10,
	  "melee_fx": { "MULTI_TARGET": 1 } },

	{ "name": "淬毒短刀", "desc": "近战带毒持续伤害",
	  "type": ME, "cat": W, "tier": 2, "upcat": "武器", "icon": "🗡️", "rarity": 2,
	  "tags": ["双刀"], "dmg": 0.8, "asp": 1.4, "psn": 10,
	  "melee_fx": { "MULTI_TARGET": 1 } },

	{ "name": "巨斧", "desc": "超高伤害溅射",
	  "type": ME, "cat": W, "tier": 3, "upcat": "武器", "icon": "🪓", "rarity": 3,
	  "tags": ["战斧"], "dmg": 1.8, "asp": 0.6, "hcost": 5,
	  "melee_fx": { "SPLASH": 2, "KNOCKBACK": 1 } },

	{ "name": "长枪", "desc": "穿透多目标",
	  "type": ME, "cat": W, "tier": 3, "upcat": "武器", "icon": "🔱", "rarity": 3,
	  "tags": ["巨斧"], "dmg": 1.4, "asp": 0.8,
	  "melee_fx": { "MULTI_TARGET": 2, "KNOCKBACK": 1 } },

	{ "name": "死神镰刀", "desc": "击杀AOE+吸血",
	  "type": ME, "cat": W, "tier": 4, "upcat": "武器", "icon": "⚰️", "rarity": 4,
	  "tags": ["长枪"], "dmg": 2.0, "asp": 0.9, "hcost": 10,
	  "melee_fx": { "KILL_EXPLOSION": 2, "KILL_SHIELD": 1 } },

	# ═══ 远程武器 ═══
	{ "name": "弓箭", "desc": "基础远程武器",
	  "type": RA, "cat": W, "tier": 1, "upcat": "武器", "icon": "🏹", "rarity": 1,
	  "dmg": 0.9, "asp": 0.8, "cd": 0.6 },

	{ "name": "重弩", "desc": "连发武器",
	  "type": RA, "cat": W, "tier": 2, "upcat": "武器", "icon": "🎯", "rarity": 2,
	  "tags": ["弓箭"], "dmg": 0.8, "asp": 0.7, "cd": 0.8,
	  "ranged_fx": { "MULTI_SHOT": 1 } },

	{ "name": "散弹枪", "desc": "扇形攻击",
	  "type": RA, "cat": W, "tier": 2, "upcat": "武器", "icon": "🔫", "rarity": 2,
	  "tags": ["弓箭"], "dmg": 0.6, "asp": 0.6, "cd": 1.0,
	  "ranged_fx": { "MULTI_SHOT": 2 } },

	{ "name": "狙击弩", "desc": "超高伤害穿透",
	  "type": RA, "cat": W, "tier": 3, "upcat": "武器", "icon": "🎯", "rarity": 3,
	  "tags": ["重弩"], "dmg": 2.2, "asp": 0.4, "cd": 1.5,
	  "ranged_fx": { "PIERCING": 2 } },

	{ "name": "轨迹弩", "desc": "路径伤害",
	  "type": RA, "cat": W, "tier": 3, "upcat": "武器", "icon": "➰", "rarity": 2,
	  "tags": ["重弩"], "dmg": 1.0, "asp": 0.75, "cd": 0.7,
	  "ranged_fx": { "TRAJECTORY_DAMAGE": 1 } },

	{ "name": "回旋镖", "desc": "弹道返回",
	  "type": RA, "cat": W, "tier": 3, "upcat": "武器", "icon": "🪃", "rarity": 2,
	  "tags": ["散弹枪"], "dmg": 0.7, "asp": 0.8, "cd": 0.6,
	  "ranged_fx": { "RETURNING": 1 } },

	# ═══ 魔法武器 ═══
	{ "name": "法杖", "desc": "基础魔法武器",
	  "type": MA, "cat": W, "tier": 1, "upcat": "武器", "icon": "🪄", "rarity": 1,
	  "asp": 0.5, "magic_fx": { "DOT": 1 } },

	{ "name": "火球法杖", "desc": "连锁击退",
	  "type": MA, "cat": W, "tier": 2, "upcat": "武器", "icon": "🔥", "rarity": 2,
	  "tags": ["法杖"], "dmg": 1.2, "asp": 0.5,
	  "magic_fx": { "CHAIN": 1, "PUSH": 1 } },

	{ "name": "魅惑法杖", "desc": "让敌人互相攻击",
	  "type": MA, "cat": W, "tier": 2, "upcat": "武器", "icon": "💖", "rarity": 2,
	  "tags": ["法杖"], "dmg": 0.8, "asp": 0.6,
	  "magic_fx": { "CHARM": 3 } },

	{ "name": "冰霜法杖", "desc": "定身敌人",
	  "type": MA, "cat": W, "tier": 3, "upcat": "武器", "icon": "❄️", "rarity": 2,
	  "tags": ["火球法杖"], "dmg": 0.9, "asp": 0.55,
	  "magic_fx": { "STUN": 2 } },

	{ "name": "雷电法杖", "desc": "强力连锁",
	  "type": MA, "cat": W, "tier": 3, "upcat": "武器", "icon": "⚡", "rarity": 3,
	  "tags": ["火球法杖"], "dmg": 1.3, "asp": 0.45,
	  "magic_fx": { "CHAIN": 3 } },

	{ "name": "死神法杖", "desc": "死亡连锁",
	  "type": MA, "cat": W, "tier": 4, "upcat": "武器", "icon": "💀", "rarity": 4,
	  "tags": ["雷电法杖"], "dmg": 1.5, "asp": 0.4, "hcost": 15,
	  "magic_fx": { "CHAIN": 4, "DOT": 3 } },

	{ "name": "力场法杖", "desc": "范围击退所有敌人",
	  "type": MA, "cat": W, "tier": 2, "upcat": "武器", "icon": "🌀", "rarity": 2,
	  "tags": ["法杖"], "dmg": 0.9, "asp": 0.55,
	  "magic_fx": { "PUSH": 2 } },

	# ═══ 装备 ═══
	{ "name": "生命药水", "desc": "恢复生命",
	  "type": ME, "cat": E, "tier": 1, "upcat": "buff", "icon": "🧪", "rarity": 1, "hp": 30 },

	{ "name": "敏捷靴", "desc": "提升速度",
	  "type": ME, "cat": E, "tier": 1, "upcat": "鞋子", "icon": "👢", "rarity": 1,
	  "spd_mod": 20, "hp": -5 },

	{ "name": "攻击宝石", "desc": "提升伤害",
	  "type": ME, "cat": E, "tier": 1, "upcat": "特效", "icon": "💎", "rarity": 1,
	  "dmg": 1.15, "hp": -10 },

	{ "name": "暴击指环", "desc": "提升暴击率",
	  "type": ME, "cat": E, "tier": 2, "upcat": "戒指", "icon": "💍", "rarity": 2,
	  "crit": 12 },

	{ "name": "吸血护符", "desc": "攻击附带吸血效果",
	  "type": ME, "cat": E, "tier": 2, "upcat": "buff", "icon": "🧿", "rarity": 2,
	  "ls": 6 },

	{ "name": "寒霜徽章", "desc": "增加冻结几率",
	  "type": ME, "cat": E, "tier": 2, "upcat": "特效", "icon": "❄️", "rarity": 2,
	  "frz": 15 },

	{ "name": "剧毒之戒", "desc": "增加中毒几率",
	  "type": ME, "cat": E, "tier": 2, "upcat": "戒指", "icon": "☠️", "rarity": 2,
	  "psn": 12 },

	{ "name": "冰霜护盾", "desc": "开启环绕护盾",
	  "type": ME, "cat": E, "tier": 3, "upcat": "护甲", "icon": "🛡️", "rarity": 3,
	  "aura": true },

	{ "name": "铁盔", "desc": "提高生命上限",
	  "type": ME, "cat": E, "tier": 1, "upcat": "头盔", "icon": "⛑️", "hp": 15 },

	{ "name": "皮甲", "desc": "提高生命上限，略降速度",
	  "type": ME, "cat": E, "tier": 1, "upcat": "护甲", "icon": "🧥",
	  "hp": 20, "spd_mod": -5 },

	{ "name": "疾风护符", "desc": "提升攻击速度",
	  "type": ME, "cat": E, "tier": 2, "upcat": "buff", "icon": "💨", "asp": 1.2 },

	{ "name": "再生戒指", "desc": "提高生命上限并附带少量吸血",
	  "type": ME, "cat": E, "tier": 2, "upcat": "戒指", "icon": "💚",
	  "hp": 10, "ls": 3 },

	{ "name": "狂战士护腕", "desc": "大幅提升伤害，但降低防御",
	  "type": ME, "cat": E, "tier": 2, "upcat": "buff", "icon": "💪",
	  "dmg": 1.35, "hp": -20 },

	{ "name": "猎人印记", "desc": "增加暴击并提升伤害",
	  "type": ME, "cat": E, "tier": 3, "upcat": "buff", "icon": "🎯",
	  "crit": 15, "dmg": 1.1 },

	{ "name": "幽灵斗篷", "desc": "提升移动速度，代价是少量生命上限",
	  "type": ME, "cat": E, "tier": 2, "upcat": "护甲", "icon": "👻",
	  "spd_mod": 30, "hp": -5 },
]

# ═══════════════════════════════════════════════
# 内部构建
# ═══════════════════════════════════════════════

static func _build_card(d: Dictionary) -> WeaponCard:
	var c = WeaponCard.new()
	c.init(d["name"], d["desc"], d["type"], d.get("tags", []))
	c.category = d["cat"]
	c.tier = d.get("tier", 1)
	c.upgrade_category = d.get("upcat", "")
	c.icon = d.get("icon", "⚔️")

	# 属性修改器
	if d.has("dmg"):    c.damage_modifier = d["dmg"]
	if d.has("asp"):    c.attack_speed_modifier = d["asp"]
	if d.has("cd"):     c.cooldown = d["cd"]
	if d.has("hp"):     c.health_modifier = d["hp"]
	if d.has("spd_mod"):c.speed_modifier = d["spd_mod"]
	if d.has("crit"):   c.critical_bonus = d["crit"]
	if d.has("ls"):     c.life_steal_bonus = d["ls"]
	if d.has("frz"):    c.freeze_bonus = d["frz"]
	if d.has("psn"):    c.poison_bonus = d["psn"]
	if d.has("hcost"):  c.health_cost = d["hcost"]
	if d.has("aura"):   c.grants_aura = d["aura"]
	if d.has("rarity"): c.rarity = d["rarity"]

	# 效果（字符串 enum 名 → enum 值）
	if d.has("melee_fx"):
		for fx_name in d["melee_fx"]:
			var fx = WeaponCard.MeleeEffect[fx_name]
			c.melee_effects.append(fx)
			c.melee_effect_levels[fx] = d["melee_fx"][fx_name]
	if d.has("ranged_fx"):
		for fx_name in d["ranged_fx"]:
			var fx = WeaponCard.RangedEffect[fx_name]
			c.ranged_effects.append(fx)
			c.ranged_effect_levels[fx] = d["ranged_fx"][fx_name]
	if d.has("magic_fx"):
		for fx_name in d["magic_fx"]:
			var fx = WeaponCard.MagicEffect[fx_name]
			c.magic_effects.append(fx)
			c.magic_effect_levels[fx] = d["magic_fx"][fx_name]
	return c

static func _auto_assign_rarity(cards: Array[WeaponCard]):
	for card in cards:
		if card.rarity > 0:
			continue  # 已手动指定
		var score = 0
		if card.damage_modifier != 1.0: score += 1
		if card.attack_speed_modifier != 1.0: score += 1
		if card.health_modifier != 0: score += 1
		if card.speed_modifier != 0: score += 1
		if card.critical_bonus > 0: score += 1
		if card.life_steal_bonus > 0: score += 1
		if card.freeze_bonus > 0: score += 1
		if card.poison_bonus > 0: score += 1
		if card.grants_aura: score += 2
		score += card.melee_effects.size()
		score += card.ranged_effects.size()
		score += card.magic_effects.size()
		if score <= 0:    card.rarity = 1
		elif score == 1:  card.rarity = 2
		elif score <= 3:  card.rarity = 3
		else:             card.rarity = 4
