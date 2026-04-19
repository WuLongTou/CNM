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
    public enum ZombieType { Basic, Fast, Tank, Exploder, Invisible, Spitter, Healer, Summoner, Archer }
    public enum EliteAffix { None, Regenerating, Swift, Explosive }
    public enum GameState { WeaponSelection, Combat, RoundTransition, GetReady, GameOver }
    public enum BuffType { Poison, Burn, Frozen, Stunned, Charmed, Invincible, SpeedBoost, DamageBoost }
    public enum BuffSource { ZombieSpitter, ZombieHealer, PlayerItem, WeaponEffect }
    public enum AbilityType { GhostPhase, Teleport, Cloner, Bomber, MindController }
}
