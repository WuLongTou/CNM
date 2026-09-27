// 妖物机制表（R58）：14 种妖物的原生机制与应对集中于此，
// 帮助页妖物图鉴渲染 / 测试核对从这里取，与 sim 行为保持同源叙述。

export interface FoeMech {
  /** 机制简述（它做什么）。 */
  mech: string;
  /** 应对建议（你该怎么做）。 */
  counter: string;
}

export const FOE_MECHS: Record<number, FoeMech> = {
  0: { mech: "无特殊机制", counter: "基础目标，练走砍" },
  1: { mech: "高速贴身追击", counter: "别直线逃，切角拉开" },
  2: { mech: "厚重高血，硬直抗性高", counter: "集中火力速杀" },
  3: { mech: "贴近后自爆（范围伤害）", counter: "见它贴上来立刻走开" },
  4: { mech: "潜行不可见，近身才显形", counter: "留意脚下灰圈与呼吸声" },
  5: { mech: "远程毒弹（附带持续伤害）", counter: "横向走位躲弹，别站桩" },
  6: { mech: "为周围妖物持续回血", counter: "优先点掉，否则越打越硬" },
  7: { mech: "周期召唤蚀群增援", counter: "先杀召唤者，别陷入消耗" },
  8: { mech: "远程箭矢射击", counter: "躲开箭线，靠近它有硬直窗口" },
  9: { mech: "飘忽漂浮轨迹，难预判", counter: "用范围/溅射武器更稳" },
  10: { mech: "护甲减伤 25%", counter: "高攻速武器或淬毒/灼烧" },
  11: { mech: "周期举盾 1.2s 完全免伤", counter: "举盾收手，落盾倾泻火力" },
  12: { mech: "蓄力后锁定方向高速冲撞", counter: "见红光预警横向切角" },
  13: { mech: "成群快攻，单体脆弱", counter: "范围伤害一波清" },
};
