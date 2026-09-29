// R198 武器精通称号（纯外观，无数值加成）：击杀数 → 五档称号。
// 纯函数可单测；装备卡与武器图鉴消费。

export interface MasteryTier {
  level: number;
  title: string;
}

/** 阈值表：击杀数下限 → 档位（闭区间下限，升序）。 */
const THRESHOLDS: Array<{ min: number; level: number; title: string }> = [
  { min: 0, level: 1, title: "初识" },
  { min: 50, level: 2, title: "入门" },
  { min: 200, level: 3, title: "熟练" },
  { min: 500, level: 4, title: "精通" },
  { min: 1000, level: 5, title: "宗师" },
];

/** 击杀数 → 精通档位（纯函数）。 */
export function masteryTier(kills: number): MasteryTier {
  let cur: MasteryTier = THRESHOLDS[0]!;
  for (const t of THRESHOLDS) {
    if (kills >= t.min) cur = { level: t.level, title: t.title };
  }
  return cur;
}
