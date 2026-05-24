"""Generate improved 64x64 pixel-art PNG icons with proper alpha."""
from PIL import Image, ImageDraw
import os
import math

OUT = "/Users/liyou/Work/ZVS/assets/sprites/generated"

def new():
    return Image.new("RGBA", (64, 64), (0, 0, 0, 0))

def px(d, x, y, color, s=2):
    d.rectangle([x*s, y*s, (x+1)*s-1, (y+1)*s-1], fill=color)

# ============================================================
# BOOTS SPEED - single boot with clear wing emblem
# ============================================================
def boots_speed():
    """敏捷靴 - single boot with cyan wing on side."""
    img = new()
    d = ImageDraw.Draw(img)

    brown = (130, 90, 50)
    brown_dark = (90, 55, 30)
    brown_light = (170, 120, 70)
    cyan = (60, 190, 210)
    cyan_light = (140, 235, 245)
    highlight = (255, 255, 255)

    # Boot shaft
    for y in range(14, 26):
        for x in range(22, 42):
            px(d, x, y, brown)

    # Boot foot
    for y in range(26, 32):
        for x in range(18, 42):
            px(d, x, y, brown)

    # Sole
    for x in range(18, 42):
        px(d, x, 31, brown_dark)
        px(d, x, 32, (65, 40, 20))

    # Top opening
    for x in range(22, 42):
        px(d, x, 14, brown_dark)

    # Highlight stripe on front
    for y in range(16, 28):
        px(d, 23, y, brown_light)
        px(d, 24, y, highlight)

    # Wing emblem - cleaner shape
    wing = [(12, 18), (10, 20), (8, 22), (6, 24), (8, 20), (6, 18),
            (14, 18), (14, 22), (16, 24), (18, 26), (16, 22)]
    for x, y in wing:
        px(d, x, y, cyan)

    # Wing highlight
    for x, y in [(12, 17), (10, 19), (8, 21)]:
        px(d, x, y, cyan_light)

    # Speed lines - 3 lines going left
    for x, y in [(2, 20), (4, 22), (6, 24), (2, 24), (4, 26)]:
        px(d, x, y, cyan_light)

    return img

# ============================================================
# POTION HEALTH - classic red potion bottle
# ============================================================
def potion_health():
    """生命药水 - red potion with heart emblem."""
    img = new()
    d = ImageDraw.Draw(img)

    cork = (160, 110, 50)
    cork_dark = (110, 75, 35)
    glass = (190, 60, 60)
    glass_dark = (140, 40, 40)
    glass_light = (220, 100, 100)
    highlight = (255, 220, 220)

    # Cork
    for x in range(26, 38):
        px(d, x, 4, cork)
        px(d, x, 5, cork)
    for x in range(28, 36):
        px(d, x, 3, cork_dark)

    # Neck
    for y in range(6, 12):
        for x in range(28, 36):
            px(d, x, y, glass)
        px(d, 27, y, glass_dark)
        px(d, 36, y, glass_dark)

    # Body - rounded rectangle
    for y in range(12, 28):
        width = 14 if y < 20 else 14 - (y - 20)
        for x in range(16 - width, 16 + width):
            px(d, x, y, glass)

    # Body shading - right side darker
    for y in range(12, 28):
        for x in range(24, 30):
            px(d, x, y, glass_dark)

    # Glass highlight - left side
    px(d, 17, 14, glass_light)
    px(d, 18, 15, highlight)
    px(d, 17, 16, highlight)
    px(d, 18, 17, glass_light)

    # Bottom
    for x in range(16, 30):
        px(d, x, 27, glass_dark)
        px(d, x, 28, (100, 30, 30))

    # Heart emblem on bottle
    for x in range(14, 18):
        px(d, x, 18, (255, 100, 100))
        px(d, x, 19, (255, 100, 100))
    for x in range(18, 22):
        px(d, x, 18, (255, 100, 100))
        px(d, x, 19, (255, 100, 100))
    for x in range(14, 22):
        px(d, x, 20, (255, 100, 100))
    px(d, 15, 21, (255, 100, 100))
    px(d, 16, 22, (255, 100, 100))
    px(d, 17, 21, (255, 100, 100))
    px(d, 18, 21, (255, 100, 100))
    px(d, 16, 23, (255, 100, 100))

    return img

icons = {
    "boots_speed": boots_speed,
    "potion_health": potion_health,
}

for name, fn in icons.items():
    path = os.path.join(OUT, f"{name}.png")
    img = fn()
    img.save(path, "PNG")
    print(f"Created {path}")