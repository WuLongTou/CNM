#!/usr/bin/env python3
"""Generate WAV audio files for Zombie Wars using Python's wave module."""
import wave
import struct
import math
import random
import os

SAMPLE_RATE = 44100
OUTPUT_DIR = "/Users/liyou/Work/ZVS/assets/audio"

os.makedirs(OUTPUT_DIR, exist_ok=True)


def write_wav(filename, samples, channels=1, normalize=True):
    path = os.path.join(OUTPUT_DIR, filename)
    if normalize and samples:
        max_amp = max(abs(s) for s in samples)
        if max_amp > 0.95:
            samples = [s / max_amp * 0.95 for s in samples]
    with wave.open(path, 'wb') as w:
        w.setnchannels(channels)
        w.setsampwidth(2)  # 16-bit
        w.setframerate(SAMPLE_RATE)
        for s in samples:
            clamped = max(-1.0, min(1.0, s))
            w.writeframes(struct.pack('<h', int(clamped * 32767)))


def sine(freq, duration, amp=0.5):
    n = int(SAMPLE_RATE * duration)
    return [amp * math.sin(2 * math.pi * freq * i / SAMPLE_RATE) for i in range(n)]


def envelope(samples, attack=0.01, release=0.05):
    out = list(samples)
    n = len(out)
    attack_n = int(SAMPLE_RATE * attack)
    release_n = int(SAMPLE_RATE * release)
    for i in range(min(attack_n, n)):
        out[i] *= i / max(attack_n, 1)
    for i in range(max(0, n - release_n), n):
        out[i] *= (n - i) / max(release_n, 1)
    return out


def noise(duration, amp=0.3):
    n = int(SAMPLE_RATE * duration)
    return [amp * (random.random() * 2 - 1) for _ in range(n)]


def sweep(start_freq, end_freq, duration, amp=0.4):
    n = int(SAMPLE_RATE * duration)
    return [amp * math.sin(2 * math.pi * (start_freq + (end_freq - start_freq) * i / n) * i / SAMPLE_RATE) for i in range(n)]


def add(*signals):
    if not signals:
        return []
    max_len = max(len(s) for s in signals)
    result = []
    for i in range(max_len):
        val = sum(s[i] if i < len(s) else 0 for s in signals)
        result.append(val)
    return result


# ===== Sound Effects =====

# Melee attack - punchy impact
print("Generating melee_attack.wav...")
buf = add(sine(150, 0.08, 0.6), sine(300, 0.06, 0.3))
write_wav("melee_attack.wav", envelope(buf, 0.002, 0.04))

# Ranged attack - crisp snap
print("Generating ranged_attack.wav...")
buf = add(sine(800, 0.05, 0.4), sine(1200, 0.04, 0.2))
write_wav("ranged_attack.wav", envelope(buf, 0.001, 0.03))

# Magic attack - mystical whoosh
print("Generating magic_attack.wav...")
buf = add(sine(440, 0.15, 0.3), sine(880, 0.12, 0.2), sweep(200, 2000, 0.1, 0.2))
write_wav("magic_attack.wav", envelope(buf, 0.01, 0.08))

# Zombie hit - grunt
print("Generating zombie_hit.wav...")
buf = sine(180, 0.12, 0.5)
write_wav("zombie_hit.wav", envelope(buf, 0.005, 0.05))

# Zombie die - splat
print("Generating zombie_die.wav...")
buf = add(sine(100, 0.2, 0.6), noise(0.15, 0.2))
write_wav("zombie_die.wav", envelope(buf, 0.01, 0.1))

# Player hurt - pain yelp
print("Generating player_hurt.wav...")
buf = add(sine(300, 0.1, 0.5), sine(200, 0.15, 0.3))
write_wav("player_hurt.wav", envelope(buf, 0.005, 0.08))

# Player die - dramatic fall
print("Generating player_die.wav...")
buf = sweep(400, 80, 0.4, 0.5)
write_wav("player_die.wav", envelope(buf, 0.02, 0.2))

# Weapon select - satisfying click
print("Generating weapon_select.wav...")
buf = sine(660, 0.08, 0.35)
write_wav("weapon_select.wav", envelope(buf, 0.002, 0.04))

# Upgrade - power-up chime
print("Generating upgrade.wav...")
buf = add(sine(523, 0.1, 0.4), sine(659, 0.1, 0.3), sine(784, 0.15, 0.3))
write_wav("upgrade.wav", envelope(buf, 0.005, 0.1))

# Game over - ominous
print("Generating game_over.wav...")
buf = sweep(300, 60, 0.8, 0.5)
write_wav("game_over.wav", envelope(buf, 0.05, 0.3))

# Explosion - boom
print("Generating explosion.wav...")
buf = add(noise(0.4, 0.6), sweep(200, 40, 0.35, 0.5))
write_wav("explosion.wav", envelope(buf, 0.005, 0.25))

# Lightning strike
print("Generating lightning.wav...")
buf = add(noise(0.3, 0.7), sine(2000, 0.05, 0.4))
write_wav("lightning.wav", envelope(buf, 0.001, 0.2))

# Charm effect
print("Generating charm.wav...")
buf = add(sine(440, 0.2, 0.3), sine(550, 0.2, 0.2), sine(660, 0.25, 0.2))
write_wav("charm.wav", envelope(buf, 0.01, 0.1))

# ===== Background Music =====

# Action battle theme - loopable 8-bar phrase
print("Generating bg_music.wav...")
BPM = 140
beat = 60.0 / BPM  # seconds per beat
bar = beat * 4

def note(freq, start_beat, dur_beats, amp=0.3):
    n_start = int(start_beat * SAMPLE_RATE)
    n_dur = int(dur_beats * SAMPLE_RATE)
    samples = [0.0] * (n_start + n_dur)
    for i in range(n_dur):
        t = i / SAMPLE_RATE
        env = 1.0 if i < n_dur - int(0.05 * SAMPLE_RATE) else (n_dur - i) / (0.05 * SAMPLE_RATE)
        samples[n_start + i] = amp * math.sin(2 * math.pi * freq * t) * env
    return samples

# Bass pattern (E minor)
bass_notes = [
    (82.41, 0, 1), (82.41, 1, 1), (98.00, 2, 1), (98.00, 3, 1),  # E E F# F#
    (110.0, 4, 1), (110.0, 5, 1), (98.00, 6, 1), (82.41, 7, 1),  # G# G# F# E
    (82.41, 8, 1), (82.41, 9, 1), (98.00, 10, 1), (98.00, 11, 1),
    (110.0, 12, 1), (110.0, 13, 1), (82.41, 14, 2),
    (73.42, 16, 1), (73.42, 17, 1), (82.41, 18, 1), (73.42, 19, 1),
    (65.41, 20, 2), (55.0, 22, 2),
]

# Melody pattern (pentatonic E minor)
melody_notes = [
    (329.63, 0.5, 0.5, 0.25), (392.00, 1.5, 0.5, 0.25),
    (440.00, 2.5, 1.0, 0.3), (392.00, 4.0, 0.5, 0.25),
    (329.63, 5.0, 0.5, 0.25), (293.66, 6.0, 1.0, 0.3),
    (329.63, 7.5, 0.5, 0.25), (392.00, 8.5, 0.5, 0.25),
    (440.00, 9.5, 1.0, 0.3), (493.88, 11.0, 1.0, 0.35),
    (440.00, 13.0, 0.5, 0.25), (392.00, 14.0, 0.5, 0.25),
    (329.63, 15.0, 2.0, 0.3),
]

# Percussion - kick and snare pattern (8 bars, loop)
def kick(beat_num):
    if beat_num % 8 == 0:
        return sweep(120, 40, 0.12, 0.5)
    return []

def snare(beat_num):
    if beat_num % 4 in (2, 6):
        return noise(0.1, 0.4)
    return []

# Build the track
length_bars = 8
samples_per_bar = int(bar * SAMPLE_RATE)
total_samples = length_bars * samples_per_bar
music = [0.0] * total_samples

for i in range(length_bars):
    bar_start = i * samples_per_bar

    # Bass
    for freq, b_start, b_dur in bass_notes:
        n = int(b_start * SAMPLE_RATE)
        d = int(b_dur * SAMPLE_RATE)
        for j in range(d):
            if bar_start + n + j < total_samples:
                t = j / SAMPLE_RATE
                env = min(1.0, j / max(int(0.02 * SAMPLE_RATE), 1)) * max(0, (d - j) / max(int(0.05 * SAMPLE_RATE), 1))
                music[bar_start + n + j] += 0.4 * math.sin(2 * math.pi * freq * t) * env

    # Melody
    for freq, m_start, m_dur, amp in melody_notes:
        n = int(m_start * SAMPLE_RATE)
        d = int(m_dur * SAMPLE_RATE)
        for j in range(d):
            if bar_start + n + j < total_samples:
                t = j / SAMPLE_RATE
                env = min(1.0, j / max(int(0.01 * SAMPLE_RATE), 1)) * max(0, (d - j) / max(int(0.05 * SAMPLE_RATE), 1))
                music[bar_start + n + j] += amp * math.sin(2 * math.pi * freq * t) * env

    # Drums
    for beat_n in range(4):
        beat_offset = int(beat_n * beat * SAMPLE_RATE)
        k = kick(beat_n)
        for j, s in enumerate(k):
            if bar_start + beat_offset + j < total_samples:
                music[bar_start + beat_offset + j] += s
        s = snare(beat_n)
        for j, sample in enumerate(s):
            if bar_start + beat_offset + j < total_samples:
                music[bar_start + beat_offset + j] += sample

# Normalize
max_amp = max(abs(s) for s in music) if music else 1.0
if max_amp > 1.0:
    music = [s / max_amp * 0.9 for s in music]

write_wav("bg_music.wav", music)

print(f"\nGenerated audio files in {OUTPUT_DIR}:")
for f in sorted(os.listdir(OUTPUT_DIR)):
    size = os.path.getsize(os.path.join(OUTPUT_DIR, f))
    print(f"  {f} ({size:,} bytes)")
print("Done!")
