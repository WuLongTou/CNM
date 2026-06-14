namespace ZombieWars.Math;

using Godot;
using ZombieWars.Core;

/// <summary>
/// 分形进化系统：僵尸和武器的递归分裂机制。
///
/// 核心数学（分形/递归）：
/// - 分形僵尸死亡时递归分裂为更小的子体
/// - 分裂规则：childHP = parentHP / branching^1.5
///             childSize = parentSize / branching^0.5
///             childSpeed = parentSpeed × 1.3
///             childDepth = parentDepth + 1
/// - 总HP守恒但递减：总子HP ≈ parentHP × branching / branching^1.5 = parentHP / branching^0.5
/// - 敌人数量指数增长但单体更弱——风险回报机制
///
/// 解锁时间线：
/// - R7-9: 分形僵尸出现（maxDepth=1）
/// - R10-14: maxDepth=2
/// - R15+: maxDepth=3
/// </summary>
public sealed partial class FractalSystem : Node
{
    /// <summary>当前回合编号</summary>
    public int CurrentRound { get; private set; }

    /// <summary>当前最大分形深度</summary>
    public int MaxDepth { get; private set; }

    // ═══════════════════════════════════════════════
    // 公共方法
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 为指定回合配置分形系统参数。
    /// </summary>
    public void ConfigureForRound(int round)
    {
        CurrentRound = round;
        MaxDepth = round < 7 ? 0
            : round < 10 ? 1
            : round < 15 ? 2
            : GameConfig.FractalMaxDepth;
    }

    /// <summary>
    /// 计算分形僵尸死亡时的分裂参数。
    /// </summary>
    /// <param name="parentHp">父体HP</param>
    /// <param name="parentDamage">父体伤害</param>
    /// <param name="parentSpeed">父体速度</param>
    /// <param name="parentSize">父体尺寸</param>
    /// <param name="currentDepth">当前分形深度</param>
    /// <param name="branchingFactor">分支因子（2或3）</param>
    /// <returns>分裂结果；若不能分裂（已达最大深度）返回 null</returns>
    public FractalSplitResult? CalculateSplit(
        int parentHp, int parentDamage, float parentSpeed, float parentSize,
        int currentDepth, int branchingFactor)
    {
        if (currentDepth >= MaxDepth) return null;
        if (branchingFactor < 2 || branchingFactor > 3) return null;

        float branchF = branchingFactor;

        return new FractalSplitResult
        {
            ChildCount = branchingFactor,
            ChildHp = Mathf.Max(1, Mathf.RoundToInt(parentHp / Mathf.Pow(branchF, 1.5f))),
            ChildDamage = Mathf.Max(1, Mathf.RoundToInt(parentDamage / branchF)),
            ChildSpeed = parentSpeed * GameConfig.FractalSpeedMultiplier,
            ChildSize = parentSize / Mathf.Pow(branchF, 0.5f),
            ChildDepth = currentDepth + 1,
            BranchingFactor = branchingFactor,
        };
    }

    /// <summary>
    /// 计算分形武器投射物分裂参数。
    /// </summary>
    /// <param name="parentDamage">父投射物伤害</param>
    /// <param name="currentSplitDepth">当前分裂深度</param>
    /// <param name="maxSplitDepth">最大分裂深度</param>
    /// <param name="splitCount">分裂数量</param>
    public FractalSplitResult? CalculateWeaponSplit(
        float parentDamage, int currentSplitDepth, int maxSplitDepth, int splitCount)
    {
        if (currentSplitDepth >= maxSplitDepth) return null;
        if (splitCount < 2 || splitCount > 3) return null;

        float splitF = splitCount;

        return new FractalSplitResult
        {
            ChildCount = splitCount,
            ChildHp = 0, // 投射物无HP
            ChildDamage = Mathf.RoundToInt(parentDamage / Mathf.Pow(splitF, 1.2f)),
            ChildSpeed = 0, // 投射物速度由武器决定
            ChildSize = 1.0f / Mathf.Pow(splitF, 0.3f),
            ChildDepth = currentSplitDepth + 1,
            BranchingFactor = splitCount,
        };
    }

    /// <summary>
    /// 判断指定回合是否有分形僵尸。
    /// </summary>
    public static bool IsFractalActive(int round) => round >= 7;

    /// <summary>
    /// 获取指定深度下分形僵尸的视觉透明度（越深越透明）。
    /// </summary>
    public static float GetDepthAlpha(int depth)
    {
        // 深度0=不透明，深度1=0.85，深度2=0.7，深度3=0.55
        return Mathf.Max(0.4f, 1.0f - depth * 0.15f);
    }
}

/// <summary>
/// 分形分裂结果。
/// </summary>
public sealed class FractalSplitResult
{
    public int ChildCount;
    public int ChildHp;
    public int ChildDamage;
    public float ChildSpeed;
    public float ChildSize;
    public int ChildDepth;
    public int BranchingFactor;
}
