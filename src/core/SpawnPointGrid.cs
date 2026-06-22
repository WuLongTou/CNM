namespace ZombieWars.Core;

using Godot;
using System.Collections.Generic;

/// <summary>刷怪点网格 — 从 GameManager 提取的独立组件，维护世界边缘的刷怪点列表。</summary>
public partial class SpawnPointGrid : Node
{
    private readonly List<Vector2> _points = new();

    public override void _Ready()
    {
        GetViewport().SizeChanged += Rebuild;
        Rebuild();
    }

    public Vector2 PickRandom()
    {
        if (_points.Count == 0) Rebuild();
        return _points[(int)(GD.Randi() % _points.Count)];
    }

    public void Rebuild()
    {
        _points.Clear();
        var worldSize = MapContext.CurrentWorldSize;
        var margin = GameConfig.SpawnMargin;
        var topMargin = GameConfig.SpawnTopMarginFallback;

        var topEdge = topMargin + margin;
        var bottomEdge = worldSize.Y - margin;
        var leftEdge = margin;
        var rightEdge = worldSize.X - margin;

        int hSteps = 5 + (int)(4 * GameConfig.WorldScale);
        int vSteps = 4 + (int)(4 * GameConfig.WorldScale);
        for (int i = 0; i <= hSteps; i++)
        {
            float t = i / (float)hSteps;
            _points.Add(new Vector2(t * worldSize.X, topEdge));
            _points.Add(new Vector2(t * worldSize.X, bottomEdge));
        }
        float vSpan = worldSize.Y - topEdge - bottomEdge;
        for (int i = 0; i <= vSteps; i++)
        {
            float t = i / (float)vSteps;
            _points.Add(new Vector2(leftEdge, topEdge + t * vSpan));
            _points.Add(new Vector2(rightEdge, topEdge + t * vSpan));
        }
    }
}
