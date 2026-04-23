#!/usr/bin/env python3
"""Extract 8 frames from AI-generated sprite sheet."""

from PIL import Image
import os

BASE = "/Users/liyou/Work/ZVS/assets/sprites/player"


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


def extract_frames(img_path, output_prefix, frames_x=4, frames_y=2, target_size=96):
    """Extract frames from a sprite sheet."""
    img = Image.open(img_path)
    w, h = img.size

    frame_w = w // frames_x
    frame_h = h // frames_y

    for row in range(frames_y):
        for col in range(frames_x):
            idx = row * frames_x + col + 1
            x = col * frame_w
            y = row * frame_h
            frame = img.crop((x, y, x + frame_w, y + frame_h))
            frame = strip_white(frame)

            # Resize to target
            if max(frame.size) > target_size:
                ratio = target_size / max(frame.size)
                new_size = (int(frame.size[0] * ratio), int(frame.size[1] * ratio))
                frame = frame.resize(new_size, Image.Resampling.LANCZOS)

            output_path = f"{BASE}/{output_prefix}_{idx}.png"
            frame.save(output_path, "PNG")
            print(f"  Created {output_path}")


print("=== EXTRACTING WARRIOR WALK FRAMES ===")
extract_frames(
    f"{BASE}/image_0_Pixel_ar_20260422_015344.jpg",
    "warrior_walk",
    frames_x=4,
    frames_y=2,
    target_size=96
)

# Cleanup
print("\n=== CLEANUP ===")
for f in [
    "image_0_Pixel_ar_20260422_015344.jpg",
    "image_1_Pixel_ar_20260422_015344.jpg",
    "image_2_Pixel_ar_20260422_015345.jpg",
    "image_3_Pixel_ar_20260422_015345.jpg",
    "image_4_Pixel_ar_20260422_015345.jpg",
    "image_5_Pixel_ar_20260422_015345.jpg",
    "image_6_Pixel_ar_20260422_015346.jpg",
    "image_7_Pixel_ar_20260422_015346.jpg",
]:
    path = f"{BASE}/{f}"
    if os.path.exists(path):
        os.remove(path)
        print(f"  Deleted {f}")

print("\nDone!")