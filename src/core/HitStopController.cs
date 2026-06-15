namespace ZombieWars.Core;

using System;
using Godot;

/// <summary>
/// 命中停顿（HitStop）控制器——打击感的灵魂。
/// 暴击 / 击杀 / Boss 事件时短暂冻结 <see cref="Engine.TimeScale"/>，制造"咔"的凝固冲击，
/// 让震屏、粒子、伤害数字的反馈被"放大"成真正的重击感。
///
/// 关键实现要点（均经过验证）：
/// - 用真实墙钟（<see cref="Time.GetTicksMsec"/>）计时，而非 delta——因为冻结期 delta≈0，
///   若用 delta 递减计时器将永远无法恢复。
/// - <see cref="ProcessMode"/> = Always：即使游戏暂停（GetTree().Paused）或切到 GameOver，
///   本节点仍每帧运行，保证 TimeScale 一定恢复到 1.0，绝不卡死。
/// - <see cref="Trigger"/> 取 max：短停不会打断正在进行的长停（如 Boss 击杀慢动作）。
/// </summary>
public partial class HitStopController : Node
{
    public static HitStopController? Instance { get; private set; }

    // TimeScale 维持 HitStopFrozenScale 直到此墙钟时刻；墙钟避免 delta≈0 的死循环
    private ulong _frozenUntilMs;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        Engine.TimeScale = 1.0;
    }

    /// <summary>当前是否处于冻结期（供外部判断，例如避免演出重叠）。</summary>
    public bool IsActive => Time.GetTicksMsec() < _frozenUntilMs;

    /// <summary>
    /// 触发一次命中停顿：把 TimeScale 压到 <see cref="GameConfig.HitStopFrozenScale"/>，
    /// 维持 <paramref name="durationMs"/> 毫秒后由 _Process 自动恢复。
    /// </summary>
    public void Trigger(int durationMs)
    {
        if (durationMs <= 0) return;
        ulong now = Time.GetTicksMsec();
        _frozenUntilMs = Math.Max(_frozenUntilMs, now + (ulong)durationMs);
        Engine.TimeScale = GameConfig.HitStopFrozenScale;
    }

    public override void _Process(double delta)
    {
        if (Engine.TimeScale == 1.0) return;

        // 进入暂停态（卡牌选择 / 暂停 / GameOver）时立即解除凝固：暂停期间 UI 入场 tween
        // 仍会播放，若 TimeScale≈0 会把它们拉慢数十倍（卡牌十几秒才弹出）。
        // delta 在冻结期≈0，故恢复判断用墙钟/状态而非 delta。
        if (GetTree().Paused || Time.GetTicksMsec() >= _frozenUntilMs)
            Engine.TimeScale = 1.0;
    }

    /// <summary>立即解除冻结（GameOver / 场景切换兜底）。</summary>
    public void Reset()
    {
        _frozenUntilMs = 0;
        Engine.TimeScale = 1.0;
    }

    public override void _ExitTree()
    {
        Engine.TimeScale = 1.0;
        if (Instance == this)
            Instance = null;
    }
}
