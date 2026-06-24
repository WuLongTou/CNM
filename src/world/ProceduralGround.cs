namespace ZombieWars.World;

using System.Collections.Generic;
using Godot;
using ZombieWars.Core;

/// <summary>
/// 地面纹理加载器 —— 从 Blender 烘焙的 PNG 文件加载（assets/sprites/ground/ground_*.png）。
/// 替代了原来的程序化烘焙（_Paint* 函数族），保留缓存和接口签名。
/// 启动时 GameManager.PreGenerateAll 预热所有地图纹理，避免运行时卡顿。
/// </summary>
public static class ProceduralGround
{
    // 纹理缓存：同一 seed 只加载一次
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

    /// <summary>获取烘焙好的地面纹理（带缓存）。首次调用从 PNG 加载，后续直接返回缓存。</summary>
    public static ImageTexture GetTexture(int seed = 20240615)
    {
        if (_cache.TryGetValue(seed, out var cached))
            return cached;
        var tex = _LoadFromPng(seed);
        if (tex != null) _cache[seed] = tex;
        return tex;
    }

    /// <summary>预生成所有地图的地面纹理（在欢迎画面期间调用，分散首次加载开销）。</summary>
    public static void PreGenerateAll(int[] seeds)
    {
        foreach (var seed in seeds)
        {
            if (!_cache.ContainsKey(seed))
            {
                var tex = _LoadFromPng(seed);
                if (tex != null) _cache[seed] = tex;
            }
        }
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
        // 调整为世界尺寸（按需 resize 到 WorldSize）
        var targetSize = MapContext.CurrentWorldSize;
        if (img.GetWidth() != (int)targetSize.X || img.GetHeight() != (int)targetSize.Y)
        {
            img.Resize((int)targetSize.X, (int)targetSize.Y, Image.Interpolation.Bilinear);
        }
        return ImageTexture.CreateFromImage(img);
    }
}
