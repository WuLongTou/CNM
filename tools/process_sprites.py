#!/usr/bin/env python3
"""Process AI-generated sprite sheets into individual frames."""

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


def process_warrior_variations():
    """Process the 4 warrior variation images into sprite frames."""
    print("=== WARRIOR VARIATIONS ===")
    files = [
        "image_0_Pixel_ar_20260422_011147.jpg",
        "image_1_Pixel_ar_20260422_011147.jpg",
        "image_2_Pixel_ar_20260422_011148.jpg",
        "image_3_Pixel_ar_20260422_011148.jpg",
    ]

    # First, use the consistent top-right image (with armor) as base for idle
    idle_img = Image.open(f"{BASE}/image_1_Pixel_ar_20260422_011147.jpg")
    idle_img = strip_white(idle_img)
    idle_img.save(f"{BASE}/warrior_idle.jpg", "PNG")  # Save as PNG

    # For walk frames, we need to extract the walking poses
    # The images show different armor variations, not different poses
    # We need to generate proper walk animation from a single sprite sheet
    print("  Note: warrior variation images show armor options, not walk poses")
    print("  Using mage walk frames as reference for warrior walk generation")


def process_sprite_sheet_to_frames(img_path, output_prefix, num_frames=8, frames_per_row=4):
    """Split a sprite sheet into individual frames."""
    img = Image.open(img_path)
    w, h = img.size
    frame_w = w // frames_per_row
    frame_h = h // (num_frames // frames_per_row)

    frames = []
    for i in range(num_frames):
        row = i // frames_per_row
        col = i % frames_per_row
        x = col * frame_w
        y = row * frame_h
        frame = img.crop((x, y, x + frame_w, y + frame_h))
        frames.append(strip_white(frame))

    return frames


if __name__ == "__main__":
    # Process warrior variations
    process_warrior_variations()

    print("\nDone! Note: Need proper sprite sheets with multiple poses for walk animations.")