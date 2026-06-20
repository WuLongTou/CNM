namespace ZombieWars.World;

using Godot;
using ZombieWars.Core;

/// <summary>
/// 程序化烘焙高质量地面纹理:多材质层(基底泥土 / 鹅卵石战斗区 / 血迹 / 放射裂缝 / 边缘苔藓 / 碎石散布)。
/// 用 FastNoiseLite 连续噪声替代旧的离散 hash 噪点,一次性烘焙到 Image 后放大到世界尺寸。
/// 零美术依赖,保持暗黑血色基调。
/// </summary>
public static class ProceduralGround
{
    // 烘焙分辨率（降低至 640×360 仍保持双线性放大到 WorldSize，减少 ~55% 像素处理）
    private const int BakeW = 640;
    private const int BakeH = 360;

    // 中心战斗区鹅卵石椭圆半径占比
    private const float InnerRadiusX = 0.42f;
    private const float InnerRadiusY = 0.40f;

    public static Image Bake(int seed = 20240615)
    {
        var img = Image.CreateEmpty(BakeW, BakeH, false, Image.Format.Rgba8);

        var soil = new FastNoiseLite
        {
            Seed = seed,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Frequency = 0.014f,
            FractalOctaves = 4,
            FractalLacunarity = 2.0f,
            FractalGain = 0.5f
        };
        var detail = new FastNoiseLite
        {
            Seed = seed ^ 0x55aa,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Frequency = 0.11f
        };

        _PaintBaseSoil(img, soil, detail);
        _PaintCobblestone(img, seed);
        _PaintCracks(img, seed);
        _PaintBloodStains(img, seed);
        _PaintEdgeMoss(img, detail);
        _PaintGravel(img, seed);

        img.Resize((int)MapContext.CurrentWorldSize.X, (int)MapContext.CurrentWorldSize.Y,
            Image.Interpolation.Bilinear);
        return img;
    }

    // 基底泥土:多频率连续噪声 + 暖色调偏移
    private static void _PaintBaseSoil(Image img, FastNoiseLite soil, FastNoiseLite detail)
    {
        int w = img.GetWidth();
        int h = img.GetHeight();
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float n = (soil.GetNoise2D(x, y) + 1f) * 0.5f;   // 0..1
                float d = (detail.GetNoise2D(x, y) + 1f) * 0.5f;
                float lum = 0.82f + n * 0.30f + (d - 0.5f) * 0.14f;
                // 暗灰褐基底,略带暖红(暗黑血色基调)
                float r = (0.112f * lum) + n * 0.014f;
                float g = (0.098f * lum) + n * 0.006f;
                float b = (0.084f * lum) - n * 0.010f;
                img.SetPixel(x, y, new Color(r, g, b, 1f));
            }
        }
    }

    // 鹅卵石战斗区:中心椭圆内抖动网格放置径向渐变石块
    private static void _PaintCobblestone(Image img, int seed)
    {
        int w = img.GetWidth();
        int h = img.GetHeight();
        var rng = new RandomNumberGenerator { Seed = (ulong)(seed + 1) };
        float cx = w * 0.5f;
        float cy = h * 0.5f;
        float rx = w * InnerRadiusX;
        float ry = h * InnerRadiusY;

        float spacing = 18f;
        for (float py = spacing * 0.5f; py < h; py += spacing)
        {
            int row = (int)(py / spacing);
            float xOff = (row % 2) * (spacing * 0.5f);
            for (float px = spacing * 0.5f + xOff; px < w; px += spacing)
            {
                float jx = px + rng.RandfRange(-4f, 4f);
                float jy = py + rng.RandfRange(-4f, 4f);
                float ex = (jx - cx) / rx;
                float ey = (jy - cy) / ry;
                float ed = ex * ex + ey * ey;
                if (ed > 1.3f)
                    continue;
                float prominence = Mathf.Clamp(1f - ed * 0.75f, 0f, 1f);
                if (rng.Randf() > prominence * 0.85f + 0.15f)
                    continue;

                float radius = rng.RandfRange(4.5f, 7.5f);
                _StampCobble(img, (int)jx, (int)jy, radius, prominence, rng);
            }
        }
    }

    private static void _StampCobble(Image img, int cx, int cy, float r, float prom,
        RandomNumberGenerator rng)
    {
        int w = img.GetWidth();
        int h = img.GetHeight();
        float grayVar = rng.RandfRange(-0.035f, 0.045f);
        int ir = (int)Mathf.Ceil(r);
        for (int dy = -ir; dy <= ir; dy++)
        {
            for (int dx = -ir; dx <= ir; dx++)
            {
                int x = cx + dx;
                int y = cy + dy;
                if (x < 0 || x >= w || y < 0 || y >= h)
                    continue;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) / r;
                if (dist > 1f)
                    continue;
                float shade = (1f - dist) * 0.55f + 0.45f;
                float stoneGray = (0.30f + grayVar) * shade;
                float highlight = Mathf.Max(0f, 0.35f - dist) * 0.55f;
                float gapDark = dist > 0.82f ? 0.55f : 1f;
                float sR = (stoneGray + highlight) * gapDark;
                float sG = (stoneGray * 0.95f + highlight * 0.9f) * gapDark;
                float sB = (stoneGray * 0.86f + highlight * 0.8f) * gapDark;
                Color basePx = img.GetPixel(x, y);
                float k = prom * 0.92f;
                img.SetPixel(x, y, new Color(
                    Mathf.Lerp(basePx.R, sR, k),
                    Mathf.Lerp(basePx.G, sG, k),
                    Mathf.Lerp(basePx.B, sB, k), 1f));
            }
        }
    }

    // 放射裂缝:从中心向外的抖动暗折线
    private static void _PaintCracks(Image img, int seed)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)(seed + 2) };
        int w = img.GetWidth();
        int h = img.GetHeight();
        float cx = w * 0.5f;
        float cy = h * 0.5f;

        int count = 7;
        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count) * Mathf.Tau + rng.RandfRange(-0.2f, 0.2f);
            float len = rng.RandfRange(w * 0.25f, w * 0.48f);
            float px = cx;
            float py = cy;
            float curAngle = angle;
            int steps = (int)(len / 4f);
            for (int s = 0; s < steps; s++)
            {
                float nx = px + Mathf.Cos(curAngle) * 4f;
                float ny = py + Mathf.Sin(curAngle) * 4f;
                _DrawDarkLine(img, (int)px, (int)py, (int)nx, (int)ny, 0.55f, 1);
                curAngle += rng.RandfRange(-0.25f, 0.25f);
                px = nx;
                py = ny;
            }
        }

        // 散布的短裂缝
        for (int i = 0; i < 10; i++)
        {
            float sx = rng.RandfRange(20, w - 20);
            float sy = rng.RandfRange(20, h - 20);
            float a = rng.RandfRange(0, Mathf.Tau);
            float l = rng.RandfRange(12, 34);
            _DrawDarkLine(img, (int)sx, (int)sy,
                (int)(sx + Mathf.Cos(a) * l), (int)(sy + Mathf.Sin(a) * l), 0.45f, 1);
        }
    }

    private static void _DrawDarkLine(Image img, int x0, int y0, int x1, int y1,
        float darkness, int thick)
    {
        int w = img.GetWidth();
        int h = img.GetHeight();
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        int cx = x0;
        int cy = y0;
        while (true)
        {
            for (int oy = -thick; oy <= thick; oy++)
            {
                for (int ox = -thick; ox <= thick; ox++)
                {
                    int px = cx + ox;
                    int py = cy + oy;
                    if (px < 0 || px >= w || py < 0 || py >= h)
                        continue;
                    Color c = img.GetPixel(px, py);
                    img.SetPixel(px, py, c.Darkened(darkness));
                }
            }
            if (cx == x1 && cy == y1)
                break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; cx += sx; }
            if (e2 < dx) { err += dx; cy += sy; }
        }
    }

    // 血迹斑块:随机径向暗红渐变
    private static void _PaintBloodStains(Image img, int seed)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)(seed + 3) };
        int w = img.GetWidth();
        int h = img.GetHeight();
        for (int i = 0; i < 10; i++)
        {
            int sx = (int)rng.RandfRange(15, w - 15);
            int sy = (int)rng.RandfRange(15, h - 15);
            float radius = rng.RandfRange(18, 48);
            int ir = (int)Mathf.Ceil(radius);
            bool dark = rng.Randf() < 0.5f;
            for (int dy = -ir; dy <= ir; dy++)
            {
                for (int dx = -ir; dx <= ir; dx++)
                {
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > radius)
                        continue;
                    float falloff = 1f - d / radius;
                    falloff *= falloff;
                    int px = sx + dx;
                    int py = sy + dy;
                    if (px < 0 || px >= w || py < 0 || py >= h)
                        continue;
                    Color c = img.GetPixel(px, py);
                    Color blood = dark
                        ? new Color(0.20f, 0.03f, 0.02f)
                        : new Color(0.32f, 0.06f, 0.04f);
                    img.SetPixel(px, py, c.Lerp(blood, falloff * 0.55f));
                }
            }
        }
    }

    // 边缘苔藓:靠近世界边缘的区域 lerp 向潮湿绿褐色
    private static void _PaintEdgeMoss(Image img, FastNoiseLite detail)
    {
        int w = img.GetWidth();
        int h = img.GetHeight();
        float margin = 0.16f;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float edgeX = Mathf.Min(x / (float)w, 1f - x / (float)w);
                float edgeY = Mathf.Min(y / (float)h, 1f - y / (float)h);
                float edge = Mathf.Min(edgeX, edgeY);
                if (edge > margin)
                    continue;
                float t = (margin - edge) / margin;
                float n = (detail.GetNoise2D(x, y) + 1f) * 0.5f;
                t *= 0.5f + n * 0.5f;
                Color c = img.GetPixel(x, y);
                Color moss = new Color(0.06f, 0.085f, 0.05f);
                img.SetPixel(x, y, c.Lerp(moss, Mathf.Clamp(t * 0.7f, 0f, 0.7f)));
            }
        }
    }

    // 碎石散布:细小亮/暗点增加颗粒质感
    private static void _PaintGravel(Image img, int seed)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)(seed + 4) };
        int w = img.GetWidth();
        int h = img.GetHeight();
        for (int i = 0; i < 200; i++)
        {
            int x = (int)rng.RandfRange(2, w - 2);
            int y = (int)rng.RandfRange(2, h - 2);
            bool bright = rng.Randf() < 0.5f;
            Color c = img.GetPixel(x, y);
            Color pebble = bright
                ? c.Lightened(0.18f)
                : c.Darkened(0.22f);
            img.SetPixel(x, y, pebble);
            if (rng.Randf() < 0.4f)
                img.SetPixel(x + 1, y, pebble);
        }
    }
}
