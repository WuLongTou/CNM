// 血誓契约（R25）：对标 Hades 惩罚契约的自选逆境——开局立誓加难，结算分数加成。
// R70 血誓分级（Hades 热量式）：每誓三档，档位越高逆境与结算加成越大；
// 一档数值与 R25 旧值完全一致（向后兼容：旧 set_pacts ids 调用 = 各誓一级）。
// 默认全关（档 0），可叠加，分数乘数连乘；重开沿用（同职业/难度，档位随携）。

export type PactId = "blood" | "horde" | "frail";

/** 单档效果数值（全部字段供齐，未用字段为中性值）。 */
export interface PactTier {
  /** 本级一句话效果（欢迎页血誓卡展示）。 */
  desc: string;
  /** 结算分数乘数（连乘；经分数自然流入结算金币）。 */
  scoreMult: number;
  /** 妖物生命乘数（血战；createEnemy 生效）。 */
  hpMult: number;
  /** 额外刷怪概率（人海；每次维持刷怪后按此概率多刷一只）。 */
  spawnBonus: number;
  /** 玩家生命上限乘数（脆体；建角/改誓时生效）。 */
  frailMult: number;
}

export interface PactDef {
  id: PactId;
  name: string;
  /** 各档效果（索引 0 = 一级；长度 = PACT_MAX_TIER）。 */
  tiers: PactTier[];
}

export const PACT_MAX_TIER = 3;

/** 档位罗马数字（分享文案/血誓卡徽标；索引 0 = 未立誓空串）。 */
export const PACT_ROMAN = ["", "I", "II", "III"];

export const PACTS: Record<PactId, PactDef> = {
  blood: {
    id: "blood", name: "血战", tiers: [
      { desc: "妖物生命 +25% · 结算 +25%", scoreMult: 1.25, hpMult: 1.25, spawnBonus: 0, frailMult: 1 },
      { desc: "妖物生命 +45% · 结算 +45%", scoreMult: 1.45, hpMult: 1.45, spawnBonus: 0, frailMult: 1 },
      { desc: "妖物生命 +70% · 结算 +70%", scoreMult: 1.7, hpMult: 1.7, spawnBonus: 0, frailMult: 1 },
    ],
  },
  horde: {
    id: "horde", name: "人海", tiers: [
      { desc: "妖潮密度 +30% · 结算 +30%", scoreMult: 1.3, hpMult: 1, spawnBonus: 0.3, frailMult: 1 },
      { desc: "妖潮密度 +50% · 结算 +50%", scoreMult: 1.5, hpMult: 1, spawnBonus: 0.5, frailMult: 1 },
      { desc: "妖潮密度 +75% · 结算 +80%", scoreMult: 1.8, hpMult: 1, spawnBonus: 0.75, frailMult: 1 },
    ],
  },
  frail: {
    id: "frail", name: "脆体", tiers: [
      { desc: "生命上限 -25% · 结算 +50%", scoreMult: 1.5, hpMult: 1, spawnBonus: 0, frailMult: 0.75 },
      { desc: "生命上限 -35% · 结算 +80%", scoreMult: 1.8, hpMult: 1, spawnBonus: 0, frailMult: 0.65 },
      { desc: "生命上限 -45% · 结算 +120%", scoreMult: 2.2, hpMult: 1, spawnBonus: 0, frailMult: 0.55 },
    ],
  },
};

export const PACT_IDS = Object.keys(PACTS) as PactId[];

/** 档位合法性（1..3 整数）。 */
export function isValidTier(t: unknown): t is number {
  return typeof t === "number" && Number.isInteger(t) && t >= 1 && t <= PACT_MAX_TIER;
}

/** 血誓文案标签：一档纯名（旧文案兼容），二/三档附罗马数字。 */
export function pactLabel(name: string, tier: number): string {
  return tier >= 2 && tier <= PACT_MAX_TIER ? `${name}${PACT_ROMAN[tier]}` : name;
}
