// 天气系统（简化复刻）：4 种天气 × 数值修正。每 60s 概率切换，种子化随机。
// 数值语义对齐 WeatherSystem：雨减速、血月增妖速与精英率、雾降能见度（表现层处理）。

export type WeatherType = "Clear" | "Rain" | "Fog" | "BloodMoon";
export type WeatherIntensity = "Light" | "Moderate";

export interface WeatherState {
  type: WeatherType;
  intensity: WeatherIntensity;
  /** 妖物速度乘数。 */
  enemySpeedMult: number;
  /** 玩家速度乘数。 */
  playerSpeedMult: number;
  /** 精英概率加成。 */
  eliteBonus: number;
}

import type { Rng } from "./rng";

const WEATHER_TABLE: Record<WeatherType, Omit<WeatherState, "intensity">> = {
  Clear: { type: "Clear", enemySpeedMult: 1, playerSpeedMult: 1, eliteBonus: 0 },
  Rain: { type: "Rain", enemySpeedMult: 0.85, playerSpeedMult: 0.92, eliteBonus: 0 },
  Fog: { type: "Fog", enemySpeedMult: 0.95, playerSpeedMult: 0.95, eliteBonus: 0.02 },
  BloodMoon: { type: "BloodMoon", enemySpeedMult: 1.15, playerSpeedMult: 1, eliteBonus: 0.1 },
};

/** 天气影响一句话（R54 纯函数可单测）：播报时告知玩家实际效果（数值表派生）。 */
export function weatherEffectTip(state: WeatherState): string {
  switch (state.type) {
    case "Clear":
      return "风平浪静";
    case "Rain":
      return "雨地湿滑：妖物迟缓 15%，你亦缓 8%";
    case "Fog":
      return `浓雾弥漫：视野收窄，精英更易现身（+${Math.round(state.eliteBonus * 100)}%）`;
    case "BloodMoon":
      return `血月当空：妖物迅捷 15%，精英频出（+${Math.round(state.eliteBonus * 100)}%）`;
  }
}

export class WeatherSystem {
  state: WeatherState = { ...WEATHER_TABLE.Clear, intensity: "Light" };
  private timer = 0;
  private static SWITCH_INTERVAL = 60;

  /** R39 每日主题：非空时 reroll 有 50% 概率命中偏向天气。 */
  constructor(private rng: Rng, private favor: WeatherType | null = null) {}

  update(dt: number): void {
    this.timer += dt;
    if (this.timer >= WeatherSystem.SWITCH_INTERVAL) {
      this.timer -= WeatherSystem.SWITCH_INTERVAL;
      if (this.rng.chance(0.6)) this.reroll();
    }
  }

  private reroll(): void {
    // R39 每日主题偏向：50% 直接锁定主题天气（其余走均匀随机）
    const type = this.favor && this.rng.chance(0.5)
      ? this.favor
      : this.rng.pick(["Clear", "Rain", "Fog", "BloodMoon"] as const);
    const intensity = this.rng.chance(0.7) ? "Light" : "Moderate";
    const base = WEATHER_TABLE[type];
    const scale = intensity === "Moderate" ? 1.5 : 1;
    this.state = {
      type,
      intensity,
      enemySpeedMult: 1 + (base.enemySpeedMult - 1) * scale,
      playerSpeedMult: 1 + (base.playerSpeedMult - 1) * scale,
      eliteBonus: base.eliteBonus * scale,
    };
  }
}
