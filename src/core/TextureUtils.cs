namespace ZombieWars.Core;

using Godot;
using System.Collections.Generic;

/// <summary>
/// Centralized texture utilities for consistent sprite loading and normalization.
/// Includes texture caching to avoid reloading/processing same assets.
/// </summary>
public static class TextureUtils
{
    /// <summary>
    /// Target size for normalized sprites (in pixels).
    /// All loaded textures will be scaled to this size for consistent display.
    /// </summary>
    public const int NormalSpriteSize = 96;

    // Texture cache to avoid reloading/processing same textures
    private static readonly Dictionary<string, Texture2D> _textureCache = new();
    private static readonly Dictionary<string, Godot.Collections.Array<Texture2D>> _frameCache = new();

    /// <summary>
    /// Load a texture from file and normalize it to standard sprite size.
    /// Results are cached for performance - subsequent calls return cached version.
    /// </summary>
    public static Texture2D? LoadNormalizedSprite(string path)
    {
        // Check cache first
        if (_textureCache.TryGetValue(path, out var cached))
            return cached;

        if (!FileAccess.FileExists(path))
            return null;

        var tex = GD.Load<Texture2D>(path);
        if (tex == null) return null;

        var img = tex.GetImage();
        if (img == null) return tex;

        // Convert to RGBA8 if needed
        if (img.GetFormat() != Image.Format.Rgba8)
            img.Convert(Image.Format.Rgba8);

        // Strip white background
        _StripWhiteBackground(img, 0.85f);

        // Resize to normalized size
        img = _ResizeToFit(img, NormalSpriteSize);

        var result = ImageTexture.CreateFromImage(img);
        _textureCache[path] = result;
        return result;
    }

    /// <summary>
    /// Load a texture from file without normalization.
    /// Use this for backgrounds or UI elements that should preserve original size.
    /// Results are cached.
    /// </summary>
    public static Texture2D? LoadTexture(string path)
    {
        if (_textureCache.TryGetValue(path, out var cached))
            return cached;

        if (!FileAccess.FileExists(path))
            return null;

        var tex = GD.Load<Texture2D>(path);
        if (tex == null) return null;

        var img = tex.GetImage();
        if (img == null) return tex;

        if (img.GetFormat() != Image.Format.Rgba8)
            img.Convert(Image.Format.Rgba8);

        _StripWhiteBackground(img, 0.85f);
        var result = ImageTexture.CreateFromImage(img);
        _textureCache[path] = result;
        return result;
    }

    /// <summary>
    /// Load multiple frames for animation from numbered files.
    /// Results are cached for performance.
    /// </summary>
    public static Godot.Collections.Array<Texture2D> LoadAnimationFrames(
        string basePath, int startIndex, int endIndex, string extension = ".png")
    {
        string cacheKey = $"{basePath}_{startIndex}_{endIndex}{extension}";
        if (_frameCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var frames = new Godot.Collections.Array<Texture2D>();

        for (int i = startIndex; i <= endIndex; i++)
        {
            string path = $"{basePath}{i}{extension}";
            var tex = LoadNormalizedSprite(path);
            if (tex != null)
                frames.Add(tex);
        }

        _frameCache[cacheKey] = frames;
        return frames;
    }

    /// <summary>
    /// Load multiple frames for animation from exact file paths.
    /// Results are cached.
    /// </summary>
    public static Godot.Collections.Array<Texture2D> LoadAnimationFramesExact(
        params string[] paths)
    {
        string cacheKey = string.Join("|", paths);
        if (_frameCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var frames = new Godot.Collections.Array<Texture2D>();

        foreach (var path in paths)
        {
            var tex = LoadNormalizedSprite(path);
            if (tex != null)
                frames.Add(tex);
        }

        _frameCache[cacheKey] = frames;
        return frames;
    }

    /// <summary>
    /// Preload a set of frames into cache. Call during loading screens or scene initialization.
    /// </summary>
    public static void PreloadFrames(string basePath, int startIndex, int endIndex, string extension = ".png")
    {
        for (int i = startIndex; i <= endIndex; i++)
        {
            string path = $"{basePath}{i}{extension}";
            LoadNormalizedSprite(path);
        }
    }

    /// <summary>
    /// Clear all caches. Call this when changing scenes if memory is a concern.
    /// </summary>
    public static void ClearCache()
    {
        _textureCache.Clear();
        _frameCache.Clear();
    }

    /// <summary>
    /// Get cache statistics for debugging.
    /// </summary>
    public static int GetCacheSize() => _textureCache.Count;

    /// <summary>
    /// Resize an image to fit within maxSize while maintaining aspect ratio.
    /// </summary>
    private static Image _ResizeToFit(Image img, int maxSize)
    {
        int w = img.GetWidth();
        int h = img.GetHeight();

        if (w <= maxSize && h <= maxSize)
            return img;

        float ratio = Mathf.Min((float)maxSize / w, (float)maxSize / h);
        int newW = (int)(w * ratio);
        int newH = (int)(h * ratio);

        var resized = img.Duplicate() as Image;
        if (resized != null)
        {
            resized.Resize(newW, newH);
            return resized;
        }
        return img;
    }

    /// <summary>
    /// Strip white/light background from image for transparency.
    /// </summary>
    private static void _StripWhiteBackground(Image img, float threshold)
    {
        int w = img.GetWidth();
        int h = img.GetHeight();

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var c = img.GetPixel(x, y);
                float brightness = (c.R + c.G + c.B) / 3.0f;
                if (brightness > threshold && c.R > threshold && c.G > threshold && c.B > threshold)
                {
                    float alpha = Mathf.Clamp((1.0f - brightness) / (1.0f - threshold), 0.0f, 1.0f);
                    img.SetPixel(x, y, new Color(c.R, c.G, c.B, alpha * c.A));
                }
            }
        }
    }
}
