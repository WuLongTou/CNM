// 应用装配入口：PixiJS 初始化、固定步长主循环（含命中停顿）、debug API、
// 音频、全套 UI 装配（HUD/装备/合成/融合/帮助/暂停/小地图/通知）。

import { Application } from "pixi.js";
import { GameSim, type ExecResult, type SimEvent } from "../sim/game";
import { localStorageStorage } from "../sim/save";
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
import { MiniMap } from "../ui/minimap";
import { Notify } from "../ui/notify";
import { TouchControls, shouldUseTouch } from "../ui/touch";
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
      /** 直接访问 sim 实例（Playwright evaluate 深度断言用）。 */
      sim: GameSim;
      /** 当前渲染帧率（ticker 滚动平均；上限 TARGET_FPS=60）。 */
      fps: number;
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
  const view = new GameView();
  const input = new InputManager(sim);
  const audio = new AudioManager();
  await view.init(app);
  view.attachFx();
  await audio.init();

  const hud = new Hud(sim, () => { sim.start(); audio.playBgm(); }, () => restart());
  const equipment = new EquipmentPanel(sim);
  const crafting = new CraftingPanel(sim);
  const fusion = new FusionPanel(sim);
  const upgrade = new UpgradePanel(sim);
  const help = new HelpPanel(sim);
  const pauseMenu = new PauseMenu(sim, audio);
  const minimap = new MiniMap(() => sim);
  const notify = new Notify();
  const eventBuffer: SimEvent[] = [];

  // 表现层订阅：伤害数字 + 震屏 + 音效
  sim.onDamage.push((x, y, dmg, crit) => {
    view.spawnDamageNumber(x, y, dmg, crit);
    view.shake(crit ? 8 : 3);
  });
  sim.onAttack.push((type) => {
    audio.play(type === "Ranged" ? "ranged_attack" : type === "Magic" ? "magic_attack" : "melee_attack");
    view.playAttack();
  });
  sim.onKill.push(() => audio.play("enemy_die"));
  sim.onPlayerHurt.push(() => audio.play("player_hurt"));

  // sim 事件 → 通知条/音效（事件本身仍进 eventBuffer 供 Playwright 断言）
  const handleEvent = (e: SimEvent) => {
    eventBuffer.push(e);
    const d = e.data as Record<string, string>;
    if (e.event === "weapon_gained") { notify.push(`获得武器：${d.name}`); audio.play("weapon_select"); }
    else if (e.event === "chest_opened") { notify.push(`宝箱开启：${d.weapon}`); audio.play("upgrade"); }
    else if (e.event === "achievement") notify.push(`成就达成：${d.name}`);
    else if (e.event === "weapon_ascended") { notify.push(`武器进阶：${d.name}`); audio.play("upgrade"); }
    else if (e.event === "weapon_ultimate") { notify.push(`武器终极：${d.name}`); audio.play("combo_5"); }
    else if (e.event === "crafted") { notify.push(`合成：${d.name}`); audio.play("upgrade"); }
    else if (e.event === "upgrade_offer") upgrade.show();
    else if (e.event === "upgrade_applied") { notify.push(`符箓铭刻：${d.name}（${d.stat === "dmg" ? "伤害" : d.stat === "asp" ? "攻速" : "范围"}）`); audio.play("upgrade"); }
    else if (e.event === "fusion_offer") fusion.show();
    else if (e.event === "weather_changed") {
      const WNAMES: Record<string, string> = { Rain: "阴雨", Fog: "浓雾", BloodMoon: "血月升起" };
      notify.push(WNAMES[d.type] ? `天气异变：${WNAMES[d.type]}` : "天色放晴");
      audio.play(d.type === "BloodMoon" ? "lightning" : "charm");
    }
    else if (e.event === "boss_phase") { notify.push(`妖王狂暴！阶段 ${d.phase}`); view.shake(14); }
    else if (e.event === "enemy_explode") { view.spawnBurst(Number(d.x), Number(d.y), 0xff7a3d); view.shake(8); audio.play("explosion"); }
    else if (e.event === "enemy_heal") view.spawnBurst(Number(d.x), Number(d.y), 0x6ab04c);
    else if (e.event === "enemy_summon") view.spawnBurst(Number(d.x), Number(d.y), 0x8e44ad);
    else if (e.event === "enemy_block") view.spawnBurst(Number(d.x), Number(d.y), 0x8ab8d8);
    else if (e.event === "enemy_charge") {
      if (d.phase === "windup") { view.spawnBurst(Number(d.x), Number(d.y), 0xd93a2b); audio.play("enemy_hit"); }
      else view.shake(5);
    }
    else if (e.event === "player_dash") audio.play("dash");
    else if (e.event === "combo_milestone") audio.play("combo_3");
    else if (e.event === "game_over") { audio.play("player_die"); audio.stopBgm(); view.shake(18); }
  };

  // 键位路由优先级：升级三选一（1/2/3，吞掉其余键）> 融合面板（Enter/Delete/Esc）
  // > 帮助切页 > 模态开关。H=帮助、Esc=暂停（pause action）、C=装备、I=合成；
  // 模态互斥由各面板内部暂停处理。
  // UI 动作提取为 uiActions 供键盘与触屏按钮排共用（触屏无键盘，同一套互斥语义）。
  const anyModalOpen = () =>
    upgrade.visible || equipment.visible || crafting.visible || fusion.visible || help.visible || pauseMenu.visible;
  // 仅关闭可见面板：不可见面板 toggle(false) 也会改 sim.paused，会破坏升级报价的暂停态
  const closeAllModals = () => {
    if (equipment.visible) equipment.toggle(false);
    if (crafting.visible) crafting.toggle(false);
    if (help.visible) help.toggle(false);
    if (pauseMenu.visible) pauseMenu.toggle(false);
  };
  const uiActions = {
    toggleHelp(): void {
      if (sim.state === "GameOver") return;
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
  };
  const onKey = (e: KeyboardEvent) => {
    if (upgrade.onKey(e)) return;
    if (fusion.onKey(e)) return;
    if (help.onKey(e)) return;
    if (e.code === "KeyH") uiActions.toggleHelp();
    else if (e.code === "Escape") uiActions.togglePause();
    else if (e.code === "KeyC") uiActions.openEquipment();
    else if (e.code === "KeyI") uiActions.openCrafting();
  };
  window.addEventListener("keydown", onKey);

  // 触屏适配：触屏设备（或 ?touch=1）挂载虚拟摇杆/攻击冲刺/UI 按钮排
  if (shouldUseTouch()) {
    input.setTouchSource(new TouchControls(uiActions));
    hud.setHint("左侧摇杆移动 · 击钮攻击 · 冲钮冲刺");
  }

  function restart(): void {
    sim = new GameSim(Date.now() % 100000, localStorageStorage());
    wireSim();
    input.forceSim(sim);
    hud.resimulate(sim);
    equipment.resimulate(sim);
    crafting.resimulate(sim);
    upgrade.resimulate(sim);
    help.resimulate(sim);
    input.clearExternal();
  }

  function wireSim(): void {
    sim.onDamage.push((x, y, dmg, crit) => {
      view.spawnDamageNumber(x, y, dmg, crit);
      view.shake(crit ? 8 : 3);
    });
    sim.onAttack.push((type) => {
      audio.play(type === "Ranged" ? "ranged_attack" : type === "Magic" ? "magic_attack" : "melee_attack");
      view.playAttack();
    });
    sim.onKill.push(() => audio.play("enemy_die"));
    sim.onPlayerHurt.push(() => audio.play("player_hurt"));
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
    get sim() { return sim; },
    get fps() { return Math.round(app.ticker.FPS); },
  };

  // 主循环：固定 60Hz 步长累加器（确定性），命中停顿按墙钟缩放 dt，渲染每帧读最新状态
  let acc = 0;
  let last = performance.now();
  app.ticker.add(() => {
    const now = performance.now();
    const frameMs = Math.min((now - last) / 1000, 0.25) * 1000;
    last = now;
    acc += frameMs / 1000;
    while (acc >= FIXED_DT) {
      const scale = sim.hitStop.scale(FIXED_DT * 1000);
      input.sample();
      sim.tick(FIXED_DT * scale);
      acc -= FIXED_DT;
    }
    const dtSec = app.ticker.deltaMS / 1000;
    view.render(sim, dtSec);
    hud.update();
    minimap.setCamera(view.getCamera().x, view.getCamera().y, view.getCamera().w, view.getCamera().h);
    minimap.update();
    if (equipment.visible) equipment.refresh();
    if (crafting.visible) crafting.refresh();
    fusion.update();
    upgrade.update();
  });

  // 启动完成：移除 boot 文案
  document.getElementById("boot")?.remove();
}

boot().catch((e) => {
  const boot = document.getElementById("boot");
  if (boot) boot.textContent = `启动失败: ${e}`;
  console.error(e);
});
