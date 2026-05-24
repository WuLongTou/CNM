"""Generate improved 64x64 pixel-art PNG icons with proper alpha."""
from PIL import Image, ImageDraw
import os

OUT = "/Users/liyou/Work/ZVS/assets/sprites/generated"

def new():
    return Image.new("RGBA", (64, 64), (0, 0, 0, 0))

def px(d, x, y, color, s=2):
    d.rectangle([x*s, y*s, (x+1)*s-1, (y+1)*s-1], fill=color)

# ============================================================
# BOOTS SPEED - Side-view boot with speed wing
# ============================================================
def boots_speed():
    """敏捷靴 - Side-view boot with wing emblem."""
    img = new()
    d = ImageDraw.Draw(img)

    brown = (140, 95, 55)
    brown_dark = (95, 60, 35)
    brown_light = (175, 130, 80)
    brown_highlight = (200, 160, 100)
    cyan = (45, 185, 215)
    cyan_light = (100, 230, 250)

    # Boot shaft (vertical part)
    for y in range(8, 22):
        for x in range(24, 36):
            px(d, x, y, brown)

    # Boot foot (horizontal part, pointing left)
    for y in range(22, 30):
        for x in range(14, 36):
            px(d, x, y, brown)

    # Sole
    for x in range(14, 36):
        px(d, x, 29, brown_dark)
        px(d, x, 30, (60, 35, 20))

    # Heel
    for y in range(26, 30):
        px(d, 32, y, brown_dark)

    # Top opening
    for x in range(24, 36):
        px(d, x, 8, brown_dark)

    # Highlight on shaft (front edge)
    for y in range(10, 20):
        px(d, 25, y, brown_light)

    # Wing emblem on side
    wing_pts = [(18, 16), (16, 14), (14, 12), (16, 18), (14, 16), (12, 14),
                (20, 18), (18, 20), (16, 22)]
    for x, y in wing_pts:
        px(d, x, y, cyan)

    # Wing highlight dots
    for x, y in [(17, 15), (15, 13), (13, 11)]:
        px(d, x, y, cyan_light)

    # Speed lines behind
    for x, y in [(8, 16), (6, 18), (4, 16), (6, 20), (4, 20)]:
        px(d, x, y, cyan_light)

    return img

# ============================================================
# POTION HEALTH - Classic round-bottom flask with cross
# ============================================================
def potion_health():
    """生命药水 - Round-bottom flask with white cross."""
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
    for x in range(26, 38):
        px(d, x, 4, cork)
    for x in range(28, 36):
        px(d, x, 3, cork_dark)

    # Bottle neck
    for y in range(6, 14):
        for x in range(28, 36):
            px(d, x, y, glass)
        px(d, 27, y, glass_dark)
        px(d, 36, y, glass_dark)

    # Flask body (round bottom) - use actual circle approach
    # Main body circle centered at (16, 24) with radius 10
    for y in range(14, 32):
        for x in range(6, 26):
            # Check if point is inside circle
            dx = x - 16
            dy = y - 26
            if dx*dx + dy*dy <= 100:  # radius 10
                px(d, x, y, glass)

    # Body shading (right side darker)
    for y in range(14, 32):
        for x in range(20, 26):
            dx = x - 16
            dy = y - 26
            if dx*dx + dy*dy <= 100:
                px(d, x, y, glass_dark)

    # Glass highlight (left side)
    for y in range(18, 26):
        for x in range(8, 12):
            dx = x - 16
            dy = y - 26
            if dx*dx + dy*dy <= 100:
                px(d, x, y, glass_light)
    px(d, 10, 20, highlight)
    px(d, 9, 22, highlight)

    # White cross (medical symbol)
    # Vertical
    for y in range(20, 28):
        px(d, 15, y, white)
        px(d, 16, y, white)
    # Horizontal
    for x in range(12, 20):
        px(d, x, 23, white)
        px(d, x, 24, white)

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