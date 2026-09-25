// 讨伐难度（R10）：对标 Hades 热度 / Vampire Survivors Hyper /
// Brotato 危险等级的终局留存环——通关逐档解锁更高难度，妖物血伤
// 与金币结算同乘。量级对标 Brotato 危险等级（+25%/+60%）。

export type DifficultyId = 0 | 1 | 2;

export interface DifficultyDef {
  id: DifficultyId;
  name: string;
  /** 一句话效果（欢迎页选择器展示，与实际乘数一一对应）。 */
  desc: string;
  /** 妖物血伤乘数（createEnemy 全类型生效，速度不动——保证走位公平）。 */
  foeMult: number;
  /** 金币结算乘数（finalizeRun 与点金融乘）。 */
  coinMult: number;
  /** 解锁条件：需通关的难度 id（null = 开局即开放）。 */
  unlockBy: DifficultyId | null;
}

export const DIFFICULTIES: Record<DifficultyId, DifficultyDef> = {
  0: {
    id: 0, name: "平妖", desc: "妖物 ×1.0 · 金币 ×1.0",
    foeMult: 1, coinMult: 1, unlockBy: null,
  },
  1: {
    id: 1, name: "伏魔", desc: "妖物 ×1.25 · 金币 ×1.25",
    foeMult: 1.25, coinMult: 1.25, unlockBy: 0,
  },
  2: {
    id: 2, name: "镇狱", desc: "妖物 ×1.6 · 金币 ×1.6",
    foeMult: 1.6, coinMult: 1.6, unlockBy: 1,
  },
};

export const DEFAULT_DIFFICULTY: DifficultyId = 0;

/** 按 id 取难度定义；未知 id 直接抛错（协议层返回 ok:false，不静默回退）。 */
export function getDifficulty(id: number): DifficultyDef {
  const d = DIFFICULTIES[id as DifficultyId];
  if (!d) throw new Error("未知难度: " + id);
  return d;
}
