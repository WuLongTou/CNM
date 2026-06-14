namespace ZombieWars.World;

using Godot;
using System;
using System.Collections.Generic;
using static ZombieWars.Core.GameEnums;
using ZombieWars.Core;

/// <summary>
/// 向量场节点——玩家放置在世界中的力场实体。
///
/// 视觉：炼金术粉笔阵法（同心圆 + 内接多边形 + 流线），随类型不同颜色不同。
/// 力学：由 VectorFieldSystem 统一计算场力，本类只负责视觉与生命周期。
/// </summary>
public partial class VectorFieldNode : Node2D
{
    /// <summary>场类型</summary>
    public FieldType FieldType { get; private set; }

    /// <summary>场强度</summary>
    public float Strength { get; private set; }

    /// <summary>存在时长（秒）</summary>
    public float Duration { get; private set; }

    /// <summary>已存在时间</summary>
    public float ElapsedTime { get; private set; }

    /// <summary>是否已过期</summary>
    public bool IsExpired => ElapsedTime >= Duration;

    private Line2D _outerCircle = null!;
    private Line2D _innerCircle = null!;
    private Line2D _polygon = null!;
    private Line2D _streamLine = null!;
    private float _rotation;
    private float _streamPhase;

    // 类型对应的颜色
    private static Color GetColor(FieldType type) => type switch
    {
        FieldType.Attractor => new Color(0.788f, 0.659f, 0.298f),  // 金色（吸引）
        FieldType.Repulsor => new Color(0.831f, 0.325f, 0.255f),   // 铁锈红（排斥）
        FieldType.Vortex => new Color(0.290f, 0.486f, 0.435f),     // 铜锈绿（旋涡）
        _ => Colors.White
    };

    private static string GetSymbol(FieldType type) => type switch
    {
        FieldType.Attractor => "⊕",  // 向内
        FieldType.Repulsor => "⊗",   // 向外
        FieldType.Vortex => "⊙",     // 旋转
        _ => "?"
    };

    public void Setup(FieldType type, float strength, float duration)
    {
        FieldType = type;
        Strength = strength;
        Duration = duration;
    }

    public override void _Ready()
    {
        ZIndex = 5; // 在僵尸下方，地面上方
        Color color = GetColor(FieldType);

        // 外圆（粉笔主圈）
        _outerCircle = _MakeCircle(50, 2.5f, color, 1.0f);
        AddChild(_outerCircle);

        // 内圆
        _innerCircle = _MakeCircle(30, 1.5f, new Color(color, 0.7f), 0.9f);
        AddChild(_innerCircle);

        // 内接多边形（阵法符号）—— 三角形(旋涡)/方形(排斥)/五角(吸引)
        int polySides = FieldType switch
        {
            FieldType.Vortex => 3,
            FieldType.Repulsor => 4,
            FieldType.Attractor => 5,
            _ => 4
        };
        _polygon = _MakePolygon(polySides, 18, 1.5f, color);
        AddChild(_polygon);

        // 流线（显示场方向的动画线）
        _streamLine = new Line2D
        {
            Width = 1.5f,
            DefaultColor = new Color(color, 0.6f),
        };
        AddChild(_streamLine);

        _BuildStreamLine();
    }

    public override void _Process(double delta)
    {
        float d = (float)delta;
        ElapsedTime += d;

        // 旋转动画（旋涡场转得更快）
        float rotSpeed = FieldType == FieldType.Vortex ? 2.0f : 0.5f;
        _rotation += d * rotSpeed;
        _polygon.Rotation = _rotation;
        _outerCircle.Rotation = -_rotation * 0.5f;
        _innerCircle.Rotation = _rotation;

        // 流线动画
        _streamPhase += d * 1.5f;
        _BuildStreamLine();

        // 接近过期时淡出（最后30%）
        float lifeRatio = ElapsedTime / Duration;
        float alpha = lifeRatio > 0.7f
            ? 1.0f - (lifeRatio - 0.7f) / 0.3f
            : 1.0f;
        Modulate = new Color(1, 1, 1, Mathf.Max(0.2f, alpha));

        if (IsExpired)
        {
            QueueFree();
        }
    }

    private Line2D _MakeCircle(float radius, float width, Color color, float alphaMult)
    {
        var line = new Line2D
        {
            Width = width,
            DefaultColor = new Color(color, color.A * alphaMult),
            Closed = true,
        };
        int segments = 32;
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.Tau;
            line.AddPoint(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
        return line;
    }

    private Line2D _MakePolygon(int sides, float radius, float width, Color color)
    {
        var line = new Line2D
        {
            Width = width,
            DefaultColor = color,
            Closed = true,
        };
        for (int i = 0; i < sides; i++)
        {
            float angle = (float)i / sides * Mathf.Tau - Mathf.Pi / 2;
            line.AddPoint(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
        return line;
    }

    /// <summary>
    /// 构建流线——可视化场方向（吸引=向内, 排斥=向外, 旋涡=螺旋）。
    /// </summary>
    private void _BuildStreamLine()
    {
        _streamLine.ClearPoints();
        int points = 12;
        float baseRadius = 45;

        for (int i = 0; i < points; i++)
        {
            float t = (float)i / (points - 1);
            float phase = t * Mathf.Tau + _streamPhase;
            Vector2 p;

            switch (FieldType)
            {
                case FieldType.Attractor:
                    // 向内汇聚的线
                    {
                        float r = baseRadius * (1.0f - t);
                        float a = phase * 0.5f;
                        p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    }
                    break;
                case FieldType.Repulsor:
                    // 向外发散的线
                    {
                        float r = baseRadius * t;
                        float a = phase * 0.5f;
                        p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    }
                    break;
                case FieldType.Vortex:
                    // 螺旋线
                    {
                        float r = baseRadius * (1.0f - t * 0.7f);
                        p = new Vector2(Mathf.Cos(phase), Mathf.Sin(phase)) * r;
                    }
                    break;
                default:
                    p = Vector2.Zero;
                    break;
            }
            _streamLine.AddPoint(p);
        }
    }
}
