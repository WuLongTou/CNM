namespace ZombieWars.World;

using System.Collections.Generic;
using Godot;
using ZombieWars.Core;

/// <summary>
/// 地面纹理加载器 —— 从 Blender 烘焙的 PNG 文件加载（assets/sprites/ground/ground_*.png）。
/// 按需加载当前地图一张纹理（探索模式下一局只用一张，不再全量预热 9 张）。
/// 直接使用 PNG 原生分辨率（1920），由 GPU 拉伸显示——CPU 侧不再放大
/// （放大不增加细节，只浪费启动时间与显存：3840×2160 RGBA ≈ 33MB/张）。
/// </summary>
public static class ProceduralGround
{
    // 纹理缓存：同一 seed 只加载一次（探索模式下实际只会驻留一张）
    private static readonly Dictionary<int, ImageTexture> _cache = new();

    // seed → 文件名映射（与 MapData._Gen* 函数的 seed 一致）
    private static readonly Dictionary<int, string> _seedToFile = new()
    {
        [11111] = "res://assets/sprites/ground/ground_graveyard.png",
        [22222] = "res://assets/sprites/ground/ground_ruins.png",
        [33333] = "res://assets/sprites/ground/ground_dungeon.png",
        [44444] = "res://assets/sprites/ground/ground_swamp.png",
        [55555] = "res://assets/sprites/ground/ground_cathedral.png",
        [66666] = "res://assets/sprites/ground/ground_wasteland.png",
        [77777] = "res://assets/sprites/ground/ground_glacier.png",
        [88888] = "res://assets/sprites/ground/ground_volcano.png",
        [99999] = "res://assets/sprites/ground/ground_desert.png",
    };

    /// <summary>获取烘焙好的地面纹理（带缓存）。缺失时返回 null，由调用方兜底。</summary>
    public static ImageTexture? GetTexture(int seed = 20240615)
    {
        if (_cache.TryGetValue(seed, out var cached))
            return cached;
        var tex = _LoadFromPng(seed);
        if (tex != null) _cache[seed] = tex;
        return tex;
    }

    /// <summary>保留旧 API 兼容：返回 raw Image（不再程序化生成，直接从 PNG 加载）。</summary>
    public static Image Bake(int seed = 20240615)
    {
        var tex = GetTexture(seed);
        if (tex == null)
            return Image.CreateEmpty(2, 2, false, Image.Format.Rgba8);
        return tex.GetImage();
    }

    private static ImageTexture? _LoadFromPng(int seed)
    {
        if (!_seedToFile.TryGetValue(seed, out var path))
        {
            LogUtils.Print($"[ProceduralGround] 未知 seed {seed}，无对应地面纹理");
            return null;
        }
        if (!ResourceLoader.Exists(path))
        {
            LogUtils.Print($"[ProceduralGround] 地面纹理不存在: {path}");
            return null;
        }
        var img = ResourceLoader.Load<Texture2D>(path)?.GetImage();
        if (img == null)
        {
            LogUtils.Print($"[ProceduralGround] 地面纹理加载失败: {path}");
            return null;
        }
        return ImageTexture.CreateFromImage(img);
    }
}
