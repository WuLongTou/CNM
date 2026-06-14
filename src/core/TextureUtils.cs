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
    /// Load an image texture directly from the source asset. This avoids runtime
    /// dependency on Godot's .godot/imported cache in fresh checkouts.
    /// </summary>
    public static Texture2D? LoadTexture(string path)
    {
        string cacheKey = $"raw:{path}";
        if (_textureCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var result = _LoadImportedTexture(path);
        if (result == null)
        {
            var img = _LoadImageFile(path);
            if (img == null)
                return null;
            result = ImageTexture.CreateFromImage(img);
        }
        _textureCache[cacheKey] = result;
        return result;
    }

    /// <summary>
    /// Load a texture from file and normalize it to standard sprite size.
    /// Results are cached for performance - subsequent calls return cached version.
    /// </summary>
    public static Texture2D? LoadNormalizedSprite(string path)
    {
        // Check cache first — early exit avoids re-processing and reference aliasing
        if (_textureCache.TryGetValue(path, out var cached))
            return cached;

        if (!FileAccess.FileExists(path))
            return null;

        var img = _LoadImage(path);
        if (img == null)
            return null;

        // Strip white background
        _StripWhiteBackground(img, 0.85f);

        // Trim transparent borders so the character fills the frame consistently
        img = _TrimTransparent(img);

        // Resize to normalized size — always duplicate so the cached ImageTexture
        // owns its own Image buffer and no external GetImage() caller can mutate it
        img = _ResizeToFit(img, NormalSpriteSize);
        if (img != null)
        {
            var copy = img.Duplicate() as Image;
            if (copy != null) img = copy;
        }

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

        var img = _LoadImage(path);
        if (img == null) return frames;

        int cellW = img.GetWidth() / cols;
        int cellH = img.GetHeight() / rows;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int x = col * cellW;
                int y = row * cellH;
                // 最后一列/行使用剩余像素，避免整数除法丢失边缘
                int regionW = (col == cols - 1) ? img.GetWidth() - x : cellW;
                int regionH = (row == rows - 1) ? img.GetHeight() - y : cellH;
                var region = img.GetRegion(new Rect2I(x, y, regionW, regionH));

                if (_IsMostlyTransparent(region))
                    continue;

                _StripWhiteBackground(region, 0.85f);

                // 白底去除后可能产生全透明帧，二次过滤
                if (_IsMostlyTransparent(region))
                    continue;

                region = _TrimTransparent(region);
                region = _ResizeToFit(region, NormalSpriteSize);
                frames.Add(ImageTexture.CreateFromImage(region));
            }
        }

        _frameCache[cacheKey] = frames;
        return frames;
    }

    /// <summary>
    /// Load a fixed-grid sprite sheet in row-major order while preserving each
    /// cell's canvas. This keeps the sprite anchor stable across animation
    /// frames and avoids projection-split jump cuts.
    /// </summary>
    public static Godot.Collections.Array<Texture2D> LoadSpriteSheetGridFrames(
        string path, int cols = 4, int rows = 4, int maxFrames = 0)
    {
        string cacheKey = $"grid:{path}_{cols}x{rows}_{maxFrames}";
        if (_frameCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var frames = new Godot.Collections.Array<Texture2D>();
        var img = _LoadImage(path);
        if (img == null)
            return frames;

        _StripWhiteBackground(img, 0.85f);

        int cellW = img.GetWidth() / cols;
        int cellH = img.GetHeight() / rows;
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                if (maxFrames > 0 && frames.Count >= maxFrames)
                    break;

                int x = col * cellW;
                int y = row * cellH;
                int regionW = col == cols - 1 ? img.GetWidth() - x : cellW;
                int regionH = row == rows - 1 ? img.GetHeight() - y : cellH;
                var region = img.GetRegion(new Rect2I(x, y, regionW, regionH));
                if (_IsMostlyTransparent(region))
                    continue;

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

        var img = _LoadImage(path);
        if (img == null) return frames;

        // 必须先剥白底再去检测 sprite 区域——否则白色背景像素会被 _FindSpriteRegions
        // 误判为"内容行"，导致整张图被识别为 1 个 region
        _StripWhiteBackground(img, 0.85f);

        // Use projection-based segmentation: scan rows then columns
        // to find bounding boxes of individual sprites by detecting
        // gaps of dark/background pixels between them.
        var regions = _FindSpriteRegions(img);

        if (regions.Count == 0)
        {
            // No sprites detected — return whole image as one frame
            _StripWhiteBackground(img, 0.85f);
            img = _TrimTransparent(img);
            img = _ResizeToFit(img, NormalSpriteSize);
            frames.Add(ImageTexture.CreateFromImage(img));
            _frameCache[cacheKey] = frames;
            return frames;
        }

        // Extract each detected region as a frame
        for (int i = 0; i < regions.Count; i++)
        {
            var r = regions[i];
            var region = img.GetRegion(r);

            // 跳过空帧（无实际内容的区域）
            if (_IsMostlyTransparent(region))
                continue;

            _StripWhiteBackground(region, 0.85f);
            region = _TrimTransparent(region);
            region = _ResizeToFit(region, NormalSpriteSize);
            frames.Add(ImageTexture.CreateFromImage(region));
        }

        _frameCache[cacheKey] = frames;
        return frames;
    }

    /// <summary>
    /// Projection-based sprite region detection.
    /// Scans rows for bright-pixel bands, then columns within each band
    /// to find individual sprite bounding boxes. Only counts non-transparent
    /// pixels (alpha > 0.01) so transparent background doesn't get misread
    /// as content.
    /// </summary>
    private static System.Collections.Generic.List<Rect2I> _FindSpriteRegions(Image img)
    {
        int w = img.GetWidth();
        int h = img.GetHeight();
        const float bright = 0.10f;
        var result = new System.Collections.Generic.List<Rect2I>();

        // Step 1: find "content rows" — rows with at least one non-transparent
        // bright pixel. 必须同时看 alpha：透明背景（即使 RGB=255）不应算内容。
        var contentRow = new bool[h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var c = img.GetPixel(x, y);
                if (c.A > 0.01f && (c.R > bright || c.G > bright || c.B > bright))
                {
                    contentRow[y] = true;
                    break;
                }

            }
        }

        // Step 2: group consecutive content rows into horizontal bands
        int bandStart = -1;
        for (int y = 0; y <= h; y++)
        {
            bool content = y < h && contentRow[y];
            if (content && bandStart < 0)
                bandStart = y;
            else if (!content && bandStart >= 0)
            {
                _SplitBand(img, bandStart, y - 1, bright, result);
                bandStart = -1;
            }
        }

        return result;
    }

    private static Image? _LoadImage(string path)
    {
        var tex = _LoadImportedTexture(path);
        var img = tex?.GetImage();
        if (img == null)
            img = _LoadImageFile(path);
        if (img == null)
            return null;

        if (img.GetFormat() != Image.Format.Rgba8)
            img.Convert(Image.Format.Rgba8);

        return img;
    }

    private static Texture2D? _LoadImportedTexture(string path)
    {
        if (!ResourceLoader.Exists(path))
            return null;

        return ResourceLoader.Load<Texture2D>(path);
    }

    private static Image? _LoadImageFile(string path)
    {
        if (!FileAccess.FileExists(path))
            return null;

        var img = Image.LoadFromFile(path);
        if (img == null || img.GetWidth() <= 0 || img.GetHeight() <= 0)
            return null;

        return img;
    }

    /// Split a horizontal band into individual sprites by scanning columns.
    private static void _SplitBand(Image img, int y0, int y1, float bright,
        System.Collections.Generic.List<Rect2I> result)
    {
        int w = img.GetWidth();

        // Find "content columns" within this band (also requires alpha > 0.01)
        var contentCol = new bool[w];
        for (int x = 0; x < w; x++)
        {
            for (int y = y0; y <= y1; y++)
            {
                var c = img.GetPixel(x, y);
                if (c.A > 0.01f && (c.R > bright || c.G > bright || c.B > bright))
                {
                    contentCol[x] = true;
                    break;
                }
            }
        }

        // Group consecutive content columns into sprites
        int colStart = -1;
        for (int x = 0; x <= w; x++)
        {
            bool content = x < w && contentCol[x];
            if (content && colStart < 0)
                colStart = x;
            else if (!content && colStart >= 0)
            {
                // Add padding to include outlines
                int pad = 3;
                int rx = Mathf.Max(0, colStart - pad);
                int ry = Mathf.Max(0, y0 - pad);
                int rx2 = Mathf.Min(w - 1, x - 1 + pad);
                int ry2 = Mathf.Min(img.GetHeight() - 1, y1 + pad);
                int rw = rx2 - rx + 1;
                int rh = ry2 - ry + 1;
                // 过滤过小碎片（至少 NormalSpriteSize/4 宽和高）
                if (rw >= NormalSpriteSize / 4 && rh >= NormalSpriteSize / 4)
                    result.Add(new Rect2I(rx, ry, rw, rh));
                colStart = -1;
            }
        }
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
        float ratio = Mathf.Min((float)maxSize / w, (float)maxSize / h);
        int newW = (int)(w * ratio);
        int newH = (int)(h * ratio);

        // Always duplicate to ensure the returned Image is owned exclusively
        // (avoids shared buffer issues when the original texture is reused)
        var copy = img.Duplicate() as Image;
        if (copy == null) return img;

        if (copy.GetWidth() != newW || copy.GetHeight() != newH)
            copy.Resize(newW, newH, Image.Interpolation.Nearest);
        return copy;
    }

    /// <summary>
    /// Crop transparent borders, keeping only the bounding box of non-transparent pixels.
    /// Ensures sprites from different sources are sized consistently after resize.
    /// </summary>
    private static Image _TrimTransparent(Image img)
    {
        int w = img.GetWidth();
        int h = img.GetHeight();
        int minX = w, minY = h, maxX = 0, maxY = 0;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (img.GetPixel(x, y).A >= 0.01f)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (maxX < minX || maxY < minY)
            return img;

        // Add a small margin (2px) to avoid clipping edges
        int margin = 2;
        minX = Mathf.Max(0, minX - margin);
        minY = Mathf.Max(0, minY - margin);
        maxX = Mathf.Min(w - 1, maxX + margin);
        maxY = Mathf.Min(h - 1, maxY + margin);

        int cropW = maxX - minX + 1;
        int cropH = maxY - minY + 1;

        if (cropW == w && cropH == h)
            return img;

        return img.GetRegion(new Rect2I(minX, minY, cropW, cropH));
    }

    /// <summary>
    /// Strip white/light background from image for transparency.
    /// </summary>
    private static void _StripWhiteBackground(Image img, float threshold)
    {
        int w = img.GetWidth();
        int h = img.GetHeight();

        // 统计背景颜色倾向（排除全透明像素）
        int darkPixels = 0, brightPixels = 0;
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var c = img.GetPixel(x, y);
                if (c.A < 0.01f) continue;
                float brightness = (c.R + c.G + c.B) / 3.0f;
                if (brightness > threshold)
                    brightPixels++;
                else if (brightness < (1.0f - threshold))
                    darkPixels++;
            }
        }

        // 根据背景倾向选择处理模式
        bool stripBright = brightPixels > darkPixels;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var c = img.GetPixel(x, y);
                float brightness = (c.R + c.G + c.B) / 3.0f;

                if (stripBright)
                {
                    // 处理亮色背景（白/米黄/暗红纹理等）。
                    // 注意：不能用 "R/G/B 三通道都 > threshold" 严格判断，
                    // 因为 aged-paper 风格的背景往往是米黄/暗红色，R、G 高但 B 略低。
                    // 改用 "min(R,G,B) > threshold - 0.10" 容差判断：min 高说明是亮背景，
                    // 任一通道偏低则说明是角色本色（红/绿/蓝等），保留。
                    float minChannel = Mathf.Min(c.R, Mathf.Min(c.G, c.B));
                    if (brightness > threshold && minChannel > (threshold - 0.10f))
                    {
                        float alpha = Mathf.Clamp((1.0f - brightness) / (1.0f - threshold), 0.0f, 1.0f);
                        img.SetPixel(x, y, new Color(c.R, c.G, c.B, alpha * c.A));
                    }
                }
                else
                {
                    // 处理黑色/暗色背景：检测纯黑背景像素并设为透明
                    if (c.R < 0.12f && c.G < 0.12f && c.B < 0.12f)
                    {
                        img.SetPixel(x, y, new Color(c.R, c.G, c.B, 0.0f));
                    }
                }
            }
        }
    }

    public static Image FlipHorizontal(Image src)
    {
        int w = src.GetWidth();
        int h = src.GetHeight();
        var dst = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                dst.SetPixel(x, y, src.GetPixel(w - 1 - x, y));
            }
        }
        return dst;
    }

    public static bool FacesLeft(Texture2D tex)
    {
        float leftMass = 0f, rightMass = 0f;
        _AccumulateHorizontalMass(tex, ref leftMass, ref rightMass);
        return leftMass > rightMass * 1.08f;
    }

    public static bool FramesFaceLeft(Godot.Collections.Array<Texture2D> frames, int sampleCount = 3)
    {
        if (frames.Count == 0)
            return false;

        float leftMass = 0f, rightMass = 0f;
        int count = Mathf.Min(sampleCount, frames.Count);
        for (int i = 0; i < count; i++)
            _AccumulateHorizontalMass(frames[i], ref leftMass, ref rightMass);

        return leftMass > rightMass * 1.08f;
    }

    private static void _AccumulateHorizontalMass(Texture2D tex, ref float leftMass, ref float rightMass)
    {
        var img = tex.GetImage();
        if (img == null) return;
        int w = img.GetWidth();
        int mid = w / 2;
        for (int y = 0; y < img.GetHeight(); y++)
        {
            for (int x = 0; x < w; x++)
            {
                float a = img.GetPixel(x, y).A;
                if (a < 0.1f) continue;
                if (x < mid) leftMass += a;
                else rightMass += a;
            }
        }
    }

    public static Texture2D FlipHorizontal(Texture2D tex)
    {
        var img = tex.GetImage();
        if (img == null) return tex;
        var flipped = FlipHorizontal(img);
        return ImageTexture.CreateFromImage(flipped);
    }
}
