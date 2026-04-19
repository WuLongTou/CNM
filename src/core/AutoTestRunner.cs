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

        _player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (_player == null)
        {
            GD.PrintErr("[AUTOTEST] Player not found");
            return;
        }

        _initialRound = _gameManager.CurrentRound;
        GD.Print("[AUTOTEST] Runner attached");
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
