#!/usr/bin/env python3
"""Process AI-generated mage sprites into game-ready frames."""

from PIL import Image
import os

BASE = "/Users/liyou/Work/ZVS/assets/sprites/player"
TARGET_SIZE = 128


def strip_white(img, threshold=230):
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


def process_and_save(input_path, output_path):
    img = Image.open(input_path)
    img = strip_white(img)
    if max(img.size) > TARGET_SIZE:
        ratio = TARGET_SIZE / max(img.size)
        new_size = (int(img.size[0] * ratio), int(img.size[1] * ratio))
        img = img.resize(new_size, Image.Resampling.LANCZOS)
    img.save(output_path, "PNG")
    print(f"  Created {output_path}")


def delete_file(path):
    if os.path.exists(path):
        os.remove(path)
        print(f"  Deleted {path}")


print("=== CREATING MAGE SPRITES ===\n")

# Idle
print("Idle:")
process_and_save(f"{BASE}/image_0_Pixel_ar_20260422_013227.jpg", f"{BASE}/mage_idle.png")

# Walk frames (8)
walk_files = [
    "image_0_Pixel_ar_20260422_013117.jpg",
    "image_1_Pixel_ar_20260422_013117.jpg",
    "image_2_Pixel_ar_20260422_013117.jpg",
    "image_3_Pixel_ar_20260422_013118.jpg",
    "image_4_Pixel_ar_20260422_013118.jpg",
    "image_5_Pixel_ar_20260422_013119.jpg",
    "image_6_Pixel_ar_20260422_013119.jpg",
    "image_7_Pixel_ar_20260422_013119.jpg",
]
print("\nWalk frames:")
for i, f in enumerate(walk_files):
    process_and_save(f"{BASE}/{f}", f"{BASE}/mage_walk_{i+1}.png")

# Attack frames (6)
attack_files = [
    "image_0_Pixel_ar_20260422_013153.jpg",
    "image_1_Pixel_ar_20260422_013154.jpg",
    "image_2_Pixel_ar_20260422_013154.jpg",
    "image_3_Pixel_ar_20260422_013155.jpg",
    "image_4_Pixel_ar_20260422_013155.jpg",
    "image_5_Pixel_ar_20260422_013156.jpg",
]
print("\nAttack frames:")
for i, f in enumerate(attack_files):
    process_and_save(f"{BASE}/{f}", f"{BASE}/mage_attack_{i+1}.png")

# Cleanup
print("\n=== CLEANUP ===\n")
delete_file(f"{BASE}/image_0_Pixel_ar_20260422_013227.jpg")
for f in walk_files:
    delete_file(f"{BASE}/{f}")
for f in attack_files:
    delete_file(f"{BASE}/{f}")

print("\nDone!")