# Blender 烘焙 2D Sprite 管线

本目录存放把程序化 3D 模型烘焙为 2D sprite PNG 的所有工具与配置。

## 文件清单

| 文件 | 作用 |
|---|---|
| `render_sprites.py` | 主烘焙脚本（Blender Python）。读取 manifest，用 bpy 程序化建模，统一灯光/相机/材质，渲染为透明 PNG。 |
| `sprite_manifest.json` | sprite 清单（每个 item 描述模型函数 + 输出路径 + canvas 尺寸）。 |
| `sprite_template.blend` | 模板 `.blend` 文件（备用：保存预设置的相机/灯光/材质场景）。当前 `render_sprites.py` 用代码方式 setup，不依赖此文件。 |
| `README.md` | 本文档。 |

## 用法

```bash
# 从项目根目录
cd /Users/liyou/work/CNM
/Applications/Blender.app/Contents/MacOS/Blender -b -P assets/blender/render_sprites.py -- \
    assets/blender/sprite_manifest.json assets/sprites
```

输出 PNG 直接落到 `assets/sprites/{props,resources}/...`，Godot 编辑器下次启动会自动生成 `.import`。

## 新增 sprite 流程

1. 在 `render_sprites.py` 加 `make_xxx()` 函数，返回 bpy.types.Object
   - 用 `add_box` / `add_cylinder` / `add_sphere` / `add_cone` / `add_torus` / `add_ico` 组合
   - 用 `PALETTE` 字典里的统一调色板（保证风格一致）
   - 物体在 X-Y 平面居中，Z 范围建议 -0.5 到 +0.5（适配 ortho_scale=2.4）
2. 在 `sprite_manifest.json` 加 item
   ```json
   { "key": "...", "model": "xxx", "output": "props/xxx.png", "canvas_size": 96 }
   ```
3. 重跑烘焙命令

## 风格统一策略

- **调色板统一**：`PALETTE` 字典定义 13 种主色（wood / stone / metal / leaves / berry / gold / crystal 等），所有材质从此取
- **PBR 材质**：`make_material()` 用 Principled BSDF + Noise Texture（bump）制造表面细节
- **灯光统一**：`setup_template()` 设置 Sun（暖色 50°斜射）+ Area（冷蓝顶光）+ 弱背景
- **相机统一**：Orthographic 顶视，ortho_scale=2.4 适配 1.0 单位的物体
- **尺寸规范**：所有 sprite 默认 96×96，特殊可改 canvas_size

## 调试

```bash
# 单独烘焙一项（修改 manifest 只留 1 个 item）
# 然后用 macOS Finder 看效果
open assets/sprites/props/chest_0.png
```

若风格漂移，调整 `setup_template()` 中的灯光参数或 `make_material()` 的 roughness。
