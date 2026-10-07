---
name: cnm-auto-test
description: 斩妖录 Web 版自动化测试协议（window.__CNM__ 指令与事件接口）。触发：用 JSON 指令协议驱动游戏做自动化测试、状态探查、跳阶、战斗压测时加载。
---

# CNM 自动测试入口

页面加载后暴露 `window.__CNM__`（JSON 指令协议，与旧版 cmd-port 同构）：
`exec({id, action, args})` 支持 status/game_state/start/input/choose/force_combat/
spawn/give/upgrade/weapons/craft/offer_fusion/buy_meta/meta/inventory/teleport/
goto_round（测试跳阶）/continue（无尽）/quit；
`getEvents()` 拉取事件推送；`sim` 直接暴露 GameSim 实例。

## 冒烟 hygiene（实测）

- `getEvents()` 读即清空（splice）：`waitForFunction` 轮询会与其消费竞态，
  事件断言用“定时候读缓冲”（sleep 后一次性读）或 `sim.onEvent` 直接窃听更稳。
- 战斗冒烟先清场：`sim.enemies/terrain.altars/chests/pickups` 置空，否则出生点旁的
  融合祭坛会在第一 tick 弹模态暂停全年；`spawn` 的 `near` 带随机偏移，精确摆位须
  事后覆写 `e.x/e.y` 并固定 `player.aimAng`。
- 通知条类名是 `.notify`（非 `.notify-item`），只保留最近若干条、数秒过期——
  断言通知文本要在 1 秒左右抓取，过期/被顶掉属正常，不代表链路坏（以事件为准）。

## 跑 e2e 的进程坑（2026-10 实测）

- `playwright.config` 的 webServer 是 `reuseExistingServer: true`：只要 5173 有响应就**复用**，
  不会校验它属于哪棵树。若上一次全量 e2e 起的 dev server 仍挂在 main 树，随后在 worktree
  里跑 spec 会**对着 main 的代码**断言——本轮实测表现为「新加的 fusion_done 纸签查不到」，
  而其他用例全绿（因为它们断言的特性 main 里已有），极易误判成自己代码写错。
  排查与规矩：跑 worktree 的 e2e 前先 `lsof -tiTCP:5173 -sTCP:LISTEN` 确认为空（或确属本树），
  用完 `kill` 掉；全量 e2e 结束后也顺手清理该 server。
- 证据链：`lsof -p <pid> | grep cwd` 直接看 server 的工作目录，一眼分辨归属。
