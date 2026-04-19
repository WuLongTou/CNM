#!/bin/bash
# ============================================================
# MiniMax 资产批量生成脚本
# 僵尸大战 (Zombie Wars) - C# Godot 4.6 项目
# ============================================================
# 使用方法:
#   1. 确保 MiniMax MCP 服务已启动
#   2. cd 到项目根目录执行: bash generate_assets.sh
#   3. 图片将保存到 assets/sprites/
#   4. 音频将保存到 assets/audio/sfx/
#   5. 音乐将保存到 assets/audio/music/
# ============================================================

OUTPUT_BASE="/Users/liyou/Work/ZVS/assets"
mkdir -p "$OUTPUT_BASE/sprites/player"
mkdir -p "$OUTPUT_BASE/sprites/zombies"
mkdir -p "$OUTPUT_BASE/sprites/ui"
mkdir -p "$OUTPUT_BASE/sprites/backgrounds"
mkdir -p "$OUTPUT_BASE/audio/sfx"
mkdir -p "$OUTPUT_BASE/audio/music"

echo "=========================================="
echo "僵尸大战 - 资产批量生成"
echo "=========================================="

# -----------------------
# 1. 玩家角色精灵
# -----------------------
echo "[1/7] 生成玩家角色精灵..."

minimax_gen_image \
  --prompt "pixel art warrior survivor character facing right, dark combat gear with red accents, heroic pose, white background isolation, 128x128px game sprite" \
  --aspect_ratio "1:1" \
  --output "$OUTPUT_BASE/sprites/player/player_warrior.png"

minimax_gen_image \
  --prompt "pixel art mage wizard character, dark robes with blue magical glow, staff weapon, heroic pose, white background, 128x128px game sprite" \
  --aspect_ratio "1:1" \
  --output "$OUTPUT_BASE/sprites/player/player_mage.png"

minimax_gen_image \
  --prompt "pixel art ranger archer character, green hood and cloak, bow weapon, heroic pose, white background, 128x128px game sprite" \
  --aspect_ratio "1:1" \
  --output "$OUTPUT_BASE/sprites/player/player_ranger.png"

# -----------------------
# 2. 僵尸类型精灵 (8种)
# -----------------------
echo "[2/7] 生成僵尸精灵..."

# 基础僵尸
minimax_gen_image \
  --prompt "pixel art zombie shambling, green decayed skin torn clothes, glowing red eyes, white background, 64x64px" \
  --aspect_ratio "1:1" \
  --output "$OUTPUT_BASE/sprites/zombies/zombie_basic.png"

# 快速僵尸
minimax_gen_image \
  --prompt "pixel art fast zombie runner, thin purple glowing, speed lines, white background, 64x64px" \
  --aspect_ratio "1:1" \
  --output "$OUTPUT_BASE/sprites/zombies/zombie_fast.png"

# 坦克僵尸
minimax_gen_image \
  --prompt "pixel art tank zombie hulking brute, armor plates, massive build, white background, 64x64px" \
  --aspect_ratio "1:1" \
  --output "$OUTPUT_BASE/sprites/zombies/zombie_tank.png"

# 爆炸僵尸
minimax_gen_image \
  --prompt "pixel art exploding zombie, bloated green body, volatile yellow glowing markings, white background, 64x64px" \
  --aspect_ratio "1:1" \
  --output "$OUTPUT_BASE/sprites/zombies/zombie_exploder.png"

# 隐身僵尸
minimax_gen_image \
  --prompt "pixel art invisible ghost zombie, semi-transparent ghostly silhouette, glowing purple eyes, white background, 64x64px" \
  --aspect_ratio "1:1" \
  --output "$OUTPUT_BASE/sprites/zombies/zombie_invisible.png"

# 喷毒僵尸
minimax_gen_image \
  --prompt "pixel art spitter zombie, hunched poison zombie, glowing green mouth acid spit, white background, 64x64px" \
  --aspect_ratio "1:1" \
  --output "$OUTPUT_BASE/sprites/zombies/zombie_spitter.png"

# 治疗僵尸
minimax_gen_image \
  --prompt "pixel art healer zombie, pale white, glowing cyan aura, medical cross symbol, white background, 64x64px" \
  --aspect_ratio "1:1" \
  --output "$OUTPUT_BASE/sprites/zombies/zombie_healer.png"

# 召唤师僵尸
minimax_gen_image \
  --prompt "pixel art summoner necromancer zombie, dark robes, purple soul-fire, skeletal hands, white background, 64x64px" \
  --aspect_ratio "1:1" \
  --output "$OUTPUT_BASE/sprites/zombies/zombie_summoner.png"

# -----------------------
# 3. 精英/Boss 僵尸
# -----------------------
echo "[3/7] 生成精英和Boss僵尸..."

minimax_gen_image \
  --prompt "pixel art elite zombie with golden glowing aura, crown symbol, powerful build, white background, 64x64px" \
  --aspect_ratio "1:1" \
  --output "$OUTPUT_BASE/sprites/zombies/zombie_elite.png"

minimax_gen_image \
  --prompt "pixel art boss zombie massive hulking king zombie, crown spikes, red glowing eyes, enormous size, white background, 128x128px" \
  --aspect_ratio "1:1" \
  --output "$OUTPUT_BASE/sprites/zombies/boss_zombie.png"

# -----------------------
# 4. UI 元素精灵
# -----------------------
echo "[4/7] 生成UI元素..."

minimax_gen_image \
  --prompt "game UI card frame pixel art, dark background, golden border ornate, weapon card style, white background, 64x64px" \
  --aspect_ratio "1:1" \
  --output "$OUTPUT_BASE/sprites/ui/card_frame.png"

minimax_gen_image \
  --prompt "pixel art weapon icons set: sword axe bow wand dagger, 32x32 each, white background, game icons" \
  --aspect_ratio "1:1" \
  --output "$OUTPUT_BASE/sprites/ui/weapon_icons.png"

minimax_gen_image \
  --prompt "pixel art status effect icons: shield flame ice poison skull heart, 32x32 each, white background, game icons" \
  --aspect_ratio "1:1" \
  --output "$OUTPUT_BASE/sprites/ui/status_icons.png"

minimax_gen_image \
  --prompt "pixel art HUD elements: health heart potion, mana crystal, coin gold, 32x32 each, white background, game UI" \
  --aspect_ratio "1:1" \
  --output "$OUTPUT_BASE/sprites/ui/hud_elements.png"

# -----------------------
# 5. 背景纹理
# -----------------------
echo "[5/7] 生成背景纹理..."

minimax_gen_image \
  --prompt "dark dungeon stone floor texture seamless tileable, 128x128px, top-down game view, dark gray brown tones, subtle cracks" \
  --aspect_ratio "1:1" \
  --output "$OUTPUT_BASE/sprites/backgrounds/ground_stone.png"

minimax_gen_image \
  --prompt "dark horror graveyard ground texture, muddy dark earth, grass tufts, top-down game view, 128x128px" \
  --aspect_ratio "1:1" \
  --output "$OUTPUT_BASE/sprites/backgrounds/ground_graveyard.png"

minimax_gen_image \
  --prompt "dark night sky with blood moon atmosphere, horror game background, purple red gradient, 128x128px" \
  --aspect_ratio "1:1" \
  --output "$OUTPUT_BASE/sprites/backgrounds/atmosphere_bloodmoon.png"

# -----------------------
# 6. 音效生成
# -----------------------
echo "[6/7] 生成音效..."

# 近战攻击
minimax_gen_audio \
  --text "whoosh slash swoosh combat sword hit" \
  --output "$OUTPUT_BASE/audio/sfx/attack_melee.mp3"

# 远程射击
minimax_gen_audio \
  --text "arrow projectile shoot fire whoosh" \
  --output "$OUTPUT_BASE/audio/sfx/attack_ranged.mp3"

# 魔法攻击
minimax_gen_audio \
  --text "magic spell cast arcane whoosh shimmer" \
  --output "$OUTPUT_BASE/audio/sfx/attack_magic.mp3"

# 僵尸死亡
minimax_gen_audio \
  --text "zombie groan moan dying death sound" \
  --output "$OUTPUT_BASE/audio/sfx/zombie_death.mp3"

# 僵尸攻击
minimax_gen_audio \
  --text "zombie growl attack snarl" \
  --output "$OUTPUT_BASE/audio/sfx/zombie_attack.mp3"

# 爆炸
minimax_gen_audio \
  --text "explosion boom blast fire impact" \
  --output "$OUTPUT_BASE/audio/sfx/explosion.mp3"

# 玩家受伤
minimax_gen_audio \
  --text "player hurt damage grunt pain" \
  --output "$OUTPUT_BASE/audio/sfx/player_hurt.mp3"

# 捡取/获得
minimax_gen_audio \
  --text "coin collect pickup reward jingle" \
  --output "$OUTPUT_BASE/audio/sfx/item_pickup.mp3"

# 游戏结束
minimax_gen_audio \
  --text "game over death sad dramatic tone" \
  --output "$OUTPUT_BASE/audio/sfx/game_over.mp3"

# 武器选择
minimax_gen_audio \
  --text "card draw select whoosh magic sparkle" \
  --output "$OUTPUT_BASE/audio/sfx/weapon_select.mp3"

# Boss来袭
minimax_gen_audio \
  --text "boss roar thunder dramatic warning alert" \
  --output "$OUTPUT_BASE/audio/sfx/boss_warning.mp3"

# 闪电
minimax_gen_audio \
  --text "thunder lightning strike crack" \
  --output "$OUTPUT_BASE/audio/sfx/lightning.mp3"

# -----------------------
# 7. 背景音乐
# -----------------------
echo "[7/7] 生成背景音乐..."

# 战斗音乐 - 第1-4回合
minimax_gen_music \
  --prompt "intense action game music, survival horror, dark atmosphere, suspenseful battle theme, medium tempo drums and strings" \
  --lyrics "la la la" \
  --output "$OUTPUT_BASE/audio/music/battle_early.mp3"

# 激烈战斗 - 第5-9回合
minimax_gen_music \
  --prompt "epic intense battle music, zombie survival game, fast drums, dark orchestra, heroic tension, fast tempo" \
  --lyrics "la la la" \
  --output "$OUTPUT_BASE/audio/music/battle_intense.mp3"

# Boss战音乐
minimax_gen_music \
  --prompt "boss battle music, epic dramatic boss fight theme, dark orchestra, heavy drums, menacing atmosphere, slow powerful build" \
  --lyrics "la la la" \
  --output "$OUTPUT_BASE/audio/music/boss_battle.mp3"

# 后期绝望音乐
minimax_gen_music \
  --prompt "desperate survival horror music, late game intensity, dark desperate atmosphere, fast tempo drums, eerie strings, high tension" \
  --lyrics "la la la" \
  --output "$OUTPUT_BASE/audio/music/battle_late.mp3"

echo "=========================================="
echo "所有资产生成完成!"
echo "=========================================="
