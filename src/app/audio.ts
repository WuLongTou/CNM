// WebAudio 音频：预加载 wav → 按名播放；BGM 循环；音乐/音效双音量持久化
// localStorage（R67 拆分，对标 Dead Cells / Celeste 设置面板）。
// 资产来自 assets/audio（命名即用途，对齐 SoundManager）。

/** 音量钳制 0-1（纯函数，vitest 直测）。 */
export function clampVolume(v: number): number {
  return Math.min(1, Math.max(0, Number.isFinite(v) ? v : 0));
}

/** 老单音量键迁移（R67 前仅主音量，BGM 固定 ×0.4）：拆为 音乐=旧×0.4 / 音效=旧。
 *  saved 为 null（无旧档）时返回默认（音乐 0.28 / 音效 0.7，等价旧默认 0.7）。 */
export function legacyVolumeSplit(saved: string | null): { music: number; sfx: number } {
  if (saved === null || saved === "") return { music: 0.28, sfx: 0.7 };
  const v = clampVolume(Number(saved));
  return { music: clampVolume(v * 0.4), sfx: v };
}

const KEY_MUSIC = "zhanyaolu_music";
const KEY_SFX = "zhanyaolu_sfx";
const KEY_LEGACY = "zhanyaolu_volume";

export class AudioManager {
  private ctx: AudioContext | null = null;
  private buffers = new Map<string, AudioBuffer>();
  private bgmSource: AudioBufferSourceNode | null = null;
  private bgmGain: GainNode | null = null;
  private sfxGain: GainNode | null = null;
  /** 音乐音量 0-1（持久化；默认等价旧主音量 0.7×0.4）。 */
  musicVolume: number;
  /** 音效音量 0-1（持久化）。 */
  sfxVolume: number;

  constructor() {
    // R67 双键优先；老单键迁移一次（旧 BGM 固定 ×0.4，听感不变）
    const legacy = localStorage.getItem(KEY_LEGACY);
    const music = localStorage.getItem(KEY_MUSIC);
    const sfx = localStorage.getItem(KEY_SFX);
    if (music !== null && sfx !== null) {
      this.musicVolume = clampVolume(Number(music));
      this.sfxVolume = clampVolume(Number(sfx));
    } else {
      const split = legacyVolumeSplit(legacy);
      this.musicVolume = split.music;
      this.sfxVolume = split.sfx;
    }
  }

  async init(): Promise<void> {
    try {
      this.ctx = new AudioContext();
      this.sfxGain = this.ctx.createGain();
      this.sfxGain.gain.value = this.sfxVolume;
      this.sfxGain.connect(this.ctx.destination);
      this.bgmGain = this.ctx.createGain();
      this.bgmGain.gain.value = this.musicVolume;
      this.bgmGain.connect(this.ctx.destination);
      const files = ["melee_attack", "ranged_attack", "magic_attack", "enemy_hit", "enemy_die",
        "player_hurt", "player_die", "game_over", "dash", "upgrade", "weapon_select", "combo_3", "combo_5",
        "lightning", "charm", "explosion", "footstep"];
      await Promise.all(files.map(async (f) => {
        const res = await fetch(`assets/audio/${f}.wav`);
        if (!res.ok) return;
        this.buffers.set(f, await this.ctx!.decodeAudioData(await res.arrayBuffer()));
      }));
      const bgm = await fetch("assets/audio/bg_music.wav");
      if (bgm.ok) {
        this.buffers.set("bg_music", await this.ctx.decodeAudioData(await bgm.arrayBuffer()));
      }
    } catch {
      // 音频失败不阻塞游戏（无头测试环境常见）
      this.ctx = null;
    }
  }

  /** 播放音效。pitch 变调（随机化避免重复感），vol 相对音效音量的乘数（<1 压低）。 */
  play(name: string, opts?: { pitch?: number; vol?: number }): void {
    if (!this.ctx || !this.sfxGain) return;
    const buf = this.buffers.get(name);
    if (!buf) return;
    const src = this.ctx.createBufferSource();
    src.buffer = buf;
    src.playbackRate.value = opts?.pitch ?? 1;
    if (opts?.vol !== undefined && opts.vol < 1) {
      const g = this.ctx.createGain();
      g.gain.value = opts.vol;
      src.connect(g);
      g.connect(this.sfxGain);
    } else {
      src.connect(this.sfxGain);
    }
    src.start();
  }

  playBgm(): void {
    if (!this.ctx || !this.bgmGain) return;
    const buf = this.buffers.get("bg_music");
    if (!buf || this.bgmSource) return;
    const src = this.ctx.createBufferSource();
    src.buffer = buf;
    src.loop = true;
    src.connect(this.bgmGain);
    src.start();
    this.bgmSource = src;
  }

  /** 停止 BGM（GameOver 结算等场景）。 */
  stopBgm(): void {
    if (this.bgmSource) {
      try { this.bgmSource.stop(); } catch { /* 已停止则忽略 */ }
      this.bgmSource = null;
    }
  }

  setMusicVolume(v: number): void {
    this.musicVolume = clampVolume(v);
    if (this.bgmGain) this.bgmGain.gain.value = this.musicVolume;
    localStorage.setItem(KEY_MUSIC, String(this.musicVolume));
  }

  setSfxVolume(v: number): void {
    this.sfxVolume = clampVolume(v);
    if (this.sfxGain) this.sfxGain.gain.value = this.sfxVolume;
    localStorage.setItem(KEY_SFX, String(this.sfxVolume));
  }

  /** 手势唤醒（R68 浏览器自动播放策略）：ctx 在 boot 无手势期创建会被置为
   *  suspended，必须在用户手势内 resume，否则 BGM/音效全程静音。
   *  运行中调用是无害空操作（幂等），首手势后每次手势重复调用安全。 */
  resume(): void {
    if (this.ctx && this.ctx.state === "suspended") {
      this.ctx.resume().catch(() => { /* 唤醒失败静默（自动播放策略极端环境） */ });
    }
  }

  /** 当前 AudioContext 状态（none = ctx 未初始化；调试探针 __CNM__.audioState）。 */
  get state(): string {
    return this.ctx?.state ?? "none";
  }
}
