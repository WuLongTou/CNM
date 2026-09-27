// 每日妖潮（R26）：对标 Spelunky/STS 每日挑战——日期派生种子，全服同题；
// 今日最佳只统计当日种子开局（输赢都计分，取最高）。
// R39 每日主题：按星期派生天气偏向（雨日/雾日/血月日…），让每日有可感知的风味差异。

import type { WeatherType } from "./weather";

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

export interface DailyTheme {
  /** 天气偏向（reroll 时 50% 命中）；周日与周三为 null = 无偏向。 */
  favor: WeatherType | null;
  /** 展示名（每日行/横幅用）。 */
  label: string;
}

/** 每日天气主题（R39）：按星期固定——日晴/一雨/二雾/三无/四雨/五血月/六雾。 */
export function dailyTheme(d = new Date()): DailyTheme {
  const map: Record<number, DailyTheme> = {
    0: { favor: "Clear", label: "晴空日" },
    1: { favor: "Rain", label: "雨日" },
    2: { favor: "Fog", label: "雾日" },
    3: { favor: null, label: "乱流日" },
    4: { favor: "Rain", label: "雨日" },
    5: { favor: "BloodMoon", label: "血月日" },
    6: { favor: "Fog", label: "雾日" },
  };
  return map[d.getDay()];
}
