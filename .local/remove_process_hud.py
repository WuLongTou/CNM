"""Remove old HUD _Process code from UIManager (lines 1028-1067)."""
path = "/Users/liyou/Work/ZVS/src/ui/UIManager.cs"
with open(path, "r") as f:
    lines = f.readlines()

# Verify anchors
assert "if (_gameManager != null && _gameManager.PlayerRef != null)" in lines[1027], f"Line 1028 mismatch: {lines[1027].strip()}"
assert "if (_screenFx != null)" in lines[1068], f"Line 1069 mismatch: {lines[1068].strip()}"

# Remove lines 1028-1067 (0-indexed: 1027-1066)
new_lines = lines[:1027] + lines[1067:]

with open(path, "w") as f:
    f.writelines(new_lines)

print(f"Done. Original: {len(lines)}, New: {len(new_lines)}")
