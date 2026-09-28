// 表现层偏好（R61 无障碍设置）：震屏 / 伤害数字 / 顿帧三开关。
// 独立于游戏存档（SaveData）——重置战绩不应丢失 UI 偏好（业内惯例）；
// 纯数据 + 持久化策略，零 DOM 依赖（vitest 直测）。
// 开关只在消费端生效（view 震屏/飘字、main 顿帧缩放），sim 层零改动。

/** 存储最小接口（浏览器 localStorage 天然满足；测试注入内存版）。 */
export interface KVStorage {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
}

export interface VisualSettings {
  /** 屏幕震动（命中/击杀/事件震屏）。 */
  shake: boolean;
  /** 伤害数字飘字。 */
  damageNumbers: boolean;
  /** 命中停顿（打击感凝固）。 */
  hitstop: boolean;
  /** R63 手柄震动反馈（受击/击杀/妖王现身/完美闪避）。 */
  rumble: boolean;
}

export const VISUAL_SETTINGS_KEY = "zhanyaolu_settings";

export function defaultVisualSettings(): VisualSettings {
  return { shake: true, damageNumbers: true, hitstop: true, rumble: true };
}

/** 读取偏好：损坏 JSON / 缺字段 / 类型不对一律回退默认（老用户与异常数据零迁移成本）。 */
export function loadVisualSettings(storage: KVStorage): VisualSettings {
  const d = defaultVisualSettings();
  try {
    const raw = storage.getItem(VISUAL_SETTINGS_KEY);
    if (!raw) return d;
    const parsed = JSON.parse(raw) as Partial<VisualSettings>;
    return {
      shake: typeof parsed.shake === "boolean" ? parsed.shake : d.shake,
      damageNumbers: typeof parsed.damageNumbers === "boolean" ? parsed.damageNumbers : d.damageNumbers,
      hitstop: typeof parsed.hitstop === "boolean" ? parsed.hitstop : d.hitstop,
      rumble: typeof parsed.rumble === "boolean" ? parsed.rumble : d.rumble,
    };
  } catch {
    return d;
  }
}

export function saveVisualSettings(storage: KVStorage, s: VisualSettings): void {
  try {
    storage.setItem(VISUAL_SETTINGS_KEY, JSON.stringify(s));
  } catch { /* 隐私模式等场景忽略，与 save.ts 同策略 */ }
}
