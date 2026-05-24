"""Generate 64x64 pixel-art PNG icons for equipment cards - improved version."""
from PIL import Image, ImageDraw
import os

OUT = "/Users/liyou/Work/ZVS/assets/sprites/generated"

def new():
    return Image.new("RGBA", (64, 64), (0, 0, 0, 0))

def draw_pixel(d, x, y, color, size=2):
    """Draw a pixel block at scaled coordinates."""
    d.rectangle([x*size, y*size, (x+1)*size-1, (y+1)*size-1], fill=color)

def add_highlight(d, points, color, size=2):
    """Add highlight pixels."""
    for x, y in points:
        draw_pixel(d, x, y, color, size)

def add_shadow(d, points, color, size=2):
    """Add shadow pixels."""
    for x, y in points:
        draw_pixel(d, x, y, color, size)

# ============================================================
# POtion HEALTH - red bottle with highlight
# ============================================================
def potion_health():
    """生命药水 - red potion bottle with cork."""
    img = new()
    d = ImageDraw.Draw(img)
    s = 2  # pixel scale

    # Cork (top)
    for x in range(26, 38):
        draw_pixel(d, x, 4, (139, 90, 43), s)
    for x in range(27, 37):
        draw_pixel(d, x, 3, (139, 90, 43), s)

    # Neck
    for y in range(5, 10):
        for x in range(28, 36):
            draw_pixel(d, x, y, (180, 60, 60), s)

    # Body - main red
    for y in range(10, 28):
        for x in range(22, 42):
            draw_pixel(d, x, y, (200, 50, 50), s)

    # Body - darker shade (right side shadow)
    for y in range(10, 28):
        for x in range(35, 42):
            draw_pixel(d, x, y, (150, 30, 30), s)

    # Body - lighter (left side highlight)
    for y in range(10, 28):
        draw_pixel(d, 24, y, (230, 100, 100), s)
        draw_pixel(d, 25, y, (220, 80, 80), s)

    # Highlight (shine)
    draw_pixel(d, 26, 14, (255, 200, 200), s)
    draw_pixel(d, 27, 15, (255, 200, 200), s)
    draw_pixel(d, 26, 15, (255, 180, 180), s)

    # Bottom curve
    for x in range(24, 40):
        draw_pixel(d, x, 28, (150, 30, 30), s)
        draw_pixel(d, x+1 if x < 31 else x-1, 29, (120, 20, 20), s)

    return img

# ============================================================
# POtion SPEED - yellow potion
# ============================================================
def potion_speed():
    """疾风护符 - yellow speed potion."""
    img = new()
    d = ImageDraw.Draw(img)
    s = 2

    # Cork
    for x in range(26, 38):
        draw_pixel(d, x, 4, (139, 90, 43), s)
    for x in range(27, 37):
        draw_pixel(d, x, 3, (139, 90, 43), s)

    # Neck
    for y in range(5, 10):
        for x in range(28, 36):
            draw_pixel(d, x, y, (200, 180, 60), s)

    # Body - yellow
    for y in range(10, 28):
        for x in range(22, 42):
            draw_pixel(d, x, y, (220, 200, 60), s)

    # Shadow
    for y in range(10, 28):
        for x in range(35, 42):
            draw_pixel(d, x, y, (180, 160, 30), s)

    # Highlight
    draw_pixel(d, 24, 14, (255, 240, 150), s)
    draw_pixel(d, 25, 15, (255, 240, 150), s)

    # Bottom
    for x in range(24, 40):
        draw_pixel(d, x, 28, (180, 160, 30), s)

    return img

# ============================================================
# BOOTS SPEED - simple boots
# ============================================================
def boots_speed():
    """敏捷靴 - speed boots with wing design."""
    img = new()
    d = ImageDraw.Draw(img)
    s = 2

    # Boot outline - dark brown
    boot_color = (101, 67, 33)
    highlight_color = (150, 100, 50)
    shadow_color = (60, 40, 20)

    # Left boot
    for y in range(16, 30):
        for x in range(10, 22):
            draw_pixel(d, x, y, boot_color, s)
    # Right boot
    for y in range(16, 30):
        for x in range(26, 38):
            draw_pixel(d, x, y, boot_color, s)

    # Boot sole (darker)
    for x in range(10, 22):
        draw_pixel(d, x, 29, shadow_color, s)
    for x in range(26, 38):
        draw_pixel(d, x, 29, shadow_color, s)

    # Highlight
    for y in range(17, 25):
        draw_pixel(d, 11, y, highlight_color, s)

    for y in range(17, 25):
        draw_pixel(d, 27, y, highlight_color, s)

    # Wing on side (cyan for speed)
    wing_color = (80, 200, 220)
    for x, y in [(8, 14), (7, 15), (8, 16), (7, 17), (6, 18)]:
        draw_pixel(d, x, y, wing_color, s)
    for x, y in [(38, 14), (39, 15), (38, 16), (39, 17), (40, 18)]:
        draw_pixel(d, x, y, wing_color, s)

    return img

# ============================================================
# GEM DAMAGE - red diamond gem
# ============================================================
def gem_damage():
    """攻击宝石 - red diamond gem."""
    img = new()
    d = ImageDraw.Draw(img)
    s = 2

    # Diamond shape
    # Top half (lighter red)
    for y in range(8, 16):
        offset = y - 8
        for x in range(16 + offset, 32 - offset):
            draw_pixel(d, x, y, (220, 80, 80), s)

    # Bottom half (darker red)
    for y in range(16, 24):
        offset = 16 - y + 8
        for x in range(offset, 32 - offset):
            draw_pixel(d, x, y, (180, 40, 40), s)

    # Highlight (white reflection)
    draw_pixel(d, 20, 12, (255, 200, 200), s)
    draw_pixel(d, 21, 13, (255, 220, 220), s)
    draw_pixel(d, 19, 13, (255, 180, 180), s)

    # Dark edge
    for x in range(16, 24):
        draw_pixel(d, x, 23, (120, 20, 20), s)
    for x in range(24, 32):
        draw_pixel(d, x, 23, (120, 20, 20), s)

    return img

# ============================================================
# GEM CRIT - orange gem
# ============================================================
def gem_crit():
    """狂战士护腕 - orange critical gem."""
    img = new()
    d = ImageDraw.Draw(img)
    s = 2

    # Diamond shape
    for y in range(8, 16):
        offset = y - 8
        for x in range(16 + offset, 32 - offset):
            draw_pixel(d, x, y, (230, 130, 40), s)

    for y in range(16, 24):
        offset = 16 - y + 8
        for x in range(offset, 32 - offset):
            draw_pixel(d, x, y, (200, 90, 20), s)

    # Highlight
    draw_pixel(d, 20, 12, (255, 220, 150), s)
    draw_pixel(d, 21, 13, (255, 200, 100), s)

    return img

# ============================================================
# RING CRIT - golden ring with gem
# ============================================================
def ring_crit():
    """暴击指环 - golden ring with red gem."""
    img = new()
    d = ImageDraw.Draw(img)
    s = 2

    # Ring band (gold)
    ring_color = (200, 170, 60)
    ring_dark = (160, 130, 40)
    gem_color = (200, 50, 50)

    # Outer ring
    for angle in range(0, 360, 15):
        import math
        rad = math.radians(angle)
        cx, cy = 16, 16
        r = 10
        x = int(cx + r * math.cos(rad))
        y = int(cy + r * math.sin(rad))
        if 8 <= x <= 24 and 8 <= y <= 24:
            draw_pixel(d, x, y, ring_color, s)

    # Inner ring (hole simulation - dark)
    for angle in range(0, 360, 20):
        import math
        rad = math.radians(angle)
        cx, cy = 16, 16
        r = 6
        x = int(cx + r * math.cos(rad))
        y = int(cy + r * math.sin(rad))
        if 10 <= x <= 22 and 10 <= y <= 22:
            draw_pixel(d, x, y, (40, 30, 20), s)

    # Center gem
    draw_pixel(d, 16, 16, gem_color, s)
    draw_pixel(d, 15, 15, (255, 150, 150), s)

    return img

# ============================================================
# RING POISON - green ring
# ============================================================
def ring_poison():
    """剧毒之戒 - green poison ring."""
    img = new()
    d = ImageDraw.Draw(img)
    s = 2

    ring_color = (60, 160, 60)
    ring_dark = (40, 100, 40)

    # Ring band
    for angle in range(0, 360, 15):
        import math
        rad = math.radians(angle)
        cx, cy = 16, 16
        r = 10
        x = int(cx + r * math.cos(rad))
        y = int(cy + r * math.sin(rad))
        if 8 <= x <= 24 and 8 <= y <= 24:
            draw_pixel(d, x, y, ring_color, s)

    # Inner
    for angle in range(0, 360, 20):
        import math
        rad = math.radians(angle)
        cx, cy = 16, 16
        r = 6
        x = int(cx + r * math.cos(rad))
        y = int(cy + r * math.sin(rad))
        if 10 <= x <= 22 and 10 <= y <= 22:
            draw_pixel(d, x, y, (20, 50, 20), s)

    # Poison drop in center
    draw_pixel(d, 16, 16, (80, 220, 80), s)
    draw_pixel(d, 15, 15, (150, 255, 150), s)

    return img

# ============================================================
# RING REGEN - blue ring
# ============================================================
def ring_regen():
    """再生戒指 - blue regeneration ring."""
    img = new()
    d = ImageDraw.Draw(img)
    s = 2

    ring_color = (80, 140, 200)
    ring_dark = (50, 90, 130)

    for angle in range(0, 360, 15):
        import math
        rad = math.radians(angle)
        cx, cy = 16, 16
        r = 10
        x = int(cx + r * math.cos(rad))
        y = int(cy + r * math.sin(rad))
        if 8 <= x <= 24 and 8 <= y <= 24:
            draw_pixel(d, x, y, ring_color, s)

    for angle in range(0, 360, 20):
        import math
        rad = math.radians(angle)
        cx, cy = 16, 16
        r = 6
        x = int(cx + r * math.cos(rad))
        y = int(cy + r * math.sin(rad))
        if 10 <= x <= 22 and 10 <= y <= 22:
            draw_pixel(d, x, y, (30, 50, 70), s)

    # Plus sign (health regen)
    for x in range(14, 19):
        draw_pixel(d, x, 16, (150, 220, 255), s)
    for y in range(14, 19):
        draw_pixel(d, 16, y, (150, 220, 255), s)

    return img

# ============================================================
# RING LIFESTEAL - purple ring
# ============================================================
def ring_lifesteal():
    """生命汲取 - purple lifesteal ring."""
    img = new()
    d = ImageDraw.Draw(img)
    s = 2

    ring_color = (140, 80, 180)
    ring_dark = (90, 50, 120)

    for angle in range(0, 360, 15):
        import math
        rad = math.radians(angle)
        cx, cy = 16, 16
        r = 10
        x = int(cx + r * math.cos(rad))
        y = int(cy + r * math.sin(rad))
        if 8 <= x <= 24 and 8 <= y <= 24:
            draw_pixel(d, x, y, ring_color, s)

    for angle in range(0, 360, 20):
        import math
        rad = math.radians(angle)
        cx, cy = 16, 16
        r = 6
        x = int(cx + r * math.cos(rad))
        y = int(cy + r * math.sin(rad))
        if 10 <= x <= 22 and 10 <= y <= 22:
            draw_pixel(d, x, y, (50, 30, 60), s)

    # Heart drop
    draw_pixel(d, 16, 15, (220, 100, 150), s)
    draw_pixel(d, 15, 16, (220, 100, 150), s)
    draw_pixel(d, 17, 16, (220, 100, 150), s)
    draw_pixel(d, 16, 17, (220, 100, 150), s)
    draw_pixel(d, 16, 16, (255, 150, 180), s)

    return img

# ============================================================
# AMULET LIFESTEAL - red amulet
# ============================================================
def amulet_lifesteal():
    """吸血护符 - red heart amulet."""
    img = new()
    d = ImageDraw.Draw(img)
    s = 2

    # Chain/string
    for y in range(4, 12):
        draw_pixel(d, 16, y, (180, 150, 50), s)

    # Amulet base (gold)
    for x in range(12, 20):
        draw_pixel(d, x, 12, (200, 180, 60), s)

    # Heart shape
    heart_color = (200, 50, 50)
    heart_highlight = (255, 150, 150)

    # Heart - top lobes
    for x in range(12, 16):
        draw_pixel(d, x, 16, heart_color, s)
        draw_pixel(d, x, 17, heart_color, s)
    for x in range(16, 20):
        draw_pixel(d, x, 16, heart_color, s)
        draw_pixel(d, x, 17, heart_color, s)

    # Heart - bottom point
    for x in range(13, 19):
        draw_pixel(d, x, 18, heart_color, s)
    draw_pixel(d, 14, 19, heart_color, s)
    draw_pixel(d, 15, 20, heart_color, s)
    draw_pixel(d, 16, 21, heart_color, s)
    draw_pixel(d, 17, 19, heart_color, s)

    # Highlight
    draw_pixel(d, 13, 16, heart_highlight, s)
    draw_pixel(d, 18, 16, heart_highlight, s)

    return img

# ============================================================
# BADGE FROST - blue badge
# ============================================================
def badge_frost():
    """寒霜徽章 - blue ice badge."""
    img = new()
    d = ImageDraw.Draw(img)
    s = 2

    # Badge base (pentagon shield shape)
    base_color = (60, 120, 180)
    highlight_color = (120, 180, 230)
    shadow_color = (40, 80, 140)

    # Shield shape
    for y in range(12, 24):
        width = 12 if y < 18 else 12 - (y - 18)
        for x in range(16 - width, 16 + width):
            draw_pixel(d, x, y, base_color, s)

    # Bottom point
    for x in range(12, 20):
        draw_pixel(d, x, 24, shadow_color, s)

    # Highlight (top)
    for x in range(12, 16):
        draw_pixel(d, x, 13, highlight_color, s)

    # Snowflake center
    draw_pixel(d, 16, 16, (220, 240, 255), s)
    for x in range(14, 18):
        draw_pixel(d, x, 16, (200, 220, 255), s)
    for y in range(14, 18):
        draw_pixel(d, 16, y, (200, 220, 255), s)

    return img

# ============================================================
# BADGE HUNTER - green badge
# ============================================================
def badge_hunter():
    """猎人印记 - green crosshair badge."""
    img = new()
    d = ImageDraw.Draw(img)
    s = 2

    base_color = (60, 160, 80)
    highlight_color = (100, 200, 120)
    shadow_color = (40, 100, 50)

    # Shield shape
    for y in range(12, 24):
        width = 12 if y < 18 else 12 - (y - 18)
        for x in range(16 - width, 16 + width):
            draw_pixel(d, x, y, base_color, s)

    for x in range(12, 20):
        draw_pixel(d, x, 24, shadow_color, s)

    # Highlight
    for x in range(12, 16):
        draw_pixel(d, x, 13, highlight_color, s)

    # Crosshair center
    draw_pixel(d, 16, 16, (220, 255, 200), s)
    for x in range(12, 20):
        draw_pixel(d, x, 16, (180, 255, 150), s)
    for y in range(12, 20):
        draw_pixel(d, 16, y, (180, 255, 150), s)

    # Outer ring
    for angle in range(0, 360, 30):
        import math
        rad = math.radians(angle)
        cx, cy = 16, 16
        r = 8
        x = int(cx + r * math.cos(rad))
        y = int(cy + r * math.sin(rad))
        draw_pixel(d, x, y, (100, 200, 100), s)

    return img

# ============================================================
# SHIELD FROST - blue shield
# ============================================================
def shield_frost():
    """冰霜护盾 - blue shield with cross."""
    img = new()
    d = ImageDraw.Draw(img)
    s = 2

    shield_color = (50, 100, 180)
    highlight_color = (100, 150, 220)
    shadow_color = (30, 60, 120)

    # Shield body (classic shape)
    for y in range(10, 26):
        width = 10 if y < 18 else 10 - (y - 18) * 2 // 8 * 3
        for x in range(16 - width, 16 + width):
            draw_pixel(d, x, y, shield_color, s)

    # Highlight (top left)
    for x in range(10, 14):
        draw_pixel(d, x, 10, highlight_color, s)
    for y in range(10, 14):
        draw_pixel(d, 10, y, highlight_color, s)

    # Cross symbol
    cross_color = (220, 240, 255)
    for x in range(14, 18):
        draw_pixel(d, x, 15, cross_color, s)
        draw_pixel(d, x, 17, cross_color, s)
    for y in range(14, 18):
        draw_pixel(d, 14, y, cross_color, s)
        draw_pixel(d, 16, y, cross_color, s)

    return img

# ============================================================
# SHIELD IRON - grey shield
# ============================================================
def shield_iron():
    """钢铁意志 - grey steel shield."""
    img = new()
    d = ImageDraw.Draw(img)
    s = 2

    shield_color = (120, 120, 130)
    highlight_color = (180, 180, 190)
    shadow_color = (80, 80, 90)

    # Shield body
    for y in range(10, 26):
        width = 10 if y < 18 else 10 - (y - 18) * 2 // 8 * 3
        for x in range(16 - width, 16 + width):
            draw_pixel(d, x, y, shield_color, s)

    # Highlight
    for x in range(10, 14):
        draw_pixel(d, x, 10, highlight_color, s)
    for y in range(10, 14):
        draw_pixel(d, 10, y, highlight_color, s)

    # Iron cross
    for x in range(13, 19):
        draw_pixel(d, x, 16, (220, 220, 230), s)
    for y in range(13, 19):
        draw_pixel(d, 16, y, (220, 220, 230), s)

    # Rivets
    for x, y in [(8, 12), (24, 12), (8, 24), (24, 24)]:
        draw_pixel(d, x, y, (100, 100, 110), s)

    return img

# ============================================================
# HELMET IRON - simple helmet
# ============================================================
def helmet_iron():
    """铁盔 - iron helmet."""
    img = new()
    d = ImageDraw.Draw(img)
    s = 2

    helmet_color = (130, 130, 140)
    highlight_color = (180, 180, 190)
    shadow_color = (80, 80, 90)

    # Helmet dome
    for y in range(8, 20):
        width = 8 if y < 14 else 8 - (y - 14)
        for x in range(16 - width, 16 + width):
            draw_pixel(d, x, y, helmet_color, s)

    # Rim
    for x in range(8, 24):
        draw_pixel(d, x, 19, shadow_color, s)
        draw_pixel(d, x, 20, (100, 100, 110), s)

    # Highlight
    for x in range(12, 16):
        draw_pixel(d, x, 9, highlight_color, s)
    for y in range(9, 13):
        draw_pixel(d, 12, y, highlight_color, s)

    # Face guard
    for y in range(18, 24):
        draw_pixel(d, 14, y, shadow_color, s)
        draw_pixel(d, 15, y, shadow_color, s)
        draw_pixel(d, 17, y, shadow_color, s)
        draw_pixel(d, 18, y, shadow_color, s)

    return img

# ============================================================
# ARMOR LEATHER - brown chestplate
# ============================================================
def armor_leather():
    """皮甲 - brown leather armor."""
    img = new()
    d = ImageDraw.Draw(img)
    s = 2

    armor_color = (130, 90, 50)
    highlight_color = (170, 130, 80)
    shadow_color = (80, 50, 30)

    # Chestplate shape
    for y in range(10, 26):
        width = 10 if y < 16 else 10 - (y - 16) // 3
        for x in range(16 - width, 16 + width):
            draw_pixel(d, x, y, armor_color, s)

    # Shoulders (wider)
    for x in range(6, 12):
        draw_pixel(d, x, 10, armor_color, s)
        draw_pixel(d, x, 11, armor_color, s)
    for x in range(20, 26):
        draw_pixel(d, x, 10, armor_color, s)
        draw_pixel(d, x, 11, armor_color, s)

    # Highlight
    for x in range(14, 18):
        draw_pixel(d, x, 12, highlight_color, s)

    # Stitching detail
    for y in range(14, 24):
        draw_pixel(d, 16, y, shadow_color, s)

    # Edge darkening
    for x in range(6, 10):
        draw_pixel(d, x, 10, shadow_color, s)
    for x in range(22, 26):
        draw_pixel(d, x, 10, shadow_color, s)

    return img

# ============================================================
# ARMOR IRON - grey armor
# ============================================================
def armor_iron():
    """钢甲 - grey steel armor."""
    img = new()
    d = ImageDraw.Draw(img)
    s = 2

    armor_color = (130, 130, 140)
    highlight_color = (180, 180, 190)
    shadow_color = (80, 80, 90)

    # Chestplate
    for y in range(10, 26):
        width = 10 if y < 16 else 10 - (y - 16) // 3
        for x in range(16 - width, 16 + width):
            draw_pixel(d, x, y, armor_color, s)

    # Shoulders
    for x in range(6, 12):
        draw_pixel(d, x, 10, armor_color, s)
        draw_pixel(d, x, 11, armor_color, s)
    for x in range(20, 26):
        draw_pixel(d, x, 10, armor_color, s)
        draw_pixel(d, x, 11, armor_color, s)

    # Highlight
    for x in range(14, 18):
        draw_pixel(d, x, 12, highlight_color, s)

    # Center line
    for y in range(14, 24):
        draw_pixel(d, 16, y, shadow_color, s)

    # Rivets
    for x, y in [(8, 12), (24, 12), (8, 22), (24, 22)]:
        draw_pixel(d, x, y, (220, 220, 230), s)

    return img

icons = {
    "potion_health": potion_health,
    "potion_speed": potion_speed,
    "boots_speed": boots_speed,
    "gem_damage": gem_damage,
    "gem_crit": gem_crit,
    "ring_crit": ring_crit,
    "ring_poison": ring_poison,
    "ring_regen": ring_regen,
    "ring_lifesteal": ring_lifesteal,
    "amulet_lifesteal": amulet_lifesteal,
    "badge_frost": badge_frost,
    "badge_hunter": badge_hunter,
    "shield_frost": shield_frost,
    "shield_iron": shield_iron,
    "helmet_iron": helmet_iron,
    "armor_leather": armor_leather,
    "armor_iron": armor_iron,
}

for name, fn in icons.items():
    path = os.path.join(OUT, f"{name}.png")
    img = fn()
    img.save(path, "PNG")
    print(f"Created {path} ({img.size[0]}x{img.size[1]}, mode={img.mode})")