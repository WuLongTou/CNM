namespace ZombieWars.World;

using System.Collections.Generic;
using Godot;
using ZombieWars.Core;

/// <summary>
/// 地面纹理加载器 —— 从 Blender 烘焙的 PNG 文件加载（assets/sprites/ground/ground_*.png）。
/// 按地图主题取对应纹理（探索模式下一局只用一张，带缓存）。
/// 直接使用 PNG 原生分辨率（1920），由 GPU 拉伸显示——CPU 侧不再放大
/// （放大不增加细节，只浪费启动时间与显存：3840×2160 RGBA ≈ 33MB/张）。
/// 区域混色（GroundMask + ground_blend shader）仍由程序化 seed 驱动，保留每局变化。
/// </summary>
public static class ProceduralGround
{
    // 主题 → 烘焙纹理（与 MapTheme 一一对应）
    private static readonly Dictionary<MapTheme, string> _themeToFile = new()
    {
        [MapTheme.Graveyard] = "res://assets/sprites/ground/ground_graveyard.png",
        [MapTheme.Ruins] = "res://assets/sprites/ground/ground_ruins.png",
        [MapTheme.Dungeon] = "res://assets/sprites/ground/ground_dungeon.png",
        [MapTheme.Swamp] = "res://assets/sprites/ground/ground_swamp.png",
        [MapTheme.Cathedral] = "res://assets/sprites/ground/ground_cathedral.png",
        [MapTheme.Wasteland] = "res://assets/sprites/ground/ground_wasteland.png",
        [MapTheme.Glacier] = "res://assets/sprites/ground/ground_glacier.png",
        [MapTheme.Volcano] = "res://assets/sprites/ground/ground_volcano.png",
        [MapTheme.Desert] = "res://assets/sprites/ground/ground_desert.png",
    };

    // 路径缓存：同一纹理只加载一次（探索模式下实际只会驻留一张）
    private static readonly Dictionary<string, ImageTexture> _cache = new();

    /// <summary>按地图主题获取烘焙地面纹理（带缓存）。缺失时返回 null，由调用方兜底。</summary>
    public static ImageTexture? GetTextureForTheme(MapTheme theme)
    {
        if (!_themeToFile.TryGetValue(theme, out var path))
        {
            LogUtils.Print($"[ProceduralGround] 主题 {theme} 无对应地面纹理");
            return null;
        }
        if (_cache.TryGetValue(path, out var cached))
            return cached;
        var tex = _LoadFromPng(path, theme.ToString());
        if (tex != null) _cache[path] = tex;
        return tex;
    }

    private static ImageTexture? _LoadFromPng(string path, string label)
    {
        if (!ResourceLoader.Exists(path))
        {
            LogUtils.Print($"[ProceduralGround] 地面纹理不存在: {path}");
            return null;
        }
        var img = ResourceLoader.Load<Texture2D>(path)?.GetImage();
        if (img == null)
        {
            LogUtils.Print($"[ProceduralGround] 地面纹理加载失败: {path} ({label})");
            return null;
        }
        return ImageTexture.CreateFromImage(img);
    }
}
