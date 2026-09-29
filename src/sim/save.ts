// 存档接口与默认实现。sim 层只依赖 SaveStorage 接口（可注入内存版做测试），
// localStorage 适配在 app 层。
// 并扩展永久升级（R5 起 6 维：metaHp/metaDmg/metaSpd/metaMag/metaGreed/metaRegen）
// 与成就持久化（achievements）。

export interface SaveData {
  highScore: number;
  totalKills: number;
  games: number;
  coins: number;
  /** 永久升级等级：生命 +20/级。 */
  metaHp: number;
  /** 永久升级等级：伤害 +10%/级。 */
  metaDmg: number;
  /** 永久升级等级：移速 +4%/级（R5）。 */
  metaSpd: number;
  /** 永久升级等级：拾取半径 +12%/级（R5）。 */
  metaMag: number;
  /** 永久升级等级：金币结算 +10%/级（R5）。 */
  metaGreed: number;
  /** 永久升级等级：生命回复 +0.4/s/级（R5）。 */
  metaRegen: number;
  /** 通关过的最高难度 id（R10；-1 = 从未通关，老存档缺字段默认 -1）。 */
  maxDifficulty: number;
  /** 图鉴讨伐数（R15；妖物类型 id → 跨局累计击杀，老存档缺字段默认 {}）。 */
  codex: Record<number, number>;
  /** 制霸记录（R21；职业 id → 通关过的难度 id 列表，老存档缺字段默认 {}）。 */
  clears: Record<string, number[]>;
  /** 今日最佳（R26；仅当日种子开局计分，老存档缺字段默认空）。 */
  dailyBest: { date: string; score: number };
  /** 武器藏珍（R30；拥有过的武器 defId，老存档缺字段默认 []）。 */
  armory: string[];
  /** 融合图鉴（R32；实际炼成过的武器 defId，老存档缺字段默认 []）。 */
  fused: string[];
  /** 终极图鉴（R35；达成过终极阶段的武器 defId，老存档缺字段默认 []）。 */
  ultimates: string[];
  /** Boss 连战最佳分（R45；老存档缺字段默认 0）。 */
  rushBest: number;
  /** 每日变异通关次数（R74；变异 id → 通关数，老存档缺字段默认 {}）。 */
  mutatorClears: Record<string, number>;
  /** R99 每日挑战连胜：最后挑战日期键 + 连续天数（跨天断裂重置 1）。 */
  dailyStreak: { last: string; count: number };
  /** R174 每日挑战周历：当日挑战成绩（date → 当日最佳分，新在前，cap 28 天；老存档缺字段默认 []）。 */
  dailyHistory: Array<{ date: string; score: number }>;
  /** R112 Boss Rush 每日最佳（按日轮换，与全时 rushBest 并行）。 */
  dailyRushBest: { date: string; score: number };
  /** R102 本地战绩历史：最近 10 局（新的在前）。 */
  runHistory: RunRecord[];
  /** 生涯统计（R64；老存档缺字段逐项回退默认）。 */
  stats: LifetimeStats;
  /** 跨局持久成就 id。 */
  achievements: string[];
}

/** R102 单局战绩（帮助页「战绩」tab 陈列）。 */
export interface RunRecord {
  score: number;
  kills: number;
  survivalTime: number;
  round: number;
  roleName: string;
  difficultyName: string;
  mode: string;
  seed: number;
  date: string;
  cleared: boolean;
  /** R154 本局解锁的成就名快照。 */
  achievements: string[];
}

/** 生涯统计（R64）：Hades 式主菜单生涯陈列的持久底座。 */
export interface LifetimeStats {
  /** 总存活秒。 */
  timeAlive: number;
  /** 累计获得金币（结算口径，含加成；无尽二次结算按实发累加）。 */
  coinsEarned: number;
  /** 职业 id → 局数（最爱职业）。 */
  gamesByRole: Record<string, number>;
  /** 单局最高击杀。 */
  maxKills: number;
  /** 生涯最高连击。 */
  maxCombo: number;
  /** 累计精英讨伐。 */
  totalElites: number;
  /** 累计妖王讨伐。 */
  totalBosses: number;
}

export function defaultStats(): LifetimeStats {
  return {
    timeAlive: 0, coinsEarned: 0, gamesByRole: {},
    maxKills: 0, maxCombo: 0, totalElites: 0, totalBosses: 0,
  };
}

/** 存档归一化：顶层缺字段与嵌套 stats 逐项回退默认（老存档/半新存档零迁移成本）。 */
export function normalizeSave(parsed: Partial<SaveData> | null): SaveData {
  const d = defaultSave();
  if (!parsed) return d;
  return {
    ...d,
    ...parsed,
    mutatorClears: { ...(parsed.mutatorClears ?? {}) },
    dailyStreak: { last: "", count: 0, ...(parsed.dailyStreak ?? {}) },
    dailyHistory: (parsed.dailyHistory ?? []).slice(0, 28),
    dailyRushBest: { date: "", score: 0, ...(parsed.dailyRushBest ?? {}) },
    runHistory: Array.isArray(parsed.runHistory) ? parsed.runHistory.slice(0, 10) : [],
    stats: { ...d.stats, ...(parsed.stats ?? {}) },
  };
}

export interface SaveStorage {
  load(): SaveData | null;
  save(data: SaveData): void;
}

export function defaultSave(): SaveData {
  return {
    highScore: 0, totalKills: 0, games: 0, coins: 0,
    metaHp: 0, metaDmg: 0, metaSpd: 0, metaMag: 0, metaGreed: 0, metaRegen: 0,
    maxDifficulty: -1,
    codex: {},
    clears: {},
    dailyBest: { date: "", score: 0 },
    armory: [],
    fused: [],
    ultimates: [],
    rushBest: 0,
    mutatorClears: {},
    dailyStreak: { last: "", count: 0 },
    dailyHistory: [],
    dailyRushBest: { date: "", score: 0 },
    runHistory: [],
    stats: defaultStats(),
    achievements: [],
  };
}

/** 内存版（单测/临时会话用）。 */
export function memoryStorage(): SaveStorage & { data: SaveData } {
  let data: SaveData | null = null;
  return {
    get data() { return data ?? defaultSave(); },
    load() { return data; },
    save(d: SaveData) { data = d; },
  };
}

/** 浏览器 localStorage 版。 */
export function localStorageStorage(key = "zhanyaolu_save"): SaveStorage {
  return {
    load() {
      try {
        const raw = localStorage.getItem(key);
        return raw ? normalizeSave(JSON.parse(raw) as Partial<SaveData>) : null;
      } catch {
        return null;
      }
    },
    save(d: SaveData) {
      try { localStorage.setItem(key, JSON.stringify(d)); } catch { /* 隐私模式等场景忽略 */ }
    },
  };
}
