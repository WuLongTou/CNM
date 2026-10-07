// 库存：采集材料计数（对齐 C# Inventory：Wood/Stone/Fiber/Berry + Changed 事件语义）。

export type Material = "wood" | "stone" | "fiber" | "berry";

/** R210 材料中文名：数据层单一事实源（合成面板与拒绝原因共用，防中英混排与漂移）。 */
export const MATERIAL_NAMES: Record<Material, string> = {
  wood: "木材", stone: "石料", fiber: "纤维", berry: "浆果",
};

export class Inventory {
  counts: Record<Material, number> = { wood: 0, stone: 0, fiber: 0, berry: 0 };
  onChange: Array<() => void> = [];

  add(m: Material, n: number): void {
    this.counts[m] += n;
    for (const cb of this.onChange) cb();
  }

  take(m: Material, n: number): boolean {
    if (this.counts[m] < n) return false;
    this.counts[m] -= n;
    for (const cb of this.onChange) cb();
    return true;
  }

  reset(): void {
    this.counts = { wood: 0, stone: 0, fiber: 0, berry: 0 };
  }
}
