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
 * 剥白底/黑底（直译 _StripWhiteBackground，threshold=0.85）：
 * 1) 采样内圈边缘统计背景明暗倾向；2) 纯黑像素(R,G,B<0.12)一律置透明；
 * 3) 亮底图额外剥离低饱和浅灰/白，有色彩的高光按亮度渐变保留。
 * @param {Img} src
 * @returns {Img}
 */
export function stripBackground(src, threshold = 0.85) {
  const img = { data: new Uint8ClampedArray(src.data), width: src.width, height: src.height };
  const { width: w, height: h } = img;

  const offset = Math.min(10, Math.floor(Math.min(w, h) / 4));
  const counters = { dark: 0, bright: 0 };
  const innerW = w - 2 * offset;
  const innerH = h - 2 * offset;
  const stepX = Math.max(1, Math.floor(innerW / 64));
  const stepY = Math.max(1, Math.floor(innerH / 64));
  for (let x = offset; x < w - offset; x += stepX) {
    classifyEdgePixel(getPixel(img, x, offset), threshold, counters);
    classifyEdgePixel(getPixel(img, x, h - 1 - offset), threshold, counters);
  }
  for (let y = offset; y < h - offset; y += stepY) {
    classifyEdgePixel(getPixel(img, offset, y), threshold, counters);
    classifyEdgePixel(getPixel(img, w - 1 - offset, y), threshold, counters);
  }
  const stripBright = counters.bright > counters.dark;

  for (let x = 0; x < w; x++) {
    for (let y = 0; y < h; y++) {
      const [r, g, b, a] = getPixel(img, x, y);
      if (r < 0.12 && g < 0.12 && b < 0.12) {
        setPixel(img, x, y, r, g, b, 0);
        continue;
      }
      if (stripBright) {
        const brightness = (r + g + b) / 3;
        const minChannel = Math.min(r, g, b);
        if (brightness > threshold && minChannel > threshold - 0.1) {
          const maxChannel = Math.max(r, g, b);
          const sat = maxChannel - minChannel;
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

/** 投影分割精灵区域（直译 _FindSpriteRegions + _SplitBand）。 @returns {Rect[]} */
export function findSpriteRegions(/** @type {Img} */ img) {
  const { width: w, height: h } = img;
  const bright = 0.1;
  /** @type {Rect[]} */
  const result = [];

  const contentRow = new Array(h).fill(false);
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const [r, g, b, a] = getPixel(img, x, y);
      if (a > 0.01 && (r > bright || g > bright || b > bright)) { contentRow[y] = true; break; }
    }
  }

  let bandStart = -1;
  for (let y = 0; y <= h; y++) {
    const content = y < h && contentRow[y];
    if (content && bandStart < 0) bandStart = y;
    else if (!content && bandStart >= 0) {
      splitBand(img, bandStart, y - 1, bright, result);
      bandStart = -1;
    }
  }
  return result;
}

function splitBand(img, y0, y1, bright, result) {
  const { width: w } = img;
  const contentCol = new Array(w).fill(false);
  for (let x = 0; x < w; x++) {
    for (let y = y0; y <= y1; y++) {
      const [r, g, b, a] = getPixel(img, x, y);
      if (a > 0.01 && (r > bright || g > bright || b > bright)) { contentCol[x] = true; break; }
    }
  }
  let colStart = -1;
  for (let x = 0; x <= w; x++) {
    const content = x < w && contentCol[x];
    if (content && colStart < 0) colStart = x;
    else if (!content && colStart >= 0) {
      const pad = 3;
      const rx = Math.max(0, colStart - pad);
      const ry = Math.max(0, y0 - pad);
      const rx2 = Math.min(w - 1, x - 1 + pad);
      const ry2 = Math.min(img.height - 1, y1 + pad);
      const rw = rx2 - rx + 1;
      const rh = ry2 - ry + 1;
      if (rw >= NORMAL_SPRITE_SIZE / 4 && rh >= NORMAL_SPRITE_SIZE / 4)
        result.push({ x: rx, y: ry, w: rw, h: rh });
      colStart = -1;
    }
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
