namespace ZombieWars.Core;

using System;
using Godot;
using Godot.Collections;

public partial class SaveManager : Node
{
    public static SaveManager? Instance { get; private set; }
    private const string SAVE_FILE_PATH = "user://zvs_save.dat";

    private Godot.Collections.Dictionary _saveData = new();
    private bool _dirty;
    private float _flushTimer;
    private const float FlushInterval = 3.0f; // 节流：最多每 3 秒写盘一次

    public override void _Ready()
    {
        Instance = this;
        _LoadGame();
    }

    public override void _ExitTree()
    {
        Instance = null;
    }

    public override void _Process(double delta)
    {
        // 脏标志节流：累积变更达到间隔后统一写盘，避免每次击杀都 IO
        if (_dirty)
        {
            _flushTimer -= (float)delta;
            if (_flushTimer <= 0)
            {
                _dirty = false;
                _flushTimer = 0;
                _SaveGame();
            }
        }
    }

    /// <summary>标记数据已变更，延迟写盘。</summary>
    private void _MarkDirty()
    {
        _dirty = true;
        if (_flushTimer <= 0)
            _flushTimer = FlushInterval;
    }

    /// <summary>立即写盘（用于游戏结束/退出等必须即时保存的场景）。</summary>
    public static void Flush()
    {
        if (Instance == null || !Instance._dirty) return;
        Instance._dirty = false;
        Instance._flushTimer = 0;
        Instance._SaveGame();
    }

    public static void SaveHighScore(int newScore)
    {
        if (Instance == null) return;
        var current = Instance._saveData.ContainsKey("high_score")
            ? (int)Instance._saveData["high_score"]
            : 0;
        if (newScore > current)
        {
            Instance._saveData["high_score"] = newScore;
            Instance._SaveGame();
        }
    }

    public static int GetHighScore()
    {
        if (Instance == null) return 0;
        return Instance._saveData.ContainsKey("high_score")
            ? (int)Instance._saveData["high_score"]
            : 0;
    }

    public static void AddKill()
    {
        if (Instance == null) return;
        int kills = Instance._saveData.ContainsKey("total_kills")
            ? (int)Instance._saveData["total_kills"]
            : 0;
        Instance._saveData["total_kills"] = kills + 1;
        Instance._MarkDirty();
    }

    public static void AddGamePlayed()
    {
        if (Instance == null) return;
        int games = Instance._saveData.ContainsKey("games_played")
            ? (int)Instance._saveData["games_played"]
            : 0;
        Instance._saveData["games_played"] = games + 1;
        Instance._SaveGame();
    }

    public static void AddRoundsSurvived(int rounds)
    {
        if (Instance == null) return;
        int total = Instance._saveData.ContainsKey("total_rounds_survived")
            ? (int)Instance._saveData["total_rounds_survived"]
            : 0;
        Instance._saveData["total_rounds_survived"] = total + rounds;
        int maxRound = Instance._saveData.ContainsKey("max_round")
            ? (int)Instance._saveData["max_round"]
            : 0;
        if (rounds > maxRound)
            Instance._saveData["max_round"] = rounds;
        Instance._CheckAchievements();
        Instance._SaveGame();
    }

    // ══ 跨局成长：金币 + 永久升级（meta progression）══

    public static void AddCoins(int amount)
    {
        if (Instance == null || amount <= 0) return;
        int coins = Instance._saveData.ContainsKey("coins") ? (int)Instance._saveData["coins"] : 0;
        Instance._saveData["coins"] = coins + amount;
        Instance._MarkDirty();
    }

    public static int GetCoins()
    {
        if (Instance == null) return 0;
        return Instance._saveData.ContainsKey("coins") ? (int)Instance._saveData["coins"] : 0;
    }

    public static int GetPermLevel(string key)
    {
        if (Instance == null) return 0;
        string k = $"perm_{key}_level";
        return Instance._saveData.ContainsKey(k) ? (int)Instance._saveData[k] : 0;
    }

    public static int GetPermCost(string key) => (GetPermLevel(key) + 1) * 100;

    public static bool UpgradePerm(string key)
    {
        if (Instance == null) return false;
        int cost = GetPermCost(key);
        int coins = GetCoins();
        if (coins < cost) return false;
        Instance._saveData["coins"] = coins - cost;
        string k = $"perm_{key}_level";
        Instance._saveData[k] = GetPermLevel(key) + 1;
        Instance._SaveGame();
        return true;
    }

    /// <summary>应用永久升级到玩家（新局开始时调用）：血+20/级、伤+10%/级、速+5%/级。</summary>
    public static void ApplyPermBuffs(ZombieWars.Player.Player player)
    {
        if (Instance == null || player == null) return;
        int hpLevel = GetPermLevel("health");
        int dmgLevel = GetPermLevel("damage");
        int spdLevel = GetPermLevel("speed");
        if (hpLevel > 0)
        {
            player.MaxHealth += hpLevel * 20;
            player.BaseMaxHealth = player.MaxHealth;
            player.CurrentHealth = player.MaxHealth;
        }
        if (dmgLevel > 0)
            player.BaseDamage *= 1.0f + dmgLevel * 0.1f;
        if (spdLevel > 0)
            player.BaseSpeed *= 1.0f + spdLevel * 0.05f;
    }

    private Godot.Collections.Array _CheckAchievements()
    {
        var unlocked = _saveData.ContainsKey("unlocked_achievements")
            ? (Godot.Collections.Array)_saveData["unlocked_achievements"]
            : new Godot.Collections.Array();
        var newlyUnlocked = new Godot.Collections.Array();
        int kills = _saveData.ContainsKey("total_kills")
            ? (int)_saveData["total_kills"]
            : 0;
        int maxRound = _saveData.ContainsKey("max_round")
            ? (int)_saveData["max_round"]
            : 0;
        int score = _saveData.ContainsKey("high_score")
            ? (int)_saveData["high_score"]
            : 0;
        int games = _saveData.ContainsKey("games_played")
            ? (int)_saveData["games_played"]
            : 0;

        if (kills >= 1 && !_ArrayContains(unlocked, "first_blood"))
            newlyUnlocked.Add("first_blood");
        if (kills >= 100 && !_ArrayContains(unlocked, "zombie_slayer"))
            newlyUnlocked.Add("zombie_slayer");
        if (kills >= 500 && !_ArrayContains(unlocked, "zombie_hunter"))
            newlyUnlocked.Add("zombie_hunter");
        if (maxRound >= 5 && !_ArrayContains(unlocked, "survivor"))
            newlyUnlocked.Add("survivor");
        if (maxRound >= 10 && !_ArrayContains(unlocked, "veteran"))
            newlyUnlocked.Add("veteran");
        if (maxRound >= 20 && !_ArrayContains(unlocked, "legend"))
            newlyUnlocked.Add("legend");
        if (score >= 1000 && !_ArrayContains(unlocked, "score_1000"))
            newlyUnlocked.Add("score_1000");
        if (score >= 5000 && !_ArrayContains(unlocked, "score_5000"))
            newlyUnlocked.Add("score_5000");
        if (games >= 10 && !_ArrayContains(unlocked, "dedicated"))
            newlyUnlocked.Add("dedicated");

        foreach (string id in newlyUnlocked)
        {
            if (!_ArrayContains(unlocked, id))
                unlocked.Add(id);
        }
        _saveData["unlocked_achievements"] = unlocked;
        return newlyUnlocked;
    }

    private static bool _ArrayContains(Godot.Collections.Array arr, string value)
    {
        foreach (var item in arr)
            if (item.AsString() == value) return true;
        return false;
    }

    private void _LoadGame()
    {
        if (!FileAccess.FileExists(SAVE_FILE_PATH)) return;
        var file = FileAccess.Open(SAVE_FILE_PATH, FileAccess.ModeFlags.Read);
        if (file == null) return;
        string text = file.GetAsText();
        file.Close();
        if (string.IsNullOrEmpty(text)) return;
        var json = new Json();
        var result = json.Parse(text);
        if (result == Error.Ok && json.Data.Obj is Godot.Collections.Dictionary d)
            _saveData = d;
    }

    private void _SaveGame()
    {
        var file = FileAccess.Open(SAVE_FILE_PATH, FileAccess.ModeFlags.Write);
        if (file == null) return;
        file.StoreString(Json.Stringify(_saveData));
        file.Close();
    }
}
