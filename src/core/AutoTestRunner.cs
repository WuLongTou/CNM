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

    private static readonly System.Reflection.BindingFlags PrivateInstance =
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    public override async void _Ready()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        _gameManager = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (_gameManager == null)
        {
            LogUtils.PrintErr("[AUTOTEST] GameManager not found");
            return;
        }

        _player = GetTree().GetFirstNodeInGroup("player") as Player
            ?? GetNodeOrNull<Player>("/root/Main/Player")
            ?? GetTree().CurrentScene?.GetNodeOrNull<Player>("Player");
        if (_player == null)
        {
            LogUtils.PrintErr("[AUTOTEST] Player not found");
            return;
        }

        _initialRound = _gameManager.CurrentRound;
        LogUtils.Print("[AUTOTEST] Runner attached");
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

            // 持续探索模式：游戏经 _StartExploring 直接进入 Combat（不再有选卡环节）
            if (!_started && _gameManager.CurrentState == GameState.Combat)
            {
                _started = true;
                LogUtils.Print($"[AUTOTEST] exploring mode entered combat (round={_gameManager.CurrentRound})");
            }

            if (!_combatSeen && _gameManager.CurrentState == GameState.Combat)
            {
                _combatSeen = true;
                LogUtils.Print("[AUTOTEST] entered combat");
            }

            if (_combatSeen && !_attackRegressionChecksPassed && _gameManager.CurrentState == GameState.Combat)
            {
                try
                {
                    await _RunAttackRegressionChecks();
                    _attackRegressionChecksPassed = true;
                }
                catch (Exception e)
                {
                    LogUtils.PrintErr($"[AUTOTEST] attack regression threw: {e.Message}\n{e.StackTrace}");
                    GetTree().Quit(3);
                    return;
                }
            }

            if (_combatSeen && _gameManager.CurrentState == GameState.Combat)
            {
                foreach (var zombie in ZombieRegistry.All)
                {
                    if (IsInstanceValid(zombie))
                        zombie.TakeDamage(999999);
                }
            }

            // 持续探索模式下"回合"由时间推进（45s/阶），20s 内未必推进；
            // 核心回归（sprite + 攻击）全通过即视为成功退出。
            if (!_roundCompleted && _combatSeen && _attackRegressionChecksPassed && _spriteRegressionChecksPassed)
            {
                _roundCompleted = true;
                LogUtils.Print($"[AUTOTEST] all regression checks passed (round={_gameManager.CurrentRound})");
                GetTree().Quit(0);
                return;
            }
        }

        LogUtils.PrintErr("[AUTOTEST] timeout before reaching next round");
        GetTree().Quit(2);
    }

    private void _RunSpriteRegressionChecks()
    {
        if (_player?.Vfx == null)
            throw new Exception("[AUTOTEST] Player VFX missing");

        LogUtils.Print("[AUTOTEST] running sprite regression checks");

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

        // 帧数下限断言：运行时按 Count 取模循环播放，任意帧数均可工作；
        // 断言只守住"够播动画"的下限，兼容不同美术版本的构图（涂鸦版 5 帧 / 2.5D 版 8 帧）。
        if (warriorWalk == null || warriorWalk.Count < 5)
            throw new Exception($"[AUTOTEST] warrior walk should load >= 5 frames, got {warriorWalk?.Count ?? -1}");

        if (warriorAttack == null || warriorAttack.Count < 4)
            throw new Exception($"[AUTOTEST] warrior attack should load >= 4 frames, got {warriorAttack?.Count ?? -1}");

        if (warriorIdle == null)
            throw new Exception("[AUTOTEST] warrior idle sprite should be loaded");

        if (rangerWalk == null || rangerWalk.Count < 5)
            throw new Exception($"[AUTOTEST] ranger walk should load >= 5 frames, got {rangerWalk?.Count ?? -1}");

        if (mageWalk == null || mageWalk.Count < 5)
            throw new Exception($"[AUTOTEST] mage walk should load >= 5 frames, got {mageWalk?.Count ?? -1}");

        LogUtils.Print("[AUTOTEST] sprite regression checks passed");
    }

    private async Task _RunAttackRegressionChecks()
    {
        if (_player?.Combat == null)
            throw new Exception("[AUTOTEST] Player combat missing");

        var combat = _player.Combat;
        LogUtils.Print("[AUTOTEST] running attack regression checks");

        combat.CanAttack = true;
        combat.PerformAttack();
        if (combat.CanAttack)
            throw new Exception("[AUTOTEST] attack should enter cooldown after PerformAttack");

        _AssertAttackAnimTriggeredWithoutTargets();

        combat.RecalculateStats();
        if (!combat.CanAttack || !combat.AttackTimer.IsStopped())
            throw new Exception("[AUTOTEST] RecalculateStats should resync attack state and stop stale cooldown timer");

        int originalHealth = _player.CurrentHealth;
        _player.HealthCostPerAttack = originalHealth + 1;  // 超过血量触发"生命不足"失败路径（不扣血/不致死），验证失败攻击不卡冷却
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

        LogUtils.Print("[AUTOTEST] attack regression checks passed");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void _AssertAttackAnimTriggeredWithoutTargets()
    {
        if (_player?.Vfx == null)
            throw new Exception("[AUTOTEST] Player VFX missing during attack assertion");

        var vfx = _player.Vfx;
        var animStateField = typeof(PlayerVfx).GetField("_animState", PrivateInstance);
        var stateTimerField = typeof(PlayerVfx).GetField("_stateTimer", PrivateInstance);
        var attackFramesField = typeof(PlayerVfx).GetField("_attackFrames", PrivateInstance);

        var animState = animStateField?.GetValue(vfx)?.ToString();
        var stateTimer = (float?)stateTimerField?.GetValue(vfx) ?? 0.0f;
        var attackFrames = attackFramesField?.GetValue(vfx) as Godot.Collections.Array<Texture2D>;

        if (attackFrames == null || attackFrames.Count == 0)
            throw new Exception("[AUTOTEST] attack frames should be available before attack assertion");

        if (animState != "Attack" || stateTimer <= 0.0f)
            throw new Exception($"[AUTOTEST] attack without targets should still trigger attack animation, got state={animState}, timer={stateTimer}");
    }
}
