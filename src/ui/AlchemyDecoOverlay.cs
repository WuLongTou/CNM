namespace ZombieWars.UI;

using Godot;

/// <summary>
/// 炼金术阵法装饰层——在战斗背景叠加缓慢旋转的粉笔魔法阵。
/// 纯程序化绘制（Line2D），营造炼金术手稿氛围。
/// 性能友好：4个低多边形阵法，每帧旋转。
/// </summary>
public partial class AlchemyDecoOverlay : Node2D
{
    private readonly Line2D[] _circles = new Line2D[4];
    private readonly float[] _rotSpeeds = { 0.08f, -0.05f, 0.12f, -0.07f };
    private readonly float[] _baseRotations = { 0f, 0.4f, 0.8f, 1.2f };
    private float _time;

    public override void _Ready()
    {
        ZIndex = -5; // 在背景之上、僵尸之下
        Modulate = new Color(1, 1, 1, 0.10f); // 非常淡，不干扰游戏

        // 4个不同位置/尺寸的阵法
        var configs = new (Vector2 pos, float radius, int sides, Color color)[]
        {
            (new Vector2(960, 540), 280, 6, new Color(0.788f, 0.659f, 0.298f)),  // 中央六芒星
            (new Vector2(400, 300), 180, 5, new Color(0.290f, 0.486f, 0.435f)),  // 左上五角
            (new Vector2(1500, 780), 200, 3, new Color(0.545f, 0.227f, 0.184f)), // 右下三角
            (new Vector2(1500, 300), 150, 8, new Color(0.357f, 0.227f, 0.420f)), // 右上八边形
        };

        for (int i = 0; i < 4; i++)
        {
            var (pos, radius, sides, color) = configs[i];
            _circles[i] = _BuildCircle(pos, radius, sides, color);
            AddChild(_circles[i]);
        }
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        for (int i = 0; i < _circles.Length; i++)
        {
            if (_circles[i] != null)
                _circles[i].Rotation = _baseRotations[i] + _time * _rotSpeeds[i];
        }
    }

    /// <summary>构建一个魔法阵：外圆 + 内接多边形 + 内圆</summary>
    private static Line2D _BuildCircle(Vector2 pos, float radius, int sides, Color color)
    {
        var root = new Line2D
        {
            Position = pos,
            Width = 2.0f,
            DefaultColor = color,
        };

        // 外圆
        int segments = 48;
        for (int i = 0; i <= segments; i++)
        {
            float a = (float)i / segments * Mathf.Tau;
            root.AddPoint(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
        }

        // 内接多边形（两次，错开形成星形）
        for (int pass = 0; pass < 2; pass++)
        {
            float offset = pass * (Mathf.Pi / sides);
            for (int i = 0; i <= sides; i++)
            {
                float a = (float)i / sides * Mathf.Tau - Mathf.Pi / 2 + offset;
                root.AddPoint(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * 0.85f);
            }
            if (pass < 1) root.AddPoint(new Vector2(0, 0) * 0); // 连接断点占位
        }

        // 内圆
        float innerR = radius * 0.5f;
        for (int i = 0; i <= segments; i++)
        {
            float a = (float)i / segments * Mathf.Tau;
            root.AddPoint(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * innerR);
        }

        return root;
    }
}
