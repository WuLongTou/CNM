namespace ZombieWars.Core;

using System;
using Godot;
using Godot.Collections;

public partial class SaveManager : Node
{
    public static SaveManager? Instance { get; private set; }
    private const string SAVE_FILE_PATH = "user://zvs_save.dat";

    private Godot.Collections.Dictionary _saveData = new();

    public override void _Ready()
    {
        Instance = this;
        _LoadGame();
    }

    public override void _ExitTree()
    {
        Instance = null;
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
        Instance._SaveGame();
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
