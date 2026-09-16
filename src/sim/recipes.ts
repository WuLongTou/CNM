// 合成配方表（对齐 C# Recipes：6 条）。cost 为材料消耗，apply 为合成效果。

import type { Material } from "./inventory";

export interface Recipe {
  id: string;
  name: string;
  desc: string;
  cost: Partial<Record<Material, number>>;
  /** 合成产物：武器 id 或属性效果。 */
  weapon?: string;
  effect?: "maxhp" | "heal";
  amount?: number;
}

export const RECIPES: Recipe[] = [
  { id: "wood_sword", name: "木剑", desc: "应急的近战武器", cost: { wood: 3 }, weapon: "tiejian" },
  { id: "stone_axe", name: "石斧", desc: "沉重的采集斧", cost: { wood: 2, stone: 3 }, weapon: "zhanfu" },
  { id: "wood_bow", name: "木弓", desc: "简易远程武器", cost: { wood: 3, fiber: 2 }, weapon: "gongjian" },
  { id: "magic_staff", name: "魔法杖", desc: "注入妖力的法杖", cost: { wood: 2, stone: 2, fiber: 2 }, weapon: "fazhang" },
  { id: "leather_armor", name: "皮甲", desc: "永久 +20 上限生命", cost: { fiber: 4, berry: 1 }, effect: "maxhp", amount: 20 },
  { id: "herb_paste", name: "草药膏", desc: "立即恢复 40 生命", cost: { berry: 3 }, effect: "heal", amount: 40 },
];
