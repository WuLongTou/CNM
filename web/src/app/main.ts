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
import { MiniMap } from "../ui/minimap";
import { Notify } from "../ui/notify";
import "./style.css";

const FIXED_DT = 1 / 60;

declare global {
  interface Window {
    /** 调试/AI 测试 API（JSON 指令协议，与 Godot 旧版 cmd-port 同构）。 */
    __CNM__: {
      version: string;
      exec: (cmd: { id?: number; action: string; args?: Record<string, unknown> }) => ExecResult;
      getEvents: () => SimEvent[];
      ready: boolean;
      /** 直接访问 sim 实例（Playwright evaluate 深度断言用）。 */
      sim: GameSim;
    };
  }
}

async function boot(): Promise<void> {
  const app = new Application();
  await app.init({ resizeTo: window, background: 0x140a0a, antialias: true });
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
  });
  sim.onKill.push(() => audio.play("zombie_die"));
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
    else if (e.event === "fusion_offer") fusion.show();
    else if (e.event === "boss_phase") { notify.push(`妖王狂暴！阶段 ${d.phase}`); view.shake(14); }
    else if (e.event === "game_over") { audio.play("game_over"); view.shake(18); }
  };

  // 键位路由优先级：融合面板（Enter/Delete/Esc）> 帮助切页 > 模态开关。
  // H=帮助、Esc=暂停（pause action）、C=装备、I=合成；模态互斥由各面板内部暂停处理。
  const anyModalOpen = () => equipment.visible || crafting.visible || fusion.visible || help.visible || pauseMenu.visible;
  const closeAllModals = () => {
    equipment.toggle(false); crafting.toggle(false); help.toggle(false); pauseMenu.toggle(false);
  };
  const onKey = (e: KeyboardEvent) => {
    if (fusion.onKey(e)) return;
    if (help.onKey(e)) return;
    if (e.code === "KeyH" && sim.state !== "GameOver") {
      closeAllModals();
      help.toggle();
      return;
    }
    if (e.code === "Escape") {
      if (anyModalOpen()) { closeAllModals(); return; }
      if (sim.state === "Combat") pauseMenu.toggle();
      return;
    }
    if (sim.state !== "Combat") return;
    if (e.code === "KeyC") {
      crafting.toggle(false);
      equipment.toggle();
    } else if (e.code === "KeyI") {
      equipment.toggle(false);
      crafting.toggle();
    }
  };
  window.addEventListener("keydown", onKey);

  function restart(): void {
    sim = new GameSim(Date.now() % 100000, localStorageStorage());
    wireSim();
    input.forceSim(sim);
    hud.resimulate(sim);
    equipment.resimulate(sim);
    crafting.resimulate(sim);
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
    });
    sim.onKill.push(() => audio.play("zombie_die"));
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
    get sim() { return sim; },
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
  });

  // 启动完成：移除 boot 文案
  document.getElementById("boot")?.remove();
}

boot().catch((e) => {
  const boot = document.getElementById("boot");
  if (boot) boot.textContent = `启动失败: ${e}`;
  console.error(e);
});
