class_name SoundManager
extends Node

static var instance: SoundManager

var music_player: AudioStreamPlayer
var sfx_player: AudioStreamPlayer

var current_music: AudioStream = null

enum SoundType {
	MELEE_ATTACK,
	RANGED_ATTACK,
	MAGIC_ATTACK,
	ZOMBIE_HIT,
	ZOMBIE_DIE,
	PLAYER_HURT,
	PLAYER_DIE,
	WEAPON_SELECT,
	UPGRADE,
	GAME_OVER,
	CHARM,
	EXPLOSION
}

func _ready():
	instance = self
	_setup_audio_players()

func _setup_audio_players():
	music_player = AudioStreamPlayer.new()
	music_player.volume_db = -10.0
	add_child(music_player)
	
	sfx_player = AudioStreamPlayer.new()
	sfx_player.volume_db = -5.0
	add_child(sfx_player)

func _exit_tree():
	instance = null

static func play_sound(sound_type: SoundType):
	if not instance:
		return
	
	var stream = instance._generate_sound(sound_type)
	if stream:
		if instance.sfx_player.playing:
			instance.sfx_player.stop()
		instance.sfx_player.stream = stream
		instance.sfx_player.play()

static func play_melee_attack():
	play_sound(SoundType.MELEE_ATTACK)

static func play_ranged_attack():
	play_sound(SoundType.RANGED_ATTACK)

static func play_magic_attack():
	play_sound(SoundType.MAGIC_ATTACK)

static func play_zombie_hit():
	play_sound(SoundType.ZOMBIE_HIT)

static func play_zombie_die():
	play_sound(SoundType.ZOMBIE_DIE)

static func play_player_hurt():
	play_sound(SoundType.PLAYER_HURT)

static func play_player_die():
	play_sound(SoundType.PLAYER_DIE)

static func play_weapon_select():
	play_sound(SoundType.WEAPON_SELECT)

static func play_upgrade():
	play_sound(SoundType.UPGRADE)

static func play_game_over():
	play_sound(SoundType.GAME_OVER)

static func play_charm():
	play_sound(SoundType.CHARM)

static func play_explosion():
	play_sound(SoundType.EXPLOSION)

func _generate_sound(sound_type: SoundType) -> AudioStream:
	var audio_stream = AudioStreamGenerator.new()
	audio_stream.mix_rate = 44100
	
	match sound_type:
		SoundType.MELEE_ATTACK:
			return _create_impact_sound(150, 0.1)
		SoundType.RANGED_ATTACK:
			return _create_shoot_sound(400, 0.15)
		SoundType.MAGIC_ATTACK:
			return _create_magic_sound(600, 0.3)
		SoundType.ZOMBIE_HIT:
			return _create_impact_sound(200, 0.05)
		SoundType.ZOMBIE_DIE:
			return _create_impact_sound(100, 0.15)
		SoundType.PLAYER_HURT:
			return _create_impact_sound(180, 0.08)
		SoundType.PLAYER_DIE:
			return _create_fall_sound(0.5)
		SoundType.WEAPON_SELECT:
			return _create_chime_sound(880, 0.2)
		SoundType.UPGRADE:
			return _create_chime_sound(1100, 0.3)
		SoundType.GAME_OVER:
			return _create_descend_sound(0.8)
		SoundType.CHARM:
			return _create_chime_sound(1200, 0.25)
		SoundType.EXPLOSION:
			return _create_explosion_sound(0.4)
	
	return null

func _create_impact_sound(frequency: float, duration: float) -> AudioStream:
	var stream = AudioStreamGenerator.new()
	stream.mix_rate = 44100
	
	var sample = PackedByteArray()
	var length = int(44100 * duration)
	sample.resize(length)
	
	var amplitude = 0.3
	var phase = 0.0
	var phase_increment = frequency * 2.0 * PI / 44100.0
	
	for i in range(length):
		var t = float(i) / 44100.0
		var envelope = 1.0 - (t / duration)
		envelope = max(0.0, envelope * envelope)
		
		var sample_value = sin(phase) * amplitude * envelope
		sample[i] = int(clamp(sample_value * 127 + 128, 0, 255))
		
		phase += phase_increment
		if phase > 2.0 * PI:
			phase -= 2.0 * PI
	
	var wav_stream = AudioStreamWAV.new()
	wav_stream.format = AudioStreamWAV.FORMAT_8_BITS
	wav_stream.data = sample
	wav_stream.loop_mode = AudioStreamWAV.LOOP_DISABLED
	
	return wav_stream

func _create_shoot_sound(frequency: float, duration: float) -> AudioStream:
	var stream = AudioStreamGenerator.new()
	stream.mix_rate = 44100
	
	var sample = PackedByteArray()
	var length = int(44100 * duration)
	sample.resize(length)
	
	var amplitude = 0.2
	var phase = 0.0
	var phase_increment = frequency * 2.0 * PI / 44100.0
	
	for i in range(length):
		var t = float(i) / 44100.0
		var envelope = 1.0 - t
		envelope = envelope * envelope
		
		var sample_value = sin(phase) * amplitude * envelope
		sample[i] = int(clamp(sample_value * 127 + 128, 0, 255))
		
		phase += phase_increment
	
	var wav_stream = AudioStreamWAV.new()
	wav_stream.format = AudioStreamWAV.FORMAT_8_BITS
	wav_stream.data = sample
	wav_stream.loop_mode = AudioStreamWAV.LOOP_DISABLED
	
	return wav_stream

func _create_magic_sound(frequency: float, duration: float) -> AudioStream:
	var stream = AudioStreamGenerator.new()
	stream.mix_rate = 44100
	
	var sample = PackedByteArray()
	var length = int(44100 * duration)
	sample.resize(length)
	
	var amplitude = 0.15
	
	for i in range(length):
		var t = float(i) / 44100.0
		var freq = frequency + sin(t * 10.0) * 200.0
		var phase = t * freq * 2.0 * PI
		var envelope = 1.0 - (t / duration)
		envelope = max(0.0, envelope * envelope)
		
		var sample_value = sin(phase) * amplitude * envelope
		sample[i] = int(clamp(sample_value * 127 + 128, 0, 255))
	
	var wav_stream = AudioStreamWAV.new()
	wav_stream.format = AudioStreamWAV.FORMAT_8_BITS
	wav_stream.data = sample
	wav_stream.loop_mode = AudioStreamWAV.LOOP_DISABLED
	
	return wav_stream

func _create_chime_sound(frequency: float, duration: float) -> AudioStream:
	var stream = AudioStreamGenerator.new()
	stream.mix_rate = 44100
	
	var sample = PackedByteArray()
	var length = int(44100 * duration)
	sample.resize(length)
	
	var amplitude = 0.2
	
	for i in range(length):
		var t = float(i) / 44100.0
		var freq = frequency * (1.0 + sin(t * 5.0) * 0.1)
		var phase = t * freq * 2.0 * PI
		var envelope = 1.0 - (t / duration)
		envelope = envelope * envelope
		
		var sample_value = sin(phase) * amplitude * envelope
		sample[i] = int(clamp(sample_value * 127 + 128, 0, 255))
	
	var wav_stream = AudioStreamWAV.new()
	wav_stream.format = AudioStreamWAV.FORMAT_8_BITS
	wav_stream.data = sample
	wav_stream.loop_mode = AudioStreamWAV.LOOP_DISABLED
	
	return wav_stream

func _create_fall_sound(duration: float) -> AudioStream:
	var stream = AudioStreamGenerator.new()
	stream.mix_rate = 44100
	
	var sample = PackedByteArray()
	var length = int(44100 * duration)
	sample.resize(length)
	
	var amplitude = 0.2
	
	for i in range(length):
		var t = float(i) / 44100.0
		var freq = 200.0 * (1.0 - t * 0.8)
		var phase = t * freq * 2.0 * PI
		var envelope = 1.0 - t
		envelope = envelope * envelope
		
		var sample_value = sin(phase) * amplitude * envelope
		sample[i] = int(clamp(sample_value * 127 + 128, 0, 255))
	
	var wav_stream = AudioStreamWAV.new()
	wav_stream.format = AudioStreamWAV.FORMAT_8_BITS
	wav_stream.data = sample
	wav_stream.loop_mode = AudioStreamWAV.LOOP_DISABLED
	
	return wav_stream

func _create_descend_sound(duration: float) -> AudioStream:
	var stream = AudioStreamGenerator.new()
	stream.mix_rate = 44100
	
	var sample = PackedByteArray()
	var length = int(44100 * duration)
	sample.resize(length)
	
	var amplitude = 0.2
	
	for i in range(length):
		var t = float(i) / 44100.0
		var freq = 400.0 * (1.0 - t * 0.9)
		var phase = t * freq * 2.0 * PI
		var envelope = 1.0 - t
		envelope = envelope * envelope * envelope
		
		var sample_value = sin(phase) * amplitude * envelope
		sample[i] = int(clamp(sample_value * 127 + 128, 0, 255))
	
	var wav_stream = AudioStreamWAV.new()
	wav_stream.format = AudioStreamWAV.FORMAT_8_BITS
	wav_stream.data = sample
	wav_stream.loop_mode = AudioStreamWAV.LOOP_DISABLED
	
	return wav_stream

func _create_explosion_sound(duration: float) -> AudioStream:
	var stream = AudioStreamGenerator.new()
	stream.mix_rate = 44100
	
	var sample = PackedByteArray()
	var length = int(44100 * duration)
	sample.resize(length)
	
	var amplitude = 0.3
	
	for i in range(length):
		var t = float(i) / 44100.0
		var _freq = 100.0 * (1.0 - t * 0.95)
		var noise = randf_range(-1.0, 1.0)
		var envelope = 1.0 - t
		envelope = envelope * envelope
		
		var sample_value = noise * amplitude * envelope
		sample[i] = int(clamp(sample_value * 127 + 128, 0, 255))
	
	var wav_stream = AudioStreamWAV.new()
	wav_stream.format = AudioStreamWAV.FORMAT_8_BITS
	wav_stream.data = sample
	wav_stream.loop_mode = AudioStreamWAV.LOOP_DISABLED
	
	return wav_stream

