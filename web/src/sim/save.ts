// 存档接口与默认实现。sim 层只依赖 SaveStorage 接口（可注入内存版做测试），
// localStorage 适配在 app 层。结构对齐 Godot 版 SaveManager 的 JSON 字典。

export interface SaveData {
  highScore: number;
  totalKills: number;
  games: number;
  coins: number;
}

export interface SaveStorage {
  load(): SaveData | null;
  save(data: SaveData): void;
}

export function defaultSave(): SaveData {
  return { highScore: 0, totalKills: 0, games: 0, coins: 0 };
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
export function localStorageStorage(key = "zvs_web_save"): SaveStorage {
  return {
    load() {
      try {
        const raw = localStorage.getItem(key);
        return raw ? (JSON.parse(raw) as SaveData) : null;
      } catch {
        return null;
      }
    },
    save(d: SaveData) {
      try { localStorage.setItem(key, JSON.stringify(d)); } catch { /* 隐私模式等场景忽略 */ }
    },
  };
}
