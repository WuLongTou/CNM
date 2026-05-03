namespace ZombieWars.Weapons;

using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using static ZombieWars.Core.GameEnums;
using Player = ZombieWars.Player.Player;

/// <summary>
/// Manages the weapon/equipment card pool and smart draw logic.
/// Responsible for initialising the library and recommending upgrade paths.
/// </summary>
public partial class WeaponDeck : Node
{
	private Array<WeaponCard> _allWeapons = new();
	private Godot.Collections.Dictionary _prerequisites = new();
	private WeaponEvolutionSystem _evolution = new() { Name = "EvolutionSystem" };

	public override void _Ready()
	{
		_allWeapons = new Array<WeaponCard>(WeaponFactory.BuildAll());
		_prerequisites = WeaponFactory.GetPrerequisites();
		AddChild(_evolution);
	}

	/// <summary>
	/// Draws up to 3 unique cards, optionally filtered by the player's current loadout.
	/// </summary>
	public Array<WeaponCard> DrawThreeCards(Player? player = null)
	{
		Array<WeaponCard> pool;

		if (player == null)
		{
			pool = new Array<WeaponCard>(_allWeapons);
		}
		else
		{
			pool = _BuildSmartPool(player);
		}

		if (pool.Count == 0)
			pool = new Array<WeaponCard>(_allWeapons);

		// Fisher-Yates shuffle.
		var list = new System.Collections.Generic.List<WeaponCard>(pool);
		var rng = new RandomNumberGenerator();
		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = (int)(rng.Randi() % (i + 1));
			(list[i], list[j]) = (list[j], list[i]);
		}
		pool = new Array<WeaponCard>(list);

		// Draw unique cards using HashSet for O(1) lookups
		var drawn = new List<WeaponCard>(3);
		var names = new HashSet<string>();

		for (int i = 0; i < pool.Count && drawn.Count < 3; i++)
		{
			var card = pool[i];
			if (names.Add(card.CardName))
			{
				drawn.Add(card);
			}
		}

		// Fill remaining slots from the full library
		if (drawn.Count < 3)
		{
			var backup = new System.Collections.Generic.List<WeaponCard>(_allWeapons);
			for (int i = backup.Count - 1; i > 0; i--)
			{
				int j = (int)(rng.Randi() % (i + 1));
				(backup[i], backup[j]) = (backup[j], backup[i]);
			}

			foreach (var card in backup)
			{
				if (drawn.Count >= 3)
					break;
				if (names.Add(card.CardName))
				{
					drawn.Add(card);
				}
			}
		}

		return new Array<WeaponCard>(drawn);
	}

	public WeaponCard? GetRandomWeapon()
	{
		if (_allWeapons.Count == 0)
			return null;
		return _allWeapons[(int)(GD.Randi() % _allWeapons.Count)];
	}

	private Array<WeaponCard> _BuildSmartPool(Player player)
	{
		var inventoryNames = new HashSet<string>();
		string? currentType = null;
		int currentTier = 0;

		var playerFx = new Dictionary
		{
			["melee"]  = new Dictionary(),
			["ranged"] = new Dictionary(),
			["magic"]  = new Dictionary()
		};

		foreach (var w in player.ActiveEffects)
		{
			inventoryNames.Add(w.CardName);

			if (w.Category == WeaponCategory.Weapon)
			{
				currentType = w.GetWeaponTypeText();
				currentTier = Math.Max(currentTier, w.Tier);
			}

			_CollectEffects(w, playerFx);
		}

		var upgradePool    = new List<WeaponCard>();
		var sidegradePool = new List<WeaponCard>();
		var offTypePool   = new List<WeaponCard>();
		var equipmentPool = new List<WeaponCard>();

		foreach (var w in _allWeapons)
		{
			if (w.Category == WeaponCategory.Equipment)
			{
				if (!inventoryNames.Contains(w.CardName) && _MeetsPrerequisite(w, inventoryNames))
					equipmentPool.Add(w);
				continue;
			}

			if (currentType != null && w.GetWeaponTypeText() == currentType)
			{
				if (_IsValidUpgrade(w, inventoryNames, playerFx, currentTier))
				{
					var nameArray = new Godot.Collections.Array();
					foreach (var n in inventoryNames) nameArray.Add(n);
					if (w.Tier == currentTier + 1 && w.IsUpgradeFor(nameArray))
						upgradePool.Add(w);
					else if (w.Tier == currentTier)
						sidegradePool.Add(w);
				}
			}
			else
			{
				if (w.Rarity <= 2 && !inventoryNames.Contains(w.CardName) && _MeetsPrerequisite(w, inventoryNames))
					offTypePool.Add(w);
			}
		}

		// Weighted pool construction
		var pool = new List<WeaponCard>();
		foreach (var w in upgradePool)
		{
			pool.Add(w);
			pool.Add(w); // upgrade cards get double weight
		}
		foreach (var w in sidegradePool) pool.Add(w);
		foreach (var w in offTypePool)   pool.Add(w);
		foreach (var w in equipmentPool)
		{
			if (GD.Randf() < 0.35f)
				pool.Add(w);
		}

		// Upgrade cards from existing effects
		float upgradeWeight = 0.25f;
		if (player.ActiveEffects.Count >= 4)
			upgradeWeight = 0.55f;
		else if (player.ActiveEffects.Count >= 2)
			upgradeWeight = 0.40f;

		var addedTargets = new HashSet<string>();
		foreach (var effect in player.ActiveEffects)
		{
			if (effect is WeaponCard wc && addedTargets.Add(wc.CardName))
			{
				if (GD.Randf() < upgradeWeight)
				{
					pool.Add(UpgradeGenerator.Generate(wc));
				}
			}
		}

		return new Array<WeaponCard>(pool);
	}

	private int _GetFxLevel(Dictionary dict, int key)
	{
		var vk = (Variant)key;
		return dict.TryGetValue(vk, out var v) ? v.AsInt32() : 0;
	}

	private void _CollectEffects(WeaponCard w, Dictionary fx)
	{
		foreach (Variant ve in w.MeleeEffects)
		{
			int ek = (int)ve;
			int lv = _GetFxLevel(w.MeleeEffectLevels, ek);
			int current = _GetFxLevel((Dictionary)fx["melee"], ek);
			if (current < lv)
				((Dictionary)fx["melee"])[(Variant)ek] = lv;
		}

		foreach (Variant ve in w.RangedEffects)
		{
			int ek = (int)ve;
			int lv = _GetFxLevel(w.RangedEffectLevels, ek);
			int current = _GetFxLevel((Dictionary)fx["ranged"], ek);
			if (current < lv)
				((Dictionary)fx["ranged"])[(Variant)ek] = lv;
		}

		foreach (Variant ve in w.MagicEffects)
		{
			int ek = (int)ve;
			int lv = _GetFxLevel(w.MagicEffectLevels, ek);
			int current = _GetFxLevel((Dictionary)fx["magic"], ek);
			if (current < lv)
				((Dictionary)fx["magic"])[(Variant)ek] = lv;
		}
	}

	private bool _IsValidUpgrade(WeaponCard w, HashSet<string> inventory, Dictionary playerFx, int currentTier)
	{
		if (!_MeetsPrerequisite(w, inventory))
			return false;
		if (w.Tier > currentTier)
			return true;

		// Check whether the card introduces a higher-level effect than the player currently has.
		string fxKey;
		Godot.Collections.Array fxList;

		switch (w.WeaponType)
		{
			case WeaponType.Melee:
				fxKey  = "melee";
				fxList = w.MeleeEffects;
				break;
			case WeaponType.Ranged:
				fxKey  = "ranged";
				fxList = w.RangedEffects;
				break;
			default:
				fxKey  = "magic";
				fxList = w.MagicEffects;
				break;
		}

		var fxLevels = w.WeaponType switch
		{
			WeaponType.Melee   => w.MeleeEffectLevels,
			WeaponType.Ranged  => w.RangedEffectLevels,
			_                             => w.MagicEffectLevels
		};

		foreach (Variant ve in fxList)
		{
			int ek = (int)ve;
			int newLv  = _GetFxLevel(fxLevels, ek);
			int playerLv = _GetFxLevel((Dictionary)playerFx[fxKey], ek);
			if (newLv > playerLv)
				return true;
		}

		return false;
	}

	private bool _MeetsPrerequisite(WeaponCard card, HashSet<string> inventory)
	{
		if (_prerequisites.ContainsKey((string)card.CardName))
			return inventory.Contains((string)_prerequisites[card.CardName]);

		if (card.Tags.Count == 0)
			return true;

		foreach (var tag in card.Tags)
		{
			if (inventory.Contains((string)tag))
				return true;
		}

		return false;
	}

	/// <summary>Attempt to fuse two weapon cards. Returns the fused result or null.</summary>
	public WeaponCard? TryFuse(WeaponCard w1, WeaponCard w2)
	{
		if (!_evolution.CanFuse(w1, w2)) return null;
		return _evolution.Fuse(w1, w2);
	}

	/// <summary>Check if two weapons have a valid fusion recipe.</summary>
	public bool CanFuse(WeaponCard w1, WeaponCard w2) => _evolution.CanFuse(w1, w2);

	/// <summary>Evolve a weapon if it has reached the ascension threshold.</summary>
	public WeaponCard? TryEvolve(WeaponCard w) => _evolution.Evolve(w);

	/// <summary>Transform a weapon to its ultimate form if threshold is met.</summary>
	public WeaponCard? TryTransformUltimate(WeaponCard w) => _evolution.TransformUltimate(w);

	/// <summary>Get all known fusion recipe descriptions for the UI.</summary>
	public string[] GetAllFusionRecipes() => _evolution.GetAllRecipes();

	/// <summary>Get the fusion result name for two weapons, or null if no recipe.</summary>
	public string? GetFusionResultName(WeaponCard w1, WeaponCard w2) => _evolution.GetFusionResultName(w1, w2);
}
