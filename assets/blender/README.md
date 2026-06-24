# Blender 烘焙管线 —— 项目级 2.5D 资产生成

本目录存放把程序化 3D 模型烘焙为 2D sprite PNG 或 3D `.glb` 模型的所有工具与配置。
最终被 Godot 用作 2D 场景中的视觉资源（Sprite2D / MeshInstance2D）。

## 文件清单

| 文件 | 作用 |
|---|---|
| `render_sprites.py` | 主烘焙脚本（Blender Python）。读取 manifest，用 bpy 程序化建模，统一灯光/相机/材质。支持 3 种输出格式：`png`（透明 PNG sprite）、`gltf`（3D 模型供 Godot MeshInstance2D 渲染）、`ground`（大尺寸 1920×1080 地面贴图）。 |
| `sprite_manifest.json` | sprite 清单（每个 item 描述模型函数 + 输出路径 + format + canvas 尺寸）。 |
| `README.md` | 本文档。 |

## 三种输出格式

| format | 用途 | 输出位置 | canvas_size | C# 端加载方式 |
|---|---|---|---|---|
| `png` | 2D sprite 透明 PNG（备选方案） | `assets/sprites/{props,resources}/` | 96 | `GD.Load<Texture2D>` + `Sprite2D` |
| `gltf` | 3D 模型（用于 2.5D 折中方案：MeshInstance2D 渲染） | `assets/models/{props,resources,obstacles}/` | n/a | `ModelLoader.LoadMesh()` 提取 Mesh + `MeshInstance2D` |
| `ground` | 大尺寸地面贴图（1920×1080 PNG） | `assets/sprites/ground/` | 1920 | `ProceduralGround.GetTexture(seed)` |

## 用法

```bash
# 从项目根目录
cd /Users/liyou/work/CNM

# 烘焙（Blender headless）
/Applications/Blender.app/Contents/MacOS/Blender -b -P assets/blender/render_sprites.py -- \
    assets/blender/sprite_manifest.json assets/sprites

# 烘焙到 assets/models/（gltf 格式）
/Applications/Blender.app/Contents/MacOS/Blender -b -P assets/blender/render_sprites.py -- \
    assets/blender/sprite_manifest.json assets/models
```

## Godot 资源导入

```bash
# 烘焙出新文件后，必须触发 Godot import 生成 .import 元数据
/Applications/Godot_mono.app/Contents/MacOS/Godot --headless --import
```

## 新增 sprite 流程

### 1. 道具/资源/障碍（gltf 格式）

1. 在 `render_sprites.py` 加 `make_xxx()` 函数，返回 bpy.types.Object
   - 用 `add_box` / `add_cylinder` / `add_sphere` / `add_cone` / `add_torus` / `add_ico` 组合
   - 用 `PALETTE` 字典里的统一调色板（保证风格一致）
   - 物体在 X-Y 平面居中，Z 范围建议 -0.5 到 +1.0
2. 在 `sprite_manifest.json` 加 item
   ```json
   {
     "key": "chest_0",
     "model": "chest",
     "args": { "lid_tilt": 0 },
     "format": "gltf",
     "output": "props/chest_0.glb"
   }
   ```
3. 重跑烘焙 + Godot import
4. C# 端用 `ModelLoader.LoadMesh("res://path.glb")` 加载，添加到 `MeshInstance2D`

### 2. 地面（ground 格式）

1. 在 `sprite_manifest.json` 加 item，指定 `seed` + `tint`
   ```json
   {
     "key": "ground_graveyard",
     "model": "ground",
     "format": "ground",
     "seed": 11111,
     "tint": [0.88, 0.90, 0.92, 1.0],
     "canvas_size": 1920,
     "output": "ground/ground_graveyard.png"
   }
   ```
2. 重跑烘焙 + Godot import
3. C# 端用 `ProceduralGround.GetTexture(seed)` 加载（自动按 seed 映射文件名）

## 风格统一策略

- **调色板统一**：`PALETTE` 字典定义 13 种主色（wood / stone / metal / leaves / berry / gold / crystal 等），所有材质从此取
- **PBR 材质**：`make_material()` 用 Principled BSDF，无 bump 节点（Blender 5.1 Eevee 兼容性问题）
- **灯光统一**：Sun（暖色 45°斜射，energy=4.5）+ Area（冷蓝顶光，energy=200）
- **相机统一**：
  - 单体资产（gltf）：90° 正俯视，ortho_scale=1.6
  - 地面：90° 正俯视，ortho_scale=1.0
- **尺寸规范**：
  - 单体资产：1.0-1.4 单位宽（适配 ortho_scale=1.6）
  - 地面：1.0×1.0 单位，渲染到 1920×1080 PNG

## 关键 API 注意

- Blender 5.1：渲染器名是 `BLENDER_EEVEE`（不是 `BLENDER_EEVEE_NEXT`）
- Blender 5.1：`scene.eevee.use_shadow` 已删除（shadows 默认开启）
- Blender 5.1：`bpy.ops.export_scene.gltf` 移除了 `export_textures` 参数
- Godot 4：`.glb` 默认导入为 PackedScene（Node3D 根 + MeshInstance3D 子节点），需 `ModelLoader.LoadMesh()` 提取 Mesh 资源

## 调试

```bash
# 单独烘焙一项：修改 manifest 只留 1 个 item 再跑
# 快速检查：mcp__chrome-devtools 不能直接看 Blender 输出，但可以用 Read 看 PNG
```

## C# 端集成点

| 资源类 | 加载方式 | 位置 |
|---|---|---|
| `Chest` | 4 帧 `chest_{0..3}.glb` 切换 Mesh | `src/world/Chest.cs` |
| `Altar` | `altar.glb` + Modulate 调色（5 boon） | `src/world/Altar.cs` |
| `Workbench` / `ForgeStation` | `workbench.glb` / `forge_station.glb` | `src/world/Workbench.cs` / `ForgeStation.cs` |
| `ResourceNode` | 4 variant → `tree/stone/bush/berry_bush.glb` 字典 | `src/world/ResourceNode.cs` |
| `SolidObstacle` | 10 variant 字典（0-9 → stone_block/tombstone/.../tower） | `src/world/Obstacles.cs` |
| `DestructibleObstacle` | variant 2/3 → wood_fence，其他 → stone_rubble | `src/world/Obstacles.cs` |
| `ExplosiveBarrel` | `explosive_barrel.glb` | `src/world/Obstacles.cs` |
| `LowObstacle` | variant 1 → water_puddle，其他 → grass | `src/world/Obstacles.cs` |
| `HazardZone` | variant 1/3 → toxic，2/4 → fire | `src/world/Obstacles.cs` |
| 地面 | `ProceduralGround.GetTexture(seed)` → 按 seed 加载 PNG | `src/world/ProceduralGround.cs` |
