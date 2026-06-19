namespace ZombieWars.World;

using Godot;

/// <summary>
/// 视差远景：大场景滚动时，远景山脉以不同速度慢移，产生纵深层次感。
/// 两层剪影（远山 + 中景丘陵）+ 雾气，motion_scale 不同实现视差。
/// 增量加在 Background 之下（更靠后），不破坏现有背景渲染。
/// </summary>
public partial class ParallaxBackdrop : Node
{
    public override void _Ready()
    {
        Name = "ParallaxBackdrop";

        var vpSize = GetViewport().GetVisibleRect().Size;
        int texW = Mathf.Max(1024, (int)vpSize.X);

        // 远景山脉（ScrollScale 小 = 滚动最慢，最远）
        var farLayer = new Parallax2D
        {
            ScrollScale = new Vector2(0.30f, 0.08f),
            RepeatSize = new Vector2(texW, 0),
            FollowViewport = true,
            ZIndex = -20,
        };
        var farTex = _MakeMountainTex(texW, (int)(vpSize.Y * 0.55f),
            new Color(0.11f, 0.10f, 0.09f, 0.92f), seed: 11, peaks: 3, amp: 0.42f, baseY: 0.45f);
        var farSprite = new Sprite2D { Texture = farTex, Centered = false, Offset = new Vector2(0, vpSize.Y * 0.45f) };
        farLayer.AddChild(farSprite);
        AddChild(farLayer);

        // 中景丘陵（ScrollScale 中 = 滚动较快，较近）
        var midLayer = new Parallax2D
        {
            ScrollScale = new Vector2(0.55f, 0.12f),
            RepeatSize = new Vector2(texW, 0),
            FollowViewport = true,
            ZIndex = -19,
        };
        var midTex = _MakeMountainTex(texW, (int)(vpSize.Y * 0.40f),
            new Color(0.07f, 0.06f, 0.05f, 0.96f), seed: 23, peaks: 5, amp: 0.50f, baseY: 0.50f);
        var midSprite = new Sprite2D { Texture = midTex, Centered = false, Offset = new Vector2(0, vpSize.Y * 0.60f) };
        midLayer.AddChild(midSprite);
        AddChild(midLayer);
    }

    /// <summary>生成山脉剪影纹理：peaks 个山峰（正弦叠加，周期连续保证 mirroring 无缝）。</summary>
    private static Texture2D _MakeMountainTex(int w, int h, Color color, int seed, int peaks, float amp, float baseY)
    {
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        var rng = new RandomNumberGenerator { Seed = (ulong)seed };

        // 先算轮廓线（baseY 为中线，amp 为起伏幅度）
        float[] line = new float[w];
        for (int x = 0; x < w; x++)
        {
            float t = (float)x / w;
            // 多频正弦叠加（peaks 控制主峰数，保证一个纹理周期内有整数主峰 → mirroring 无缝）
            float y = baseY
                - Mathf.Sin(t * Mathf.Tau * peaks) * amp * 0.30f
                - Mathf.Sin(t * Mathf.Tau * peaks * 2.3f + 1.7f) * amp * 0.14f
                - Mathf.Sin(t * Mathf.Tau * peaks * 4.7f + 0.5f) * amp * 0.07f;
            y += (rng.Randf() - 0.5f) * amp * 0.10f;
            line[x] = Mathf.Clamp(y, 0.05f, 0.95f);
        }

        for (int x = 0; x < w; x++)
        {
            int top = (int)(line[x] * h);
            for (int yy = top; yy < h; yy++)
            {
                float depth = (float)(yy - top) / Mathf.Max(1, h - top);
                img.SetPixel(x, yy, new Color(color.R, color.G, color.B, color.A * (0.75f + depth * 0.25f)));
            }
        }
        return ImageTexture.CreateFromImage(img);
    }
}
