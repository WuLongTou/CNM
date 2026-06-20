namespace ZombieWars.Weapons;

using System;
using System.Collections.Generic;
using Godot;
using ZombieWars.Core;
using ZombieWars.Player;
using static ZombieWars.Core.GameEnums;

/// <summary>
/// 特效目录 — 武器特效的「单一数据源」。
/// 每个特效的「应用逻辑」(Apply) 与「描述公式」(Summary/Detail) 绑定在同一个条目里，
/// 物理上相邻，确保数值永远同步：改一处即描述与实际同时更新。
/// 描述中的机制说明（溅射比例、连锁衰减、抗性等）直接引用 GameConfig 常量，避免再次硬编码。
/// </summary>
public static class EffectCatalog
{
    /// <summary>单个特效的定义：简述、详情、应用逻辑。</summary>
    public sealed class EffectDef
    {
        public readonly Func<int, string> Summary;
        public readonly Func<int, string> Detail;
        public readonly Action<Player, int> Apply;

        public EffectDef(Func<int, string> summary, Func<int, string> detail, Action<Player, int> apply)
        {
            Summary = summary;
            Detail = detail;
            Apply = apply;
        }
    }

    // ═══════════════════════════════════════════════
    // 近战特效
    // ═══════════════════════════════════════════════
    public static readonly Dictionary<MeleeEffect, EffectDef> Melee = new()
    {
        [MeleeEffect.Splash] = new EffectDef(
            lv => $"溅射 Lv.{lv} (范围+{lv * 20}%)",
            lv => $"攻击时伤害波及周围敌人（溅射伤害为全额的{Mathf.RoundToInt(GameConfig.SplashDamageMult * 100)}%）。当前等级: 范围半径 {30 + lv * 10} 像素",
            (p, lv) => p.SplashRadius = 30.0f + lv * 10.0f
        ),
        [MeleeEffect.MultiTarget] = new EffectDef(
            lv => $"多目标 Lv.{lv} (同时攻击{1 + lv}个)",
            lv => $"一次攻击可命中多个敌人，近战清场神技。当前等级: 同时攻击 {1 + lv} 个目标",
            (p, lv) => p.MeleeTargetCount = Math.Max(p.MeleeTargetCount, 1 + lv)
        ),
        [MeleeEffect.KillExplosion] = new EffectDef(
            lv => $"击杀AOE Lv.{lv} (周围+{lv * 50}伤害)",
            lv => $"击杀僵尸时发生爆炸（爆炸半径{Mathf.RoundToInt(GameConfig.KillExplosionRadius)}）。当前等级: 对周围造成 {lv * 50} 伤害",
            (p, lv) => p.KillExplosionDamage += lv * 50
        ),
        [MeleeEffect.KillShield] = new EffectDef(
            lv => $"击杀护盾 Lv.{lv} (获得+{lv * 20}护盾)",
            lv => $"击杀僵尸时获得临时护盾（上限{GameConfig.MaxShield}）。当前等级: 获得 {lv * 20} 护盾",
            (p, lv) => p.KillShieldAmount += lv * 20
        ),
        [MeleeEffect.Knockback] = new EffectDef(
            lv => $"击退 Lv.{lv} (推开{lv * 50}像素)",
            lv => $"攻击时将敌人击退，使其暂时无法靠近。当前等级: 击退 {lv * 50} 像素",
            (p, lv) => p.KnockbackForce += lv * 50
        ),
    };

    // ═══════════════════════════════════════════════
    // 远程特效
    // ═══════════════════════════════════════════════
    public static readonly Dictionary<RangedEffect, EffectDef> Ranged = new()
    {
        [RangedEffect.MultiShot] = new EffectDef(
            lv => $"多发 Lv.{lv} (发射{1 + lv}发)",
            lv => $"同时发射多发子弹，大幅提高DPS。当前等级: 发射 {1 + lv} 发",
            (p, lv) => p.MultiShotCount = Math.Max(p.MultiShotCount, 1 + lv)
        ),
        [RangedEffect.TrajectoryDamage] = new EffectDef(
            lv => $"路径伤害 Lv.{lv} (途经{lv * 30}%伤害)",
            lv => $"子弹飞行轨迹上的敌人也会受到伤害。当前等级: 途经伤害为子弹的 {lv * 30}%",
            (p, lv) => p.TrajectoryDamageRatio = Math.Max(p.TrajectoryDamageRatio, lv * 0.3f)
        ),
        [RangedEffect.Returning] = new EffectDef(
            lv => $"反弹 Lv.{lv} (反弹{lv}次)",
            lv => $"子弹命中敌人或到达边界后反弹继续飞行。当前等级: 反弹 {lv} 次",
            (p, lv) =>
            {
                p.ReturningProjectiles = true;
                p.BounceCount = Math.Max(p.BounceCount, lv);
            }
        ),
        [RangedEffect.Piercing] = new EffectDef(
            lv => $"穿透 Lv.{lv} (穿透{lv}个敌人)",
            lv => $"子弹可穿透敌人继续飞行，攻击后续目标。当前等级: 穿透 {lv} 个敌人",
            (p, lv) => p.PiercingCount += lv
        ),
    };

    // ═══════════════════════════════════════════════
    // 魔法特效
    // ═══════════════════════════════════════════════
    public static readonly Dictionary<MagicEffect, EffectDef> Magic = new()
    {
        [MagicEffect.Charm] = new EffectDef(
            lv => $"魅惑 Lv.{lv} (持续{lv * 5}秒)",
            lv => $"使敌人陷入混乱，攻击其他僵尸。当前等级: 持续 {lv * 5} 秒（坦克-{Mathf.RoundToInt(GameConfig.CharmResistTank * 100)}%、快速-{Mathf.RoundToInt(GameConfig.CharmResistFast * 100)}% 抗性）",
            (p, lv) => p.CharmDuration = Math.Max(p.CharmDuration, lv * 5.0f)
        ),
        [MagicEffect.Push] = new EffectDef(
            lv => $"击退 Lv.{lv} (推开{lv * 80}像素)",
            lv => $"强大的冲击波将敌人推开较远距离。当前等级: 推开 {lv * 80} 像素",
            (p, lv) => p.PushForce += lv * 80
        ),
        [MagicEffect.Stun] = new EffectDef(
            lv => $"定身 Lv.{lv} (持续{lv * 1.5}秒)",
            lv => $"使敌人暂时无法移动和攻击。当前等级: 持续 {lv * 1.5} 秒",
            (p, lv) => p.StunDuration = Math.Max(p.StunDuration, lv * 1.5f)
        ),
        [MagicEffect.Chain] = new EffectDef(
            lv => $"连锁 Lv.{lv} (命中{lv + 1}个目标)",
            lv => $"伤害在敌人之间连锁跳跃（每次连锁伤害×{GameConfig.ChainDamageDecay}）。当前等级: 命中 {lv + 1} 个目标",
            (p, lv) => p.ChainCount = Math.Max(p.ChainCount, lv)
        ),
        [MagicEffect.Dot] = new EffectDef(
            lv => $"持续伤害 Lv.{lv} (每秒{lv * 5}伤害)",
            lv => $"使敌人持续受到火焰/毒液伤害。当前等级: 每秒 {lv * 5} 伤害",
            (p, lv) => p.DotDamage += lv * 5
        ),
    };

    // ═══════════════════════════════════════════════
    // 便捷查询（供 WeaponCard / PlayerCombat 委托调用）
    // ═══════════════════════════════════════════════
    public static string GetSummary(MeleeEffect e, int lv) => Melee[e].Summary(lv);
    public static string GetSummary(RangedEffect e, int lv) => Ranged[e].Summary(lv);
    public static string GetSummary(MagicEffect e, int lv) => Magic[e].Summary(lv);

    public static string GetDetail(MeleeEffect e, int lv) => Melee[e].Detail(lv);
    public static string GetDetail(RangedEffect e, int lv) => Ranged[e].Detail(lv);
    public static string GetDetail(MagicEffect e, int lv) => Magic[e].Detail(lv);

    public static void Apply(MeleeEffect e, int lv, Player p) => Melee[e].Apply(p, lv);
    public static void Apply(RangedEffect e, int lv, Player p) => Ranged[e].Apply(p, lv);
    public static void Apply(MagicEffect e, int lv, Player p) => Magic[e].Apply(p, lv);
}
