// PixiJS 渲染层：只读 sim 状态绘制，不修改任何逻辑数据。
// 简笔画（速写本）风格：米白纸面地面、炭棕线条线稿实体、血红点缀。
// 实体=预切 96×96 atlas 帧动画；相机=指数平滑跟随。

import { Application, Assets, Container, Graphics, Sprite, Text, Texture } from "pixi.js";
import type { GameSim, Enemy } from "../sim/game";
import { CFG, EnemyType, ENEMY_NAMES } from "../sim/config";

/** 妖物类型 → 预切 atlas 名（assets/sprites/enemies/{name}.json）。 */
const ENEMY_ATLAS: Record<number, string> = {
  0: "enemy_basic_walk", 1: "enemy_fast_walk", 2: "enemy_tank_walk",
  3: "enemy_exploder_walk", 4: "enemy_invisible_walk", 5: "enemy_spitter_walk",
  6: "enemy_healer_walk", 7: "enemy_summoner_walk", 8: "enemy_archer_walk",
  9: "enemy_ship_walk", 10: "enemy_mech_walk", 11: "enemy_knight_walk", 12: "enemy_dragon_walk", 13: "enemy_swarm_walk",
};

// ── 简笔画风格调色（与 tools/gen-sketch.mjs 保持一致）──
const INK = 0x332e29;       // 炭棕线条
const INK_SOFT = 0x6b6154;  // 铅笔灰
const BLOOD = 0xc0392b;     // 血红
const PAPER = 0xf5eede;     // 纸白

const TAU = Math.PI * 2;
const CAMERA_SMOOTHING = 6.5; // 对齐 GameConfig.CameraSmoothing
const WALK_ANIM_STEP = 0.06;  // 对齐 GameConfig.WalkAnimSpeed
const ATTACK_ANIM_TIME = 0.3; // 攻击动画总时长（onAttack 触发播一轮）
const HURT_ANIM_TIME = 0.3;   // 受击动画总时长（playHurt 置位）
const DEATH_ANIM_TIME = 0.6;  // 死亡动画总时长（阵亡边沿置位，播完再隐藏）

interface EnemyView {
  container: Container;
  body: Sprite;
  hpFill: Sprite;
  hpBar: Container;
  animName: string;
  frame: number;
  animTimer: number;
  /** 攻击帧动画（AI 资产，缺省 null 时维持程序动作）。 */
  attackFrames: Texture[] | null;
  /** 妖王形态帧（AI 资产，缺省 null 时用 walk 放大）。 */
  bossFrames: Texture[] | null;
  /** 当前播放的帧列表标记（walk/attack/boss），切换时帧序重置。 */
  animKey: string;
  /** 朝向插值（-1..1）。 */
  faceCur: number;
  /** 浮动/弹跳相位（个体错开，破除集体同步）。 */
  bobPhase: number;
  /** 上一帧位置（差分判断移动）。 */
  lastX: number; lastY: number;
  /** 扬尘节流。 */
  dustCd: number;
  /** 死亡碎片参数（最后受击方向 × 精英/妖王加倍）。 */
  hitUx: number; hitUy: number; big: boolean;
}

/** 玩家外观皮肤：对应武器类型（Melee→warrior / Ranged→ranger / Magic→mage）。 */
interface PlayerSkin {
  walk: Texture[];
  attack: Texture[];
  idle: Texture[];
  /** 扩展动作（AI 资产，缺省 null 回退既有动画）。 */
  dash: Texture[] | null;
  hurt: Texture[] | null;
  death: Texture[] | null;
}

/** 浮空妖物：呼吸浮动 + 轻微摇摆（游魂/魅影/隐煞/浮灯/蚀群）。 */
const FLOATY_TYPES = new Set<number>([0, 1, 4, 9, 13]);
/** 远程妖物：出手动作为后坐缩放（瘴妖/弩妖），近战型为前扑位移。 */
const RANGED_TYPES = new Set<number>([5, 8]);
const IDLE_ANIM_STEP = 0.28;
const AFTERIMAGE_LIFE = 0.22;

export class GameView {
  private app!: Application;
  private worldLayer = new Container();
  private playerSprite!: Sprite;
  private playerSkins: Record<string, PlayerSkin> = {};
  private curSkin = "warrior";
  /** 攻击动画剩余时长（playAttack 置位，递减归零回落 walk/idle）。 */
  private attackTimer = 0;
  /** 受击动画剩余时长（playHurt 置位；播完回落）。 */
  private hurtTimer = 0;
  /** 死亡动画剩余时长（阵亡边沿置位，播完再隐藏）。 */
  private deathTimer = 0;
  /** 当前攻击动画是否近战（近战带前顶步，远程站桩）。 */
  private attackIsMelee = true;
  private enemyViews = new Map<number, EnemyView>();
  private enemyTextures = new Map<string, Texture[]>();
  /** 妖物攻击帧（键不含 _walk 后缀）与妖王形态帧（缺资产为 null）。 */
  private enemyAttackTextures = new Map<string, Texture[] | null>();
  private enemyBossTextures = new Map<string, Texture[] | null>();
  /** 场景物件贴图（AI 资产，缺省回退矢量线稿）。 */
  private propTextures = new Map<string, Texture>();
  /** 物件精灵层（贴地，实体之下）与按对象键控的精灵缓存。 */
  private propLayer = new Container();
  private obstacleSprites = new Map<object, Sprite>();
  private altarSprites = new Map<object, Sprite>();
  private chestSprites = new Map<object, Sprite>();
  private pickupSprites = new Map<object, Sprite>();
  private mineSprites = new Map<object, Sprite>();
  /** 地面整图与分幕地面（act2-4 可选资产，缺省沿用幕 1）。 */
  private groundSprite!: Sprite;
  private groundAct1Tex!: Texture;
  private actGroundTextures: Texture[] = [];
  private curAct = -1;
  private camX = 0;
  private camY = 0;
  /** 相机移动前瞻（朝输入方向平滑偏移，行走更带感）。 */
  private lookX = 0;
  private lookY = 0;
  private animTimer = 0;
  private frameIndex = 0;
  /** 上一帧玩家死亡标记（边沿触发死亡消散）。 */
  private prevPlayerDead = false;
  /** 玩家朝向插值（-1..1，转身过渡；sim 的 facing 是瞬时 ±1）。 */
  private playerFace = 1;
  /** 冲刺残影（渐隐拷贝）。 */
  private afterimages: Array<{ spr: Sprite; life: number }> = [];
  private afterimageCd = 0;

  /** 加载全部资产并搭建场景。 */
  async init(app: Application): Promise<void> {
    this.app = app;
    app.stage.addChild(this.worldLayer);

    // ── 资产并发加载:全部请求立即发起,随后按依赖顺序 await 组装 ──
    // PixiJS Assets 同 URL 并发调用内部缓存去重,并发安全。总耗时 ≈ 最慢单请求,
    // 而非逐个串行相加(实测经 CF 串行 40+ 请求要 30s+,并发后 ~2s)。
    const PROP_NAMES = ["tree", "rock", "bush", "stone_block", "altar", "altar_used", "chest",
      "talisman", "mine_tiejili", "mine_jishu", "mine_huolei"] as const;
    const ROLES = ["warrior", "ranger", "mage"] as const;
    const ACT_GROUNDS = ["ground_act2", "ground_act3", "ground_act4"] as const;
    const enemyNames = [...new Set(Object.values(ENEMY_ATLAS))];

    const groundP = Assets.load<Texture>("assets/sprites/backgrounds/ground_cemetery.jpg");
    const actGroundP = new Map(ACT_GROUNDS.map((f) =>
      [f, this.loadTextureOptional(`assets/sprites/backgrounds/${f}.jpg`)] as const));
    const propP = new Map(PROP_NAMES.map((n) =>
      [n, this.loadTextureOptional(`assets/sprites/icons/props/${n}.png`)] as const));
    const playerP = new Map(ROLES.map((who) => {
      const walk = this.loadFrames(`assets/sprites/player/${who}_walk.json`);
      const attack = this.loadFrames(`assets/sprites/player/${who}_attack.json`);
      const idle = this.loadFrames(`assets/sprites/player/${who}_idle.json`);
      const dash = this.loadFramesOptional(`assets/sprites/player/${who}_dash.json`);
      const hurt = this.loadFramesOptional(`assets/sprites/player/${who}_hurt.json`);
      const death = this.loadFramesOptional(`assets/sprites/player/${who}_death.json`);
      return [who, { walk, attack, idle, dash, hurt, death }] as const;
    }));
    const enemyWalkP = new Map(enemyNames.map((n) =>
      [n, this.loadFrames(`assets/sprites/enemies/${n}.json`)] as const));
    const enemyOptP = new Map(enemyNames.map((n) => {
      const base = n.replace(/_walk$/, "");
      return [base, {
        attack: this.loadFramesOptional(`assets/sprites/enemies/${base}_attack.json`),
        boss: this.loadFramesOptional(`assets/sprites/enemies/${base}_boss.json`),
      }] as const;
    }));

    // 等齐全部请求(必选失败自然 throw → boot 显示启动失败;可选失败已在各 helper 内消化)
    await Promise.all([
      groundP,
      ...actGroundP.values(), ...propP.values(),
      ...ROLES.flatMap((who) => Object.values(playerP.get(who)!)),
      ...enemyWalkP.values(),
      ...[...enemyOptP.values()].flatMap((m) => Object.values(m)),
    ]);

    // ── 组装(依赖顺序与原实现一致) ──
    // 地面:速写纸整图(自带涂鸦/血渍/晕影),原色拉伸铺满,不再叠加血色调制
    const groundTex = await groundP;
    const ground = new Sprite(groundTex);
    ground.width = 3840; ground.height = 2160;
    this.worldLayer.addChild(ground);
    this.groundSprite = ground;
    this.groundAct1Tex = groundTex;
    // 分幕地面(act2-4 可选资产,404/缺失沿用幕 1,不阻塞启动)
    for (const f of ACT_GROUNDS) {
      this.actGroundTextures.push((await actGroundP.get(f)!) ?? groundTex);
    }
    // 物件精灵层贴地(地面之上、实体之下)
    this.worldLayer.addChildAt(this.propLayer, 1);
    // 场景物件贴图(可选;全部缺失时维持矢量线稿渲染)
    for (const name of PROP_NAMES) {
      const tex = await propP.get(name)!;
      if (tex) this.propTextures.set(name, tex);
    }

    // 玩家三套外观(walk 8 帧 / attack 6 帧 / idle 4 帧呼吸)+ 可选扩展动作,按手持武器类型切换
    for (const who of ROLES) {
      const pr = playerP.get(who)!;
      this.playerSkins[who] = {
        walk: await pr.walk,
        attack: await pr.attack,
        idle: await pr.idle,
        dash: await pr.dash,
        hurt: await pr.hurt,
        death: await pr.death,
      };
    }
    this.playerSprite = new Sprite(this.playerSkins.warrior.idle[0]);
    this.playerSprite.anchor.set(0.5);
    this.worldLayer.addChild(this.playerSprite);

    // 预加载全部妖物 atlas
    for (const name of enemyNames) {
      this.enemyTextures.set(name, await enemyWalkP.get(name)!);
    }
    // 妖物攻击帧与妖王形态帧(可选 AI 资产,缺省回退 walk + 程序动作)
    for (const name of enemyNames) {
      const base = name.replace(/_walk$/, "");
      const opt = enemyOptP.get(base)!;
      this.enemyAttackTextures.set(base, await opt.attack);
      this.enemyBossTextures.set(base, await opt.boss);
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

  /** 可选 atlas：AI 资产可能缺某动作，缺省返回 null（回退既有动画）。 */
  private async loadFramesOptional(atlasUrl: string): Promise<Texture[] | null> {
    try { return await this.loadFrames(atlasUrl); } catch { return null; }
  }

  /** 可选整图：缺省返回 null。 */
  private async loadTextureOptional(url: string): Promise<Texture | null> {
    try { return await Assets.load<Texture>(url); } catch { return null; }
  }

  /** 词缀徽标（R17）：头顶 10px 级小符号，一眼可辨七词缀（boss 均为 none，不画）。 */
  private markAffix(container: Container, affix: string): void {
    const my = -70;
    const g = new Graphics();
    switch (affix) {
      case "swift": // 双速线
        g.moveTo(-7, my - 2).lineTo(7, my - 2).stroke({ width: 2, color: INK });
        g.moveTo(-7, my + 2).lineTo(7, my + 2).stroke({ width: 2, color: INK });
        break;
      case "regen": // 绿十字
        g.moveTo(-2.5, my - 5).lineTo(-2.5, my + 5).stroke({ width: 3, color: 0x6ab04c });
        g.moveTo(-6, my - 1.5).lineTo(1, my - 1.5).stroke({ width: 3, color: 0x6ab04c });
        break;
      case "shield": // 淡蓝圈
        g.circle(0, my - 1, 5).stroke({ width: 2.2, color: 0x8ab8d8 });
        break;
      case "explosive": // 血红爆点
        g.circle(0, my - 1, 3.2).fill(BLOOD);
        g.moveTo(-6, my - 1).lineTo(6, my - 1).stroke({ width: 1.6, color: BLOOD });
        g.moveTo(0, my - 7).lineTo(0, my + 5).stroke({ width: 1.6, color: BLOOD });
        break;
      case "frenzy": // 暗红双线
        g.moveTo(-6, my - 2).lineTo(6, my - 2).stroke({ width: 2, color: 0x8e2a20 });
        g.moveTo(-6, my + 2).lineTo(6, my + 2).stroke({ width: 2, color: 0x8e2a20 });
        break;
      case "split": // 一大一小双圈（母子）
        g.circle(-3.5, my - 1, 4).stroke({ width: 2, color: INK });
        g.circle(4, my, 2.4).stroke({ width: 1.8, color: INK });
        break;
      case "blink": // 紫菱形
        g.moveTo(0, my - 6).lineTo(4.5, my - 1).lineTo(0, my + 4).lineTo(-4.5, my - 1).closePath()
          .fill(0x8e44ad);
        break;
      default: return;
    }
    container.addChild(g);
  }

  private makeEnemyView(z: Enemy): EnemyView {
    const animName = ENEMY_ATLAS[z.type] ?? "enemy_basic_walk";
    const base = animName.replace(/_walk$/, "");
    const attackFrames = this.enemyAttackTextures.get(base) ?? null;
    const bossFrames = this.enemyBossTextures.get(base) ?? null;
    const frames = (z.boss && bossFrames) ? bossFrames
      : this.enemyTextures.get(animName) ?? this.enemyTextures.get("enemy_basic_walk")!;
    const container = new Container();
    const body = new Sprite(frames[0]);
    body.anchor.set(0.5);
    container.addChild(body);

    // HP 条：纸白底 + 炭描边 + 血红填充（白纸上清晰）
    const hpBar = new Container();
    const bg = new Sprite(Texture.WHITE);
    bg.tint = PAPER; bg.width = 30; bg.height = 4; bg.x = -15; bg.y = -52;
    const fill = new Sprite(Texture.WHITE);
    fill.tint = BLOOD; fill.width = 30; fill.height = 4; fill.x = -15; fill.y = -52;
    const edge = new Graphics();
    edge.rect(-15.5, -52.5, 31, 5).stroke({ width: 1, color: INK, alpha: 0.8 });
    hpBar.addChild(bg, fill, edge);
    container.addChild(hpBar);

    if (z.elite || z.boss || z.goblin) {
      const ring = new Sprite(Texture.WHITE);
      // R14 宝妖：亮金环（精英暗金、妖王古铜，三者可辨）
      ring.tint = z.boss ? 0xb8860b : z.goblin ? 0xffd94d : 0x9a6a10;
      ring.alpha = 0.9;
      ring.width = z.boss ? 60 : 40; ring.height = z.boss ? 6 : 4;
      ring.x = -(z.boss ? 30 : 20); ring.y = -60;
      container.addChild(ring);
    }
    // R17 词缀徽标：头顶小符号（见词缀知打法；色调沿用既有语义——治疗绿/格挡蓝/召唤紫/危险红）
    if (z.affix !== "none") this.markAffix(container, z.affix);
    if (z.boss) container.scale.set(CFG.Enemy.BossScale);
    this.worldLayer.addChild(container);
    // 相位随机化：每只妖物帧起点/计时/浮动相位错开，避免"集体广场舞"
    return {
      container, body, hpFill: fill, hpBar, animName,
      attackFrames, bossFrames, animKey: "walk",
      frame: Math.floor(Math.random() * frames.length),
      animTimer: Math.random() * 0.1,
      faceCur: z.facing, bobPhase: Math.random() * Math.PI * 2,
      lastX: z.x, lastY: z.y, dustCd: Math.random() * 0.16,
      hitUx: z.lastHitUx, hitUy: z.lastHitUy, big: z.elite || z.boss || z.goblin,
    };
  }

  /** 每帧渲染：tick 固定 60Hz 与渲染同频，直接读最新状态。 */
  render(sim: GameSim, dt: number): void {
    // 相机：指数平滑跟随 + 世界边界钳制（对齐 GameCamera 的阻尼跟随）
    const screenW = this.app.screen.width;
    const screenH = this.app.screen.height;
    const k = 1 - Math.exp(-CAMERA_SMOOTHING * dt);
    // 前瞻：镜头朝移动方向探出一点（横 56 / 纵 40），急停时平滑回中
    this.lookX += (sim.input.moveX * 56 - this.lookX) * Math.min(1, dt * 3);
    this.lookY += (sim.input.moveY * 40 - this.lookY) * Math.min(1, dt * 3);
    this.camX += (sim.player.x + this.lookX - screenW / 2 - this.camX) * k;
    this.camY += (sim.player.y + this.lookY - screenH / 2 - this.camY) * k;
    this.camX = Math.min(sim.world.w - screenW, Math.max(0, this.camX));
    this.camY = Math.min(sim.world.h - screenH, Math.max(0, this.camY));
    // 震屏：随机偏移 × 衰减强度
    this.shakePower = Math.max(0, this.shakePower - 16 * this.shakePower * dt);
    const sx = this.shakePower > 0.1 ? (Math.random() * 2 - 1) * this.shakePower : 0;
    const sy = this.shakePower > 0.1 ? (Math.random() * 2 - 1) * this.shakePower : 0;
    this.worldLayer.x = -this.camX + sx;
    this.worldLayer.y = -this.camY + sy;
    // 分幕换地皮：幕推进时切换地面整图（缺省资产沿用幕 1）
    if (sim.act !== this.curAct) {
      this.curAct = sim.act;
      this.groundSprite.texture = this.curAct > 0
        ? this.actGroundTextures[this.curAct - 1]
        : this.groundAct1Tex;
    }
    this.updateDamageTexts(dt);
    this.updateDebris(dt);
    this.updateBursts(dt);
    this.updateScribbles(dt);
    this.updateRipples(dt);
    this.updateBarrageWarnings(dt);
    this.updateDust(dt);
    this.updateSparks(dt);
    this.updateSlashes(dt);

    // 玩家：移动走 walk 帧、静止 idle 帧；死亡播 death 帧后隐藏+墨迹消散；无敌帧闪烁
    const p = sim.player;
    if (!p.dead) this.deathTimer = 0;
    if (!p.dead && p.invincible <= 0 && this.prevPlayerDead) {
      // 重生/新局：复位消散标记
      this.prevPlayerDead = false;
    }
    if (p.dead && !this.prevPlayerDead && sim.state === "GameOver") {
      // 阵亡瞬间：死亡动画起播 + 大号墨迹消散（双份错位，与妖物消散同语）
      this.deathTimer = DEATH_ANIM_TIME;
      this.spawnDeathScribble(p.x, p.y, 2.4);
      this.spawnDeathScribble(p.x + 10, p.y - 6, 1.6);
      this.prevPlayerDead = true;
    }
    if (p.dead && this.deathTimer > 0 && this.playerSkins[this.curSkin].death) {
      // 死亡动画：播完 death 帧序列再隐藏
      this.deathTimer -= dt;
      const dSkin = this.playerSkins[this.curSkin];
      this.playerSprite.visible = true;
      this.playerSprite.alpha = 1;
      this.playerSprite.position.set(p.x, p.y);
      const dfr = Math.min(dSkin.death!.length - 1,
        Math.floor((1 - this.deathTimer / DEATH_ANIM_TIME) * dSkin.death!.length));
      this.playerSprite.texture = dSkin.death![dfr];
    } else {
      this.playerSprite.visible = !p.dead;
    }
    if (!p.dead) {
      // 皮肤跟随武器类型：近战=除妖师 / 远程=弓手 / 法术=法师
      const want = sim.activeWeapon?.type === "Ranged" ? "ranger"
        : sim.activeWeapon?.type === "Magic" ? "mage" : "warrior";
      if (want !== this.curSkin) {
        this.curSkin = want;
        this.animTimer = 0;
        this.frameIndex = 0;
      }
      const skin = this.playerSkins[this.curSkin];
      this.playerSprite.x = p.x; this.playerSprite.y = p.y;
      // 转身插值：scale.x 快速趋近目标 ±1，翻转有了过程（纸片人转身感）
      this.playerFace += (p.facing - this.playerFace) * Math.min(1, dt * 14);
      this.playerSprite.scale.x = Math.abs(this.playerFace) < 0.08
        ? (this.playerFace < 0 ? -0.08 : 0.08) : this.playerFace;
      if (this.attackTimer > 0) {
        // 攻击动画优先：按帧数均摊播完一轮；近战附带迈步前顶（出手重量感）
        this.attackTimer -= dt;
        const prog = 1 - this.attackTimer / ATTACK_ANIM_TIME;
        const at = Math.min(skin.attack.length - 1, Math.floor(prog * skin.attack.length));
        this.playerSprite.texture = skin.attack[at];
        const lunge = this.attackIsMelee ? Math.sin(prog * Math.PI) * 7 : 0;
        this.playerSprite.x = p.x + p.facing * lunge;
        this.playerSprite.y = p.y;
        this.animTimer = 0;
        this.frameIndex = 0;
      } else if (this.hurtTimer > 0 && skin.hurt) {
        // 受击动画：后仰踉跄两帧，优先于走跑（打击反馈）
        this.hurtTimer -= dt;
        const ht = Math.min(skin.hurt.length - 1,
          Math.floor((1 - Math.max(0, this.hurtTimer) / HURT_ANIM_TIME) * skin.hurt.length));
        this.playerSprite.texture = skin.hurt[ht];
        this.animTimer = 0;
        this.frameIndex = 0;
      } else if (p.dashTimer > 0 && skin.dash) {
        // 冲刺动画：按冲刺剩余时间取帧（前倾疾冲）
        const dfr = Math.min(skin.dash.length - 1,
          Math.floor((1 - p.dashTimer / CFG.Player.DashDuration) * skin.dash.length));
        this.playerSprite.texture = skin.dash[dfr];
        this.animTimer = 0;
        this.frameIndex = 0;
      } else if (p.moving) {
        this.animTimer += dt;
        if (this.animTimer >= WALK_ANIM_STEP) {
          this.animTimer -= WALK_ANIM_STEP;
          this.frameIndex = (this.frameIndex + 1) % skin.walk.length;
        }
        this.playerSprite.texture = skin.walk[this.frameIndex];
        // 行走弹跳：步点轻微起伏（帧动画已有腿摆，这里补重心位移）
        this.playerSprite.y -= Math.abs(Math.sin(this.animTimer / WALK_ANIM_STEP * Math.PI * 0.5)) * 0;
      } else {
        // 站立呼吸：idle 4 帧慢循环（不再僵死单帧）
        this.animTimer += dt;
        if (this.animTimer >= IDLE_ANIM_STEP) {
          this.animTimer -= IDLE_ANIM_STEP;
          this.frameIndex = (this.frameIndex + 1) % skin.idle.length;
        }
        this.playerSprite.texture = skin.idle[this.frameIndex];
      }
      this.playerSprite.alpha = p.invincible > 0 ? 0.55 + 0.45 * Math.sin(p.invincible * 40) : 1;
      // 移动扬尘：地面行进按节拍在脚下冒灰点
      this.playerDustCd -= dt;
      if (p.moving && this.playerDustCd <= 0) {
        this.playerDustCd = p.dashTimer > 0 ? 0.03 : 0.12;
        this.spawnDust(p.x, p.y);
      }
      // 冲刺残影：高速位移期按节拍丢出渐隐拷贝
      this.afterimageCd -= dt;
      if (p.dashTimer > 0 && this.afterimageCd <= 0) {
        this.afterimageCd = 0.045;
        const ghost = new Sprite(this.playerSprite.texture);
        ghost.anchor.set(0.5);
        ghost.position.set(this.playerSprite.x, this.playerSprite.y);
        ghost.scale.copyFrom(this.playerSprite.scale);
        ghost.alpha = 0.32;
        this.worldLayer.addChildAt(ghost, this.worldLayer.getChildIndex(this.playerSprite));
        this.afterimages.push({ spr: ghost, life: AFTERIMAGE_LIFE });
      }
    }
    this.updateAfterimages(dt);

    // 妖物：增量同步视图（受击红闪 + HP 条 + 帧动画）
    const alive = new Set<number>();
    for (const z of sim.enemies) {
      alive.add(z.id);
      let v = this.enemyViews.get(z.id);
      if (!v) { v = this.makeEnemyView(z); this.enemyViews.set(z.id, v); }
      v.container.x = z.x; v.container.y = z.y;
      // 转身插值（与玩家同语）
      v.faceCur += (z.facing - v.faceCur) * Math.min(1, dt * 12);
      v.body.scale.x = Math.abs(v.faceCur) < 0.1 ? (v.faceCur < 0 ? -0.1 : 0.1) : v.faceCur;
      v.body.tint = z.hitFlash > 0 ? 0xff4632 : 0xffffff;
      v.hitUx = z.lastHitUx; v.hitUy = z.lastHitUy;
      // 受击抽搐：红闪期身体小幅抖动（container 定位不动，血条稳定）
      v.body.x = z.hitFlash > 0 ? (Math.random() * 2 - 1) * 3 : 0;
      // 隐煞隐身：近乎透明（保留一点轮廓提示玩家位置威胁）
      v.container.alpha = z.stealth ? 0.12 : 1;
      // 动感：浮空妖物正弦浮动+轻摇摆；地面妖物行走时小弹跳（相位个体错开）
      const now = performance.now() / 1000;
      if (z.hitFlash > 0) {
        // 受击 pop：身体鼓一下再回落（打击的形体反馈，优先于一切动作）
        const pop = 1 + 0.26 * (z.hitFlash / 0.16);
        v.body.scale.set(v.body.scale.x >= 0 ? pop : -pop, pop);
      } else if (z.strikeT > 0) {
        // 出手动作：近战型向玩家扑击（位移+前倾），远程/妖王后坐缩放
        const p2 = z.strikeT / 0.16;
        const punch = Math.sin(Math.min(1, (1 - p2) * 1.6) * Math.PI); // 起手快收手慢
        const dx = sim.player.x - z.x, dy = sim.player.y - z.y;
        const dd = Math.hypot(dx, dy) || 1;
        if (RANGED_TYPES.has(z.type) || z.boss) {
          v.body.y = 0;
          v.body.rotation = 0;
          const sc = 1 + punch * 0.14;
          v.body.scale.set(v.faceCur * sc, sc);
        } else {
          v.body.y = Math.sin(punch * Math.PI) * -4;
          v.body.rotation = v.faceCur * 0.3 * punch;
          v.body.x += (dx / dd) * punch * 12;
          v.body.y += (dy / dd) * punch * 9;
        }
      } else if (z.attackWind > 0) {
        // 攻击前摇（可读预警）：后倾蓄力 + 血红脉冲，出手意图一目了然
        v.body.y = 0;
        v.body.rotation = -v.faceCur * 0.28;
        const pulse = 0.5 + 0.5 * Math.sin(now * 22);
        v.body.tint = z.hitFlash > 0 ? 0xff4632 : (pulse > 0.5 ? 0xe8705e : 0xffffff);
      } else if (FLOATY_TYPES.has(z.type)) {
        v.body.y = Math.sin(now * 2.6 + v.bobPhase) * 3.2;
        v.body.rotation = Math.sin(now * 1.7 + v.bobPhase) * 0.055;
        v.body.scale.y = 1;
      } else {
        // 地面型：移动时步点弹跳，静止时轻微呼吸；位移由上一帧位置差分得出
        const moved = Math.hypot(z.x - v.lastX, z.y - v.lastY) > 0.4;
        v.body.y = -Math.abs(Math.sin(now * 7 + v.bobPhase)) * (moved ? 2.2 : 0.5);
        v.body.rotation = 0;
        v.body.scale.y = 1;
        v.dustCd -= dt;
        if (moved && v.dustCd <= 0) {
          v.dustCd = 0.16;
          this.spawnDust(z.x, z.y);
        }
      }
      v.lastX = z.x; v.lastY = z.y;
      const ratio = Math.max(0, z.hp / z.maxHp);
      v.hpFill.width = 30 * ratio;
      // 妖王走顶部大血条，头顶小条隐藏
      v.hpBar.visible = !z.boss && z.hp < z.maxHp;
      // 帧列表选择：出手窗口播 attack 帧，妖王常驻 boss 形态（出手反馈走程序脉冲），其余 walk；
      // 列表切换时帧序重置（相位随机化仅限 walk）
      const attacking = (z.attackWind > 0 || z.strikeT > 0) && v.attackFrames !== null
        && !(z.boss && v.bossFrames);
      const key = attacking ? "attack" : (z.boss && v.bossFrames ? "boss" : "walk");
      const list = attacking ? v.attackFrames!
        : key === "boss" ? v.bossFrames!
        : this.enemyTextures.get(v.animName)!;
      if (key !== v.animKey) {
        v.animKey = key;
        v.frame = key === "walk" ? Math.floor(Math.random() * list.length) : 0;
        v.animTimer = 0;
        v.body.texture = list[v.frame];
      }
      if (list.length > 1) {
        v.animTimer += dt;
        if (v.animTimer >= 0.1) {
          v.animTimer -= 0.1;
          v.frame = (v.frame + 1) % list.length;
          v.body.texture = list[v.frame];
        }
      }
    }
    for (const [id, v] of this.enemyViews) {
      if (!alive.has(id)) {
        // 妖物退场：碎片沿最后受击方向飞溅 + 手绘"墨迹消散"（纸面擦除感）
        this.spawnDeathDebris(v.container.x, v.container.y, v.hitUx, v.hitUy, v.big);
        this.spawnDeathScribble(v.container.x, v.container.y);
        v.container.destroy({ children: true });
        this.enemyViews.delete(id);
      }
    }

    this.renderProjectiles(sim);
    this.renderEnemyProjectiles(sim);
    this.renderEnemyStates(sim);
    this.renderMines(sim);
    this.renderPickups(sim);
    this.renderHealOrbs(sim);
    this.renderChests(sim);
    this.renderShrines(sim);
    this.renderTerrain(sim);
    this.renderWeather(sim);
    this.renderBossBar(sim);
    this.renderAttackRange(sim);
    this.renderThreatArrows(sim);
  }

  /** 妖潮来袭标（R28）：屏外精英/妖王/宝妖的边缘方向箭头。
   *  小地图给位置，箭头给即时方向（色调沿用身份环语义：妖王血红/宝妖亮金/精英暗金）。 */
  private threatGfx = new Graphics();
  private renderThreatArrows(sim: GameSim): void {
    const g = this.threatGfx;
    if (g.parent !== this.app.stage) this.app.stage.addChild(g);
    g.clear();
    const W = this.app.screen.width, H = this.app.screen.height;
    // 安全边距：避开顶部 HUD 条与底部教练条/触屏区（DOM 盖在画布上）
    const ML = 30, MR = 30, MT = 76, MB = 100;
    const cands: Array<{ d: number; sx: number; sy: number; color: number }> = [];
    for (const e of sim.enemies) {
      if (!e.elite && !e.boss && !e.goblin) continue;
      const s = this.worldToScreen(e.x, e.y);
      if (s.x > -20 && s.x < W + 20 && s.y > -20 && s.y < H + 20) continue;
      cands.push({
        d: (e.x - sim.player.x) ** 2 + (e.y - sim.player.y) ** 2,
        sx: s.x, sy: s.y,
        color: e.boss ? BLOOD : e.goblin ? 0xffd94d : 0x9a6a10,
      });
    }
    // R88 香火龛指引：限时消费机会优先展示（纸金圆点 + INK 描边，同小地图语义）
    for (const sh of sim.shrines) {
      const s = this.worldToScreen(sh.x, sh.y);
      if (s.x > -20 && s.x < W + 20 && s.y > -20 && s.y < H + 20) continue;
      cands.unshift({ d: -1, sx: s.x, sy: s.y, color: 0xe8d5a8 });
    }
    cands.sort((a, b) => a.d - b.d);
    for (const c of cands.slice(0, 6)) {
      const ang = Math.atan2(c.sy - H / 2, c.sx - W / 2);
      const x = Math.min(W - MR, Math.max(ML, c.sx));
      const y = Math.min(H - MB, Math.max(MT, c.sy));
      const s = 9;
      const dx = Math.cos(ang), dy = Math.sin(ang);
      g.moveTo(x + dx * s, y + dy * s)
        .lineTo(x - dx * s * 0.6 - dy * s * 0.7, y - dy * s * 0.6 + dx * s * 0.7)
        .lineTo(x - dx * s * 0.6 + dy * s * 0.7, y - dy * s * 0.6 - dx * s * 0.7)
        .closePath()
        .fill(c.color)
        .stroke({ width: 1.5, color: INK });
    }
  }

  /** 攻击范围指示：妖物接近时浮现真实判定圈（冲刺斩时扩大），圈内妖物脚下血红标线。 */
  private renderAttackRange(sim: GameSim): void {
    const g = this.rangeGfx;
    if (g.parent !== this.fxLayer) this.fxLayer.addChild(g);
    g.clear();
    if (sim.player.dead) return;
    const r = CFG.Player.BaseAttackRange * sim.stats.rangeMult
      * (sim.player.dashTimer > 0 ? CFG.Combat.DashStrikeRangeMult : 1);
    let nearest = Infinity;
    for (const z of sim.enemies) {
      if (z.stealth) continue;
      nearest = Math.min(nearest, Math.hypot(z.x - sim.player.x, z.y - sim.player.y));
    }
    if (nearest > r * 1.45) return; // 没有威胁接近时不画，保持纸面干净
    this.sketchCircle(g, sim.player.x, sim.player.y, r, { color: 0x8a8175, width: 1.5, alpha: 0.4 });
    for (const z of sim.enemies) {
      if (z.stealth) continue;
      if (Math.hypot(z.x - sim.player.x, z.y - sim.player.y) <= r) {
        g.moveTo(z.x - 10, z.y + 32).lineTo(z.x + 10, z.y + 32)
          .stroke({ width: 2.5, color: BLOOD, alpha: 0.85 });
      }
    }
  }

  /** 妖王血条：屏幕顶居中，纸白底 + 血红填充 + 炭描边；阶段 2 起带裂纹。 */
  private renderBossBar(sim: GameSim): void {
    const g = this.bossBarGfx;
    if (g.parent !== this.app.stage) this.app.stage.addChild(g);
    const boss = sim.enemies.find((e) => e.boss);
    if (!boss) {
      g.clear();
      if (this.bossNameText) { this.bossNameText.destroy(); this.bossNameText = null; }
      return;
    }
    const sw = this.app.screen.width;
    const W = 460, H = 14, x = sw / 2 - W / 2, y = 62;
    const ratio = Math.max(0, boss.hp / boss.maxHp);
    g.clear();
    g.rect(x, y, W, H).fill({ color: PAPER, alpha: 0.92 });
    g.rect(x + 2, y + 2, (W - 4) * ratio, H - 4).fill(BLOOD);
    // R98 阶段刻度：66%/33% 狂暴线常显（铅笔灰虚位）；R103 临近（5% 内）转血红高亮
    const near66 = ratio <= 0.71 && ratio > 0.60;
    const near33 = ratio <= 0.38 && ratio > 0.27;
    g.moveTo(x + W * 0.34, y + 1).lineTo(x + W * 0.34, y + H - 1)
      .stroke({ width: near66 ? 2.5 : 1.5, color: near66 ? BLOOD : INK, alpha: near66 ? 1 : 0.4 });
    g.moveTo(x + W * 0.66, y + 1).lineTo(x + W * 0.66, y + H - 1)
      .stroke({ width: near33 ? 2.5 : 1.5, color: near33 ? BLOOD : INK, alpha: near33 ? 1 : 0.4 });
    if (boss.bossPhase >= 1) {
      // 阶段 2+：填充上两道裂纹，宣告狂暴
      g.moveTo(x + W * 0.38, y).lineTo(x + W * 0.34, y + H)
        .moveTo(x + W * 0.66, y).lineTo(x + W * 0.70, y + H)
        .stroke({ width: 2, color: PAPER, alpha: 0.85 });
    }
    g.rect(x, y, W, H).stroke({ width: 2.5, color: INK });
    if (!this.bossNameText) {
      this.bossNameText = new Text({ text: "", style: {
        fontFamily: "system-ui", fontSize: 13, fontWeight: "700",
        fill: 0x8e2a20, stroke: { color: 0xf5eede, width: 3 },
      } });
      this.bossNameText.anchor.set(0.5, 0);
      this.app.stage.addChild(this.bossNameText);
    }
    // R29 血量百分比（随时知道还剩多少）
    // R103 阶段圆点：● 已过/当前阶段 ○ 未达（3 阶制）
    const pips = [0, 1, 2].map((i) => (i <= boss.bossPhase ? "●" : "○")).join("");
    this.bossNameText.text = `妖王 · ${ENEMY_NAMES[boss.type] ?? "???"}${boss.bossPhase > 0 ? ` · 狂暴 ${boss.bossPhase + 1} 阶` : ""} · ${pips} · ${Math.ceil(ratio * 100)}%`;
    this.bossNameText.position.set(sw / 2, y + H + 4);
  }

  /** 完美闪避纸面波纹：同心双圈 + 放射短线，金色 1.1s 扩散（区别于普通事件圈）。 */
  private ripples: Array<{ gfx: Graphics; x: number; y: number; seed: number; life: number }> = [];

  spawnRipple(x: number, y: number): void {
    if (this.ripples.length > 4) return;
    const rp = { gfx: new Graphics(), x, y, seed: Math.random() * TAU, life: 1.1 };
    this.ripples.push(rp);
    this.fxLayer.addChild(rp.gfx);
  }

  private updateRipples(dt: number): void {
    const alive: typeof this.ripples = [];
    for (const rp of this.ripples) {
      rp.life -= dt;
      if (rp.life <= 0) { rp.gfx.destroy(); continue; }
      const t = 1 - rp.life / 1.1;
      const g = rp.gfx;
      g.clear();
      g.alpha = (1 - t) * 0.9;
      this.sketchCircle(g, rp.x, rp.y, 18 + t * 74, { color: 0xa8741a, width: 3.5, alpha: 0.9 });
      this.sketchCircle(g, rp.x, rp.y, 10 + t * 46, { color: 0xa8741a, width: 2.5, alpha: 0.6 });
      for (let i = 0; i < 8; i++) {
        const a = rp.seed + (i / 8) * TAU;
        const r0 = (26 + t * 58), r1 = r0 + 11;
        g.moveTo(rp.x + Math.cos(a) * r0, rp.y + Math.sin(a) * r0 * 0.8)
          .lineTo(rp.x + Math.cos(a) * r1, rp.y + Math.sin(a) * r1 * 0.8)
          .stroke({ width: 2.2, color: 0xa8741a, alpha: 0.8 });
      }
      alive.push(rp);
    }
    this.ripples = alive;
  }

  /** 妖王弹幕预警（R203）：血红收缩环——弹幕发射前 windup 秒提示走位窗口。
   *  纯表现层，时序由 sim 事件驱动（boss_barrage_windup），零平衡参与。 */
  private barrageWarnings: Array<{ gfx: Graphics; x: number; y: number; life: number; total: number }> = [];

  spawnBarrageWarning(x: number, y: number, t: number): void {
    if (this.barrageWarnings.length > 3) return;
    const bw = { gfx: new Graphics(), x, y, life: t, total: t };
    this.barrageWarnings.push(bw);
    this.fxLayer.addChild(bw.gfx);
  }

  private updateBarrageWarnings(dt: number): void {
    const alive: typeof this.barrageWarnings = [];
    for (const bw of this.barrageWarnings) {
      bw.life -= dt;
      if (bw.life <= 0) { bw.gfx.destroy(); continue; }
      const p = 1 - bw.life / bw.total; // 0→1 收缩进度
      const g = bw.gfx;
      g.clear();
      g.alpha = 0.35 + p * 0.55;
      // 血红收缩环：外环 110→30 向妖王收拢（能量聚拢意象），内环同步跟进
      this.sketchCircle(g, bw.x, bw.y, 110 - p * 80, { color: 0xd93a2b, width: 3.5, alpha: 0.85 });
      this.sketchCircle(g, bw.x, bw.y, 86 - p * 62, { color: 0xd93a2b, width: 2.2, alpha: 0.55 });
      alive.push(bw);
    }
    this.barrageWarnings = alive;
  }

  /** 高阶妖状态提示：甲妖举盾蓝灰圈 / 蛟蓄力红光闪烁 / 前摇"！"预警；
   *  玩家近战范围圈与圈内妖物标线（攻击可读性）。 */
  private enemyStateGfx = new Graphics();
  private rangeGfx = new Graphics();
  /** 妖王顶部血条（屏幕空间，随存活妖王重绘）。 */
  private bossBarGfx = new Graphics();
  private bossNameText: Text | null = null;
  private renderEnemyStates(sim: GameSim): void {
    const g = this.enemyStateGfx;
    if (g.parent !== this.fxLayer) this.fxLayer.addChild(g);
    g.clear();
    for (const z of sim.enemies) {
      if (z.attackWind > 0) {
        // 前摇预警：头顶手绘"！"（血红，急促脉冲）
        const pulse = 0.55 + 0.45 * Math.sin(performance.now() / 45);
        const ly = z.y - 58;
        g.moveTo(z.x, ly).lineTo(z.x, ly - 11)
          .stroke({ width: 3.2, color: BLOOD, alpha: pulse });
        g.circle(z.x, ly + 5, 1.9).fill({ color: BLOOD, alpha: pulse });
      }
      if (z.aimWarned) {
        // R204 远程读题：头顶血红蓄力环渐大（开火即消，区别于近战"！"）
        const p = 1 - Math.min(1, z.attackCd / CFG.Enemy.AttackWindup);
        g.circle(z.x, z.y - 46, 3.5 + p * 5.5)
          .stroke({ width: 2.4, color: BLOOD, alpha: 0.5 + p * 0.45 });
      }
      if (z.type === EnemyType.Knight && z.blockT > 0) {
        g.circle(z.x, z.y, 26).stroke({ width: 3, color: 0x4a6a8a, alpha: 0.85 });
        g.circle(z.x, z.y, 30).stroke({ width: 1.5, color: 0x3a5a7a, alpha: 0.4 });
      } else if (z.type === EnemyType.Dragon && z.chargeWind > 0) {
        const flash = 0.3 + 0.28 * Math.sin(performance.now() / 60);
        g.circle(z.x, z.y, 30).fill({ color: BLOOD, alpha: flash });
      }
    }
  }

  /** 弹道：箭=炭黑细长条按速度方向旋转；魔法/追踪弹=血红圆点。 */
  private fxLayer = new Container();
  private projGfx = new Graphics();
  private mineGfx = new Graphics();
  private chestGfx = new Graphics();

  private renderProjectiles(sim: GameSim): void {
    const g = this.projGfx;
    if (this.projGfx.parent !== this.fxLayer) this.fxLayer.addChild(g);
    g.clear();
    for (const pr of sim.projectiles) {
      const sp = Math.hypot(pr.vx, pr.vy) || 1;
      const ux = pr.vx / sp, uy = pr.vy / sp;
      // 拖尾：运动反方向一道渐隐细线（箭/弹通用）
      g.moveTo(pr.x - ux * (sp / 500) * 30, pr.y - uy * (sp / 500) * 30)
        .lineTo(pr.x - ux * 8, pr.y - uy * 8)
        .stroke({ width: 2, color: pr.homing ? 0x9a2a1e : INK_SOFT, alpha: 0.3 });
      if (pr.homing) {
        g.circle(pr.x, pr.y, 5).fill(BLOOD).alpha = 0.95;
        g.circle(pr.x, pr.y, 8).stroke({ width: 2, color: 0x9a2a1e, alpha: 0.55 });
      } else {
        const len = 14;
        g.moveTo(pr.x - ux * len, pr.y - uy * len)
          .lineTo(pr.x + ux * len * 0.5, pr.y + uy * len * 0.5)
          .stroke({ width: 3, color: INK, alpha: 0.95 });
      }
    }
    g.alpha = 1;
  }

  /** 地雷：按布雷武器选贴图（缺省回退铁蒺藜再回退矢量），布防期整体泛白。 */
  private renderMines(sim: GameSim): void {
    const g = this.mineGfx;
    if (g.parent !== this.fxLayer) this.fxLayer.addChild(g);
    g.clear();
    const texDefault = this.propTextures.get("mine_tiejili");
    const alive = new Set<object>();
    for (const m of sim.mines) {
      alive.add(m);
      const tex = (m.defId === "diyu_huolei" ? this.propTextures.get("mine_huolei")
        : m.defId === "jishulei" ? this.propTextures.get("mine_jishu") : null)
        ?? texDefault;
      if (tex) {
        let spr = this.mineSprites.get(m);
        if (!spr) {
          spr = new Sprite(tex);
          spr.anchor.set(0.5, 0.7);
          this.mineSprites.set(m, spr);
          this.propLayer.addChild(spr);
        }
        spr.visible = true;
        spr.position.set(m.x, m.y);
        spr.width = 30; spr.height = 30;
        spr.tint = m.arm > 0 ? 0xd8d0c0 : 0xffffff;
        continue;
      }
      g.circle(m.x, m.y, 7).fill(m.arm > 0 ? INK_SOFT : BLOOD).alpha = 0.9;
      g.circle(m.x, m.y, 10).stroke({ width: 1.5, color: INK, alpha: 0.6 });
    }
    this.pruneSprites(this.mineSprites, alive);
  }

  /** 敌方弹道：毒弹=墨绿点 / 箭矢=血红细条（与玩家炭黑弹道区分）。 */
  private enemyProjGfx = new Graphics();
  private renderEnemyProjectiles(sim: GameSim): void {
    const g = this.enemyProjGfx;
    if (g.parent !== this.fxLayer) this.fxLayer.addChild(g);
    g.clear();
    for (const pr of sim.enemyProjectiles) {
      if (pr.psn) {
        g.circle(pr.x, pr.y, 5).fill({ color: 0x3a6a2a, alpha: 0.95 });
        g.circle(pr.x, pr.y, 8).stroke({ width: 1.5, color: 0x2a4a1e, alpha: 0.45 });
      } else {
        const sp = Math.hypot(pr.vx, pr.vy) || 1;
        const ux = pr.vx / sp, uy = pr.vy / sp;
        g.moveTo(pr.x - ux * 13, pr.y - uy * 13)
          .lineTo(pr.x + ux * 6, pr.y + uy * 6)
          .stroke({ width: 2.5, color: BLOOD, alpha: 0.95 });
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
      b.gfx.alpha = (1 - t) * 0.85;
      this.sketchCircle(b.gfx, b.x, b.y, 10 + t * 46, { color: b.color, width: 3.5, alpha: 1 });
      alive.push(b);
    }
    this.bursts = alive;
  }

  private updateAfterimages(dt: number): void {
    const alive: typeof this.afterimages = [];
    for (const a of this.afterimages) {
      a.life -= dt;
      if (a.life <= 0) { a.spr.destroy(); continue; }
      a.spr.alpha = (a.life / AFTERIMAGE_LIFE) * 0.32;
      alive.push(a);
    }
    this.afterimages = alive;
  }

  /** 移动扬尘：脚下铅笔灰小点，上飘淡出（浮空型不产）。 */
  private dustGfx = new Graphics();
  private dusts: Array<{ x: number; y: number; vx: number; vy: number; r: number; life: number }> = [];
  private playerDustCd = 0;

  private spawnDust(x: number, y: number): void {
    if (this.dusts.length > 26) return;
    this.dusts.push({
      x: x + (Math.random() * 12 - 6), y: y + 30,
      vx: Math.random() * 22 - 11, vy: -14 - Math.random() * 14,
      r: 1.4 + Math.random() * 1.6, life: 0.42,
    });
  }

  private updateDust(dt: number): void {
    const alive: typeof this.dusts = [];
    const g = this.dustGfx;
    if (g.parent !== this.fxLayer) this.fxLayer.addChild(g);
    g.clear();
    for (const d of this.dusts) {
      d.life -= dt;
      if (d.life <= 0) continue;
      d.x += d.vx * dt; d.y += d.vy * dt; d.vy += 26 * dt;
      g.circle(d.x, d.y, d.r * (0.5 + d.life / 0.42 * 0.5)).fill({ color: INK_SOFT, alpha: (d.life / 0.42) * 0.4 });
      alive.push(d);
    }
    this.dusts = alive;
  }

  /** 死亡碎片：沿最后受击方向飞溅的墨点（重力 + 落地摩擦），妖物退场的实体反馈。 */
  private debrisGfx = new Graphics();
  private debris: Array<{ x: number; y: number; vx: number; vy: number; r: number; life: number; maxLife: number; blood: boolean }> = [];

  private spawnDeathDebris(x: number, y: number, ux: number, uy: number, big: boolean): void {
    const n = big ? 14 : 8;
    for (let i = 0; i < n && this.debris.length < 80; i++) {
      const a = Math.atan2(uy, ux) + (Math.random() - 0.5) * 1.9;
      const sp = 90 + Math.random() * 200;
      const life = 0.45 + Math.random() * 0.25;
      this.debris.push({
        x, y: y - 6,
        vx: Math.cos(a) * sp,
        vy: Math.sin(a) * sp * 0.6 - 70 - Math.random() * 90,
        r: 1.6 + Math.random() * 2.2,
        life, maxLife: life,
        blood: Math.random() < 0.18,
      });
    }
  }

  private updateDebris(dt: number): void {
    const alive: typeof this.debris = [];
    const g = this.debrisGfx;
    if (g.parent !== this.fxLayer) this.fxLayer.addChild(g);
    g.clear();
    for (const d of this.debris) {
      d.life -= dt;
      if (d.life <= 0) continue;
      d.x += d.vx * dt;
      d.y += d.vy * dt;
      d.vy += 560 * dt;            // 重力
      d.vx *= Math.exp(-2.5 * dt); // 空气阻力
      if (d.vy > 0) d.vy *= Math.exp(-14 * dt); // 下落段强阻尼 → 贴地散开
      g.circle(d.x, d.y, d.r)
        .fill({ color: d.blood ? BLOOD : INK, alpha: Math.min(1, (d.life / d.maxLife) * 1.4) * 0.9 });
      alive.push(d);
    }
    this.debris = alive;
  }

  /** 命中火花：命中点迸 4 条炭线快速淡出（0.16s）。 */  private sparkGfx = new Graphics();
  private sparks: Array<{ x: number; y: number; seed: number; life: number }> = [];

  spawnHitSpark(x: number, y: number): void {
    if (this.sparks.length > 14) return;
    this.sparks.push({ x, y, seed: Math.random() * TAU, life: 0.16 });
  }

  private updateSparks(dt: number): void {
    const alive: typeof this.sparks = [];
    const g = this.sparkGfx;
    if (g.parent !== this.fxLayer) this.fxLayer.addChild(g);
    g.clear();
    for (const sp of this.sparks) {
      sp.life -= dt;
      if (sp.life <= 0) continue;
      const t = sp.life / 0.16;
      for (let i = 0; i < 4; i++) {
        const a = sp.seed + (i / 4) * TAU;
        const r0 = 4 + (1 - t) * 10, r1 = r0 + 5 + t * 4;
        g.moveTo(sp.x + Math.cos(a) * r0, sp.y + Math.sin(a) * r0)
          .lineTo(sp.x + Math.cos(a) * r1, sp.y + Math.sin(a) * r1)
          .stroke({ width: 2, color: INK, alpha: t * 0.85 });
      }
      alive.push(sp);
    }
    this.sparks = alive;
  }

  /** 近战挥砍弧：沿实际攻击方向一道手绘弧快速淡出（冲刺斩更大更红）。 */
  private slashes: Array<{ gfx: Graphics; x: number; y: number; dir: number; dash: boolean; life: number; range: number }> = [];

  spawnSlash(x: number, y: number, dir: number, dash: boolean, range: number): void {
    if (this.slashes.length > 6) return;
    // R181 挥砍弧随实际射程缩放（R176 范围 70→92 后视觉/判定对齐）
    const sl = { gfx: new Graphics(), x, y, dir, dash, life: 0.18, range };
    this.slashes.push(sl);
    this.fxLayer.addChild(sl.gfx);
  }

  private updateSlashes(dt: number): void {
    const alive: typeof this.slashes = [];
    for (const sl of this.slashes) {
      sl.life -= dt;
      if (sl.life <= 0) { sl.gfx.destroy(); continue; }
      const t = 1 - sl.life / 0.18;
      const g = sl.gfx;
      g.clear();
      g.alpha = (1 - t) * 0.85;
      const r = sl.range * (0.62 + t * 0.1) * (sl.dash ? 1.35 : 1);
      // 朝向前方 -75°..+55° 的手绘弧（中段最亮，两端收细）
      const N = 12;
      const a0 = sl.dir - 1.31, a1 = sl.dir + 0.96;
      const pts = [];
      for (let i = 0; i <= N; i++) {
        const a = a0 + (a1 - a0) * (i / N);
        const wob = 1 + Math.sin(a * 3.1 + sl.x * 0.05) * 0.05; // 手绘颤动
        pts.push([sl.x + Math.cos(a) * r * wob, sl.y + Math.sin(a) * r * 0.92 * wob]);
      }
      g.moveTo(pts[0][0], pts[0][1]);
      for (let i = 1; i <= N; i++) g.lineTo(pts[i][0], pts[i][1]);
      g.stroke({ width: (sl.dash ? 5.5 : 4) * (1 - t * 0.5), color: sl.dash ? BLOOD : INK, alpha: 1 });
      alive.push(sl);
    }
    this.slashes = alive;
  }

  /** 墨迹消散：放射炭线 + 小圈 + 血红点，快速扩张淡出（纸上擦除感）。 */
  private scribbles: Array<{ gfx: Graphics; x: number; y: number; seed: number; life: number; scale: number }> = [];

  private spawnDeathScribble(x: number, y: number, scale = 1): void {
    if (this.scribbles.length > 10) return; // 上限节流
    const sc = { gfx: new Graphics(), x, y, seed: Math.random() * TAU, life: 0.4, scale };
    this.scribbles.push(sc);
    this.fxLayer.addChild(sc.gfx);
  }

  private updateScribbles(dt: number): void {
    const alive2: typeof this.scribbles = [];
    for (const sc of this.scribbles) {
      sc.life -= dt;
      if (sc.life <= 0) { sc.gfx.destroy(); continue; }
      const t = 1 - sc.life / 0.4;
      const g = sc.gfx;
      g.clear();
      g.alpha = (1 - t) * 0.9;
      const k = sc.scale;
      // 5 条放射短线（手绘抖动：长度/角度带随机相位偏移）
      for (let i = 0; i < 5; i++) {
        const a = sc.seed + (i / 5) * TAU;
        const r0 = (6 + t * 16) * k, r1 = r0 + (9 + Math.sin(sc.seed + i * 2.7) * 3) * k;
        g.moveTo(sc.x + Math.cos(a) * r0, sc.y + Math.sin(a) * r0 * 0.75)
          .lineTo(sc.x + Math.cos(a) * r1, sc.y + Math.sin(a) * r1 * 0.75)
          .stroke({ width: 2.4, color: INK, alpha: 0.8 });
      }
      // 一个歪斜小圈 + 血红残点
      this.sketchCircle(g, sc.x, sc.y - 2 * k, (5 + t * 12) * k, { color: INK_SOFT, width: 2, alpha: 0.7 });
      g.circle(sc.x + Math.sin(sc.seed) * 6 * k, sc.y - 4 * k, 2.2 * Math.max(1, k * 0.7)).fill({ color: BLOOD, alpha: 0.85 });
      alive2.push(sc);
    }
    this.scribbles = alive2;
  }

  /** 手绘抖动圆：N 段折线 + sin 相位扰动（区别于矢量完美圆）；段数随半径自适应。 */
  private sketchCircle(g: Graphics, cx: number, cy: number, r: number, o: { color: number; width: number; alpha: number }): void {
    const N = Math.max(14, Math.round(r * 0.55));
    const wob = r * 0.08;
    for (let i = 0; i <= N; i++) {
      const a = (i / N) * TAU;
      const rr = r + Math.sin(a * 3 + cx * 0.1 + cy * 0.07) * wob;
      const px = cx + Math.cos(a) * rr, py = cy + Math.sin(a) * rr;
      if (i === 0) g.moveTo(px, py);
      else g.lineTo(px, py);
    }
    g.stroke({ width: o.width, color: o.color, alpha: o.alpha });
  }

  /** 回春丹（R20）：绿十字药丸 + 淡圈，整体轻微上下浮动（与符箓同相位节奏）。 */
  private healGfx = new Graphics();
  private renderHealOrbs(sim: GameSim): void {
    const g = this.healGfx;
    if (g.parent !== this.fxLayer) this.fxLayer.addChild(g);
    g.clear();
    const bob = Math.sin(performance.now() / 220) * 3;
    for (const o of sim.healOrbs) {
      g.circle(o.x, o.y + 4, 12).stroke({ width: 1.5, color: INK, alpha: 0.3 });
      g.circle(o.x, o.y + bob, 7)
        .fill({ color: 0xfbf6ea, alpha: 0.95 })
        .stroke({ width: 1.8, color: 0x6ab04c });
      g.rect(o.x - 1.8, o.y - 5 + bob, 3.6, 10).fill(0x6ab04c);
      g.rect(o.x - 5, o.y - 1.8 + bob, 10, 3.6).fill(0x6ab04c);
    }
  }

  /** 宝箱：简笔木箱——炭描边方框 + 箱盖线 + 血红锁扣点。 */
  /** 对象键控精灵缓存清理：退场物件销毁对应精灵。 */
  private pruneSprites<T extends object>(map: Map<T, Sprite>, alive: Set<T>): void {
    for (const [obj, spr] of map) {
      if (!alive.has(obj)) {
        spr.destroy();
        map.delete(obj);
      }
    }
  }

  /** 宝箱：优先简笔木箱贴图，缺失回退矢量（炭描边方框 + 箱盖线 + 血红锁扣点）。 */
  private renderChests(sim: GameSim): void {
    const g = this.chestGfx;
    if (g.parent !== this.fxLayer) this.fxLayer.addChild(g);
    g.clear();
    const tex = this.propTextures.get("chest");
    const alive = new Set<object>();
    for (const c of sim.chests) {
      if (c.opened) continue;
      // R8 精英箱：同纸金底 + 血红描边/锁 + 大一圈（高价值一望可辨）
      if (c.elite) {
        g.rect(c.x - 15, c.y - 12, 30, 24).fill({ color: 0xe8d5a8, alpha: 0.95 })
          .stroke({ width: 3, color: BLOOD });
        g.moveTo(c.x - 15, c.y - 4).lineTo(c.x + 15, c.y - 4).stroke({ width: 2, color: BLOOD, alpha: 0.85 });
        g.circle(c.x, c.y - 2, 3).fill(BLOOD);
      } else if (tex) {
        // R36 贴图宝箱（AI 资产）：普通箱走精灵，缺失回退简笔
        let spr = this.chestSprites.get(c);
        if (!spr) {
          spr = new Sprite(tex);
          spr.anchor.set(0.5, 0.8);
          this.chestSprites.set(c, spr);
          this.propLayer.addChild(spr);
        }
        spr.visible = true;
        spr.position.set(c.x, c.y);
        spr.width = 44; spr.height = 44;
        alive.add(c);
        continue;
      } else {
        g.rect(c.x - 13, c.y - 10, 26, 20).fill({ color: 0xe8d5a8, alpha: 0.95 })
          .stroke({ width: 2.5, color: INK });
        g.moveTo(c.x - 13, c.y - 3).lineTo(c.x + 13, c.y - 3).stroke({ width: 1.8, color: INK, alpha: 0.7 });
        g.circle(c.x, c.y - 2, 2.4).fill(BLOOD);
      }
      alive.add(c);
    }
    this.pruneSprites(this.chestSprites, alive);
  }

  /** 香火龛（R65）：简笔龛台——纸金小龛 + 顶檐 + 血红香火点（区别于宝箱方箱）。 */
  private shrineGfx = new Graphics();
  private renderShrines(sim: GameSim): void {
    const g = this.shrineGfx;
    if (g.parent !== this.fxLayer) this.fxLayer.addChild(g);
    g.clear();
    for (const s of sim.shrines) {
      g.rect(s.x - 11, s.y - 14, 22, 20).fill({ color: 0xe8d5a8, alpha: 0.95 })
        .stroke({ width: 2.5, color: INK });
      g.moveTo(s.x - 14, s.y - 14).lineTo(s.x + 14, s.y - 14).stroke({ width: 3, color: INK });
      g.circle(s.x, s.y - 4, 3).fill(BLOOD);
      g.moveTo(s.x - 6, s.y + 6).lineTo(s.x + 6, s.y + 6).stroke({ width: 2, color: INK, alpha: 0.7 });
    }
  }

  /** 升级符箓：竖长符纸 + 血红符文 + 光圈，整体轻微上下浮动。 */
  /** 升级符箓：优先符纸贴图 + 上下浮动，缺失回退矢量。 */
  private pickupGfx = new Graphics();
  private renderPickups(sim: GameSim): void {
    const g = this.pickupGfx;
    if (g.parent !== this.fxLayer) this.fxLayer.addChild(g);
    g.clear();
    const tex = this.propTextures.get("talisman");
    const bob = Math.sin(performance.now() / 220) * 3;
    const alive = new Set<object>();
    for (const u of sim.pickups) {
      alive.add(u);
      if (tex) {
        let spr = this.pickupSprites.get(u);
        if (!spr) {
          spr = new Sprite(tex);
          spr.anchor.set(0.5, 0.6);
          this.pickupSprites.set(u, spr);
          this.propLayer.addChild(spr);
        }
        spr.visible = true;
        spr.position.set(u.x, u.y + bob);
        spr.width = 30; spr.height = 30;
        continue;
      }
      g.circle(u.x, u.y + 4, 15).stroke({ width: 1.5, color: INK, alpha: 0.3 });
      g.rect(u.x - 6, u.y - 9 + bob, 12, 18)
        .fill({ color: 0xfbf6ea, alpha: 0.95 })
        .stroke({ width: 1.8, color: BLOOD });
      g.circle(u.x, u.y - 3 + bob, 2.5).fill(BLOOD);
      g.rect(u.x - 3, u.y + 1 + bob, 6, 1.5).fill(BLOOD);
    }
    this.pruneSprites(this.pickupSprites, alive);
  }

  /** 天气表现：屏幕空间色调覆盖（雨=冷蓝+雨线 / 雾=灰蒙 / 血月=暗红呼吸脉冲）。 */
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
      g.rect(0, 0, sw, sh).fill({ color: 0x5a7090, alpha: 0.14 * k });
      // 雨线：相位随时间下落（i 播撒位置，t 驱动下落循环）
      const t = performance.now() / 1000;
      g.stroke({ width: 1.5, color: 0x4a6078, alpha: 0.5 });
      for (let i = 0; i < 34; i++) {
        const bx = ((i * 757) % sw + ((i * 331) % 60)) % sw;
        const y0 = ((t * (520 + (i % 5) * 70) + (i * 137)) % (sh + 120)) - 60;
        g.moveTo(bx, y0).lineTo(bx - 7, y0 + 22);
      }
    } else if (w.type === "Fog") {
      g.rect(0, 0, sw, sh).fill({ color: 0x9a948a, alpha: 0.28 * k });
      // 手绘雾团：三团歪圈组合，随时间缓慢漂移（纸上擦过的灰痕）
      const t = performance.now() / 1000;
      for (let i = 0; i < 3; i++) {
        const cx = ((t * (12 + i * 7) + i * 500) % (sw + 360)) - 180;
        const cy = sh * (0.22 + 0.3 * ((i * 0.37) % 1)) + Math.sin(t * 0.4 + i * 2.1) * 22;
        g.circle(cx, cy, 95 + i * 26).fill({ color: 0xe8e3d6, alpha: 0.16 * k });
        g.circle(cx + 70, cy + 18, 70 + i * 20).fill({ color: 0xe8e3d6, alpha: 0.14 * k });
        g.circle(cx - 80, cy + 8, 60 + i * 16).fill({ color: 0xe8e3d6, alpha: 0.12 * k });
        this.sketchCircle(g, cx, cy, 95 + i * 26, { color: 0xd8d2c2, width: 3.5, alpha: 0.35 * k });
        this.sketchCircle(g, cx + 70, cy + 18, 70 + i * 20, { color: 0xd8d2c2, width: 3, alpha: 0.3 * k });
      }
    } else if (w.type === "BloodMoon") {
      const pulse = 0.16 + 0.05 * Math.sin(performance.now() / 450);
      g.rect(0, 0, sw, sh).fill({ color: 0x8e1010, alpha: pulse * k });
      // 手绘血月（右上角）：外轮 + 两笔月海 + 一笔云带，与暗红罩同呼吸
      const mx = sw - 250, my = 175;
      const breath = 1 + 0.03 * Math.sin(performance.now() / 450);
      g.alpha = 0.75 + 0.25 * Math.sin(performance.now() / 450) * 0.3;
      this.sketchCircle(g, mx, my, 46 * breath, { color: 0xb02a1e, width: 4.5, alpha: 0.95 });
      this.moonArc(g, mx - 12, my - 8, 16, { color: 0xb02a1e, alpha: 0.5 });
      this.moonArc(g, mx + 8, my + 10, 12, { color: 0xb02a1e, alpha: 0.4 });
      g.moveTo(mx - 70, my + 28)
        .lineTo(mx - 32, my + 20).lineTo(mx + 4, my + 28).lineTo(mx + 46, my + 18)
        .stroke({ width: 2.4, color: 0xb02a1e, alpha: 0.6 });
      g.alpha = 1;
    }
  }

  /** 月面弧（短弧线段，供血月月海）。 */
  private moonArc(g: Graphics, cx: number, cy: number, r: number, o: { color: number; alpha: number }): void {
    const pts = [];
    for (let i = 0; i <= 7; i++) {
      const a = Math.PI + (i / 7) * Math.PI * 0.72;
      pts.push([cx + Math.cos(a) * r, cy + Math.sin(a) * r * 0.6]);
    }
    g.moveTo(pts[0][0], pts[0][1]);
    for (let i = 1; i < pts.length; i++) g.lineTo(pts[i][0], pts[i][1]);
    g.stroke({ width: 2.2, color: o.color, alpha: o.alpha });
  }

  /** fxLayer 需在 init 里挂到 worldLayer 之上；静态障碍/祭坛也在 init 后挂一次。 */
  attachFx(): void {
    this.worldLayer.addChild(this.fxLayer);
  }

  /** 触发震屏（强度≈像素幅度）；表现设置关闭时静默跳过（R61）。 */
  shake(power: number): void {
    if (!this.shakeEnabled) return;
    this.shakePower = Math.max(this.shakePower, power);
  }

  /** 播放一轮玩家攻击动画（onAttack 事件触发，期间覆盖 walk/idle 帧）。 */
  playAttack(type?: string): void {
    this.attackTimer = ATTACK_ANIM_TIME;
    this.attackIsMelee = type === undefined || type === "Melee";
  }

  /** 播放一轮玩家受击动画（onPlayerHurt 事件触发）。 */
  playHurt(): void {
    this.hurtTimer = HURT_ANIM_TIME;
  }

  /** 伤害数字：世界上浮淡出（暴击更大；白纸上炭红字 + 纸白描边可读）。
   *  R1：出生带缩放 pop（暴击弹得更高），打击的视觉锚点。 */
  spawnDamageNumber(x: number, y: number, dmg: number, crit: boolean): void {
    if (!this.damageNumbersEnabled) return; // R61 表现设置：关闭飘字
    if (this.damageTexts.length > 24) return; // 上限节流
    const t = new Text({ text: String(dmg), style: {
      fontFamily: "system-ui", fontSize: crit ? 26 : 17, fontWeight: "700",
      fill: crit ? 0x9a6a0a : 0x9a2a1e, stroke: { color: 0xf5eede, width: 3 },
    } });
    t.anchor.set(0.5);
    t.position.set(x + (Math.random() * 16 - 8), y - 40);
    this.worldLayer.addChild(t);
    this.damageTexts.push({ t, life: 0.8, age: 0, popFrom: crit ? 1.9 : 1.45 });
  }

  /** R184 治疗数字：绿色 +40 上浮（与伤害数字同池渲染，负向红正向绿对称）。 */
  spawnHealNumber(x: number, y: number, heal: number): void {
    if (!this.damageNumbersEnabled) return;
    if (this.damageTexts.length > 24) return;
    const t = new Text({ text: `+${heal}`, style: {
      fontFamily: "system-ui", fontSize: 19, fontWeight: "700",
      fill: 0x6ab04c, stroke: { color: 0xf5eede, width: 3 },
    } });
    t.anchor.set(0.5);
    t.position.set(x + (Math.random() * 14 - 7), y - 44);
    this.worldLayer.addChild(t);
    this.damageTexts.push({ t, life: 0.9, age: 0, popFrom: 1.5 });
  }

  private updateDamageTexts(dt: number): void {
    const alive: Array<{ t: Text; life: number; age: number; popFrom: number }> = [];
    for (const d of this.damageTexts) {
      d.life -= dt;
      d.age += dt;
      if (d.life <= 0) { d.t.destroy(); continue; }
      // pop：0.14s 内从 popFrom 缩回 1（ease-out，暴击弹得更高）
      const k = Math.min(1, d.age / 0.14);
      const ease = 1 - (1 - k) * (1 - k);
      d.t.scale.set(1 + (d.popFrom - 1) * (1 - ease));
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
  /** R61 表现设置：false 时命中/击杀不再震屏（无障碍，偏好由 app 层装配）。 */
  shakeEnabled = true;
  /** R61 表现设置：false 时不再生成伤害数字飘字。 */
  damageNumbersEnabled = true;
  private damageTexts: Array<{ t: Text; life: number; age: number; popFrom: number }> = [];

  /** 障碍与祭坛：优先场景物件贴图（AI 资产），缺失回退速写本线稿；每帧低开销同步。 */
  renderTerrain(sim: GameSim): void {
    if (this.terrainGfx.parent !== this.worldLayer) this.worldLayer.addChild(this.terrainGfx);
    if (this.altarGfx.parent !== this.worldLayer) this.worldLayer.addChild(this.altarGfx);
    const g = this.terrainGfx;
    g.clear();
    // 障碍线稿形（fill=淡彩 / stroke=炭）：tree=干+冠圈 / rock=顽石纹 / bush=灌丛三球 / stone_block=石砖
    for (const o of sim.terrain.obstacles) {
      if (o.dead) continue;
      g.circle(o.x, o.y + o.r * 0.55, o.r * 0.8).fill({ color: 0x6b6154, alpha: 0.14 }); // 落影
      const tex = this.propTextures.get(o.kind);
      if (tex) {
        let spr = this.obstacleSprites.get(o);
        if (!spr) {
          spr = new Sprite(tex);
          spr.anchor.set(0.5, 0.82);
          this.obstacleSprites.set(o, spr);
          this.propLayer.addChild(spr);
        }
        spr.visible = true;
        spr.position.set(o.x, o.y + o.r * 0.4);
        const s = (o.r * 2.6) / tex.width;
        spr.scale.set(s);
        continue;
      }
      if (o.kind === "tree") {
        g.moveTo(o.x, o.y + o.r * 0.5).lineTo(o.x, o.y - o.r * 0.1)
          .stroke({ width: 3, color: INK });
        g.circle(o.x, o.y - o.r * 0.35, o.r * 0.72)
          .fill({ color: 0xcfd8b0, alpha: 0.85 }).stroke({ width: 2.5, color: INK });
        if (o.hp > 0) g.moveTo(o.x - o.r * 0.3, o.y - o.r * 0.4).lineTo(o.x + o.r * 0.28, o.y - o.r * 0.28)
          .stroke({ width: 1.8, color: INK, alpha: 0.5 });
      } else if (o.kind === "rock" || o.kind === "stone_block") {
        if (o.kind === "stone_block") {
          g.rect(o.x - o.r * 0.85, o.y - o.r * 0.7, o.r * 1.7, o.r * 1.35)
            .fill({ color: 0xd8d0c0, alpha: 0.9 }).stroke({ width: 2.5, color: INK });
          g.moveTo(o.x - o.r * 0.85, o.y).lineTo(o.x + o.r * 0.85, o.y)
            .stroke({ width: 1.5, color: INK, alpha: 0.45 });
        } else {
          g.circle(o.x, o.y, o.r)
            .fill({ color: 0xd8d0c0, alpha: 0.9 }).stroke({ width: 2.5, color: INK });
          g.moveTo(o.x - o.r * 0.5, o.y + o.r * 0.15).lineTo(o.x + o.r * 0.1, o.y - o.r * 0.2)
            .stroke({ width: 1.8, color: INK, alpha: 0.5 });
        }
      } else { // bush
        for (const [dx, r] of [[-0.45, 0.5], [0, 0.65], [0.45, 0.5]] as const) {
          g.circle(o.x + o.r * dx, o.y + o.r * 0.1, o.r * r)
            .fill({ color: 0xcfd8b0, alpha: 0.8 }).stroke({ width: 2, color: INK, alpha: 0.9 });
        }
      }
    }
    // 祭坛：优先贴图（血红符阵，用后褪色），缺失回退矢量符阵
    const a = this.altarGfx;
    a.clear();
    for (const alt of sim.terrain.altars) {
      const tex = this.propTextures.get(alt.used ? "altar_used" : "altar");
      if (tex) {
        let spr = this.altarSprites.get(alt);
        if (!spr) {
          spr = new Sprite(tex);
          spr.anchor.set(0.5, 0.78);
          this.altarSprites.set(alt, spr);
          this.propLayer.addChild(spr);
        }
        spr.texture = tex;
        spr.position.set(alt.x, alt.y);
        spr.width = 68; spr.height = 68;
        spr.alpha = alt.used ? 0.55 : 1;
        continue;
      }
      const alpha = alt.used ? 0.25 : 0.85;
      a.circle(alt.x, alt.y, 22).fill({ color: 0xe8c8c0, alpha: alpha * 0.5 })
        .stroke({ width: 2.5, color: BLOOD, alpha });
      a.circle(alt.x, alt.y, 30).stroke({ width: 2, color: BLOOD, alpha: alpha * 0.6 });
      a.circle(alt.x, alt.y, 5).fill({ color: BLOOD, alpha });
      if (!alt.used) {
        a.circle(alt.x, alt.y, 38).stroke({ width: 1.5, color: BLOOD, alpha: 0.3 });
      }
    }
  }
}
