// 资产同步与预处理：从仓库根 assets/ 生成 web/public/assets/。
// - 直拷：ground 大图、背景 JPG、音频 WAV、小图标 PNG
// - 剥底：weapons/effects 等白底 JPG → RGBA PNG（规则直译 TextureUtils._StripWhiteBackground）
// - 切帧：玩家 9 张 + 敌人 14 张 spritesheet → 预切 96×96 帧 + PixiJS TexturePacker atlas JSON
// 用法：npm run assets:sync（源：web/assets-src，生成：web/public/assets）
import { createRequire } from "node:module";
import path from "node:path";
import fs from "node:fs";
import { fileURLToPath } from "node:url";
import {
  stripBackground, findSpriteRegions, trimTransparent, isMostlyTransparent,
  takeFramesEvenly, crop, NORMAL_SPRITE_SIZE,
} from "./lib/imageops.mjs";

const require = createRequire(import.meta.url);
const sharp = require("sharp");

const WEB_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const ASSETS_SRC = path.join(WEB_ROOT, "assets-src"); // 资产已迁入 web 内（源文件名全部中性）
const SRC = ASSETS_SRC;
const DST = path.join(WEB_ROOT, "public", "assets");

/** @returns {Promise<{data: Uint8ClampedArray, width: number, height: number}>} */
async function loadRaw(file) {
  const { data, info } = await sharp(file).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  return { data: new Uint8ClampedArray(data), width: info.width, height: info.height };
}

async function saveRaw(img, file) {
  await sharp(img.data, { raw: { width: img.width, height: img.height, channels: 4 } })
    .png().toFile(file);
}

/** 等比缩放 fit 到 size×size 内并居中（直译 _ResizeToFit，Nearest 插值）。 */
async function fitToCanvas(img, size = NORMAL_SPRITE_SIZE) {
  const buf = await sharp(img.data, { raw: { width: img.width, height: img.height, channels: 4 } })
    .resize(size, size, { fit: "contain", kernel: "nearest", background: { r: 0, g: 0, b: 0, alpha: 0 } })
    .raw().toBuffer();
  return { data: new Uint8ClampedArray(buf), width: size, height: size };
}

/** 瘦长帧强制拉伸到 targetW×96 再水平居中（直译 LoadAndStretchSplitFrames）。 */
async function stretchFrame(img, targetW) {
  const mid = await sharp(img.data, { raw: { width: img.width, height: img.height, channels: 4 } })
    .resize(targetW, NORMAL_SPRITE_SIZE, { fit: "fill", kernel: "nearest" })
    .raw().toBuffer();
  const left = Math.floor((NORMAL_SPRITE_SIZE - targetW) / 2);
  const right = NORMAL_SPRITE_SIZE - targetW - left;
  const buf = await sharp(mid, { raw: { width: targetW, height: NORMAL_SPRITE_SIZE, channels: 4 } })
    .extend({ left, right, top: 0, bottom: 0, background: { r: 0, g: 0, b: 0, alpha: 0 } })
    .raw().toBuffer();
  return { data: new Uint8ClampedArray(buf), width: NORMAL_SPRITE_SIZE, height: NORMAL_SPRITE_SIZE };
}

function flopRaw(img) {
  // 逐行水平翻转（对应 TextureUtils.FlipHorizontal）
  const out = { data: new Uint8ClampedArray(img.data.length), width: img.width, height: img.height };
  const { width: w, height: h } = img;
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const si = (y * w + (w - 1 - x)) * 4;
      const di = (y * w + x) * 4;
      out.data[di] = img.data[si];
      out.data[di + 1] = img.data[si + 1];
      out.data[di + 2] = img.data[si + 2];
      out.data[di + 3] = img.data[si + 3];
    }
  }
  return out;
}

/**
 * 投影分割切帧（直译 LoadAndSplitSpriteSheet）：
 * 全图剥底 → 投影分割 → 每 region 剥底+trim+fit96。无 region 时整图兜底。
 */
async function splitFrames(file) {
  let img = await loadRaw(file);
  img = stripBackground(img);
  const regions = findSpriteRegions(img);
  const frames = [];
  if (regions.length <= 1) {
    // 单区域/空：整图 trim + fit（与 C# 一致的单帧兜底）
    let single = trimTransparent(img);
    if (!isMostlyTransparent(single)) frames.push(await fitToCanvas(single));
  } else {
    for (const r of regions) {
      let region = crop(img, r.x, r.y, r.w, r.h);
      region = stripBackground(region);
      region = trimTransparent(region);
      frames.push(await fitToCanvas(region));
    }
  }
  return frames;
}

/** 瘦长全身像切帧（直译 LoadAndStretchSplitFrames，widthRatio 0.45）。 */
async function stretchFrames(file, widthRatio = 0.45) {
  let img = await loadRaw(file);
  img = stripBackground(img);
  const regions = findSpriteRegions(img);
  const targetW = Math.max(8, Math.floor(NORMAL_SPRITE_SIZE * widthRatio));
  const frames = [];
  for (const r of regions) {
    let region = crop(img, r.x, r.y, r.w, r.h);
    if (isMostlyTransparent(region)) continue;
    region = stripBackground(region);
    region = trimTransparent(region);
    frames.push(await stretchFrame(region, targetW));
  }
  if (frames.length === 0) {
    frames.push(await stretchFrame(trimTransparent(img), targetW));
  }
  return frames;
}

/** 网格切帧（直译 LoadSpriteSheetGridFrames）。 */
async function gridFrames(file, cols, rows) {
  const img = stripBackground(await loadRaw(file));
  const cw = Math.floor(img.width / cols);
  const ch = Math.floor(img.height / rows);
  const frames = [];
  for (let gy = 0; gy < rows; gy++) {
    for (let gx = 0; gx < cols; gx++) {
      let region = crop(img, gx * cw, gy * ch, cw, ch);
      region = stripBackground(region);
      region = trimTransparent(region);
      if (!isMostlyTransparent(region)) frames.push(await fitToCanvas(region));
    }
  }
  return frames;
}

/** 把帧序列打包成 grid atlas PNG + TexturePacker JSON（PixiJS Assets 可直接加载）。 */
async function writeAtlas(frames, baseName, outDir) {
  const n = frames.length;
  const cols = Math.max(1, Math.ceil(Math.sqrt(n)));
  const rows = Math.ceil(n / cols);
  const atlas = { data: new Uint8ClampedArray(cols * rows * NORMAL_SPRITE_SIZE ** 2 * 4), width: cols * NORMAL_SPRITE_SIZE, height: rows * NORMAL_SPRITE_SIZE };
  /** @type {any[]} */
  const jsonFrames = [];
  frames.forEach((f, i) => {
    const cx = (i % cols) * NORMAL_SPRITE_SIZE;
    const cy = Math.floor(i / cols) * NORMAL_SPRITE_SIZE;
    for (let y = 0; y < NORMAL_SPRITE_SIZE; y++) {
      const srcRow = y * NORMAL_SPRITE_SIZE * 4;
      atlas.data.set(f.data.subarray(srcRow, srcRow + NORMAL_SPRITE_SIZE * 4), ((cy + y) * atlas.width + cx) * 4);
    }
    jsonFrames.push({
      filename: `${baseName}_${i}`,
      frame: { x: cx, y: cy, w: NORMAL_SPRITE_SIZE, h: NORMAL_SPRITE_SIZE },
      rotated: false,
      trimmed: false,
      spriteSourceSize: { x: 0, y: 0, w: NORMAL_SPRITE_SIZE, h: NORMAL_SPRITE_SIZE },
      sourceSize: { w: NORMAL_SPRITE_SIZE, h: NORMAL_SPRITE_SIZE },
    });
  });
  await saveRaw(atlas, path.join(outDir, `${baseName}.png`));
  fs.writeFileSync(path.join(outDir, `${baseName}.json`), JSON.stringify({
    frames: jsonFrames,
    meta: { app: "zhanyaolu-web", image: `${baseName}.png`, size: { w: atlas.width, h: atlas.height }, scale: "1" },
  }));
  return { name: baseName, frames: n };
}

async function copyDir(srcSub, dstSub) {
  const src = path.join(SRC, srcSub);
  if (!fs.existsSync(src)) return 0;
  const dst = path.join(DST, dstSub);
  fs.mkdirSync(dst, { recursive: true });
  let count = 0;
  for (const f of fs.readdirSync(src)) {
    if (f.endsWith(".import") || f.startsWith(".")) continue;
    fs.copyFileSync(path.join(src, f), path.join(dst, f));
    count++;
  }
  return count;
}

/** 白底 JPG/PNG → 剥底转 RGBA PNG。 */
async function stripDir(srcSub, dstSub) {
  const src = path.join(SRC, srcSub);
  if (!fs.existsSync(src)) return 0;
  const dst = path.join(DST, dstSub);
  fs.mkdirSync(dst, { recursive: true });
  let count = 0;
  for (const f of fs.readdirSync(src)) {
    if (!/\.(png|jpg|jpeg)$/i.test(f)) continue;
    const img = stripBackground(await loadRaw(path.join(src, f)));
    await saveRaw(img, path.join(dst, f.replace(/\.jpe?g$/i, ".png")));
    count++;
  }
  return count;
}

async function main() {
  fs.rmSync(DST, { recursive: true, force: true });
  fs.mkdirSync(DST, { recursive: true });
  const report = {};

  report.audio = await copyDir("audio", "audio");
  report.ground = await copyDir(path.join("sprites", "ground"), "sprites/ground");
  report.backgrounds = await copyDir(path.join("sprites", "backgrounds"), "sprites/backgrounds");
  report.equipment = await copyDir(path.join("sprites", "equipment"), "sprites/equipment");
  report.pickups = await copyDir(path.join("sprites", "pickups"), "sprites/pickups");
  report.buildings = await copyDir(path.join("sprites", "buildings"), "sprites/buildings");
  report.weapons = await stripDir(path.join("sprites", "weapons"), "sprites/weapons");
  report.effects = await stripDir(path.join("sprites", "effects"), "sprites/effects");

  // 玩家：walk/attack 投影分割取 8/6 帧，idle 2×2 网格取 4 帧；全部翻转归一化朝右
  const atlasDir = path.join(DST, "sprites", "player");
  fs.mkdirSync(atlasDir, { recursive: true });
  const playerDir = path.join(SRC, "sprites", "player", "sheets");
  /** @type {any[]} */
  const atlases = [];
  for (const who of ["warrior", "ranger", "mage"]) {
    const walk = takeFramesEvenly(await splitFrames(path.join(playerDir, `${who}_walk.png`)), 8);
    const attack = takeFramesEvenly(await splitFrames(path.join(playerDir, `${who}_attack.png`)), 6);
    const idle = takeFramesEvenly(await gridFrames(path.join(playerDir, `${who}_idle.png`), 2, 2), 4);
    for (const [anim, frames] of [["walk", walk], ["attack", attack], ["idle", idle]]) {
      atlases.push(await writeAtlas(frames.map(flopRaw), `${who}_${anim}`, atlasDir));
    }
  }

  // 敌人：瘦长全身像强制拉伸切帧（源文件名已中性化 enemy_*）
  const enemyDir = path.join(DST, "sprites", "enemies");
  fs.mkdirSync(enemyDir, { recursive: true });
  const enemySrc = path.join(SRC, "sprites", "enemies", "sheets");
  for (const f of fs.readdirSync(enemySrc)) {
    if (!f.endsWith(".png")) continue;
    const frames = await stretchFrames(path.join(enemySrc, f));
    atlases.push(await writeAtlas(frames, f.replace(/\.png$/, ""), enemyDir));
  }
  report.atlases = atlases;

  fs.writeFileSync(path.join(WEB_ROOT, "tools", "sync-report.json"), JSON.stringify(report, null, 2));
  console.log("[assets] 同步完成:", JSON.stringify(report, null, 2));
}

main().catch((e) => { console.error(e); process.exit(1); });
