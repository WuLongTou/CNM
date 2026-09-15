// 世界静态实体：障碍/资源节点/祭坛。布局由注入的随机源生成（同 seed 同世界）。

export interface Obstacle {
  id: number;
  x: number; y: number;
  r: number;
  kind: "rock" | "tree" | "bush" | "stone_block";
  /** 可采集节点血量（tree 3 / rock 5 / bush 1，对齐 ResourceNodeHealth*）；0 = 不可破坏。 */
  hp: number;
  dead: boolean;
}

export interface Altar {
  id: number;
  x: number; y: number;
  used: boolean;
}

export interface WorldSpawnRng {
  range(min: number, max: number): number;
}

export class World {
  obstacles: Obstacle[] = [];
  altars: Altar[] = [];

  constructor(rng: WorldSpawnRng, worldSize: { w: number; h: number }, nextId: () => number) {
    // 障碍：12 个（3 石块 + 4 树 + 3 灌木 + 2 岩石），避开中心出生安全区（对齐 MapSpawnSafeRadius）
    const cx = worldSize.w / 2, cy = worldSize.h / 2;
    const safeR = 320;
    let tries = 0;
    const kinds: Array<{ kind: Obstacle["kind"]; hp: number; r: number; n: number }> = [
      { kind: "tree", hp: 3, r: 20, n: 5 },
      { kind: "rock", hp: 5, r: 18, n: 4 },
      { kind: "bush", hp: 1, r: 14, n: 3 },
      { kind: "stone_block", hp: 0, r: 34, n: 3 },
    ];
    for (const k of kinds) {
      for (let i = 0; i < k.n && tries < 500; i++) {
        tries++;
        const x = rng.range(60, worldSize.w - 60);
        const y = rng.range(60, worldSize.h - 60);
        if ((x - cx) ** 2 + (y - cy) ** 2 < safeR ** 2) { i--; continue; }
        this.obstacles.push({ id: nextId(), x, y, r: k.r, kind: k.kind, hp: k.hp, dead: false });
      }
    }
    // 祭坛 2 座：对角布置
    this.altars.push({ id: nextId(), x: worldSize.w * 0.2, y: worldSize.h * 0.2, used: false });
    this.altars.push({ id: nextId(), x: worldSize.w * 0.8, y: worldSize.h * 0.8, used: false });
  }

  /** 圆形实体碰撞推离（简单圆-圆分离）。 */
  separate(x: number, y: number, radius: number): { x: number; y: number } {
    let nx = x, ny = y;
    for (const o of this.obstacles) {
      if (o.dead) continue;
      const dx = nx - o.x, dy = ny - o.y;
      const d = Math.hypot(dx, dy);
      const minD = o.r + radius;
      if (d < minD && d > 0.001) {
        nx = o.x + (dx / d) * minD;
        ny = o.y + (dy / d) * minD;
      }
    }
    return { x: nx, y: ny };
  }
}
