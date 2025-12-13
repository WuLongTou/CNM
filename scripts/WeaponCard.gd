class_name WeaponCard
extends Resource

enum WeaponType {
	MELEE,    # 近战武器
	RANGED,   # 远程武器
	MAGIC,    # 魔法武器
	SUPPORT   # 辅助武器
}

enum EffectType {
	DAMAGE_BOOST,      # 攻击力提升
	HEALTH_BOOST,      # 生命值提升
	SPEED_BOOST,       # 移动速度提升
	MULTI_SHOT,        # 多重射击
	PIERCING,          # 穿透攻击
	AREA_DAMAGE,       # 范围伤害
	LIFE_STEAL,        # 生命偷取
	CRITICAL_CHANCE,   # 暴击几率
	FREEZE,            # 冰冻效果
	POISON             # 中毒效果
}

@export var card_name: String
@export var description: String
@export var weapon_type: WeaponType
@export var effect_type: EffectType
@export var effect_value: float
@export var duration: int = -1  # -1表示永久效果
@export var icon: Texture2D
@export var rarity: int = 1  # 1-普通, 2-稀有, 3-史诗, 4-传说

func _init(name: String = "", desc: String = "", type: WeaponType = WeaponType.MELEE, 
          effect: EffectType = EffectType.DAMAGE_BOOST, value: float = 0.0):
	card_name = name
	description = desc
	weapon_type = type
	effect_type = effect
	effect_value = value

func get_effect_description() -> String:
	match effect_type:
		EffectType.DAMAGE_BOOST:
			return "攻击力 +" + str(effect_value)
		EffectType.HEALTH_BOOST:
			return "生命值 +" + str(effect_value)
		EffectType.SPEED_BOOST:
			return "移动速度 +" + str(effect_value) + "%"
		EffectType.MULTI_SHOT:
			return "发射 " + str(effect_value) + " 发子弹"
		EffectType.PIERCING:
			return "穿透 " + str(effect_value) + " 个敌人"
		EffectType.AREA_DAMAGE:
			return "范围伤害 " + str(effect_value)
		EffectType.LIFE_STEAL:
			return "生命偷取 " + str(effect_value) + "%"
		EffectType.CRITICAL_CHANCE:
			return "暴击几率 +" + str(effect_value) + "%"
		EffectType.FREEZE:
			return "冰冻敌人 " + str(effect_value) + " 秒"
		EffectType.POISON:
			return "中毒伤害 " + str(effect_value) + "/秒"
		_:
			return "未知效果"
