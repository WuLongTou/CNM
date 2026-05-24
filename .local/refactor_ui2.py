"""Surgical line-level replacements in UIManager.cs."""
import sys

path = "/Users/liyou/Work/ZVS/src/ui/UIManager.cs"
with open(path, "r") as f:
    lines = f.readlines()

print(f"Total lines: {len(lines)}")

def find_method_end(lines, start_idx):
    """Find the closing brace of a method starting at start_idx (0-indexed)."""
    depth = 0
    for i in range(start_idx, len(lines)):
        for ch in lines[i]:
            if ch == '{':
                depth += 1
            elif ch == '}':
                depth -= 1
                if depth == 0:
                    return i
    return len(lines) - 1

# Method start lines (1-indexed) and their replacement text
method_map = {
    1870: """\tpublic void UpdateRoundDisplay(int roundNumber)
\t\t{
\t\t\t_hudManager?.UpdateRoundDisplay(roundNumber);
\t\t}
""",
    2104: """\tpublic void UpdateScoreDisplay(int newScore)
\t\t{
\t\t\t_hudManager?.UpdateScoreDisplay(newScore);
\t\t}
""",
    2110: """\tpublic void UpdateHealthDisplay(int currentHealth, int maxHealth)
\t\t{
\t\t\t_hudManager?.UpdateHealthDisplay(currentHealth, maxHealth);
\t\t}
""",
    2146: """\tpublic void UpdateZombiesDisplay(int zombiesRemaining, int totalZombies)
\t\t{
\t\t\t_hudManager?.UpdateZombiesDisplay(zombiesRemaining, totalZombies);
\t\t}
""",
    2152: """\tpublic void UpdatePlayerStatus(Player player)
\t\t{
\t\t\t_hudManager?.UpdatePlayerStatus(player);
\t\t}
""",
    2537: """\tpublic void UpdateWeatherIndicator(string weatherName, Color weatherColor)
\t\t{
\t\t\t_hudManager?.UpdateWeatherIndicator(weatherName);
\t\t}
""",
}

# Single-line replacements
single_line_replacements = {}
for i, line in enumerate(lines):
    if "GetHudPanel() => _hudPanel" in line:
        single_line_replacements[i] = "\tpublic Control GetHudPanel() => _hudManager?.GetHudPanel()!;\n"
        print(f"GetHudPanel at line {i+1}")
    if "_gameOverUi.ShowGameOver(_hudPanel)" in line:
        single_line_replacements[i] = line.replace("_hudPanel", "_hudManager?.GetHudPanel()")
        print(f"ShowGameOver fix at line {i+1}")

# Build replacement map: 0-indexed start -> (0-indexed end, replacement)
replace_map = {}

for start_line_1, repl_text in method_map.items():
    start_idx = start_line_1 - 1
    end_idx = find_method_end(lines, start_idx)
    replace_map[start_idx] = (end_idx, repl_text)
    print(f"Method at line {start_line_1}: ends at line {end_idx + 1}")

for idx, repl in single_line_replacements.items():
    if idx not in replace_map:
        replace_map[idx] = (idx, repl)

# Apply replacements
new_lines = []
i = 0
while i < len(lines):
    if i in replace_map:
        end_idx, repl_text = replace_map[i]
        new_lines.append(repl_text)
        i = end_idx + 1
    else:
        new_lines.append(lines[i])
        i += 1

with open(path, "w") as f:
    f.writelines(new_lines)

print(f"Done. Original: {len(lines)}, New: {len(new_lines)}")
