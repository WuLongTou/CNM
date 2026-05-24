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
# BOOTS SPEED - clear boot silhouette with speed wing
# ============================================================
def boots_speed():
    """敏捷靴 - clear boot shape with wing."""
    img = new()
    d = ImageDraw.Draw(img)

    brown = (140, 90, 50)
    brown_dark = (100, 60, 35)
    brown_light = (180, 130, 80)
    cyan = (50, 180, 210)
    cyan_light = (120, 230, 250)
    dark = (30, 30, 40)

    # Boot shape - clearly a boot
    # Shaft (upper part)
    for y in range(12, 24):
        for x in range(20, 38):
            px(d, x, y, brown)

    # Ankle area
    for y in range(24, 28):
        for x in range(18, 38):
            px(d, x, y, brown)

    # Foot part
    for y in range(28, 32):
        for x in range(14, 38):
            px(d, x, y, brown)

    # Sole - darker bottom
    for x in range(14, 38):
        px(d, x, 31, brown_dark)
        px(d, x, 32, (70, 45, 25))

    # Boot top opening
    for x in range(20, 38):
        px(d, x, 12, brown_dark)

    # Front highlight (shoe tongue area)
    for y in range(14, 26):
        px(d, 21, y, brown_light)

    # Wing emblem - clear speed symbol
    # Wing curves up and back
    wing = [(12, 18), (10, 16), (8, 14), (10, 18), (8, 16),
            (12, 20), (10, 22), (8, 24), (10, 20)]
    for x, y in wing:
        px(d, x, y, cyan)

    # Wing highlight
    for x, y in [(11, 17), (9, 15), (7, 13)]:
        px(d, x, y, cyan_light)

    # Speed lines - 3 distinct lines
    for x, y in [(4, 16), (6, 18), (2, 18)]:
        px(d, x, y, cyan_light)
    for x, y in [(4, 20), (6, 22), (2, 22)]:
        px(d, x, y, cyan_light)

    return img

# ============================================================
# POTION HEALTH - classic red potion bottle with cross
# ============================================================
def potion_health():
    """生命药水 - red potion with white cross medical symbol."""
    img = new()
    d = ImageDraw.Draw(img)

    cork = (160, 110, 50)
    cork_dark = (120, 80, 35)
    glass = (200, 60, 60)
    glass_dark = (150, 40, 40)
    glass_light = (230, 120, 120)
    highlight = (255, 230, 230)
    white = (255, 255, 255)

    # Cork (stopper)
    for x in range(26, 38):
        px(d, x, 4, cork)
    for x in range(28, 36):
        px(d, x, 3, cork_dark)

    # Bottle neck
    for y in range(6, 12):
        for x in range(28, 36):
            px(d, x, y, glass)
        px(d, 27, y, glass_dark)
        px(d, 36, y, glass_dark)

    # Bottle body (rounded bottom)
    for y in range(12, 28):
        width = 14 if y < 20 else 14 - (y - 20)
        for x in range(16 - width, 16 + width):
            px(d, x, y, glass)

    # Body shading (right side darker)
    for y in range(12, 28):
        for x in range(24, 30):
            px(d, x, y, glass_dark)

    # Glass highlight (left side reflection)
    px(d, 17, 14, glass_light)
    px(d, 18, 15, highlight)
    px(d, 17, 16, highlight)

    # Bottom
    for x in range(16, 30):
        px(d, x, 27, glass_dark)
        px(d, x, 28, (120, 30, 30))

    # White cross on bottle (medical symbol)
    # Vertical bar
    for y in range(17, 23):
        px(d, 16, y, white)
        px(d, 17, y, white)
    # Horizontal bar
    for x in range(14, 20):
        px(d, x, 19, white)
        px(d, x, 20, white)

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