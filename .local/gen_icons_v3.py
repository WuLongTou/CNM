"""Generate improved 64x64 pixel-art PNG icons."""
from PIL import Image, ImageDraw
import os
import math

OUT = "/Users/liyou/Work/ZVS/assets/sprites/generated"

def new():
    return Image.new("RGBA", (64, 64), (0, 0, 0, 0))

def px(d, x, y, color, s=2):
    d.rectangle([x*s, y*s, (x+1)*s-1, (y+1)*s-1], fill=color)

# ============================================================
# BOOTS SPEED - clear boot shape with speed lines
# ============================================================
def boots_speed():
    """敏捷靴 - brown boots with speed lines."""
    img = new()
    d = ImageDraw.Draw(img)
    brown = (120, 80, 40)
    brown_light = (160, 110, 60)
    brown_dark = (80, 50, 25)
    cyan = (80, 200, 220)
    cyan_light = (150, 240, 250)

    # Left boot
    for y in range(18, 30):
        for x in range(12, 24):
            px(d, x, y, brown)
    # Right boot
    for y in range(18, 30):
        for x in range(28, 40):
            px(d, x, y, brown)

    # Boot soles
    for x in range(12, 24):
        px(d, x, 29, brown_dark)
    for x in range(28, 40):
        px(d, x, 29, brown_dark)

    # Boot tops (lighter)
    for x in range(12, 24):
        px(d, x, 18, brown_light)
        px(d, x, 19, brown_light)
    for x in range(28, 40):
        px(d, x, 18, brown_light)
        px(d, x, 19, brown_light)

    # Ankle opening (dark)
    for x in range(14, 22):
        px(d, x, 22, brown_dark)
    for x in range(30, 38):
        px(d, x, 22, brown_dark)

    # Speed lines behind boots (motion effect)
    for x, y in [(6, 20), (7, 24), (8, 28), (6, 26)]:
        px(d, x, y, cyan)
    for x, y in [(42, 20), (43, 24), (44, 28), (42, 26)]:
        px(d, x, y, cyan)

    # Arrow indicators
    px(d, 4, 22, cyan_light)
    px(d, 5, 21, cyan_light)
    px(d, 5, 23, cyan_light)
    px(d, 46, 22, cyan_light)
    px(d, 45, 21, cyan_light)
    px(d, 45, 23, cyan_light)

    return img

# ============================================================
# GEM DAMAGE - proper diamond gem
# ============================================================
def gem_damage():
    """攻击宝石 - red diamond gem with facets."""
    img = new()
    d = ImageDraw.Draw(img)
    red_dark = (150, 30, 30)
    red = (200, 50, 50)
    red_light = (230, 80, 80)
    red_highlight = (255, 180, 180)
    white = (255, 220, 220)

    # Diamond shape - classic cut gem
    # Top half (triangle pointing up)
    for y in range(8, 18):
        offset = y - 8
        for x in range(16 - offset, 16 + offset + 1):
            px(d, x, y, red)
            if x < 16:
                px(d, x, y, red_light)
            else:
                px(d, x, y, red_dark)

    # Equator line
    for x in range(10, 22):
        px(d, x, 18, red_dark)

    # Bottom half (inverted triangle)
    for y in range(19, 30):
        height = y - 18
        offset = height
        for x in range(16 - offset, 16 + offset + 1):
            px(d, x, y, red)
            if x < 16:
                px(d, x, y, red_dark)
            else:
                px(d, x, y, (180, 40, 40))

    # Bottom point
    px(d, 16, 30, (120, 20, 20))

    # Top highlight
    px(d, 16, 8, red_highlight)
    px(d, 15, 9, red_highlight)
    px(d, 14, 10, red_light)

    # Center facet highlight
    px(d, 16, 16, white)
    px(d, 15, 15, red_highlight)

    return img

# ============================================================
# RING CRIT - proper ring with gem on top
# ============================================================
def ring_crit():
    """暴击指环 - golden ring with ruby on top."""
    img = new()
    d = ImageDraw.Draw(img)
    gold = (220, 180, 60)
    gold_dark = (180, 140, 40)
    gold_light = (255, 220, 120)
    ruby = (180, 40, 40)
    ruby_light = (220, 80, 80)
    ruby_highlight = (255, 150, 150)

    # Ring band (torus shape using circles)
    for angle in range(0, 360, 10):
        rad = math.radians(angle)
        r = 11
        cx, cy = 16, 20
        x = int(cx + r * math.cos(rad))
        y = int(cy + r * math.sin(rad))
        if 5 <= x <= 27 and 9 <= y <= 31:
            px(d, x, y, gold)

    # Ring inner (dark hole)
    for angle in range(0, 360, 15):
        rad = math.radians(angle)
        r = 7
        cx, cy = 16, 20
        x = int(cx + r * math.cos(rad))
        y = int(cy + r * math.sin(rad))
        if 9 <= x <= 23 and 13 <= y <= 27:
            px(d, x, y, (30, 20, 10))

    # Top gem (raised)
    for y in range(8, 16):
        width = 4 if y < 10 else 4 - (y - 10)
        for x in range(16 - width, 16 + width + 1):
            px(d, x, y, ruby)
    px(d, 16, 8, ruby_highlight)
    px(d, 15, 9, ruby_light)
    px(d, 14, 10, ruby)

    return img

# ============================================================
# HELMET IRON - clear helmet shape
# ============================================================
def helmet_iron():
    """铁盔 - grey iron helmet with face guard."""
    img = new()
    d = ImageDraw.Draw(img)
    grey = (140, 140, 150)
    grey_light = (180, 180, 190)
    grey_dark = (90, 90, 100)

    # Helmet dome
    for y in range(10, 22):
        width = 8 if y < 16 else 8 - (y - 16)
        for x in range(16 - width, 16 + width + 1):
            px(d, x, y, grey)

    # Rim at bottom
    for x in range(8, 24):
        px(d, x, 21, grey_dark)
        px(d, x, 22, grey_light)

    # Face guard (vertical bars)
    for y in range(18, 26):
        px(d, 13, y, grey_dark)
        px(d, 18, y, grey_dark)

    # Nose guard
    for y in range(20, 24):
        px(d, 15, y, grey_dark)
        px(d, 16, y, grey_dark)

    # Highlight on dome
    px(d, 12, 12, grey_light)
    px(d, 13, 11, grey_light)
    px(d, 14, 10, grey_light)

    return img

# ============================================================
# ARMOR LEATHER - chest armor shape
# ============================================================
def armor_leather():
    """皮甲 - brown leather chestplate with straps."""
    img = new()
    d = ImageDraw.Draw(img)
    brown = (140, 90, 50)
    brown_light = (170, 120, 70)
    brown_dark = (90, 60, 35)
    dark = (60, 40, 20)

    # Main chest
    for y in range(12, 28):
        width = 8 if y < 18 else 8 - (y - 18) // 2
        for x in range(16 - width, 16 + width + 1):
            px(d, x, y, brown)

    # Shoulder pads
    for x in range(6, 14):
        px(d, x, 12, brown)
        px(d, x, 13, brown_light)
    for x in range(18, 26):
        px(d, x, 12, brown)
        px(d, x, 13, brown_light)

    # Center strap
    for y in range(14, 26):
        px(d, 15, y, brown_dark)
        px(d, 16, y, brown_dark)

    # Buckle
    px(d, 15, 16, (200, 180, 80))
    px(d, 16, 16, (200, 180, 80))

    # Stitching details
    for y in range(14, 26, 3):
        px(d, 14, y, brown_light)
        px(d, 17, y, brown_light)

    return img

# ============================================================
# RING POISON - green ring with poison drop
# ============================================================
def ring_poison():
    """剧毒之戒 - green ring with dripping poison."""
    img = new()
    d = ImageDraw.Draw(img)
    green = (60, 180, 60)
    green_dark = (40, 120, 40)
    green_light = (120, 220, 120)
    poison = (150, 255, 80)
    poison_highlight = (200, 255, 150)

    # Ring band
    for angle in range(0, 360, 10):
        rad = math.radians(angle)
        r = 11
        cx, cy = 16, 18
        x = int(cx + r * math.cos(rad))
        y = int(cy + r * math.sin(rad))
        if 5 <= x <= 27 and 7 <= y <= 29:
            px(d, x, y, green)

    # Ring inner
    for angle in range(0, 360, 15):
        rad = math.radians(angle)
        r = 7
        cx, cy = 16, 18
        x = int(cx + r * math.cos(rad))
        y = int(cy + r * math.sin(rad))
        if 9 <= x <= 23 and 11 <= y <= 25:
            px(d, x, y, (20, 60, 20))

    # Poison drop hanging
    for y in range(26, 30):
        for x in range(15, 17):
            px(d, x, y, poison)
    px(d, 16, 30, poison)
    px(d, 15, 25, poison_highlight)
    px(d, 16, 25, poison_highlight)

    return img

# ============================================================
# RING REGEN - blue ring with plus sign
# ============================================================
def ring_regen():
    """再生戒指 - blue ring with health plus."""
    img = new()
    d = ImageDraw.Draw(img)
    blue = (60, 140, 220)
    blue_dark = (40, 90, 150)
    blue_light = (120, 180, 255)
    white = (240, 250, 255)

    # Ring band
    for angle in range(0, 360, 10):
        rad = math.radians(angle)
        r = 11
        cx, cy = 16, 18
        x = int(cx + r * math.cos(rad))
        y = int(cy + r * math.sin(rad))
        if 5 <= x <= 27 and 7 <= y <= 29:
            px(d, x, y, blue)

    # Ring inner
    for angle in range(0, 360, 15):
        rad = math.radians(angle)
        r = 7
        cx, cy = 16, 18
        x = int(cx + r * math.cos(rad))
        y = int(cy + r * math.sin(rad))
        if 9 <= x <= 23 and 11 <= y <= 25:
            px(d, x, y, (20, 50, 80))

    # Plus sign (health regen)
    for x in range(14, 18):
        px(d, x, 18, white)
    for y in range(16, 20):
        px(d, 16, y, white)

    return img

# ============================================================
# AMULET LIFESTEAL - heart amulet on chain
# ============================================================
def amulet_lifesteal():
    """吸血护符 - red heart on gold chain."""
    img = new()
    d = ImageDraw.Draw(img)
    gold = (220, 180, 60)
    gold_dark = (180, 140, 40)
    red = (200, 50, 50)
    red_light = (240, 100, 100)
    pink = (255, 180, 180)

    # Chain
    for y in range(4, 12):
        if y % 2 == 0:
            px(d, 16, y, gold)
        else:
            px(d, 15, y, gold)
            px(d, 17, y, gold)

    # Heart body
    for y in range(14, 22):
        if y < 18:
            # Top lobes
            for x in range(12, 20):
                px(d, x, y, red)
        elif y == 18:
            for x in range(12, 20):
                px(d, x, y, red)
        else:
            # Bottom point
            width = 22 - y
            for x in range(16 - width, 16 + width + 1):
                px(d, x, y, red)

    # Heart highlight
    px(d, 13, 15, red_light)
    px(d, 14, 16, pink)

    return img

icons = {
    "boots_speed": boots_speed,
    "gem_damage": gem_damage,
    "ring_crit": ring_crit,
    "helmet_iron": helmet_iron,
    "armor_leather": armor_leather,
    "ring_poison": ring_poison,
    "ring_regen": ring_regen,
    "amulet_lifesteal": amulet_lifesteal,
}

for name, fn in icons.items():
    path = os.path.join(OUT, f"{name}.png")
    img = fn()
    img.save(path, "PNG")
    print(f"Created {path}")