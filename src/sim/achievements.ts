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
}

export interface AchievementDef {
  id: string;
  name: string;
  desc: string;
  check: (s: AchievementState) => boolean;
}

export const ACHIEVEMENTS: AchievementDef[] = [
  { id: "first_blood", name: "初试锋芒", desc: "击杀第一只妖物", check: (s) => s.kills >= 1 },
  { id: "slayer_50", name: "除妖行者", desc: "累计击杀 50", check: (s) => s.totalKills >= 50 },
  { id: "slayer_200", name: "妖物克星", desc: "累计击杀 200", check: (s) => s.totalKills >= 200 },
  { id: "survive_60", name: "站稳脚跟", desc: "单局存活 60 秒", check: (s) => s.survivalTime >= 60 },
  { id: "survive_180", name: "久经沙场", desc: "单局存活 180 秒", check: (s) => s.survivalTime >= 180 },
  { id: "survive_300", name: "守山之人", desc: "单局存活 300 秒", check: (s) => s.survivalTime >= 300 },
  { id: "boss_slayer", name: "斩妖大将", desc: "击杀一只妖王", check: (s) => s.bossKills >= 1 },
  // ── R12 系统成就 ──
  { id: "fate_twist", name: "变数", desc: "重抽一次升级报价", check: (s) => s.rerollsUsed >= 1 },
  { id: "sever", name: "断舍离", desc: "放逐一个升级组合", check: (s) => s.banishesUsed >= 1 },
  { id: "hunter_10", name: "猎手", desc: "讨伐 10 只精英妖物", check: (s) => s.eliteKills >= 10 },
  { id: "hunter_30", name: "猎魔人", desc: "讨伐 30 只精英妖物", check: (s) => s.eliteKills >= 30 },
  { id: "combo_20", name: "连绵不绝", desc: "单局最高连击 20", check: (s) => s.maxCombo >= 20 },
  { id: "combo_50", name: "万军取首", desc: "单局最高连击 50", check: (s) => s.maxCombo >= 50 },
  { id: "arsenal", name: "六械齐备", desc: "单局集齐 6 件武器", check: (s) => s.weaponCount >= 6 },
  { id: "goblin_slayer", name: "宝中取宝", desc: "讨伐一只宝妖", check: (s) => s.goblinKills >= 1 },
  // 通关三档由 onVictory 直接 grant（终局后 tick 停摆，走 tick 判定够不着）
  { id: "pacify", name: "妖潮平定", desc: "讨伐终幕妖王通关一次", check: () => false },
  { id: "demon_queller", name: "伏魔真君", desc: "以伏魔难度通关", check: () => false },
  { id: "warden", name: "镇狱天师", desc: "以镇狱难度通关", check: () => false },
  // R21 制霸双璧同样由 onVictory 直接 grant
  { id: "three_heroes", name: "三英聚义", desc: "三个职业各通关至少一次", check: () => false },
  { id: "nine_realms", name: "九界平定", desc: "制霸全部 9 格（3 职业×3 难度）", check: () => false },
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
      if (!this.unlocked.has(a.id) && a.check(state)) {
        this.grant(a.id);
      }
    }
  }
}
