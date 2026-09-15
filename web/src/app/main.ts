// 应用装配入口：PixiJS 初始化、固定步长主循环、debug API、UI 装配。

import { Application } from "pixi.js";
import { GameSim, type ExecResult, type SimEvent } from "../sim/game";
import { localStorageStorage } from "../sim/save";
import { GameView } from "../render/view";
import { InputManager } from "./input";
import { Hud } from "../ui/hud";
import "./style.css";

const FIXED_DT = 1 / 60;

declare global {
  interface Window {
    /** 调试/AI 测试 API（协议对齐 ExternalCommandService）。 */
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
  await view.init(app);
  const hud = new Hud(sim, () => sim.start(), () => restart());

  function restart(): void {
    sim = new GameSim(Date.now() % 100000, localStorageStorage());
    sim.onEvent((e) => eventBuffer.push(e));
    input.forceSim(sim);
    hud.resimulate(sim);
    input.clearExternal();
  }

  // debug API：协议与字段名对齐 Godot 版 ExternalCommandService。
  // input 指令走 InputManager（外部输入与真实键盘叠加、持续有效），
  // 其余指令透传 sim.exec。
  const eventBuffer: SimEvent[] = [];
  sim.onEvent((e) => eventBuffer.push(e));
  window.__CNM__ = {
    version: "0.1.0-m1",
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

  // 主循环：固定 60Hz 步长累加器（确定性），渲染每帧插值读取最新状态
  let acc = 0;
  let last = performance.now();
  app.ticker.add(() => {
    const now = performance.now();
    acc += Math.min((now - last) / 1000, 0.25); // 切后台回来避免补帧风暴
    last = now;
    while (acc >= FIXED_DT) {
      input.sample();
      sim.tick(FIXED_DT);
      acc -= FIXED_DT;
    }
    view.render(sim, app.ticker.deltaMS / 1000);
    hud.update();
  });

  // 启动完成：移除 boot 文案
  document.getElementById("boot")?.remove();
}

boot().catch((e) => {
  const boot = document.getElementById("boot");
  if (boot) boot.textContent = `启动失败: ${e}`;
  console.error(e);
});
