#!/usr/bin/env python3
"""Process AI-generated single-pose sprites into game frames."""

from PIL import Image
import os

BASE = "/Users/liyou/Work/ZVS/assets/sprites/player"
TARGET_SIZE = 96


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


def process_sprite(input_path, output_path):
    img = Image.open(input_path)
    img = strip_white(img)
    if max(img.size) > TARGET_SIZE:
        ratio = TARGET_SIZE / max(img.size)
        new_size = (int(img.size[0] * ratio), int(img.size[1] * ratio))
        img = img.resize(new_size, Image.Resampling.LANCZOS)
    img.save(output_path, "PNG")


print("=== CREATING WARRIOR WALK FRAMES (8 SEPARATE POSES) ===")

walk_files = [
    "image_0_Pixel_ar_20260422_015808.jpg",
    "image_1_Pixel_ar_20260422_015808.jpg",
    "image_2_Pixel_ar_20260422_015809.jpg",
    "image_3_Pixel_ar_20260422_015810.jpg",
    "image_4_Pixel_ar_20260422_015810.jpg",
    "image_5_Pixel_ar_20260422_015810.jpg",
    "image_6_Pixel_ar_20260422_015811.jpg",
    "image_7_Pixel_ar_20260422_015811.jpg",
]

for i, f in enumerate(walk_files):
    process_sprite(f"{BASE}/{f}", f"{BASE}/warrior_walk_{i+1}.png")
    print(f"  Created warrior_walk_{i+1}.png")

# Idle from first frame
process_sprite(f"{BASE}/{walk_files[0]}", f"{BASE}/warrior_idle.png")
print(f"  Created warrior_idle.png")

# Cleanup
print("\n=== CLEANUP ===")
for f in walk_files:
    os.remove(f"{BASE}/{f}")
    print(f"  Deleted {f}")

print("\nDone!")