// 武器数据表与装备/进阶逻辑。字段语义对齐 C# 版 WeaponFactory._WEAPON_DATA /
// EffectCatalog（dmg 伤害乘数、asp 攻速乘数、rng 范围乘数、hp 每击血代价、
// psn 中毒概率%、crit 暴击率加成%、multi 近战/弹道目标数、splash 溅射半径、
// chain 链式弹射次数、freeze 冰缓概率%、homing 追踪）。
// 26 件代表性武器覆盖全部机制，表驱动可按同结构扩至 C# 版全量 63 件。

export type AttackType = "Melee" | "Ranged" | "Magic" | "Mine";

export interface WeaponDef {
  name: string;
  desc: string;
  type: AttackType;
  tier: 1 | 2 | 3 | 4;
  rarity: 1 | 2 | 3 | 4;
  /** 前置武器名（抽卡加权用）。 */
  prereq?: string;
  dmg?: number;
  asp?: number;
  rng?: number;
  hp?: number;
  psn?: number;
  crit?: number;
  multi?: number;
  splash?: number;
  chain?: number;
  freeze?: number;
  homing?: boolean;
  /** Ranged 散射弹数。 */
  spread?: number;
  /** 弹道穿透数。 */
  pierce?: number;
  /** 每击吸血量（近战命中回复生命，R77；不超上限）。 */
  steal?: number;
}

export const WEAPON_DATA: Record<string, WeaponDef> = {
  // ═══ 近战 ═══
  tiejian: { name: "铁剑", desc: "平衡的近战武器，适合新手", type: "Melee", tier: 1, rarity: 1 },
  zhanfu: { name: "战斧", desc: "高伤害低攻速，溅射", type: "Melee", tier: 2, rarity: 2, prereq: "铁剑", dmg: 1.5, asp: 0.7, splash: 60 },
  shuangdao: { name: "双刀", desc: "高攻速多目标", type: "Melee", tier: 2, rarity: 2, prereq: "铁剑", dmg: 0.7, asp: 1.5, multi: 2 },
  duandao: { name: "淬毒短刀", desc: "近战带毒持续伤害", type: "Melee", tier: 2, rarity: 2, prereq: "双刀", dmg: 0.8, asp: 1.4, psn: 10, multi: 2 },
  shuangya: { name: "霜牙", desc: "近战冰缓妖物", type: "Melee", tier: 2, rarity: 2, prereq: "淬毒短刀", dmg: 0.9, asp: 1.2, freeze: 15 },
  jinfu: { name: "巨斧", desc: "极高的单发伤害", type: "Melee", tier: 3, rarity: 3, prereq: "战斧", dmg: 2.2, asp: 0.5, splash: 80 },
  changqiang: { name: "长枪", desc: "长距离突刺多目标", type: "Melee", tier: 3, rarity: 3, prereq: "巨斧", dmg: 1.4, asp: 0.9, rng: 1.5, multi: 2 },
  sishenliandao: { name: "死神镰刀", desc: "收割成片妖物", type: "Melee", tier: 4, rarity: 4, prereq: "长枪", dmg: 2.0, asp: 0.8, rng: 1.6, multi: 3, crit: 15 },
  yingren: { name: "影刃", desc: "极速且易暴击", type: "Melee", tier: 3, rarity: 3, prereq: "淬毒短刀", dmg: 1.0, asp: 1.6, crit: 20 },
  xueyubi: { name: "血玉匕", desc: "极速短刃，命中吸血", type: "Melee", tier: 3, rarity: 3, prereq: "影刃", dmg: 0.9, asp: 1.4, rng: 0.85, steal: 10 },
  // ═══ 远程（直线弹道） ═══
  gongjian: { name: "弓箭", desc: "可靠的远程武器", type: "Ranged", tier: 1, rarity: 1 },
  zhongnu: { name: "重弩", desc: "沉重的单发重击", type: "Ranged", tier: 2, rarity: 2, prereq: "弓箭", dmg: 1.6, asp: 0.6 },
  dujian: { name: "毒箭", desc: "远程带毒持续伤害", type: "Ranged", tier: 2, rarity: 2, prereq: "弓箭", dmg: 0.7, asp: 1.1, psn: 20 },
  sandanqiang: { name: "散弹枪", desc: "一次三发散射", type: "Ranged", tier: 2, rarity: 2, prereq: "弓箭", dmg: 0.6, asp: 1.2, spread: 3 },
  jijinu: { name: "狙击弩", desc: "超远射程与暴击", type: "Ranged", tier: 3, rarity: 3, prereq: "重弩", dmg: 2.5, asp: 0.5, rng: 1.4, crit: 25 },
  guijinu: { name: "轨迹弩", desc: "箭矢自动追踪妖物", type: "Ranged", tier: 3, rarity: 3, prereq: "重弩", dmg: 1.3, asp: 0.9, homing: true },
  huixuanbiao: { name: "回旋镖", desc: "穿透多个妖物", type: "Ranged", tier: 3, rarity: 3, prereq: "散弹枪", dmg: 1.0, asp: 1.0, pierce: 3 },
  // ═══ 魔法（追踪弹） ═══
  fazhang: { name: "法杖", desc: "基础的妖术弹", type: "Magic", tier: 1, rarity: 1 },
  huoqiufazhang: { name: "火球法杖", desc: "爆炸波及周围", type: "Magic", tier: 2, rarity: 2, prereq: "法杖", dmg: 1.4, splash: 70 },
  meihuofazhang: { name: "魅火法杖", desc: "妖火灼烧持续伤害", type: "Magic", tier: 2, rarity: 2, prereq: "法杖", dmg: 0.8, asp: 1.2, psn: 15 },
  bingshuangfazhang: { name: "冰霜法杖", desc: "冻结减速妖物", type: "Magic", tier: 3, rarity: 3, prereq: "火球法杖", dmg: 1.1, freeze: 20 },
  leidianfazhang: { name: "雷电法杖", desc: "闪电链式弹射", type: "Magic", tier: 3, rarity: 3, prereq: "火球法杖", dmg: 1.5, chain: 2 },
  sishenfazhang: { name: "死神法杖", desc: "致命的连环妖雷", type: "Magic", tier: 4, rarity: 4, prereq: "雷电法杖", dmg: 2.0, chain: 3, crit: 20 },
  // ═══ 地雷 ═══
  tiejili: { name: "铁蒺藜", desc: "原地布雷，触发爆炸", type: "Mine", tier: 1, rarity: 1 },
  jishulei: { name: "集束地雷", desc: "爆炸后分裂小雷", type: "Mine", tier: 2, rarity: 2, prereq: "铁蒺藜", dmg: 1.3, splash: 90 },
  diyu_huolei: { name: "地狱火雷", desc: "大范围燃烧地雷", type: "Mine", tier: 3, rarity: 4, prereq: "集束地雷", dmg: 1.8, splash: 130, psn: 30 },
};

/** 进阶阈值：同武器累计升级 3 次进阶（ASCENDED），6 次终极（ULTIMATE）。对齐升天 3 / 终极 6。 */
export const ASCEND_AT = 3;
export const ULTIMATE_AT = 6;
/** 进阶/终极的全属性补正。 */
export const ASCEND_MULT = 1.5;
export const ULTIMATE_MULT = 2.2;

/** 升级卡词条池：数值词条（dmg/asp/rng）+ 行为词条（R2 质变成长——
 *  multi 多重 / chain 连锁 / pierce 穿透 / psn 淬毒 / freeze 冰缓 / crit 会心 / splash 溅射）。 */
export const UPGRADE_POOL = [
  "dmg", "asp", "rng", "multi", "chain", "pierce", "psn", "freeze", "crit", "splash",
] as const;
export type UpgradeStat = (typeof UPGRADE_POOL)[number];
/** 各词条单级增量：乘数类为比例加法，机制类为计数/百分点。 */
export const UPGRADE_STEP: Record<UpgradeStat, number> = {
  dmg: 0.15, asp: 0.1, rng: 0.12,
  multi: 1, chain: 1, pierce: 1,
  psn: 15, freeze: 15, crit: 10, splash: 50,
};

/** 词条对武器类型是否有效（升级卡只抽有效词条，避免出废卡）。 */
export function statValidFor(type: AttackType, st: UpgradeStat): boolean {
  switch (st) {
    case "multi": return true;                    // 近战多目标 / 远程魔法多发
    case "chain": return type === "Ranged" || type === "Magic";
    case "pierce": return type === "Ranged";
    case "splash": return type === "Melee" || type === "Mine";
    case "psn": case "freeze": case "crit": return type !== "Mine";
    default: return true;                          // dmg/asp/rng 全类型有效
  }
}

/** 某武器类型的有效词条池。 */
export function validStats(type: AttackType): UpgradeStat[] {
  return UPGRADE_POOL.filter((st) => statValidFor(type, st));
}

/** 融合配方：两件武器名 → 新武器 id（祭坛触发，M3 接入实体）。 */
export const FUSION_RECIPES: Array<{ a: string; b: string; result: string }> = [
  { a: "铁剑", b: "法杖", result: "yingren" },
  { a: "战斧", b: "火球法杖", result: "jinfu" },
  { a: "弓箭", b: "雷电法杖", result: "guijinu" },
  { a: "双刀", b: "冰霜法杖", result: "yingren" },
  { a: "重弩", b: "淬毒短刀", result: "jijinu" },
  { a: "死神镰刀", b: "死神法杖", result: "sishenliandao" },
];

export interface WeaponInstance {
  defId: string;
  name: string;
  type: AttackType;
  /** 升级卡累计次数（达到阈值进阶/终极）。 */
  ups: number;
  /** 升级词条累计值（缺省视为 0；机制类词条为整数计数/百分点）。 */
  bonus: Partial<Record<UpgradeStat, number>>;
  /** 独立出手冷却（R2 齐射：每把武器各自计时，同时出手）。 */
  cd: number;
  /** R83 战斗统计：本件武器累计造成的伤害（makeWeapon 初始 0，重开随实例重建）。 */
  dmgDealt: number;
}

/** 构造一件武器实例（bonus 空、冷却就绪）。 */
export function makeWeapon(defId: string): WeaponInstance {
  const def = WEAPON_DATA[defId];
  return { defId, name: def.name, type: def.type, ups: 0, bonus: {}, cd: 0, dmgDealt: 0 };
}

/** 读词条累计值（缺省 0）。 */
export function bonusOf(w: WeaponInstance, st: UpgradeStat): number {
  return w.bonus[st] ?? 0;
}

/** 计算实例当前阶段倍率（终极覆盖进阶）。 */
export function stageMult(w: WeaponInstance): number {
  if (w.ups >= ULTIMATE_AT) return ULTIMATE_MULT;
  if (w.ups >= ASCEND_AT) return ASCEND_MULT;
  return 1;
}

/** 实例的阶段标签。 */
export function stageTag(w: WeaponInstance): "ultimate" | "ascended" | "base" {
  if (w.ups >= ULTIMATE_AT) return "ultimate";
  if (w.ups >= ASCEND_AT) return "ascended";
  return "base";
}
