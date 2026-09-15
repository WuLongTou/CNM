// 种子化 PRNG（mulberry32）：sim 层唯一随机源。
// 确定性要求：同 seed + 同输入序列 → 同结果，逻辑回归可复现。

export class Rng {
  private s: number;

  constructor(seed: number) {
    this.s = (seed >>> 0) || 1;
  }

  /** [0,1) 均匀分布。 */
  next(): number {
    this.s = (this.s + 0x6d2b79f5) >>> 0;
    let t = this.s;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  }

  /** [min,max) 浮点。 */
  range(min: number, max: number): number {
    return min + this.next() * (max - min);
  }

  /** [min,max] 整数（含两端）。 */
  int(min: number, max: number): number {
    return Math.floor(this.range(min, max + 1));
  }

  chance(p: number): boolean {
    return this.next() < p;
  }

  pick<T>(arr: readonly T[]): T {
    return arr[this.int(0, arr.length - 1)];
  }

  /** 按权重表抽样：weights 为 [值, 权重] 数组。 */
  weighted<T>(entries: ReadonlyArray<readonly [T, number]>): T {
    let total = 0;
    for (const [, w] of entries) total += w;
    let roll = this.next() * total;
    for (const [v, w] of entries) {
      roll -= w;
      if (roll <= 0) return v;
    }
    return entries[entries.length - 1][0];
  }
}
