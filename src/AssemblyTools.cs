// AssemblyTools.cs — Global singleton accessor for ZombieWars
// Use this to access common managers from anywhere in the codebase.
//
// Pattern: Static readonly properties that resolve the node path once.
// All getters return null if the node hasn't been added yet (e.g. during early _Ready).

using Godot;
using System;

namespace ZombieWars;

public static partial class AssemblyTools
{
    // ─────────────────────────────────────────────────────────────────────────
    // Node paths — match the actual scene structure in Main.tscn
    // Dynamic nodes (SoundManager, SaveManager, VFXManager) are added at runtime
    // by Main._Ready() / Main.cs — they won't exist until that point.
    // ─────────────────────────────────────────────────────────────────────────

    private const string ROOT_PATH = "/root/Main";
    private const string PLAYER_PATH = "/root/Main/Player";
    private const string GAME_MANAGER_PATH = "/root/Main/GameManager";
    private const string WEAPON_DECK_PATH = "/root/Main/WeaponDeck";
    private const string UI_MANAGER_PATH = "/root/Main/UILayer/UIManager";
    private const string SOUND_MANAGER_PATH = "/root/Main/SoundManager";
    private const string SAVE_MANAGER_PATH = "/root/Main/SaveManager";
    private const string VFX_MANAGER_PATH = "/root/Main/VFXManager";
    private const string BACKGROUND_PATH = "/root/Main/Background";

    // ─────────────────────────────────────────────────────────────────────────
    // Scene singletons — lazily resolved on first access
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>The player CharacterBody2D node.</summary>
    public static ZombieWars.Player.Player? Player =>
        TryGetNode<ZombieWars.Player.Player>(PLAYER_PATH);

    /// <summary>The central GameManager node.</summary>
    public static Core.GameManager? GameManager =>
        TryGetNode<Core.GameManager>(GAME_MANAGER_PATH);

    /// <summary>The WeaponDeck node.</summary>
    public static Weapons.WeaponDeck? WeaponDeck =>
        TryGetNode<Weapons.WeaponDeck>(WEAPON_DECK_PATH);

    /// <summary>The UIManager Control node under UILayer.</summary>
    public static UI.UIManager? UI =>
        TryGetNode<UI.UIManager>(UI_MANAGER_PATH);

    /// <summary>SoundManager — dynamically added by Main._Ready().</summary>
    public static Audio.SoundManager? Audio =>
        TryGetNode<Audio.SoundManager>(SOUND_MANAGER_PATH);

    /// <summary>SaveManager — dynamically added by Main._Ready().</summary>
    public static Core.SaveManager? Save =>
        TryGetNode<Core.SaveManager>(SAVE_MANAGER_PATH);

    /// <summary>VFXManager — dynamically added by Main._Ready().</summary>
    public static ZombieWars.Vfx.VFXManager? VFX =>
        TryGetNode<ZombieWars.Vfx.VFXManager>(VFX_MANAGER_PATH);

    /// <summary>Background CanvasLayer node.</summary>
    public static World.Background? Background =>
        TryGetNode<World.Background>(BACKGROUND_PATH);

    // ─────────────────────────────────────────────────────────────────────────
    // Core singletons (also available individually above)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Shorthand for Audio.</summary>
    public static Audio.SoundManager? SoundManager => Audio;

    /// <summary>Shorthand for VFX.</summary>
    public static ZombieWars.Vfx.VFXManager? VfxManager => VFX;

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Safely get a node by absolute path. Returns null if the node does not
    /// exist or is not of type T. Does NOT throw.
    /// </summary>
    public static T? TryGetNode<T>(string path) where T : class
    {
        if (string.IsNullOrEmpty(path))
            return null;

        var tree = Engine.GetMainLoop();
        if (tree == null)
            return null;

        // GetTree() returns SceneTree which is the main loop in Godot
        if (tree is SceneTree sceneTree)
        {
            var root = sceneTree.CurrentScene;
            if (root == null)
                return null;

            // Guard: IsInstanceValid was removed from the public API in Godot 4.
            // Use the root node's HasNode check instead.
            if (!root.HasNode(path))
                return null;

            var node = root.GetNode(path);
            return node as T;
        }

        return null;
    }

    /// <summary>
    /// Get a node by path, throwing if not found or wrong type.
    /// Use this when you are certain the node exists (e.g. in a @onready equivalent).
    /// </summary>
    public static T GetNode<T>(string path) where T : class
    {
        var result = TryGetNode<T>(path);
        if (result == null)
            throw new InvalidOperationException(
                $"AssemblyTools: Node not found or wrong type at path \"{path}\" (expected {typeof(T).Name})");
        return result;
    }

    /// <summary>
    /// True if the given GodotObject reference is still valid (the node hasn't
    /// been freed from the tree). Use instead of the old GDScript `is_instance_valid()`.
    /// </summary>
    public static bool IsInstanceValid(GodotObject obj)
    {
        return obj != null && !obj.IsQueuedForDeletion();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Convenience utilities
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Center of the current viewport in world coordinates.</summary>
    public static Vector2 GetScreenCenter()
    {
        var viewport = Engine.GetMainLoop() is SceneTree st ? st.CurrentScene?.GetViewport() : null;
        return viewport != null ? viewport.GetVisibleRect().Size / 2 : new Vector2(640, 360);
    }

    /// <summary>Full viewport size in pixels.</summary>
    public static Vector2 GetViewportSize()
    {
        var viewport = Engine.GetMainLoop() is SceneTree st ? st.CurrentScene?.GetViewport() : null;
        return viewport != null ? viewport.GetVisibleRect().Size : new Vector2(1280, 720);
    }

    /// <summary>The main SceneTree, or null if not available.</summary>
    public static SceneTree? GetSceneTree()
    {
        return Engine.GetMainLoop() as SceneTree;
    }

    /// <summary>The current root scene (Main.tscn instance).</summary>
    public static Node? GetCurrentScene()
    {
        return Engine.GetMainLoop() is SceneTree st ? st.CurrentScene : null;
    }

    /// <summary>True if the game tree is paused.</summary>
    public static bool IsPaused()
    {
        return Engine.GetMainLoop() is SceneTree st && st.Paused;
    }

    /// <summary>Quit the game.</summary>
    public static void Quit(int exitCode = 0)
    {
        (Engine.GetMainLoop() as SceneTree)?.Quit();
    }

    /// <summary>Print a message to the Godot console with the [ZombieWars] prefix.</summary>
    public static void Log(string message)
    {
        GD.Print($"[ZombieWars] {message}");
    }

    /// <summary>Print a warning to the Godot console.</summary>
    public static void Warn(string message)
    {
        GD.PushWarning($"[ZombieWars] {message}");
    }

    /// <summary>Print an error to the Godot console.</summary>
    public static void Error(string message)
    {
        GD.PushError($"[ZombieWars] {message}");
    }
}
