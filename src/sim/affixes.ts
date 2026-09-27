// 精英词缀表（R56）：单一事实源——徽标形状/色调/应对/触发说明集中于此，
// view 层画徽标、help 层渲染图鉴、sim 抽池都从这里取，避免三处漂移。

export type AffixId = "swift" | "regen" | "shield" | "explosive" | "frenzy" | "split" | "blink";

export interface AffixDef {
  id: AffixId;
  name: string;
  /** 头顶徽标形状描述（与 view.markAffix 一一对应）。 */
  badge: string;
  /** 行为说明（做了什么）。 */
  effect: string;
  /** 应对建议（玩家该怎么做）。 */
  counter: string;
}

export const AFFIXES: Record<AffixId, AffixDef> = {
  swift: {
    id: "swift", name: "迅捷", badge: "双速线",
    effect: "移速 ×1.5，贴身咬得紧",
    counter: "别直线逃，用冲刺切角拉开",
  },
  regen: {
    id: "regen", name: "再生", badge: "绿十字",
    effect: "未满血时持续回血",
    counter: "集火打死，别打两下就走",
  },
  shield: {
    id: "shield", name: "护盾", badge: "淡蓝圈",
    effect: "额外 25 点护盾值（先破盾）",
    counter: "多段/高攻速武器破盾更快",
  },
  explosive: {
    id: "explosive", name: "爆裂", badge: "血红爆点",
    effect: "死亡自爆，范围内的你受伤",
    counter: "击杀后立刻走开，勿贴脸输出",
  },
  frenzy: {
    id: "frenzy", name: "狂乱", badge: "暗红双线",
    effect: "血量低于 40% 时移速 ×1.5",
    counter: "残血阶段保持距离，远程收尾",
  },
  split: {
    id: "split", name: "分裂", badge: "母子双圈",
    effect: "死亡时爆出 2 只满血蚀群（不连锁）",
    counter: "备好溅射/穿透，否则越打越多",
  },
  blink: {
    id: "blink", name: "疾影", badge: "紫菱形",
    effect: "每 4 秒闪现至你 150px 内",
    counter: "闪现落点在近战范围外，留反应距离",
  },
};

export const AFFIX_IDS = Object.keys(AFFIXES) as AffixId[];
