// 简笔画资产生成器 v2（M22 全量重绘）：
// 填充式马克笔手绘风——每个身体部位是「平滑色块填充 + 炭描边」，Q 版大头比例，
// 取代 v1 的纯线条火柴风（用户反馈太粗糙）。
// SVG 参数化 → sharp 栅格化 → assets:sync 切帧（玩家/妖物表已跳过剥底，
// 透明产物；配色仍保持足够色散以防管线变化侵蚀浅色填充）。
//
// 风格规范（README「美术」章节同步维护）：
// - 炭棕描边 #332e29（w3 主轮廓 / w1.8 细节），圆头线帽，抖动 ≤0.6（干净曲线）
// - 身体部位全部 blob 填充（Catmull-Rom 平滑闭合），哑光纸感配色（色散 ≥0.09）
// - 眼睛 = 墨点 + 白高光；妖物血红点睛元素；玩家无红眼——一眼分辨敌我
// - 布局：玩家 walk/attack 96×96 网格紧排（8×1/6×1）、idle 2×2；妖物 128×96 横排
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
const INK = "#332e29";        // 炭棕描边
const PENCIL = "#8a8175";     // 铅笔灰（辅助线）
const BLOOD = "#c0392b";      // 血红点缀
const GOLD = "#c89a3c";       // 暖金（愈疗/火花）
const SHADOW = "#6b6154";     // 落影

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

const TAU = Math.PI * 2;
const deg = (d) => (d * Math.PI) / 180;

/** 闭合点列 → Catmull-Rom 平滑 Bezier path（带轻微抖动，手绘但不脏）。 */
function smoothClosed(ptsIn, jitter, rnd) {
  const p = ptsIn.map(([x, y]) => [x + (rnd() * 2 - 1) * jitter, y + (rnd() * 2 - 1) * jitter]);
  const n = p.length;
  let d = `M${p[0][0].toFixed(1)},${p[0][1].toFixed(1)}`;
  for (let i = 0; i < n; i++) {
    const p0 = p[(i - 1 + n) % n], p1 = p[i], p2 = p[(i + 1) % n], p3 = p[(i + 2) % n];
    const c1x = p1[0] + (p2[0] - p0[0]) / 6, c1y = p1[1] + (p2[1] - p0[1]) / 6;
    const c2x = p2[0] - (p3[0] - p1[0]) / 6, c2y = p2[1] - (p3[1] - p1[1]) / 6;
    d += ` C${c1x.toFixed(1)},${c1y.toFixed(1)} ${c2x.toFixed(1)},${c2y.toFixed(1)} ${p2[0].toFixed(1)},${p2[1].toFixed(1)}`;
  }
  return d + " Z";
}

/** 开放点列 → 抖动平滑 path（Catmull-Rom 开放形式）。 */
function smoothOpen(ptsIn, jitter, rnd) {
  const p = ptsIn.map(([x, y]) => [x + (rnd() * 2 - 1) * jitter, y + (rnd() * 2 - 1) * jitter]);
  const n = p.length;
  let d = `M${p[0][0].toFixed(1)},${p[0][1].toFixed(1)}`;
  for (let i = 0; i + 1 < n; i++) {
    const p0 = p[Math.max(0, i - 1)], p1 = p[i], p2 = p[i + 1], p3 = p[Math.min(n - 1, i + 2)];
    const c1x = p1[0] + (p2[0] - p0[0]) / 6, c1y = p1[1] + (p2[1] - p0[1]) / 6;
    const c2x = p2[0] - (p3[0] - p1[0]) / 6, c2y = p2[1] - (p3[1] - p1[1]) / 6;
    d += ` C${c1x.toFixed(1)},${c1y.toFixed(1)} ${c2x.toFixed(1)},${c2y.toFixed(1)} ${p2[0].toFixed(1)},${p2[1].toFixed(1)}`;
  }
  return d;
}

function ovalPts(cx, cy, rx, ry, n = 14, a0 = 0) {
  const out = [];
  for (let i = 0; i < n; i++) {
    const a = a0 + (i / n) * TAU;
    out.push([cx + Math.cos(a) * rx, cy + Math.sin(a) * ry]);
  }
  return out;
}

/**
 * 一支"笔"：向 body 追加 SVG 片段。seed 递增保证多次绘制抖动纹理不同。
 */
function makePen(seedStart = 1) {
  let seed = seedStart;
  const body = [];
  const pen = {
    body,
    /** 开放曲线（平滑）；pts 接受扁平 [x1,y1,...]。 */
    line(ptsIn, o = {}) {
      let pts = ptsIn;
      if (typeof pts[0] === "number") {
        pts = [];
        for (let i = 0; i + 1 < ptsIn.length; i += 2) pts.push([ptsIn[i], ptsIn[i + 1]]);
      }
      const rnd = rng32(seed += 7919);
      body.push(`<path d="${smoothOpen(pts, o.jitter ?? 0.22, rnd)}" fill="none" stroke="${o.color ?? INK}" stroke-width="${o.width ?? 3}" stroke-linecap="round" stroke-linejoin="round"${o.alpha ? ` opacity="${o.alpha}"` : ""}${o.dash ? ` stroke-dasharray="${o.dash}"` : ""}/>`);
    },
    /** 平滑填充色块（马克笔形状）。 */
    blob(pts, o = {}) {
      const rnd = rng32(seed += 7919);
      body.push(`<path d="${smoothClosed(pts, o.jitter ?? 0.28, rnd)}" fill="${o.fill ?? "none"}" stroke="${o.stroke ?? INK}" stroke-width="${o.w ?? 3}" stroke-linejoin="round"${o.alpha ? ` opacity="${o.alpha}"` : ""}${o.dash ? ` stroke-dasharray="${o.dash}"` : ""}/>`);
    },
    /** 手绘椭圆色块。 */
    ellipse(cx, cy, rx, ry, o = {}) {
      pen.blob(ovalPts(cx, cy, rx, ry, o.n ?? 14), o);
    },
    /** 实心点（眼/饰件）。 */
    dot(cx, cy, r, o = {}) {
      body.push(`<circle cx="${cx}" cy="${cy}" r="${r}" fill="${o.color ?? INK}"${o.alpha ? ` opacity="${o.alpha}"` : ""}/>`);
    },
    /** 弧线段（角度制）。 */
    arc(cx, cy, r, a0, a1, o = {}) {
      const n = o.n ?? 10;
      const pts = [];
      for (let i = 0; i <= n; i++) {
        const a = deg(a0 + (a1 - a0) * (i / n));
        pts.push([cx + Math.cos(a) * r, cy + Math.sin(a) * r]);
      }
      pen.line(pts, o);
    },
    /** 马克笔高光：主体色块左上的一道白弧（简洁感的工艺细节）。 */
    shine(cx, cy, rx, ry, o = {}) {
      pen.arc(cx, cy, Math.max(rx, ry) * 0.66, o.a0 ?? -165, o.a1 ?? -85,
        { color: "#ffffff", w: o.w ?? 2.6, alpha: o.alpha ?? 0.5, n: 6 });
    },
    /** 眼睛：墨点 + 白高光（有神的关键）。 */
    eye(cx, cy, r = 2, o = {}) {
      pen.dot(cx, cy, r, { color: o.color ?? INK });
      pen.dot(cx - r * 0.35, cy - r * 0.4, r * 0.34, { color: "#ffffff" });
    },
  };
  return pen;
}

// ── 帧画布组装 ──

async function sheetHStack(frames, cellW, cellH, file, gap = 28) {
  const w = frames.length * cellW + (frames.length - 1) * gap;
  const inner = frames
    .map((body, i) => `<g transform="translate(${i * (cellW + gap)},0)">${body.join("")}</g>`)
    .join("");
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${cellH}">${inner}</svg>`;
  fs.mkdirSync(path.dirname(file), { recursive: true });
  await sharp(Buffer.from(svg)).png().toFile(file);
  console.log(`[gen] ${path.relative(PROJECT_ROOT, file)}  (${frames.length} 帧)`);
}

async function sheetGrid4(frames, cell, file) {
  const inner = frames
    .map((body, i) => `<g transform="translate(${(i % 2) * cell},${Math.floor(i / 2) * cell})">${body.join("")}</g>`)
    .join("");
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${cell * 2}" height="${cell * 2}">${inner}</svg>`;
  fs.mkdirSync(path.dirname(file), { recursive: true });
  await sharp(Buffer.from(svg)).png().toFile(file);
  console.log(`[gen] ${path.relative(PROJECT_ROOT, file)}  (2×2 idle)`);
}

// ════════════════════ 玩家：Q 版除妖师（填充）════════════════════
// 96 cell：帽顶 y≈4、头圆心 (48,25) r10.5、肩 43、袍摆 76、脚 89。面朝右。

const ROBE = { warrior: "#bf9d6a", ranger: "#7fa389", mage: "#8e97cc" };
const SKIN = "#eccfa0";

function drawPlayer(pen, { pose, t, weapon }) {
  let legA = 0, armA = 0, bob = 0;
  if (pose === "walk") {
    const ph = t * TAU;
    legA = Math.sin(ph) * deg(16);
    armA = -Math.sin(ph) * deg(18);
    bob = -Math.abs(Math.cos(ph)) * 1.6;
  } else if (pose === "attack") {
    const SW = [-118, -95, -40, 42, 58, 52];
    armA = deg(SW[Math.floor(t * 6) % 6] - 15);
    bob = t < 0.35 ? -1 : 0.5;
  } else {
    bob = Math.sin(t * TAU) * 1.2;
    armA = deg(6 + Math.sin(t * TAU) * 3);
  }
  if (pose === "attack") legA = deg(12);

  const hip = [48, 62 + bob], neck = [48, 40 + bob], headC = [48, 25 + bob];
  const robe = ROBE[weapon] ?? ROBE.warrior;

  // 落影
  pen.ellipse(48, 91, 12, 3, { fill: SHADOW, stroke: "none", w: 0, alpha: 0.16 });

  // 腿（两段式粗笔 + 脚掌）
  const legs = pose === "walk" ? [legA, -legA] : [legA, -legA * 0.6];
  for (const a of legs) {
    const kx = hip[0] + Math.sin(a) * 13, ky = hip[1] + 12;
    const fx = kx + Math.sin(a * 0.7) * 11, fy = 89;
    pen.line([[hip[0], hip[1] + 6], [kx, ky], [fx, fy]], { w: 5 });
    pen.line([[fx - 3, fy], [fx + 3.5, fy]], { w: 5.5 });
  }

  // 袍身（下摆随步伐微摆）
  const sway = pose === "walk" ? Math.sin(t * TAU) * 1.6 : 0;
  pen.blob([
    [38, 43], [30, 52], [31 + sway, 64], [33 + sway, 72],
    [63 - sway, 72], [65 - sway, 64], [66, 52], [58, 43],
  ], { fill: robe });
  pen.line([[48, 45], [48, 70]], { w: 1.5, alpha: 0.35 });            // 对襟
  pen.line([[41, 57 + bob], [55, 57 + bob]], { color: BLOOD, w: 3 });  // 腰带
  pen.line([[48, 58 + bob], [48, 64 + bob]], { color: BLOOD, w: 1.6 });// 挂饰
  pen.dot(48, 65.5 + bob, 1.6, { color: BLOOD });
  pen.shine(48, 24 + bob, 8, 8);

  // 后臂
  const backA = -armA * 0.75;
  pen.line([
    [neck[0] - 2, neck[1] + 5],
    [neck[0] - 2 + Math.sin(backA) * 8, neck[1] + 12],
    [neck[0] - 2 + Math.sin(backA) * 14, neck[1] + 19],
  ], { w: 4, alpha: 0.92 });

  // 头 + 脸 + 斗笠
  pen.ellipse(headC[0], headC[1], 10.5, 10.5, { fill: SKIN });
  pen.eye(headC[0] + 2.5, headC[1] + 1, 1.8);
  pen.eye(headC[0] + 7, headC[1] + 1, 1.8);
  pen.arc(headC[0] + 2, headC[1] + 5.5, 3, 25, 155, { w: 1.6 });      // 嘴
  pen.line([[headC[0] + 2, headC[1] - 3], [headC[0] + 7.5, headC[1] - 2.5]], { w: 2 }); // 眉
  pen.blob([[36, 17], [43, 6], [53, 6], [60, 17]], { fill: "#ba9c58" });
  pen.ellipse(48, 17.5, 18, 5, { fill: "#ba9c58" });
  pen.line([[35, 15.5], [61, 15.5]], { color: BLOOD, w: 2.4 });

  // 前臂 + 手 + 武器
  const sh = [neck[0] + 1, neck[1] + 6];
  const hand = [sh[0] + Math.sin(armA) * 13, sh[1] + Math.cos(-armA) * 5 + 9];
  pen.line([[sh[0], sh[1]], [(sh[0] + hand[0]) / 2 + 1.5, (sh[1] + hand[1]) / 2], hand], { w: 4.5 });
  pen.ellipse(hand[0], hand[1], 2.6, 2.6, { fill: SKIN, w: 2.2 });

  if (weapon === "warrior") {
    const wa = armA + deg(38);
    const tip = [hand[0] + Math.sin(wa) * 27, hand[1] - Math.cos(wa) * 27];
    const gx = hand[0] - Math.cos(wa) * 4.5, gy = hand[1] + Math.sin(wa) * 4.5;
    pen.line([[hand[0] - Math.cos(wa) * 4.5, hand[1] + Math.sin(wa) * 4.5], [tip[0], tip[1]]], { color: "#7a7268", w: 3.4 });
    pen.line([[gx - Math.sin(wa) * 5, gy + Math.cos(wa) * 5], [gx + Math.sin(wa) * 5, gy - Math.cos(wa) * 5]], { w: 2.6 });
  } else if (weapon === "ranger") {
    pen.arc(hand[0] + 3, hand[1], 15, 145, 395, { w: 3.2, n: 12 });
    pen.line([
      [hand[0] + 3 + Math.cos(deg(145)) * 15, hand[1] + Math.sin(deg(145)) * 15],
      [hand[0] + 3 + Math.cos(deg(395)) * 15, hand[1] + Math.sin(deg(395)) * 15],
    ], { w: 1.6, alpha: 0.8 });
    pen.line([[hand[0] - 12, hand[1] - 1], [hand[0] + 14, hand[1] - 2]], { w: 2.2, alpha: 0.9 });
  } else {
    const top = [hand[0] + 2, hand[1] - 26];
    pen.line([[hand[0] - 1, hand[1] + 12], top], { w: 3 });
    pen.ellipse(top[0], top[1] - 5, 5.5, 5.5, { fill: "#bcc2e2" });
    pen.dot(top[0], top[1] - 5, 1.8, { color: BLOOD });
  }
}

async function genPlayers() {
  const dir = path.join(SRC, "sprites", "player", "sheets");
  for (const who of ["warrior", "ranger", "mage"]) {
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
    const idle = Array.from({ length: 4 }, (_, i) => {
      const p = makePen(3000 + i * 41 + who.length);
      drawPlayer(p, { pose: "idle", t: i / 4, weapon: who });
      return p.body.map((seg) => `<g transform="translate(64,80)">${seg}</g>`);
    });
    await sheetGrid4(idle, 256, path.join(dir, `${who}_idle.png`));
  }
}

// ════════════════════ 妖物：14 种填充简笔小妖 ═════════════════════
// cell 128×96，主体中心 x=48、脚底 ~90；每帧闭包 (pen, t)。

/** 落影脚注。 */
function shadow(pen, x = 48, rx = 12) {
  pen.ellipse(x, 90.5, rx, 3, { fill: SHADOW, stroke: "none", w: 0, alpha: 0.15 });
}

const ENEMIES = {
  // 0 游魂：胖幽灵（波浪裙摆 + 白肚 + 垂手小蹼）
  enemy_basic_walk(pen, t) {
    const bob = Math.sin(t * TAU) * 2.5;
    shadow(pen, 48, 10);
    const top = [];
    for (let i = 0; i <= 6; i++) {
      const a = Math.PI + (i / 6) * Math.PI;
      top.push([48 + Math.cos(a) * 19, 44 + bob - Math.sin(a) * 19]);
    }
    pen.blob([
      ...top,
      [67, 70 + bob], [61, 78 + bob], [55, 72 + bob], [48, 80 + bob],
      [41, 72 + bob], [35, 78 + bob], [29, 70 + bob],
    ], { fill: "#a9bade", jitter: 0.3 });
    pen.ellipse(48, 58 + bob, 9.5, 11, { fill: "#e6d8b8", stroke: "none", w: 0 });
    pen.shine(48, 32 + bob, 12, 12);
    pen.eye(41.5, 44 + bob, 2.4);
    pen.eye(54.5, 44 + bob, 2.4);
    pen.arc(48, 52 + bob, 3.5, 20, 160, { w: 1.8 });
    pen.blob([[26, 52 + bob], [20, 58 + bob], [26, 62 + bob], [30, 56 + bob]], { fill: "#a9bade", w: 2.4 });
    pen.blob([[70, 52 + bob], [76, 58 + bob], [70, 62 + bob], [66, 56 + bob]], { fill: "#a9bade", w: 2.4 });
  },
  // 1 魅影：飘移长影（斜长身 + 速度残线 + 血瞳）
  enemy_fast_walk(pen, t) {
    const sway = Math.sin(t * TAU) * 4;
    shadow(pen, 48, 9);
    pen.blob([
      [48 + sway * 0.3, 10], [62, 26], [64, 44], [52 + sway * 0.5, 58],
      [56 + sway, 80], [50, 68], [40, 54], [36, 30], [40, 16],
    ], { fill: "#b0a0e0", jitter: 0.3 });
    pen.shine(48 + sway * 0.3, 22, 8, 10);
    pen.eye(45 + sway * 0.3, 22, 2.2, { color: BLOOD });
    pen.eye(54 + sway * 0.3, 26, 2.2, { color: BLOOD });
    for (let i = 0; i < 3; i++) {
      const al = 0.5 - i * 0.13;
      pen.line([[62 + i * 2, 34 + i * 14], [74 + i * 2, 38 + i * 14]], { color: PENCIL, w: 2.4, alpha: al });
    }
  },
  // 2 石魅：岩石巨婴（石块身 + 裂纹 + 粗臂短腿）
  enemy_tank_walk(pen, t) {
    const step = Math.sin(t * TAU);
    shadow(pen, 48, 15);
    pen.blob([[27, 44], [34, 24], [52, 18], [67, 30], [70, 50], [60, 66], [38, 66], [26, 56]], { fill: "#a5977a", jitter: 0.45 });
    pen.shine(48, 30, 13, 9);
    pen.line([[36, 34], [44, 30], [50, 36]], { w: 2, alpha: 0.55 });
    pen.line([[56, 56], [62, 52]], { w: 2, alpha: 0.5 });
    pen.line([[36, 40], [43, 44]], { w: 3 });
    pen.line([[60, 40], [53, 44]], { w: 3 });
    pen.eye(41, 48, 2.2);
    pen.eye(55, 48, 2.2);
    pen.line([[43, 56], [53, 56]], { w: 2.2 });
    pen.line([[40, 66], [38 + step * 3, 84]], { w: 6 });
    pen.line([[56, 66], [58 - step * 3, 84]], { w: 6 });
    pen.line([[22, 46], [12, 54 + step * 4]], { w: 5.5 });
    pen.line([[74, 46], [84, 54 - step * 4]], { w: 5.5 });
  },
  // 3 爆魈：圆鼓炸弹（引线火花 + 高光 + 蠢萌咧嘴）
  enemy_exploder_walk(pen, t) {
    const pulse = 1 + Math.sin(t * TAU) * 0.08;
    const r = 18 * pulse, cy = 56;
    shadow(pen, 48, 14 * pulse);
    pen.ellipse(48, cy, r, r, { fill: "#dcab6c" });
    pen.shine(48, cy - 6, r * 0.75, r * 0.75);
    pen.line([[48, cy - r + 1], [53, cy - r - 8]], { w: 3 });
    const sa = t * TAU;
    for (let i = 0; i < 3; i++) {
      const a = sa + (i / 3) * TAU;
      pen.line([[55, cy - r - 10], [55 + Math.cos(a) * 4, cy - r - 10 + Math.sin(a) * 4]], { color: GOLD, w: 2.2 });
    }
    pen.eye(42, cy - 3, 2.4);
    pen.eye(54, cy - 3, 2.4);
    pen.arc(48, cy + 4, 6.5, 15, 165, { w: 2.2 });
    pen.line([[40, cy + r - 3], [37, cy + r + 8]], { w: 4.5 });
    pen.line([[56, cy + r - 3], [59, cy + r + 8]], { w: 4.5 });
  },
  // 4 隐煞：虚线幽灵（只有轮廓与血红双瞳）
  enemy_invisible_walk(pen, t) {
    const bob = Math.sin(t * TAU + 1) * 2.5;
    const top = [];
    for (let i = 0; i <= 6; i++) {
      const a = Math.PI + (i / 6) * Math.PI;
      top.push([48 + Math.cos(a) * 18, 44 + bob - Math.sin(a) * 18]);
    }
    pen.blob([
      ...top,
      [66, 70 + bob], [55, 76 + bob], [48, 80 + bob], [41, 76 + bob], [30, 70 + bob],
    ], { fill: "none", dash: "8 7", w: 2.6, jitter: 0.3 });
    pen.dot(42, 44 + bob, 2.2, { color: BLOOD });
    pen.dot(54, 44 + bob, 2.2, { color: BLOOD });
  },
  // 5 瘴妖：毒滴怪（斜眼獠牙 + 下毒滴）
  enemy_spitter_walk(pen, t) {
    const bob = Math.sin(t * TAU) * 1.5;
    shadow(pen, 48, 12);
    pen.blob([
      [48, 24], [60, 42], [64, 56 + bob], [58, 68 + bob], [48, 72 + bob],
      [38, 68 + bob], [32, 56 + bob], [36, 42],
    ], { fill: "#9dbb72" });
    pen.shine(48, 38, 7, 9);
    pen.line([[42, 46 + bob], [48, 49 + bob]], { w: 2.4 });
    pen.line([[54, 46 + bob], [48, 49 + bob]], { w: 2.4 });
    pen.eye(43, 51 + bob, 2);
    pen.eye(53, 51 + bob, 2);
    pen.arc(48, 58 + bob, 4.5, 200, 340, { w: 2 });
    pen.line([[45, 61 + bob], [44, 65 + bob]], { w: 1.8, color: "#ffffff", alpha: 0.9 });
    const dy = (t * 16) % 16;
    pen.blob([[57, 70 + dy], [60, 74 + dy], [57, 78 + dy], [54, 74 + dy]], { fill: "#82a054", w: 1.8 });
    pen.line([[40, 72 + bob], [38, 84]], { w: 4 });
    pen.line([[54, 72 + bob], [56, 84]], { w: 4 });
  },
  // 6 愈妖：暖胖天使（眯眼笑 + 光环 + 金星环绕 + 红晕）
  enemy_healer_walk(pen, t) {
    const bob = Math.sin(t * TAU) * 2;
    shadow(pen, 48, 13);
    pen.ellipse(48, 58 + bob, 17, 20, { fill: "#e3bd80" });
    pen.shine(48, 48 + bob, 10, 13);
    pen.ellipse(48, 42 + bob, 7, 3.5, { fill: "none", stroke: GOLD, w: 2.4 });
    pen.arc(42, 52 + bob, 3.6, 195, 345, { w: 2.2 });
    pen.arc(54, 52 + bob, 3.6, 195, 345, { w: 2.2 });
    pen.arc(48, 60 + bob, 3, 25, 155, { w: 1.8 });
    pen.dot(37, 60 + bob, 2.2, { color: "#e8a090" });
    pen.dot(59, 60 + bob, 2.2, { color: "#e8a090" });
    for (let i = 0; i < 3; i++) {
      const a = t * TAU + (i / 3) * TAU;
      const gx = 48 + Math.cos(a) * 27, gy = 56 + bob + Math.sin(a) * 24;
      pen.line([[gx - 3.5, gy], [gx + 3.5, gy]], { color: GOLD, w: 2.6, alpha: 0.9 });
      pen.line([[gx, gy - 3.5], [gx, gy + 3.5]], { color: GOLD, w: 2.6, alpha: 0.9 });
    }
    pen.line([[42, 77 + bob], [41, 86]], { w: 4 });
    pen.line([[54, 77 + bob], [55, 86]], { w: 4 });
  },
  // 7 唤灵：尖帽巫（宽袍 + 血瞳 + 咒符手杖）
  enemy_summoner_walk(pen, t) {
    const sway = Math.sin(t * TAU) * 2;
    shadow(pen, 48, 13);
    pen.blob([[38, 40], [30, 62], [28 + sway, 82], [40 + sway, 78], [48, 82], [56 + sway, 78], [68 + sway, 82], [66, 62], [58, 40]], { fill: "#ad8fd8" });
    pen.blob([[33, 30], [48, 6], [63, 30]], { fill: "#9f82cc" });
    pen.line([[30, 31], [66, 31]], { w: 2.8 });
    pen.shine(44, 60, 9, 12);
    pen.eye(43, 38, 2.2, { color: BLOOD });
    pen.eye(53, 38, 2.2, { color: BLOOD });
    pen.line([[60, 46], [68, 24]], { w: 3 });
    pen.ellipse(69, 19, 5.5, 5.5, { fill: "#c8b0ec", w: 2.4 });
    pen.dot(69, 19, 1.8, { color: BLOOD });
    pen.line([[56, 48], [62, 44]], { w: 3.4 });
  },
  // 8 弩妖：矮壮弩手（尖耳 + 獠牙 + 十字弩）
  enemy_archer_walk(pen, t) {
    const bob = Math.sin(t * TAU) * 1.5;
    shadow(pen, 48, 12);
    pen.blob([[38, 44], [32, 58 + bob], [36, 72 + bob], [60, 72 + bob], [64, 58 + bob], [58, 44]], { fill: "#c6a878" });
    pen.blob([[37, 37], [29, 31], [38, 33]], { fill: "#c6a878", w: 2.2 });
    pen.blob([[59, 37], [67, 31], [58, 33]], { fill: "#c6a878", w: 2.2 });
    pen.shine(46, 50 + bob, 8, 9);
    pen.eye(42, 52 + bob, 2.1);
    pen.eye(52, 52 + bob, 2.1);
    pen.line([[40, 60 + bob], [44, 63 + bob], [48, 61 + bob], [52, 63 + bob], [56, 60 + bob]], { w: 1.8 });
    pen.line([[58, 56 + bob], [76, 50 + bob]], { w: 3.4 });
    pen.arc(76, 50 + bob, 9, 110, 250, { w: 2.6, n: 8 });
    pen.line([[58, 56 + bob], [74, 51 + bob]], { w: 1.4, alpha: 0.7 });
    pen.line([[36, 72 + bob], [34, 85]], { w: 4.5 });
    pen.line([[56, 72 + bob], [58, 85]], { w: 4.5 });
  },
  // 9 浮灯：灯笼精（提梁 + 血焰灯芯 + 穗）
  enemy_ship_walk(pen, t) {
    const bob = Math.sin(t * TAU) * 3.5;
    shadow(pen, 48, 10);
    pen.blob([
      [36, 38 + bob], [60, 38 + bob], [64, 54 + bob], [60, 70 + bob], [36, 70 + bob], [32, 54 + bob],
    ], { fill: "#dbb45e", jitter: 0.3 });
    pen.arc(48, 36 + bob, 7, 180, 360, { w: 2.6, n: 8 });
    pen.line([[40, 38 + bob], [40, 70 + bob]], { w: 1.8, alpha: 0.5 });
    pen.line([[56, 38 + bob], [56, 70 + bob]], { w: 1.8, alpha: 0.5 });
    pen.line([[34, 38 + bob], [62, 38 + bob]], { w: 2.8 });
    pen.line([[34, 70 + bob], [62, 70 + bob]], { w: 2.8 });
    pen.blob([[48, 46 + bob], [53, 56 + bob], [48, 64 + bob], [43, 56 + bob]], { fill: BLOOD, w: 2 });
    pen.shine(41, 44 + bob, 6, 8);
    pen.eye(42, 50 + bob, 1.9);
    pen.eye(54, 50 + bob, 1.9);
    pen.line([[45, 71 + bob], [44, 79 + bob]], { w: 2.2, alpha: 0.85 });
    pen.line([[51, 71 + bob], [52, 80 + bob]], { w: 2.2, alpha: 0.85 });
  },
  // 10 铁傀：装甲巨人（方甲 + 血瞳眼罩 + 天线 + 铆钉拳臂）
  enemy_mech_walk(pen, t) {
    const bob = Math.abs(Math.sin(t * TAU)) * 2;
    const y = 40 + bob;
    shadow(pen, 48, 15);
    pen.blob([[28, y], [68, y], [68, y + 34], [28, y + 34]], { fill: "#8fa2bc", jitter: 0.35 });
    pen.line([[28, y + 10], [68, y + 10]], { w: 2, alpha: 0.5 });
    pen.shine(38, y + 6, 8, 6);
    pen.blob([[36, y + 15], [60, y + 15], [60, y + 26], [36, y + 26]], { fill: INK, w: 2.4 });
    pen.dot(48, y + 20.5, 3, { color: BLOOD });
    pen.dot(48, y + 20.5, 1.1, { color: "#ffffff" });
    pen.line([[48, y], [48, y - 9]], { w: 2.6 });
    pen.dot(48, y - 11, 2.2, { color: BLOOD });
    for (const [rx, ry] of [[33, y + 5], [63, y + 5], [33, y + 29], [63, y + 29]]) pen.dot(rx, ry, 1.5, { color: PENCIL });
    pen.line([[36, y + 34], [34, 84]], { w: 5.5 });
    pen.line([[60, y + 34], [62, 84]], { w: 5.5 });
    pen.line([[20, y + 8], [28, y + 14]], { w: 5 });
    pen.line([[76, y + 8], [68, y + 14]], { w: 5 });
  },
  // 11 甲妖：持盾武卒（圆盔 + 血徽大盾 + 推盾步）
  enemy_knight_walk(pen, t) {
    const bob = Math.sin(t * TAU) * 1.5;
    const push = Math.sin(t * TAU) * 2.5;
    shadow(pen, 48, 12);
    pen.blob([[40, 40], [36, 60 + bob], [42, 76 + bob], [56, 76 + bob], [62, 60 + bob], [58, 40]], { fill: "#9cb2cc" });
    pen.ellipse(48, 32 + bob, 13, 12, { fill: "#9cb2cc" });
    pen.shine(44, 28 + bob, 8, 7);
    pen.arc(48, 30 + bob, 13, 180, 360, { w: 3, n: 10 });
    pen.line([[48, 20 + bob], [48, 26 + bob]], { w: 2.6, alpha: 0.9 });
    pen.line([[41, 32 + bob], [55, 32 + bob]], { w: 2.4 });
    pen.eye(44, 36 + bob, 1.9);
    pen.eye(52, 36 + bob, 1.9);
    const sx = 63 + push;
    pen.blob([[sx, 38], [sx + 10, 44], [sx + 10, 64], [sx, 72], [sx - 10, 64], [sx - 10, 44]], { fill: "#8aa4c0" });
    pen.ellipse(sx, 54, 4, 6, { fill: BLOOD, stroke: "none", w: 0, alpha: 0.9 });
    pen.line([[54, 48 + bob], [sx - 10, 50]], { w: 3.4 });
    pen.line([[44, 76 + bob], [42, 86]], { w: 4.5 });
    pen.line([[54, 76 + bob], [56, 86]], { w: 4.5 });
  },
  // 12 蛟：青鳞蛇妖（粗身 S 蠕动 + 白腹 + 鹿角头）
  enemy_dragon_walk(pen, t) {
    const ph = t * TAU;
    shadow(pen, 40, 12);
    const pts = [];
    for (let i = 0; i <= 14; i++) {
      const u = i / 14;
      pts.push([44 + Math.sin(u * 5.2 + ph) * (3 + u * 10), 86 - u * 58]);
    }
    pen.line(pts, { color: "#8fae7e", width: 13 });
    pen.line(pts, { color: "#a8bc86", width: 5, alpha: 0.55 });
    const [hx, hy] = pts[14];
    pen.ellipse(hx + 5, hy - 4, 11, 8, { fill: "#84a862" });
    pen.shine(hx + 3, hy - 7, 6, 4);
    pen.line([[hx + 2, hy - 9], [hx - 2, hy - 15]], { w: 2.4 });
    pen.line([[hx + 8, hy - 10], [hx + 10, hy - 16]], { w: 2.4 });
    pen.eye(hx + 8, hy - 5, 2.1, { color: BLOOD });
    pen.line([[hx + 14, hy - 3], [hx + 17, hy - 2]], { color: BLOOD, w: 1.6 });
    for (let i = 3; i <= 11; i += 4) {
      const [fx, fy] = pts[i];
      pen.line([[fx - 5, fy - 3], [fx - 9, fy - 8]], { w: 2.2, alpha: 0.85 });
    }
  },
  // 13 蚀群：墨点虫群（圆点家族 + 领头双白瞳）
  enemy_swarm_walk(pen, t) {
    const rnd = rng32(4242);
    const ph = t * TAU;
    shadow(pen, 48, 13);
    for (let i = 0; i < 13; i++) {
      const bx = 48 + (rnd() - 0.5) * 28;
      const by = 54 + (rnd() - 0.5) * 30;
      const wob = Math.sin(ph + i * 2.3) * 3;
      const r = 2.6 + rnd() * 3.2;
      if (i < 7) pen.dot(bx + wob, by + wob * 0.5, r, { color: INK });
      else pen.ellipse(bx - wob * 0.7, by, r + 1.5, r + 1, { fill: "none", w: 2.2, alpha: 0.85 });
    }
    pen.dot(43, 48, 2.4, { color: INK });
    pen.dot(43, 48, 0.8, { color: "#ffffff" });
    pen.dot(55, 54, 2.4, { color: INK });
    pen.dot(55, 54, 0.8, { color: "#ffffff" });
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
async function genTitle() {
  const W = 800, H = 340;
  const parts = [];
  // 血月
  parts.push(`<g>${(() => { const p = makePen(60602);
    p.ellipse(660, 84, 44, 44, { fill: "#d08c74", stroke: BLOOD, w: 4 });
    p.arc(648, 76, 16, 200, 330, { color: BLOOD, w: 2.2, alpha: 0.5, n: 8 });
    p.arc(668, 94, 12, 200, 330, { color: BLOOD, w: 2, alpha: 0.4, n: 7 });
    p.line([[590, 112], [628, 104], [664, 112], [706, 102]], { width: 2.4, alpha: 0.6 });
    return p.body.join(""); })()}</g>`);
  // 远山
  parts.push(`<g>${(() => { const p = makePen(60603);
    p.line([[30, 210], [130, 168], [220, 196], [330, 152], [430, 190], [540, 160], [660, 192], [770, 172]], { width: 2.6, alpha: 0.55 });
    return p.body.join(""); })()}</g>`);
  // 地面
  parts.push(`<g>${(() => { const p = makePen(60604);
    p.line([[40, 292], [760, 288]], { width: 3 });
    const rnd = rng32(60605);
    for (let i = 0; i < 9; i++) {
      const x = 70 + rnd() * 660, y = 288 + rnd() * 6, s2 = 7 + rnd() * 6;
      for (let k = -1; k <= 1; k++) p.line([[x, y], [x + k * s2 * 0.8, y - s2 * (1 - Math.abs(k) * 0.3)]], { width: 2, alpha: 0.5 });
    }
    return p.body.join(""); })()}</g>`);
  // 除妖师（举剑势）
  {
    const p = makePen(60610);
    drawPlayer(p, { pose: "attack", t: 0.18, weapon: "warrior" });
    parts.push(`<g transform="translate(78,92) scale(2.4)">${p.body.join("")}</g>`);
  }
  // 三妖
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
  // 飘符
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
  // 簇拥构图：8 个簇心各落 2-4 笔（草丛/小石），替代均匀散点——克制而不空
  const clusters = 8;
  for (let ci = 0; ci < clusters; ci++) {
    const cx = 140 + rnd() * (W - 280), cy = 140 + rnd() * (H - 280);
    const marks = 2 + Math.floor(rnd() * 3);
    for (let k = 0; k < marks; k++) {
      const x = cx + (rnd() - 0.5) * 120, y = cy + (rnd() - 0.5) * 90;
      if (rnd() < 0.62) {
        const s2 = 6 + rnd() * 6;
        for (let b = -1; b <= 1; b++) pen.line([[x, y], [x + b * s2 * 0.8, y - s2 * (1 - Math.abs(b) * 0.3)]], { color: PENCIL, width: 2, alpha: 0.42 });
      } else {
        const r = 3.5 + rnd() * 4;
        pen.ellipse(x, y, r, r * 0.8, { fill: "none", stroke: PENCIL, width: 2, alpha: 0.38, n: 8 });
      }
    }
  }
  // 一条横穿画面的铅笔小径（有意的构图线）
  pen.line([[180, 980], [640, 900], [1180, 960], [1720, 880], [2260, 950]], { color: PENCIL, width: 3, alpha: 0.22, jitter: 0.8 });
  // 墓园语义（第 1 幕地表可辨识）：5 座铅笔墓碑 + 2 根符纸木桩，克制的背景层
  for (let i = 0; i < 5; i++) {
    const gx = 220 + rnd() * (W - 440), gy = 160 + rnd() * (H - 320);
    const s = 0.8 + rnd() * 0.5;
    // 圆头墓碑（淡纸灰填充 + 铅笔描边）
    pen.blob([[gx - 14 * s, gy], [gx - 14 * s, gy - 30 * s], [gx, gy - 40 * s], [gx + 14 * s, gy - 30 * s], [gx + 14 * s, gy]],
      { fill: "#e2dbc9", stroke: PENCIL, w: 2.4, alpha: 0.8, jitter: 0.5 });
    pen.line([[gx - 6 * s, gy - 26 * s], [gx + 6 * s, gy - 26 * s]], { color: PENCIL, w: 1.6, alpha: 0.5 });
    pen.line([[gx - 6 * s, gy - 18 * s], [gx + 6 * s, gy - 18 * s]], { color: PENCIL, w: 1.4, alpha: 0.4 });
  }
  for (let i = 0; i < 2; i++) {
    const gx = 220 + rnd() * (W - 440), gy = 160 + rnd() * (H - 320);
    // 木桩 + 飘符（血红点睛，呼应除妖主题）
    pen.line([[gx, gy], [gx, gy - 46]], { color: "#9a8468", w: 3.4, alpha: 0.75 });
    pen.blob([[gx - 8, gy - 46], [gx + 8, gy - 46], [gx + 6, gy - 18], [gx - 6, gy - 18]],
      { fill: "#f1e6d0", stroke: PENCIL, w: 2, alpha: 0.85, jitter: 0.35 });
    pen.line([[gx, gy - 40], [gx, gy - 24]], { color: BLOOD, w: 1.8, alpha: 0.6 });
  }
  let noise = "";
  for (let i = 0; i < 1100; i++) {
    const x = (rnd() * W).toFixed(0), y = (rnd() * H).toFixed(0);
    const a = (0.012 + rnd() * 0.02).toFixed(3);
    noise += `<rect x="${x}" y="${y}" width="${rnd() > 0.5 ? 2 : 1}" height="1" fill="#6b6154" opacity="${a}"/>`;
  }
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}">
    <rect width="${W}" height="${H}" fill="#efe7d4"/>
    ${noise}
    ${pen.body.join("")}
    <radialGradient id="v" cx="50%" cy="50%" r="72%">
      <stop offset="82%" stop-color="rgba(90,66,40,0)"/><stop offset="100%" stop-color="rgba(90,66,40,0.05)"/>
    </radialGradient>
    <rect width="${W}" height="${H}" fill="url(#v)"/>
  </svg>`;
  const file = path.join(SRC, "sprites", "backgrounds", "ground_cemetery.jpg");
  await sharp(Buffer.from(svg)).jpeg({ quality: 88 }).toFile(file);
  console.log(`[gen] ${path.relative(PROJECT_ROOT, file)}  (速写纸地面 ${W}×${H})`);
}

// ════════════════════ 场景物件：速写纸风（与角色同语言）════════════════════
// 画布 288×224 透明底，接地线 y≈184（渲染锚点 0.5/0.82）。
// 配色沿用哑光纸感：叶=淡橄榄、石=纸灰、木=暖纸棕，血红只做点睛。

const PROP_CANVAS = { W: 288, H: 224, GROUND: 184 };
const LEAF = "#b6bf97";       // 淡橄榄（冠/灌）
const STONE = "#d3ccbd";      // 纸灰（石）
const WOOD = "#b3906a";       // 暖纸棕（木）

async function propPng(body, file) {
  const { W, H } = PROP_CANVAS;
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}">${body.join("\n")}</svg>`;
  fs.mkdirSync(path.dirname(file), { recursive: true });
  await sharp(Buffer.from(svg)).png().toFile(file);
  console.log(`[gen] ${path.relative(PROJECT_ROOT, file)}  (场景物件 ${W}×${H})`);
}

function drawProp(name) {
  const { GROUND } = PROP_CANVAS;
  const pen = makePen({ tree: 4100, rock: 4200, bush: 4300, stone_block: 4400,
    altar: 4500, altar_used: 4600, chest: 4700, talisman: 4800,
    mine_tiejili: 4900, mine_jishu: 5000, mine_huolei: 5100 }[name]);
  const body = [];
  // 通用落影（与渲染层矢量影一致口径）
  const shadowW = name === "tree" ? 34 : name === "chest" || name.startsWith("mine") ? 20 : 26;
  pen.ellipse(144, GROUND, shadowW, 5, { fill: SHADOW, stroke: "none", w: 0, alpha: 0.16 });

  if (name === "tree") {
    // 干（微锥梯形）
    pen.blob([[136, GROUND - 2], [138, 122], [150, 122], [152, GROUND - 2]], { fill: WOOD, w: 3 });
    pen.line([[141, 150], [147, 148]], { color: INK, w: 1.6, alpha: 0.5 });
    // 冠：三球叠 + 左上高光 + 血红果点
    pen.ellipse(118, 98, 22, 18, { fill: LEAF, w: 2.6 });
    pen.ellipse(170, 98, 22, 18, { fill: LEAF, w: 2.6 });
    pen.ellipse(144, 84, 42, 36, { fill: LEAF, w: 3.2 });
    pen.shine(138, 76, 30, 24, { alpha: 0.45 });
    pen.dot(128, 92, 2.2, { color: BLOOD });
    pen.dot(162, 100, 2.2, { color: BLOOD });
  } else if (name === "rock") {
    // 顽石：不规则纸灰色块 + 裂纹 + 高光
    pen.blob([[104, GROUND - 2], [112, 128], [140, 110], [174, 122], [186, GROUND - 2]],
      { fill: STONE, w: 3.2, jitter: 0.5 });
    pen.line([[132, 142], [146, 132], [152, 138]], { w: 1.8, alpha: 0.55 });
    pen.shine(136, 150, 30, 18, { a0: -160, a1: -100, alpha: 0.4 });
  } else if (name === "bush") {
    // 灌丛三球 + 血红果
    pen.ellipse(114, GROUND - 20, 24, 22, { fill: LEAF, w: 2.6 });
    pen.ellipse(174, GROUND - 20, 24, 22, { fill: LEAF, w: 2.6 });
    pen.ellipse(144, GROUND - 34, 30, 27, { fill: LEAF, w: 3 });
    pen.shine(136, GROUND - 42, 20, 14, { alpha: 0.4 });
    pen.dot(134, GROUND - 30, 2.2, { color: BLOOD });
    pen.dot(158, GROUND - 24, 2.2, { color: BLOOD });
  } else if (name === "stone_block") {
    // 石砖：主块 + 砖缝 + 高光
    pen.blob([[100, GROUND - 2], [100, 122], [188, 122], [188, GROUND - 2]], { fill: STONE, w: 3.2, jitter: 0.4 });
    pen.line([[100, 152], [188, 152]], { w: 1.8, alpha: 0.55 });
    pen.line([[144, 152], [144, GROUND - 2]], { w: 1.5, alpha: 0.45 });
    pen.line([[122, 122], [122, 136]], { w: 1.5, alpha: 0.4 });
    pen.line([[166, 136], [166, 152]], { w: 1.5, alpha: 0.4 });
    pen.shine(132, 140, 26, 12, { alpha: 0.4 });
  } else if (name === "altar" || name === "altar_used") {
    // 融合祭坛：双层石台 + 血红符阵（用后符文褪为铅笔灰）
    const rune = name === "altar" ? BLOOD : PENCIL;
    const rw = name === "altar" ? 2.6 : 1.8;
    const rAlpha = name === "altar" ? 1 : 0.6;
    pen.blob([[96, GROUND - 2], [96, 160], [192, 160], [192, GROUND - 2]], { fill: STONE, w: 3, jitter: 0.35 });
    pen.blob([[110, 160], [110, 130], [178, 130], [178, 160]], { fill: "#ddd5c5", w: 3, jitter: 0.35 });
    pen.ellipse(144, 118, 34, 12, { fill: "#e8e0d0", w: 2.6 });
    // 符阵：圆环 + 中心符文 + 刻度
    pen.ellipse(144, 116, 22, 8, { fill: "none", stroke: rune, w: rw, alpha: rAlpha, n: 12 });
    pen.line([[144, 110], [144, 122]], { color: rune, w: rw, alpha: rAlpha });
    pen.line([[138, 113], [138, 119]], { color: rune, w: rw * 0.7, alpha: rAlpha });
    pen.line([[150, 113], [150, 119]], { color: rune, w: rw * 0.7, alpha: rAlpha });
    pen.dot(144, 116, 2, { color: rune });
  } else if (name === "chest") {
    // 宝箱：暖纸棕箱体 + 弧盖 + 血红锁
    pen.blob([[100, GROUND - 2], [100, 140], [188, 140], [188, GROUND - 2]], { fill: WOOD, w: 3.2, jitter: 0.35 });
    pen.blob([[100, 140], [104, 112], [184, 112], [188, 140]], { fill: "#c49b72", w: 3.2, jitter: 0.35 });
    pen.line([[100, 140], [188, 140]], { w: 2.4 });
    pen.line([[112, 122], [112, 138]], { w: 1.6, alpha: 0.5 });
    pen.line([[176, 122], [176, 138]], { w: 1.6, alpha: 0.5 });
    pen.blob([[136, 138], [136, 158], [152, 158], [152, 138]], { fill: BLOOD, w: 2.4, jitter: 0.2 });
    pen.dot(144, 148, 2, { color: "#f5eede" });
  } else if (name === "talisman") {
    // 符箓：飘符纸（血红符文竖条）
    pen.blob([[130, GROUND - 2], [128, 92], [160, 92], [158, GROUND - 2]], { fill: "#f1e6d0", w: 2.8, jitter: 0.3 });
    pen.line([[144, 104], [144, 168]], { color: BLOOD, w: 2.2, alpha: 0.85 });
    pen.arc(144, 120, 10, -50, 50, { color: BLOOD, w: 1.8, alpha: 0.75 });
    pen.line([[138, 100], [150, 100]], { color: BLOOD, w: 1.8, alpha: 0.75 });
  } else {
    // 地雷三兄弟：圆雷体 + 顶针 + 状态灯（铁吉力=钢灰 / 吉术=暖金纹 / 火雷=血红）
    const skin = name === "mine_huolei" ? "#c46a5a" : name === "mine_jishu" ? "#c8b183" : "#a8a199";
    const lamp = name === "mine_jishu" ? GOLD : BLOOD;
    pen.ellipse(144, GROUND - 22, 26, 22, { fill: skin, w: 3 });
    pen.line([[144, GROUND - 44], [144, GROUND - 52]], { w: 3.4 });
    pen.dot(144, GROUND - 54, 2.6, { color: lamp });
    for (const a of [-135, -90, -45, 0, 45, 135, 180]) {
      const rad = deg(a), r1 = 26, r2 = 32;
      pen.line([[144 + Math.cos(rad) * r1, GROUND - 22 + Math.sin(rad) * r1 * 0.85],
                [144 + Math.cos(rad) * r2, GROUND - 22 + Math.sin(rad) * r2 * 0.85]], { w: 2.4, alpha: 0.85 });
    }
    pen.shine(136, GROUND - 30, 12, 8, { alpha: 0.45 });
  }
  return pen.body;
}

async function genProps() {
  const names = ["tree", "rock", "bush", "stone_block", "altar", "altar_used", "chest",
    "talisman", "mine_tiejili", "mine_jishu", "mine_huolei"];
  for (const n of names) {
    await propPng(drawProp(n), path.join(SRC, "sprites", "icons", "props", `${n}.png`));
  }
}

async function main() {
  console.log("[gen] 简笔画资产生成开始…");
  await genTitle();
  await genGround();
  await genProps();
  await genPlayers();
  await genEnemies();
  console.log("[gen] 全部完成。请执行 npm run assets:sync 生成 public/assets。");
}

main().catch((e) => { console.error(e); process.exit(1); });
