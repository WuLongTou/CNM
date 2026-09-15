// PixiJS 渲染层：只读 sim 状态绘制，不修改任何逻辑数据。
// 地面=ground 大图血色调制平铺；实体=预切 96×96 atlas 帧动画；相机=指数平滑跟随。

import { Application, Assets, Container, Sprite, Texture } from "pixi.js";
import type { GameSim, Enemy } from "../sim/game";
import { CFG } from "../sim/config";

/** 妖物类型 → 预切 atlas 名（assets/sprites/enemies/{name}.json）。 */
const ENEMY_ATLAS: Record<number, string> = {
  0: "enemy_basic_walk", 1: "enemy_fast_walk", 2: "enemy_tank_walk",
  3: "enemy_exploder_walk", 4: "enemy_invisible_walk", 5: "enemy_spitter_walk",
  6: "enemy_healer_walk", 7: "enemy_summoner_walk", 8: "enemy_archer_walk",
  9: "ship_walk", 10: "mech_walk", 11: "knight_walk", 12: "dragon_walk", 13: "swarm_walk",
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
    this.worldLayer.x = -this.camX;
    this.worldLayer.y = -this.camY;

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
  }
}
