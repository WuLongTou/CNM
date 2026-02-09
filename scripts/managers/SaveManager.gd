class_name SaveManager
extends Node

static var instance: SaveManager

const SAVE_FILE_PATH = "user://zvs_save.dat"

# 成就定义
const ACHIEVEMENTS = {
	"first_blood": {"name": "初次见血", "desc": "击杀第一只僵尸", "target": 1, "stat": "total_kills"},
	"zombie_slayer": {"name": "僵尸杀手", "desc": "累计击杀100只僵尸", "target": 100, "stat": "total_kills"},
	"zombie_hunter": {"name": "僵尸猎人", "desc": "累计击杀500只僵尸", "target": 500, "stat": "total_kills"},
	"survivor": {"name": "幸存者", "desc": "坚持5回合", "target": 5, "stat": "max_round"},
	"veteran": {"name": "老兵", "desc": "坚持10回合", "target": 10, "stat": "max_round"},
	"legend": {"name": "传奇", "desc": "坚持20回合", "target": 20, "stat": "max_round"},
	"score_1000": {"name": "千分达人", "desc": "单局得分1000", "target": 1000, "stat": "high_score"},
	"score_5000": {"name": "高分选手", "desc": "单局得分5000", "target": 5000, "stat": "high_score"},
	"dedicated": {"name": "坚持不懈", "desc": "游玩10局游戏", "target": 10, "stat": "games_played"},
}

var save_data: Dictionary = {
	"high_score": 0,
	"total_kills": 0,
	"games_played": 0,
	"total_rounds_survived": 0,
	"max_round": 0,
	"unlocked_achievements": []
}

func _ready():
	instance = self
	_load_game()

func _exit_tree():
	instance = null

static func save_high_score(new_score: int):
	if instance:
		if new_score > instance.save_data["high_score"]:
			instance.save_data["high_score"] = new_score
			instance._save_game()

static func add_kill():
	if instance:
		instance.save_data["total_kills"] += 1
		instance._save_game()

static func add_game_played():
	if instance:
		instance.save_data["games_played"] += 1
		instance._save_game()

static func add_rounds_survived(rounds: int):
	if instance:
		instance.save_data["total_rounds_survived"] += rounds
		if rounds > instance.save_data.get("max_round", 0):
			instance.save_data["max_round"] = rounds
		instance._check_achievements()
		instance._save_game()

static func check_and_unlock_achievements() -> Array:
	if instance:
		return instance._check_achievements()
	return []

static func get_achievement_progress(achievement_id: String) -> Dictionary:
	if not instance or not ACHIEVEMENTS.has(achievement_id):
		return {}
	
	var achievement = ACHIEVEMENTS[achievement_id]
	var current_value = instance.save_data.get(achievement["stat"], 0)
	var unlocked = instance.save_data.get("unlocked_achievements", []).has(achievement_id)
	
	return {
		"id": achievement_id,
		"name": achievement["name"],
		"desc": achievement["desc"],
		"current": current_value,
		"target": achievement["target"],
		"unlocked": unlocked,
		"progress": min(1.0, float(current_value) / float(achievement["target"]))
	}

static func get_all_achievements() -> Array:
	var result = []
	for id in ACHIEVEMENTS.keys():
		result.append(get_achievement_progress(id))
	return result

func _save_game():
	var file = FileAccess.open(SAVE_FILE_PATH, FileAccess.WRITE)
	if file:
		var json_string = JSON.stringify(save_data)
		file.store_string(json_string)
		print("游戏已保存: " + SAVE_FILE_PATH)

func _load_game():
	if FileAccess.file_exists(SAVE_FILE_PATH):
		var file = FileAccess.open(SAVE_FILE_PATH, FileAccess.READ)
		if file:
			var json_string = file.get_as_text()
			var json = JSON.new()
			var error = json.parse(json_string)
			if error == OK:
				save_data = json.data
			print("游戏已加载: " + SAVE_FILE_PATH)

static func get_high_score() -> int:
	if instance:
		return instance.save_data.get("high_score", 0)
	return 0

static func get_total_kills() -> int:
	if instance:
		return instance.save_data.get("total_kills", 0)
	return 0

static func get_games_played() -> int:
	if instance:
		return instance.save_data.get("games_played", 0)
	return 0

static func get_total_rounds_survived() -> int:
	if instance:
		return instance.save_data.get("total_rounds_survived", 0)
	return 0

func clear_save_data():
	save_data = {
		"high_score": 0,
		"total_kills": 0,
		"games_played": 0,
		"total_rounds_survived": 0,
		"max_round": 0,
		"unlocked_achievements": []
	}
	_save_game()

func _check_achievements() -> Array:
	var newly_unlocked = []
	var unlocked = save_data.get("unlocked_achievements", [])
	
	for id in ACHIEVEMENTS.keys():
		if unlocked.has(id):
			continue
		
		var achievement = ACHIEVEMENTS[id]
		var current_value = save_data.get(achievement["stat"], 0)
		
		if current_value >= achievement["target"]:
			unlocked.append(id)
			newly_unlocked.append({
				"id": id,
				"name": achievement["name"],
				"desc": achievement["desc"]
			})
			print("🏆 成就解锁: " + achievement["name"] + " - " + achievement["desc"])
	
	save_data["unlocked_achievements"] = unlocked
	return newly_unlocked
