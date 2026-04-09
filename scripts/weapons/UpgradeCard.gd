class_name UpgradeCard
extends Resource
## 升级卡 — 对已有装备/武器的增量修改
## 出现在武器选择界面中，与新武器/装备混合抽取

enum TradeoffType {
	NONE,
	HEALTH_COST,
	SPEED,
	HEALTH_MAX,
}

enum UpgradeDirection {
	NONE,
	DAMAGE,
	SPEED,
	CRIT,
	LEECH,
	DEFENSE,
	EFFECT,
}

var target_ref: WeaponCard         ## 指向 active_effects 中的卡牌实例
var display_name: String           ## 展示名称（如 "强化·铁剑"）

# ═══════════════════════════════════════════════
# 数值增量（叠加到 target_ref 对应属性上）
# ═══════════════════════════════════════════════
var damage_delta: float = 0.0
var attack_speed_delta: float = 0.0
var health_delta: float = 0.0
var speed_delta: float = 0.0
var crit_delta: float = 0.0
var life_steal_delta: float = 0.0
var freeze_delta: float = 0.0
var poison_delta: float = 0.0

# 特效等级提升 { effect_enum_value: level_delta }
var effect_upgrades: Dictionary = {}

# ═══════════════════════════════════════════════
# 副作用
# ═══════════════════════════════════════════════
var tradeoff_type: TradeoffType = TradeoffType.NONE
var tradeoff_value: float = 0.0

# ═══════════════════════════════════════════════
# 展示
# ═══════════════════════════════════════════════
var upgrade_direction: UpgradeDirection = UpgradeDirection.NONE
var icon: String = "⬆"
var rarity: int = 1
var is_upgrade_card: bool = true

var card_name: String:
	get: return display_name
var category: int = 0
var tier: int = 1
var upgrade_category: String = "强化"
var description: String:
	get: return _build_description()

func _build_description() -> String:
	var parts: Array[String] = []
	if damage_delta > 0:
		parts.append("伤害+" + _fmt_pct(damage_delta))
	if attack_speed_delta > 0:
		parts.append("攻速+" + _fmt_pct(attack_speed_delta))
	if health_delta > 0:
		parts.append("生命+" + str(int(health_delta)))
	if speed_delta > 0:
		parts.append("速度+" + str(int(speed_delta)))
	if crit_delta > 0:
		parts.append("暴击+" + str(int(crit_delta)) + "%")
	if life_steal_delta > 0:
		parts.append("吸血+" + str(int(life_steal_delta)) + "%")
	if freeze_delta > 0:
		parts.append("冰冻+" + str(int(freeze_delta)) + "%")
	if poison_delta > 0:
		parts.append("中毒+" + str(int(poison_delta)) + "%")
	for fx_key in effect_upgrades:
		parts.append("特效强化")

	if tradeoff_type != TradeoffType.NONE:
		match tradeoff_type:
			TradeoffType.HEALTH_COST:
				parts.append("消耗生命+" + str(int(tradeoff_value)))
			TradeoffType.SPEED:
				parts.append("速度" + str(int(tradeoff_value)))
			TradeoffType.HEALTH_MAX:
				parts.append("生命" + str(int(tradeoff_value)))

	return "  ".join(parts) if parts.size() > 0 else "强化"

static func _fmt_pct(val: float) -> String:
	return str(int(val * 100)) + "%"
