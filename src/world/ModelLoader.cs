namespace ZombieWars.World;

using System.Collections.Generic;
using Godot;
using ZombieWars.Core;

/// <summary>
/// 3D 模型（.glb）加载工具 — 惰性加载 + 缓存。
///
/// 关键：Godot 的 ResourceLoader 必须在引擎初始化后才能调用，不能放在
/// static readonly 字段 = ... 中（CLR 会在 Godot ready 前跑 .cctor），
/// 故用 Dictionary 在首次访问时惰性加载。
///
/// GetScene：原生 3D 加载（供 ModelProxy3D 用 MeshInstance3D 渲染，材质在 3D 管线下
/// 正常显色，解决 MeshInstance2D 把 albedo 当白色的白方块问题）。
/// GetMesh：旧 2D 路径（剥 Mesh + Unshaded hack，已废弃，保留兼容）。
/// </summary>
public static class ModelLoader
{
    private static readonly Dictionary<string, Mesh?> _cache = new();
    private static readonly Dictionary<string, PackedScene?> _sceneCache = new();
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

    /// <summary>原生 3D 加载 .glb 为 PackedScene（不改材质，3D 渲染下原材质正常显色）。
    /// 供 ModelProxy3D 用。不存在返回 null。</summary>
    public static PackedScene? GetScene(string scenePath)
    {
        lock (_lock)
        {
            if (_sceneCache.TryGetValue(scenePath, out var ps)) return ps;
            if (_failed.Contains(scenePath)) return null;

            if (!ResourceLoader.Exists(scenePath))
            {
                _failed.Add(scenePath);
                if (!_logged.Contains(scenePath))
                { LogUtils.Print($"[ModelLoader] 路径不存在: {scenePath}"); _logged.Add(scenePath); }
                return null;
            }
            ps = ResourceLoader.Load<PackedScene>(scenePath);
            if (ps == null)
            {
                _failed.Add(scenePath);
                if (!_logged.Contains(scenePath))
                { LogUtils.Print($"[ModelLoader] 加载 PackedScene 失败: {scenePath}"); _logged.Add(scenePath); }
                return null;
            }
            _sceneCache[scenePath] = ps;
            if (!_logged.Contains(scenePath))
            { LogUtils.Print($"[ModelLoader] ✓ 3D {scenePath}"); _logged.Add(scenePath); }
            return ps;
        }
    }

    /// <summary>将 Mesh 所有 surface 的材质替换为 Unshaded（2D 场景无光照必需）。
    /// 原有材质的 Albedo Color / Emission Color 保留但关掉光照计算。</summary>
    private static void _SetUnshaded(Mesh mesh)
    {
        for (int i = 0; i < mesh.GetSurfaceCount(); i++)
        {
            var srcMat = mesh.SurfaceGetMaterial(i);
            if (srcMat == null)
            {
                // 无材质 surface → 创建一个最小 Unshaded 材质用白色标记
                var fallback = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    AlbedoColor = new Color(1f, 0.2f, 0.2f), // 红色标记无材质 surface
                };
                mesh.SurfaceSetMaterial(i, fallback);
                LogUtils.Print($"[ModelLoader] surface {i} ← 新建 Unshaded (原为空)");
                continue;
            }

            if (srcMat is BaseMaterial3D bm)
            {
                bm.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                LogUtils.Print($"[ModelLoader] surface {i} → Unshaded (albedo={bm.AlbedoColor})");
            }
            else
            {
                // 非 BaseMaterial3D（如 ShaderMaterial）→ 替换为 Unshaded StandardMaterial3D
                var replacement = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    AlbedoColor = new Color(0.5f, 0.8f, 1.0f), // 浅蓝色标记
                };
                mesh.SurfaceSetMaterial(i, replacement);
                LogUtils.Print($"[ModelLoader] surface {i} ← 替换为 Unshaded (原为 {srcMat.GetType().Name})");
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
