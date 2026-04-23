#!/usr/bin/env python3
"""Generate interpolated walk/attack frames from AI-generated sprite images."""

from PIL import Image
import os
import math

BASE = "/Users/liyou/Work/ZVS/assets/sprites"


def strip_white(img, threshold=230):
    """Convert white/near-white pixels to transparent."""
    img = img.convert("RGBA")
    datas = img.getdata()
    new_data = []
    for item in datas:
        r, g, b, a = item
        brightness = (r + g + b) / 3.0
        if r > threshold and g > threshold and b > threshold and brightness > threshold:
            new_data.append((r, g, b, 0))
        else:
            new_data.append((r, g, b, min(255, a + 30) if a > 50 else a))
    img.putdata(new_data)
    return img


def load_and_strip(path):
    if not os.path.exists(path):
        return None
    print(f"  Loading {path}")
    img = Image.open(path)
    return strip_white(img)


def blend_pair(a, b, t):
    return Image.blend(a, b, t)


def generate_from_cycle(frames, count):
    """Generate `count` frames by evenly interpolating through a cyclic walk sequence."""
    n = len(frames)
    result = []
    for i in range(count):
        t = i * n / count
        idx_a = int(t) % n
        idx_b = (int(t) + 1) % n
        frac = t - int(t)
        blended = blend_pair(frames[idx_a], frames[idx_b], frac)
        result.append(blended)
    return result


def generate_attack_frames(idle, attacks, count):
    """Generate attack sequence: idle -> attack_1 -> attack_2 -> attack_3 -> idle."""
    seq = [idle] + attacks + [idle]
    return generate_from_cycle(seq, count)


def process_character(char_name, walk_count, attack_count):
    """Process a character type: load JPGs, interpolate, save PNGs."""
    print(f"\n=== {char_name.upper()} ===")
    char_dir = f"{BASE}/player"

    # Load walk frames
    walks = []
    for i in range(1, 5):
        img = load_and_strip(f"{char_dir}/{char_name}_walk_{i}.jpg")
        if img:
            walks.append(img)

    if len(walks) >= 2:
        frames = generate_from_cycle(walks, walk_count)
        for i, frame in enumerate(frames):
            out = f"{char_dir}/{char_name}_walk_{i+1}.png"
            frame.save(out, "PNG")
            print(f"  Saved {out} ({frame.size})")

    # Load idle
    idle = load_and_strip(f"{char_dir}/{char_name}_idle.jpg")

    # Load attack frames
    attacks = []
    for i in range(1, 4):
        img = load_and_strip(f"{char_dir}/{char_name}_attack_{i}.jpg")
        if img:
            attacks.append(img)

    if idle and len(attacks) >= 1:
        frames = generate_attack_frames(idle, attacks, attack_count)
        for i, frame in enumerate(frames):
            out = f"{char_dir}/{char_name}_attack_{i+1}.png"
            frame.save(out, "PNG")
            print(f"  Saved {out} ({frame.size})")


def process_zombie(ztype, walk_count=6):
    """Process a zombie type: load JPGs, interpolate, save PNGs."""
    print(f"\n=== ZOMBIE {ztype.upper()} ===")
    z_dir = f"{BASE}/zombies"
    base_name = f"zombie_{ztype}"

    walks = []
    for i in range(1, 5):
        img = load_and_strip(f"{z_dir}/{base_name}_walk_{i}.jpg")
        if img:
            walks.append(img)

    if len(walks) >= 2:
        frames = generate_from_cycle(walks, walk_count)
        for i, frame in enumerate(frames):
            out = f"{z_dir}/{base_name}_walk_{i+1}.png"
            frame.save(out, "PNG")
            print(f"  Saved {out} ({frame.size})")
    elif len(walks) == 1:
        # Ping-pong: original -> shifted -> original -> shifted other way
        base = walks[0]
        for i in range(walk_count):
            dy = int(math.sin(i * math.pi / (walk_count / 2)) * 8)
            dx = int(math.cos(i * math.pi / (walk_count / 2)) * 4)
            shifted = base.transform(base.size, Image.Transform.AFFINE,
                                     (1, 0, dx, 0, 1, dy),
                                     Image.Resampling.BICUBIC)
            out = f"{z_dir}/{base_name}_walk_{i+1}.png"
            shifted.save(out, "PNG")
            print(f"  Saved {out} ({shifted.size})")


if __name__ == "__main__":
    # Player characters (8 walk frames, 6 attack frames)
    for char in ["warrior", "ranger", "mage"]:
        process_character(char, walk_count=8, attack_count=6)

    # Zombie types (6 walk frames each)
    for ztype in ["basic", "fast", "tank", "exploder", "spitter", "healer", "invisible", "summoner"]:
        process_zombie(ztype, walk_count=6)

    print("\nAll done!")
