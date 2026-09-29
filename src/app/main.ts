// 应用装配入口：PixiJS 初始化、固定步长主循环（含命中停顿）、debug API、
// 音频、全套 UI 装配（HUD/装备/合成/融合/帮助/暂停/小地图/通知）。

import { Application } from "pixi.js";
import { GameSim, UPGRADE_STAT_NAMES, type ExecResult, type SimEvent } from "../sim/game";
import { HITSTOP } from "../sim/hitstop";
import { weatherEffectTip } from "../sim/weather";
import { localStorageStorage } from "../sim/save";
import { loadVisualSettings, saveVisualSettings } from "./settings";
import { RumbleManager, type RumbleActuator } from "./rumble";
import { GameView } from "../render/view";
import { InputManager } from "./input";
import { AudioManager } from "./audio";
import { Hud } from "../ui/hud";
import { EquipmentPanel } from "../ui/equipment";
import { CraftingPanel } from "../ui/crafting";
import { FusionPanel } from "../ui/fusion";
import { HelpPanel } from "../ui/help";
import { PauseMenu } from "../ui/pausemenu";
import { UpgradePanel } from "../ui/upgrade";
import { ShopPanel } from "../ui/shop";
import { PadMenuNav } from "../ui/padnav";
import { MiniMap } from "../ui/minimap";
import { Notify } from "../ui/notify";
import { TouchControls, shouldUseTouch } from "../ui/touch";
import { Coach } from "../ui/coach";
import "./style.css";

/** 目标帧率：渲染限帧与逻辑步长共用同一来源（sim 固定 60Hz 步长）。 */
const TARGET_FPS = 60;
const FIXED_DT = 1 / TARGET_FPS;

declare global {
  interface Window {
    /** 调试/AI 测试 API（JSON 指令协议，与 Godot 旧版 cmd-port 同构）。 */
    __CNM__: {
      version: string;
      exec: (cmd: { id?: number; action: string; args?: Record<string, unknown> }) => ExecResult;
      getEvents: () => SimEvent[];
      ready: boolean;
      /** 当前画面快照（Pixi extract，JPEG dataURL；scale 为分辨率系数）。 */
      shot: (scale?: number) => string;
      /** R100 结算屏截图（JPEG dataURL；hud 截图分享按钮用）。 */
      capture: () => string;
      /** 直接访问 sim 实例（Playwright evaluate 深度断言用）。 */
      sim: GameSim;
      /** 当前渲染帧率（ticker 滚动平均；上限 TARGET_FPS=60）。 */
      fps: number;
      /** R68 音频上下文状态（suspended/running/none；自动播放策略探针）。 */
      audioState: string;
    };
  }
}

async function boot(): Promise<void> {
  const app = new Application();
  await app.init({ resizeTo: window, background: 0xefe7d4, antialias: true });
  // 帧率上限 60：高刷屏（120Hz+）上 rAF 默认跑满刷新率白烧 GPU；逻辑步长本就是 60Hz
  app.ticker.maxFPS = TARGET_FPS;
  document.getElementById("app")!.prepend(app.canvas);

  let sim = new GameSim(Date.now() % 100000, localStorageStorage());
  // R61 表现设置：独立于游戏存档的 UI 偏好（localStorage），装配时同步给消费端
  const settings = loadVisualSettings(localStorage);
  const view = new GameView();
  const input = new InputManager(sim);
  const audio = new AudioManager();
  await view.init(app);
  view.attachFx();
  await audio.init();
  view.shakeEnabled = settings.shake;
  view.damageNumbersEnabled = settings.damageNumbers;
  // R63 手柄震动：执行器结构化适配取自当前手柄（Chromium 扩展属性，lib 无类型）；
  // 开关走 R61 表现偏好（enabled 闭包实时读），无手柄/失败静默
  type PadWithRumble = { vibrationActuator?: RumbleActuator };
  const rumble = new RumbleManager(
    () => (navigator.getGamepads?.()[0] as unknown as PadWithRumble | undefined)?.vibrationActuator,
    () => settings.rumble,
  );

  // R22：欢迎页填了自定义种子则用该种子开局（可复现速通），否则沿用当前实例
  // R44：Boss 连战开关 → 以 boss_rush 模式重建 sim（血誓/种子照常）
  const hud = new Hud(sim, () => {
    const seed = hud.pendingSeed();
    if (hud.pendingBossRush) {
      hud.pendingBossRush = false;
      restart(seed ?? undefined, "boss_rush");
    }
    else if (seed !== null) restart(seed);
    sim.start();
    audio.playBgm();
  }, () => restart());
  hud.onEndless = () => { sim.exec({ action: "continue" }); audio.playBgm(); notify.push("无尽讨伐 · 妖潮再起"); };
  // R100 结算屏截图分享：Pixi extract 全屏 JPEG → PNG 下载（share-shot 回调）
  hud.onShareShot = () => {
    const canvas = app.renderer.extract.canvas({ target: app.stage, resolution: 1 }) as HTMLCanvasElement;
    const url = canvas.toDataURL("image/jpeg", 0.85);
    const a = document.createElement("a");
    a.href = url;
    a.download = `zhanyaolu-${Date.now()}.jpg`;
    a.click();
    notify.push("战斗截图已保存");
  };
  hud.onShareCopy = (ok) => notify.push(ok ? "战绩已复制，去跟同道切磋吧" : "复制失败（浏览器未授权剪贴板）");
  const equipment = new EquipmentPanel(sim);
  const crafting = new CraftingPanel(sim);
  const fusion = new FusionPanel(sim);
  const upgrade = new UpgradePanel(sim);
  const shop = new ShopPanel(sim);
  const padNav = new PadMenuNav();
  const help = new HelpPanel(sim);
  const pauseMenu = new PauseMenu(sim, audio, settings, () => {
    saveVisualSettings(localStorage, settings);
    view.shakeEnabled = settings.shake;
    view.damageNumbersEnabled = settings.damageNumbers;
  });
  // R36：暂停菜单重开走正常 restart()（保留血誓/自定义种子）
  pauseMenu.onRestart = () => restart();
  const minimap = new MiniMap(() => sim);
  const notify = new Notify();
  const coach = new Coach(sim);
  const eventBuffer: SimEvent[] = [];

  // sim 事件 → 通知条/音效（事件本身仍进 eventBuffer 供 Playwright 断言）
  const handleEvent = (e: SimEvent) => {
    eventBuffer.push(e);
    const d = e.data as Record<string, string>;
    if (e.event === "weapon_gained") { notify.push(`获得武器：${d.name}`); audio.play("weapon_select"); }
    else if (e.event === "aspect_switched") { notify.push(`形态切换：${d.name}`); audio.play("weapon_select", { pitch: 1.3 }); }
    else if (e.event === "chest_opened") {
      const elite = (e.data as { elite?: boolean }).elite === true;
      notify.push(elite ? `精英宝箱开启：${d.weapon}（+符箓）` : `宝箱开启：${d.weapon}`);
      audio.play("upgrade");
    }
    else if (e.event === "achievement") { notify.push(`成就达成：${d.name}`); notify.banner("成就达成", d.name); }
    else if (e.event === "weapon_ascended") { notify.push(`武器进阶：${d.name}`); audio.play("upgrade"); }
    else if (e.event === "weapon_ultimate") { notify.push(`武器终极：${d.name}`); audio.play("combo_5"); }
    else if (e.event === "crafted") { notify.push(`合成：${d.name}`); audio.play("upgrade"); }
    else if (e.event === "heal_picked") { audio.play("charm", { vol: 0.4 }); }
    else if (e.event === "difficulty_unlocked") { notify.push(`新难度解锁：${d.name}（下局可选）`); notify.banner("新难度解锁", d.name); audio.play("combo_5"); }
    else if (e.event === "upgrade_offer") {
      upgrade.show();
      const b = e.data as { banished?: boolean; name?: string; statName?: string };
      if (b.banished) { notify.push(`放逐：${b.name}·${b.statName}（本局不再出现）`); audio.play("charm"); }
    }
    else if (e.event === "upgrade_applied") { notify.push(`符箓铭刻：${d.name}（${UPGRADE_STAT_NAMES[d.stat as keyof typeof UPGRADE_STAT_NAMES] ?? d.stat}）`); audio.play("upgrade"); }
    else if (e.event === "upgrade_skipped") { notify.push(`跳过升级 · 调息回复 ${d.heal} 生命`); audio.play("charm"); }
    else if (e.event === "role_unlocked") { notify.push(`新职业解锁：${d.name}（下局可选）`); notify.banner("新职业解锁", d.name); audio.play("combo_5"); }
    else if (e.event === "fusion_offer") fusion.show();
    else if (e.event === "weather_changed") {
      const WNAMES: Record<string, string> = { Rain: "阴雨", Fog: "浓雾", BloodMoon: "血月升起" };
      // R54：天气播报带实际影响（数值表派生，玩家知道意味着什么）
      notify.push(`${WNAMES[d.type] ? `天气异变：${WNAMES[d.type]}` : "天色放晴"}——${weatherEffectTip(sim.weather.state)}`);
      audio.play(d.type === "BloodMoon" ? "lightning" : "charm");
    }
    else if (e.event === "boss_phase") { notify.push(`妖王狂暴！阶段 ${d.phase}`); view.shake(14); audio.play("lightning", { pitch: 0.8 - (Number(d.phase) - 2) * 0.1, vol: 0.7 }); }
    else if (e.event === "boss_spawn") { notify.push("妖王现世！"); view.shake(10); audio.play("lightning"); rumble.play("boss"); }
    else if (e.event === "final_boss") { notify.banner("终幕妖王", "讨伐成功即平定妖潮"); view.shake(14); audio.play("lightning"); }
    else if (e.event === "elite_wave") { notify.push("精英潮来袭！"); audio.play("charm"); }
    else if (e.event === "threat_tier") { notify.push(`威胁阶 ${d.tier} · 越阶香火 +${d.bonus}`); audio.play("charm", { vol: 0.4 }); }
    else if (e.event === "shrine_spawn") { notify.push("山野间浮起香火龛 · 走近以香火钱换机缘"); audio.play("charm"); }
    else if (e.event === "shop_offer") shop.show();
    else if (e.event === "shop_bought") { notify.push(`香火换得：${d.name}（-${d.price} 文）`); audio.play("charm"); }
    else if (e.event === "shop_denied") { notify.push(`香火不足（需 ${d.price} · 现有 ${d.coins}）`); audio.play("player_hurt", { vol: 0.3 }); }
    else if (e.event === "map_event") {
      const k = (e.data as { kind?: string }).kind;
      const label = k === "surge" ? "妖群合围" : k === "hunt" ? "精锐现身" : k === "goblin" ? "宝妖游荡" : k === "market" ? "庙会开市 · 香龛半价" : "宝藏显形";
      notify.banner("妖潮异变", label);
      notify.push(`妖潮异变：${label}`);
      audio.play(k === "cache" ? "charm" : "lightning");
    }
    else if (e.event === "victory") { notify.banner("妖潮平定", "除妖师大胜"); view.shake(12); audio.play("combo_5"); audio.stopBgm(); }
    else if (e.event === "boss_barrage") { audio.play("magic_attack", { pitch: 0.85, vol: 0.8 }); }
    else if (e.event === "enemy_explode") { view.spawnBurst(Number(d.x), Number(d.y), 0xff7a3d); view.shake(8); audio.play("explosion"); }
    else if (e.event === "goblin_slayed") { view.spawnBurst(Number(d.x), Number(d.y), 0xffd94d); view.shake(6); notify.push("讨伐宝妖，大丰收！"); audio.play("combo_5"); }
    else if (e.event === "goblin_escape") { notify.push("宝妖带着宝藏逃走了…"); audio.play("charm"); }
    else if (e.event === "enemy_heal") view.spawnBurst(Number(d.x), Number(d.y), 0x6ab04c);
    else if (e.event === "enemy_summon") view.spawnBurst(Number(d.x), Number(d.y), 0x8e44ad);
    else if (e.event === "enemy_block") view.spawnBurst(Number(d.x), Number(d.y), 0x8ab8d8);
    else if (e.event === "enemy_blink") { view.spawnBurst(Number(d.x), Number(d.y), 0x8e44ad); audio.play("magic_attack", { pitch: 1.3, vol: 0.5 }); }
    else if (e.event === "enemy_charge") {
      if (d.phase === "windup") { view.spawnBurst(Number(d.x), Number(d.y), 0xd93a2b); audio.play("lightning"); }
      else view.shake(5);
    }
    else if (e.event === "player_dash") audio.play("dash");
    else if (e.event === "perfect_dodge") {
      view.spawnBurst(Number(d.x), Number(d.y), 0xa8741a);
      view.shake(4);
      rumble.play("dodge");
      audio.play("combo_5");
      notify.push("完美闪避！妖物迟滞 · 妖力凝聚");
    }
    else if (e.event === "combo_milestone") {
      const n = Number(d.count ?? 3);
      if (n >= 50) { notify.push(`连击 ${n}！`); audio.play("combo_5"); }
      else if (n >= 10) { notify.push(`连击 ${n}！`); audio.play("combo_3"); }
      else audio.play("combo_3");
    }
    else if (e.event === "combo_century") { notify.push("百连斩！"); notify.banner("百连斩", "连击 100"); audio.play("combo_5"); }
    else if (e.event === "first_elite") { notify.push("讨伐精英！"); audio.play("combo_5"); }
    else if (e.event === "kill_milestone") { notify.push(`击杀 ${d.count}！`); audio.play("combo_3"); }
    else if (e.event === "kill_legend") { notify.push("千人斩！"); notify.banner("千人斩", "单局击杀 1000"); audio.play("combo_5"); }
    else if (e.event === "daily_record") { notify.banner("今日最佳刷新", `分数 ${d.score}`); audio.play("combo_5"); }
    else if (e.event === "rush_record") { notify.banner("连战纪录刷新", `分数 ${d.score}`); audio.play("combo_5"); }
    else if (e.event === "rush_escalate") { notify.push(`妖潮狂化 ${d.tier} 档（妖王血伤×${Number(d.mult).toFixed(2)}）`); audio.play("lightning"); }
    else if (e.event === "game_over") { audio.play("player_die"); audio.stopBgm(); view.shake(18); }
  };

  // 键位路由优先级：升级三选一（1/2/3，吞掉其余键）> 融合面板（Enter/Delete/Esc）
  // > 帮助切页 > 模态开关。H=帮助、Esc=暂停（pause action）、C=装备、I=合成；
  // 模态互斥由各面板内部暂停处理。
  // UI 动作提取为 uiActions 供键盘与触屏按钮排共用（触屏无键盘，同一套互斥语义）。
  const anyModalOpen = () =>
    upgrade.visible || shop.visible || equipment.visible || crafting.visible || fusion.visible || help.visible || pauseMenu.visible;
  // 仅关闭可见面板：不可见面板 toggle(false) 也会改 sim.paused，会破坏升级报价的暂停态
  const closeAllModals = () => {
    if (equipment.visible) equipment.toggle(false);
    if (crafting.visible) crafting.toggle(false);
    if (help.visible) help.toggle(false);
    if (pauseMenu.visible) pauseMenu.toggle(false);
    if (shop.visible) shop.close();
  };
  const uiActions = {
    toggleHelp(): void {
      if (sim.state === "GameOver") return;
      // 已开则只关（旧流程 closeAllModals 后又 toggle 翻回去，H 永远关不上）
      if (help.visible) { closeAllModals(); return; }
      closeAllModals();
      help.toggle();
    },
    togglePause(): void {
      if (anyModalOpen()) { closeAllModals(); return; }
      if (sim.state === "Combat") pauseMenu.toggle();
    },
    openEquipment(): void {
      if (sim.state !== "Combat") return;
      crafting.toggle(false);
      equipment.toggle();
    },
    openCrafting(): void {
      if (sim.state !== "Combat") return;
      equipment.toggle(false);
      crafting.toggle();
    },
    quickRestart(): void {
      // R18 结算屏一键重开：仅 GameOver/Victory 且无模态时；战斗中按 R 无效果（防误触）。
      // R 与升级重抽同键但上下文互斥（报价打开时 upgrade.onKey 先吞掉，此处 unreachable）。
      if (anyModalOpen()) return;
      if (sim.state !== "GameOver" && sim.state !== "Victory") return;
      restart();
    },
  };
  const onKey = (e: KeyboardEvent) => {
    if (upgrade.onKey(e)) return;
    if (shop.onKey(e)) return;
    if (fusion.onKey(e)) return;
    if (help.onKey(e)) return;
    if (e.code === "KeyH") uiActions.toggleHelp();
    else if (e.code === "Escape") uiActions.togglePause();
    else if (e.code === "KeyC") uiActions.openEquipment();
    else if (e.code === "KeyI") uiActions.openCrafting();
    else if (e.code === "KeyR") uiActions.quickRestart();
    else if (e.code === "KeyM") { const muted = audio.toggleMute(); notify.push(muted ? "已静音（M 恢复）" : "声音已恢复"); }
  };
  window.addEventListener("keydown", onKey);

  // R68 浏览器自动播放策略：AudioContext 在 boot（无手势期）创建即为 suspended，
  // 必须在用户手势内 resume，否则 BGM/音效全程静音。pointerdown/keydown 任一首
  // 手势即唤醒（幂等，运行中重复调用无害）。
  const wakeAudio = () => audio.resume();
  window.addEventListener("pointerdown", wakeAudio);
  window.addEventListener("keydown", wakeAudio);

  // R62 手柄：Start 键走键盘 Esc 同一暂停路由；连接/断开播报（轮询不依赖事件，
  // 事件只做 UI 通知——业内惯例：拔插手柄不中断对局）
  input.onStartButton = () => uiActions.togglePause();
  window.addEventListener("gamepadconnected", () =>
    notify.push("手柄已连接：左摇杆移动 · A 攻击 · RB/RT 冲刺 · Start 暂停"));
  window.addEventListener("gamepaddisconnected", () => notify.push("手柄已断开"));

  // 触屏适配：触屏设备（或 ?touch=1）挂载虚拟摇杆/攻击冲刺/UI 按钮排
  if (shouldUseTouch()) {
    input.setTouchSource(new TouchControls(uiActions));
    hud.setHint("左侧摇杆移动 · 击钮攻击 · 冲钮冲刺");
  }
  // R19 显隐缓存（桌面端无触屏节点，恒 null 跳过）
  const touchRoot = document.getElementById("touch-root");
  const joyBase = document.getElementById("joy-base");
  const touchBtns = document.getElementById("touch-btns");

  function restart(seed?: number, mode?: "normal" | "boss_rush"): void {
    // R18 记住上局选择：职业/难度沿用（同存档下锁天然成立，无需额外校验）；
    // R22 自定义种子优先（输入框留值则连打同一种子；清空回随机）；
    // R44 模式沿用（Boss 连战后 R 重开仍连战；普通开局回普通模式）
    const roleId = sim.role.id;
    const difficultyId = sim.difficulty.id;
    const runMode: "normal" | "boss_rush" = mode ?? (sim.bossRush ? "boss_rush" : "normal");
    const runSeed = seed ?? hud.pendingSeed() ?? Date.now() % 100000;
    // R25 血誓沿用（同职业/难度）
    sim = new GameSim(runSeed, localStorageStorage(), roleId, difficultyId, { ...sim.pactTiers }, runMode);
    wireSim();
    input.forceSim(sim);
    hud.resimulate(sim);
    equipment.resimulate(sim);
    crafting.resimulate(sim);
    upgrade.resimulate(sim);
    help.resimulate(sim);
    fusion.resimulate(sim);
    pauseMenu.resimulate(sim);
    shop.resimulate(sim);
    coach.resimulate(sim);
    input.clearExternal();
  }

  function wireSim(): void {
    // 命中音效节流：高密度割草时限制最短间隔，避免机枪感
    let lastHitSfxAt = 0;
    sim.onDamage.push((x, y, dmg, crit) => {
      view.spawnDamageNumber(x, y, dmg, crit);
      view.spawnHitSpark(x, y);
      view.shake(crit ? 9 : 4.5);
      const now = performance.now();
      if (now - lastHitSfxAt >= 40) {
        lastHitSfxAt = now;
        // 每击随机变调，暴击高亢——打击的听觉锚点
        audio.play("enemy_hit", { pitch: crit ? 1.25 : 0.92 + Math.random() * 0.16, vol: 0.6 });
      }
    });
    sim.onAttack.push((type, ang) => {
      audio.play(type === "Ranged" ? "ranged_attack" : type === "Magic" ? "magic_attack" : "melee_attack");
      view.playAttack(type);
      if (type === "Melee") view.spawnSlash(sim.player.x, sim.player.y, ang, sim.player.dashTimer > 0);
    });
    sim.onKill.push((_type, elite, boss) => {
      audio.play("enemy_die");
      view.shake(boss ? 12 : elite ? 8 : 4);
      rumble.play("kill"); // 节流在 RumbleManager 内（割草不常转）
    });
    sim.onPlayerHurt.push(() => { audio.play("player_hurt"); rumble.play("hurt"); });
    sim.onDashStrike.push((x, y) => { view.shake(3); view.spawnBurst(x, y, 0x6b6154); });
    sim.onPerfectDodge.push((x, y) => view.spawnRipple(x, y));
    sim.onEvent(handleEvent);
  }
  wireSim();

  // debug API：协议与字段名对齐 Godot 版 ExternalCommandService。
  // input 指令走 InputManager（外部输入与真实键盘叠加、持续有效），
  // 其余指令透传 sim.exec。
  window.__CNM__ = {
    version: "0.2.0-m5",
    exec(cmd) {
      if (cmd.action === "input") {
        input.applyExternal(cmd.args ?? {});
        return { id: cmd.id ?? -1, ok: true, data: { applied: true } };
      }
      return sim.exec(cmd);
    },
    getEvents() { return eventBuffer.splice(0); },
    ready: true,
    // 页面内截图（WebGL 画布无法直接 toDataURL，走 Pixi extract 离屏渲染）
    shot(scale = 0.4) {
      const canvas = app.renderer.extract.canvas({ target: app.stage, resolution: scale }) as HTMLCanvasElement;
      return canvas.toDataURL("image/jpeg", 0.7);
    },
    /** R100 结算屏截图分享（hud 复用；JPEG dataURL）。 */
    capture: () => {
      const canvas = app.renderer.extract.canvas({ target: app.stage, resolution: 1 }) as HTMLCanvasElement;
      return canvas.toDataURL("image/jpeg", 0.85);
    },
    get sim() { return sim; },
    get fps() { return Math.round(app.ticker.FPS); },
    get audioState() { return audio.state; },
  };

  // 主循环：固定 60Hz 步长累加器（确定性），命中停顿按墙钟缩放 dt，渲染每帧读最新状态。
  // 凝固期间渲染 dt 同步减速——飘字/动画/相机与逻辑一起凝滞（打击感的"世界暂停"感）
  let acc = 0;
  let last = performance.now();
  app.ticker.add(() => {
    const now = performance.now();
    const frameMs = Math.min((now - last) / 1000, 0.25) * 1000;
    last = now;
    acc += frameMs / 1000;
    while (acc >= FIXED_DT) {
      // R61 表现设置：关闭顿帧时凝固无消费方（逻辑与渲染都全速推进）
      const scale = settings.hitstop ? sim.hitStop.scale(FIXED_DT * 1000) : 1;
      input.sample();
      sim.tick(FIXED_DT * scale);
      acc -= FIXED_DT;
    }
    const dtSec = (app.ticker.deltaMS / 1000) *
      (sim.hitStop.active && settings.hitstop ? HITSTOP.FrozenScale : 1);
    view.render(sim, dtSec);
    hud.update();
    minimap.setCamera(view.getCamera().x, view.getCamera().y, view.getCamera().w, view.getCamera().h);
    minimap.update();
    if (equipment.visible) equipment.refresh();
    if (crafting.visible) crafting.refresh();
    fusion.update();
    upgrade.update();
    shop.update();
    coach.update();
    // R19 触屏层级：非战斗藏整棵触屏 UI；模态打开时藏摇杆/击/冲（留 UI 按钮排解暂停）
    if (touchRoot) touchRoot.classList.toggle("hidden", sim.state !== "Combat");
    const modal = anyModalOpen();
    if (joyBase) joyBase.classList.toggle("hidden", modal);
    if (touchBtns) touchBtns.classList.toggle("hidden", modal);
    // R76 手柄菜单导航：模态开着时 dpad/摇杆移动焦点、A 确认、B 关闭
    // （高频三面板：暂停/升级/商店；升级面板不可取消故 B 无效）
    if (modal) {
      const container = pauseMenu.visible ? pauseMenu.el
        : upgrade.visible ? upgrade.el
        : shop.visible ? shop.el
        : equipment.visible ? equipment.el
        : crafting.visible ? crafting.el
        : help.visible ? help.el
        : null;
      padNav.sync(container);
      const menu = input.menuRead();
      if (menu.back) {
        if (shop.visible) shop.close();
        else if (equipment.visible) equipment.toggle(false);
        else if (crafting.visible) crafting.toggle(false);
        else if (help.visible) help.toggle(false);
        else if (pauseMenu.visible) pauseMenu.toggle(false);
      } else if (menu.reroll && shop.visible) {
        shop.reroll(); // R79 手柄 Y 刷新报价
      } else if (menu.equip) {
        equipment.toggle(); // R122 手柄 X 开合装备面板
      } else if (menu.reroll && !shop.visible) {
        crafting.toggle(); // R122 手柄 Y（非商店时）开合合成
      } else if (help.visible && menu.rb) {
        help.onKey({ code: "ArrowRight" } as KeyboardEvent); // R150 RB 下一页
      } else if (help.visible && menu.lb) {
        help.onKey({ code: "ArrowLeft" } as KeyboardEvent); // R150 LB 上一页
      } else if (container) {
        padNav.handle(menu);
      }
    } else if (sim.state === "GameOver" || sim.state === "Victory") {
      // R80 结算屏手柄「再来一局」：dpad 移动焦点、A 点击（quickRestart 语义同 R 键）
      const panel = sim.state === "GameOver" ? hud.gameover : hud.victory;
      padNav.sync(panel);
      const menu = input.menuRead();
      if (menu.confirm) padNav.handle(menu); // A 点击焦点按钮（再来一局）
      else if (menu.up || menu.down || menu.left || menu.right) padNav.handle(menu);
    } else {
      padNav.detach();
    }
  });

  // 启动完成：移除 boot 文案
  document.getElementById("boot")?.remove();
}

boot().catch((e) => {
  const boot = document.getElementById("boot");
  if (boot) boot.textContent = `启动失败: ${e}`;
  console.error(e);
});
