// 血誓契约（R25）：对标 Hades 惩罚契约的自选逆境——开局立誓加难，结算分数加成。
// 默认全关，对现有数值零影响；可叠加，分数乘数连乘；重开沿用（同职业/难度）。

export type PactId = "blood" | "horde" | "frail";

export interface PactDef {
  id: PactId;
  name: string;
  /** 一句话效果（欢迎页展示，与实际乘数一一对应）。 */
  desc: string;
  /** 结算分数乘数（连乘；经分数自然流入结算金币）。 */
  scoreMult: number;
  /** 妖物生命乘数（血战；createEnemy 生效）。 */
  hpMult: number;
  /** 额外刷怪概率（人海；每次维持刷怪后按此概率多刷一只）。 */
  spawnBonus: number;
  /** 玩家生命上限乘数（脆体；建角时生效）。 */
  frailMult: number;
}

export const PACTS: Record<PactId, PactDef> = {
  blood: {
    id: "blood", name: "血战", desc: "妖物生命 +25% · 结算 +25%",
    scoreMult: 1.25, hpMult: 1.25, spawnBonus: 0, frailMult: 1,
  },
  horde: {
    id: "horde", name: "人海", desc: "妖潮密度 +30% · 结算 +30%",
    scoreMult: 1.3, hpMult: 1, spawnBonus: 0.3, frailMult: 1,
  },
  frail: {
    id: "frail", name: "脆体", desc: "生命上限 -25% · 结算 +50%",
    scoreMult: 1.5, hpMult: 1, spawnBonus: 0, frailMult: 0.75,
  },
};

export const PACT_IDS = Object.keys(PACTS) as PactId[];
