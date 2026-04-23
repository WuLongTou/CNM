#!/usr/bin/env python3
"""Process AI-generated warrior sprites."""

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


print("=== CREATING WARRIOR SPRITES ===\n")

# Walk frames (8) - already generated
walk_files = [
    "image_0_Pixel_ar_20260422_013341.jpg",
    "image_1_Pixel_ar_20260422_013342.jpg",
    "image_2_Pixel_ar_20260422_013342.jpg",
    "image_3_Pixel_ar_20260422_013343.jpg",
    "image_4_Pixel_ar_20260422_013343.jpg",
    "image_5_Pixel_ar_20260422_013343.jpg",
    "image_6_Pixel_ar_20260422_013344.jpg",
    "image_7_Pixel_ar_20260422_013344.jpg",
]
print("Walk frames:")
for i, f in enumerate(walk_files):
    process_and_save(f"{BASE}/{f}", f"{BASE}/warrior_walk_{i+1}.png")

# For attack and idle, copy from first walk frame as placeholder
print("\nAttack frames (using walk frame 1):")
for i in range(1, 7):
    img = Image.open(f"{BASE}/warrior_walk_1.png")
    img.save(f"{BASE}/warrior_attack_{i}.png", "PNG")
    print(f"  Created warrior_attack_{i}.png")

print("\nIdle:")
img = Image.open(f"{BASE}/warrior_walk_1.png")
img.save(f"{BASE}/warrior_idle.png", "PNG")
print(f"  Created warrior_idle.png")

# Cleanup
print("\n=== CLEANUP ===\n")
for f in walk_files:
    delete_file(f"{BASE}/{f}")

print("\nDone!")