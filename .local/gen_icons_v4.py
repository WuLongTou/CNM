"""Generate improved icons for lowest-scoring items."""
from PIL import Image, ImageDraw
import os
import math

OUT = "/Users/liyou/Work/ZVS/assets/sprites/generated"

def new():
    return Image.new("RGBA", (64, 64), (0, 0, 0, 0))

def px(d, x, y, color, s=2):
    d.rectangle([x*s, y*s, (x+1)*s-1, (y+1)*s-1], fill=color)

# ============================================================
# BOOTS SPEED - single boot with prominent speed design
# ============================================================
def boots_speed():
    """敏捷靴 - single boot with wing emblem."""
    img = new()
    d = ImageDraw.Draw(img)

    brown = (130, 90, 50)
    brown_dark = (90, 60, 35)
    brown_light = (170, 120, 70)
    cyan = (80, 200, 220)
    cyan_light = (150, 240, 250)

    # Single boot (centered)
    # Boot shaft
    for y in range(14, 26):
        for x in range(22, 42):
            px(d, x, y, brown)

    # Boot foot part
    for y in range(26, 32):
        for x in range(18, 42):
            px(d, x, y, brown)

    # Sole
    for x in range(18, 42):
        px(d, x, 31, brown_dark)
        px(d, x, 32, (70, 45, 25))

    # Top opening (darker)
    for x in range(22, 42):
        px(d, x, 14, brown_dark)

    # Highlight (front of boot)
    for y in range(16, 28):
        px(d, 23, y, brown_light)

    # Wing emblem on side
    wing_points = [(14, 20), (12, 22), (14, 24), (12, 26), (10, 28),
                   (16, 22), (18, 24), (16, 26), (18, 28)]
    for x, y in wing_points:
        px(d, x, y, cyan)

    # Speed lines
    for x, y in [(6, 22), (8, 24), (4, 24), (6, 26)]:
        px(d, x, y, cyan_light)

    return img

# ============================================================
# RING REGEN - blue ring with clear plus
# ============================================================
def ring_regen():
    """再生戒指 - blue ring with green plus."""
    img = new()
    d = ImageDraw.Draw(img)

    blue = (60, 130, 200)
    blue_dark = (40, 80, 130)
    green = (80, 200, 100)
    green_light = (150, 255, 150)
    dark_bg = (20, 40, 60)

    # Ring band
    for angle in range(0, 360, 8):
        rad = math.radians(angle)
        r = 12
        cx, cy = 16, 18
        x = int(cx + r * math.cos(rad))
        y = int(cy + r * math.sin(rad))
        if 4 <= x <= 28 and 6 <= y <= 30:
            px(d, x, y, blue)

    # Ring inner shadow
    for angle in range(0, 360, 12):
        rad = math.radians(angle)
        r = 7
        cx, cy = 16, 18
        x = int(cx + r * math.cos(rad))
        y = int(cy + r * math.sin(rad))
        if 9 <= x <= 23 and 11 <= y <= 25:
            px(d, x, y, dark_bg)

    # Plus sign (health regeneration) - vertical bar
    for y in range(14, 22):
        px(d, 16, y, green)
        if y > 15:
            px(d, 15, y, green_light)

    # Horizontal bar
    for x in range(12, 20):
        px(d, x, 18, green)
        if x > 14:
            px(d, x, 17, green_light)

    # Shine
    px(d, 14, 14, green_light)
    px(d, 18, 14, green_light)

    return img

# ============================================================
# AMULET LIFESTEAL - pendant with heart
# ============================================================
def amulet_lifesteal():
    """吸血护符 - gold pendant with ruby heart."""
    img = new()
    d = ImageDraw.Draw(img)

    gold = (220, 180, 60)
    gold_dark = (170, 140, 40)
    ruby = (180, 40, 40)
    ruby_light = (230, 80, 80)
    ruby_highlight = (255, 180, 180)

    # Chain links
    for y in range(4, 14):
        if y % 2 == 0:
            px(d, 16, y, gold)
            px(d, 15, y, gold_dark)
            px(d, 17, y, gold_dark)
        else:
            px(d, 16, y, gold_dark)

    # Pendant bail (connector)
    px(d, 14, 14, gold)
    px(d, 15, 14, gold)
    px(d, 16, 14, gold)
    px(d, 17, 14, gold)
    px(d, 14, 15, gold)
    px(d, 17, 15, gold)

    # Heart shape - top lobes
    for x in range(12, 16):
        px(d, x, 16, ruby)
        px(d, x, 17, ruby)
    for x in range(16, 20):
        px(d, x, 16, ruby)
        px(d, x, 17, ruby)

    # Heart middle
    for x in range(12, 20):
        px(d, x, 18, ruby)

    # Heart bottom
    for x in range(13, 19):
        px(d, x, 19, ruby)
    px(d, 14, 20, ruby)
    px(d, 15, 21, ruby)
    px(d, 16, 22, ruby)
    px(d, 17, 20, ruby)

    # Heart shading (right side darker)
    px(d, 17, 17, ruby_light)
    px(d, 18, 17, ruby_light)
    px(d, 17, 18, ruby_light)
    px(d, 18, 18, ruby_light)

    # Heart highlight
    px(d, 13, 17, ruby_highlight)
    px(d, 14, 16, ruby_highlight)

    return img

# ============================================================
# POITION HEALTH - clearer bottle shape
# ============================================================
def potion_health():
    """生命药水 - red potion bottle."""
    img = new()
    d = ImageDraw.Draw(img)

    cork = (160, 110, 50)
    cork_dark = (120, 80, 35)
    glass = (200, 80, 80)
    glass_dark = (150, 50, 50)
    glass_light = (230, 120, 120)
    highlight = (255, 220, 220)

    # Cork
    for x in range(26, 38):
        px(d, x, 4, cork)
        px(d, x, 5, cork)
    for x in range(28, 36):
        px(d, x, 3, cork_dark)

    # Bottle neck
    for y in range(6, 12):
        for x in range(28, 36):
            px(d, x, y, glass)
        px(d, 27, y, glass_dark)
        px(d, 36, y, glass_dark)

    # Bottle body (rounded rectangle)
    for y in range(12, 28):
        width = 14 if y < 20 else 14 - (y - 20)
        for x in range(16 - width, 16 + width):
            px(d, x, y, glass)

    # Body shading (right side darker)
    for y in range(12, 28):
        for x in range(24, 30):
            px(d, x, y, glass_dark)

    # Glass highlight (left side)
    px(d, 17, 14, glass_light)
    px(d, 18, 15, highlight)
    px(d, 17, 16, highlight)

    # Bottom
    for x in range(16, 30):
        px(d, x, 27, glass_dark)
        px(d, x, 28, (120, 40, 40))

    return img

# ============================================================
# POITION SPEED - yellow potion with energy lines
# ============================================================
def potion_speed():
    """疾风护符 - yellow speed potion with energy."""
    img = new()
    d = ImageDraw.Draw(img)

    cork = (160, 110, 50)
    yellow = (240, 200, 60)
    yellow_dark = (200, 160, 40)
    yellow_light = (255, 240, 120)
    highlight = (255, 255, 200)

    # Cork
    for x in range(26, 38):
        px(d, x, 4, cork)
    for x in range(28, 36):
        px(d, x, 3, (120, 80, 35))

    # Bottle neck
    for y in range(6, 12):
        for x in range(28, 36):
            px(d, x, y, yellow)

    # Bottle body
    for y in range(12, 28):
        width = 14 if y < 20 else 14 - (y - 20)
        for x in range(16 - width, 16 + width):
            px(d, x, y, yellow)

    # Shading
    for y in range(12, 28):
        for x in range(24, 30):
            px(d, x, y, yellow_dark)

    # Energy lines (motion effect)
    for x, y in [(10, 16), (8, 18), (10, 20), (8, 22), (6, 18), (6, 20)]:
        px(d, x, y, yellow_light)
    for x, y in [(38, 16), (40, 18), (38, 20), (40, 22), (42, 18), (42, 20)]:
        px(d, x, y, yellow_light)

    # Highlight
    px(d, 17, 14, yellow_light)
    px(d, 18, 15, highlight)

    return img

icons = {
    "boots_speed": boots_speed,
    "ring_regen": ring_regen,
    "amulet_lifesteal": amulet_lifesteal,
    "potion_health": potion_health,
    "potion_speed": potion_speed,
}

for name, fn in icons.items():
    path = os.path.join(OUT, f"{name}.png")
    img = fn()
    img.save(path, "PNG")
    print(f"Created {path}")