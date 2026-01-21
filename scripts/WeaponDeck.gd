class_name WeaponDeck
extends Node

var all_weapons: Array[WeaponCard] = []
var prerequisites: Dictionary = {}

func _ready():
	_initialize_weapon_deck()
	_setup_prerequisites()

func _setup_prerequisites():
	prerequisites = {
		# 近战升级路线
		"战斧": "铁剑",
		"双刀": "铁剑",
		"巨斧": "战斧",
		"长枪": "巨斧",
		"死神镰刀": "长枪",
		
		# 远程升级路线
		"重弩": "弓箭",
		"散弹枪": "弓箭",
		"狙击弩": "重弩",
		"轨迹弩": "重弩",
		"回旋镖": "散弹枪",
	"连环回旋镖": "回旋镖",
		
		# 魔法升级路线
		"火球法杖": "法杖",
		"魅惑法杖": "法杖",
		"魅惑法杖·精通": "魅惑法杖",
		"冰霜法杖": "火球法杖",
		"雷电法杖": "火球法杖",
		"时间法杖": "冰霜法杖",
		"死神法杖": "雷电法杖",
	}

func _initialize_weapon_deck():
	# ===== 近战武器 =====
	
	# 铁剑 - 基础近战（平衡型入门武器）
	var sword = WeaponCard.new()
	sword.init("铁剑", "平衡的近战武器，适合新手", WeaponCard.WeaponType.MELEE)
	sword.category = WeaponCard.WeaponCategory.WEAPON
	sword.tier = 1
	sword.upgrade_category = "武器"
	sword.damage_modifier = 1.0
	sword.attack_speed_modifier = 1.0
	sword.range_modifier = 1.0
	sword.icon = "⚔️"
	sword.rarity = 1
	all_weapons.append(sword)
	
	# 战斧 - 高伤害慢速
	var battle_axe = WeaponCard.new()
	battle_axe.init("战斧", "高伤害低攻速", WeaponCard.WeaponType.MELEE, ["铁剑"])
	battle_axe.category = WeaponCard.WeaponCategory.WEAPON
	battle_axe.tier = 2
	battle_axe.upgrade_category = "武器"
	battle_axe.damage_modifier = 1.5
	battle_axe.attack_speed_modifier = 0.7  # 攻速慢
	battle_axe.melee_effects = [WeaponCard.MeleeEffect.SPLASH]
	battle_axe.melee_effect_levels[WeaponCard.MeleeEffect.SPLASH] = 1
	battle_axe.icon = "🪓"
	battle_axe.rarity = 2
	all_weapons.append(battle_axe)
	
	# 双刀 - 快速多目标
	var dual_blades = WeaponCard.new()
	dual_blades.init("双刀", "高攻速多目标", WeaponCard.WeaponType.MELEE, ["铁剑"])
	dual_blades.category = WeaponCard.WeaponCategory.WEAPON
	dual_blades.tier = 2
	dual_blades.upgrade_category = "武器"
	dual_blades.damage_modifier = 0.7
	dual_blades.attack_speed_modifier = 1.5  # 攻速快
	dual_blades.melee_effects = [WeaponCard.MeleeEffect.MULTI_TARGET]
	dual_blades.melee_effect_levels[WeaponCard.MeleeEffect.MULTI_TARGET] = 1
	dual_blades.health_modifier = -10
	dual_blades.icon = "🗡️"
	dual_blades.rarity = 2
	all_weapons.append(dual_blades)
	
	# 巨斧 - 超高伤害
	var giant_axe = WeaponCard.new()
	giant_axe.init("巨斧", "超高伤害溅射", WeaponCard.WeaponType.MELEE, ["战斧"])
	giant_axe.category = WeaponCard.WeaponCategory.WEAPON
	giant_axe.tier = 3
	giant_axe.upgrade_category = "武器"
	giant_axe.damage_modifier = 1.8
	giant_axe.attack_speed_modifier = 0.6  # 攻速更慢
	giant_axe.melee_effects = [WeaponCard.MeleeEffect.SPLASH, WeaponCard.MeleeEffect.KNOCKBACK]
	giant_axe.melee_effect_levels[WeaponCard.MeleeEffect.SPLASH] = 2
	giant_axe.melee_effect_levels[WeaponCard.MeleeEffect.KNOCKBACK] = 1
	giant_axe.health_cost = 5
	giant_axe.icon = "🪓"
	giant_axe.rarity = 3
	all_weapons.append(giant_axe)
	
	# 长枪 - 穿透多目标
	var spear = WeaponCard.new()
	spear.init("长枪", "穿透多目标", WeaponCard.WeaponType.MELEE, ["巨斧"])
	spear.category = WeaponCard.WeaponCategory.WEAPON
	spear.tier = 3
	spear.upgrade_category = "武器"
	spear.damage_modifier = 1.4
	spear.attack_speed_modifier = 0.8
	spear.melee_effects = [WeaponCard.MeleeEffect.MULTI_TARGET, WeaponCard.MeleeEffect.KNOCKBACK]
	spear.melee_effect_levels[WeaponCard.MeleeEffect.MULTI_TARGET] = 2
	spear.melee_effect_levels[WeaponCard.MeleeEffect.KNOCKBACK] = 1
	spear.icon = "🔱"
	spear.rarity = 3
	all_weapons.append(spear)
	
	# 死神镰刀 - 传说级
	var reaper = WeaponCard.new()
	reaper.init("死神镰刀", "击杀AOE+吸血", WeaponCard.WeaponType.MELEE, ["长枪"])
	reaper.category = WeaponCard.WeaponCategory.WEAPON
	reaper.tier = 4
	reaper.upgrade_category = "武器"
	reaper.damage_modifier = 2.0
	reaper.attack_speed_modifier = 0.9
	reaper.melee_effects = [WeaponCard.MeleeEffect.KILL_EXPLOSION, WeaponCard.MeleeEffect.KILL_SHIELD]
	reaper.melee_effect_levels[WeaponCard.MeleeEffect.KILL_EXPLOSION] = 2
	reaper.melee_effect_levels[WeaponCard.MeleeEffect.KILL_SHIELD] = 1
	reaper.health_cost = 10
	reaper.icon = "⚰️"
	reaper.rarity = 4
	all_weapons.append(reaper)
	
	# ===== 远程武器 =====
	
	# 弓箭 - 基础
	var bow = WeaponCard.new()
	bow.init("弓箭", "基础远程武器", WeaponCard.WeaponType.RANGED)
	bow.category = WeaponCard.WeaponCategory.WEAPON
	bow.tier = 1
	bow.upgrade_category = "武器"
	bow.damage_modifier = 0.9
	bow.attack_speed_modifier = 0.8  # 远程武器攻速较慢
	bow.cooldown = 0.6  # 额外冷却时间
	bow.ammo_cost = 1
	bow.icon = "🏹"
	bow.rarity = 1
	all_weapons.append(bow)
	
	# 重弩 - 多发
	var crossbow = WeaponCard.new()
	crossbow.init("重弩", "连发武器", WeaponCard.WeaponType.RANGED, ["弓箭"])
	crossbow.category = WeaponCard.WeaponCategory.WEAPON
	crossbow.tier = 2
	crossbow.upgrade_category = "武器"
	crossbow.damage_modifier = 0.8
	crossbow.attack_speed_modifier = 0.7
	crossbow.cooldown = 0.8
	crossbow.ranged_effects = [WeaponCard.RangedEffect.MULTI_SHOT]
	crossbow.ranged_effect_levels[WeaponCard.RangedEffect.MULTI_SHOT] = 1
	crossbow.ammo_cost = 2
	crossbow.icon = "🎯"
	crossbow.rarity = 2
	all_weapons.append(crossbow)
	
	# 散弹枪 - 扇形
	var shotgun = WeaponCard.new()
	shotgun.init("散弹枪", "扇形攻击", WeaponCard.WeaponType.RANGED, ["弓箭"])
	shotgun.category = WeaponCard.WeaponCategory.WEAPON
	shotgun.tier = 2
	shotgun.upgrade_category = "武器"
	shotgun.damage_modifier = 0.6
	shotgun.attack_speed_modifier = 0.6
	shotgun.cooldown = 1.0
	shotgun.ranged_effects = [WeaponCard.RangedEffect.MULTI_SHOT]
	shotgun.ranged_effect_levels[WeaponCard.RangedEffect.MULTI_SHOT] = 2
	shotgun.ammo_cost = 5
	shotgun.icon = "🔫"
	shotgun.rarity = 2
	all_weapons.append(shotgun)
	
	# 狙击弩 - 高伤害穿透
	var sniper = WeaponCard.new()
	sniper.init("狙击弩", "超高伤害穿透", WeaponCard.WeaponType.RANGED, ["重弩"])
	sniper.category = WeaponCard.WeaponCategory.WEAPON
	sniper.tier = 3
	sniper.upgrade_category = "武器"
	sniper.damage_modifier = 2.2
	sniper.attack_speed_modifier = 0.4
	sniper.cooldown = 1.5
	sniper.ranged_effects = [WeaponCard.RangedEffect.PIERCING]
	sniper.ranged_effect_levels[WeaponCard.RangedEffect.PIERCING] = 2
	sniper.ammo_cost = 2
	sniper.icon = "🎯"
	sniper.rarity = 3
	all_weapons.append(sniper)
	
	# 轨迹弩 - 路径伤害
	var trajectory_bow = WeaponCard.new()
	trajectory_bow.init("轨迹弩", "路径伤害", WeaponCard.WeaponType.RANGED, ["重弩"])
	trajectory_bow.category = WeaponCard.WeaponCategory.WEAPON
	trajectory_bow.tier = 3
	trajectory_bow.upgrade_category = "武器"
	trajectory_bow.damage_modifier = 1.0
	trajectory_bow.attack_speed_modifier = 0.75
	trajectory_bow.cooldown = 0.7
	trajectory_bow.ranged_effects = [WeaponCard.RangedEffect.TRAJECTORY_DAMAGE]
	trajectory_bow.ranged_effect_levels[WeaponCard.RangedEffect.TRAJECTORY_DAMAGE] = 1
	trajectory_bow.ammo_cost = 1
	trajectory_bow.icon = "➰"
	trajectory_bow.rarity = 2
	all_weapons.append(trajectory_bow)
	
	# 回旋镖 - 弹道返回
	var boomerang = WeaponCard.new()
	boomerang.init("回旋镖", "弹道返回", WeaponCard.WeaponType.RANGED, ["散弹枪"])
	boomerang.category = WeaponCard.WeaponCategory.WEAPON
	boomerang.tier = 3
	boomerang.upgrade_category = "武器"
	boomerang.damage_modifier = 0.7
	boomerang.attack_speed_modifier = 0.8
	boomerang.cooldown = 0.6
	boomerang.ranged_effects = [WeaponCard.RangedEffect.RETURNING]
	boomerang.ranged_effect_levels[WeaponCard.RangedEffect.RETURNING] = 1
	boomerang.ammo_cost = 1
	boomerang.icon = "🪃"
	boomerang.rarity = 2
	all_weapons.append(boomerang)

	# 连环回旋镖 - 更多反弹
	var chain_boomerang = WeaponCard.new()
	chain_boomerang.init("连环回旋镖", "反弹次数提升", WeaponCard.WeaponType.RANGED, ["回旋镖"])
	chain_boomerang.category = WeaponCard.WeaponCategory.WEAPON
	chain_boomerang.tier = 4
	chain_boomerang.upgrade_category = "武器"
	chain_boomerang.damage_modifier = 0.8
	chain_boomerang.attack_speed_modifier = 0.85
	chain_boomerang.cooldown = 0.6
	chain_boomerang.ranged_effects = [WeaponCard.RangedEffect.RETURNING]
	chain_boomerang.ranged_effect_levels[WeaponCard.RangedEffect.RETURNING] = 2
	chain_boomerang.ammo_cost = 1
	chain_boomerang.icon = "🪃"
	chain_boomerang.rarity = 3
	all_weapons.append(chain_boomerang)
	
	# ===== 魔法武器 =====
	
	# 法杖 - 基础
	var staff = WeaponCard.new()
	staff.init("法杖", "基础魔法武器", WeaponCard.WeaponType.MAGIC)
	staff.category = WeaponCard.WeaponCategory.WEAPON
	staff.tier = 1
	staff.upgrade_category = "武器"
	staff.damage_modifier = 1.0
	staff.attack_speed_modifier = 0.5  # 魔法武器攻速很慢
	staff.cooldown = 0.0
	staff.magic_effects = [WeaponCard.MagicEffect.DOT]
	staff.magic_effect_levels[WeaponCard.MagicEffect.DOT] = 1
	staff.icon = "🪄"
	staff.rarity = 1
	all_weapons.append(staff)
	
	# 火球法杖 - 爆炸
	var fire_staff = WeaponCard.new()
	fire_staff.init("火球法杖", "连锁爆炸", WeaponCard.WeaponType.MAGIC, ["法杖"])
	fire_staff.category = WeaponCard.WeaponCategory.WEAPON
	fire_staff.tier = 2
	fire_staff.upgrade_category = "武器"
	fire_staff.damage_modifier = 1.2
	fire_staff.attack_speed_modifier = 0.5
	fire_staff.cooldown = 0.0
	fire_staff.magic_effects = [WeaponCard.MagicEffect.CHAIN, WeaponCard.MagicEffect.PUSH]
	fire_staff.magic_effect_levels[WeaponCard.MagicEffect.CHAIN] = 1
	fire_staff.magic_effect_levels[WeaponCard.MagicEffect.PUSH] = 1
	fire_staff.icon = "🔥"
	fire_staff.rarity = 2
	all_weapons.append(fire_staff)
	
	# 魅惑法杖 - 控制
	var charm_staff = WeaponCard.new()
	charm_staff.init("魅惑法杖", "让敌人互相攻击", WeaponCard.WeaponType.MAGIC, ["法杖"])
	charm_staff.category = WeaponCard.WeaponCategory.WEAPON
	charm_staff.tier = 2
	charm_staff.upgrade_category = "武器"
	charm_staff.damage_modifier = 0.8
	charm_staff.attack_speed_modifier = 0.6
	charm_staff.cooldown = 0.0
	charm_staff.magic_effects = [WeaponCard.MagicEffect.CHARM]
	charm_staff.magic_effect_levels[WeaponCard.MagicEffect.CHARM] = 3
	charm_staff.icon = "💖"
	charm_staff.rarity = 2
	all_weapons.append(charm_staff)
	
	# 魅惑法杖·精通 - 魅惑升级
	var charm_staff_plus = WeaponCard.new()
	charm_staff_plus.init("魅惑法杖·精通", "魅惑强度提升", WeaponCard.WeaponType.MAGIC, ["魅惑法杖"])
	charm_staff_plus.category = WeaponCard.WeaponCategory.WEAPON
	charm_staff_plus.tier = 3
	charm_staff_plus.upgrade_category = "特效"
	charm_staff_plus.damage_modifier = 0.85
	charm_staff_plus.attack_speed_modifier = 0.6
	charm_staff_plus.cooldown = 0.0
	charm_staff_plus.magic_effects = [WeaponCard.MagicEffect.CHARM]
	charm_staff_plus.magic_effect_levels[WeaponCard.MagicEffect.CHARM] = 4
	charm_staff_plus.icon = "💖"
	charm_staff_plus.rarity = 3
	all_weapons.append(charm_staff_plus)
	
	# 冰霜法杖 - 定身
	var ice_staff = WeaponCard.new()
	ice_staff.init("冰霜法杖", "定身敌人", WeaponCard.WeaponType.MAGIC, ["火球法杖"])
	ice_staff.category = WeaponCard.WeaponCategory.WEAPON
	ice_staff.tier = 3
	ice_staff.upgrade_category = "武器"
	ice_staff.damage_modifier = 0.9
	ice_staff.attack_speed_modifier = 0.55
	ice_staff.cooldown = 0.0
	ice_staff.magic_effects = [WeaponCard.MagicEffect.STUN]
	ice_staff.magic_effect_levels[WeaponCard.MagicEffect.STUN] = 2
	ice_staff.icon = "❄️"
	ice_staff.rarity = 2
	all_weapons.append(ice_staff)
	
	# 雷电法杖 - 强力连锁
	var lightning_staff = WeaponCard.new()
	lightning_staff.init("雷电法杖", "强力连锁", WeaponCard.WeaponType.MAGIC, ["火球法杖"])
	lightning_staff.category = WeaponCard.WeaponCategory.WEAPON
	lightning_staff.tier = 3
	lightning_staff.upgrade_category = "武器"
	lightning_staff.damage_modifier = 1.3
	lightning_staff.attack_speed_modifier = 0.45
	lightning_staff.cooldown = 0.0
	lightning_staff.magic_effects = [WeaponCard.MagicEffect.CHAIN]
	lightning_staff.magic_effect_levels[WeaponCard.MagicEffect.CHAIN] = 3
	lightning_staff.icon = "⚡"
	lightning_staff.rarity = 3
	all_weapons.append(lightning_staff)
	
	# 时间法杖
	var time_staff = WeaponCard.new()
	time_staff.init("时间法杖", "时间减速", WeaponCard.WeaponType.MAGIC, ["冰霜法杖"])
	time_staff.category = WeaponCard.WeaponCategory.WEAPON
	time_staff.tier = 4
	time_staff.upgrade_category = "武器"
	time_staff.damage_modifier = 1.0
	time_staff.attack_speed_modifier = 0.5
	time_staff.cooldown = 0.0
	time_staff.magic_effects = [WeaponCard.MagicEffect.STUN]
	time_staff.magic_effect_levels[WeaponCard.MagicEffect.STUN] = 4
	time_staff.health_cost = 5
	time_staff.icon = "⏳"
	time_staff.rarity = 3
	all_weapons.append(time_staff)
	
	# 死神法杖 - 传说级
	var death_staff = WeaponCard.new()
	death_staff.init("死神法杖", "死亡连锁", WeaponCard.WeaponType.MAGIC, ["雷电法杖"])
	death_staff.category = WeaponCard.WeaponCategory.WEAPON
	death_staff.tier = 4
	death_staff.upgrade_category = "武器"
	death_staff.damage_modifier = 1.5
	death_staff.attack_speed_modifier = 0.4
	death_staff.cooldown = 0.0
	death_staff.magic_effects = [WeaponCard.MagicEffect.CHAIN, WeaponCard.MagicEffect.DOT]
	death_staff.magic_effect_levels[WeaponCard.MagicEffect.CHAIN] = 4
	death_staff.magic_effect_levels[WeaponCard.MagicEffect.DOT] = 3
	death_staff.health_cost = 15
	death_staff.icon = "💀"
	death_staff.rarity = 4
	all_weapons.append(death_staff)
	
	# ===== 装备 =====
	
	# 生命药水
	var health_potion = WeaponCard.new()
	health_potion.init("生命药水", "恢复生命", WeaponCard.WeaponType.MELEE)
	health_potion.category = WeaponCard.WeaponCategory.EQUIPMENT
	health_potion.tier = 1
	health_potion.upgrade_category = "buff"
	health_potion.health_modifier = 30
	health_potion.icon = "🧪"
	health_potion.rarity = 1
	all_weapons.append(health_potion)
	
	# 敏捷靴
	var speed_boots = WeaponCard.new()
	speed_boots.init("敏捷靴", "提升速度", WeaponCard.WeaponType.MELEE)
	speed_boots.category = WeaponCard.WeaponCategory.EQUIPMENT
	speed_boots.tier = 1
	speed_boots.upgrade_category = "鞋子"
	speed_boots.speed_modifier = 20
	speed_boots.health_modifier = -5
	speed_boots.icon = "👢"
	speed_boots.rarity = 1
	all_weapons.append(speed_boots)
	
	# 攻击宝石
	var attack_gem = WeaponCard.new()
	attack_gem.init("攻击宝石", "提升伤害", WeaponCard.WeaponType.MELEE)
	attack_gem.category = WeaponCard.WeaponCategory.EQUIPMENT
	attack_gem.tier = 1
	attack_gem.upgrade_category = "特效"
	attack_gem.damage_modifier = 1.15
	attack_gem.health_modifier = -10
	attack_gem.icon = "💎"
	attack_gem.rarity = 1
	all_weapons.append(attack_gem)
	
	var attack_gem_plus = WeaponCard.new()
	attack_gem_plus.init("攻击宝石·精良", "提升伤害", WeaponCard.WeaponType.MELEE, ["攻击宝石"])
	attack_gem_plus.category = WeaponCard.WeaponCategory.EQUIPMENT
	attack_gem_plus.tier = 2
	attack_gem_plus.upgrade_category = "特效"
	attack_gem_plus.damage_modifier = 1.3
	attack_gem_plus.health_modifier = -12
	attack_gem_plus.icon = "💎"
	all_weapons.append(attack_gem_plus)

	# 暴击指环
	var crit_ring = WeaponCard.new()
	crit_ring.init("暴击指环", "提升暴击率", WeaponCard.WeaponType.MELEE)
	crit_ring.category = WeaponCard.WeaponCategory.EQUIPMENT
	crit_ring.tier = 2
	crit_ring.upgrade_category = "戒指"
	crit_ring.critical_bonus = 12
	crit_ring.icon = "💍"
	crit_ring.rarity = 2
	all_weapons.append(crit_ring)

	# 吸血护符
	var lifesteal_charm = WeaponCard.new()
	lifesteal_charm.init("吸血护符", "攻击回复生命", WeaponCard.WeaponType.MELEE)
	lifesteal_charm.category = WeaponCard.WeaponCategory.EQUIPMENT
	lifesteal_charm.tier = 2
	lifesteal_charm.upgrade_category = "buff"
	lifesteal_charm.life_steal_bonus = 6
	lifesteal_charm.icon = "🧿"
	lifesteal_charm.rarity = 2
	all_weapons.append(lifesteal_charm)

	# 寒霜徽章
	var frost_emblem = WeaponCard.new()
	frost_emblem.init("寒霜徽章", "增加冻结几率", WeaponCard.WeaponType.MELEE)
	frost_emblem.category = WeaponCard.WeaponCategory.EQUIPMENT
	frost_emblem.tier = 2
	frost_emblem.upgrade_category = "特效"
	frost_emblem.freeze_bonus = 15
	frost_emblem.icon = "❄️"
	frost_emblem.rarity = 2
	all_weapons.append(frost_emblem)

	# 剧毒之戒
	var poison_ring = WeaponCard.new()
	poison_ring.init("剧毒之戒", "增加中毒几率", WeaponCard.WeaponType.MELEE)
	poison_ring.category = WeaponCard.WeaponCategory.EQUIPMENT
	poison_ring.tier = 2
	poison_ring.upgrade_category = "戒指"
	poison_ring.poison_bonus = 12
	poison_ring.icon = "☠️"
	poison_ring.rarity = 2
	all_weapons.append(poison_ring)

	# 冰霜护盾
	var frost_barrier = WeaponCard.new()
	frost_barrier.init("冰霜护盾", "开启环绕护盾", WeaponCard.WeaponType.MELEE)
	frost_barrier.category = WeaponCard.WeaponCategory.EQUIPMENT
	frost_barrier.tier = 3
	frost_barrier.upgrade_category = "护甲"
	frost_barrier.grants_aura = true
	frost_barrier.icon = "🛡️"
	frost_barrier.rarity = 3
	all_weapons.append(frost_barrier)
	
	# 头盔
	var iron_helmet = WeaponCard.new()
	iron_helmet.init("铁盔", "提高生命上限", WeaponCard.WeaponType.MELEE)
	iron_helmet.category = WeaponCard.WeaponCategory.EQUIPMENT
	iron_helmet.tier = 1
	iron_helmet.upgrade_category = "头盔"
	iron_helmet.health_modifier = 15
	iron_helmet.icon = "⛑️"
	all_weapons.append(iron_helmet)
	
	var steel_helmet = WeaponCard.new()
	steel_helmet.init("钢盔", "显著提高生命上限", WeaponCard.WeaponType.MELEE, ["铁盔"])
	steel_helmet.category = WeaponCard.WeaponCategory.EQUIPMENT
	steel_helmet.tier = 2
	steel_helmet.upgrade_category = "头盔"
	steel_helmet.health_modifier = 30
	steel_helmet.icon = "⛑️"
	all_weapons.append(steel_helmet)
	
	# 护甲
	var leather_armor = WeaponCard.new()
	leather_armor.init("皮甲", "提高生命上限，略降速度", WeaponCard.WeaponType.MELEE)
	leather_armor.category = WeaponCard.WeaponCategory.EQUIPMENT
	leather_armor.tier = 1
	leather_armor.upgrade_category = "护甲"
	leather_armor.health_modifier = 20
	leather_armor.speed_modifier = -5
	leather_armor.icon = "🧥"
	all_weapons.append(leather_armor)
	
	var plate_armor = WeaponCard.new()
	plate_armor.init("板甲", "大幅提高生命上限", WeaponCard.WeaponType.MELEE, ["皮甲"])
	plate_armor.category = WeaponCard.WeaponCategory.EQUIPMENT
	plate_armor.tier = 2
	plate_armor.upgrade_category = "护甲"
	plate_armor.health_modifier = 40
	plate_armor.speed_modifier = -10
	plate_armor.icon = "🧥"
	all_weapons.append(plate_armor)
	
	# 鞋子升级
	var swift_boots = WeaponCard.new()
	swift_boots.init("迅捷靴", "大幅提升速度", WeaponCard.WeaponType.MELEE, ["敏捷靴"])
	swift_boots.category = WeaponCard.WeaponCategory.EQUIPMENT
	swift_boots.tier = 2
	swift_boots.upgrade_category = "鞋子"
	swift_boots.speed_modifier = 35
	swift_boots.health_modifier = -8
	swift_boots.icon = "👟"
	all_weapons.append(swift_boots)
	
	# 戒指升级
	var crit_ring_plus = WeaponCard.new()
	crit_ring_plus.init("暴击指环·精良", "更高暴击率", WeaponCard.WeaponType.MELEE, ["暴击指环"])
	crit_ring_plus.category = WeaponCard.WeaponCategory.EQUIPMENT
	crit_ring_plus.tier = 3
	crit_ring_plus.upgrade_category = "戒指"
	crit_ring_plus.critical_bonus = 20
	crit_ring_plus.icon = "💍"
	all_weapons.append(crit_ring_plus)
	
	# 吸血升级
	var lifesteal_charm_plus = WeaponCard.new()
	lifesteal_charm_plus.init("吸血护符·精良", "更高吸血比例", WeaponCard.WeaponType.MELEE, ["吸血护符"])
	lifesteal_charm_plus.category = WeaponCard.WeaponCategory.EQUIPMENT
	lifesteal_charm_plus.tier = 3
	lifesteal_charm_plus.upgrade_category = "buff"
	lifesteal_charm_plus.life_steal_bonus = 12
	lifesteal_charm_plus.icon = "🧿"
	all_weapons.append(lifesteal_charm_plus)
	
	# 攻速升级
	var haste_charm = WeaponCard.new()
	haste_charm.init("疾风护符", "提升攻击速度", WeaponCard.WeaponType.MELEE)
	haste_charm.category = WeaponCard.WeaponCategory.EQUIPMENT
	haste_charm.tier = 2
	haste_charm.upgrade_category = "buff"
	haste_charm.attack_speed_modifier = 1.2
	haste_charm.icon = "💨"
	all_weapons.append(haste_charm)
	
	var haste_charm_plus = WeaponCard.new()
	haste_charm_plus.init("疾风护符·精良", "显著提升攻击速度", WeaponCard.WeaponType.MELEE, ["疾风护符"])
	haste_charm_plus.category = WeaponCard.WeaponCategory.EQUIPMENT
	haste_charm_plus.tier = 3
	haste_charm_plus.upgrade_category = "buff"
	haste_charm_plus.attack_speed_modifier = 1.35
	haste_charm_plus.icon = "💨"
	all_weapons.append(haste_charm_plus)
	
	# 冰冻升级
	var frost_emblem_plus = WeaponCard.new()
	frost_emblem_plus.init("寒霜徽章·精良", "更高冰冻几率", WeaponCard.WeaponType.MELEE, ["寒霜徽章"])
	frost_emblem_plus.category = WeaponCard.WeaponCategory.EQUIPMENT
	frost_emblem_plus.tier = 3
	frost_emblem_plus.upgrade_category = "特效"
	frost_emblem_plus.freeze_bonus = 25
	frost_emblem_plus.icon = "❄️"
	all_weapons.append(frost_emblem_plus)
	
	# ===== 新增装备 =====
	
	# 再生戒指 - 生命回复
	var regen_ring = WeaponCard.new()
	regen_ring.init("再生戒指", "每回合恢复额外生命", WeaponCard.WeaponType.MELEE)
	regen_ring.category = WeaponCard.WeaponCategory.EQUIPMENT
	regen_ring.tier = 2
	regen_ring.upgrade_category = "戒指"
	regen_ring.health_modifier = 10
	regen_ring.life_steal_bonus = 3
	regen_ring.icon = "💚"
	all_weapons.append(regen_ring)
	
	# 狂战士护腕 - 高风险高收益
	var berserker_bracer = WeaponCard.new()
	berserker_bracer.init("狂战士护腕", "大幅提升伤害，但降低防御", WeaponCard.WeaponType.MELEE)
	berserker_bracer.category = WeaponCard.WeaponCategory.EQUIPMENT
	berserker_bracer.tier = 2
	berserker_bracer.upgrade_category = "buff"
	berserker_bracer.damage_modifier = 1.35
	berserker_bracer.health_modifier = -20
	berserker_bracer.icon = "💪"
	all_weapons.append(berserker_bracer)
	
	# 猎人印记 - 对精英加成
	var hunter_mark = WeaponCard.new()
	hunter_mark.init("猎人印记", "增加暴击和暴击伤害", WeaponCard.WeaponType.MELEE)
	hunter_mark.category = WeaponCard.WeaponCategory.EQUIPMENT
	hunter_mark.tier = 3
	hunter_mark.upgrade_category = "buff"
	hunter_mark.critical_bonus = 15
	hunter_mark.damage_modifier = 1.1
	hunter_mark.icon = "🎯"
	all_weapons.append(hunter_mark)
	
	# 幽灵斗篷 - 闪避增强
	var ghost_cloak = WeaponCard.new()
	ghost_cloak.init("幽灵斗篷", "提升移动速度和闪避效果", WeaponCard.WeaponType.MELEE)
	ghost_cloak.category = WeaponCard.WeaponCategory.EQUIPMENT
	ghost_cloak.tier = 2
	ghost_cloak.upgrade_category = "护甲"
	ghost_cloak.speed_modifier = 30
	ghost_cloak.health_modifier = -5
	ghost_cloak.icon = "👻"
	all_weapons.append(ghost_cloak)
	
	_auto_assign_rarity()
	
	print("武器库初始化完成: " + str(all_weapons.size()) + " 种武器")

func _auto_assign_rarity():
	for card in all_weapons:
		var score = 0
		if card.damage_modifier != 1.0:
			score += 1
		if card.attack_speed_modifier != 1.0:
			score += 1
		if card.health_modifier != 0:
			score += 1
		if card.speed_modifier != 0:
			score += 1
		if card.critical_bonus > 0:
			score += 1
		if card.life_steal_bonus > 0:
			score += 1
		if card.freeze_bonus > 0:
			score += 1
		if card.poison_bonus > 0:
			score += 1
		if card.grants_aura:
			score += 2
		
		score += card.melee_effects.size()
		score += card.ranged_effects.size()
		score += card.magic_effects.size()
		
		if score <= 0:
			card.rarity = 1
		elif score == 1:
			card.rarity = 2
		elif score <= 3:
			card.rarity = 3
		else:
			card.rarity = 4

func draw_three_cards(player: Player = null) -> Array[WeaponCard]:
	var pool: Array[WeaponCard] = []
	
	if player == null:
		pool = all_weapons.duplicate()
	else:
		var inventory_names: Array[String] = []
		var current_type = null
		var current_tier = 0
		var player_melee_effects: Dictionary = {}
		var player_ranged_effects: Dictionary = {}
		var player_magic_effects: Dictionary = {}
		
		for w in player.active_effects:
			inventory_names.append(w.card_name)
			if w.category == WeaponCard.WeaponCategory.WEAPON:
				current_type = w.weapon_type
				current_tier = max(current_tier, w.tier)
				
			for effect in w.melee_effects:
				var current_level = w.melee_effect_levels.get(effect, 0)
				if player_melee_effects.get(effect, 0) < current_level:
					player_melee_effects[effect] = current_level
			
			for effect in w.ranged_effects:
				var current_level = w.ranged_effect_levels.get(effect, 0)
				if player_ranged_effects.get(effect, 0) < current_level:
					player_ranged_effects[effect] = current_level
			
			for effect in w.magic_effects:
				var current_level = w.magic_effect_levels.get(effect, 0)
				if player_magic_effects.get(effect, 0) < current_level:
					player_magic_effects[effect] = current_level
		
		# 优先选择当前武器类型的升级
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
				var is_valid_upgrade = _meets_prerequisite(w, inventory_names)

				# 检查是否包含已经达到高等级的特效
				if is_valid_upgrade:
					var has_higher_effect = false
					if w.weapon_type == WeaponCard.WeaponType.MELEE:
						for effect in w.melee_effects:
							var new_level = w.melee_effect_levels.get(effect, 0)
							if new_level > player_melee_effects.get(effect, 0):
								has_higher_effect = true
								break

					elif w.weapon_type == WeaponCard.WeaponType.RANGED:
						for effect in w.ranged_effects:
							var new_level = w.ranged_effect_levels.get(effect, 0)
							if new_level > player_ranged_effects.get(effect, 0):
								has_higher_effect = true
								break

					elif w.weapon_type == WeaponCard.WeaponType.MAGIC:
						for effect in w.magic_effects:
							var new_level = w.magic_effect_levels.get(effect, 0)
							if new_level > player_magic_effects.get(effect, 0):
								has_higher_effect = true
								break

					if w.tier <= current_tier and not has_higher_effect:
						is_valid_upgrade = false

				if is_valid_upgrade:
					if w.tier == current_tier + 1 and w.is_upgrade_for(inventory_names):
						upgrade_pool.append(w)
					elif w.tier == current_tier:
						sidegrade_pool.append(w)
			else:
				if w.rarity <= 2 and not inventory_names.has(w.card_name) and _meets_prerequisite(w, inventory_names):
					off_type_pool.append(w)

		# 权重组合：升级 > 同阶补强 > 转系
		for w in upgrade_pool:
			pool.append(w)
			pool.append(w)
		for w in sidegrade_pool:
			pool.append(w)
		for w in off_type_pool:
			pool.append(w)
		
		# 装备池：中等概率混入
		for w in equipment_pool:
			if randf() < 0.35:
				pool.append(w)
	
	if pool.is_empty():
		pool = all_weapons.duplicate()
	
	pool.shuffle()
	
	var drawn_cards: Array[WeaponCard] = []
	var selected_names: Array[String] = []
	
	for card in pool:
		if drawn_cards.size() >= 3:
			break
		
		if not selected_names.has(card.card_name):
			drawn_cards.append(card)
			selected_names.append(card.card_name)
	
	if drawn_cards.size() < 3:
		var backup_pool = all_weapons.duplicate()
		backup_pool.shuffle()
		for card in backup_pool:
			if drawn_cards.size() >= 3:
				break
			if not selected_names.has(card.card_name):
				drawn_cards.append(card)
				selected_names.append(card.card_name)
	
	return drawn_cards

func _meets_prerequisite(card: WeaponCard, inventory_names: Array[String]) -> bool:
	if prerequisites.has(card.card_name):
		return inventory_names.has(prerequisites[card.card_name])
	if card.tags.size() == 0:
		return true
	for tag in card.tags:
		if inventory_names.has(tag):
			return true
	return false

func get_random_weapon() -> WeaponCard:
	if all_weapons.size() == 0:
		return null
	return all_weapons.pick_random()
