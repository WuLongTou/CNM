"""Generate 64x64 pixel-art PNG icons using 16x16 effective grid."""
from PIL import Image, ImageDraw
import os

OUT = "/Users/liyou/Work/ZVS/assets/sprites/generated"

def new():
    return Image.new("RGBA", (64, 64), (0, 0, 0, 0))

def px(d, x, y, color, s=4):
    """Draw pixel at (x,y) with size s."""
    d.rectangle([x*s, y*s, (x+1)*s-1, (y+1)*s-1], fill=color)

# ============================================================
# BOOTS SPEED - Boot with wing emblem on 16x16 grid
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

    # Boot shaft (vertical part on right)
    for y in range(2, 6):
        for x in range(6, 10):
            px(d, x, y, brown)

    # Boot foot (horizontal part pointing left)
    for y in range(6, 8):
        for x in range(4, 10):
            px(d, x, y, brown)

    # Boot toe (pointed)
    for y in range(7, 9):
        for x in range(3, 5):
            px(d, x, y, brown)

    # Sole
    for x in range(3, 10):
        px(d, x, 8, brown_dark)

    # Top opening (darker)
    for x in range(6, 10):
        px(d, x, 2, brown_dark)

    # Highlight on shaft
    px(d, 6, 3, brown_light)

    # Wing emblem
    wing = [(4, 3), (3, 2), (2, 1), (3, 4), (2, 3)]
    for x, y in wing:
        px(d, x, y, cyan)

    # Wing highlight
    px(d, 3, 2, cyan_light)
    px(d, 2, 1, cyan_light)

    # Speed lines
    px(d, 0, 2, cyan_light)
    px(d, 1, 3, cyan_light)
    px(d, 0, 4, cyan_light)

    return img

# ============================================================
# POTION HEALTH - Flask with cross on 16x16 grid
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
    for x in range(7, 10):
        px(d, x, 0, cork)
    px(d, 7, 1, cork_dark)

    # Neck
    for y in range(1, 5):
        for x in range(7, 10):
            px(d, x, y, glass)
        px(d, 6, y, glass_dark)

    # Body (circle at 8, 10 radius 5)
    for y in range(5, 15):
        for x in range(3, 13):
            dx = x - 8
            dy = y - 10
            if dx*dx + dy*dy <= 25:
                px(d, x, y, glass)

    # Body shading (right side darker)
    for y in range(5, 15):
        for x in range(9, 13):
            dx = x - 8
            dy = y - 10
            if dx*dx + dy*dy <= 25:
                px(d, x, y, glass_dark)

    # Highlight
    for y in range(6, 9):
        for x in range(4, 7):
            dx = x - 8
            dy = y - 10
            if dx*dx + dy*dy <= 25:
                px(d, x, y, glass_light)
    px(d, 5, 7, highlight)

    # White cross
    for y in range(8, 12):
        px(d, 7, y, white)
    for x in range(5, 10):
        px(d, x, 9, white)

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