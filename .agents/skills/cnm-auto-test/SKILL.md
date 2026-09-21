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
