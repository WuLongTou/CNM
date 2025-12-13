class_name WeaponDeck
extends Node

var all_weapons: Array[WeaponCard] = []
var current_deck: Array[WeaponCard] = []
var prerequisites: Dictionary = {}

func _ready():
	_initialize_weapon_deck()
	_setup_prerequisites()

func _setup_prerequisites():
	# 定义升级路线： 后续武器 -> 前置武器
	prerequisites = {
		"战斧": "铁剑",
		"吸血剑": "铁剑",
		"狂战士之怒": "战斧",
		"连弩": "弓箭",
		"穿甲弩": "连弩",
		"爆裂弩": "穿甲弩",
		"狙击弩": "穿甲弩",
		"毒箭": "弓箭",
		"雷电法杖": "火球法杖",
		"冰霜法杖": "火球法杖",
		"时间法杖": "冰霜法杖",
		"混沌法杖": "雷电法杖",
		"护盾发生器": "生命药剂",
		"狂暴药剂": "敏捷靴",
		"多重射击": "连弩",  # 连弩可以解锁多重射击被动
	}

func _initialize_weapon_deck():
	# 近战武器
	all_weapons.append(WeaponCard.new("铁剑", "基础近战武器，提升攻击力", WeaponCard.WeaponType.MELEE, WeaponCard.EffectType.DAMAGE_BOOST, 10.0))
	all_weapons.append(WeaponCard.new("战斧", "重型近战武器，大幅提升攻击力", WeaponCard.WeaponType.MELEE, WeaponCard.EffectType.DAMAGE_BOOST, 20.0))
	all_weapons.append(WeaponCard.new("吸血剑", "攻击时恢复生命值", WeaponCard.WeaponType.MELEE, WeaponCard.EffectType.LIFE_STEAL, 15.0))
	all_weapons.append(WeaponCard.new("雷电之刃", "攻击附带电击效果", WeaponCard.WeaponType.MELEE, WeaponCard.EffectType.AREA_DAMAGE, 12.0))
	all_weapons.append(WeaponCard.new("狂战士之怒", "大幅提升攻击速度", WeaponCard.WeaponType.MELEE, WeaponCard.EffectType.SPEED_BOOST, 40.0))
	
	# 远程武器
	all_weapons.append(WeaponCard.new("弓箭", "基础远程武器", WeaponCard.WeaponType.RANGED, WeaponCard.EffectType.DAMAGE_BOOST, 8.0))
	all_weapons.append(WeaponCard.new("霰弹枪", "一次发射多枚子弹", WeaponCard.WeaponType.RANGED, WeaponCard.EffectType.MULTI_SHOT, 3.0))
	all_weapons.append(WeaponCard.new("连弩", "发射多发箭矢", WeaponCard.WeaponType.RANGED, WeaponCard.EffectType.MULTI_SHOT, 2.0))
	all_weapons.append(WeaponCard.new("穿甲弩", "箭矢可以穿透敌人", WeaponCard.WeaponType.RANGED, WeaponCard.EffectType.PIERCING, 2.0))
	all_weapons.append(WeaponCard.new("爆裂弩", "箭矢爆炸造成范围伤害", WeaponCard.WeaponType.RANGED, WeaponCard.EffectType.AREA_DAMAGE, 18.0))
	all_weapons.append(WeaponCard.new("狙击弩", "超高伤害远程武器", WeaponCard.WeaponType.RANGED, WeaponCard.EffectType.DAMAGE_BOOST, 35.0))
	all_weapons.append(WeaponCard.new("毒箭", "箭矢附带剧毒", WeaponCard.WeaponType.RANGED, WeaponCard.EffectType.POISON, 8.0))
	
	# 魔法武器
	all_weapons.append(WeaponCard.new("火球法杖", "发射火球造成范围伤害", WeaponCard.WeaponType.MAGIC, WeaponCard.EffectType.AREA_DAMAGE, 15.0))
	all_weapons.append(WeaponCard.new("冰霜法杖", "冰冻敌人", WeaponCard.WeaponType.MAGIC, WeaponCard.EffectType.FREEZE, 2.0))
	all_weapons.append(WeaponCard.new("毒素法杖", "使敌人中毒", WeaponCard.WeaponType.MAGIC, WeaponCard.EffectType.POISON, 5.0))
	all_weapons.append(WeaponCard.new("雷电法杖", "召唤闪电攻击", WeaponCard.WeaponType.MAGIC, WeaponCard.EffectType.AREA_DAMAGE, 25.0))
	all_weapons.append(WeaponCard.new("治疗法杖", "战斗中持续恢复生命", WeaponCard.WeaponType.MAGIC, WeaponCard.EffectType.LIFE_STEAL, 25.0))
	all_weapons.append(WeaponCard.new("时间法杖", "减缓敌人移动速度", WeaponCard.WeaponType.MAGIC, WeaponCard.EffectType.FREEZE, 3.0))
	all_weapons.append(WeaponCard.new("混沌法杖", "随机魔法效果", WeaponCard.WeaponType.MAGIC, WeaponCard.EffectType.CRITICAL_CHANCE, 30.0))
	
	# 辅助武器
	all_weapons.append(WeaponCard.new("生命药剂", "增加最大生命值", WeaponCard.WeaponType.SUPPORT, WeaponCard.EffectType.HEALTH_BOOST, 50.0))
	all_weapons.append(WeaponCard.new("敏捷靴", "提升移动速度", WeaponCard.WeaponType.SUPPORT, WeaponCard.EffectType.SPEED_BOOST, 25.0))
	all_weapons.append(WeaponCard.new("幸运护符", "提升暴击几率", WeaponCard.WeaponType.SUPPORT, WeaponCard.EffectType.CRITICAL_CHANCE, 20.0))
	all_weapons.append(WeaponCard.new("护盾发生器", "大幅增加生命值", WeaponCard.WeaponType.SUPPORT, WeaponCard.EffectType.HEALTH_BOOST, 100.0))
	all_weapons.append(WeaponCard.new("狂暴药剂", "攻击力和速度双提升", WeaponCard.WeaponType.SUPPORT, WeaponCard.EffectType.DAMAGE_BOOST, 15.0))
	all_weapons.append(WeaponCard.new("反射护甲", "反弹部分伤害", WeaponCard.WeaponType.SUPPORT, WeaponCard.EffectType.LIFE_STEAL, 10.0))
	all_weapons.append(WeaponCard.new("多重射击", "同时发射多发子弹", WeaponCard.WeaponType.SUPPORT, WeaponCard.EffectType.MULTI_SHOT, 5.0))
	all_weapons.append(WeaponCard.new("冰霜护盾", "召唤围绕自身的冰球，造成伤害并减速敌人", WeaponCard.WeaponType.SUPPORT, WeaponCard.EffectType.FREEZE, 1.0))

	# 设置稀有度
	for i in range(all_weapons.size()):
		var weapon = all_weapons[i]
		if i < 8:  # 前8个是普通武器
			weapon.rarity = 1
		elif i < 16:  # 中间8个是稀有武器
			weapon.rarity = 2
		elif i < 20:  # 4个史诗武器
			weapon.rarity = 3
		else:  # 剩余的是传说武器
			weapon.rarity = 4
	
	_shuffle_deck()

func _shuffle_deck():
	current_deck = all_weapons.duplicate()
	current_deck.shuffle()

func draw_three_cards(player: Player = null) -> Array[WeaponCard]:
	var pool: Array[WeaponCard] = []
	
	if player == null:
		# 如果没有玩家引用，回退到随机模式
		pool = all_weapons.duplicate()
	else:
		# 获取玩家现有武器名称
		var inventory_names: Array[String] = []
		for w in player.active_effects:
			inventory_names.append(w.card_name)
			
		for w in all_weapons:
			# 1. 检查前置条件
			if prerequisites.has(w.card_name):
				var required = prerequisites[w.card_name]
				if not inventory_names.has(required):
					continue # 玩家没有前置武器，跳过此升级
			
			# 2. 基础武器总是可以出现 (不在prerequisites的key里的)
			# 但为了避免稀释池子，如果已经拥有了基础武器，降低其再次出现的权重(除非是想叠属性)
			
			# 添加到池子
			pool.append(w)
			
			# 3. 权重加成：如果这是一个可用的升级（有前置），增加权重
			if prerequisites.has(w.card_name):
				pool.append(w) # 双倍权重
				pool.append(w) # 三倍权重
				
			# 4. 协同/策略加成 (简单示例)
			# 如果玩家有"多重射击"，增加"连弩"或"霰弹枪"权重
			if inventory_names.has("多重射击") and (w.card_name == "连弩" or w.card_name == "霰弹枪"):
				pool.append(w)
	
	# 如果池子为空（极端情况），使用全武器
	if pool.size() == 0:
		pool = all_weapons.duplicate()

	pool.shuffle()
	
	var drawn_cards: Array[WeaponCard] = []
	var selected_names: Array[String] = []
	
	for card in pool:
		if drawn_cards.size() >= 3:
			break
			
		# 确保一次抽卡中不出现重复的卡
		if not selected_names.has(card.card_name):
			drawn_cards.append(card)
			selected_names.append(card.card_name)
	
	# 如果不足3张，从全库里补
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

func get_random_weapon() -> WeaponCard:
	if all_weapons.size() == 0:
		return null
	return all_weapons[randi() % all_weapons.size()]
