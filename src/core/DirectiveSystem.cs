namespace ZombieWars.Core;

using Godot;
using System;
using ZombieWars.UI;
using static ZombieWars.Core.GameEnums;

/// <summary>战术目标系统 — 从 GameManager 提取的独立组件，管理每回合的 Directive。</summary>
public partial class DirectiveSystem : Node
{
    private enum RoundDirective { Standard, RiftHarvest, EliteHunt, SwarmSurge }

    private const float RiftRadius = 190.0f;

    private RoundDirective _directive = RoundDirective.Standard;
    private Vector2 _riftPosition;
    private Node2D? _riftVisual;
    private int _progress;

    private UIManager? _uiManager;
    private Player.Player? _player;

    /// <summary>当前 Directive 类型（供 SpawnZombie 查询 EliteHunt/SwarmSurge 分支）</summary>
    public bool IsEliteHunt => _directive == RoundDirective.EliteHunt;
    public bool IsSwarmSurge => _directive == RoundDirective.SwarmSurge;

    public void Setup(UIManager ui, Player.Player player)
    {
        _uiManager = ui;
        _player = player;
    }

    /// <summary>回合开始时选派 Directive</summary>
    public void OnRoundStart(int round)
    {
        ClearVisual();
        if (round <= 1) { _directive = RoundDirective.Standard; return; }
        _directive = (round % 4) switch
        {
            1 => RoundDirective.RiftHarvest,
            2 => RoundDirective.EliteHunt,
            3 => RoundDirective.SwarmSurge,
            _ => RoundDirective.Standard
        };
    }

    /// <summary>战斗开始时激活 Directive 视觉与提示</summary>
    public void OnCombatActivated()
    {
        switch (_directive)
        {
            case RoundDirective.RiftHarvest:
                _riftPosition = _PickArenaPoint(0.22f);
                CreateRiftVisual();
                _uiManager?.ShowLevelUpNotification("战术目标: 在炼金裂隙附近击杀 5 只僵尸", new Color(1.0f, 0.62f, 0.24f));
                break;
            case RoundDirective.EliteHunt:
                _uiManager?.ShowLevelUpNotification("战术目标: 击杀精英悬赏目标", new Color(0.95f, 0.35f, 0.25f));
                break;
            case RoundDirective.SwarmSurge:
                _uiManager?.ShowLevelUpNotification("事件: 虫群潮汐，尸群会成组涌入", new Color(0.55f, 0.85f, 0.35f));
                break;
            default:
                _uiManager?.ShowLevelUpNotification("战术目标: 保持连击，快速清场", new Color(0.6f, 0.7f, 0.9f));
                break;
        }
    }

    /// <summary>击杀结算，返回获得的额外分数</summary>
    public int OnZombieKilled(ZombieWars.Enemy.Zombie zombie, int basePoints)
    {
        if (_player == null) return 0;
        int bonus = 0;

        switch (_directive)
        {
            case RoundDirective.RiftHarvest:
                if (_riftVisual != null && zombie.GlobalPosition.DistanceTo(_riftPosition) <= RiftRadius)
                {
                    _progress++;
                    bonus = Mathf.Max(8, basePoints / 2);
                    if (_progress >= 5)
                    {
                        _player.Heal(Mathf.Max(8, _player.MaxHealth / 12));
                        _uiManager?.UpdateHealthDisplay(_player.CurrentHealth, _player.MaxHealth);
                        _uiManager?.ShowLevelUpNotification("裂隙净化完成: 回复生命并获得额外分数", new Color(1.0f, 0.78f, 0.34f));
                        ClearVisual();
                        _directive = RoundDirective.Standard;
                    }
                }
                break;
            case RoundDirective.EliteHunt:
                if (zombie.IsElite || zombie.IsBoss)
                {
                    _progress++;
                    bonus = basePoints;
                    _uiManager?.ShowLevelUpNotification("精英悬赏完成: 分数翻倍", new Color(1.0f, 0.35f, 0.25f));
                    _directive = RoundDirective.Standard;
                }
                break;
        }

        if (bonus > 0)
        {
            _uiManager?.ShowKillEffect(zombie.GlobalPosition, bonus);
        }
        return bonus;
    }

    public void ApplyAtmosphere(World.Background background)
    {
        switch (_directive)
        {
            case RoundDirective.RiftHarvest: background.SetAtmosphereTint(1.05f, 0.82f, 1.12f, 0.28f, 1.0f); break;
            case RoundDirective.EliteHunt: background.SetAtmosphereTint(1.18f, 0.72f, 0.58f, 0.35f, 1.2f); break;
            case RoundDirective.SwarmSurge: background.SetAtmosphereTint(0.82f, 1.05f, 0.72f, 0.30f, 0.9f); break;
        }
    }

    public void OnPlayerDied() => ClearVisual();

    private Vector2 _PickArenaPoint(float marginRatio)
    {
        var ws = MapContext.CurrentWorldSize;
        float mx = ws.X * marginRatio;
        float my = ws.Y * marginRatio;
        return new Vector2((float)GD.RandRange(mx, ws.X - mx), (float)GD.RandRange(my, ws.Y - my));
    }

    private void CreateRiftVisual()
    {
        ClearVisual();
        var root = new Node2D { Name = "RoundRift", GlobalPosition = _riftPosition, ZIndex = -1 };
        GetTree().CurrentScene.AddChild(root);
        _riftVisual = root;

        var outer = new Line2D { Width = 2.0f, DefaultColor = new Color(1.0f, 0.62f, 0.24f, 0.55f), Closed = true };
        var inner = new Line2D { Width = 1.2f, DefaultColor = new Color(0.55f, 0.20f, 0.95f, 0.48f), Closed = true };
        for (int i = 0; i < 48; i++)
        {
            float angle = i * Mathf.Tau / 48.0f;
            outer.AddPoint(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * RiftRadius);
            inner.AddPoint(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (RiftRadius * 0.55f));
        }
        root.AddChild(outer);
        root.AddChild(inner);

        var pulse = root.CreateTween().SetLoops(-1);
        pulse.TweenProperty(root, "scale", new Vector2(1.06f, 1.06f), 0.7f).SetTrans(Tween.TransitionType.Sine);
        pulse.TweenProperty(root, "scale", Vector2.One, 0.7f).SetTrans(Tween.TransitionType.Sine);
    }

    private void ClearVisual()
    {
        if (_riftVisual != null && IsInstanceValid(_riftVisual))
            _riftVisual.QueueFree();
        _riftVisual = null;
    }
}
