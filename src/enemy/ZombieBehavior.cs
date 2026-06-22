namespace ZombieWars.Enemy;

using Godot;

/// <summary>僵尸行为接口 — 复杂 AI 类型的独立行为组件。</summary>
public interface IZombieBehavior
{
    /// <summary>初始化行为（在 Zombie._Ready 中调用）</summary>
    void OnReady(Zombie zombie);
    /// <summary>每帧更新（在 Zombie._PhysicsProcess 中调用）</summary>
    void OnProcess(Zombie zombie, float delta);
}
