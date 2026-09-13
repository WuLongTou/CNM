namespace ZombieWars.World;

using System;
using System.Collections.Generic;
using Godot;
using ZombieWars.Core;

/// <summary>
/// 地图管理器 —— 持有当前地图数据并同步更新 MapContext。
/// 持续探索模式下一局内地图固定：开局随机选一张，局内不再切换。
/// 场景重建由 Main 订阅 MapChanged 事件后委托 MapBuilder/Background 完成。
/// </summary>
public partial class MapManager : Node
{
    public MapData Current { get; private set; } = null!;
    public event Action<MapData>? MapChanged;

    private List<MapData> _maps = new();
    private int _lastIndex = -1;

    public override void _Ready()
    {
        AddToGroup("map_manager");
        _maps = MapLibrary.BuildAll();
        Current = _maps[0];
        MapContext.CurrentWorldSize = Current.WorldSize;
    }

    /// <summary>游戏开始时调用一次，随机选定首张地图。</summary>
    public void SelectInitialRandom()
    {
        SelectRandom();
    }

    /// <summary>随机选一张地图（避免与上一张相同），同步上下文并广播事件。</summary>
    public void SelectRandom()
    {
        if (_maps.Count == 0) return;
        int idx;
        int guard = 0;
        do
        {
            idx = GD.RandRange(0, _maps.Count - 1);
            guard++;
        } while (idx == _lastIndex && _maps.Count > 1 && guard < 8);

        _lastIndex = idx;
        Current = _maps[idx];
        MapContext.CurrentWorldSize = Current.WorldSize;
        LogUtils.Print($"[MapManager] 切换地图 → {Current.Name}（{Current.Id}，{Current.Obstacles.Count} 个障碍）");
        MapChanged?.Invoke(Current);
    }
}
