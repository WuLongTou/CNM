namespace ZombieWars.Player;

using System;
using System.Collections.Generic;
using Godot;
using ZombieWars.Core;
using static ZombieWars.Core.GameEnums;

/// <summary>
/// One inventory slot holding one consumable item type and a count.
/// </summary>
public class ConsumableSlot
{
    public ConsumableItem Item { get; set; }
    public int Count { get; set; }

    public ConsumableSlot(ConsumableItem item)
    {
        Item = item;
        Count = 1;
    }

    public bool IsEmpty => Count <= 0;
    public bool IsFull => Count >= Item.MaxStack;
}

/// <summary>
/// Manages the player's consumable inventory: up to 3 slots,
/// global cooldown per slot, hotkey input (1/2/3), and HUD updates.
/// Attached to Player as a child node.
/// </summary>
public partial class ConsumableInventory : Node
{
    public const int MaxSlots = 3;
    public const float GlobalCooldown = 0.5f; // minimum time between any two uses

    /// <summary>Fired when a slot is used: (slotIndex 0-based, newCount).</summary>
    public event Action<int, int>? SlotChanged;

    private readonly List<ConsumableSlot> _slots = new();
    private float[] _cooldowns = new float[MaxSlots];
    private float _globalCooldown;
    private Player? _player;

    public override void _Ready()
    {
        base._Ready();
        // Auto-register two starting slots so the player always has something
        AddSlot(new HealthPotionItem());
        AddSlot(new SpeedPotionItem());
    }

    public void Setup(Player player)
    {
        _player = player;
    }

    // ─── Slot Management ────────────────────────────────────────────────────────

    /// <summary>Add a new slot or top up an existing one. Returns false if inventory is full.</summary>
    public bool AddItem(ConsumableItem item)
    {
        // Try to stack into existing slot of same type
        foreach (var slot in _slots)
        {
            if (slot.Item.Type == item.Type && !slot.IsFull)
            {
                slot.Count++;
                SlotChanged?.Invoke(_slots.IndexOf(slot), slot.Count);
                return true;
            }
        }

        // Try to open a new slot
        if (_slots.Count >= MaxSlots)
            return false;

        _slots.Add(new ConsumableSlot(item));
        SlotChanged?.Invoke(_slots.Count - 1, 1);
        return true;
    }

    private void AddSlot(ConsumableItem item)
    {
        _slots.Add(new ConsumableSlot(item));
    }

    public int SlotCount => _slots.Count;

    public ConsumableSlot? GetSlot(int index)
    {
        if (index < 0 || index >= _slots.Count) return null;
        return _slots[index];
    }

    public float GetCooldown(int index) =>
        index < 0 || index >= MaxSlots ? 0.0f : _cooldowns[index];

    public bool IsOnCooldown(int index) =>
        index < 0 || index >= MaxSlots ? true : _cooldowns[index] > 0.0f;

    // ─── Input Handler ─────────────────────────────────────────────────────────

    public override void _Process(double delta)
    {
        float d = (float)delta;

        // Tick cooldowns
        for (int i = 0; i < MaxSlots; i++)
        {
            if (_cooldowns[i] > 0)
                _cooldowns[i] = Mathf.Max(0.0f, _cooldowns[i] - d);
        }
        if (_globalCooldown > 0)
            _globalCooldown = Mathf.Max(0.0f, _globalCooldown - d);

        // Check hotkeys (only on the frame they are pressed)
        if (_globalCooldown > 0 || _player == null || _player.IsDead)
            return;

        if (Input.IsActionJustPressed("consumable_1")) TryUseSlot(0);
        else if (Input.IsActionJustPressed("consumable_2")) TryUseSlot(1);
        else if (Input.IsActionJustPressed("consumable_3")) TryUseSlot(2);
    }

    /// <summary>Attempt to use slot index (0-based). Returns true if the item was used.</summary>
    public bool TryUseSlot(int index)
    {
        if (_globalCooldown > 0) return false;
        if (IsOnCooldown(index)) return false;
        if (_player == null || _player.IsDead) return false;

        var slot = GetSlot(index);
        if (slot == null || slot.IsEmpty) return false;

        bool success = slot.Item.Apply(_player);
        if (success)
        {
            slot.Count--;
            _cooldowns[index] = slot.Item.Cooldown;
            _globalCooldown = GlobalCooldown;
            SlotChanged?.Invoke(index, slot.Count);
            GameServices.UI?.UpdateConsumableDisplay(_player!);
            LogUtils.Print($"使用 {slot.Item.Name}，剩余 {slot.Count}");
        }
        return success;
    }

    // ─── Notifications ──────────────────────────────────────────────────────────

    public void OnPlayerDied()
    {
        _globalCooldown = float.MaxValue; // block all use
    }

    public void OnNewRound()
    {
        // Cooldowns do NOT reset between rounds — intentional
    }

    public void Serialize(out int[] counts)
    {
        counts = new int[MaxSlots];
        for (int i = 0; i < MaxSlots; i++)
            counts[i] = i < _slots.Count ? _slots[i].Count : 0;
    }
}