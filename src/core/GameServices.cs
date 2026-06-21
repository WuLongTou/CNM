namespace ZombieWars.Core;

using System;
using ZombieWars.UI;
using ZombieWars.Vfx;
using ZombieWars.Audio;

/// <summary>
/// Simple service locator for type-safe access to game services.
/// Replaces string-based GetNode() and Call() patterns.
/// </summary>
public static class GameServices
{
    private static UIManager? _uiManager;
    private static GameManager? _gameManager;
    private static SaveManager? _saveManager;
    private static VFXManager? _vfxManager;
    private static SoundManager? _soundManager;

    public static UIManager UI => _uiManager
        ?? throw new InvalidOperationException("UIManager not initialized");

    public static GameManager Game => _gameManager
        ?? throw new InvalidOperationException("GameManager not initialized");

    /// <summary>安全可空访问（场景卸载/重载期间 Game 可能为 null）。</summary>
    public static GameManager? GameOrNull => _gameManager;

    public static SaveManager Save => _saveManager
        ?? throw new InvalidOperationException("SaveManager not initialized");

    public static VFXManager VFX => _vfxManager
        ?? throw new InvalidOperationException("VFXManager not initialized");

    public static SoundManager Audio => _soundManager
        ?? throw new InvalidOperationException("SoundManager not initialized");

    public static void Register(UIManager ui) => _uiManager = ui;
    public static void Register(GameManager game) => _gameManager = game;
    public static void Register(SaveManager save) => _saveManager = save;
    public static void Register(VFXManager vfx) => _vfxManager = vfx;
    public static void Register(SoundManager audio) => _soundManager = audio;

    public static void Clear()
    {
        _uiManager = null;
        _gameManager = null;
        _saveManager = null;
        _vfxManager = null;
        _soundManager = null;
    }
}
