namespace ZombieWars.Render;

using System.Collections.Generic;
using Godot;
using ZombieWars.Core;
using ZombieWars.World;

/// <summary>
/// 3D 模型视觉代理:在 <see cref="RenderViewport"/> 内用 MeshInstance3D 原生渲染 glb。
///
/// 3D 渲染管线下 glb 原材质(StandardMaterial3D albedo + emission)正常显色,
/// 彻底解决 MeshInstance2D 把 albedo 当白色的白方块问题。
///
/// 2.5D:位置由绑定的 2D 玩法节点每帧同步(2D 坐标 → 3D,见 <see cref="Coord"/>)。
/// 支持 SetModulate(3D 无 Modulate 属性 → 逐 surface 改 albedo)、SetMeshScene(多帧,如宝箱)、
/// Offset(受击抖动偏移)、Scale(继承自 Node3D)。
/// </summary>
public partial class ModelProxy3D : Node3D
{
    private Node2D? _source;          // 绑定的 2D 玩法节点(位置同步源)
    private Node3D? _root;             // glb Instantiate 的根(挂在本节点下,保留 glb transform)
    private MeshInstance3D? _meshInst; // 根内首个 MeshInstance3D(调色目标)
    private readonly Dictionary<int, Color> _origAlbedo = new();
    private Color _modulate = Colors.White;
    private bool _modulateDirty;
    private bool _meshUnique;          // mesh 是否已 Duplicate(调色需独立,避免改共享 glb mesh)

    /// <summary>渲染位置的本地偏移(受击抖动等用,叠加在 2D 同步位置之上)。</summary>
    public Vector3 Offset { get; set; } = Vector3.Zero;

    /// <summary>是否成功挂载了模型(MeshInstance3D 存在)。</summary>
    public bool HasModel => _meshInst != null;

    /// <summary>当前调色(白色 = 未调色,用原材质)。</summary>
    public Color Modulate => _modulate;

    /// <summary>按水平占地(XZ)自适应缩放:让模型最大水平维度 = targetPixels。
    /// 各 glb aabb 尺寸差异大(0.4~18),统一 Scale 不可行,故按占地归一。
    /// 俯视相机下水平尺寸即视觉占地,高度方向被压缩不显示。</summary>
    public void FitToFootprint(float targetPixels)
    {
        if (_meshInst?.Mesh == null) return;
        var size = _meshInst.Mesh.GetAabb().Size;
        float maxHoriz = Mathf.Max(size.X, size.Z);
        if (maxHoriz > 0.001f)
            Scale = Vector3.One * (targetPixels / maxHoriz);
    }

    /// <summary>便捷工厂:创建代理 + 加载 glb + 绑定 2D owner + 按占地缩放 + 挂到 RenderViewport。
    /// 加载失败返回 null(调用方走 _Draw 兜底)。</summary>
    public static ModelProxy3D? CreateFor(Node2D owner, string glbPath, float footprint)
    {
        var proxy = new ModelProxy3D();
        proxy.SetMeshFromPath(glbPath);
        if (!proxy.HasModel) return null;
        proxy.BindTo2D(owner);
        proxy.FitToFootprint(footprint);
        RenderViewport.Instance?.AddProxy(proxy);
        return proxy;
    }

    /// <summary>从 glb 路径加载模型(<see cref="ModelLoader.GetScene"/> 原生 3D 缓存)。</summary>
    public void SetMeshFromPath(string glbPath)
    {
        var ps = ModelLoader.GetScene(glbPath);
        if (ps != null) SetMeshScene(ps);
    }

    /// <summary>用 PackedScene 设置/替换模型(保留 glb 根的 transform)。</summary>
    public void SetMeshScene(PackedScene ps)
    {
        _ClearModel();
        _root = ps.Instantiate() as Node3D;
        if (_root == null) return;
        AddChild(_root);
        _meshInst = _FindFirstMeshInstance3D(_root);
        _RecordOrigAlbedo();
        _meshUnique = false;
        if (_modulate != Colors.White) _modulateDirty = true;
    }

    /// <summary>绑定 2D 玩法节点:每帧同步其 GlobalPosition 到本代理 3D 位置。</summary>
    public void BindTo2D(Node2D source) => _source = source;

    /// <summary>调色(等价 MeshInstance2D.Modulate):乘到各 surface albedo。白色 = 不调色(用原材质)。</summary>
    public void SetModulate(Color color)
    {
        _modulate = color;
        _modulateDirty = true;
    }

    public override void _Process(double delta)
    {
        // 位置同步:2D 玩法层 GlobalPosition → 3D 渲染层(+ 抖动偏移)
        if (_source != null && GodotObject.IsInstanceValid(_source))
            GlobalPosition = Coord.World2DTo3D(_source.GlobalPosition) + Offset;

        if (_modulateDirty && _meshInst?.Mesh != null)
        {
            _ApplyModulate();
            _modulateDirty = false;
        }
    }

    private void _ClearModel()
    {
        if (_root != null) { _root.QueueFree(); _root = null; }
        _meshInst = null;
        _origAlbedo.Clear();
    }

    private void _RecordOrigAlbedo()
    {
        _origAlbedo.Clear();
        if (_meshInst?.Mesh == null) return;
        for (int i = 0; i < _meshInst.Mesh.GetSurfaceCount(); i++)
        {
            if (_meshInst.Mesh.SurfaceGetMaterial(i) is BaseMaterial3D m)
                _origAlbedo[i] = m.AlbedoColor;
        }
    }

    private void _ApplyModulate()
    {
        if (_meshInst?.Mesh == null) return;
        if (_modulate == Colors.White) return; // 白色 = 保留原材质

        // 调色需 mesh 独立(避免改共享 glb mesh,影响其他代理)
        if (!_meshUnique)
        {
            _meshInst.Mesh = _meshInst.Mesh.Duplicate() as Mesh;
            _meshUnique = true;
        }
        var mesh = _meshInst.Mesh!;
        for (int i = 0; i < mesh.GetSurfaceCount(); i++)
        {
            if (!_origAlbedo.TryGetValue(i, out var orig)) continue;
            // Duplicate 当前材质(保留 roughness/emission/metallic 等),只改 albedo = 原色 × modulate
            var src = mesh.SurfaceGetMaterial(i) as BaseMaterial3D;
            var m = src != null ? (BaseMaterial3D)src.Duplicate() : new StandardMaterial3D();
            m.AlbedoColor = new Color(
                orig.R * _modulate.R, orig.G * _modulate.G,
                orig.B * _modulate.B, orig.A * _modulate.A);
            mesh.SurfaceSetMaterial(i, m);
        }
    }

    private static MeshInstance3D? _FindFirstMeshInstance3D(Node node)
    {
        if (node is MeshInstance3D mi) return mi;
        foreach (var c in node.GetChildren())
        {
            var f = _FindFirstMeshInstance3D(c);
            if (f != null) return f;
        }
        return null;
    }
}
