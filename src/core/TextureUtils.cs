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
    /// Load a fixed-grid atlas and split into individual frames.
    /// Each cell is cropped, white-stripped, and resized to NormalSpriteSize.
    /// </summary>
    public static Godot.Collections.Array<Texture2D> LoadAtlasFrames(
        string path, int cols = 4, int rows = 4)
    {
        string cacheKey = $"atlas:{path}_{cols}x{rows}";
        if (_frameCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var frames = new Godot.Collections.Array<Texture2D>();

        if (!FileAccess.FileExists(path))
            return frames;

        var tex = GD.Load<Texture2D>(path);
        if (tex == null) return frames;

        var img = tex.GetImage();
        if (img == null) return frames;

        if (img.GetFormat() != Image.Format.Rgba8)
            img.Convert(Image.Format.Rgba8);

        int cellW = img.GetWidth() / cols;
        int cellH = img.GetHeight() / rows;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int x = col * cellW;
                int y = row * cellH;
                var region = img.GetRegion(new Rect2I(x, y, cellW, cellH));

                if (_IsMostlyTransparent(region))
                    continue;

                _StripWhiteBackground(region, 0.85f);
                region = _ResizeToFit(region, NormalSpriteSize);
                frames.Add(ImageTexture.CreateFromImage(region));
            }
        }

        _frameCache[cacheKey] = frames;
        return frames;
    }

    /// <summary>
    /// Load a sprite sheet image and split it into individual frames.
    /// Uses connected component analysis to detect individual sprite regions.
    /// </summary>
    public static Godot.Collections.Array<Texture2D> LoadAndSplitSpriteSheet(string path)
    {
        var cacheKey = $"split:{path}";
        if (_frameCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var frames = new Godot.Collections.Array<Texture2D>();

        if (!FileAccess.FileExists(path))
            return frames;

        var tex = GD.Load<Texture2D>(path);
        if (tex == null) return frames;

        var img = tex.GetImage();
        if (img == null) return frames;

        if (img.GetFormat() != Image.Format.Rgba8)
            img.Convert(Image.Format.Rgba8);

        // Find connected components (sprites) in the image
        var components = _FindConnectedComponents(img);

        if (components.Count == 0)
        {
            _frameCache[cacheKey] = frames;
            return frames;
        }

        if (components.Count == 1)
        {
            // Single frame
            _StripWhiteBackground(img, 0.85f);
            img = _ResizeToFit(img, NormalSpriteSize);
            frames.Add(ImageTexture.CreateFromImage(img));
            _frameCache[cacheKey] = frames;
            return frames;
        }

        // Calculate grid from component bounding boxes
        var grid = _CalculateGridFromComponents(components, img.GetWidth(), img.GetHeight());

        if (grid.cols <= 1 && grid.rows <= 1)
        {
            // Fallback: single frame
            _StripWhiteBackground(img, 0.85f);
            img = _ResizeToFit(img, NormalSpriteSize);
            frames.Add(ImageTexture.CreateFromImage(img));
            _frameCache[cacheKey] = frames;
            return frames;
        }

        int frameW = img.GetWidth() / grid.cols;
        int frameH = img.GetHeight() / grid.rows;

        // Extract frames from grid cells that contain components
        for (int row = 0; row < grid.rows; row++)
        {
            for (int col = 0; col < grid.cols; col++)
            {
                int x = col * frameW;
                int y = row * frameH;
                var region = img.GetRegion(new Rect2I(x, y, frameW, frameH));

                // Skip fully transparent frames
                if (_IsMostlyTransparent(region))
                    continue;

                _StripWhiteBackground(region, 0.85f);
                region = _ResizeToFit(region, NormalSpriteSize);
                frames.Add(ImageTexture.CreateFromImage(region));
            }
        }

        _frameCache[cacheKey] = frames;
        return frames;
    }

    private struct ComponentBounds
    {
        public int MinX, MinY, MaxX, MaxY;
    }

    private struct GridInfo
    {
        public int cols, rows;
    }

    /// Find all connected opaque regions using flood-fill.
    private static System.Collections.Generic.List<ComponentBounds> _FindConnectedComponents(Image img)
    {
        int w = img.GetWidth();
        int h = img.GetHeight();

        // Binary mask: true = opaque pixel, false = transparent
        var visited = new bool[w, h];
        var components = new System.Collections.Generic.List<ComponentBounds>();

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!visited[x, y] && img.GetPixel(x, y).A >= 0.01f)
                {
                    // Found a new component, flood-fill to find all connected pixels
                    var bounds = _FloodFillComponent(img, visited, x, y);
                    if (bounds.HasValue)
                    {
                        components.Add(bounds.Value);
                    }
                }
            }
        }

        return components;
    }

    /// Flood-fill to find all pixels connected to the starting point.
    private static ComponentBounds? _FloodFillComponent(Image img, bool[,] visited, int startX, int startY)
    {
        int w = img.GetWidth();
        int h = img.GetHeight();

        int minX = startX, minY = startY, maxX = startX, maxY = startY;

        // Use efficient int stacks (avoid Godot.Array's O(n) RemoveAt)
        int capacity = w * h / 4; // Estimate max stack size
        var stackX = new int[capacity];
        var stackY = new int[capacity];
        int stackTop = 0;
        stackX[stackTop] = startX;
        stackY[stackTop] = startY;
        stackTop++;

        while (stackTop > 0)
        {
            stackTop--;
            int x = stackX[stackTop];
            int y = stackY[stackTop];

            if (x < 0 || x >= w || y < 0 || y >= h)
                continue;

            if (visited[x, y])
                continue;

            if (img.GetPixel(x, y).A < 0.01f)
                continue;

            visited[x, y] = true;

            // Update bounds
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;

            // Push neighbors (ensure stack capacity)
            if (stackTop >= capacity - 4)
            {
                // Expand stacks
                var newStackX = new int[capacity * 2];
                var newStackY = new int[capacity * 2];
                System.Array.Copy(stackX, newStackX, capacity);
                System.Array.Copy(stackY, newStackY, capacity);
                stackX = newStackX;
                stackY = newStackY;
                capacity *= 2;
            }

            stackX[stackTop] = x + 1; stackY[stackTop] = y; stackTop++;
            stackX[stackTop] = x - 1; stackY[stackTop] = y; stackTop++;
            stackX[stackTop] = x; stackY[stackTop] = y + 1; stackTop++;
            stackX[stackTop] = x; stackY[stackTop] = y - 1; stackTop++;
        }

        // Only return if component has significant size (filter noise)
        int width = maxX - minX + 1;
        int height = maxY - minY + 1;
        if (width < 5 || height < 5)
            return null;

        return new ComponentBounds { MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY };
    }

    /// Calculate grid dimensions from component positions.
    private static GridInfo _CalculateGridFromComponents(System.Collections.Generic.List<ComponentBounds> components, int imgW, int imgH)
    {
        if (components.Count == 0)
            return new GridInfo { cols = 1, rows = 1 };

        // Calculate average component size
        int totalW = 0, totalH = 0;
        foreach (var c in components)
        {
            totalW += (c.MaxX - c.MinX + 1);
            totalH += (c.MaxY - c.MinY + 1);
        }
        int avgW = totalW / components.Count;
        int avgH = totalH / components.Count;

        // Estimate grid dimensions based on image size and average sprite size
        int cols = System.Math.Max(1, (int)System.Math.Round((double)imgW / avgW));
        int rows = System.Math.Max(1, (int)System.Math.Round((double)imgH / avgH));

        // Clamp to reasonable range based on component count
        int targetCells = cols * rows;
        if (components.Count < targetCells)
        {
            // Try to find a better grid fit
            for (int r = 1; r <= components.Count; r++)
            {
                int c = (components.Count + r - 1) / r;
                if (c * r >= components.Count)
                {
                    // Check if this grid is plausible
                    double colFit = (double)imgW / c;
                    double rowFit = (double)imgH / r;
                    double ratio = System.Math.Min(colFit / avgW, rowFit / avgH);
                    if (ratio > 0.5 && ratio < 2.0)
                    {
                        cols = c;
                        rows = r;
                        break;
                    }
                }
            }
        }

        return new GridInfo { cols = cols, rows = rows };
    }

    /// Check if a frame region is mostly fully transparent.
    private static bool _IsMostlyTransparent(Image img)
    {
        int w = img.GetWidth();
        int h = img.GetHeight();

        // Sample every 4th pixel
        for (int x = 0; x < w; x += 4)
        {
            for (int y = 0; y < h; y += 4)
            {
                if (img.GetPixel(x, y).A >= 0.01f)
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Shift sprite colors toward the blood-red palette:
    /// boost red, reduce green and blue slightly for a warm/reddish tint.
    /// </summary>
    public static void TintToPalette(Image image)
    {
        int w = image.GetWidth();
        int h = image.GetHeight();
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var c = image.GetPixel(x, y);
                if (c.A < 0.01f) continue;
                image.SetPixel(x, y, new Color(
                    Mathf.Clamp(c.R * 1.08f, 0f, 1f),
                    Mathf.Clamp(c.G * 0.92f, 0f, 1f),
                    Mathf.Clamp(c.B * 0.88f, 0f, 1f),
                    c.A));
            }
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
