// 命中停顿（打击感凝固）：对齐 C# HitStopController 的时长参数。
// sim 层只维护计时；app 层据 remaining 计算 dt 缩放（墙钟驱动不会卡死）。

export const HITSTOP = {
  FrozenScale: 0.03,
  CritMs: 45,
  KillMs: 70,
  EliteKillMs: 110,
  BossSpawnMs: 180,
  BossPhaseMs: 100,
  BossKillMs: 220,
} as const;

export class HitStop {
  remainingMs = 0;

  trigger(ms: number): void {
    this.remainingMs = Math.max(this.remainingMs, ms);
  }

  /** 本帧时间缩放（1 = 正常）。 */
  scale(dtMs: number): number {
    if (this.remainingMs <= 0) return 1;
    this.remainingMs -= dtMs;
    return HITSTOP.FrozenScale;
  }
}
