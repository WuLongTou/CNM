class_name SoundManager
extends Node
## 音效管理器
## 攻击音效使用「欢乐颂」旋律 — 每次攻击播放下一个音符
## 连击/击杀会升调加速，营造越打越爽的节奏感

static var instance: SoundManager

var music_player: AudioStreamPlayer
# 多声道 SFX，支持音符重叠
var _sfx_channels: Array[AudioStreamPlayer] = []
var _sfx_channel_idx: int = 0
const SFX_CHANNEL_COUNT: int = 4

enum SoundType {
	MELEE_ATTACK, RANGED_ATTACK, MAGIC_ATTACK,
	ZOMBIE_HIT, ZOMBIE_DIE, PLAYER_HURT, PLAYER_DIE,
	WEAPON_SELECT, UPGRADE, GAME_OVER, CHARM, EXPLOSION
}

# ═══════════════════════════════════════════════
# 欢乐颂旋律 — C4 基准频率表
# ═══════════════════════════════════════════════
# 贝多芬第九交响曲「欢乐颂」主旋律
# E E F G | G F E D | C C D E | E. D D
# E E F G | G F E D | C C D E | D. C C
const _NOTE_C4: float = 261.63
const _NOTE_D4: float = 293.66
const _NOTE_E4: float = 329.63
const _NOTE_F4: float = 349.23
const _NOTE_G4: float = 392.00
const _NOTE_A4: float = 440.00

var _ode_to_joy: Array = [
	329.63, 329.63, 349.23, 392.00,  # E E F G
	392.00, 349.23, 329.63, 293.66,  # G F E D
	261.63, 261.63, 293.66, 329.63,  # C C D E
	329.63, 293.66, 293.66,          # E. D D
	329.63, 329.63, 349.23, 392.00,  # E E F G
	392.00, 349.23, 329.63, 293.66,  # G F E D
	261.63, 261.63, 293.66, 329.63,  # C C D E
	293.66, 261.63, 261.63,          # D. C C
]

# 旋律状态
var _melody_idx: int = 0
var _combo_count: int = 0
var _combo_timer: float = 0.0
const COMBO_TIMEOUT: float = 2.0

# ═══════════════════════════════════════════════
# 非攻击音效参数表
# ═══════════════════════════════════════════════
var _sound_defs: Dictionary = {
	SoundType.ZOMBIE_HIT:    { "freq": 200.0,  "dur": 0.05, "amp": 0.15, "gen": "impact" },
	SoundType.ZOMBIE_DIE:    { "freq": 100.0,  "dur": 0.15, "amp": 0.20, "gen": "impact" },
	SoundType.PLAYER_HURT:   { "freq": 180.0,  "dur": 0.08, "amp": 0.25, "gen": "impact" },
	SoundType.WEAPON_SELECT: { "freq": 880.0,  "dur": 0.20, "amp": 0.20, "gen": "wobble", "mod_freq": 5.0, "mod_depth": 0.1 },
	SoundType.UPGRADE:       { "freq": 1100.0, "dur": 0.30, "amp": 0.20, "gen": "wobble", "mod_freq": 5.0, "mod_depth": 0.1 },
	SoundType.CHARM:         { "freq": 1200.0, "dur": 0.25, "amp": 0.20, "gen": "wobble", "mod_freq": 5.0, "mod_depth": 0.1 },
	SoundType.PLAYER_DIE:    { "freq": 200.0,  "dur": 0.50, "amp": 0.20, "gen": "sweep_down", "sweep": 0.8 },
	SoundType.GAME_OVER:     { "freq": 400.0,  "dur": 0.80, "amp": 0.20, "gen": "sweep_down", "sweep": 0.9 },
	SoundType.EXPLOSION:     { "freq": 100.0,  "dur": 0.40, "amp": 0.30, "gen": "noise" },
}

# ═══════════════════════════════════════════════
# 生命周期
# ═══════════════════════════════════════════════

func _ready():
	instance = self
	music_player = AudioStreamPlayer.new()
	music_player.volume_db = GameConfig.MUSIC_VOLUME_DB
	add_child(music_player)

	for i in range(SFX_CHANNEL_COUNT):
		var ch := AudioStreamPlayer.new()
		ch.volume_db = GameConfig.SFX_VOLUME_DB
		add_child(ch)
		_sfx_channels.append(ch)

func _process(delta: float):
	if _combo_timer > 0:
		_combo_timer -= delta
		if _combo_timer <= 0:
			_combo_count = 0

func _exit_tree():
	if instance == self:
		instance = null

# ═══════════════════════════════════════════════
# 旋律攻击音效
# ═══════════════════════════════════════════════

func _play_melody_note(attack_type: int):
	## attack_type: 0=melee, 1=ranged, 2=magic
	var base_freq: float = _ode_to_joy[_melody_idx]
	_melody_idx = (_melody_idx + 1) % _ode_to_joy.size()

	# combo 升调：每 4 连击升半个八度
	var octave_shift: float = 1.0
	if _combo_count >= 16:
		octave_shift = 2.0   # 两个八度
	elif _combo_count >= 8:
		octave_shift = 1.5   # 一个半八度
	elif _combo_count >= 4:
		octave_shift = 1.25  # 五度

	var freq: float = base_freq * octave_shift

	# 不同攻击类型的音色差异
	var dur: float = 0.12
	var amp: float = 0.22
	var harmonics: float = 0.0  # 谐波量
	match attack_type:
		0:  # 近战 — 短促有力
			dur = 0.10
			amp = 0.28
			harmonics = 0.3
		1:  # 远程 — 清脆
			dur = 0.08
			amp = 0.20
			harmonics = 0.1
		2:  # 魔法 — 带颤音，稍长
			dur = 0.18
			amp = 0.18
			harmonics = 0.05

	# combo 越高节奏越快（缩短音符时长）
	dur = maxf(0.05, dur - _combo_count * 0.003)

	var stream := _build_melody_note(freq, dur, amp, harmonics, attack_type == 2)
	_play_on_channel(stream)

	_combo_count += 1
	_combo_timer = COMBO_TIMEOUT

func _build_melody_note(freq: float, dur: float, amp: float, harmonics: float, vibrato: bool) -> AudioStreamWAV:
	var rate: int = GameConfig.AUDIO_SAMPLE_RATE
	var length: int = int(rate * dur)
	var buf := PackedByteArray()
	buf.resize(length)

	var phase: float = 0.0
	var phase2: float = 0.0
	var inc: float = freq * TAU / rate
	var inc2: float = freq * 2.0 * TAU / rate  # 二次谐波

	for i in range(length):
		var t: float = float(i) / rate
		# ADSR 包络：快速起音 + 自然衰减
		var env: float
		if t < 0.008:
			env = t / 0.008  # 8ms attack
		else:
			env = maxf(0.0, (1.0 - (t - 0.008) / (dur - 0.008)) ** 1.5)

		var f_mod: float = freq
		if vibrato:
			f_mod = freq + sin(t * 12.0) * 8.0  # 魔法颤音
			inc = f_mod * TAU / rate

		# 基波 + 谐波
		var sample: float = sin(phase) + sin(phase2) * harmonics
		sample = sample / (1.0 + harmonics)  # 归一化

		buf[i] = int(clamp(sample * amp * env * 127 + 128, 0, 255))
		phase += inc
		phase2 += inc2
		if phase > TAU:
			phase -= TAU
		if phase2 > TAU:
			phase2 -= TAU

	return _pack_wav(buf)

# ═══════════════════════════════════════════════
# 公共播放接口
# ═══════════════════════════════════════════════

static func play_sound(sound_type: SoundType):
	if not instance:
		return
	var stream := instance._build_sound(sound_type)
	if stream:
		instance._play_on_channel(stream)

static func play_melee_attack():
	if instance: instance._play_melody_note(0)
static func play_ranged_attack():
	if instance: instance._play_melody_note(1)
static func play_magic_attack():
	if instance: instance._play_melody_note(2)

static func play_zombie_hit():     play_sound(SoundType.ZOMBIE_HIT)
static func play_zombie_die():     play_sound(SoundType.ZOMBIE_DIE)
static func play_player_hurt():    play_sound(SoundType.PLAYER_HURT)
static func play_player_die():     play_sound(SoundType.PLAYER_DIE)
static func play_weapon_select():  play_sound(SoundType.WEAPON_SELECT)
static func play_upgrade():        play_sound(SoundType.UPGRADE)
static func play_game_over():      play_sound(SoundType.GAME_OVER)
static func play_charm():          play_sound(SoundType.CHARM)
static func play_explosion():      play_sound(SoundType.EXPLOSION)

func notify_kill():
	## 击杀通知 — 额外加速旋律推进
	_combo_count += 2
	_combo_timer = COMBO_TIMEOUT

# ═══════════════════════════════════════════════
# 多声道播放
# ═══════════════════════════════════════════════

func _play_on_channel(stream: AudioStream):
	var ch := _sfx_channels[_sfx_channel_idx]
	_sfx_channel_idx = (_sfx_channel_idx + 1) % SFX_CHANNEL_COUNT
	if ch.playing:
		ch.stop()
	ch.stream = stream
	ch.play()

# ═══════════════════════════════════════════════
# WAV 构建器 — 非攻击音效
# ═══════════════════════════════════════════════

func _build_sound(sound_type: SoundType) -> AudioStream:
	if not _sound_defs.has(sound_type):
		return null

	var def: Dictionary = _sound_defs[sound_type]
	var dur: float = def["dur"]
	var amp: float = def["amp"]
	var freq: float = def["freq"]
	var rate: int = GameConfig.AUDIO_SAMPLE_RATE
	var length: int = int(rate * dur)
	var samples := PackedByteArray()
	samples.resize(length)

	match def["gen"]:
		"impact":
			_gen_impact(samples, length, rate, freq, amp, dur)
		"wobble":
			_gen_wobble(samples, length, rate, freq, amp, dur,
				def.get("mod_freq", 5.0), def.get("mod_depth", 0.1))
		"sweep_down":
			_gen_sweep_down(samples, length, rate, freq, amp, dur,
				def.get("sweep", 0.8))
		"noise":
			_gen_noise(samples, length, amp, dur)

	return _pack_wav(samples)

# ═══════════════════════════════════════════════
# 波形生成器
# ═══════════════════════════════════════════════

func _gen_impact(buf: PackedByteArray, length: int, rate: int, freq: float, amp: float, dur: float):
	var phase: float = 0.0
	var inc: float = freq * TAU / rate
	for i in range(length):
		var t: float = float(i) / rate
		var env: float = maxf(0.0, (1.0 - t / dur) ** 2)
		buf[i] = int(clamp(sin(phase) * amp * env * 127 + 128, 0, 255))
		phase += inc
		if phase > TAU:
			phase -= TAU

func _gen_wobble(buf: PackedByteArray, length: int, rate: int, freq: float, amp: float, dur: float, mod_freq: float, mod_depth: float):
	for i in range(length):
		var t: float = float(i) / rate
		var f: float
		if mod_depth < 1.0:
			f = freq * (1.0 + sin(t * mod_freq) * mod_depth)
		else:
			f = freq + sin(t * mod_freq) * mod_depth
		var phase: float = t * f * TAU
		var env: float = maxf(0.0, (1.0 - t / dur) ** 2)
		buf[i] = int(clamp(sin(phase) * amp * env * 127 + 128, 0, 255))

func _gen_sweep_down(buf: PackedByteArray, length: int, rate: int, freq: float, amp: float, dur: float, sweep: float):
	for i in range(length):
		var t: float = float(i) / rate
		var f: float = freq * (1.0 - t * sweep)
		var phase: float = t * f * TAU
		var env: float = (1.0 - t / dur) ** (2.0 if sweep < 0.85 else 3.0)
		env = maxf(0.0, env)
		buf[i] = int(clamp(sin(phase) * amp * env * 127 + 128, 0, 255))

func _gen_noise(buf: PackedByteArray, length: int, amp: float, dur: float):
	var rate: int = GameConfig.AUDIO_SAMPLE_RATE
	for i in range(length):
		var t: float = float(i) / rate
		var env: float = maxf(0.0, (1.0 - t / dur) ** 2)
		var n: float = randf_range(-1.0, 1.0)
		buf[i] = int(clamp(n * amp * env * 127 + 128, 0, 255))

func _pack_wav(samples: PackedByteArray) -> AudioStreamWAV:
	var wav := AudioStreamWAV.new()
	wav.format = AudioStreamWAV.FORMAT_8_BITS
	wav.data = samples
	wav.loop_mode = AudioStreamWAV.LOOP_DISABLED
	return wav
