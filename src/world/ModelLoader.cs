namespace ZombieWars.World;

using Godot;

/// <summary>
/// 3D 模型（.glb）加载工具 —— 烘焙自 Blender，导入 Godot 后为 PackedScene。
/// 提取场景内第一个 MeshInstance3D 的 Mesh 资源，缓存为静态字段。
///
/// 用法：
///   private static readonly Mesh? _mesh = ModelLoader.LoadMesh("res://assets/models/foo.glb");
///   var mi = new MeshInstance2D { Mesh = _mesh, Scale = new Vector2(2, 2) };
///   AddChild(mi);
///
/// 兜底：路径不存在 / Mesh 提取失败时返回 null，调用方继续走老的 _Draw 路径。
/// </summary>
public static class ModelLoader
{
    /// <summary>从 .glb PackedScene 提取第一个 Mesh。失败返回 null。</summary>
    public static Mesh? LoadMesh(string scenePath)
    {
        if (!ResourceLoader.Exists(scenePath)) return null;
        var ps = ResourceLoader.Load<PackedScene>(scenePath);
        if (ps == null) return null;
        var inst = ps.Instantiate();
        var mi3d = FindFirstMeshInstance3D(inst);
        Mesh? mesh = mi3d?.Mesh;
        inst.QueueFree();
        return mesh;
    }

    private static MeshInstance3D? FindFirstMeshInstance3D(Node node)
    {
        if (node is MeshInstance3D mi) return mi;
        foreach (var child in node.GetChildren())
        {
            var found = FindFirstMeshInstance3D(child);
            if (found != null) return found;
        }
        return null;
    }
}
