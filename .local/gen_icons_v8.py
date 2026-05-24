"""Generate 64x64 pixel-art PNG icons with larger pixels (less messy)."""
from PIL import Image, ImageDraw
import os

OUT = "/Users/liyou/Work/ZVS/assets/sprites/generated"

def new():
    return Image.new("RGBA", (64, 64), (0, 0, 0, 0))

def px(d, x, y, color, s=3):
    d.rectangle([x*s, y*s, (x+1)*s-1, (y+1)*s-1], fill=color)

# ============================================================
# BOOTS SPEED - Clear boot shape with wing
# ============================================================
def boots_speed():
    """敏捷靴 - Boot with speed wing."""
    img = new()
    d = ImageDraw.Draw(img)

    brown = (140, 95, 55)
    brown_dark = (95, 60, 35)
    brown_light = (175, 130, 80)
    cyan = (45, 185, 215)
    cyan_light = (100, 230, 250)

    # Boot shaft
    for y in range(10, 22):
        for x in range(24, 38):
            px(d, x, y, brown)

    # Boot foot (pointing left)
    for y in range(22, 30):
        for x in range(14, 38):
            px(d, x, y, brown)

    # Sole
    for x in range(14, 38):
        px(d, x, 29, brown_dark)

    # Top opening
    for x in range(24, 38):
        px(d, x, 10, brown_dark)

    # Highlight
    for y in range(12, 20):
        px(d, 25, y, brown_light)

    # Wing emblem
    wing = [(18, 14), (16, 12), (14, 10), (16, 16), (14, 14), (12, 12)]
    for x, y in wing:
        px(d, x, y, cyan)

    # Speed lines
    for x, y in [(8, 14), (6, 16), (4, 14), (6, 18)]:
        px(d, x, y, cyan_light)

    return img

# ============================================================
# POTION HEALTH - Flask with cross
# ============================================================
def potion_health():
    """生命药水 - Flask with white cross."""
    img = new()
    d = ImageDraw.Draw(img)

    cork = (165, 115, 55)
    cork_dark = (115, 75, 35)
    glass = (195, 55, 55)
    glass_dark = (140, 35, 35)
    glass_light = (225, 110, 110)
    highlight = (255, 235, 235)
    white = (255, 255, 255)

    # Cork
    for x in range(10, 14):
        px(d, x, 2, cork)
    for x in range(10, 14):
        px(d, x, 3, cork_dark)

    # Neck
    for y in range(4, 12):
        for x in range(10, 14):
            px(d, x, y, glass)
        px(d, 9, y, glass_dark)
        px(d, 14, y, glass_dark)

    # Body (circle centered at 16, 22, radius 9)
    for y in range(12, 32):
        for x in range(7, 25):
            dx = x - 16
            dy = y - 24
            if dx*dx + dy*dy <= 81:
                px(d, x, y, glass)

    # Body shading (right side)
    for y in range(12, 32):
        for x in range(18, 25):
            dx = x - 16
            dy = y - 24
            if dx*dx + dy*dy <= 81:
                px(d, x, y, glass_dark)

    # Highlight
    for y in range(16, 22):
        for x in range(8, 12):
            dx = x - 16
            dy = y - 24
            if dx*dx + dy*dy <= 81:
                px(d, x, y, glass_light)
    px(d, 10, 18, highlight)

    # White cross
    for y in range(20, 28):
        px(d, 15, y, white)
    for x in range(12, 19):
        px(d, x, 23, white)

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