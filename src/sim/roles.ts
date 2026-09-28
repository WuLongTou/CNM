// 开局职业（R3）：对标 Brotato / 20 Minutes Till Dawn / Vampire Survivors 的
// 「角色选择决定起手 build」核心循环——每职业独立起手武器 + 1~2 条简明被动。
// 被动量级 ≈ 1 级符箓词条（dmg +0.15 / asp +0.1 / rng +0.12），不碾压局内成长线。
// 玩家外观无需新增资产：M13 起外观随武器类型切换，职业决定起手武器即决定外观。

export type RoleId = "sword" | "archer" | "mage" | "hermit" | "wusheng";

export interface RoleDef {
  id: RoleId;
  name: string;
  /** 流派一句话（欢迎页卡片副标题）。 */
  desc: string;
  /** 被动效果一句话（欢迎页卡片展示，与实际数值一一对应）。 */
  passive: string;
  /** 起手武器（WEAPON_DATA id）。 */
  startWeapon: string;
  /** 全局伤害乘数（rollDamage 全武器生效）。 */
  dmgMult: number;
  /** 全局攻速乘数（fireWeapon 冷却生效）。 */
  aspdMult: number;
  /** 全局出手范围乘数（近战扇形/远程索敌/弹道寿命生效）。 */
  rangeMult: number;
  /** 冲刺冷却乘数（<1 = 转好更快）。 */
  dashCdMult: number;
  /** 符箓/掉落拾取半径乘数。 */
  pickMult: number;
  /** 解锁条件：成就 id（R6；null = 开局即开放）。单事实源——改成就即改解锁。 */
  unlockAchievement: string | null;
}

export const ROLES: Record<RoleId, RoleDef> = {
  sword: {
    id: "sword", name: "剑修", desc: "近战除妖师",
    passive: "伤害 +15%", startWeapon: "tiejian",
    dmgMult: 1.15, aspdMult: 1, rangeMult: 1, dashCdMult: 1, pickMult: 1,
    unlockAchievement: null,
  },
  archer: {
    id: "archer", name: "弓手", desc: "远程风筝流",
    passive: "攻速 +15% · 冲刺冷却 -20%", startWeapon: "gongjian",
    dmgMult: 1, aspdMult: 1.15, rangeMult: 1, dashCdMult: 0.8, pickMult: 1,
    unlockAchievement: "slayer_50",
  },
  mage: {
    id: "mage", name: "术士", desc: "妖术控场",
    passive: "范围 +20% · 拾取半径 +50%", startWeapon: "fazhang",
    dmgMult: 1, aspdMult: 1, rangeMult: 1.2, dashCdMult: 1, pickMult: 1.5,
    unlockAchievement: "boss_slayer",
  },
  // R123 秘密职业：无伤大师的孤注一掷（铁壁解锁；高风险高回报玻璃大炮）
  hermit: {
    id: "hermit", name: "隐士", desc: "无伤大师的孤注一掷",
    passive: "伤害 +30% · 拾取半径 +30% · 攻速 -10% · 起手引雷形态", startWeapon: "leidianfazhang",
    dmgMult: 1.3, aspdMult: 0.9, rangeMult: 1, dashCdMult: 1, pickMult: 1.3,
    unlockAchievement: "flawless",
  },
  // R124 第二秘密职业：全职业制霸的宗师（九界平定解锁；全维 +10% 无短板）
  wusheng: {
    id: "wusheng", name: "武圣", desc: "制霸九界的宗师",
    passive: "全维 +10%：伤害/攻速/范围/拾取", startWeapon: "sishenliandao",
    dmgMult: 1.1, aspdMult: 1.1, rangeMult: 1.1, dashCdMult: 1, pickMult: 1.1,
    unlockAchievement: "nine_realms",
  },
};

export const DEFAULT_ROLE: RoleId = "sword";

/** 按 id 取职业定义；未知 id 直接抛错（协议层返回 ok:false，不静默回退）。 */
export function getRole(id: string): RoleDef {
  const r = ROLES[id as RoleId];
  if (!r) throw new Error("未知职业: " + id);
  return r;
}
