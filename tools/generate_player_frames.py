#!/usr/bin/env python3
"""Generate missing player sprite frames: idle PNGs and ranger attack frames."""

from PIL import Image
import os

BASE = "/Users/liyou/Work/ZVS/assets/sprites/player"


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


def generate_idle_from_source(source_path, char_name):
    """Convert idle.jpg to idle.png with proper processing."""
    img = load_and_strip(source_path)
    if img is None:
        print(f"  Source not found: {source_path}")
        return False

    out_path = f"{BASE}/{char_name}_idle.png"
    img.save(out_path, "PNG")
    print(f"  Saved {out_path}")
    return True


def generate_ranger_attack():
    """Generate ranger attack frames from available sources: aim, shot, player_attack."""
    print("\n=== RANGER ATTACK ===")

    sources = [
        ("ranger_aim.jpg", 0.0),
        ("ranger_shot.jpg", 0.3),
        ("player_attack_2.jpg", 0.5),
        ("player_attack_3.jpg", 0.7),
        ("ranger_shot.jpg", 0.9),
        ("ranger_aim.jpg", 1.0),
    ]

    for i, (src_name, _) in enumerate(sources):
        src_path = f"{BASE}/{src_name}"
        img = load_and_strip(src_path)
        if img:
            out_path = f"{BASE}/ranger_attack_{i+1}.png"
            img.save(out_path, "PNG")
            print(f"  Saved {out_path}")
        else:
            print(f"  Source not found: {src_path}, using fallback")
            # Create a simple fallback
            fallback = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
            out_path = f"{BASE}/ranger_attack_{i+1}.png"
            fallback.save(out_path, "PNG")


def generate_warrior_idle():
    """Generate warrior idle.png from warrior_idle.jpg."""
    print("\n=== WARRIOR IDLE ===")
    source_path = f"{BASE}/warrior_idle.jpg"
    img = load_and_strip(source_path)
    if img:
        out_path = f"{BASE}/warrior_idle.png"
        img.save(out_path, "PNG")
        print(f"  Saved {out_path}")


def generate_mage_idle():
    """Generate mage idle.png from mage_idle.jpg."""
    print("\n=== MAGE IDLE ===")
    source_path = f"{BASE}/mage_idle.jpg"
    img = load_and_strip(source_path)
    if img:
        out_path = f"{BASE}/mage_idle.png"
        img.save(out_path, "PNG")
        print(f"  Saved {out_path}")


def generate_ranger_idle():
    """Generate ranger idle.png - use first walk frame as base and modify."""
    print("\n=== RANGER IDLE ===")
    # Use first walk frame and create a stable stance version
    src_path = f"{BASE}/ranger_walk_1.png"
    if os.path.exists(src_path):
        img = Image.open(src_path)
        # Just strip white and save as idle
        img = strip_white(img)
        out_path = f"{BASE}/ranger_idle.png"
        img.save(out_path, "PNG")
        print(f"  Saved {out_path}")
    else:
        print(f"  Source not found: {src_path}")


if __name__ == "__main__":
    generate_warrior_idle()
    generate_mage_idle()
    generate_ranger_idle()
    generate_ranger_attack()

    print("\nAll player sprite frames generated!")