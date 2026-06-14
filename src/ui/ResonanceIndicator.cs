namespace ZombieWars.UI;

using Godot;
using System;
using ZombieWars.Math;
using ZombieWars.Core;

/// <summary>
/// 波形共振指示器——战斗 HUD 组件。
/// 实时显示当前波纹的波形曲线和武器频率匹配状态。
/// 当武器频率与波频匹配（共振）时，曲线变为金色并发光。
/// </summary>
public partial class ResonanceIndicator : Control
{
    private WaveResonanceSystem? _waveSystem;
    private Line2D _waveLine = null!;
    private Line2D _weaponLine = null!;
    private Label _titleLabel = null!;
    private Label _statusLabel = null!;

    private float _weaponFrequency = Core.GameConfig.WaveBaseFrequency;
    private float _displayWidth = 180.0f;
    private float _displayHeight = 36.0f;
    private int _sampleCount = 60;

    /// <summary>设置当前武器频率（每帧由 PlayerCombat 更新）</summary>
    public void SetWeaponFrequency(float freq) => _weaponFrequency = freq;

    /// <summary>绑定波形系统</summary>
    public void Bind(WaveResonanceSystem waveSystem) => _waveSystem = waveSystem;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(_displayWidth, _displayHeight + 28);
        MouseFilter = MouseFilterEnum.Ignore;

        // 标题
        _titleLabel = new Label
        {
            Text = "共振加成",
            Position = new Vector2(0, 0),
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", 10);
        _titleLabel.AddThemeColorOverride("font_color", AlchemyTheme.Colors.GoldAccent);
        _titleLabel.AddThemeColorOverride("font_outline_color", AlchemyTheme.Colors.InkBlack);
        _titleLabel.AddThemeConstantOverride("outline_size", 2);
        AddChild(_titleLabel);

        // 波形绘制容器
        var drawRoot = new Control
        {
            Position = new Vector2(0, 16),
            Size = new Vector2(_displayWidth, _displayHeight),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(drawRoot);

        // 背景框
        var bg = new Panel
        {
            Size = new Vector2(_displayWidth, _displayHeight),
            Modulate = new Color(1, 1, 1, 0.5f),
        };
        bg.AddThemeStyleboxOverride("panel", AlchemyTheme.MakeDarkPanel(0.6f));
        drawRoot.AddChild(bg);

        // 波纹曲线（白色）
        _waveLine = new Line2D
        {
            Width = 1.5f,
            DefaultColor = AlchemyTheme.Colors.ParchmentLight,
            Position = new Vector2(0, _displayHeight / 2.0f),
        };
        drawRoot.AddChild(_waveLine);

        // 武器频率曲线（金色，匹配时会与波纹重合）
        _weaponLine = new Line2D
        {
            Width = 1.5f,
            DefaultColor = AlchemyTheme.Colors.GoldAccent,
            Position = new Vector2(0, _displayHeight / 2.0f),
        };
        drawRoot.AddChild(_weaponLine);

        // 状态标签
        _statusLabel = new Label
        {
            Position = new Vector2(0, _displayHeight + 16),
        };
        _statusLabel.AddThemeFontSizeOverride("font_size", 9);
        _statusLabel.AddThemeColorOverride("font_color", AlchemyTheme.Colors.TextDim);
        _statusLabel.AddThemeColorOverride("font_outline_color", AlchemyTheme.Colors.InkBlack);
        _statusLabel.AddThemeConstantOverride("outline_size", 1);
        AddChild(_statusLabel);
    }

    public override void _Process(double delta)
    {
        if (_waveSystem == null || !IsVisibleInTree()) return;

        UpdateWaveform();
        UpdateStatus();
    }

    private float _phase = 0.0f;

    private void UpdateWaveform()
    {
        if (_waveSystem == null) return;

        _phase += (float)GetProcessDeltaTime() * 2.0f;

        // 波纹未激活：显示直线
        if (_waveSystem.HarmonicCount == 0)
        {
            _waveLine.ClearPoints();
            _waveLine.AddPoint(new Vector2(0, 0));
            _waveLine.AddPoint(new Vector2(_displayWidth, 0));
            _weaponLine.ClearPoints();
            _weaponLine.AddPoint(new Vector2(0, 0));
            _weaponLine.AddPoint(new Vector2(_displayWidth, 0));
            return;
        }

        float[] samples = _waveSystem.GetWaveformSamples(_sampleCount);
        float amp = Mathf.Max(_waveSystem.Amplitude, 1.0f);
        float halfH = _displayHeight / 2.0f - 2.0f;

        _waveLine.ClearPoints();
        for (int i = 0; i < _sampleCount; i++)
        {
            float x = (float)i / (_sampleCount - 1) * _displayWidth;
            float t = (float)i / _sampleCount * Mathf.Tau + _phase;
            // 使用波系统采样（静态波形）叠加时间相位实现动画
            float y = samples[i] / amp * halfH * Mathf.Cos(_phase * 0.5f);
            _waveLine.AddPoint(new Vector2(x, y));
        }

        // 武器频率曲线（根据武器频率绘制对应正弦波）
        _weaponLine.ClearPoints();
        float weaponAmp = halfH * 0.7f;
        float weaponOmega = _weaponFrequency * 4.0f; // 放大用于可视化
        for (int i = 0; i < _sampleCount; i++)
        {
            float x = (float)i / (_sampleCount - 1) * _displayWidth;
            float t = (float)i / _sampleCount * Mathf.Tau;
            float y = Mathf.Sin(weaponOmega * t / Mathf.Tau * Mathf.Tau + _phase) * weaponAmp;
            _weaponLine.AddPoint(new Vector2(x, y));
        }

        // 共振时变金并发光
        float mult = _waveSystem.CalculateResonanceMultiplier(_weaponFrequency);
        bool resonating = mult >= 1.5f;
        _weaponLine.DefaultColor = resonating
            ? AlchemyTheme.Colors.GoldBright
            : new Color(AlchemyTheme.Colors.GoldAccent, 0.6f);
        _waveLine.DefaultColor = resonating
            ? AlchemyTheme.Colors.CandleGlow
            : AlchemyTheme.Colors.ParchmentLight;
    }

    private void UpdateStatus()
    {
        if (_waveSystem == null) return;

        if (_waveSystem.HarmonicCount == 0)
        {
            _statusLabel.Text = "暂无尸潮波形";
            _statusLabel.AddThemeColorOverride("font_color", AlchemyTheme.Colors.TextDim);
            return;
        }

        float mult = _waveSystem.CalculateResonanceMultiplier(_weaponFrequency);
        string waveDesc = _waveSystem.HarmonicCount switch
        {
            1 => "正弦波",
            2 => "二阶谐波",
            3 => "三阶谐波",
            _ => $"{_waveSystem.HarmonicCount}阶合成"
        };

        if (mult >= 2.5f)
        {
            _statusLabel.Text = $"★ 共振伤害 ×{mult:F1} ★";
            _statusLabel.AddThemeColorOverride("font_color", AlchemyTheme.Colors.GoldBright);
        }
        else if (mult >= 1.5f)
        {
            _statusLabel.Text = $"共振伤害 ×{mult:F1}";
            _statusLabel.AddThemeColorOverride("font_color", AlchemyTheme.Colors.GoldAccent);
        }
        else
        {
            _statusLabel.Text = $"尸潮波形: {waveDesc}";
            _statusLabel.AddThemeColorOverride("font_color", AlchemyTheme.Colors.TextDim);
        }
    }
}
