namespace ZombieWars.Core;

using Godot;
using ZombieWars.UI;
using static ZombieWars.Core.GameEnums;

/// <summary>掉落物生成器 — 从 GameManager 提取的独立组件。</summary>
public partial class PickupSpawner : Node
{
    private UIManager? _uiManager;

    public void Setup(UIManager ui) => _uiManager = ui;

    /// <summary>尝试在僵尸死亡位置掉落物品。返回 true 表示已掉落。</summary>
    public bool TryDrop(ZombieWars.Enemy.Zombie zombie, int currentRound, int totalKills)
    {
        if (currentRound < 3) return false;

        float dropChance = 0.12f;
        if (zombie.IsElite || zombie.IsBoss) dropChance = 0.35f;
        else if (zombie.ZombieType == ZombieType.Tank || zombie.ZombieType == ZombieType.Mech) dropChance = 0.20f;
        else if (zombie.ZombieType == ZombieType.Swarm || zombie.ZombieType == ZombieType.Fast) dropChance = 0.08f;

        bool goldenDrop = GameConfig.MathSystemsEnabled
            && ZombieWars.Math.GoldenProgression.IsFibonacciDropPoint(totalKills + 1);

        if (GD.Randf() > dropChance && !goldenDrop) return false;

        if (goldenDrop)
        {
            _uiManager?.FlashGoldenDrop();
            GameServices.Audio.PlayUpgrade();
        }

        string pickupType = GD.Randf() < 0.5f ? "health_potion" : "skill_book";
        Spawn(zombie.GlobalPosition, pickupType);
        return true;
    }

    public void Spawn(Vector2 pos, string type)
    {
        PackedScene? scene = null;
        if (type == "health_potion" && ResourceLoader.Exists("res://scenes/Pickups/HealthPotion.tscn"))
            scene = GD.Load<PackedScene>("res://scenes/Pickups/HealthPotion.tscn");
        else if (type == "skill_book" && ResourceLoader.Exists("res://scenes/Pickups/SkillBook.tscn"))
            scene = GD.Load<PackedScene>("res://scenes/Pickups/SkillBook.tscn");

        if (scene == null)
        {
            var pickup = _CreatePickupNode(type, pos);
            if (pickup != null)
                GetTree().CurrentScene.AddChild(pickup);
        }
        else
        {
            var inst = scene.Instantiate();
            if (inst is Node2D n)
            {
                n.GlobalPosition = pos;
                GetTree().CurrentScene.AddChild(n);
            }
        }
    }

    private static Node? _CreatePickupNode(string type, Vector2 pos)
    {
        try
        {
            return type switch
            {
                "health_potion" => new ZombieWars.Pickups.HealthPotion { GlobalPosition = pos },
                "skill_book" => new ZombieWars.Pickups.SkillBook { GlobalPosition = pos },
                _ => null
            };
        }
        catch { return null; }
    }
}
