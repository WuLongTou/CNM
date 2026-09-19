// 简笔画资产生成器（M11 美术）：
// 用 SVG 参数化线条角色 + 帧动画 → sharp 栅格化，替换 assets-src 里的占位贴图。
// 走现有 npm run assets:sync 切帧管线，布局约定与占位源完全一致：
// - 玩家 walk/attack：水平排列多帧（投影分割），idle：2×2 网格 4 帧
// - 妖物：水平排列 6 帧瘦长全身像（管线强制拉伸 43×96，故内容按 ~40×88 画）
// - 地面：速写纸整图（view.ts 拉伸铺满世界）
//
// 简笔画风格规范（本文件与 README 共同维护）：
// - 画纸米白 #efe7d4 / 线条炭棕 #332e29（RGB 均高于管线剥底阈值 0.12）/ 血红点缀 #c0392b
// - 圆头线帽 + 每点 ≤0.8px 手绘抖动（seed 固定，产物可复现）
// - 妖物统一血红点睛（眼），玩家无红眼——一眼分辨敌我
// 用法：node tools/gen-sketch.mjs   （之后执行 npm run assets:sync）
import { createRequire } from "node:module";
import path from "node:path";
import fs from "node:fs";
import { fileURLToPath } from "node:url";

const require = createRequire(import.meta.url);
const sharp = require("sharp");

const PROJECT_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const SRC = path.join(PROJECT_ROOT, "assets-src");

// ── 风格常量 ──
const INK = "#332e29";        // 炭棕线条（不可低于管线剥黑阈值）
const PENCIL = "#8a8175";     // 铅笔灰（辅助/速度线/光点）
const BLOOD = "#c0392b";      // 血红点缀（妖物眼/符/火）
const VENOM = "#4a7a3a";      // 瘴毒绿（瘴妖专属功能色）
const GOLD = "#a8741a";       // 愈疗暖黄（愈妖光点）
const LINE_W = 3.6;           // 96px 帧内主线宽

// ── 可复现随机（mulberry32）──
function rng32(seed) {
  let a = seed >>> 0;
  return () => {
    a |= 0; a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/** 点列 → 抖动 + 中点二次平滑的 SVG path d（closed=true 时首尾相连）。 */
function sketchPath(pts, { closed = false, jitter = 0.7, rnd }) {
  const p = pts.map(([x, y]) => [x + (rnd() * 2 - 1) * jitter, y + (rnd() * 2 - 1) * jitter]);
  const n = p.length;
  if (n < 2) return "";
  let d = `M${p[0][0].toFixed(1)},${p[0][1].toFixed(1)}`;
  const last = closed ? n : n - 1;
  for (let i = 0; i < last; i++) {
    const a = p[i], b = p[(i + 1) % n];
    const mx = (a[0] + b[0]) / 2, my = (a[1] + b[1]) / 2;
    d += ` Q${a[0].toFixed(1)},${a[1].toFixed(1)} ${mx.toFixed(1)},${my.toFixed(1)}`;
  }
  if (closed) d += " Z";
  else d += ` L${p[n - 1][0].toFixed(1)},${p[n - 1][1].toFixed(1)}`;
  return d;
}

/** 圆采样点列（用于 sketchPath 画手绘圆）。 */
function circlePts(cx, cy, r, n = 14) {
  const out = [];
  for (let i = 0; i < n; i++) {
    const a = (i / n) * Math.PI * 2;
    out.push([cx + Math.cos(a) * r, cy + Math.sin(a) * r]);
  }
  return out;
}

/** 椭圆采样点列。 */
function ovalPts(cx, cy, rx, ry, n = 14) {
  const out = [];
  for (let i = 0; i < n; i++) {
    const a = (i / n) * Math.PI * 2;
    out.push([cx + Math.cos(a) * rx, cy + Math.sin(a) * ry]);
  }
  return out;
}

/**
 * 一支"笔"：往 body 数组里追加 SVG 元素。
 * 每次调用用递增 seed，保证同参数多次绘制抖动纹理略有差异（更像手绘）。
 */
function makePen(seedStart = 1) {
  let seed = seedStart;
  const body = [];
  const pen = {
    body,
    /** 开放折线（抖动+平滑）；pts 也接受扁平 [x1,y1,x2,y2,…] 形式。 */
    line(ptsIn, o = {}) {
      let pts = ptsIn;
      if (typeof pts[0] === "number") {
        pts = [];
        for (let i = 0; i + 1 < ptsIn.length; i += 2) pts.push([ptsIn[i], ptsIn[i + 1]]);
      }
      const rnd = rng32(seed += 7919);
      body.push(`<path d="${sketchPath(pts, { ...o, rnd })}" fill="none" stroke="${o.color ?? INK}" stroke-width="${o.width ?? LINE_W}" stroke-linecap="round" stroke-linejoin="round"${o.alpha ? ` opacity="${o.alpha}"` : ""}${o.dash ? ` stroke-dasharray="${o.dash}"` : ""}/>`);
    },
    /** 手绘圆（闭合线条）。 */
    circle(cx, cy, r, o = {}) {
      const rnd = rng32(seed += 7919);
      body.push(`<path d="${sketchPath(circlePts(cx, cy, r, o.n), { closed: true, jitter: o.jitter ?? 0.9, rnd })}" fill="${o.fill ?? "none"}" stroke="${o.color ?? INK}" stroke-width="${o.width ?? LINE_W}" stroke-linejoin="round"${o.alpha ? ` opacity="${o.alpha}"` : ""}${o.dash ? ` stroke-dasharray="${o.dash}"` : ""}/>`);
    },
    oval(cx, cy, rx, ry, o = {}) {
      const rnd = rng32(seed += 7919);
      body.push(`<path d="${sketchPath(ovalPts(cx, cy, rx, ry, o.n), { closed: true, jitter: o.jitter ?? 0.9, rnd })}" fill="${o.fill ?? "none"}" stroke="${o.color ?? INK}" stroke-width="${o.width ?? LINE_W}" stroke-linejoin="round"${o.alpha ? ` opacity="${o.alpha}"` : ""}/>`);
    },
    /** 实心小点（妖眼/铆钉/火花）。 */
    dot(cx, cy, r, o = {}) {
      body.push(`<circle cx="${cx}" cy="${cy}" r="${r}" fill="${o.color ?? BLOOD}"/>`);
    },
    /** 弧段（角度制，供挥砍弧/帽檐/蛇身用）。 */
    arc(cx, cy, r, a0, a1, o = {}) {
      const n = o.n ?? 10;
      const pts = [];
      for (let i = 0; i <= n; i++) {
        const a = ((a0 + (a1 - a0) * (i / n)) * Math.PI) / 180;
        pts.push([cx + Math.cos(a) * r, cy + Math.sin(a) * r]);
      }
      pen.line(pts, o);
    },
  };
  return pen;
}

// ── 帧画布组装 ──

/** 多帧水平排列成一张源图（帧 cell 间留透明间隔，供投影分割切帧）。 */
async function sheetHStack(frames /*: string[][] 内层为每帧 SVG body */, cellW, cellH, file, gap = 28) {
  const w = frames.length * cellW + (frames.length - 1) * gap;
  const h = cellH;
  const inner = frames
    .map((body, i) => `<g transform="translate(${i * (cellW + gap)},0)">${body.join("")}</g>`)
    .join("");
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}">${inner}</svg>`;
  fs.mkdirSync(path.dirname(file), { recursive: true });
  await sharp(Buffer.from(svg)).png().toFile(file);
  console.log(`[gen] ${path.relative(PROJECT_ROOT, file)}  (${frames.length} 帧)`);
}

/** 2×2 网格 idle 图。 */
async function sheetGrid4(frames, cell, file) {
  const [w, h] = [cell, cell];
  const inner = frames
    .map((body, i) => `<g transform="translate(${(i % 2) * w},${Math.floor(i / 2) * h})">${body.join("")}</g>`)
    .join("");
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${w * 2}" height="${h * 2}">${inner}</svg>`;
  fs.mkdirSync(path.dirname(file), { recursive: true });
  await sharp(Buffer.from(svg)).png().toFile(file);
  console.log(`[gen] ${path.relative(PROJECT_ROOT, file)}  (2×2 idle)`);
}

const TAU = Math.PI * 2;
const deg = (d) => (d * Math.PI) / 180;

// ════════════════════ 玩家：除妖师（火柴人骨架）════════════════════
// 96 高 cell：斗笠尖 y≈4、头圆心 (48,22) r10、肩 (48,42)、髋 (48,66)、脚 y≈93。
// walk：腿臂反相摆 + 身体 bob；attack：举剑→劈砍 + 挥弧；idle：呼吸 + 袍摆。

function drawPlayer(pen, { pose, t, weapon }) {
  // pose: "walk" | "attack" | "idle"；t ∈ [0,1) 帧相位
  let legA = 0, armA = 0, bob = 0, slashArc = 0, swordA = 0;
  if (pose === "walk") {
    const ph = t * TAU;
    legA = Math.sin(ph) * deg(26);   // 前后腿摆
    armA = -Math.sin(ph) * deg(20);
    bob = -Math.abs(Math.cos(ph)) * 1.6;
  } else if (pose === "attack") {
    // 6 帧三段式：0-1 举剑后仰，2-3 劈砍，4-5 收势（帧 3-5 露挥弧）
    const SW = [-118, -95, -40, 42, 58, 52]; // 持剑臂角度序列（0°=水平向前，正=向下劈）
    armA = deg(SW[Math.floor(t * 6) % 6] - 20);
    swordA = deg(SW[Math.floor(t * 6) % 6]) * 0.35;
    slashArc = t > 0.45 ? 1 : 0;
    bob = t < 0.35 ? -1 : 0.5;
  } else {
    bob = Math.sin(t * TAU) * 1.1;   // 呼吸
    armA = deg(8 + Math.sin(t * TAU) * 3);
  }
  if (pose === "attack") legA = deg(14); // 攻击时马步定腿

  const hip = [48, 66 + bob];
  const neck = [48, 42 + bob];
  const headC = [48, 24 + bob];

  // 身体（袍：颈到髋两笔 + 下摆开衩）
  pen.line([neck[0] - 1.5, neck[1] + 3, hip[0] - 3, hip[1] + 6, hip[0] - 6, 79 + bob]);
  pen.line([neck[0] + 1.5, neck[1] + 3, hip[0] + 3, hip[1] + 6, hip[0] + 6, 79 + bob]);
  // 腰带
  pen.line([43, 54 + bob, 53, 54.5 + bob], { width: 2.6 });

  // 腿（膝微弯，两腿相位反）
  const legs = pose === "walk" ? [legA, -legA] : [deg(12), deg(-12)];
  for (const a of legs) {
    const kx = hip[0] + Math.sin(a) * 14, ky = hip[1] + 13;
    const fx = kx + Math.sin(a * 0.5) * 12, fy = 93;
    pen.line([hip[0], hip[1] + 4, kx, ky, fx, fy]);
  }

  // 头 + 斗笠（除妖师）
  pen.circle(headC[0], headC[1], 10);
  // 帽：主弧（宽檐尖顶）
  pen.arc(headC[0], headC[1] - 3, 15, 195, 345, { width: 4 });
  pen.line([headC[0], headC[1] - 3 - 14.5, headC[0] + 0.5, headC[1] - 3 - 2.5], { width: 3 }); // 尖脊
  pen.line([headC[0] - 15.5, headC[1] - 1, headC[0] + 15.5, headC[1] - 1], { width: 3.2 });   // 檐
  // 脸：简笔点目（朝右）
  pen.dot(headC[0] + 4.5, headC[1] - 1, 1.5, { color: INK });

  // 手臂：后臂（远侧）+ 前臂（持武器）
  const sh = [neck[0], neck[1] + 6];
  const backA = -armA * 0.8;
  pen.line([sh[0], sh[1], sh[0] + Math.sin(backA) * 12, sh[1] + 10, sh[0] + Math.sin(backA) * 20, sh[1] + 20], { width: 3.1, alpha: 0.85 });
  const hand = [sh[0] + Math.sin(armA) * 16, sh[1] + 10 + Math.cos(-armA) * 8 + 8];
  pen.line([sh[0], sh[1], (sh[0] + hand[0]) / 2 + 2, (sh[1] + hand[1]) / 2, hand[0], hand[1]], { width: 3.4 });

  // 武器（画在手上）
  if (weapon === "warrior") {
    const wa = armA + deg(35); // 剑指向
    const tip = [hand[0] + Math.sin(wa) * 26, hand[1] + Math.cos(wa) * 26 - 6];
    pen.line([hand[0] - Math.sin(wa) * 5, hand[1] - Math.cos(wa) * 5, tip[0], tip[1]], { width: 3.4 });
    // 护手（垂直短线）+ 剑格点
    pen.line([hand[0] - Math.cos(wa) * 5, hand[1] + Math.sin(wa) * 5, hand[0] + Math.cos(wa) * 5, hand[1] - Math.sin(wa) * 5], { width: 3 });
    if (slashArc) {
      // 挥砍弧（攻击帧专属，从上到前 1/4 圆）
      pen.arc(sh[0], sh[1], 34, -95, 25, { color: INK, width: 4.4, alpha: 0.5, n: 12 });
    }
  } else if (weapon === "ranger") {
    // 弓：弓弧 + 弦 + 搭箭
    const bx = hand[0] + 4, by = hand[1];
    pen.arc(bx, by, 16, 150, 390, { width: 3.2, n: 12 });
    pen.line([bx + Math.cos(deg(150)) * 16, by + Math.sin(deg(150)) * 16, bx + Math.cos(deg(390)) * 16, by + Math.sin(deg(390)) * 16], { width: 1.8, alpha: 0.8 });
    pen.line([bx - 14, by, bx + 15, by - 2], { width: 2.4, alpha: 0.9 }); // 箭
  } else {
    // 法杖：长杖 + 顶端符环
    const wa = armA + deg(12);
    const top = [hand[0] + Math.sin(wa) * 8, hand[1] - 24];
    pen.line([hand[0] - Math.sin(wa) * 10, hand[1] + 12, top[0], top[1]], { width: 3 });
    pen.circle(top[0], top[1] - 4, 5, { width: 2.6 });
    pen.dot(top[0], top[1] - 4, 1.6);
  }
}

async function genPlayers() {
  const dir = path.join(SRC, "sprites", "player", "sheets");
  for (const who of ["warrior", "ranger", "mage"]) {
    // walk 8 帧 / attack 6 帧：96×96 cell 紧排网格（sync 侧用 gridFrames 等分切帧，
    // 与内容线条密度无关——细线火柴人不能用投影分割，会被腰斩成上下两半）
    const walk = Array.from({ length: 8 }, (_, i) => {
      const p = makePen(1000 + i * 31 + who.length);
      drawPlayer(p, { pose: "walk", t: i / 8, weapon: who });
      return p.body;
    });
    await sheetHStack(walk, 96, 96, path.join(dir, `${who}_walk.png`), 0);

    const attack = Array.from({ length: 6 }, (_, i) => {
      const p = makePen(2000 + i * 37 + who.length);
      drawPlayer(p, { pose: "attack", t: (i + 0.5) / 6, weapon: who });
      return p.body;
    });
    await sheetHStack(attack, 96, 96, path.join(dir, `${who}_attack.png`), 0);

    // idle：2×2 网格，cell 256（留足 trim 边距）
    const idle = Array.from({ length: 4 }, (_, i) => {
      const p = makePen(3000 + i * 41 + who.length);
      const inner = makePen(3000 + i * 41 + who.length);
      drawPlayer(inner, { pose: "idle", t: i / 4, weapon: who });
      // 居中到 256 cell
      return inner.body.map((s) => `<g transform="translate(64,80)">${s}</g>`);
    });
    await sheetGrid4(idle, 256, path.join(dir, `${who}_idle.png`));
  }
}

// ════════════════════ 妖物：14 种简笔小妖 ═════════════════════
// cell 128×96，内容画在中轴 x=48、宽 ≤44、脚底 y≈90（管线拉伸 43×96 近似 1:1）。
// 每帧闭包 (pen, t) t∈[0,1)。统一血红点睛。

/** 幽灵袍轮廓（游魂/隐煞共用），wavePh 控制底摆波浪相位。 */
function ghostOutline(pen, bob, wavePh, o = {}) {
  const top = 38 + bob, bottom = 84 + bob;
  const side = 17;
  const pts = [];
  // 头顶半圆
  for (let i = 0; i <= 6; i++) {
    const a = Math.PI + (i / 6) * Math.PI;
    pts.push([48 + Math.cos(a) * side, top + side + Math.sin(a) * side * 0.92]);
  }
  // 右侧直落
  pts.push([48 + side, bottom - 8]);
  // 底部三段波浪（相位滚动）
  for (let i = 0; i <= 8; i++) {
    const x = 48 + side - (i / 8) * side * 2;
    pts.push([x, bottom + Math.sin(wavePh + i * 2.1) * 3.2 - (i % 2) * 4]);
  }
  pts.push([48 - side, top + side + side * 0.92 - side]);
  // 左侧回升（直线到肩）
  pts.push([48 - side, top + side]);
  pen.line(pts, { ...o, closed: true });
}

const ENEMIES = {
  // 0 游魂：飘浮幽灵
  enemy_basic_walk(pen, t) {
    const bob = Math.sin(t * TAU) * 2;
    ghostOutline(pen, bob, t * TAU);
    pen.dot(42, 52 + bob, 2.1);
    pen.dot(55, 52 + bob, 2.1);
    pen.arc(48.5, 60 + bob, 5, 20, 160, { width: 2.4 }); // 波浪嘴
  },
  // 1 魅影：细长斜飘 + 速度线
  enemy_fast_walk(pen, t) {
    const sway = Math.sin(t * TAU) * 3;
    pen.line([[38, 16], [50 + sway * 0.4, 38], [44 - sway * 0.5, 62], [52 + sway * 0.6, 88]]);
    pen.arc(38, 13, 6, 140, 430, { width: 3.2, n: 10 }); // 头
    pen.dot(36, 12, 1.9);
    const fade = t < 0.5 ? 0.45 : 0.75;
    pen.line([[58, 30], [66, 36]], { color: PENCIL, width: 2.4, alpha: fade });
    pen.line([[60, 50], [69, 55]], { color: PENCIL, width: 2.4, alpha: fade * 0.8 });
    pen.line([[57, 68], [64, 73]], { color: PENCIL, width: 2.4, alpha: fade * 0.6 });
  },
  // 2 石魅：顽石巨躯 + 怒眉
  enemy_tank_walk(pen, t) {
    const tilt = Math.sin(t * TAU) * deg(3);
    const cx = 48, cy = 52;
    const rot = (x, y) => [cx + (x - cx) * Math.cos(tilt) - (y - cy) * Math.sin(tilt), cy + (x - cx) * Math.sin(tilt) + (y - cy) * Math.cos(tilt)];
    const stone = [[28, 42], [36, 26], [52, 22], [66, 30], [68, 48], [62, 64], [44, 68], [32, 60]].map(([x, y]) => rot(x, y));
    pen.line(stone, { closed: true, jitter: 1.1 });
    // 纹理两笔
    pen.line([rot(38, 38), rot(46, 34), rot(52, 40)], { width: 2.2, alpha: 0.6 });
    pen.line([rot(44, 54), rot(56, 52)], { width: 2.2, alpha: 0.5 });
    // 怒眉 + 红眼
    pen.line([rot(38, 44), rot(44, 47)], { width: 2.8 });
    pen.line([rot(58, 44), rot(52, 47)], { width: 2.8 });
    pen.dot(...rot(42, 51), 2);
    pen.dot(...rot(55, 51), 2);
    // 短粗腿
    pen.line([40, 68, 39, 84]);
    pen.line([56, 68, 57, 84]);
  },
  // 3 爆魈：圆滚引线 + 脉冲膨胀
  enemy_exploder_walk(pen, t) {
    const pulse = 1 + Math.sin(t * TAU) * 0.09;
    const r = 17 * pulse;
    const cy = 58;
    pen.circle(48, cy, r);
    pen.line([48, cy - r, 52, cy - r - 11]); // 引线
    const spark = t * TAU;
    for (let i = 0; i < 3; i++) {
      const a = spark + (i / 3) * TAU;
      pen.dot(53 + Math.cos(a) * 5, cy - r - 13 + Math.sin(a) * 4, 1.6, { color: GOLD });
    }
    pen.dot(43, cy - 3, 2);
    pen.dot(53, cy - 3, 2);
    pen.line([43, cy + 6, 47, cy + 9, 53, cy + 6]); // 咧嘴
  },
  // 4 隐煞：虚线幽灵
  enemy_invisible_walk(pen, t) {
    const bob = Math.sin(t * TAU + 1) * 2;
    ghostOutline(pen, bob, t * TAU, { dash: "7 6", alpha: 0.9 });
    pen.dot(42, 52 + bob, 2.1);
    pen.dot(55, 52 + bob, 2.1);
  },
  // 5 瘴妖：毒滴躯 + 滴液
  enemy_spitter_walk(pen, t) {
    const bob = Math.sin(t * TAU) * 1.5;
    const pts = [];
    for (let i = 0; i <= 8; i++) { // 上尖下圆滴形
      const a = Math.PI + (i / 8) * Math.PI;
      pts.push([48 + Math.cos(a) * 13, 52 + bob + Math.sin(a) * 24]);
    }
    pen.line(pts, { closed: true });
    pen.arc(48, 66 + bob, 5, 30, 150, { width: 2.4 }); // 张嘴
    pen.dot(44, 52 + bob, 1.9);
    pen.dot(53, 52 + bob, 1.9);
    // 下滴毒滴（帧相位下移）
    const dy = (t * 14) % 14;
    const rnd = rng32(88);
    pen.oval(60, 72 + dy, 2.6, 3.6, { color: VENOM, width: 2.2, fill: "none" });
    pen.dot(60, 72 + dy, 1.2, { color: VENOM });
  },
  // 6 愈妖：圆胖笑眼 + 环绕光点
  enemy_healer_walk(pen, t) {
    const bob = Math.sin(t * TAU) * 2;
    pen.oval(48, 60 + bob, 16, 20);
    // 笑眼（弧）
    pen.arc(42, 55 + bob, 3.5, 200, 340, { width: 2.4 });
    pen.arc(55, 55 + bob, 3.5, 200, 340, { width: 2.4 });
    // 环绕十字光点
    for (let i = 0; i < 3; i++) {
      const a = t * TAU + (i / 3) * TAU;
      const gx = 48 + Math.cos(a) * 26, gy = 60 + bob + Math.sin(a) * 26;
      pen.line([gx - 4, gy, gx + 4, gy], { color: GOLD, width: 3.2, alpha: 0.9 });
      pen.line([gx, gy - 4, gx, gy + 4], { color: GOLD, width: 3.2, alpha: 0.9 });
    }
    pen.line([40, 82 + bob, 44, 88], { width: 2.6 });
    pen.line([56, 82 + bob, 52, 88], { width: 2.6 });
  },
  // 7 唤灵：尖帽巫袍 + 举符圈
  enemy_summoner_walk(pen, t) {
    const sway = Math.sin(t * TAU) * 1.6;
    // 袍（梯形 + 摆动底沿）
    pen.line([[48, 34], [34 + sway, 78], [40 + sway, 84], [56 - sway, 84], [62 - sway, 78], [48, 34]], { closed: true });
    // 尖帽
    pen.line([[36, 36], [48, 12], [60, 36]], { closed: true });
    pen.line([34, 37, 62, 37]);
    pen.dot(43, 45, 2);
    pen.dot(53, 45, 2);
    // 手举符圈（圆 + 中点，血红）
    pen.line([58, 48, 66, 38], { width: 3 });
    pen.circle(68, 33, 6.5, { color: BLOOD, width: 2.6, alpha: 0.9 });
    pen.dot(68, 33, 1.8);
  },
  // 8 弩妖：矮胖 + 手弩
  enemy_archer_walk(pen, t) {
    const bob = Math.sin(t * TAU) * 1.5;
    pen.oval(46, 56 + bob, 13, 16);
    pen.dot(41, 52 + bob, 2);
    pen.dot(51, 52 + bob, 2);
    pen.line([40, 61 + bob, 45, 64 + bob, 51, 61 + bob], { width: 2.2 }); // 獠牙嘴
    pen.line([40, 72 + bob, 39, 86]);
    pen.line([52, 72 + bob, 53, 86]);
    // 手弩：竖臂 + 横弓弧 + 弦 + 箭
    const tilt = Math.sin(t * TAU) * deg(4);
    const hx = 62, hy = 56 + bob;
    const rot = (x, y) => [hx + (x - hx) * Math.cos(tilt) - (y - hy) * Math.sin(tilt), hy + (x - hx) * Math.sin(tilt) + (y - hy) * Math.cos(tilt)];
    pen.line([rot(hx - 2, hy + 8), rot(hx + 2, hy - 8)], { width: 3 });
    pen.arc(...rot(hx + 1, hy - 8).map(Number), 9, 120, 240, { width: 2.8, n: 8 });
    pen.line([rot(hx - 8, hy - 4), rot(hx + 10, hy - 6)], { width: 1.6, alpha: 0.75 });
    pen.line([rot(hx - 6, hy - 5), rot(hx + 14, hy - 6.5)], { width: 2.2 });
  },
  // 9 浮灯：灯笼漂浮
  enemy_ship_walk(pen, t) {
    const bob = Math.sin(t * TAU) * 3.5;
    const tilt = Math.sin(t * TAU + 0.8) * deg(4);
    const cy = 52 + bob, cx = 48;
    const rot = (x, y) => [cx + (x - cx) * Math.cos(tilt) - (y - cy) * Math.sin(tilt), cy + (x - cx) * Math.sin(tilt) + (y - cy) * Math.cos(tilt)];
    // 提梁 + 穗
    pen.arc(...rot(cx, cy - 16).map(Number), 6, 180, 360, { width: 2.8, n: 8 });
    pen.line([rot(cx - 2, cy + 15), rot(cx - 2, cy + 20)], { width: 2.2, alpha: 0.8 });
    pen.line([rot(cx + 2, cy + 15), rot(cx + 2, cy + 21)], { width: 2.2, alpha: 0.8 });
    // 灯笼罩：两道竖弧 + 上下盖
    pen.oval(cx, cy, 13, 16);
    pen.line([rot(cx, cy - 16), rot(cx, cy + 16)], { width: 2.4, alpha: 0.7 });
    pen.line([rot(cx - 13, cy), rot(cx + 13, cy)], { width: 2.2, alpha: 0.6 });
    pen.line([rot(cx - 10, cy - 16), rot(cx + 10, cy - 16)], { width: 3 });
    pen.line([rot(cx - 10, cy + 16), rot(cx + 10, cy + 16)], { width: 3 });
    // 内焰（血红滴形）
    pen.line([[cx, cy - 7], [cx + 4, cy + 1], [cx, cy + 8], [cx - 4, cy + 1], [cx, cy - 7]], { color: BLOOD, width: 2.4, closed: true, alpha: 0.85 });
  },
  // 10 铁傀：方甲 + 独眼窗 + 天线
  enemy_mech_walk(pen, t) {
    const bob = Math.abs(Math.sin(t * TAU)) * 2;
    const y = 44 + bob;
    pen.line([[28, y], [28, y + 30], [68, y + 30], [68, y]], { closed: true, jitter: 0.9 });
    // 甲纹 + 铆钉
    pen.line([28, y + 10, 68, y + 10], { width: 2.2, alpha: 0.6 });
    pen.line([48, y + 10, 48, y + 30], { width: 2.2, alpha: 0.5 });
    for (const [rx, ry] of [[32, y + 4], [64, y + 4], [32, y + 26], [64, y + 26]]) pen.dot(rx, ry, 1.4, { color: PENCIL });
    // 独眼窗
    pen.circle(48, y + 13, 6, { width: 2.8 });
    pen.dot(48, y + 13, 2.4);
    // 天线
    pen.line([48, y, 48, y - 10]);
    pen.dot(48, y - 11, 2);
    // 腿
    pen.line([36, y + 30, 34, 86]);
    pen.line([60, y + 30, 62, 86]);
  },
  // 11 甲妖：圆盔 + 前持盾
  enemy_knight_walk(pen, t) {
    const bob = Math.sin(t * TAU) * 1.5;
    const push = Math.sin(t * TAU) * 2;
    // 身
    pen.line([[48, 36], [38, 70], [44, 78], [56, 78], [58, 70], [48, 36]], { closed: true });
    // 圆盔（半圆 + 护鼻）
    pen.arc(48, 34 + bob, 11, 180, 360, { width: 3.4, n: 10 });
    pen.line([48, 24 + bob, 48, 38 + bob], { width: 2.6, alpha: 0.8 });
    pen.dot(43, 33 + bob, 1.8);
    pen.dot(53, 33 + bob, 1.8);
    // 盾（圆角倒梯形 + 中脊）
    const sx = 64 + push;
    pen.line([[sx, 40], [sx + 9, 44], [sx + 9, 68], [sx, 74], [sx - 9, 68], [sx - 9, 44]], { closed: true });
    pen.line([sx, 42, sx, 72], { width: 2.4, alpha: 0.8 });
    pen.line([sx - 6, 48, sx + 6, 48], { width: 2, alpha: 0.5 });
    // 持盾臂
    pen.line([56, 50, sx - 8, 52], { width: 3 });
  },
  // 12 蛟：S 形蛇身蠕动
  enemy_dragon_walk(pen, t) {
    const ph = t * TAU;
    const pts = [];
    for (let i = 0; i <= 16; i++) {
      const u = i / 16;
      pts.push([48 + Math.sin(u * 6.5 + ph) * (4 + u * 11), 88 - u * 68]);
    }
    pen.line(pts, { width: 3.8 });
    // 头（端点）+ 双角
    const [hx, hy] = pts[16];
    pen.circle(hx, hy - 5, 8, { width: 3.2 });
    pen.line([hx - 5, hy - 11, hx - 8, hy - 17], { width: 2.4 });
    pen.line([hx + 5, hy - 11, hx + 8, hy - 17], { width: 2.4 });
    pen.dot(hx + 2.5, hy - 5, 1.9);
    // 背鳍三撇
    for (let i = 4; i <= 12; i += 4) {
      const [fx, fy] = pts[i];
      pen.line([fx - 2, fy - 2, fx - 6, fy - 7], { width: 2.2, alpha: 0.8 });
    }
  },
  // 13 蚀群：墨点簇蠕动
  enemy_swarm_walk(pen, t) {
    const rnd = rng32(4242);
    const ph = t * TAU;
    for (let i = 0; i < 9; i++) {
      const bx = 48 + (rnd() - 0.5) * 30;
      const by = 52 + (rnd() - 0.5) * 34;
      const wob = Math.sin(ph + i * 2.3) * 3.2;
      const r = 2.4 + rnd() * 3.4;
      if (i < 6) pen.dot(bx + wob, by + wob * 0.6, r, { color: INK });
      else pen.circle(bx - wob * 0.7, by, r + 1, { width: 2.2, alpha: 0.8 });
    }
    pen.dot(45, 50, 2, {});
    pen.dot(55, 56, 2, {});
  },
};

async function genEnemies() {
  const dir = path.join(SRC, "sprites", "enemies", "sheets");
  for (const [name, draw] of Object.entries(ENEMIES)) {
    const frames = Array.from({ length: 6 }, (_, i) => {
      const p = makePen(5000 + i * 53 + name.length * 97);
      draw(p, i / 6);
      return p.body;
    });
    await sheetHStack(frames, 128, 96, path.join(dir, `${name}.png`));
  }
}

// ════════════════════ 欢迎页主题插画 ════════════════════
// 800×340 场景：血月 + 远山一笔 + 地面线 + 除妖师（放大 2.4×）对峙三只妖物。
async function genTitle() {
  const W = 800, H = 340;
  const pen = makePen(60601);
  const parts = [];
  // 血月（右上，双圈 + 内点）
  parts.push(`<g>${(() => { const p = makePen(60602);
    p.circle(660, 84, 44, { color: BLOOD, width: 4 });
    // 两笔月面横向短弧（月海），替代斑点
    p.arc(648, 76, 16, 200, 330, { color: BLOOD, width: 2.2, alpha: 0.5, n: 8 });
    p.arc(668, 94, 12, 200, 330, { color: BLOOD, width: 2, alpha: 0.4, n: 7 });
    // 一笔云带横穿月轮下缘
    p.line([[590, 112], [628, 104], [664, 112], [706, 102]], { width: 2.4, alpha: 0.6 });
    return p.body.join(""); })()}</g>`);
  // 远山一笔（left → right 起伏）
  parts.push(`<g>${(() => { const p = makePen(60603);
    p.line([[30, 210], [130, 168], [220, 196], [330, 152], [430, 190], [540, 160], [660, 192], [770, 172]], { width: 2.6, alpha: 0.55 });
    return p.body.join(""); })()}</g>`);
  // 地面线 + 小草
  parts.push(`<g>${(() => { const p = makePen(60604);
    p.line([[40, 292], [760, 288]], { width: 3 });
    const rnd = rng32(60605);
    for (let i = 0; i < 9; i++) {
      const x = 70 + rnd() * 660, y = 288 + rnd() * 6, s2 = 7 + rnd() * 6;
      for (let k = -1; k <= 1; k++) p.line([[x, y], [x + k * s2 * 0.8, y - s2 * (1 - Math.abs(k) * 0.3)]], { width: 2, alpha: 0.5 });
    }
    return p.body.join(""); })()}</g>`);
  // 除妖师：drawPlayer 画在 96 cell，放大 2.4 平移到 (70,86)
  {
    const p = makePen(60610);
    drawPlayer(p, { pose: "attack", t: 0.18, weapon: "warrior" });
    parts.push(`<g transform="translate(78,92) scale(2.4)">${p.body.join("")}</g>`);
  }
  // 三只妖：游魂 / 爆魈 / 蛟（各自 96 cell 原大 1.8×）
  const foes = [
    ["enemy_basic_walk", 0.25, 470, 130],
    ["enemy_exploder_walk", 0.55, 590, 170],
    ["enemy_fast_walk", 0.8, 636, 150],
  ];
  for (const [name, t, tx, ty] of foes) {
    const p = makePen(61000 + Math.floor(tx));
    ENEMIES[name](p, t);
    parts.push(`<g transform="translate(${tx},${ty}) scale(1.8)">${p.body.join("")}</g>`);
  }
  // 飘落符纸两枚（血红矩形 + 中竖）
  {
    const p = makePen(60700);
    for (const [x, y, a] of [[380, 60, -14], [560, 110, 10]]) {
      p.body.push(`<g transform="rotate(${a} ${x} ${y})">`);
      p.line([[x - 6, y - 9], [x + 6, y - 9], [x + 6, y + 9], [x - 6, y + 9], [x - 6, y - 9]], { color: BLOOD, width: 2, alpha: 0.7 });
      p.line([[x, y - 9], [x, y + 9]], { color: BLOOD, width: 1.6, alpha: 0.55 });
      p.body.push("</g>");
    }
    parts.push(p.body.join(""));
  }

  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}">
    <rect width="${W}" height="${H}" fill="#f5eede"/>
    ${parts.join("\n")}
    <radialGradient id="tv" cx="50%" cy="50%" r="75%">
      <stop offset="80%" stop-color="rgba(90,66,40,0)"/><stop offset="100%" stop-color="rgba(90,66,40,0.08)"/>
    </radialGradient>
    <rect width="${W}" height="${H}" fill="url(#tv)"/>
  </svg>`;
  const file = path.join(SRC, "sprites", "ui", "title_sketch.png");
  fs.mkdirSync(path.dirname(file), { recursive: true });
  await sharp(Buffer.from(svg)).png().toFile(file);
  console.log(`[gen] ${path.relative(PROJECT_ROOT, file)}  (欢迎页插画 ${W}×${H})`);
}

// ════════════════════ 地面：速写纸 ════════════════════
async function genGround() {
  const W = 2560, H = 1440;
  const rnd = rng32(20260919);
  const pen = makePen(777);
  // 铅笔涂鸦散点：小草三撇 / 小石圈 / 裂缝折线 / 血渍斑 / 符箓残迹
  const items = 74;
  for (let i = 0; i < items; i++) {
    const x = 60 + rnd() * (W - 120), y = 60 + rnd() * (H - 120);
    const kind = rnd();
    if (kind < 0.42) {
      // 小草三撇
      const s = 5 + rnd() * 5;
      for (let k = -1; k <= 1; k++) pen.line([[x, y], [x + k * s * 0.8, y - s * (1 - Math.abs(k) * 0.3)]], { color: PENCIL, width: 2, alpha: 0.5 });
    } else if (kind < 0.62) {
      // 小石圈
      const r = 4 + rnd() * 5;
      pen.circle(x, y, r, { color: PENCIL, width: 2, alpha: 0.45, n: 8 });
    } else if (kind < 0.8) {
      // 裂缝折线
      pen.line([[x, y], [x + 8 + rnd() * 12, y + (rnd() - 0.5) * 10], [x + 18 + rnd() * 14, y + (rnd() - 0.5) * 12]], { color: PENCIL, width: 1.8, alpha: 0.4 });
    } else if (kind < 0.92) {
      // 淡血渍（同心淡红）
      const r = 8 + rnd() * 16;
      pen.circle(x, y, r, { color: BLOOD, width: 1.6, alpha: 0.08, n: 10, jitter: 3 });
      pen.circle(x + (rnd() - 0.5) * 6, y + (rnd() - 0.5) * 6, r * 0.55, { color: BLOOD, width: 1.2, alpha: 0.09, n: 8, jitter: 2.5 });
    } else {
      // 符箓残迹：竖 + 圈
      pen.line([[x, y], [x, y - 14]], { color: BLOOD, width: 2.2, alpha: 0.16 });
      pen.circle(x, y - 20, 4, { color: BLOOD, width: 1.8, alpha: 0.16, n: 8 });
    }
  }
  // 纸纹噪点（极淡短划）
  let noise = "";
  for (let i = 0; i < 2600; i++) {
    const x = (rnd() * W).toFixed(0), y = (rnd() * H).toFixed(0);
    const a = (0.015 + rnd() * 0.03).toFixed(3);
    noise += `<rect x="${x}" y="${y}" width="${rnd() > 0.5 ? 2 : 1}" height="1" fill="#6b6154" opacity="${a}"/>`;
  }
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}">
    <rect width="${W}" height="${H}" fill="#efe7d4"/>
    ${noise}
    ${pen.body.join("")}
    <radialGradient id="v" cx="50%" cy="50%" r="72%">
      <stop offset="82%" stop-color="rgba(90,66,40,0)"/><stop offset="100%" stop-color="rgba(90,66,40,0.07)"/>
    </radialGradient>
    <rect width="${W}" height="${H}" fill="url(#v)"/>
  </svg>`;
  const file = path.join(SRC, "sprites", "backgrounds", "ground_cemetery.jpg");
  await sharp(Buffer.from(svg)).jpeg({ quality: 88 }).toFile(file);
  console.log(`[gen] ${path.relative(PROJECT_ROOT, file)}  (速写纸地面 ${W}×${H})`);
}

async function main() {
  console.log("[gen] 简笔画资产生成开始…");
  await genTitle();
  await genGround();
  await genPlayers();
  await genEnemies();
  console.log("[gen] 全部完成。请执行 npm run assets:sync 生成 public/assets。");
}

main().catch((e) => { console.error(e); process.exit(1); });
