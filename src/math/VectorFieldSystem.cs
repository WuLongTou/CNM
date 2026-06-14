namespace ZombieWars.Math;

using Godot;
using System.Collections.Generic;
using ZombieWars.Core;
using static ZombieWars.Core.GameEnums;

/// <summary>
/// 向量场战术系统：玩家可放置力场节点，通过梯度/旋度影响僵尸路径。
///
/// 核心数学：
/// - 吸引场：F = -k · normalize(node - pos) / (|node - pos| + ε)（负梯度）
/// - 排斥场：F =  k · normalize(pos - node) / (|pos - node|² + ε)（正梯度）
/// - 旋涡场：F =  k · perp(normalize(node - pos)) / (|node - pos| + ε)（旋度）
///
/// 僵尸移动受力场叠加：finalVel = seekVel + Σ fieldForce
///
/// 解锁时间线：
/// - R5: 首次获得1个力场节点（吸引）
/// - R8: 解锁排斥节点
/// - R11: 解锁旋涡节点 + 第2个节点槽位
/// - R15: 第3个节点槽位
/// </summary>
public sealed partial class VectorFieldSystem : Node
{
    /// <summary>当前活跃的力场节点</summary>
    private readonly List<FieldNodeState> _activeNodes = new();

    /// <summary>当前可用的节点放置次数</summary>
    public int AvailableCharges { get; private set; }

    /// <summary>当前最大同时存在节点数</summary>
    public int MaxActiveNodes { get; private set; }

    /// <summary>当前回合编号</summary>
    public int CurrentRound { get; private set; }

    /// <summary>是否处于节点放置模式</summary>
    public bool IsPlacementMode { get; private set; }

    /// <summary>放置模式中选择的场类型</summary>
    public FieldType SelectedFieldType { get; private set; }

    /// <summary>当前活跃节点列表（只读）</summary>
    public IReadOnlyList<FieldNodeState> ActiveNodes => _activeNodes;

    // ═══════════════════════════════════════════════
    // 公共方法
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 为指定回合配置力场系统参数。
    /// </summary>
    public void ConfigureForRound(int round)
    {
        CurrentRound = round;

        // 节点槽位：R5获得第1个，R11获得第2个，R15获得第3个
        MaxActiveNodes = round < 5 ? 0
            : round < 11 ? 1
            : round < 15 ? 2
            : GameConfig.FieldMaxActiveNodes;

        // 每3回合获得1次放置充能（从R5开始）
        if (round >= 5 && round % 3 == 2) // R5, R8, R11, R14, R17...
        {
            AvailableCharges++;
        }
    }

    /// <summary>
    /// 计算指定位置受到的所有力场叠加效应。
    /// 用于修改僵尸移动速度：finalVel = seekVel + fieldContribution
    /// </summary>
    /// <param name="position">需要计算场力的位置</param>
    /// <returns>叠加后的力向量</returns>
    public Vector2 CalculateFieldForce(Vector2 position)
    {
        Vector2 totalForce = Vector2.Zero;

        foreach (var node in _activeNodes)
        {
            if (node.IsExpired) continue;

            float strength = node.Strength * node.AgeDecayFactor;
            Vector2 force = node.Type switch
            {
                FieldType.Attractor => MathFormulas.AttractorForce(position, node.Position, strength, GameConfig.FieldSoftening),
                FieldType.Repulsor => MathFormulas.RepulsorForce(position, node.Position, strength, GameConfig.FieldSoftening),
                FieldType.Vortex => MathFormulas.VortexForce(position, node.Position, strength, GameConfig.FieldSoftening),
                _ => Vector2.Zero
            };

            totalForce += force;
        }

        // 限制最大场力，防止僵尸被弹飞
        float maxForce = GameConfig.FieldMaxForce;
        if (totalForce.LengthSquared() > maxForce * maxForce)
        {
            totalForce = totalForce.Normalized() * maxForce;
        }

        return totalForce;
    }

    /// <summary>
    /// 在指定位置放置力场节点。
    /// </summary>
    /// <returns>是否成功放置</returns>
    public bool PlaceNode(Vector2 position, FieldType type)
    {
        if (AvailableCharges <= 0) return false;
        if (_activeNodes.Count >= MaxActiveNodes) return false;

        var node = new FieldNodeState
        {
            Position = position,
            Type = type,
            Strength = GetBaseStrength(type),
            PlacedTime = 0.0f,
            Duration = GameConfig.FieldNodeDuration,
        };

        _activeNodes.Add(node);
        AvailableCharges--;
        return true;
    }

    /// <summary>
    /// 进入/退出放置模式。
    /// </summary>
    public void TogglePlacementMode()
    {
        if (IsPlacementMode)
        {
            IsPlacementMode = false;
            return;
        }

        // 只有有充能且有空位才能进入放置模式
        if (AvailableCharges > 0 && _activeNodes.Count < MaxActiveNodes)
        {
            IsPlacementMode = true;
            SelectedFieldType = GetAvailableFieldTypes()[0]; // 默认选第一个可用类型
        }
    }

    /// <summary>
    /// 切换放置的场类型。
    /// </summary>
    public void CycleFieldType()
    {
        var available = GetAvailableFieldTypes();
        int idx = available.IndexOf(SelectedFieldType);
        if (idx < 0 || available.Count <= 1) return;
        SelectedFieldType = available[(idx + 1) % available.Count];
    }

    /// <summary>
    /// 显式选择场类型（由数字键调用）。
    /// </summary>
    public void SelectFieldTypeExplicit(FieldType type)
    {
        var available = GetAvailableFieldTypes();
        if (available.Contains(type))
            SelectedFieldType = type;
    }

    /// <summary>
    /// 取消放置模式。
    /// </summary>
    public void CancelPlacement()
    {
        IsPlacementMode = false;
    }

    /// <summary>
    /// 每帧更新：移除过期节点。
    /// </summary>
    public void Update(float delta)
    {
        for (int i = _activeNodes.Count - 1; i >= 0; i--)
        {
            _activeNodes[i].PlacedTime += delta;
            if (_activeNodes[i].IsExpired)
            {
                _activeNodes.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 判断指定回合是否有力场系统。
    /// </summary>
    public static bool IsFieldActive(int round) => round >= 5;

    /// <summary>
    /// 获取当前回合可用的场类型列表。
    /// </summary>
    public List<FieldType> GetAvailableFieldTypes()
    {
        var types = new List<FieldType>();
        if (CurrentRound >= 5) types.Add(FieldType.Attractor);
        if (CurrentRound >= 8) types.Add(FieldType.Repulsor);
        if (CurrentRound >= 11) types.Add(FieldType.Vortex);
        return types;
    }

    // ═══════════════════════════════════════════════
    // 内部方法
    // ═══════════════════════════════════════════════

    private static float GetBaseStrength(FieldType type) => type switch
    {
        FieldType.Attractor => GameConfig.FieldAttractorStrength,
        FieldType.Repulsor => GameConfig.FieldRepulsorStrength,
        FieldType.Vortex => GameConfig.FieldVortexStrength,
        _ => 0.0f
    };
}

/// <summary>
/// 力场节点运行时状态。
/// </summary>
public sealed class FieldNodeState
{
    public Vector2 Position;
    public FieldType Type;
    public float Strength;
    public float PlacedTime;
    public float Duration;

    /// <summary>节点是否已过期</summary>
    public bool IsExpired => PlacedTime >= Duration;

    /// <summary>年龄衰减因子 [0, 1]，越接近过期越弱</summary>
    public float AgeDecayFactor
    {
        get
        {
            float ratio = PlacedTime / Duration;
            // 后30%生命期开始衰减（SmoothStep）
            if (ratio < 0.7f) return 1.0f;
            return 1.0f - MathFormulas.SmoothStep(0.7f, 1.0f, ratio);
        }
    }
}
