import { describe, expect, it } from "vitest";
import { GameSim } from "../../src/sim/game";
import { memoryStorage } from "../../src/sim/save";

describe("R42 天气回顾", () => {
  it("tick 按当前天气累加驻留时长", () => {
    const sim = new GameSim(421, memoryStorage());
    sim.exec({ action: "start" });
    sim.enemies.length = 0;
    sim.terrain.altars.length = 0;
    const t0 = sim.survivalTime;
    for (let i = 0; i < 60; i++) sim.tick(); // 1 秒
    const type = sim.weather.state.type;
    expect(sim.weatherTime[type]).toBeGreaterThanOrEqual(1);
    expect(sim.status()).toMatchObject({});
    void t0;
  });

  it("切天气后分段累加（snapshot 可读）", () => {
    const sim = new GameSim(421, memoryStorage());
    sim.exec({ action: "start" });
    sim.weather.state.type = "Rain";
    for (let i = 0; i < 60; i++) sim.tick();
    sim.weather.state.type = "Fog";
    for (let i = 0; i < 60; i++) sim.tick();
    const snap = sim.status() as { weather_time: Record<string, number> };
    expect(snap.weather_time.Rain).toBeGreaterThanOrEqual(1);
    expect(snap.weather_time.Fog).toBeGreaterThanOrEqual(1);
  });
});
