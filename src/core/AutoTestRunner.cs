namespace ZombieWars.Core;

using Godot;
using System;
using System.Threading.Tasks;
using ZombieWars.Enemy;
using static ZombieWars.Core.GameEnums;

public partial class AutoTestRunner : Node
{
    private GameManager? _gameManager;
    private bool _started;
    private bool _combatSeen;
    private bool _roundCompleted;
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

        _initialRound = _gameManager.CurrentRound;
        GD.Print("[AUTOTEST] Runner attached");
        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        for (int i = 0; i < 200; i++)
        {
            await ToSignal(GetTree().CreateTimer(0.1f), Timer.SignalName.Timeout);
            if (_gameManager == null) return;

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

            if (_combatSeen && _gameManager.CurrentState == GameState.Combat)
            {
                // Use ZombieRegistry instead of GetNodesInGroup for better performance
                foreach (var zombie in ZombieRegistry.All)
                {
                    if (IsInstanceValid(zombie))
                    {
                        zombie.TakeDamage(999999);
                    }
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
}
