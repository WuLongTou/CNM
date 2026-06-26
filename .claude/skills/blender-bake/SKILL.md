# Blender 烘焙管线

所有地图场景的视觉资源（障碍、探索道具、资源节点、地面）由 Blender 烘焙生成，Godot 2.5D 折中加载。

## 3 种输出格式

在 `assets/blender/sprite_manifest.json` 的 `format` 字段指定：

| format | 用途 | 输出位置 | C# 加载 |
|--------|------|----------|---------|
| `gltf` | 3D 模型（MeshInstance2D 渲染） | `assets/models/{props,resources,obstacles}/` | `ModelLoader.LoadMesh()` |
| `ground` | 大尺寸地面贴图（1920×1080 PNG） | `assets/sprites/ground/` | `ProceduralGround.GetTexture(seed)` |
| `png` | 2D sprite 透明 PNG（备选） | `assets/sprites/{props,resources}/` | `GD.Load<Texture2D>` + `Sprite2D` |

## 完整工作流

1. 修改 `assets/blender/sprite_manifest.json`（加/改 item）
2. 跑 Blender 烘焙：`/Applications/Blender.app/Contents/MacOS/Blender -b -P assets/blender/render_sprites.py -- assets/blender/sprite_manifest.json <out_dir>/`
3. 跑 Godot import 生成 `.import`：`/Applications/Godot_mono.app/Contents/MacOS/Godot --headless --import`
4. 修改 C# 集成代码（`src/world/*.cs`）
5. 编译 + 冒烟测试

详细文档见 `assets/blender/README.md`。新增模型需在 `render_sprites.py` 加 `make_xxx()` 函数，使用 `PALETTE` 调色板和 `add_*` 工具函数保证风格统一。

## 关键约定

- 风格不漂移：所有材质用 `PALETTE` 字典调色，禁止硬编码 RGB
- mesh 缺失时 fallback：每个改造类保留 `_Draw` 只画阴影，提示位置
- 动画优先用 `Modulate` 调色 / 切换 Mesh，少用 sprite sheet
