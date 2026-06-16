namespace ZombieWars.Math;

using Godot;
using System;
using static ZombieWars.Core.GameEnums;
using ZombieWars.Core;

/// <summary>
/// 波形共振系统：僵尸按数学波函数生成和移动，武器有频率属性可触发共振。
///
/// 核心数学：
/// - 僵尸生成位置受正弦波调制：SpawnPos(t) = edge + Σ A_i·sin(ω_i·t + φ_i)
/// - 武器频率与波频率匹配时产生共振伤害倍率
/// - 共振判定：resonanceMult = 1 + 2·exp(-|weaponFreq - waveFreq·harmonic|² / (2σ²))
///
/// 回合进度：
/// - R1-2: 无波形（基础玩法）
/// - R3-4: 单正弦波
/// - R5-6: 二阶傅里叶谐波
/// - R7-9: 三阶傅里叶谐波
/// - R10+: 最多五阶傅里叶合成
/// </summary>
public sealed partial class WaveResonanceSystem : Node
{
    // ═══════════════════════════════════════════════
    // 当前波纹参数
    // ═══════════════════════════════════════════════

    /// <summary>当前波纹振幅（像素），控制僵尸偏离中心路径的距离</summary>
    public float Amplitude { get; private set; }

    /// <summary>基频 ω（弧度/秒），控制波形振荡速度</summary>
    public float BaseFrequency { get; private set; }

    /// <summary>谐波数量（1=纯正弦，2+=傅里叶合成）</summary>
    public int HarmonicCount { get; private set; }

    /// <summary>相位偏移 φ，不同僵尸组错开到达时间</summary>
    public float PhaseShift { get; private set; }

    /// <summary>当前回合编号</summary>
    public int CurrentRound { get; private set; }

    // 每阶谐波的振幅和频率倍率
    private float[] _harmonicAmplitudes = Array.Empty<float>();
    private float[] _harmonicFreqMultipliers = Array.Empty<float>();
    private float[] _harmonicPhases = Array.Empty<float>();

    // ═══════════════════════════════════════════════
    // 共振参数
    // ═══════════════════════════════════════════════

    /// <summary>共振宽度 σ，决定频率匹配的宽容度</summary>
    public float ResonanceWidth => GameConfig.WaveResonanceWidth;

    // ═══════════════════════════════════════════════
    // 公共方法
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 为指定回合配置波形参数。每回合开始时调用。
    /// </summary>
    public void ConfigureForRound(int round)
    {
        CurrentRound = round;

        // 谐波数：每3回合增加1个，最多5个
        HarmonicCount = round < 3 ? 0 : Mathf.Min(1 + (round - 3) / 3, GameConfig.WaveMaxHarmonics);

        // 振幅：随回合增长
        Amplitude = GameConfig.WaveBaseAmplitude + round * GameConfig.WaveAmplitudeGrowth;

        // 基频：缓慢增长
        BaseFrequency = GameConfig.WaveBaseFrequency + round * GameConfig.WaveFrequencyGrowth;

        // 相位偏移：使用黄金角度确保每回合的波形方向不同
        PhaseShift = (round * Mathf.Pi / 7.0f) % MathFormulas.TwoPi;

        // 构建谐波参数
        BuildHarmonics();
    }

    /// <summary>
    /// 计算波形调制偏移量。用于僵尸生成位置。
    /// spawnOffset = (Σ A_i · sin(ω_i · progress + φ_i)) · visualScale
    /// </summary>
    /// <param name="progress">当前波内的进度 [0, 1]</param>
    /// <param name="harmonicOffset">额外的谐波偏移（用于同波内多个僵尸的分散）</param>
    public Vector2 GetSpawnOffset(float progress, float harmonicOffset = 0.0f)
    {
        if (HarmonicCount == 0) return Vector2.Zero;

        float t = progress * MathFormulas.TwoPi + harmonicOffset;
        float rawOffset = MathFormulas.FourierSynthesis(t, _harmonicAmplitudes, _harmonicFreqMultipliers, _harmonicPhases);

        // 归一化基准 = 谐波振幅总和（动态按实际谐波数，避免硬编码 2.28 在 WaveMaxHarmonics 改变后失效）
        float harmonicSum = 0.0f;
        foreach (var a in _harmonicAmplitudes) harmonicSum += Mathf.Abs(a);
        float normalization = Mathf.Max(harmonicSum, 1.0f);
        float normalized = rawOffset / normalization;
        float visualOffset = normalized * GameConfig.WaveSpawnVisualRange; // ±像素范围

        // 主偏移水平（沿生成边分布），次偏移垂直（略微起伏）
        return new Vector2(visualOffset, visualOffset * 0.4f);
    }

    /// <summary>
    /// 计算共振伤害倍率。
    /// resonanceMult = 1 + maxMult · exp(-|weaponFreq - waveFreq·nearestHarmonic|² / (2σ²))
    /// </summary>
    /// <param name="weaponFrequency">武器的频率属性</param>
    /// <returns>伤害倍率 [1.0, 1.0 + maxMult]</returns>
    public float CalculateResonanceMultiplier(float weaponFrequency)
    {
        if (HarmonicCount == 0) return 1.0f;

        // 寻找最近的谐波匹配
        float bestMatch = float.MaxValue;
        for (int i = 0; i < _harmonicFreqMultipliers.Length; i++)
        {
            float harmonicFreq = BaseFrequency * _harmonicFreqMultipliers[i];
            float diff = Mathf.Abs(weaponFrequency - harmonicFreq);
            bestMatch = Mathf.Min(bestMatch, diff);
        }

        // 也检查基频本身
        float baseDiff = Mathf.Abs(weaponFrequency - BaseFrequency);
        bestMatch = Mathf.Min(bestMatch, baseDiff);

        // 高斯匹配判定
        float resonance = MathFormulas.Gaussian(bestMatch, 0.0f, ResonanceWidth, GameConfig.WaveResonanceMaxMultiplier);
        return 1.0f + resonance;
    }

    /// <summary>
    /// 获取当前波形的采样点，用于 UI 波形可视化。
    /// </summary>
    /// <param name="sampleCount">采样数量</param>
    /// <returns>y 值数组，范围约 [-Amplitude, +Amplitude]</returns>
    public float[] GetWaveformSamples(int sampleCount)
    {
        float[] samples = new float[sampleCount];
        if (HarmonicCount == 0) return samples;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleCount * MathFormulas.TwoPi;
            samples[i] = MathFormulas.FourierSynthesis(t, _harmonicAmplitudes, _harmonicFreqMultipliers, _harmonicPhases);
        }
        return samples;
    }

    /// <summary>
    /// 判断指定回合是否启用了波形系统。
    /// </summary>
    public static bool IsWaveActive(int round) => round >= 3;

    // ═══════════════════════════════════════════════
    // 内部方法
    // ═══════════════════════════════════════════════

    private void BuildHarmonics()
    {
        if (HarmonicCount == 0)
        {
            _harmonicAmplitudes = Array.Empty<float>();
            _harmonicFreqMultipliers = Array.Empty<float>();
            _harmonicPhases = Array.Empty<float>();
            return;
        }

        _harmonicAmplitudes = new float[HarmonicCount];
        _harmonicFreqMultipliers = new float[HarmonicCount];
        _harmonicPhases = new float[HarmonicCount];

        for (int i = 0; i < HarmonicCount; i++)
        {
            int harmonic = i + 1; // 1阶、2阶、3阶...

            // 振幅：高阶谐波振幅递减（1/n 规则，傅里叶级数标准）
            _harmonicAmplitudes[i] = Amplitude / harmonic;

            // 频率倍率：整数倍频（傅里叶谐波）
            _harmonicFreqMultipliers[i] = harmonic;

            // 相位：每阶谐波使用黄金角度偏移，确保不重叠
            _harmonicPhases[i] = PhaseShift + MathFormulas.GoldenAngle(i);
        }
    }
}
