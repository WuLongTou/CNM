namespace ZombieWars.Core;

using System.Threading.Tasks;
using Godot;
using ZombieWars.Weapons;
using static ZombieWars.Core.GameEnums;
using Player = ZombieWars.Player.Player;

/// <summary>
/// 进化系统端到端验证：给玩家一把测试武器并连续升级 6 次，
/// 验证升 3 次自动进阶（ASCENDED）、升 6 次自动究极化（ULTIMATE）。
/// 用法：Godot --headless -- --evotest
/// </summary>
public partial class EvoTestRunner : Node
{
    private GameManager? _gameManager;
    private Player? _player;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        _gameManager = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        _player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (_gameManager == null || _player == null || _player.Combat == null)
        {
            LogUtils.PrintErr("[EVO] 缺少 GameManager/Player/Combat");
            GetTree().Quit(2);
            return;
        }

        // 推进到 Combat
        for (int i = 0; i < 300; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_gameManager.CurrentState == GameState.WeaponSelection)
                _gameManager.OnUpgradeSkipped();
            else if (_gameManager.CurrentState == GameState.Combat)
                break;
        }

        // 给玩家一把测试武器
        var weapon = new WeaponCard();
        weapon.Init("测试剑", "进化验证用", WeaponType.Melee, new Godot.Collections.Array());
        weapon.DamageModifier = 1.0f;
        weapon.AttackSpeedModifier = 1.0f;
        _player.ActiveEffects.Add(weapon);

        // 连续升级 6 次（每次 TargetRef 指向当前 ActiveEffects[0]，模拟真实选卡）
        for (int i = 0; i < 6; i++)
        {
            var target = _player.ActiveEffects[0];
            var upgrade = new UpgradeCard { TargetRef = target, DamageDelta = 1.0f };
            _player.Combat.ApplyUpgradeCard(upgrade);
            LogUtils.Print($"[EVO] 升级{i + 1}: name={_player.ActiveEffects[0].CardName}, upgradeCount={_player.ActiveEffects[0].UpgradeCount}");
        }

        var final = _player.ActiveEffects[0];
        bool ascended = final.Tags.Contains("ASCENDED");
        bool ultimate = final.Tags.Contains("ULTIMATE");

        if (ultimate)
            LogUtils.Print($"[EVO] ✓ 究极化触发成功: {final.CardName} (伤害×{final.DamageModifier:F1}, 攻速×{final.AttackSpeedModifier:F1})");
        else if (ascended)
            LogUtils.PrintErr("[EVO] [!] 仅进阶未究极化（检查 Ultimate 阈值/防反复逻辑）");
        else
            LogUtils.PrintErr("[EVO] ✗ 进化完全未触发（接线失败）");

        GetTree().Quit(ultimate ? 0 : 1);
    }
}
