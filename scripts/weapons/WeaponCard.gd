class_name WeaponCard
extends Resource
## 武器/装备卡牌资源
## 定义武器的所有属性、效果和升级信息

## 武器类型 - 决定攻击方式
enum WeaponType {
	MELEE,    ## 近战武器 - 扇形斩击，需要靠近敌人
	RANGED,   ## 远程武器 - 发射子弹，可远距离攻击
	MAGIC     ## 魔法武器 - 追踪导弹，自动锁定目标
}

## 装备类别
enum WeaponCategory {
	WEAPON,     ## 武器 - 同时只能装备一把，会替换现有武器
	EQUIPMENT   ## 装备 - 可以叠加，提供被动加成
}

enum TargetPriority {
	NEAREST,      # 最近的目标
	FARTHEST,     # 最远的目标
	MOST_ENEMIES  # 敌人最多的地方
}

# 近战特效
enum MeleeEffect {
	NONE,
	SPLASH,           # 溅射
	MULTI_TARGET,     # 同时攻击多个敌人
	KILL_EXPLOSION,   # 击杀后AOE
	KILL_SHIELD,      # 击杀后护盾
	KNOCKBACK,        # 击退
}

# 远程特效
enum RangedEffect {
	NONE,
	MULTI_SHOT,       # 多发子弹
	TRAJECTORY_DAMAGE,# 路径伤害
	RETURNING,        # 弹道返回
	PIERCING,         # 穿透
}

# 魔法特效
enum MagicEffect {
	NONE,
	CHARM,            # 魅惑
	PUSH,             # 击退
	STUN,             # 定身
	CHAIN,            # 连锁
	DOT               # 持续伤害
}

@export var card_name: String
@export var description: String
@export var weapon_type: WeaponType
@export var category: WeaponCategory = WeaponCategory.WEAPON
@export var tier: int = 1
@export var upgrade_category: String = ""

@export var damage_modifier: float = 1.0
@export var attack_speed_modifier: float = 1.0
@export var range_modifier: float = 1.0

@export var health_modifier: float = 0.0
@export var speed_modifier: float = 0.0

@export var cooldown: float = 0.0
@export var health_cost: int = 0

# 额外属性加成
@export var critical_bonus: float = 0.0
@export var life_steal_bonus: float = 0.0
@export var freeze_bonus: float = 0.0
@export var poison_bonus: float = 0.0
@export var grants_aura: bool = false

# 特效等级系统
var melee_effects: Array = []
var melee_effect_levels: Dictionary = {}

var ranged_effects: Array = []
var ranged_effect_levels: Dictionary = {}

var magic_effects: Array = []
var magic_effect_levels: Dictionary = {}

@export var target_priority: TargetPriority = TargetPriority.NEAREST

@export var icon: String = "⚔️"
@export var rarity: int = 1
@export var duration: int = -1  # -1 为永久效果

var tags: Array = []

# 升级追踪
var upgrade_count: int = 0
var upgrade_history: Dictionary = {}  # { UpgradeCard.UpgradeDirection.DAMAGE: 3, ... }
var base_name: String = ""

func init(name: String, desc: String, weapon_kind: WeaponType, card_tags: Array = []) -> WeaponCard:
	card_name = name
	base_name = name
	description = desc
	weapon_type = weapon_kind
	melee_effects = []
	ranged_effects = []
	magic_effects = []
	melee_effect_levels = {}
	ranged_effect_levels = {}
	magic_effect_levels = {}
	tags = card_tags
	return self

func get_effect_description() -> String:
	var desc_parts = []
	
	# 基础属性
	if damage_modifier != 1.0:
		var mod_text = "伤害" + ("+" if damage_modifier > 1.0 else "")
		desc_parts.append(mod_text + str(int((abs(damage_modifier) - 1.0) * 100)) + "%")
	
	if attack_speed_modifier != 1.0:
		var mod_text = "攻速" + ("+" if attack_speed_modifier > 1.0 else "")
		desc_parts.append(mod_text + str(int((abs(attack_speed_modifier) - 1.0) * 100)) + "%")
	
	if health_modifier > 0:
		desc_parts.append("生命+" + str(health_modifier))
	elif health_modifier < 0:
		desc_parts.append("生命" + str(health_modifier))
	
	if speed_modifier > 0:
		desc_parts.append("速度+" + str(speed_modifier))
	elif speed_modifier < 0:
		desc_parts.append("速度" + str(speed_modifier))
	
	if cooldown > 0:
		desc_parts.append("冷却" + str(cooldown) + "秒")
	
	if health_cost > 0:
		desc_parts.append("消耗" + str(health_cost) + "生命")

	if critical_bonus > 0:
		desc_parts.append("暴击+" + str(int(critical_bonus)) + "%")
	if life_steal_bonus > 0:
		desc_parts.append("吸血+" + str(int(life_steal_bonus)) + "%")
	if freeze_bonus > 0:
		desc_parts.append("冰冻+" + str(int(freeze_bonus)) + "%")
	if poison_bonus > 0:
		desc_parts.append("中毒+" + str(int(poison_bonus)) + "%")
	if grants_aura:
		desc_parts.append("开启护盾领域")
	
	if desc_parts.is_empty():
		return "基础武器"
	
	return " | ".join(desc_parts)

func get_weapon_type_text() -> String:
	match weapon_type:
		WeaponType.MELEE:
			return "近战"
		WeaponType.RANGED:
			return "远程"
		WeaponType.MAGIC:
			return "魔法"
		_:
			return "未知"

func get_category_text() -> String:
	match category:
		WeaponCategory.WEAPON:
			return "武器"
		WeaponCategory.EQUIPMENT:
			return "装备"
		_:
			return "未知"

func is_upgrade_for(weapon_names: Array) -> bool:
	for tag in tags:
		for name in weapon_names:
			if tag in name:
				return true
	return false

func has_negative_effects() -> bool:
	return health_modifier < 0 or speed_modifier < 0 or damage_modifier < 1.0 or health_cost > 0

# 获取特效详细描述
func get_effect_details() -> String:
	var details = ""
	
	match weapon_type:
		WeaponType.MELEE:
			details = "【特性】伤害高·攻速快\n"
		WeaponType.RANGED:
			details = "【特性】自动索敌·攻速较慢\n"
		WeaponType.MAGIC:
			details = "【特性】自动攻击·特殊效果\n"
	
	var effect_list = []
	
	for effect in melee_effects:
		var level = melee_effect_levels.get(effect, 1)
		effect_list.append(get_melee_effect_desc(effect, level))
	
	for effect in ranged_effects:
		var level = ranged_effect_levels.get(effect, 1)
		effect_list.append(get_ranged_effect_desc(effect, level))
	
	for effect in magic_effects:
		var level = magic_effect_levels.get(effect, 1)
		effect_list.append(get_magic_effect_desc(effect, level))
	
	if effect_list.size() > 0:
		details += "\n【特效】\n" + "\n".join(effect_list)
	
	return details

func get_melee_effect_desc(effect: MeleeEffect, level: int) -> String:
	match effect:
		MeleeEffect.SPLASH:
			return "溅射 Lv." + str(level) + " (范围+" + str(level * 20) + "%)"
		MeleeEffect.MULTI_TARGET:
			return "多目标 Lv." + str(level) + " (同时攻击" + str(1 + level) + "个)"
		MeleeEffect.KILL_EXPLOSION:
			return "击杀AOE Lv." + str(level) + " (周围+" + str(level * 50) + "伤害)"
		MeleeEffect.KILL_SHIELD:
			return "击杀护盾 Lv." + str(level) + " (获得+" + str(level * 20) + "护盾)"
		MeleeEffect.KNOCKBACK:
			return "击退 Lv." + str(level) + " (推开" + str(level * 50) + "像素)"
		_:
			return ""

func get_melee_effect_detail(effect: MeleeEffect, _level: int) -> String:
	match effect:
		MeleeEffect.SPLASH:
			return "攻击时伤害波及周围敌人，范围随等级提升。\n等级1: +20%范围 | 等级2: +40%范围 | 等级3: +60%范围"
		MeleeEffect.MULTI_TARGET:
			return "一次攻击可命中多个敌人，近战清场神技。\n等级1: 2个目标 | 等级2: 3个目标 | 等级3: 4个目标"
		MeleeEffect.KILL_EXPLOSION:
			return "击杀僵尸时发生爆炸，对周围所有僵尸造成伤害。\n等级1: 50伤害 | 等级2: 100伤害 | 等级3: 150伤害"
		MeleeEffect.KILL_SHIELD:
			return "击杀僵尸时获得临时护盾，可抵挡伤害。\n等级1: +20护盾 | 等级2: +40护盾 | 等级3: +60护盾"
		MeleeEffect.KNOCKBACK:
			return "攻击时将敌人击退，使其暂时无法靠近。\n等级1: 50像素 | 等级2: 100像素 | 等级3: 150像素"
		_:
			return ""

func get_ranged_effect_desc(effect: RangedEffect, level: int) -> String:
	match effect:
		RangedEffect.MULTI_SHOT:
			return "多发 Lv." + str(level) + " (发射" + str(1 + level) + "发)"
		RangedEffect.TRAJECTORY_DAMAGE:
			var percent = level * 30
			return "路径伤害 Lv." + str(level) + " (途经" + str(percent) + "%伤害)"
		RangedEffect.RETURNING:
			return "反弹 Lv." + str(level) + " (反弹" + str(level) + "次)"
		RangedEffect.PIERCING:
			return "穿透 Lv." + str(level) + " (穿透" + str(level) + "个敌人)"
		_:
			return ""

func get_ranged_effect_detail(effect: RangedEffect, _level: int) -> String:
	match effect:
		RangedEffect.MULTI_SHOT:
			return "同时发射多发子弹，大幅提高DPS。\n等级1: 2发 | 等级2: 3发 | 等级3: 4发"
		RangedEffect.TRAJECTORY_DAMAGE:
			return "子弹飞行轨迹上的敌人也会受到伤害。\n等级1: 30%伤害 | 等级2: 60%伤害 | 等级3: 90%伤害"
		RangedEffect.RETURNING:
			return "子弹命中敌人或边界后反弹继续飞行。\n等级1: 1次 | 等级2: 2次 | 等级3: 3次"
		RangedEffect.PIERCING:
			return "子弹可穿透敌人继续飞行，攻击后续目标。\n等级1: 穿透1个 | 等级2: 穿透2个 | 等级3: 穿透3个"
		_:
			return ""

func get_magic_effect_desc(effect: MagicEffect, level: int) -> String:
	match effect:
		MagicEffect.CHARM:
			return "魅惑 Lv." + str(level) + " (持续" + str(level * 5) + "秒)"
		MagicEffect.PUSH:
			return "击退 Lv." + str(level) + " (推开" + str(level * 80) + "像素)"
		MagicEffect.STUN:
			return "定身 Lv." + str(level) + " (持续" + str(level * 1.5) + "秒)"
		MagicEffect.CHAIN:
			return "连锁 Lv." + str(level) + " (传递" + str(level + 1) + "次)"
		MagicEffect.DOT:
			return "持续伤害 Lv." + str(level) + " (每秒" + str(level * 5) + "伤害)"
		_:
			return ""

func get_magic_effect_detail(effect: MagicEffect, _level: int) -> String:
	match effect:
		MagicEffect.CHARM:
			return "使敌人陷入混乱，攻击其他僵尸。\n持续时间随等级提升，坦克僵尸魅惑时间减少40%。\n等级1: 5秒 | 等级2: 10秒 | 等级3: 15秒"
		MagicEffect.PUSH:
			return "强大的冲击波将敌人推开较远距离。\n等级1: 80像素 | 等级2: 160像素 | 等级3: 240像素"
		MagicEffect.STUN:
			return "使敌人暂时无法移动和攻击。\n等级1: 1.5秒 | 等级2: 3秒 | 等级3: 4.5秒"
		MagicEffect.CHAIN:
			return "伤害在敌人之间连锁跳跃。\n等级1: 2个目标 | 等级2: 3个目标 | 等级3: 4个目标"
		MagicEffect.DOT:
			return "使敌人持续受到火焰/毒液伤害。\n等级1: 5 DPS | 等级2: 10 DPS | 等级3: 15 DPS"
		_:
			return ""

func get_effect_detail(effect_name: String, effect_type: String, level: int) -> String:
	var detail = ""
	match effect_type:
		"melee":
			match effect_name:
				"SPLASH":
					detail = get_melee_effect_detail(MeleeEffect.SPLASH, level)
				"MULTI_TARGET":
					detail = get_melee_effect_detail(MeleeEffect.MULTI_TARGET, level)
				"KILL_EXPLOSION":
					detail = get_melee_effect_detail(MeleeEffect.KILL_EXPLOSION, level)
				"KILL_SHIELD":
					detail = get_melee_effect_detail(MeleeEffect.KILL_SHIELD, level)
				"KNOCKBACK":
					detail = get_melee_effect_detail(MeleeEffect.KNOCKBACK, level)
		"ranged":
			match effect_name:
				"MULTI_SHOT":
					detail = get_ranged_effect_detail(RangedEffect.MULTI_SHOT, level)
				"TRAJECTORY_DAMAGE":
					detail = get_ranged_effect_detail(RangedEffect.TRAJECTORY_DAMAGE, level)
				"RETURNING":
					detail = get_ranged_effect_detail(RangedEffect.RETURNING, level)
				"PIERCING":
					detail = get_ranged_effect_detail(RangedEffect.PIERCING, level)
		"magic":
			match effect_name:
				"CHARM":
					detail = get_magic_effect_detail(MagicEffect.CHARM, level)
				"PUSH":
					detail = get_magic_effect_detail(MagicEffect.PUSH, level)
				"STUN":
					detail = get_magic_effect_detail(MagicEffect.STUN, level)
				"CHAIN":
					detail = get_magic_effect_detail(MagicEffect.CHAIN, level)
				"DOT":
					detail = get_magic_effect_detail(MagicEffect.DOT, level)
	return detail

# ═══════════════════════════════════════════════
# 升级进化
# ═══════════════════════════════════════════════

const _EVOLVE_PREFIXES: Dictionary = {
	2: {
		UpgradeCard.UpgradeDirection.DAMAGE: "锋利",
		UpgradeCard.UpgradeDirection.SPEED: "迅捷",
		UpgradeCard.UpgradeDirection.CRIT: "精准",
		UpgradeCard.UpgradeDirection.LEECH: "嗜血",
		UpgradeCard.UpgradeDirection.DEFENSE: "坚固",
		UpgradeCard.UpgradeDirection.EFFECT: "附魔",
	},
	5: {
		UpgradeCard.UpgradeDirection.DAMAGE: "狂暴",
		UpgradeCard.UpgradeDirection.SPEED: "疾风",
		UpgradeCard.UpgradeDirection.CRIT: "致命",
		UpgradeCard.UpgradeDirection.LEECH: "吸血",
		UpgradeCard.UpgradeDirection.DEFENSE: "铁壁",
		UpgradeCard.UpgradeDirection.EFFECT: "奥术",
	},
	8: {
		UpgradeCard.UpgradeDirection.DAMAGE: "毁灭",
		UpgradeCard.UpgradeDirection.SPEED: "闪电",
		UpgradeCard.UpgradeDirection.CRIT: "审判",
		UpgradeCard.UpgradeDirection.LEECH: "不朽",
		UpgradeCard.UpgradeDirection.DEFENSE: "永恒",
		UpgradeCard.UpgradeDirection.EFFECT: "传说",
	},
}

func evolve_name():
	if base_name == "":
		base_name = card_name
	var dominant := _get_dominant_direction()
	if dominant == UpgradeCard.UpgradeDirection.NONE:
		return
	var prefix := ""
	var thresholds := [8, 5, 2]
	for t in thresholds:
		if upgrade_count >= t and _EVOLVE_PREFIXES[t].has(dominant):
			prefix = _EVOLVE_PREFIXES[t][dominant]
			break
	if prefix != "":
		card_name = prefix + "·" + base_name

func _get_dominant_direction() -> UpgradeCard.UpgradeDirection:
	var best_dir := UpgradeCard.UpgradeDirection.NONE
	var best_count := 0
	for dir in upgrade_history:
		if upgrade_history[dir] > best_count:
			best_count = upgrade_history[dir]
			best_dir = dir
	return best_dir
