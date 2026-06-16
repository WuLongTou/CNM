namespace ZombieWars.Math;

using Godot;
using System;
using System.Collections.Generic;
using static ZombieWars.Core.GameEnums;

/// <summary>
/// 博弈论对抗系统：僵尸AI根据玩家武器使用习惯调整出兵策略。
///
/// 核心数学（纳什均衡/博弈论）：
/// - 追踪玩家近3回合的武器使用比例 (melee, ranged, magic)
/// - 僵尸出兵权重按博弈论收益矩阵调整：
///   zombieWeight[type] = baseWeight × (1 + adaptation × counterRatio)
/// - adaptationRate 随回合增长（0.1 → 0.5），逼近纳什均衡
///
/// 收益矩阵思想：
///              近战流  远程流  魔法流
/// 坦克型        +30%   -20%   +10%
/// 蜂群型        -20%   +20%   +10%
/// 远程型        -10%   +10%   -30%
///
/// 解锁时间线：
/// - R9-10: 策略适应开始（adaptation=0.1）
/// - R15+: 完全在线（adaptation=0.3+）
/// </summary>
public sealed partial class StrategySystem : Node
{
    // ═══════════════════════════════════════════════
    // 玩家武器使用追踪
    // ═══════════════════════════════════════════════

    /// <summary>近战击杀数（近3回合累计）</summary>
    public int MeleeKills { get; private set; }

    /// <summary>远程击杀数（近3回合累计）</summary>
    public int RangedKills { get; private set; }

    /// <summary>魔法击杀数（近3回合累计）</summary>
    public int MagicKills { get; private set; }

    /// <summary>总击杀数</summary>
    public int TotalKills => MeleeKills + RangedKills + MagicKills;

    /// <summary>当前回合编号</summary>
    public int CurrentRound { get; private set; }

    /// <summary>当前适应率 [0, 1]</summary>
    public float AdaptationRate { get; private set; }

    // 每回合击杀记录，用于滑动窗口
    private readonly Queue<(int round, WeaponType weaponType)> _killHistory = new();

    // ═══════════════════════════════════════════════
    // 博弈论收益矩阵
    // ═══════════════════════════════════════════════

    // [zombieCategory, playerWeapon] -> 效率修正（正=僵尸优势，负=玩家优势）
    // zombieCategory: 0=坦克型(Tank/Mech/Knight), 1=蜂群型(Swarm/Fast/Basic), 2=远程型(Archer/Spitter/Summoner)
    // playerWeapon: 0=Melee, 1=Ranged, 2=Magic
    private static readonly float[,] PayoffMatrix = new float[3, 3]
    {
        {  0.3f, -0.2f,  0.1f },  // 坦克型 vs 近战/远程/魔法
        { -0.2f,  0.2f,  0.1f },  // 蜂群型 vs 近战/远程/魔法
        { -0.1f,  0.1f, -0.3f },  // 远程型 vs 近战/远程/魔法
    };

    // 僵尸类型到策略类别的映射
    private static int ZombieToCategory(ZombieType type) => type switch
    {
        ZombieType.Tank or ZombieType.Mech or ZombieType.Knight or ZombieType.Dragon => 0, // 坦克型
        ZombieType.Swarm or ZombieType.Fast or ZombieType.Basic => 1,                      // 蜂群型
        ZombieType.Archer or ZombieType.Spitter or ZombieType.Summoner => 2,               // 远程型
        _ => 1, // 默认蜂群型
    };

    // ═══════════════════════════════════════════════
    // 公共方法
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 为指定回合配置策略系统参数。
    /// </summary>
    public void ConfigureForRound(int round)
    {
        CurrentRound = round;

        // 适应率：R9开始0.1，每回合+0.02，上限0.5
        AdaptationRate = round < 9 ? 0.0f
            : Math.Min(0.1f + (round - 9) * 0.02f, Core.GameConfig.StrategyMaxAdaptation);

        // 清理3回合之前的击杀记录
        TrimOldKills();
    }

    /// <summary>
    /// 记录一次击杀的武器类型。
    /// </summary>
    public void RecordKill(WeaponType weaponType)
    {
        _killHistory.Enqueue((CurrentRound, weaponType));
        UpdateKillCounts();
        // 立即清理过期记录，不依赖 ConfigureForRound，避免回合切换同帧时 CurrentRound 时序偏差
        TrimOldKills();
    }

    /// <summary>
    /// 根据博弈论策略调整僵尸出兵权重。
    /// 对每个僵尸类型，根据玩家偏好武器计算反制权重。
    /// </summary>
    /// <param name="baseWeights">基础权重字典</param>
    /// <returns>调整后的权重字典</returns>
    public Dictionary<ZombieType, float> AdjustWeights(Dictionary<ZombieType, float> baseWeights)
    {
        var adjusted = new Dictionary<ZombieType, float>(baseWeights);

        if (AdaptationRate <= 0 || TotalKills < 5) return adjusted;

        // 计算玩家武器使用比例
        float meleeRatio = (float)MeleeKills / TotalKills;
        float rangedRatio = (float)RangedKills / TotalKills;
        float magicRatio = (float)MagicKills / TotalKills;

        // 对每种僵尸类型，按收益矩阵调整权重
        foreach (var kv in adjusted)
        {
            int cat = ZombieToCategory(kv.Key);

            // 收益 = Σ(playerWeaponRatio × payoff[zombie, weapon])
            // 正收益 → 僵尸优势 → 增加该类型权重
            // 负收益 → 玩家优势 → 减少该类型权重
            float payoff = PayoffMatrix[cat, 0] * meleeRatio
                         + PayoffMatrix[cat, 1] * rangedRatio
                         + PayoffMatrix[cat, 2] * magicRatio;

            float weight = kv.Value * (1.0f + AdaptationRate * payoff);
            adjusted[kv.Key] = Math.Max(0.1f, weight); // 保证最低权重
        }

        return adjusted;
    }

    /// <summary>
    /// 获取当前策略分析结果，用于 UI 显示。
    /// </summary>
    public StrategyProfile GetCurrentProfile()
    {
        if (TotalKills < 3) return StrategyProfile.Unknown;

        float meleeRatio = (float)MeleeKills / TotalKills;
        float rangedRatio = (float)RangedKills / TotalKills;
        float magicRatio = (float)MagicKills / TotalKills;

        // 找出玩家最偏好的武器类型
        if (meleeRatio > rangedRatio && meleeRatio > magicRatio)
            return StrategyProfile.MeleeDominant;
        if (rangedRatio > meleeRatio && rangedRatio > magicRatio)
            return StrategyProfile.RangedDominant;
        if (magicRatio > meleeRatio && magicRatio > rangedRatio)
            return StrategyProfile.MagicDominant;

        return StrategyProfile.Balanced;
    }

    /// <summary>
    /// 判断指定回合是否启用了策略系统。
    /// </summary>
    public static bool IsStrategyActive(int round) => round >= 9;

    // ═══════════════════════════════════════════════
    // 内部方法
    // ═══════════════════════════════════════════════

    private void TrimOldKills()
    {
        while (_killHistory.Count > 0 && _killHistory.Peek().round < CurrentRound - 3)
        {
            _killHistory.Dequeue();
        }
    }

    private void UpdateKillCounts()
    {
        MeleeKills = 0;
        RangedKills = 0;
        MagicKills = 0;

        foreach (var kill in _killHistory)
        {
            switch (kill.weaponType)
            {
                case WeaponType.Melee: MeleeKills++; break;
                case WeaponType.Ranged: RangedKills++; break;
                case WeaponType.Magic: MagicKills++; break;
            }
        }
    }
}
