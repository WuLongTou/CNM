namespace ZombieWars.Core;

using System;
using System.Collections.Generic;
using Godot;
using static ZombieWars.Core.GameEnums;

/// <summary>
/// Achievement and milestone tracking system.
/// Tracks player progress and displays notifications when achievements are unlocked.
/// </summary>
public partial class AchievementSystem : Node
{
    private static AchievementSystem? _instance;
    public static AchievementSystem? Instance => _instance;

    // Tracked state
    private int _totalKills;
    private int _eliteKills;
    private int _bossKills;
    private int _maxCombo;
    private int _totalDamage;
    private float _survivalTime;
    private int _roundsSurvived;
    private bool _fullHealthRound;
    private bool _noDamageRound;

    // Unlocked achievements
    private readonly HashSet<AchievementType> _unlockedAchievements = new();
    private readonly HashSet<MilestoneType> _reachedMilestones = new();

    // Display callback
    public static Action<string, Color>? OnAchievementUnlocked;
    public static Action<string, Color>? OnMilestoneReached;

    public override void _Ready()
    {
        _instance = this;
        AddToGroup("achievement_system");
    }

    public override void _Process(double delta)
    {
        _survivalTime += (float)delta;
    }

    public void OnZombieKilled(ZombieType type, bool isElite, bool isBoss)
    {
        _totalKills++;
        if (isElite) _eliteKills++;
        if (isBoss) _bossKills++;

        // Check kill achievements
        if (_totalKills == 1) TryUnlock(AchievementType.FirstBlood);
        if (_eliteKills == 1) TryUnlock(AchievementType.FirstEliteKill);
        if (_bossKills == 1) TryUnlock(AchievementType.FirstBossKill);

        // Check milestone thresholds
        _CheckKillMilestones();
    }

    public void OnDamageDealt(int damage)
    {
        _totalDamage += damage;
        _CheckDamageMilestones();
    }

    public void OnComboAchieved(int comboCount)
    {
        if (comboCount > _maxCombo)
            _maxCombo = comboCount;

        if (comboCount >= 3) TryUnlock(AchievementType.Combo3);
        if (comboCount >= 5) TryUnlock(AchievementType.Combo5);
        if (comboCount >= 10) TryUnlock(AchievementType.Combo10);
    }

    public void OnRoundSurvived(int round)
    {
        _roundsSurvived = round;
        if (round >= 5) TryUnlock(AchievementType.Round5Survived);
        if (round >= 10) TryUnlock(AchievementType.Round10Survived);
        if (round >= 20) TryUnlock(AchievementType.Round20Survived);

        _CheckSurvivalMilestones();
    }

    public void OnRoundHealthCheck(bool fullHealth)
    {
        if (fullHealth) _fullHealthRound = true;
    }

    public void OnRoundDamageCheck(bool noDamage)
    {
        if (noDamage) _noDamageRound = true;
    }

    public void ResetRoundStats()
    {
        _fullHealthRound = false;
        _noDamageRound = false;
    }

    private void _CheckKillMilestones()
    {
        if (_totalKills >= 10 && !_reachedMilestones.Contains(MilestoneType.Kill10))
        {
            _reachedMilestones.Add(MilestoneType.Kill10);
            OnMilestoneReached?.Invoke($"击杀 10! 当前: {_totalKills}", new Color(1.0f, 0.6f, 0.2f));
        }
        if (_totalKills >= 50 && !_reachedMilestones.Contains(MilestoneType.Kill50))
        {
            _reachedMilestones.Add(MilestoneType.Kill50);
            OnMilestoneReached?.Invoke($"击杀 50! 当前: {_totalKills}", new Color(1.0f, 0.6f, 0.2f));
        }
        if (_totalKills >= 100 && !_reachedMilestones.Contains(MilestoneType.Kill100))
        {
            _reachedMilestones.Add(MilestoneType.Kill100);
            OnMilestoneReached?.Invoke($"击杀 100! 当前: {_totalKills}", new Color(1.0f, 0.8f, 0.2f));
        }
        if (_totalKills >= 500 && !_reachedMilestones.Contains(MilestoneType.Kill500))
        {
            _reachedMilestones.Add(MilestoneType.Kill500);
            OnMilestoneReached?.Invoke($"击杀 500! 当前: {_totalKills}", new Color(1.0f, 0.9f, 0.1f));
        }
    }

    private void _CheckDamageMilestones()
    {
        if (_totalDamage >= 1000 && !_reachedMilestones.Contains(MilestoneType.Damage1000))
        {
            _reachedMilestones.Add(MilestoneType.Damage1000);
            OnMilestoneReached?.Invoke($"造成伤害 1000!", new Color(0.8f, 0.4f, 1.0f));
        }
        if (_totalDamage >= 10000 && !_reachedMilestones.Contains(MilestoneType.Damage10000))
        {
            _reachedMilestones.Add(MilestoneType.Damage10000);
            OnMilestoneReached?.Invoke($"造成伤害 10000!", new Color(0.9f, 0.5f, 1.0f));
        }
        if (_totalDamage >= 50000 && !_reachedMilestones.Contains(MilestoneType.Damage50000))
        {
            _reachedMilestones.Add(MilestoneType.Damage50000);
            OnMilestoneReached?.Invoke($"造成伤害 50000!", new Color(1.0f, 0.8f, 1.0f));
        }
    }

    private void _CheckSurvivalMilestones()
    {
        float minutes = _survivalTime / 60.0f;
        if (minutes >= 5 && !_reachedMilestones.Contains(MilestoneType.Survive5Min))
        {
            _reachedMilestones.Add(MilestoneType.Survive5Min);
            OnMilestoneReached?.Invoke($"生存 5 分钟!", new Color(0.4f, 0.9f, 0.6f));
        }
        if (minutes >= 10 && !_reachedMilestones.Contains(MilestoneType.Survive10Min))
        {
            _reachedMilestones.Add(MilestoneType.Survive10Min);
            OnMilestoneReached?.Invoke($"生存 10 分钟!", new Color(0.3f, 1.0f, 0.5f));
        }
        if (minutes >= 20 && !_reachedMilestones.Contains(MilestoneType.Survive20Min))
        {
            _reachedMilestones.Add(MilestoneType.Survive20Min);
            OnMilestoneReached?.Invoke($"生存 20 分钟!", new Color(1.0f, 1.0f, 0.5f));
        }
    }

    private void TryUnlock(AchievementType achievement)
    {
        if (_unlockedAchievements.Contains(achievement))
            return;

        _unlockedAchievements.Add(achievement);
        var (name, desc, color) = _GetAchievementInfo(achievement);
        OnAchievementUnlocked?.Invoke($"🏆 {name}", color);

        LogUtils.Print($"成就解锁: {name} - {desc}");
    }

    private (string name, string desc, Color color) _GetAchievementInfo(AchievementType a)
    {
        return a switch
        {
            AchievementType.FirstBlood => ("初试锋芒", "击杀第一只僵尸", new Color(0.9f, 0.3f, 0.3f)),
            AchievementType.Round5Survived => ("初露头角", "存活5回合", new Color(0.8f, 0.6f, 0.3f)),
            AchievementType.Round10Survived => ("渐入佳境", "存活10回合", new Color(0.7f, 0.7f, 0.4f)),
            AchievementType.Round20Survived => ("久经沙场", "存活20回合", new Color(1.0f, 0.8f, 0.2f)),
            AchievementType.FirstEliteKill => ("精英猎手", "击杀第一只精英僵尸", new Color(0.9f, 0.7f, 0.2f)),
            AchievementType.FirstBossKill => ("屠龙勇士", "击杀第一只BOSS", new Color(1.0f, 0.5f, 0.1f)),
            AchievementType.Combo3 => ("小小连击", "完成3连击", new Color(0.6f, 0.8f, 0.6f)),
            AchievementType.Combo5 => ("连击高手", "完成5连击", new Color(0.5f, 0.9f, 0.5f)),
            AchievementType.Combo10 => ("连击之王", "完成10连击", new Color(1.0f, 0.9f, 0.3f)),
            AchievementType.FullHealthSurvive => ("完好无损", "满血存活一回合", new Color(0.4f, 1.0f, 0.6f)),
            AchievementType.NoDamageSurvive => ("完美闪避", "无伤通过一回合", new Color(0.3f, 0.9f, 1.0f)),
            AchievementType.MaxWeaponsCollected => ("全副武装", "收集所有武器", new Color(0.8f, 0.4f, 1.0f)),
            AchievementType.AllEliteKills => ("精英杀手", "击杀所有类型精英", new Color(1.0f, 0.7f, 0.5f)),
            _ => ("未知成就", "未知的成就", Colors.Gray)
        };
    }

    public int GetTotalKills() => _totalKills;
    public int GetMaxCombo() => _maxCombo;
    public int GetTotalDamage() => _totalDamage;
    public int GetRoundsSurvived() => _roundsSurvived;
    public float GetSurvivalTime() => _survivalTime;

    public static string FormatSurvivalTime(float seconds)
    {
        int mins = (int)(seconds / 60);
        int secs = (int)(seconds % 60);
        return $"{mins}:{secs:D2}";
    }

    public void OnGameEnded(int totalKills, int totalDamage, float survivalTime)
    {
        TryUnlock(AchievementType.Round5Survived);
        TryUnlock(AchievementType.Round10Survived);
        TryUnlock(AchievementType.Round20Survived);
    }
}