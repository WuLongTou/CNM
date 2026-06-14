// PlayerInput.cs — Decoupled input handler for the Player node.
// Attach this as a child of Player (or keep it standalone and reference Player),
// then read MoveDirection / IsAttackPressed / IsDashPressed / IsCycleTargetPressed
// from the owning Player's _PhysicsProcess.
//
// This keeps input capture separate from movement/combat logic, making both
// easier to test and port to C# from the original GDScript Player.gd.

using Godot;
using System;
using ZombieWars.Core;
using static ZombieWars.Core.GameEnums;

namespace ZombieWars.Player;

/// <summary>
/// Captures and exposes input state each frame. Designed to be a child Node
/// of Player or a sibling accessed via <see cref="PlayerRef"/>.
/// </summary>
public partial class PlayerInput : Node
{
    /// <summary>Reference to the owning Player node. Set by Player._Ready().</summary>
    public Player? PlayerRef { get; set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Input state — read these from Player._PhysicsProcess()
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Normalised 2D movement direction from WASD / arrow keys.</summary>
    public Vector2 MoveDirection { get; private set; }

    /// <summary>True on the single frame Space is held or left mouse button is pressed.</summary>
    public bool IsAttackPressed { get; private set; }

    /// <summary>True on the single frame Shift is pressed.</summary>
    public bool IsDashPressed { get; private set; }

    /// <summary>True on the single frame Tab is pressed.</summary>
    public bool IsCycleTargetPressed { get; private set; }

    /// <summary>True on the single frame H is pressed.</summary>
    public bool IsHelpToggled { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Input action names — mirror project.godot [input] section
    // ─────────────────────────────────────────────────────────────────────────

    private const string ACTION_MOVE_LEFT = "move_left";
    private const string ACTION_MOVE_RIGHT = "move_right";
    private const string ACTION_MOVE_UP = "move_up";
    private const string ACTION_MOVE_DOWN = "move_down";
    private const string ACTION_ATTACK = "attack";
    private const string ACTION_DASH = "dash";
    private const string ACTION_CYCLE_TARGET = "cycle_target";
    private const string ACTION_TOGGLE_HELP = "toggle_help";

    /// <summary>Cached GameManager reference (null if not loaded yet).</summary>
    private Core.GameManager? _gameManager;

    // ─────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        // Input capture must happen before physics so state is ready for
        // Player._PhysicsProcess().
        ProcessPriority = -10; // Run before the Player node
    }

    // ─────────────────────────────────────────────────────────────────────────
    // _Input — called before _Process / _PhysicsProcess each frame
    // ─────────────────────────────────────────────────────────────────────────

    public override void _Input(InputEvent @event)
    {
        // 卡片/暂停/Help/GameOver 等非战斗阶段：禁用所有游戏操作输入。
        // 只允许 UIManager 内部处理卡片快捷键（如 1/2/3）。
        // 这是硬约束：任何模态/选择/UI 页面打开时，游戏操作快捷键绝不能生效，
        // 避免误触移动/攻击破坏页面状态。
        if (_gameManager == null || !IsInstanceValid(_gameManager))
            _gameManager = GetTree().GetFirstNodeInGroup("game_manager") as Core.GameManager;
        if (_gameManager != null && _gameManager.CurrentState != GameState.Combat)
        {
            // 清空所有输入状态，确保 Player 不会读到残留输入
            MoveDirection = Vector2.Zero;
            IsAttackPressed = false;
            IsDashPressed = false;
            IsCycleTargetPressed = false;
            IsHelpToggled = false;
            return;
        }

        // ── Movement axis ──────────────────────────────────────────────────
        float horizontal = Input.GetAxis(ACTION_MOVE_LEFT, ACTION_MOVE_RIGHT);
        float vertical = Input.GetAxis(ACTION_MOVE_UP, ACTION_MOVE_DOWN);
        MoveDirection = new Vector2(horizontal, vertical);

        if (MoveDirection.Length() > 1.0f)
            MoveDirection = MoveDirection.Normalized();

        // ── Attack (space bar OR left mouse button just pressed) ──────────
        bool spaceHeld = Input.IsActionPressed(ACTION_ATTACK);
        bool leftMouseJustPressed = @event is InputEventMouseButton emb
            && emb.Pressed && emb.ButtonIndex == MouseButton.Left;
        IsAttackPressed = spaceHeld || leftMouseJustPressed;

        // ── Dash — single frame ─────────────────────────────────────────────
        IsDashPressed = Input.IsActionJustPressed(ACTION_DASH);

        // ── Cycle target — single frame ─────────────────────────────────────
        IsCycleTargetPressed = Input.IsActionJustPressed(ACTION_CYCLE_TARGET);

        // ── Help toggle ────────────────────────────────────────────────────
        IsHelpToggled = Input.IsActionJustPressed(ACTION_TOGGLE_HELP);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Convenience helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>True if any movement key is currently held.</summary>
    public bool IsMoving => MoveDirection != Vector2.Zero;

    /// <summary>True if the player is standing still.</summary>
    public bool IsIdle => MoveDirection == Vector2.Zero;

    /// <summary>Movement speed scalar (0.0 – 1.0).</summary>
    public float MoveMagnitude => MoveDirection.Length();

    /// <summary>Horizontal movement only (-1, 0, or 1).</summary>
    public float Horizontal => MoveDirection.X;

    /// <summary>Vertical movement only (-1, 0, or 1).</summary>
    public float Vertical => MoveDirection.Y;

    /// <summary>Reset all state to zero. Call when the player dies.</summary>
    public void Reset()
    {
        MoveDirection = Vector2.Zero;
        IsAttackPressed = false;
        IsDashPressed = false;
        IsCycleTargetPressed = false;
        IsHelpToggled = false;
    }
}
