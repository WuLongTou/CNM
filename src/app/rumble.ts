// 手柄震动反馈（R63）：dual-rumble 效果表 + 节流管理器。
// 完成手柄体验闭环（对标 Dead Cells / Celeste：受击/关键事件震动 + 可开关）。
// 效果参数集中一张表（对齐 sim/hitstop 的时长参数风格），纯函数可单测；
// 执行器走结构化最小接口（DOM GamepadHapticActuator 天然满足），注入可测。
// 开关由 app 层偏好（VisualSettings.rumble）经 enabled() 注入，本模块不碰存档。

/** dual-rumble 效果参数（字段名对齐 GamepadHapticEffectParameters.dualRumble）。 */
export interface RumbleEffect {
  /** 时长 ms。 */
  duration: number;
  /** 强马达（低频）0-1。 */
  strongMagnitude: number;
  /** 弱马达（高频）0-1。 */
  weakMagnitude: number;
}

export type RumbleKind = "hurt" | "kill" | "boss" | "dodge";

/** 效果表：受击强震 / 击杀轻震 / 妖王现身长震 / 完美闪避倒置签名（弱强互换）。 */
export const RUMBLE_FX: Record<RumbleKind, RumbleEffect> = {
  hurt: { duration: 180, strongMagnitude: 1.0, weakMagnitude: 0.4 },
  kill: { duration: 60, strongMagnitude: 0.15, weakMagnitude: 0.35 },
  boss: { duration: 320, strongMagnitude: 0.7, weakMagnitude: 0.5 },
  dodge: { duration: 140, strongMagnitude: 0.2, weakMagnitude: 0.8 },
};

/** 击杀震动节流：割草节奏下限制最短间隔，避免马达常转。 */
export const RUMBLE_KILL_THROTTLE_MS = 90;

/** 振动执行器最小结构（DOM GamepadHapticActuator 的 dual-rumble 子集）。 */
export interface RumbleActuator {
  playEffect(type: string, params: RumbleEffect): Promise<unknown>;
}

export function buildRumble(kind: RumbleKind): RumbleEffect {
  return { ...RUMBLE_FX[kind] };
}

export class RumbleManager {
  private lastKillAt = -Infinity;

  constructor(
    /** 取当前手柄振动执行器（无手柄/无执行器返回空）。 */
    private getActuator: () => RumbleActuator | null | undefined,
    /** 表现设置开关（R61 偏好，main 层注入读取闭包）。 */
    private enabled: () => boolean,
    /** 时钟注入（测试用），默认墙钟。 */
    private now: () => number = () => performance.now(),
  ) {}

  play(kind: RumbleKind): void {
    if (!this.enabled()) return;
    if (kind === "kill") {
      const t = this.now();
      if (t - this.lastKillAt < RUMBLE_KILL_THROTTLE_MS) return;
      this.lastKillAt = t;
    }
    const fx = buildRumble(kind);
    this.getActuator()?.playEffect("dual-rumble", fx).catch(() => { /* 振动失败静默（旧设备不支持） */ });
  }
}
