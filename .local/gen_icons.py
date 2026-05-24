"""Generate 64x64 transparent PNG icons for missing equipment cards."""
from PIL import Image, ImageDraw
import os

OUT = "/Users/liyou/Work/ZVS/assets/sprites/generated"

def new():
    return Image.new("RGBA", (64, 64), (0, 0, 0, 0))

def ghost_cloak():
    """幽灵斗篷 — translucent purple cloak shape."""
    img = new()
    d = ImageDraw.Draw(img)
    # Hood arc
    d.ellipse([18, 6, 46, 30], fill=(120, 60, 180, 200))
    # Cloak body
    d.polygon([(14, 26), (50, 26), (54, 56), (10, 56)], fill=(100, 50, 160, 160))
    # Inner shadow
    d.ellipse([22, 10, 42, 26], fill=(80, 30, 130, 220))
    # Eyes
    d.ellipse([24, 15, 29, 20], fill=(200, 200, 255, 240))
    d.ellipse([35, 15, 40, 20], fill=(200, 200, 255, 240))
    return img

def thorn_armor():
    """荆棘护甲 — armor with thorns."""
    img = new()
    d = ImageDraw.Draw(img)
    # Chest plate
    d.rectangle([16, 18, 48, 48], fill=(100, 100, 80, 220))
    d.rectangle([20, 22, 44, 44], fill=(130, 125, 100, 200))
    # Thorns (triangles) around edges
    for cx, cy in [(12, 20), (52, 20), (10, 34), (54, 34), (14, 48), (50, 48)]:
        d.polygon([(cx, cy-4), (cx+4, cy+3), (cx-4, cy+3)], fill=(60, 140, 40, 230))
    # Center gem
    d.ellipse([28, 28, 36, 36], fill=(200, 50, 50, 240))
    return img

def dodge_amulet():
    """闪避护符 — wing-shaped amulet."""
    img = new()
    d = ImageDraw.Draw(img)
    # String
    d.line([(32, 4), (32, 16)], fill=(200, 180, 100, 200), width=2)
    # Amulet base circle
    d.ellipse([20, 16, 44, 40], fill=(80, 180, 200, 220))
    d.ellipse([24, 20, 40, 36], fill=(120, 210, 230, 200))
    # Wind/wing lines
    d.arc([8, 12, 30, 30], 180, 0, fill=(180, 220, 240, 180), width=2)
    d.arc([34, 12, 56, 30], 180, 0, fill=(180, 220, 240, 180), width=2)
    # Center sparkle
    d.ellipse([29, 24, 35, 30], fill=(220, 240, 255, 250))
    return img

def pierce_gem():
    """穿甲弹 — sharp pointed gem."""
    img = new()
    d = ImageDraw.Draw(img)
    # Diamond/gem shape
    d.polygon([(32, 8), (52, 32), (32, 56), (12, 32)], fill=(80, 130, 220, 230))
    d.polygon([(32, 14), (47, 32), (32, 50), (17, 32)], fill=(100, 160, 240, 200))
    # Facet lines
    d.line([(32, 14), (32, 50)], fill=(140, 190, 255, 180), width=1)
    d.line([(17, 32), (47, 32)], fill=(140, 190, 255, 180), width=1)
    # Bright tip
    d.ellipse([28, 26, 36, 34], fill=(180, 210, 255, 240))
    return img

def bounce_gem():
    """弹跳之刃 — bouncing blade."""
    img = new()
    d = ImageDraw.Draw(img)
    # Blade shape
    d.polygon([(32, 6), (40, 30), (32, 54), (24, 30)], fill=(180, 140, 60, 230))
    d.polygon([(32, 12), (37, 30), (32, 48), (27, 30)], fill=(210, 170, 80, 200))
    # Bounce arcs (curved trail)
    d.arc([4, 10, 28, 28], 270, 90, fill=(255, 200, 80, 160), width=2)
    d.arc([36, 10, 60, 28], 270, 90, fill=(255, 200, 80, 160), width=2)
    d.arc([8, 38, 28, 54], 90, 270, fill=(255, 200, 80, 160), width=2)
    d.arc([36, 38, 56, 54], 90, 270, fill=(255, 200, 80, 160), width=2)
    # Center shine
    d.ellipse([28, 26, 36, 34], fill=(255, 230, 140, 250))
    return img

def destroy_breath():
    """毁灭之息 — fiery destruction emblem."""
    img = new()
    d = ImageDraw.Draw(img)
    # Core fire ball
    d.ellipse([18, 18, 46, 46], fill=(220, 80, 20, 230))
    d.ellipse([22, 22, 42, 42], fill=(240, 130, 30, 220))
    d.ellipse([26, 26, 38, 38], fill=(255, 200, 60, 240))
    # Flame tips
    d.polygon([(22, 20), (18, 6), (28, 16)], fill=(230, 100, 20, 210))
    d.polygon([(42, 20), (46, 6), (36, 16)], fill=(230, 100, 20, 210))
    d.polygon([(32, 18), (32, 4), (38, 14)], fill=(255, 160, 40, 200))
    # Bottom smoke
    d.ellipse([20, 42, 44, 58], fill=(100, 60, 30, 120))
    return img

icons = {
    "armor_ghost": ghost_cloak,
    "armor_thorn": thorn_armor,
    "amulet_dodge": dodge_amulet,
    "gem_pierce": pierce_gem,
    "gem_bounce": bounce_gem,
    "amulet_destroy": destroy_breath,
}

for name, fn in icons.items():
    path = os.path.join(OUT, f"{name}.png")
    img = fn()
    img.save(path, "PNG")
    print(f"Created {path} ({img.size[0]}x{img.size[1]}, mode={img.mode})")
