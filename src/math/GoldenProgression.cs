namespace ZombieWars.Math;

using Godot;
using System;
using ZombieWars.Core;

/// <summary>
/// 黄金比例进程系统：基于斐波那契数列和黄金比例 φ 的难度曲线与成长系统。
///
/// 核心数学（斐波那契/黄金比例）：
/// - 难度倍率使用斐波那契比率：fib(n)/fib(n-1)，随回合自然收敛到 φ≈1.618
/// - 武器升级成本遵循斐波那契：T1=1, T2=1, T3=2, T4=3, T5=5 张卡
/// - 掉落保证间隔使用斐波那契数：1, 2, 3, 5, 8, 13, 21... 次击杀
/// - 共振频率间隔使用黄金比例：ω = base × φ^k
/// </summary>
public sealed partial class GoldenProgression : Node
{
    /// <summary>当前回合编号</summary>
    public int CurrentRound { get; private set; }

    /// <summary>当前难度倍率（斐波那契比率，收敛到 φ）</summary>
    public float DifficultyMultiplier { get; private set; }

    // 预计算的斐波那契掉落阈值
    private static readonly int[] FibonacciDropThresholds = GenerateFibonacciThresholds(30);

    // ═══════════════════════════════════════════════
    // 公共方法
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 为指定回合配置黄金进程参数。
    /// </summary>
    public void ConfigureForRound(int round)
    {
        CurrentRound = round;
        DifficultyMultiplier = CalculateDifficultyMultiplier(round);
    }

    /// <summary>
    /// 计算难度倍率：fib(round) / fib(round - 1)
    /// R1=1, R2=2, R3=1.5, R4=1.667, R5=1.6, R6=1.625...
    /// 自然收敛到 φ ≈ 1.618
    /// </summary>
    public static float CalculateDifficultyMultiplier(int round)
    {
        if (round <= 1) return 1.0f;
        return MathFormulas.FibonacciRatio(round + 4); // 偏移4位使前几回合有足够差异
    }

    /// <summary>
    /// 获取指定武器等级的升级成本（斐波那契数列）。
    /// T1=1, T2=1, T3=2, T4=3, T5=5, T6=8...
    /// </summary>
    public static int GetUpgradeCost(int tier) => tier switch
    {
        <= 0 => 0,
        1 => 1,
        2 => 1,
        3 => 2,
        4 => 3,
        5 => 5,
        6 => 8,
        _ => (int)MathFormulas.Fibonacci(tier)
    };

    /// <summary>
    /// 判断指定击杀数是否为斐波那契掉落点（保证掉落）。
    /// 斐波那契数：1, 2, 3, 5, 8, 13, 21, 34, 55, 89...
    /// </summary>
    public static bool IsFibonacciDropPoint(int totalKills)
    {
        return Array.BinarySearch(FibonacciDropThresholds, totalKills) >= 0;
    }

    /// <summary>
    /// 计算共振频率的黄金间隔点。
    /// ω_k = baseFreq × φ^k，产生非等比但和谐的频率序列。
    /// </summary>
    public static float GoldenFrequency(float baseFreq, int harmonicIndex)
    {
        return baseFreq * Mathf.Pow(MathFormulas.GoldenRatio, harmonicIndex);
    }

    /// <summary>
    /// 获取回合僵尸数量的斐波那契调制。
    /// 在基础数量上叠加斐波那契波动：count = base + fib(round % 8) * 0.3
    /// </summary>
    public static int ModulateZombieCount(int baseCount, int round)
    {
        int fibMod = (int)MathFormulas.Fibonacci(round % 8 + 1);
        return baseCount + Mathf.RoundToInt(fibMod * 0.3f);
    }

    // ═══════════════════════════════════════════════
    // 内部方法
    // ═══════════════════════════════════════════════

    private static int[] GenerateFibonacciThresholds(int count)
    {
        var thresholds = new int[count];
        for (int i = 0; i < count; i++)
        {
            thresholds[i] = (int)MathFormulas.Fibonacci(i + 2); // 从fib(2)=1开始
        }
        return thresholds;
    }
}
