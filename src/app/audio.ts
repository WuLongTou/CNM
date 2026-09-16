// WebAudio 音频：预加载 wav → 按名播放；BGM 循环；主音量持久化 localStorage。
// 资产来自 assets/audio（命名即用途，对齐 SoundManager）。

export class AudioManager {
  private ctx: AudioContext | null = null;
  private buffers = new Map<string, AudioBuffer>();
  private bgmSource: AudioBufferSourceNode | null = null;
  private bgmGain: GainNode | null = null;
  private sfxGain: GainNode | null = null;
  /** 主音量 0-1（持久化）。 */
  volume = 0.7;

  constructor() {
    const saved = localStorage.getItem("zhanyaolu_volume");
    if (saved !== null) this.volume = Math.min(1, Math.max(0, Number(saved)));
  }

  async init(): Promise<void> {
    try {
      this.ctx = new AudioContext();
      this.sfxGain = this.ctx.createGain();
      this.sfxGain.gain.value = this.volume;
      this.sfxGain.connect(this.ctx.destination);
      this.bgmGain = this.ctx.createGain();
      this.bgmGain.gain.value = this.volume * 0.4;
      this.bgmGain.connect(this.ctx.destination);
      const files = ["melee_attack", "ranged_attack", "magic_attack", "enemy_hit", "enemy_die",
        "player_hurt", "player_die", "game_over", "dash", "upgrade", "weapon_select", "combo_3", "combo_5"];
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

  play(name: string): void {
    if (!this.ctx || !this.sfxGain) return;
    const buf = this.buffers.get(name);
    if (!buf) return;
    const src = this.ctx.createBufferSource();
    src.buffer = buf;
    src.connect(this.sfxGain);
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

  setVolume(v: number): void {
    this.volume = Math.min(1, Math.max(0, v));
    if (this.sfxGain) this.sfxGain.gain.value = this.volume;
    if (this.bgmGain) this.bgmGain.gain.value = this.volume * 0.4;
    localStorage.setItem("zhanyaolu_volume", String(this.volume));
  }
}
