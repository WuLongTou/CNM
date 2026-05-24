"""Refactor UIManager.cs: delegate HUD to HudManager, remove old HUD code.

Uses exact line ranges from code analysis. Operates on 1-indexed lines.
"""
import sys

path = "/Users/liyou/Work/ZVS/src/ui/UIManager.cs"
with open(path, "r") as f:
    lines = f.readlines()

total = len(lines)
print(f"Original: {total} lines")

# All line numbers are 1-indexed
# Ranges to DELETE entirely (inclusive)
delete_ranges = [
    (47, 77),    # Old HUD fields (_hudPanel through _soundToggleButton)
    (110, 110),  # _lowHealthTween
    (115, 116),  # _effectsContainer, _effectIcons
    (175, 346),  # _CreateHudPanel method
    (348, 375),  # _CreateHudCard helper
    (377, 389),  # _MakeHudValueLabel helper
    (391, 402),  # _MakeHudMetaLabel helper
    (404, 412),  # _MakeStatLabel helper
]

# Build delete set (0-indexed)
delete_set = set()
for start, end in delete_ranges:
    for ln in range(start, end + 1):
        delete_set.add(ln - 1)

# Line replacements (1-indexed line -> new content)
replacements = {
    168: "\t\t_hudManager = new HudManager();\n\t\t_hudManager.GameManager = _gameManager;\n\t\tAddChild(_hudManager);\n",
    # Delegate public HUD methods
    2080: "\t\tpublic void UpdateRoundDisplay(int round) => _hudManager?.UpdateRoundDisplay(round);\n",
    2329: "\t\tpublic void UpdateScoreDisplay(int score) => _hudManager?.UpdateScoreDisplay(score);\n",
    2764: "\t\tpublic void UpdateWeatherIndicator(string text) => _hudManager?.UpdateWeatherIndicator(text);\n",
    2780: "\t\tpublic Panel GetHudPanel() => _hudManager?.GetHudPanel()!;\n",
}

# Find the method blocks to replace (they span multiple lines, so we delete + insert)
# We need exact ranges for these methods

# UpdateHealthDisplay (lines 2335-2368)
# UpdateZombiesDisplay (lines 2371-2375)
# UpdatePlayerStatus (lines 2377-2400)
# _UpdateStatusTooltips (lines 2402-2437)
# _UpdateEffectsDisplay (lines 2439-2609)
# _OnEffectIconExited (lines 2611-2614)

# Also need to remove HUD parts from _Process (lines 1078-1119)
# _UpdateDashDisplay (lines 1125-1148)
# _UpdateAttackIntervalDisplay (lines 1150-1163)
# _UpdateLiveCombatReadouts (lines 1165-1191)
# _UpdateMomentumDisplay (lines 1193-1211)
# _GetMomentumColor (lines 1213-1222)

# Add more delete ranges for methods to remove
more_deletes = [
    (2335, 2368),  # UpdateHealthDisplay
    (2371, 2375),  # UpdateZombiesDisplay
    (2377, 2400),  # UpdatePlayerStatus
    (2402, 2437),  # _UpdateStatusTooltips
    (2439, 2609),  # _UpdateEffectsDisplay
    (2611, 2614),  # _OnEffectIconExited
    (1125, 1148),  # _UpdateDashDisplay
    (1150, 1163),  # _UpdateAttackIntervalDisplay
    (1165, 1191),  # _UpdateLiveCombatReadouts
    (1193, 1211),  # _UpdateMomentumDisplay
    (1213, 1222),  # _GetMomentumColor
]

for start, end in more_deletes:
    for ln in range(start, end + 1):
        delete_set.add(ln - 1)

# For _Process: replace lines 1078-1119 with just the tooltip positioning + screen fx
# lines 1073-1077 are fine (tooltip), 1078-1119 are HUD+boss, 1121-1123 are screen fx
for ln in range(1078, 1120):
    delete_set.add(ln - 1)

# Remove replacements for lines that are in delete set
replacements = {k: v for k, v in replacements.items() if (k - 1) not in delete_set}

# Now insert replacement methods at the right positions
# We'll insert at the first deleted line of each method group

# Insert UpdateHealthDisplay delegation at line 2335
# Insert UpdateZombiesDisplay delegation at line 2371
# Insert UpdatePlayerStatus delegation at line 2377

# These are handled as insertions at specific positions
insert_before = {
    2335: "\t\tpublic void UpdateHealthDisplay(int currentHealth, int maxHealth) => _hudManager?.UpdateHealthDisplay(currentHealth, maxHealth);\n\n",
    2371: "\t\tpublic void UpdateZombiesDisplay(int remaining, int total) => _hudManager?.UpdateZombiesDisplay(remaining, total);\n\n",
    2377: "\t\tpublic void UpdatePlayerStatus(Player player) => _hudManager?.UpdatePlayerStatus(player);\n\n",
}

# Remove insert_before entries that conflict with delete_set
# Actually we want to insert BEFORE the first deleted line, then the deleted lines are removed
# So the insert happens, then the old code is skipped

new_lines = []
i = 0
while i < total:
    line_num = i + 1  # 1-indexed

    # Check insertions
    if line_num in insert_before and line_num in [r[0] for r in delete_ranges] + [r[0] for r in more_deletes]:
        new_lines.append(insert_before[line_num])

    # Check replacements
    if line_num in replacements:
        new_lines.append(replacements[line_num])
        i += 1
        continue

    # Check deletions
    if i in delete_set:
        i += 1
        continue

    new_lines.append(lines[i])
    i += 1

with open(path, "w") as f:
    f.writelines(new_lines)

print(f"New: {len(new_lines)} lines")
