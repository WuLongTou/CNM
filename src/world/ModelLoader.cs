namespace ZombieWars.World;

using System.Collections.Generic;
using Godot;
using ZombieWars.Core;

/// <summary>
/// 3D 模型（.glb）加载工具 — 惰性加载 + 缓存 Mesh。
///
/// 关键：Godot 的 ResourceLoader 必须在引擎初始化后才能调用，不能放在
/// static readonly 字段 = ... 中（CLR 会在 Godot ready 前跑 .cctor）。
/// 所以改用 Lazy 包装，首次 GetMesh() 时真正加载。
///
/// Emission-only 材质（Blender 烘焙用纯 Emission 适配 2D 无光照场景）。
/// </summary>
public static class ModelLoader
{
    private static readonly Dictionary<string, Mesh?> _cache = new();
    private static readonly HashSet<string> _failed = new();
    private static readonly HashSet<string> _logged = new();
    private static readonly object _lock = new();

    /// <summary>获取 .glb 中的 Mesh（惰性加载 + 缓存）。不存在返回 null。</summary>
    public static Mesh? GetMesh(string scenePath)
    {
        lock (_lock)
        {
            if (_failed.Contains(scenePath)) return null;
            if (_cache.TryGetValue(scenePath, out var m)) return m;

            if (!ResourceLoader.Exists(scenePath))
            {
                _failed.Add(scenePath);
                if (!_logged.Contains(scenePath))
                { LogUtils.Print($"[ModelLoader] 路径不存在: {scenePath}"); _logged.Add(scenePath); }
                return null;
            }
            var ps = ResourceLoader.Load<PackedScene>(scenePath);
            if (ps == null)
            {
                _failed.Add(scenePath);
                if (!_logged.Contains(scenePath))
                { LogUtils.Print($"[ModelLoader] 加载 PackedScene 失败: {scenePath}"); _logged.Add(scenePath); }
                return null;
            }
            var inst = ps.Instantiate();
            var mi3d = FindFirstMeshInstance3D(inst);
            Mesh? mesh = mi3d?.Mesh;
            if (mesh != null) mesh = (Mesh)mesh.Duplicate();
            inst.QueueFree();

            if (mesh != null)
            {
                // 务必将所有 surface 材质设为 Unshaded（2D 场景无 3D 光照）
                _SetUnshaded(mesh);
                _cache[scenePath] = mesh;
                if (!_logged.Contains(scenePath))
                { LogUtils.Print($"[ModelLoader] ✓ {scenePath} (surfaces={mesh.GetSurfaceCount()})"); _logged.Add(scenePath); }
            }
            else
            {
                _failed.Add(scenePath);
                if (!_logged.Contains(scenePath))
                { LogUtils.Print($"[ModelLoader] ✗ 未识别 Mesh: {scenePath}"); _logged.Add(scenePath); }
            }
            return mesh;
        }
    }

    /// <summary>将 Mesh 所有 surface 的材质设为 Unshaded（2D 场景无光照必需）。</summary>
    private static void _SetUnshaded(Mesh mesh)
    {
        for (int i = 0; i < mesh.GetSurfaceCount(); i++)
        {
            var mat = mesh.SurfaceGetMaterial(i);
            if (mat is BaseMaterial3D bm)
            {
                bm.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                LogUtils.Print($"[ModelLoader] surface {i} → Unshaded");
            }
        }
    }

    /// <summary>查找节点树中第一个 MeshInstance3D。</summary>
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
