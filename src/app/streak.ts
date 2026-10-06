// 连杀里程碑阶梯（R180 定 10/25/50；R201 起每满 100 再奖励一档）。
// 纯函数便于单测；main 层击杀事件驱动（2s 无击杀复位，R178 口径）。

/** 命中里程碑返回该档数值，否则 null。 */
export function streakMilestone(n: number): number | null {
  if (n === 10 || n === 25 || n === 50) return n;
  if (n >= 100 && n % 100 === 0) return n;
  return null;
}
