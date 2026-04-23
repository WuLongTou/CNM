#!/usr/bin/env python3
"""Process AI-generated warrior sprites into game-ready frames."""

from PIL import Image
import os
import shutil

BASE = "/Users/liyou/Work/ZVS/assets/sprites/player"
BACKUP = f"{BASE}/backup_old"


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


def process_and_save(input_path, output_path, resize_to=128):
    """Load, strip white, and save as PNG."""
    img = Image.open(input_path)
    img = strip_white(img)
    # Resize for game use
    if max(img.size) > resize_to:
        ratio = resize_to / max(img.size)
        new_size = (int(img.size[0] * ratio), int(img.size[1] * ratio))
        img = img.resize(new_size, Image.Resampling.LANCZOS)
    img.save(output_path, "PNG")
    print(f"  Created {output_path}")


def delete_if_exists(path):
    """Delete file if it exists."""
    if os.path.exists(path):
        os.remove(path)
        print(f"  Deleted {path}")


print("=== PROCESSING WARRIOR SPRITES ===\n")

# Walk frames (8)
print("Walk frames:")
walk_files = [
    "image_0_Pixel_ar_20260422_011401.jpg",
    "image_1_Pixel_ar_20260422_011401.jpg",
    "image_2_Pixel_ar_20260422_011402.jpg",
    "image_3_Pixel_ar_20260422_011402.jpg",
    "image_4_Pixel_ar_20260422_011402.jpg",
    "image_5_Pixel_ar_20260422_011403.jpg",
    "image_6_Pixel_ar_20260422_011403.jpg",
    "image_7_Pixel_ar_20260422_011403.jpg",
]
for i, f in enumerate(walk_files):
    src = f"{BASE}/{f}"
    dst = f"{BASE}/warrior_walk_{i+1}.png"
    process_and_save(src, dst)

# Attack frames (6)
print("\nAttack frames:")
attack_files = [
    "image_0_Pixel_ar_20260422_011457.jpg",
    "image_1_Pixel_ar_20260422_011457.jpg",
    "image_2_Pixel_ar_20260422_011458.jpg",
    "image_3_Pixel_ar_20260422_011458.jpg",
    "image_4_Pixel_ar_20260422_011458.jpg",
    "image_5_Pixel_ar_20260422_011458.jpg",
]
for i, f in enumerate(attack_files):
    src = f"{BASE}/{f}"
    dst = f"{BASE}/warrior_attack_{i+1}.png"
    process_and_save(src, dst)

# Idle (from first variation image)
print("\nIdle frame:")
process_and_save(f"{BASE}/image_1_Pixel_ar_20260422_011147.jpg", f"{BASE}/warrior_idle.png")

# Delete old inconsistent files
print("\n=== CLEANING UP OLD FILES ===\n")

# Old AI variation images
for f in walk_files + attack_files:
    delete_if_exists(f"{BASE}/{f}")
for f in [
    "image_0_Pixel_ar_20260422_011147.jpg",
    "image_1_Pixel_ar_20260422_011147.jpg",
    "image_2_Pixel_ar_20260422_011148.jpg",
    "image_3_Pixel_ar_20260422_011148.jpg",
]:
    delete_if_exists(f"{BASE}/{f}")

# Old warrior JPGs (inconsistent source)
for f in [
    "warrior_attack_1.jpg",
    "warrior_attack_2.jpg",
    "warrior_attack_3.jpg",
    "warrior_idle_old.jpg",
    "warrior_idle.jpg",
]:
    delete_if_exists(f"{BASE}/{f}")

# Delete old PNGs
for i in range(1, 9):
    delete_if_exists(f"{BASE}/warrior_walk_{i}.png")
for i in range(1, 7):
    delete_if_exists(f"{BASE}/warrior_attack_{i}.png")
delete_if_exists(f"{BASE}/warrior_idle.png")

# Process mage sprites (need to check if they need regeneration)
print("\n=== PROCESSING MAGE SPRITES ===\n")
mage_files = [
    ("mage_idle.jpg", "mage_idle.png"),
    ("mage_attack_1.jpg", "mage_attack_1.png"),
    ("mage_attack_2.jpg", "mage_attack_2.png"),
    ("mage_attack_3.jpg", "mage_attack_3.png"),
    ("mage_walk_1.jpg", "mage_walk_1.png"),
    ("mage_walk_2.jpg", "mage_walk_2.png"),
    ("mage_walk_3.jpg", "mage_walk_3.png"),
    ("mage_walk_4.jpg", "mage_walk_4.png"),
]
for src_name, dst_name in mage_files:
    src = f"{BASE}/{src_name}"
    dst = f"{BASE}/{dst_name}"
    if os.path.exists(src):
        process_and_save(src, dst)

# Process ranger sprites
print("\n=== PROCESSING RANGER SPRITES ===\n")
ranger_files = [
    ("ranger_aim.jpg", "ranger_idle.png"),
    ("ranger_shot.jpg", "ranger_attack_1.png"),
    ("mage_attack_2.jpg", "ranger_attack_2.png"),
    ("mage_attack_3.jpg", "ranger_attack_3.png"),
]
for src_name, dst_name in ranger_files:
    src = f"{BASE}/{src_name}"
    dst = f"{BASE}/{dst_name}"
    if os.path.exists(src):
        process_and_save(src, dst)

# Generate ranger walk frames from mage (since they share same body type)
print("\nGenerating ranger walk frames...")
for i in range(1, 9):
    src = f"{BASE}/mage_walk_{((i-1) % 4) + 1}.png"
    dst = f"{BASE}/ranger_walk_{i}.png"
    if os.path.exists(src):
        img = Image.open(src)
        img.save(dst, "PNG")
        print(f"  Created {dst}")

print("\nAll done!")