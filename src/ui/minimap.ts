// 小地图：世界俯视缩略（右上角纸片），纸白底 + 炭框；妖物红点/宝箱金点/
// 祭坛紫点/障碍灰点/玩家炭点/符箓血色菱形。

import type { GameSim } from "../sim/game";

const W = 148;
const H = W * (2160 / 3840);

export class MiniMap {
  private canvas: HTMLCanvasElement;
  private ctx: CanvasRenderingContext2D;
  private frame = 0;

  constructor(private sim: () => GameSim) {
    this.canvas = document.createElement("canvas");
    this.canvas.width = W;
    this.canvas.height = H;
    this.canvas.id = "minimap";
    document.getElementById("ui-root")!.appendChild(this.canvas);
    this.ctx = this.canvas.getContext("2d")!;
  }

  update(): void {
    const sim = this.sim();
    // R19：非战斗态（欢迎/结算）隐藏小地图——半透明遮罩下会透出来抢戏
    const inCombat = sim.state === "Combat";
    this.canvas.style.display = inCombat ? "" : "none";
    if (!inCombat) return;
    // 15fps 节流即可
    if (++this.frame % 4 !== 0) return;
    const g = this.ctx;
    const sx = W / sim.world.w;
    const sy = H / sim.world.h;
    g.clearRect(0, 0, W, H);
    g.fillStyle = "rgba(245,238,222,0.92)";
    g.fillRect(0, 0, W, H);
    g.strokeStyle = "#332e29";
    g.lineWidth = 1.5;
    g.strokeRect(0.75, 0.75, W - 1.5, H - 1.5);
    // 视口框
    g.lineWidth = 1;
    g.strokeStyle = "rgba(51,46,41,0.5)";
    g.strokeRect(this.camX * sx, this.camY * sy, this.vw * sx, this.vh * sy);
    // 点位
    for (const o of sim.terrain.obstacles) {
      if (o.dead) continue;
      g.fillStyle = "rgba(107,97,84,0.55)";
      g.fillRect(o.x * sx - 1, o.y * sy - 1, 2, 2);
    }
    for (const a of sim.terrain.altars) {
      if (a.used) continue;
      g.fillStyle = "#8e44ad";
      g.fillRect(a.x * sx - 2, a.y * sy - 2, 4, 4);
    }
    for (const c of sim.chests) {
      if (c.opened) continue;
      // R8 精英箱：血红大方块（普通箱为金小方块，符箓为血红菱形，三者可辨）
      if (c.elite) {
        g.fillStyle = "#c0392b";
        g.fillRect(c.x * sx - 2, c.y * sy - 2, 4, 4);
      } else {
        g.fillStyle = "#d9a520";
        g.fillRect(c.x * sx - 1.5, c.y * sy - 1.5, 3, 3);
      }
    }
    // R65 香火龛：纸金圆点（区别于宝箱方块与符箓菱形，远寻可辨）
    g.fillStyle = "#e8d5a8";
    g.strokeStyle = "#332e29";
    for (const s of sim.shrines) {
      g.beginPath();
      g.arc(s.x * sx, s.y * sy, 2.6, 0, Math.PI * 2);
      g.fill();
      g.stroke();
    }
    // R20 回春丹：绿十字点（与符箓/宝箱三者可辨）
    g.fillStyle = "#6ab04c";
    for (const o of sim.healOrbs) {
      g.fillRect(o.x * sx - 1.5, o.y * sy - 1.5, 3, 3);
    }
    // 升级符箓：血红菱形（纸底上醒目，区别于宝箱金方块），屏幕外掉落也可寻
    g.fillStyle = "#c0392b";
    for (const u of sim.pickups) {
      g.beginPath();
      g.moveTo(u.x * sx, u.y * sy - 3);
      g.lineTo(u.x * sx + 3, u.y * sy);
      g.lineTo(u.x * sx, u.y * sy + 3);
      g.lineTo(u.x * sx - 3, u.y * sy);
      g.fill();
    }
    for (const e of sim.enemies) {
      // R14 宝妖：亮金大点（妖王古铜/精英橙红/普通血红之外的一眼可辨）
      g.fillStyle = e.goblin ? "#ffd94d" : e.boss ? "#a8741a" : e.elite ? "#d97b2a" : "#c0392b";
      const s = e.boss ? 4 : e.goblin ? 3.5 : 2.5;
      g.fillRect(e.x * sx - s / 2, e.y * sy - s / 2, s, s);
    }
    if (!sim.player.dead) {
      g.fillStyle = "#332e29";
      g.fillRect(sim.player.x * sx - 2, sim.player.y * sy - 2, 4, 4);
    }
  }

  private camX = 0;
  private camY = 0;
  private vw = 0;
  private vh = 0;
  /** main 每帧同步相机位置与视口尺寸（供视口框绘制）。 */
  setCamera(x: number, y: number, viewW: number, viewH: number): void {
    this.camX = x;
    this.camY = y;
    this.vw = viewW;
    this.vh = viewH;
  }
}
