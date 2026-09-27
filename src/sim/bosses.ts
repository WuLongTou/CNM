// 妖王图鉴表（R57）：五王轮换顺序与各自原生机制/应对集中于此，
// 帮助页图鉴渲染、快照/测试核对都从这里取，避免文案与数值漂移。

import { EnemyType } from "./config";

export interface BossDef {
  type: EnemyType;
  /** 王名（与 ENEMY_NAMES 拼接“X王”）。 */
  name: string;
  /** 原生机制说明（它做什么）。 */
  mech: string;
  /** 应对建议（你该怎么做）。 */
  counter: string;
}

/** 妖王轮换顺序（与 game.BOSS_ROSTER 一致：前 5 阶各一只，终幕按序下一只）。 */
export const BOSSES: BossDef[] = [
  {
    type: EnemyType.Basic, name: "游魂",
    mech: "无原生机制，纯弹幕压制（三阶段弹环渐密）",
    counter: "练走位：贴弹用冲刺触发完美闪避，换取增伤窗口",
  },
  {
    type: EnemyType.Tank, name: "石魅",
    mech: "高血量厚重体型，移动慢但硬直抗性高",
    counter: "别绕圈耗——集中火力速杀，避免被小怪夹击",
  },
  {
    type: EnemyType.Mech, name: "铁傀",
    mech: "护甲减伤 25%（所有来源伤害）",
    counter: "高攻速/多段武器更划算；淬毒与灼烧不受护甲影响",
  },
  {
    type: EnemyType.Knight, name: "甲妖",
    mech: "周期举盾 1.2 秒（期间完全免伤），冷却 5 秒",
    counter: "举盾期收手走位，落盾瞬间倾泻火力",
  },
  {
    type: EnemyType.Dragon, name: "蛟",
    mech: "蓄力 0.5 秒后锁定方向高速冲撞（6× 速度、1.5× 伤害）",
    counter: "见红光预警立即横向切角，别恋战",
  },
];

/** 弹幕参数（快照/测试与图鉴文案共用）。 */
export const BOSS_BARRAGE_INFO = { cd: 4.5, first: 3, dmgMult: 0.7, speed: 280, count: [9, 11, 13] };
