namespace ZombieWars.Core;

public static class GameEnums
{
    public enum WeaponType { Melee, Ranged, Magic, Mine }
    public enum WeaponCategory { Weapon, Equipment }
    public enum TargetPriority { Nearest, Farthest, MostEnemies }
    public enum MeleeEffect { None, Splash, MultiTarget, KillExplosion, KillShield, Knockback }
    public enum RangedEffect { None, MultiShot, TrajectoryDamage, Returning, Piercing }
    public enum MagicEffect { None, Charm, Push, Stun, Chain, Dot }
    public enum MineEffect { None, BiggerBlast, Cluster, StunBlast }
    public enum AttackType { Melee, Ranged, Area, Magic, Mine }
    public enum ZombieType { Basic, Fast, Tank, Exploder, Invisible, Spitter, Healer, Summoner, Archer, Ship, Mech, Knight, Dragon, Swarm }
    public enum EliteAffix { None, Regenerating, Swift, Explosive }
    public enum GameState { WeaponSelection, Combat, RoundTransition, GetReady, GameOver }
    public enum BuffType { Poison, Burn, Frozen, Stunned, Charmed, Invincible, SpeedBoost, DamageBoost }
    public enum BuffSource { ZombieSpitter, ZombieHealer, PlayerItem, WeaponEffect }
    public enum AbilityType { GhostPhase, Teleport, Cloner, Bomber, MindController }

    // ══ Achievement / Milestone System ══
    public enum AchievementType
    {
        FirstBlood,           // 击杀第一只僵尸
        Round5Survived,       // 存活5回合
        Round10Survived,      // 存活10回合
        Round20Survived,      // 存活20回合
        FirstEliteKill,       // 击杀第一只精英
        FirstBossKill,        // 击杀第一只BOSS
        Combo3,                // 完成3连击
        Combo5,                // 完成5连击
        Combo10,               // 完成10连击
        FullHealthSurvive,     // 满血存活一回合
        NoDamageSurvive,       // 无伤通过
        MaxWeaponsCollected,   // 收集满所有武器
        AllEliteKills,         // 击杀所有类型精英
    }

    public enum MilestoneType
    {
        Kill10, Kill50, Kill100, Kill500,      // 击杀里程碑
        Damage1000, Damage10000, Damage50000,  // 伤害里程碑
        Survive5Min, Survive10Min, Survive20Min, // 生存时间里程碑
    }
}
