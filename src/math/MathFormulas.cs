namespace ZombieWars.Math;

using Godot;
using System;

/// <summary>
/// 通用数学工具函数，供所有数学玩法系统共享。
/// </summary>
public static class MathFormulas
{
    // ═══════════════════════════════════════════════
    // 常量
    // ═══════════════════════════════════════════════

    /// <summary>黄金比例 φ ≈ 1.618</summary>
    public const float GoldenRatio = 1.618033988749895f;

    /// <summary>2π</summary>
    public const float TwoPi = Mathf.Tau;

    /// <summary>π/2</summary>
    public const float HalfPi = Mathf.Pi / 2.0f;

    // ═══════════════════════════════════════════════
    // 波形函数
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 傅里叶合成：将多个正弦谐波叠加为复合波形。
    /// f(t) = Σ A_i · sin(ω_i · t + φ_i)
    /// </summary>
    public static float FourierSynthesis(float t, float[] amplitudes, float[] frequencies, float[] phases)
    {
        float result = 0.0f;
        int count = Mathf.Min(amplitudes.Length, Mathf.Min(frequencies.Length, phases.Length));
        for (int i = 0; i < count; i++)
        {
            result += amplitudes[i] * Mathf.Sin(frequencies[i] * t + phases[i]);
        }
        return result;
    }

    /// <summary>
    /// 高斯函数（正态分布钟形曲线）。
    /// g(x) = A · exp(-(x-μ)² / (2σ²))
    /// </summary>
    public static float Gaussian(float x, float mean, float sigma, float amplitude = 1.0f)
    {
        float diff = x - mean;
        float twoSigmaSq = 2.0f * sigma * sigma;
        return amplitude * Mathf.Exp(-(diff * diff) / Mathf.Max(twoSigmaSq, 0.0001f));
    }

    // ═══════════════════════════════════════════════
    // 向量场函数
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 吸引场力：将实体拉向节点（负梯度）。
    /// F = -k · normalize(node - pos) / (|node - pos| + ε)
    /// </summary>
    public static Vector2 AttractorForce(Vector2 pos, Vector2 nodePos, float strength, float softening = 10.0f)
    {
        Vector2 dir = nodePos - pos;
        float dist = dir.Length();
        if (dist < 1.0f) return Vector2.Zero;
        return -strength * dir.Normalized() / (dist + softening);
    }

    /// <summary>
    /// 排斥场力：将实体推离节点（正梯度）。
    /// F = k · normalize(pos - node) / (|pos - node|² + ε)
    /// </summary>
    public static Vector2 RepulsorForce(Vector2 pos, Vector2 nodePos, float strength, float softening = 10.0f)
    {
        Vector2 dir = pos - nodePos;
        float distSq = dir.LengthSquared();
        if (distSq < 1.0f) return Vector2.Zero;
        float dist = Mathf.Sqrt(distSq);
        return strength * dir.Normalized() / (distSq + softening);
    }

    /// <summary>
    /// 旋涡场力：使实体围绕节点旋转（旋度场）。
    /// F = k · perpendicular(normalize(node - pos)) / (|node - pos| + ε)
    /// </summary>
    public static Vector2 VortexForce(Vector2 pos, Vector2 nodePos, float strength, float softening = 10.0f)
    {
        Vector2 dir = nodePos - pos;
        float dist = dir.Length();
        if (dist < 1.0f) return Vector2.Zero;
        // 垂直方向（逆时针90°旋转）
        Vector2 perp = new Vector2(-dir.Y, dir.X).Normalized();
        return strength * perp / (dist + softening);
    }

    // ═══════════════════════════════════════════════
    // 斐波那契 / 黄金比例
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 计算第 n 个斐波那契数。
    /// fib(1)=1, fib(2)=1, fib(3)=2, fib(4)=3, fib(5)=5...
    /// </summary>
    public static long Fibonacci(int n)
    {
        if (n <= 0) return 0;
        if (n <= 2) return 1;

        long a = 1, b = 1;
        for (int i = 3; i <= n; i++)
        {
            long temp = a + b;
            a = b;
            b = temp;
        }
        return b;
    }

    /// <summary>
    /// 斐波那契比率：fib(n) / fib(n-1)，随 n 增大收敛到 φ。
    /// 用于难度曲线，产生自然渐进的增长节奏。
    /// </summary>
    public static float FibonacciRatio(int n)
    {
        if (n <= 1) return 1.0f;
        long fibN = Fibonacci(n);
        long fibN1 = Fibonacci(n - 1);
        return fibN1 > 0 ? (float)fibN / fibN1 : 1.0f;
    }

    // ═══════════════════════════════════════════════
    // 工具
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 将值映射到黄金角度间隔（137.5°），用于均匀但不规则的分布。
    /// </summary>
    public static float GoldenAngle(int index)
    {
        return Mathf.DegToRad(index * 137.508f);
    }

    /// <summary>
    /// 平滑阶梯函数：0→1 的平滑过渡。
    /// </summary>
    public static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp((x - edge0) / (edge1 - edge0), 0.0f, 1.0f);
        return t * t * (3.0f - 2.0f * t);
    }

    /// <summary>
    /// 线性衰减：距离越远值越小。
    /// </summary>
    public static float LinearFalloff(float distance, float maxRange, float minValue = 0.0f)
    {
        if (distance >= maxRange) return minValue;
        return Mathf.Lerp(1.0f, minValue, distance / maxRange);
    }
}
