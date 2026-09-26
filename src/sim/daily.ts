// 每日妖潮（R26）：对标 Spelunky/STS 每日挑战——日期派生种子，全服同题；
// 今日最佳只统计当日种子开局（输赢都计分，取最高）。

/** 日期键 YYYY-MM-DD（存档比对与展示共用）。 */
export function dailyKey(d = new Date()): string {
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${d.getFullYear()}-${m}-${day}`;
}

/** 当日种子 YYYYMMDD（8 位整数，Rng/parseSeed 全兼容）。 */
export function dailySeed(d = new Date()): number {
  return d.getFullYear() * 10000 + (d.getMonth() + 1) * 100 + d.getDate();
}
