// 成就系统（简化复刻）：局内里程碑（首杀/存活/Boss）+ 跨局累计成就（存档 totalKills）
// + R12 系统成就（重抽/放逐/精英/连击/集齐/通关三档，还 R4–R11 的内容债）。

export interface AchievementState {
  kills: number;
  survivalTime: number;
  bossKills: number;
  totalKills: number;
  /** R12：精英讨伐 / 最高连击 / 持有武器数 / 已用重抽 / 已用放逐。 */
  eliteKills: number;
  maxCombo: number;
  weaponCount: number;
  rerollsUsed: number;
  banishesUsed: number;
  /** R14：宝妖讨伐数。 */
  goblinKills: number;
  /** R66 散财消灾：单局香火龛购买次数。 */
  shopPurchases: number;
  /** R74 历劫全域：每日变异通关次数表（id → 次数）。 */
  mutatorClears?: Record<string, number>;
  /** R99 每日挑战连胜天数。 */
  dailyStreakCount: number;
  /** R27：图鉴金边种类数（单类跨局累计满百）。 */
  codexGold: number;
  /** R30：藏珍拥有数 / 武器总数。 */
  armoryCount: number;
  armoryTotal: number;
  /** R32：已炼成配方种数 / 配方总数。 */
  fusedCount: number;
  fusedTotal: number;
  /** R38：本局达终极的不同武器数。 */
  ultimateRunCount: number;
  /** R47：Boss 连战中讨伐的妖王数（普通模式不计）。 */
  bossRushKills: number;
}

/** 成就分组（R51）：帮助页按线归类渲染，避免 28 条平铺成信息墙。 */
export type AchievementGroup = "战功" | "技艺" | "收藏" | "征伐";

export const ACHIEVEMENT_GROUPS: AchievementGroup[] = ["战功", "技艺", "收藏", "征伐"];

/** AchievementState 中数值型字段的键（R81 进度显示用；新增数值字段时同步维护）。 */
export type NumberStateKey =
  | "kills"
  | "survivalTime"
  | "bossKills"
  | "totalKills"
  | "eliteKills"
  | "maxCombo"
  | "weaponCount"
  | "rerollsUsed"
  | "banishesUsed"
  | "goblinKills"
  | "shopPurchases"
  | "dailyStreakCount"
  | "codexGold"
  | "armoryCount"
  | "fusedCount"
  | "ultimateRunCount"
  | "bossRushKills";

export interface AchievementDef {
  id: string;
  name: string;
  desc: string;
  /** 帮助页分组（同组连续排列）。 */
  group: AchievementGroup;
  /** R81 阈值型目标（声明式；帮助页可显示 cur/target 进度）。 */
  goal?: { stat: NumberStateKey; target: number };
  /** 复合型判定（通关直授/守卫型等）；goal 与 check 二选一。 */
  check?: (s: AchievementState) => boolean;
  /** R82 复合型进度（cur/target；数据齐全的复合成就提供，帮助页展示）。 */
  progress?: (s: AchievementState) => { cur: number; target: number };
}

export const ACHIEVEMENTS: AchievementDef[] = [
  { id: "first_blood", name: "初试锋芒", desc: "击杀第一只妖物", group: "战功", goal: { stat: "kills", target: 1 } },
  { id: "slayer_50", name: "除妖行者", desc: "累计击杀 50", group: "战功", goal: { stat: "totalKills", target: 50 } },
  { id: "slayer_200", name: "妖物克星", desc: "累计击杀 200", group: "战功", goal: { stat: "totalKills", target: 200 } },
  { id: "survive_60", name: "站稳脚跟", desc: "单局存活 60 秒", group: "战功", goal: { stat: "survivalTime", target: 60 } },
  { id: "survive_180", name: "久经沙场", desc: "单局存活 180 秒", group: "战功", goal: { stat: "survivalTime", target: 180 } },
  { id: "survive_300", name: "守山之人", desc: "单局存活 300 秒", group: "战功", goal: { stat: "survivalTime", target: 300 } },
  { id: "boss_slayer", name: "斩妖大将", desc: "击杀一只妖王", group: "战功", goal: { stat: "bossKills", target: 1 } },
  // ── R12 系统成就 ──
  { id: "fate_twist", name: "变数", desc: "重抽一次升级报价", group: "技艺", goal: { stat: "rerollsUsed", target: 1 } },
  { id: "sever", name: "断舍离", desc: "放逐一个升级组合", group: "技艺", goal: { stat: "banishesUsed", target: 1 } },
  { id: "hunter_10", name: "猎手", desc: "讨伐 10 只精英妖物", group: "技艺", goal: { stat: "eliteKills", target: 10 } },
  { id: "hunter_30", name: "猎魔人", desc: "讨伐 30 只精英妖物", group: "技艺", goal: { stat: "eliteKills", target: 30 } },
  { id: "combo_20", name: "连绵不绝", desc: "单局最高连击 20", group: "技艺", goal: { stat: "maxCombo", target: 20 } },
  { id: "combo_50", name: "万军取首", desc: "单局最高连击 50", group: "技艺", goal: { stat: "maxCombo", target: 50 } },
  { id: "combo_100", name: "百连斩", desc: "单局最高连击 100", group: "技艺", goal: { stat: "maxCombo", target: 100 } },
  // R25 三重血誓由 onVictory 直接 grant
  { id: "oathbound", name: "三重血誓", desc: "立下全部三重血誓并通关", group: "征伐", check: () => false },
  { id: "heat_max", name: "炼狱修罗", desc: "三誓满档（各三级）通关一次", group: "征伐", check: () => false },
  { id: "mutator_all", name: "历劫全域", desc: "五种每日变异各通关一次", group: "征伐", check: (s) => Object.keys(s.mutatorClears ?? {}).length >= 5, progress: (s) => ({ cur: Object.keys(s.mutatorClears ?? {}).length, target: 5 }) },
  { id: "daily_streak_7", name: "七日不辍", desc: "每日挑战连续 7 天", group: "征伐", goal: { stat: "dailyStreakCount", target: 7 } },
  { id: "arsenal", name: "六械齐备", desc: "单局集齐 6 件武器", group: "技艺", goal: { stat: "weaponCount", target: 6 } },
  { id: "goblin_slayer", name: "宝中取宝", desc: "讨伐一只宝妖", group: "技艺", goal: { stat: "goblinKills", target: 1 } },
  { id: "incense_5", name: "散财消灾", desc: "单局在香火龛消费 5 次", group: "技艺", goal: { stat: "shopPurchases", target: 5 } },
  // 通关三档由 onVictory 直接 grant（终局后 tick 停摆，走 tick 判定够不着）
  { id: "pacify", name: "妖潮平定", desc: "讨伐终幕妖王通关一次", group: "征伐", check: () => false },
  { id: "demon_queller", name: "伏魔真君", desc: "以伏魔难度通关", group: "征伐", check: () => false },
  { id: "warden", name: "镇狱天师", desc: "以镇狱难度通关", group: "征伐", check: () => false },
  // R21 制霸双璧同样由 onVictory 直接 grant
  { id: "three_heroes", name: "三英聚义", desc: "三个职业各通关至少一次", group: "征伐", check: () => false },
  { id: "nine_realms", name: "九界平定", desc: "制霸全部 9 格（3 职业×3 难度）", group: "征伐", check: () => false },
  // R27 博物学家走 tick 判定（14 种妖物图鉴全金边）
  { id: "naturalist", name: "博物学家", desc: "图鉴十四妖物皆满百杀镀金", group: "收藏", goal: { stat: "codexGold", target: 14 } },
  // R29 千人斩走 tick 判定（单局击杀数）
  { id: "slayer_1000", name: "千人斩", desc: "单局击杀 1000", group: "征伐", goal: { stat: "kills", target: 1000 } },
  // R30 藏珍满架走 tick 判定（拥有数追平总数）
  { id: "curator", name: "藏珍满架", desc: "集齐全部武器", group: "收藏", check: (s) => s.armoryTotal > 0 && s.armoryCount >= s.armoryTotal, progress: (s) => ({ cur: s.armoryCount, target: s.armoryTotal }) },
  // R32 炼金大师走 tick 判定（六配方全炼成）
  { id: "fusion_master", name: "炼金大师", desc: "集齐全部祭坛融合配方", group: "收藏", check: (s) => s.fusedTotal > 0 && s.fusedCount >= s.fusedTotal, progress: (s) => ({ cur: s.fusedCount, target: s.fusedTotal }) },
  // R38 终极收藏家走 tick 判定（5 把不同武器达终极）
  { id: "ultimate_hoarder", name: "终极收藏家", desc: "单局 5 把不同武器皆达终极", group: "技艺", goal: { stat: "ultimateRunCount", target: 5 } },
  // R47 妖王猎手走 tick 判定（Boss 连战 5 妖王专属）
  { id: "boss_hunter", name: "妖王猎手", desc: "Boss 连战中讨伐 5 只妖王", group: "征伐", goal: { stat: "bossRushKills", target: 5 } },
];

export class AchievementSystem {
  /** 已达成（本进程内，含跨局持久集合初始化）。 */
  unlocked = new Set<string>();
  onUnlock: Array<(a: AchievementDef) => void> = [];

  constructor(persisted: string[] = []) {
    for (const id of persisted) this.unlocked.add(id);
  }

  /** 直接授予（通关类：tick 停摆够不着判定，只能走这里；重复授予返回 false）。 */
  grant(id: string): boolean {
    const a = ACHIEVEMENTS.find((x) => x.id === id);
    if (!a || this.unlocked.has(id)) return false;
    this.unlocked.add(id);
    for (const cb of this.onUnlock) cb(a);
    return true;
  }

  update(state: AchievementState): void {
    for (const a of ACHIEVEMENTS) {
      if (this.unlocked.has(a.id)) continue;
      const g = a.goal;
      const pass = a.check
        ? a.check(state)
        : g
          ? (state[g.stat] as number) >= g.target
          : false;
      if (pass) this.grant(a.id);
    }
  }
}

export interface AchievementProgress { cur: number; target: number }

/** 成就进度（R81/R82）：复合型走 progress，阈值型走 goal，其余 null（帮助页据此显示）。 */
export function achievementProgress(a: AchievementDef, s: AchievementState): AchievementProgress | null {
  if (a.progress) return a.progress(s);
  if (!a.goal) return null;
  return { cur: s[a.goal.stat] as number, target: a.goal.target };
}

