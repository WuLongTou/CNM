namespace ZombieWars.Core;

using Godot;
using System;
using System.Threading.Tasks;
using ZombieWars.Enemy;
using ZombieWars.Player;
using ZombieWars.Weapons;
using static ZombieWars.Core.GameEnums;

public partial class AutoTestRunner : Node
{
    private GameManager? _gameManager;
    private Player? _player;
    private bool _started;
    private bool _combatSeen;
    private bool _roundCompleted;
    private bool _spriteRegressionChecksPassed;
    private bool _attackRegressionChecksPassed;
    private int _initialRound;

    public override async void _Ready()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        _gameManager = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (_gameManager == null)
        {
            GD.PrintErr("[AUTOTEST] GameManager not found");
            return;
        }

        _player = GetTree().GetFirstNodeInGroup("player") as Player
            ?? GetNodeOrNull<Player>("/root/Main/Player");
        if (_player == null)
        {
            GD.PrintErr("[AUTOTEST] Player not found");
            return;
        }

        _initialRound = _gameManager.CurrentRound;
        GD.Print("[AUTOTEST] Runner attached");
        _RunSpriteRegressionChecks();
        _spriteRegressionChecksPassed = true;
        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        for (int i = 0; i < 200; i++)
        {
            await ToSignal(GetTree().CreateTimer(0.1f), Timer.SignalName.Timeout);
            if (_gameManager == null || _player == null) return;

            if (!_started && _gameManager.CurrentState == GameState.WeaponSelection)
            {
                _started = true;
                GD.Print($"[AUTOTEST] weapon selection detected at round={_gameManager.CurrentRound}");
                _gameManager.OnUpgradeSkipped();
                GD.Print("[AUTOTEST] skipped selection to enter combat");
            }

            if (!_combatSeen && _gameManager.CurrentState == GameState.Combat)
            {
                _combatSeen = true;
                GD.Print("[AUTOTEST] entered combat");
            }

            if (_combatSeen && !_attackRegressionChecksPassed && _gameManager.CurrentState == GameState.Combat)
            {
                await _RunAttackRegressionChecks();
                _attackRegressionChecksPassed = true;
            }

            if (_combatSeen && _gameManager.CurrentState == GameState.Combat)
            {
                foreach (var zombie in ZombieRegistry.All)
                {
                    if (IsInstanceValid(zombie))
                        zombie.TakeDamage(999999);
                }
            }

            if (!_roundCompleted && _gameManager.CurrentRound > _initialRound)
            {
                _roundCompleted = true;
                GD.Print($"[AUTOTEST] next round reached: {_gameManager.CurrentRound}");
                GetTree().Quit(0);
                return;
            }
        }

        GD.PrintErr("[AUTOTEST] timeout before reaching next round");
        GetTree().Quit(2);
    }

    private void _RunSpriteRegressionChecks()
    {
        if (_player?.Vfx == null)
            throw new Exception("[AUTOTEST] Player VFX missing");

        GD.Print("[AUTOTEST] running sprite regression checks");

        var vfxType = typeof(PlayerVfx);
        var warriorWalkField = vfxType.GetField("_warriorWalkFrames", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var warriorAttackField = vfxType.GetField("_warriorAttackFrames", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var warriorIdleField = vfxType.GetField("_warriorIdle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var rangerWalkField = vfxType.GetField("_rangerWalkFrames", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var mageWalkField = vfxType.GetField("_mageWalkFrames", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var warriorWalk = warriorWalkField?.GetValue(null) as Godot.Collections.Array<Texture2D>;
        var warriorAttack = warriorAttackField?.GetValue(null) as Godot.Collections.Array<Texture2D>;
        var warriorIdle = warriorIdleField?.GetValue(null) as Texture2D;
        var rangerWalk = rangerWalkField?.GetValue(null) as Godot.Collections.Array<Texture2D>;
        var mageWalk = mageWalkField?.GetValue(null) as Godot.Collections.Array<Texture2D>;

        if (warriorWalk == null || warriorWalk.Count != 8)
            throw new Exception($"[AUTOTEST] warrior walk should load 8 frames, got {warriorWalk?.Count ?? -1}");

        if (warriorAttack == null || warriorAttack.Count < 6)
            throw new Exception($"[AUTOTEST] warrior attack should load >= 6 frames, got {warriorAttack?.Count ?? -1}");

        if (warriorIdle == null)
            throw new Exception("[AUTOTEST] warrior idle sprite should be loaded");

        if (rangerWalk == null || rangerWalk.Count != 8)
            throw new Exception($"[AUTOTEST] ranger walk should load 8 frames, got {rangerWalk?.Count ?? -1}");

        if (mageWalk == null || mageWalk.Count != 8)
            throw new Exception($"[AUTOTEST] mage walk should load 8 frames, got {mageWalk?.Count ?? -1}");

        GD.Print("[AUTOTEST] sprite regression checks passed");
    }

    private async Task _RunAttackRegressionChecks()
    {
        if (_player?.Combat == null)
            throw new Exception("[AUTOTEST] Player combat missing");

        var combat = _player.Combat;
        GD.Print("[AUTOTEST] running attack regression checks");

        combat.CanAttack = true;
        combat.PerformAttack();
        if (combat.CanAttack)
            throw new Exception("[AUTOTEST] attack should enter cooldown after PerformAttack");

        combat.RecalculateStats();
        if (!combat.CanAttack || !combat.AttackTimer.IsStopped())
            throw new Exception("[AUTOTEST] RecalculateStats should resync attack state and stop stale cooldown timer");

        int originalHealth = _player.CurrentHealth;
        _player.HealthCostPerAttack = Math.Max(1, originalHealth);
        combat.CanAttack = true;
        combat.PerformAttack();
        if (!combat.CanAttack)
            throw new Exception("[AUTOTEST] failed health-cost attack must not leave player stuck in cooldown");

        _player.HealthCostPerAttack = 0;
        _player.CurrentHealth = originalHealth;
        _player.FireHealthChanged(_player.CurrentHealth);

        combat.CanAttack = false;
        combat.AttackTimer.Start();
        _player.Die();
        if (!combat.CanAttack || !combat.AttackTimer.IsStopped())
            throw new Exception("[AUTOTEST] death should clear stale attack cooldown state");

        GD.Print("[AUTOTEST] attack regression checks passed");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
