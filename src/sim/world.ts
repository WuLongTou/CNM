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
    // R228 簇拥地形：8 个簇心（林带/岩石阵/遗迹石阵），每簇 4-7 个障碍——
    // 形成掩体与巷道，让风筝绕柱、采集路线有地形语义，替代均匀撒点。
    // 约束：全部避开中心出生安全区（MapSpawnSafeRadius 320）与祭坛周边。
    const cx = worldSize.w / 2, cy = worldSize.h / 2;
    const safeR = 320;
    this.altars.push({ id: nextId(), x: worldSize.w * 0.2, y: worldSize.h * 0.2, used: false });
    this.altars.push({ id: nextId(), x: worldSize.w * 0.8, y: worldSize.h * 0.8, used: false });
    const nearAltar = (x: number, y: number) =>
      this.altars.some((a) => (a.x - x) ** 2 + (a.y - y) ** 2 < 140 ** 2);
    type ClusterKind = "grove" | "rockfield" | "ruins";
    const kindsOf = (c: ClusterKind): Array<{ kind: Obstacle["kind"]; hp: number; r: number }> =>
      c === "grove"
        ? [{ kind: "tree", hp: 3, r: 20, }, { kind: "tree", hp: 3, r: 22 }, { kind: "tree", hp: 3, r: 19 },
           { kind: "bush", hp: 1, r: 14 }, { kind: "bush", hp: 1, r: 13 }]
        : c === "rockfield"
        ? [{ kind: "rock", hp: 5, r: 18 }, { kind: "rock", hp: 5, r: 17 }, { kind: "rock", hp: 5, r: 20 },
           { kind: "stone_block", hp: 0, r: 34 }]
        : [{ kind: "stone_block", hp: 0, r: 34 }, { kind: "stone_block", hp: 0, r: 30 },
           { kind: "rock", hp: 5, r: 16 }];
    const clusterKinds: ClusterKind[] = ["grove", "rockfield", "ruins", "grove", "rockfield", "grove", "ruins", "rockfield"];
    const centers: Array<{ x: number; y: number }> = [];
    for (let ci = 0; ci < clusterKinds.length; ci++) {
      // 簇心：远离中心安全区、远离已定簇心（≥300）与祭坛（≥140）
      let x = 0, y = 0, ok = false;
      for (let t = 0; t < 80 && !ok; t++) {
        x = rng.range(180, worldSize.w - 180);
        y = rng.range(180, worldSize.h - 180);
        ok = (x - cx) ** 2 + (y - cy) ** 2 >= (safeR + 60) ** 2
          && centers.every((c) => (c.x - x) ** 2 + (c.y - y) ** 2 >= 300 ** 2)
          && !nearAltar(x, y);
      }
      centers.push({ x, y });
      const parts = kindsOf(clusterKinds[ci]);
      parts.forEach((p, pi) => {
        // 簇内散布：半径 90 内环绕簇心（带最小间距，防全叠一起）
        for (let t = 0; t < 24; t++) {
          const a = rng.range(0, Math.PI * 2), d = 26 + (pi / parts.length) * 80 + rng.range(0, 26);
          const x2 = x + Math.cos(a + pi * 1.7) * d, y2 = y + Math.sin(a + pi * 1.7) * d * 0.8;
          if (x2 < 40 || y2 < 40 || x2 > worldSize.w - 40 || y2 > worldSize.h - 40) continue;
          if ((x2 - cx) ** 2 + (y2 - cy) ** 2 < safeR ** 2) continue;
          if ((x2 - x) ** 2 + (y2 - y) ** 2 < 20 ** 2 && pi > 0) continue;
          this.obstacles.push({ id: nextId(), x: x2, y: y2, r: p.r, kind: p.kind, hp: p.hp, dead: false });
          break;
        }
      });
    }
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
