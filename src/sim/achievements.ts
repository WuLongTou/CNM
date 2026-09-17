// 成就系统（简化复刻）：局内里程碑（首杀/存活/Boss）+ 跨局累计成就（存档 totalKills）。

export interface AchievementDef {
  id: string;
  name: string;
  desc: string;
  check: (s: { kills: number; survivalTime: number; bossKills: number; totalKills: number }) => boolean;
}

export const ACHIEVEMENTS: AchievementDef[] = [
  { id: "first_blood", name: "初试锋芒", desc: "击杀第一只妖物", check: (s) => s.kills >= 1 },
  { id: "slayer_50", name: "除妖行者", desc: "累计击杀 50", check: (s) => s.totalKills >= 50 },
  { id: "slayer_200", name: "妖物克星", desc: "累计击杀 200", check: (s) => s.totalKills >= 200 },
  { id: "survive_60", name: "站稳脚跟", desc: "单局存活 60 秒", check: (s) => s.survivalTime >= 60 },
  { id: "survive_180", name: "久经沙场", desc: "单局存活 180 秒", check: (s) => s.survivalTime >= 180 },
  { id: "survive_300", name: "守山之人", desc: "单局存活 300 秒", check: (s) => s.survivalTime >= 300 },
  { id: "boss_slayer", name: "斩妖大将", desc: "击杀一只妖王", check: (s) => s.bossKills >= 1 },
];

export class AchievementSystem {
  /** 已达成（本进程内，含跨局持久集合初始化）。 */
  unlocked = new Set<string>();
  onUnlock: Array<(a: AchievementDef) => void> = [];

  constructor(persisted: string[] = []) {
    for (const id of persisted) this.unlocked.add(id);
  }

  update(state: { kills: number; survivalTime: number; bossKills: number; totalKills: number }): void {
    for (const a of ACHIEVEMENTS) {
      if (!this.unlocked.has(a.id) && a.check(state)) {
        this.unlocked.add(a.id);
        for (const cb of this.onUnlock) cb(a);
      }
    }
  }
}
