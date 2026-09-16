// 纯像素操作库：直译自 Godot 版 src/core/TextureUtils.cs 的离线预处理规则。
// 所有函数操作 {data:Uint8ClampedArray(RGBA), width, height}，返回新对象，不修改输入。

export const NORMAL_SPRITE_SIZE = 96;

/**
 * @typedef {{data: Uint8ClampedArray, width: number, height: number}} Img
 * @typedef {{x: number, y: number, w: number, h: number}} Rect
 */

const clamp01 = (v) => (v < 0 ? 0 : v > 1 ? 1 : v);

/** @returns {[number, number, number, number]} */
export function getPixel(/** @type {Img} */ img, x, y) {
  const i = (y * img.width + x) * 4;
  return [img.data[i] / 255, img.data[i + 1] / 255, img.data[i + 2] / 255, img.data[i + 3] / 255];
}

function setPixel(/** @type {Img} */ img, x, y, r, g, b, a) {
  const i = (y * img.width + x) * 4;
  img.data[i] = Math.round(clamp01(r) * 255);
  img.data[i + 1] = Math.round(clamp01(g) * 255);
  img.data[i + 2] = Math.round(clamp01(b) * 255);
  img.data[i + 3] = Math.round(clamp01(a) * 255);
}

function classifyEdgePixel(c, threshold, counters) {
  if (c[3] < 0.01) return;
  const brightness = (c[0] + c[1] + c[2]) / 3;
  if (brightness > threshold) counters.bright++;
  else if (brightness < 1 - threshold) counters.dark++;
}

/**
 * 剥白底/黑底（增强版）：
 * 1) 边缘采样背景亮度，自适应下调阈值应对米色/牛皮纸暗底；
 * 2) 裁剪最外层微小边框杂质；
 * 3) 纯黑与高亮低饱和背景置透明。
 * @param {Img} src
 * @returns {Img}
 */
export function stripBackground(src, defaultThreshold = 0.85) {
  const img = { data: new Uint8ClampedArray(src.data), width: src.width, height: src.height };
  const { width: w, height: h } = img;

  const offset = Math.min(15, Math.floor(Math.min(w, h) / 8));
  let edgeBrightTotal = 0;
  let edgeCount = 0;
  for (let x = 0; x < w; x += 4) {
    const [r1, g1, b1] = getPixel(img, x, offset);
    const [r2, g2, b2] = getPixel(img, x, h - 1 - offset);
    edgeBrightTotal += (r1 + g1 + b1) / 3 + (r2 + g2 + b2) / 3;
    edgeCount += 2;
  }
  for (let y = 0; y < h; y += 4) {
    const [r1, g1, b1] = getPixel(img, offset, y);
    const [r2, g2, b2] = getPixel(img, w - 1 - offset, y);
    edgeBrightTotal += (r1 + g1 + b1) / 3 + (r2 + g2 + b2) / 3;
    edgeCount += 2;
  }
  const avgEdgeBright = edgeBrightTotal / (edgeCount || 1);
  const threshold = Math.min(defaultThreshold, Math.max(0.68, avgEdgeBright - 0.05));

  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const [r, g, b, a] = getPixel(img, x, y);
      if (r < 0.12 && g < 0.12 && b < 0.12) {
        setPixel(img, x, y, r, g, b, 0);
        continue;
      }
      const distToBorder = Math.min(x, y, w - 1 - x, h - 1 - y);
      if (distToBorder <= 6 && (r + g + b) / 3 > 0.6) {
        setPixel(img, x, y, r, g, b, 0);
        continue;
      }

      const brightness = (r + g + b) / 3;
      const minChannel = Math.min(r, g, b);
      if (brightness > threshold && minChannel > threshold - 0.12) {
        const maxChannel = Math.max(r, g, b);
        const sat = maxChannel - minChannel;
        if (sat < 0.08) {
          const alpha = sat < 0.02 ? 0 : clamp01((1 - brightness) / (1 - threshold));
          setPixel(img, x, y, r, g, b, alpha * a);
        }
      }
    }
  }
  return img;
}

/** 裁透明边（直译 _TrimTransparent，2px margin）。 */
export function trimTransparent(/** @type {Img} */ src, margin = 2) {
  const { width: w, height: h } = src;
  let minX = w, minY = h, maxX = 0, maxY = 0;
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      if (getPixel(src, x, y)[3] >= 0.01) {
        if (x < minX) minX = x;
        if (x > maxX) maxX = x;
        if (y < minY) minY = y;
        if (y > maxY) maxY = y;
      }
    }
  }
  if (maxX < minX || maxY < minY) return src;
  minX = Math.max(0, minX - margin);
  minY = Math.max(0, minY - margin);
  maxX = Math.min(w - 1, maxX + margin);
  maxY = Math.min(h - 1, maxY + margin);
  return crop(src, minX, minY, maxX - minX + 1, maxY - minY + 1);
}

export function crop(/** @type {Img} */ src, x0, y0, w, h) {
  const out = { data: new Uint8ClampedArray(w * h * 4), width: w, height: h };
  for (let y = 0; y < h; y++) {
    const srcRow = ((y0 + y) * src.width + x0) * 4;
    out.data.set(src.data.subarray(srcRow, srcRow + w * 4), y * w * 4);
  }
  return out;
}

/** 每隔 4 像素采样，判断是否几乎全透明（直译 _IsMostlyTransparent）。 */
export function isMostlyTransparent(/** @type {Img} */ img) {
  for (let x = 0; x < img.width; x += 4) {
    for (let y = 0; y < img.height; y += 4) {
      if (getPixel(img, x, y)[3] >= 0.01) return false;
    }
  }
  return true;
}

/** 连通域查找（用于斜向排列或复杂多对象分割）。 */
function findConnectedRegions(img, minArea = 1600) {
  const { width: w, height: h } = img;
  const visited = new Uint8Array(w * h);
  const regions = [];

  for (let y = 0; y < h; y += 2) {
    for (let x = 0; x < w; x += 2) {
      const idx = y * w + x;
      if (visited[idx]) continue;
      const [r, g, b, a] = getPixel(img, x, y);
      if (a < 0.1) continue;

      let minX = x, maxX = x, minY = y, maxY = y;
      const queue = [x, y];
      visited[idx] = 1;

      while (queue.length > 0) {
        const cy = queue.pop();
        const cx = queue.pop();
        if (cx < minX) minX = cx;
        if (cx > maxX) maxX = cx;
        if (cy < minY) minY = cy;
        if (cy > maxY) maxY = cy;

        const neighbors = [
          [cx + 3, cy], [cx - 3, cy], [cx, cy + 3], [cx, cy - 3],
          [cx + 3, cy + 3], [cx - 3, cy - 3], [cx + 3, cy - 3], [cx - 3, cy + 3]
        ];
        for (const [nx, ny] of neighbors) {
          if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
          const nidx = ny * w + nx;
          if (visited[nidx]) continue;
          visited[nidx] = 1;
          if (getPixel(img, nx, ny)[3] >= 0.1) {
            queue.push(nx, ny);
          }
        }
      }

      const rw = maxX - minX + 1;
      const rh = maxY - minY + 1;
      if (rw * rh >= minArea && rw >= 24 && rh >= 24) {
        regions.push({ x: minX, y: minY, w: rw, h: rh });
      }
    }
  }
  regions.sort((a, b) => {
    if (Math.abs(a.y - b.y) > 60) return a.y - b.y;
    return a.x - b.x;
  });
  return regions;
}

/** 投影分割精灵区域（增强版：行密度过滤、阴影过滤、波谷切割与连通域兜底）。 @returns {Rect[]} */
export function findSpriteRegions(/** @type {Img} */ img) {
  const { width: w, height: h } = img;
  const bright = 0.1;
  const result = [];

  const contentRow = new Array(h).fill(0);
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const [r, g, b, a] = getPixel(img, x, y);
      if (a > 0.05 && (r > bright || g > bright || b > bright)) contentRow[y]++;
    }
  }

  let bandStart = -1;
  for (let y = 0; y <= h; y++) {
    const hasContent = y < h && contentRow[y] > 5;
    if (hasContent && bandStart < 0) bandStart = y;
    else if (!hasContent && bandStart >= 0) {
      splitBand(img, bandStart, y - 1, bright, result);
      bandStart = -1;
    }
  }

  // 阴影细条过滤
  const filtered = result.filter(r => !(r.h <= 35 && (r.w / r.h) >= 4));

  if (filtered.length <= 1) {
    const connected = findConnectedRegions(img);
    if (connected.length > 1) return connected;
  }

  return filtered;
}

function splitBand(img, y0, y1, bright, result) {
  const { width: w } = img;
  const colDensity = new Array(w).fill(0);
  for (let x = 0; x < w; x++) {
    for (let y = y0; y <= y1; y++) {
      const [r, g, b, a] = getPixel(img, x, y);
      if (a > 0.05 && (r > bright || g > bright || b > bright)) colDensity[x]++;
    }
  }

  let colStart = -1;
  const subCols = [];
  for (let x = 0; x <= w; x++) {
    const hasCol = x < w && colDensity[x] >= 4;
    if (hasCol && colStart < 0) colStart = x;
    else if (!hasCol && colStart >= 0) {
      subCols.push({ start: colStart, end: x - 1 });
      colStart = -1;
    }
  }

  for (const col of subCols) {
    const span = col.end - col.start + 1;
    if (span > w * 0.5) {
      const splits = findValleySplits(colDensity, col.start, col.end, 3);
      let last = col.start;
      for (const s of splits) {
        addRect(img, last, s, y0, y1, result);
        last = s + 1;
      }
      addRect(img, last, col.end, y0, y1, result);
    } else {
      addRect(img, col.start, col.end, y0, y1, result);
    }
  }
}

function findValleySplits(density, start, end, expectedParts = 3) {
  const len = end - start + 1;
  const partLen = len / expectedParts;
  const splits = [];
  for (let p = 1; p < expectedParts; p++) {
    const target = Math.round(start + p * partLen);
    const window = Math.round(partLen * 0.35);
    let minVal = Infinity;
    let minIdx = target;
    for (let x = target - window; x <= target + window; x++) {
      if (density[x] < minVal) {
        minVal = density[x];
        minIdx = x;
      }
    }
    splits.push(minIdx);
  }
  return splits;
}

function addRect(img, x0, x1, y0, y1, result) {
  const pad = 3;
  const rx = Math.max(0, x0 - pad);
  const ry = Math.max(0, y0 - pad);
  const rx2 = Math.min(img.width - 1, x1 + pad);
  const ry2 = Math.min(img.height - 1, y1 + pad);
  const rw = rx2 - rx + 1;
  const rh = ry2 - ry + 1;
  if (rw >= NORMAL_SPRITE_SIZE / 4 && rh >= NORMAL_SPRITE_SIZE / 4) {
    result.push({ x: rx, y: ry, w: rw, h: rh });
  }
}

/** 等距取样（直译 _TakeFramesEvenly），保证首尾覆盖。 */
export function takeFramesEvenly(/** @type {T[]} */ src, want) {
  if (src.length === 0 || want <= 0) return [];
  if (src.length <= want) return [...src];
  const out = [];
  for (let i = 0; i < want; i++) {
    const idx = Math.round((i * (src.length - 1)) / (want - 1));
    out.push(src[Math.min(idx, src.length - 1)]);
  }
  return out;
}
