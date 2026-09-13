namespace ZombieWars.World;

using System;
using Godot;
using ZombieWars.Core;

/// <summary>
/// 地图管理器 —— 每局按随机（主题，种子）生成一张新地图并同步更新 MapContext。
/// 持续探索模式下一局内地图固定：开局生成一张，局内不再切换。
/// 场景重建由 Main 订阅 MapChanged 事件后委托 MapBuilder/Background 完成。
/// </summary>
public partial class MapManager : Node
{
    public MapData Current { get; private set; } = null!;

    public event Action<MapData>? MapChanged;

    private static readonly MapTheme[] Themes =
        (MapTheme[])System.Enum.GetValues(typeof(MapTheme));

    private MapTheme _lastTheme;
    private bool _hasLastTheme;

    public override void _Ready()
    {
        AddToGroup("map_manager");
        // 先给一个默认尺寸，避免 Background._Ready 时世界尺寸未定义
        MapContext.CurrentWorldSize = GameConfig.WorldSize;
        // 首张图走生成器（种子随机，保证每局不同）
        SelectRandom();
    }

    /// <summary>游戏开始时调用一次（MapManager._Ready 已生成首张，此处重新随机，保证与旧流程一致）。</summary>
    public void SelectInitialRandom()
    {
        SelectRandom();
    }

    /// <summary>生成一张新地图（主题 rese，避免与上一张相同），同步上下文并广播事件。</summary>
    public void SelectRandom()
    {
        var theme = _PickTheme();
        int seed = (int)GD.Randi();
        Current = MapGenerator.Generate(theme, seed, GameConfig.WorldSize);
        MapContext.CurrentWorldSize = Current.WorldSize;
        LogUtils.Print($"[MapManager] 生成地图 → {Current.Name}（{Current.Id}，{Current.Obstacles.Count} 个障碍）");
        MapChanged?.Invoke(Current);
    }

    private MapTheme _PickTheme()
    {
        if (Themes.Length == 0) return MapTheme.Graveyard;
        MapTheme theme;
        int guard = 0;
        do
        {
            theme = Themes[(int)(GD.Randi() % (uint)Themes.Length)];
            guard++;
        } while (_hasLastTheme && theme == _lastTheme && Themes.Length > 1 && guard < 8);
        _lastTheme = theme;
        _hasLastTheme = true;
        return theme;
    }
}
