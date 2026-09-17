// PixiJS 渲染层：只读 sim 状态绘制，不修改任何逻辑数据。
// 地面=ground 大图血色调制平铺；实体=预切 96×96 atlas 帧动画；相机=指数平滑跟随。

import { Application, Assets, Container, Graphics, Sprite, Text, Texture } from "pixi.js";
import type { GameSim, Enemy } from "../sim/game";
import { CFG } from "../sim/config";

/** 妖物类型 → 预切 atlas 名（assets/sprites/enemies/{name}.json）。 */
const ENEMY_ATLAS: Record<number, string> = {
  0: "enemy_basic_walk", 1: "enemy_fast_walk", 2: "enemy_tank_walk",
  3: "enemy_exploder_walk", 4: "enemy_invisible_walk", 5: "enemy_spitter_walk",
  6: "enemy_healer_walk", 7: "enemy_summoner_walk", 8: "enemy_archer_walk",
  9: "enemy_ship_walk", 10: "enemy_mech_walk", 11: "enemy_knight_walk", 12: "enemy_dragon_walk", 13: "enemy_swarm_walk",
};

const CAMERA_SMOOTHING = 6.5; // 对齐 GameConfig.CameraSmoothing
const WALK_ANIM_STEP = 0.06;  // 对齐 GameConfig.WalkAnimSpeed

interface EnemyView {
  container: Container;
  body: Sprite;
  hpFill: Sprite;
  hpBar: Container;
  animName: string;
  frame: number;
  animTimer: number;
}

export class GameView {
  private app!: Application;
  private worldLayer = new Container();
  private playerSprite!: Sprite;
  private playerWalkFrames: Texture[] = [];
  private playerIdle!: Texture;
  private enemyViews = new Map<number, EnemyView>();
  private enemyTextures = new Map<string, Texture[]>();
  private camX = 0;
  private camY = 0;
  private animTimer = 0;
  private frameIndex = 0;

  /** 加载全部资产并搭建场景。 */
  async init(app: Application): Promise<void> {
    this.app = app;
    app.stage.addChild(this.worldLayer);

    // 地面：对齐 C# 版 Background——整张地面纹理拉伸覆盖世界 + 血色暗调调制
    // （不用 1920 PNG 平铺：该图自带暗角，拼接处会叠出黑十字）
    const groundTex = await Assets.load<Texture>("assets/sprites/backgrounds/ground_cemetery.jpg");
    const ground = new Sprite(groundTex);
    ground.width = 3840; ground.height = 2160;
    ground.tint = 0xb86b6b; // ≈ 0.72/0.42/0.42 的血色乘法调制
    this.worldLayer.addChild(ground);

    // 玩家帧（warrior walk 8 帧 + idle 首帧）
    this.playerWalkFrames = await this.loadFrames(`assets/sprites/player/warrior_walk.json`);
    const idleFrames = await this.loadFrames(`assets/sprites/player/warrior_idle.json`);
    this.playerIdle = idleFrames[0];
    this.playerSprite = new Sprite(this.playerIdle);
    this.playerSprite.anchor.set(0.5);
    this.worldLayer.addChild(this.playerSprite);

    // 预加载全部妖物 atlas
    for (const name of new Set(Object.values(ENEMY_ATLAS))) {
      this.enemyTextures.set(name, await this.loadFrames(`assets/sprites/enemies/${name}.json`));
    }
  }

  /** 加载 TexturePacker atlas，按 filename 中的帧序号排序返回纹理数组。 */
  private async loadFrames(atlasUrl: string): Promise<Texture[]> {
    const sheet = await Assets.load<import("pixi.js").Spritesheet>(atlasUrl);
    const entries = Object.entries(sheet.textures);
    entries.sort((a, b) => {
      const na = Number(a[0].match(/_(\d+)$/)?.[1] ?? 0);
      const nb = Number(b[0].match(/_(\d+)$/)?.[1] ?? 0);
      return na - nb;
    });
    return entries.map(([, t]) => t);
  }

  private makeEnemyView(z: Enemy): EnemyView {
    const animName = ENEMY_ATLAS[z.type] ?? "enemy_basic_walk";
    const frames = this.enemyTextures.get(animName) ?? this.enemyTextures.get("enemy_basic_walk")!;
    const container = new Container();
    const body = new Sprite(frames[0]);
    body.anchor.set(0.5);
    container.addChild(body);

    const hpBar = new Container();
    const bg = new Sprite(Texture.WHITE);
    bg.tint = 0x3a1010; bg.width = 30; bg.height = 4; bg.x = -15; bg.y = -52;
    const fill = new Sprite(Texture.WHITE);
    fill.tint = 0xc0392b; fill.width = 30; fill.height = 4; fill.x = -15; fill.y = -52;
    hpBar.addChild(bg, fill);
    container.addChild(hpBar);

    if (z.elite || z.boss) {
      const ring = new Sprite(Texture.WHITE);
      ring.tint = z.boss ? 0xffd700 : 0xd9a520;
      ring.alpha = 0.75;
      ring.width = z.boss ? 60 : 40; ring.height = z.boss ? 6 : 4;
      ring.x = -(z.boss ? 30 : 20); ring.y = -60;
      container.addChild(ring);
    }
    if (z.boss) container.scale.set(CFG.Enemy.BossScale);
    this.worldLayer.addChild(container);
    return { container, body, hpFill: fill, hpBar, animName, frame: 0, animTimer: 0 };
  }

  /** 每帧渲染：tick 固定 60Hz 与渲染同频，直接读最新状态。 */
  render(sim: GameSim, dt: number): void {
    // 相机：指数平滑跟随 + 世界边界钳制（对齐 GameCamera 的阻尼跟随）
    const screenW = this.app.screen.width;
    const screenH = this.app.screen.height;
    const k = 1 - Math.exp(-CAMERA_SMOOTHING * dt);
    this.camX += (sim.player.x - screenW / 2 - this.camX) * k;
    this.camY += (sim.player.y - screenH / 2 - this.camY) * k;
    this.camX = Math.min(sim.world.w - screenW, Math.max(0, this.camX));
    this.camY = Math.min(sim.world.h - screenH, Math.max(0, this.camY));
    // 震屏：随机偏移 × 衰减强度
    this.shakePower = Math.max(0, this.shakePower - 16 * this.shakePower * dt);
    const sx = this.shakePower > 0.1 ? (Math.random() * 2 - 1) * this.shakePower : 0;
    const sy = this.shakePower > 0.1 ? (Math.random() * 2 - 1) * this.shakePower : 0;
    this.worldLayer.x = -this.camX + sx;
    this.worldLayer.y = -this.camY + sy;
    this.updateDamageTexts(dt);
    this.updateBursts(dt);

    // 玩家：移动走 walk 帧、静止 idle 帧；死亡隐藏；无敌帧闪烁
    const p = sim.player;
    this.playerSprite.visible = !p.dead;
    if (!p.dead) {
      this.playerSprite.x = p.x; this.playerSprite.y = p.y;
      this.playerSprite.scale.x = p.facing;
      if (p.moving) {
        this.animTimer += dt;
        if (this.animTimer >= WALK_ANIM_STEP) {
          this.animTimer -= WALK_ANIM_STEP;
          this.frameIndex = (this.frameIndex + 1) % this.playerWalkFrames.length;
        }
        this.playerSprite.texture = this.playerWalkFrames[this.frameIndex];
      } else {
        this.animTimer = 0;
        this.frameIndex = 0;
        this.playerSprite.texture = this.playerIdle;
      }
      this.playerSprite.alpha = p.invincible > 0 ? 0.55 + 0.45 * Math.sin(p.invincible * 40) : 1;
    }

    // 妖物：增量同步视图（受击红闪 + HP 条 + 帧动画）
    const alive = new Set<number>();
    for (const z of sim.enemies) {
      alive.add(z.id);
      let v = this.enemyViews.get(z.id);
      if (!v) { v = this.makeEnemyView(z); this.enemyViews.set(z.id, v); }
      v.container.x = z.x; v.container.y = z.y;
      v.body.scale.x = z.facing;
      v.body.tint = z.hitFlash > 0 ? 0xff5544 : 0xffffff;
      // 隐煞隐身：近乎透明（保留一点轮廓提示玩家位置威胁）
      v.container.alpha = z.stealth ? 0.12 : 1;
      const ratio = Math.max(0, z.hp / z.maxHp);
      v.hpFill.width = 30 * ratio;
      v.hpBar.visible = z.hp < z.maxHp;
      if (this.enemyTextures.get(v.animName)!.length > 1) {
        v.animTimer += dt;
        if (v.animTimer >= 0.1) {
          v.animTimer -= 0.1;
          v.frame = (v.frame + 1) % this.enemyTextures.get(v.animName)!.length;
          v.body.texture = this.enemyTextures.get(v.animName)![v.frame];
        }
      }
    }
    for (const [id, v] of this.enemyViews) {
      if (!alive.has(id)) {
        v.container.destroy({ children: true });
        this.enemyViews.delete(id);
      }
    }

    this.renderProjectiles(sim);
    this.renderEnemyProjectiles(sim);
    this.renderMines(sim);
    this.renderPickups(sim);
    this.renderChests(sim);
    this.renderTerrain(sim);
    this.renderWeather(sim);
  }

  /** 弹道：箭=细长条按速度方向旋转；魔法/追踪弹=发光圆点。 */
  private fxLayer = new Container();
  private projGfx = new Graphics();
  private mineGfx = new Graphics();
  private chestSprites = new Map<number, Sprite>();

  private renderProjectiles(sim: GameSim): void {
    const g = this.projGfx;
    if (this.projGfx.parent !== this.fxLayer) this.fxLayer.addChild(g);
    g.clear();
    for (const pr of sim.projectiles) {
      if (pr.homing) {
        g.circle(pr.x, pr.y, 5).fill(0xff9a3d).alpha = 0.9;
        g.circle(pr.x, pr.y, 8).stroke({ width: 2, color: 0xffcf7a, alpha: 0.5 });
      } else {
        const len = 14;
        const sp = Math.hypot(pr.vx, pr.vy) || 1;
        const ux = pr.vx / sp, uy = pr.vy / sp;
        g.moveTo(pr.x - ux * len, pr.y - uy * len)
          .lineTo(pr.x + ux * len * 0.5, pr.y + uy * len * 0.5)
          .stroke({ width: 3, color: 0xe8d8b0, alpha: 0.9 });
      }
    }
    g.alpha = 1;
  }

  private renderMines(sim: GameSim): void {
    const g = this.mineGfx;
    if (this.mineGfx.parent !== this.fxLayer) this.fxLayer.addChild(g);
    g.clear();
    for (const m of sim.mines) {
      g.circle(m.x, m.y, 7).fill(m.arm > 0 ? 0x6b3323 : 0xc0392b).alpha = 0.85;
      g.circle(m.x, m.y, 10).stroke({ width: 1, color: 0xffd28a, alpha: 0.6 });
    }
  }

  /** 敌方弹道：毒弹=绿点 / 箭矢=红色细条（与玩家浅色弹道区分）。 */
  private enemyProjGfx = new Graphics();
  private renderEnemyProjectiles(sim: GameSim): void {
    const g = this.enemyProjGfx;
    if (g.parent !== this.fxLayer) this.fxLayer.addChild(g);
    g.clear();
    for (const pr of sim.enemyProjectiles) {
      if (pr.psn) {
        g.circle(pr.x, pr.y, 5).fill({ color: 0x6ab04c, alpha: 0.9 });
        g.circle(pr.x, pr.y, 8).stroke({ width: 1.5, color: 0x9be07a, alpha: 0.4 });
      } else {
        const sp = Math.hypot(pr.vx, pr.vy) || 1;
        const ux = pr.vx / sp, uy = pr.vy / sp;
        g.moveTo(pr.x - ux * 13, pr.y - uy * 13)
          .lineTo(pr.x + ux * 6, pr.y + uy * 6)
          .stroke({ width: 2.5, color: 0xd9644a, alpha: 0.9 });
      }
    }
  }

  /** 事件扩散圈特效（自爆/治疗/召唤）：一圈快速扩张淡出。 */
  spawnBurst(x: number, y: number, color: number): void {
    if (this.bursts.length > 12) return; // 上限节流
    this.bursts.push({ gfx: new Graphics(), x, y, color, life: 0.45 });
    const b = this.bursts[this.bursts.length - 1];
    this.fxLayer.addChild(b.gfx);
  }

  private bursts: Array<{ gfx: Graphics; x: number; y: number; color: number; life: number }> = [];

  private updateBursts(dt: number): void {
    const alive: typeof this.bursts = [];
    for (const b of this.bursts) {
      b.life -= dt;
      if (b.life <= 0) { b.gfx.destroy(); continue; }
      const t = 1 - b.life / 0.45;
      b.gfx.clear();
      b.gfx.circle(b.x, b.y, 10 + t * 46).stroke({ width: 3, color: b.color, alpha: (1 - t) * 0.8 });
      alive.push(b);
    }
    this.bursts = alive;
  }

  private renderChests(sim: GameSim): void {
    const seen = new Set<number>();
    for (const c of sim.chests) {
      if (c.opened) continue;
      seen.add(c.id);
      let s = this.chestSprites.get(c.id);
      if (!s) {
        s = new Sprite(Texture.WHITE);
        s.tint = 0xd9a520;
        s.width = 26; s.height = 20;
        s.pivot.set(13, 10);
        this.fxLayer.addChild(s);
        this.chestSprites.set(c.id, s);
      }
      s.position.set(c.x, c.y);
      s.visible = true;
    }
    for (const [id, s] of this.chestSprites) {
      if (!seen.has(id)) s.visible = false;
    }
  }

  /** 升级符箓：竖长符纸 + 血红符文 + 光圈，整体轻微上下浮动。 */
  private pickupGfx = new Graphics();
  private renderPickups(sim: GameSim): void {
    const g = this.pickupGfx;
    if (g.parent !== this.fxLayer) this.fxLayer.addChild(g);
    g.clear();
    const bob = Math.sin(performance.now() / 220) * 3;
    for (const u of sim.pickups) {
      g.circle(u.x, u.y + 4, 15).stroke({ width: 1, color: 0xffd28a, alpha: 0.35 });
      g.rect(u.x - 6, u.y - 9 + bob, 12, 18)
        .fill({ color: 0xf2e8d8, alpha: 0.95 })
        .stroke({ width: 1.5, color: 0xc0392b });
      g.circle(u.x, u.y - 3 + bob, 2.5).fill(0xc0392b);
      g.rect(u.x - 3, u.y + 1 + bob, 6, 1.5).fill(0xc0392b);
    }
  }

  /** 天气表现：屏幕空间色调覆盖（雨=冷蓝+雨线 / 雾=灰白蒙尘 / 血月=暗红呼吸脉冲）。 */
  private weatherGfx = new Graphics();
  private renderWeather(sim: GameSim): void {
    const g = this.weatherGfx;
    if (g.parent !== this.app.stage) this.app.stage.addChild(g);
    g.clear();
    const w = sim.weather.state;
    if (w.type === "Clear") return;
    const sw = this.app.screen.width;
    const sh = this.app.screen.height;
    const k = w.intensity === "Moderate" ? 1.5 : 1;
    if (w.type === "Rain") {
      g.rect(0, 0, sw, sh).fill({ color: 0x2a3a55, alpha: 0.16 * k });
      // 雨线：相位随时间下落（i 播撒位置，t 驱动下落循环）
      const t = performance.now() / 1000;
      g.stroke({ width: 1.5, color: 0x9ab0d0, alpha: 0.4 });
      for (let i = 0; i < 34; i++) {
        const bx = ((i * 757) % sw + ((i * 331) % 60)) % sw;
        const y0 = ((t * (520 + (i % 5) * 70) + (i * 137)) % (sh + 120)) - 60;
        g.moveTo(bx, y0).lineTo(bx - 7, y0 + 22);
      }
    } else if (w.type === "Fog") {
      g.rect(0, 0, sw, sh).fill({ color: 0xc8c4bc, alpha: 0.3 * k });
    } else if (w.type === "BloodMoon") {
      const pulse = 0.16 + 0.05 * Math.sin(performance.now() / 450);
      g.rect(0, 0, sw, sh).fill({ color: 0x8e1010, alpha: pulse * k });
    }
  }

  /** fxLayer 需在 init 里挂到 worldLayer 之上；静态障碍/祭坛也在 init 后挂一次。 */
  attachFx(): void {
    this.worldLayer.addChild(this.fxLayer);
  }

  /** 触发震屏（强度≈像素幅度）。 */
  shake(power: number): void {
    this.shakePower = Math.max(this.shakePower, power);
  }

  /** 伤害数字：世界上浮淡出（暴击更大更红）。 */
  spawnDamageNumber(x: number, y: number, dmg: number, crit: boolean): void {
    if (this.damageTexts.length > 24) return; // 上限节流
    const t = new Text({ text: String(dmg), style: {
      fontFamily: "system-ui", fontSize: crit ? 26 : 17, fontWeight: "700",
      fill: crit ? 0xffd24a : 0xff6b52, stroke: { color: 0x140a0a, width: 3 },
    } });
    t.anchor.set(0.5);
    t.position.set(x + (Math.random() * 16 - 8), y - 40);
    this.worldLayer.addChild(t);
    this.damageTexts.push({ t, life: 0.8 });
  }

  private updateDamageTexts(dt: number): void {
    const alive: Array<{ t: Text; life: number }> = [];
    for (const d of this.damageTexts) {
      d.life -= dt;
      if (d.life <= 0) { d.t.destroy(); continue; }
      d.t.y -= 45 * dt;
      d.t.alpha = Math.min(1, d.life / 0.4);
      alive.push(d);
    }
    this.damageTexts = alive;
  }

  /** 当前相机左上角与视口尺寸（小地图用）。 */
  getCamera(): { x: number; y: number; w: number; h: number } {
    return { x: this.camX, y: this.camY, w: this.app.screen.width, h: this.app.screen.height };
  }

  /** 世界坐标 → 屏幕坐标（小地图/外部标注用）。 */
  worldToScreen(x: number, y: number): { x: number; y: number } {
    return { x: x - this.camX, y: y - this.camY };
  }

  private terrainGfx = new Graphics();
  private altarGfx = new Graphics();
  /** 震屏强度（指数衰减，对齐 CameraShakeDecay 16）。 */
  private shakePower = 0;
  private damageTexts: Array<{ t: Text; life: number }> = [];

  /** 障碍与祭坛：每帧低开销重绘（数量少），采集死亡后消失、祭坛用后变暗。 */
  renderTerrain(sim: GameSim): void {
    if (this.terrainGfx.parent !== this.worldLayer) this.worldLayer.addChild(this.terrainGfx);
    if (this.altarGfx.parent !== this.worldLayer) this.worldLayer.addChild(this.altarGfx);
    const g = this.terrainGfx;
    g.clear();
    const COLORS: Record<string, number> = {
      tree: 0x3d6b35, rock: 0x7a6a5a, bush: 0x5a7a3a, stone_block: 0x55483e,
    };
    for (const o of sim.terrain.obstacles) {
      if (o.dead) continue;
      g.circle(o.x, o.y + o.r * 0.4, o.r * 0.9).fill({ color: 0x000000, alpha: 0.25 });
      g.circle(o.x, o.y, o.r).fill(COLORS[o.kind] ?? 0x666666);
      if (o.hp > 0) g.circle(o.x, o.y, o.r * 0.55).fill({ color: 0xffffff, alpha: 0.12 });
    }
    const a = this.altarGfx;
    a.clear();
    for (const alt of sim.terrain.altars) {
      const alpha = alt.used ? 0.25 : 0.8;
      a.circle(alt.x, alt.y, 22).fill({ color: 0x8e44ad, alpha });
      a.circle(alt.x, alt.y, 30).stroke({ width: 3, color: 0xffd28a, alpha: alpha * 0.8 });
      if (!alt.used) {
        a.circle(alt.x, alt.y, 38).stroke({ width: 1, color: 0xffd28a, alpha: 0.35 });
      }
    }
  }
}
