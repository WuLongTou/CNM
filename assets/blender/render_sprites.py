"""Blender 烘焙脚本 —— 把程序化建模的 3D 资产烘焙为 2D sprite PNG。

用法:
  blender -b -P render_sprites.py -- manifest.json out_dir/

设计目标:
- 风格统一：所有 sprite 共享同一相机/灯光/材质设置（在 setup_template 中定义）
- 模型程序化：用 bpy.ops.mesh.primitive_* 组合，不依赖外部 .blend
- 输出透明 PNG：用于 Godot Sprite2D 叠加

模型函数（make_*）每个返回 bpy.types.Object（已应用材质、定位到原点）。
新增模型时按 pattern 加一个 make_xxx() 并在 MANIFEST 中引用。
"""
import bpy
import bmesh
import json
import math
import os
import sys
from pathlib import Path

# ─── 常量（统一调色板 + 渲染设置） ───────────────────────────
PALETTE = {
    "wood":      (0.36, 0.23, 0.13, 1.0),  # 木材（饥荒式暗棕）
    "wood_dark": (0.20, 0.12, 0.06, 1.0),
    "wood_lit":  (0.46, 0.31, 0.18, 1.0),
    "stone":     (0.46, 0.44, 0.40, 1.0),  # 灰岩
    "stone_dark":(0.30, 0.28, 0.26, 1.0),
    "stone_lit": (0.58, 0.56, 0.52, 1.0),
    "metal":     (0.42, 0.42, 0.45, 1.0),  # 铁/钢
    "metal_dark":(0.20, 0.20, 0.22, 1.0),
    "metal_lit": (0.62, 0.62, 0.65, 1.0),
    "leaves":    (0.24, 0.40, 0.18, 1.0),  # 绿叶
    "leaves_dk": (0.16, 0.30, 0.12, 1.0),
    "leaves_lt": (0.32, 0.52, 0.24, 1.0),
    "berry":     (0.80, 0.15, 0.20, 1.0),  # 浆果红
    "gold":      (0.82, 0.66, 0.25, 1.0),  # 金（宝箱锁/祭坛符文）
    "crystal":   (0.50, 0.70, 1.00, 1.0),  # 水晶蓝
    "rune":      (0.70, 0.30, 1.00, 1.0),  # 符文紫
    "fire":      (1.00, 0.50, 0.10, 1.0),  # 火焰橙
}

# 渲染设置
RENDER_ENGINE = "BLENDER_EEVEE"  # Eevee 快速
RENDER_SAMPLES = 64
FILM_TRANSPARENT = True  # 透明背景

# ─── 工具函数 ────────────────────────────────────────────────

def clear_scene():
    """清空场景中的所有 mesh/light/camera，保留 world 设置。"""
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    # 删残留 mesh data
    for block in bpy.data.meshes:
        bpy.data.meshes.remove(block)
    for block in bpy.data.materials:
        bpy.data.materials.remove(block)


def make_material(name, rgba, roughness=0.75, metallic=0.0):
    """创建统一 PBR 材质（Principled BSDF）。"""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = rgba
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return mat


def apply_mat(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)


def add_box(name, x, y, z, sx, sy, sz, mat):
    """创建立方体，返回 object。"""
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z))
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = (sx, sy, sz)
    apply_mat(obj, mat)
    return obj


def add_cylinder(name, x, y, z, radius, height, mat, segments=24):
    bpy.ops.mesh.primitive_cylinder_add(
        radius=radius, depth=height, vertices=segments,
        location=(x, y, z))
    obj = bpy.context.active_object
    obj.name = name
    apply_mat(obj, mat)
    return obj


def add_sphere(name, x, y, z, radius, mat, segments=16, rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(
        radius=radius, segments=segments, ring_count=rings,
        location=(x, y, z))
    obj = bpy.context.active_object
    obj.name = name
    apply_mat(obj, mat)
    return obj


def add_cone(name, x, y, z, radius1, radius2, height, mat, segments=16):
    bpy.ops.mesh.primitive_cone_add(
        radius1=radius1, radius2=radius2, depth=height,
        vertices=segments, location=(x, y, z))
    obj = bpy.context.active_object
    obj.name = name
    apply_mat(obj, mat)
    return obj


def add_torus(name, x, y, z, major_r, minor_r, mat):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major_r, minor_radius=minor_r,
        major_segments=24, minor_segments=12,
        location=(x, y, z))
    obj = bpy.context.active_object
    obj.name = name
    apply_mat(obj, mat)
    return obj


def join_objects(objs, name="joined"):
    """合并多个 object 为一个。"""
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    joined = bpy.context.active_object
    joined.name = name
    return joined


# ─── 模型定义（程序化） ────────────────────────────────────

def make_chest(lid_tilt=0):
    """宝箱：箱体 + 盖子（lid_tilt 控制盖子倾斜角度，0=闭合, 90=全开）。

    用于生成 4 帧开启动画：0, 30, 60, 90 度。
    """
    wood = make_material("ChestWood", PALETTE["wood"], roughness=0.85)
    wood_lit = make_material("ChestWoodLit", PALETTE["wood_lit"], roughness=0.8)
    wood_dk = make_material("ChestWoodDk", PALETTE["wood_dark"], roughness=0.85)
    metal = make_material("ChestMetal", PALETTE["metal"], roughness=0.4, metallic=0.6)
    metal_lit = make_material("ChestMetalLit", PALETTE["metal_lit"], roughness=0.35, metallic=0.6)
    gold = make_material("ChestGold", PALETTE["gold"], roughness=0.3, metallic=0.7)

    # 箱体（z 从 -0.4 到 0.0，z 高度 0.4 → 俯视能看到完整顶面）
    body = add_box("body", 0, 0, -0.20, 1.0, 0.7, 0.40, wood)
    # 箱体顶面亮边
    body_top = add_box("body_top", 0, 0, 0.005, 1.0, 0.7, 0.02, wood_lit)
    # 铁包边（中间一条横向）
    band1 = add_box("band1", 0, 0, -0.10, 1.02, 0.72, 0.04, metal)
    # 铁包边（底部）
    band2 = add_box("band2", 0, 0, -0.35, 1.02, 0.72, 0.05, metal_lit)
    # 锁（仅闭合时可见）
    if lid_tilt < 30:
        lock_base = add_box("lock", 0, 0.36, 0.05, 0.18, 0.04, 0.18, gold)
    else:
        # 锁在原位但用暗色（开启动画中）
        lock_base = add_box("lock", 0, 0.36, 0.05, 0.18, 0.04, 0.18, wood_dk)
    # 锁铰链
    lock_hinge = add_cylinder("hinge", 0, 0.36, 0.10, 0.04, 0.06, metal, segments=8)

    # 盖子（独立物体，z 0.05 到 0.15，z 高度 0.10）
    # 用 cursor 作 pivot：先把 cursor 移到后缘
    bpy.context.scene.cursor.location = (0, -0.36, 0.05)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0.10))
    lid = bpy.context.active_object
    lid.name = "lid"
    lid.scale = (1.0, 0.7, 0.10)
    # 把 lid 原点移到后缘下沿：(0, -0.36, 0.05)
    lid.location = (0, -0.36, 0.10)
    apply_mat(lid, wood_lit)
    # 用 cursor 作 pivot 旋转
    bpy.context.view_layer.objects.active = lid
    lid.select_set(True)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    # 重设 location（设了 origin 后位置会变）
    lid.location = (0, -0.36, 0.10)
    # 绕 X 轴旋转 lid_tilt
    bpy.ops.transform.rotate(value=math.radians(lid_tilt), orient_axis='X')

    # 盖子顶面亮边（同样旋转）
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, -0.36, 0.155))
    lid_top = bpy.context.active_object
    lid_top.name = "lid_top"
    lid_top.scale = (1.0, 0.7, 0.02)
    bpy.context.view_layer.objects.active = lid_top
    lid_top.select_set(True)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    lid_top.location = (0, -0.36, 0.155)
    bpy.ops.transform.rotate(value=math.radians(lid_tilt), orient_axis='X')
    apply_mat(lid_top, wood)

    # 盖子后缘铰链（不旋转）
    lid_hinge = add_cylinder("lid_hinge", 0, -0.36, 0.08, 0.04, 0.20, metal_lit, segments=8)

    # 重置 cursor 到原点
    bpy.context.scene.cursor.location = (0, 0, 0)

    return join_objects(
        [body, body_top, band1, band2, lock_base, lock_hinge, lid, lid_top, lid_hinge],
        f"chest_tilt{lid_tilt}")


def make_altar():
    """祭坛：石质底座 + 符文圆环 + 中心晶体。"""
    stone = make_material("AltarStone", PALETTE["stone"], roughness=0.9)
    stone_dk = make_material("AltarStoneDk", PALETTE["stone_dark"], roughness=0.9)
    crystal = make_material("AltarCrystal", PALETTE["crystal"], roughness=0.1, metallic=0.0)
    # crystal 加发光
    crystal.node_tree.nodes["Principled BSDF"].inputs["Emission Color"].default_value = (0.4, 0.7, 1.0, 1.0)
    crystal.node_tree.nodes["Principled BSDF"].inputs["Emission Strength"].default_value = 0.6

    # 底座（双层圆台）
    base1 = add_cylinder("base1", 0, 0, -0.3, 0.55, 0.18, stone)
    base2 = add_cylinder("base2", 0, 0, -0.1, 0.45, 0.12, stone_dk)
    # 顶面（中心圆盘）
    top = add_cylinder("top", 0, 0, 0.05, 0.38, 0.04, stone)
    # 符文圆环
    ring = add_torus("ring", 0, 0, 0.08, 0.30, 0.025, crystal)
    # 中心晶体（八面体）
    bpy.ops.mesh.primitive_ico_sphere_add(radius=0.10, subdivisions=1, location=(0, 0, 0.20))
    center = bpy.context.active_object
    center.name = "crystal"
    apply_mat(center, crystal)

    return join_objects([base1, base2, top, ring, center], "altar")


def make_workbench():
    """工作台：木桌 + 桌腿 + 铁砧 + 锤子。"""
    wood = make_material("BenchWood", PALETTE["wood"], roughness=0.8)
    wood_dk = make_material("BenchWoodDk", PALETTE["wood_dark"], roughness=0.85)
    metal = make_material("BenchMetal", PALETTE["metal"], roughness=0.4, metallic=0.6)
    metal_dk = make_material("BenchMetalDk", PALETTE["metal_dark"], roughness=0.45, metallic=0.5)

    # 桌面
    top = add_box("top", 0, 0, 0.0, 1.2, 0.6, 0.08, wood)
    top_edge = add_box("top_edge", 0, 0, 0.04, 1.22, 0.62, 0.02, wood_dk)
    # 桌腿（4 根）
    leg1 = add_box("leg1", -0.5, 0.25, -0.25, 0.08, 0.08, 0.5, wood_dk)
    leg2 = add_box("leg2",  0.5, 0.25, -0.25, 0.08, 0.08, 0.5, wood_dk)
    leg3 = add_box("leg3", -0.5, -0.25, -0.25, 0.08, 0.08, 0.5, wood_dk)
    leg4 = add_box("leg4",  0.5, -0.25, -0.25, 0.08, 0.08, 0.5, wood_dk)
    # 铁砧（放在桌面中央）
    anvil_base = add_box("anvil_base", 0, 0, 0.10, 0.4, 0.18, 0.08, metal)
    anvil_body = add_box("anvil_body", 0, 0, 0.18, 0.30, 0.14, 0.10, metal_dk)
    anvil_top = add_box("anvil_top", 0, 0, 0.26, 0.36, 0.10, 0.05, metal)
    # 锤子（斜放在右侧）
    handle = add_box("handle", 0.40, 0.15, 0.18, 0.04, 0.04, 0.30, wood_dk)
    head = add_box("head", 0.40, 0.15, 0.34, 0.16, 0.10, 0.10, metal)

    return join_objects(
        [top, top_edge, leg1, leg2, leg3, leg4, anvil_base, anvil_body, anvil_top, handle, head],
        "workbench")


def make_forge():
    """锻造台：铁砧（更大）+ 锤 + 砧座。"""
    metal = make_material("ForgeMetal", PALETTE["metal"], roughness=0.4, metallic=0.6)
    metal_dk = make_material("ForgeMetalDk", PALETTE["metal_dark"], roughness=0.45, metallic=0.5)
    metal_lit = make_material("ForgeMetalLit", PALETTE["metal_lit"], roughness=0.35, metallic=0.6)
    wood = make_material("ForgeWood", PALETTE["wood_dark"], roughness=0.85)

    # 砧座（木墩）
    stump = add_cylinder("stump", 0, 0, -0.20, 0.35, 0.30, wood)
    # 铁砧底座
    base = add_box("base", 0, 0, 0.0, 0.7, 0.4, 0.10, metal_dk)
    # 铁砧主体
    body = add_box("body", 0, 0, 0.10, 0.50, 0.28, 0.16, metal)
    # 砧面（凸出长条）
    top = add_box("top", 0, 0, 0.20, 0.62, 0.16, 0.05, metal_lit)
    # 锤（立在右侧地面）
    handle = add_cylinder("handle", 0.45, 0.20, -0.05, 0.025, 0.55, wood)
    head = add_box("head", 0.45, 0.20, 0.26, 0.18, 0.10, 0.10, metal)

    return join_objects([stump, base, body, top, handle, head], "forge")


def make_tree():
    """树：锥形树干（z 高 0.6）+ 多球冠。"""
    wood = make_material("TreeWood", PALETTE["wood_dark"], roughness=0.9)
    wood_lit = make_material("TreeWoodLit", PALETTE["wood"], roughness=0.85)
    leaves = make_material("TreeLeaves", PALETTE["leaves"], roughness=0.85)
    leaves_dk = make_material("TreeLeavesDk", PALETTE["leaves_dk"], roughness=0.9)
    leaves_lt = make_material("TreeLeavesLt", PALETTE["leaves_lt"], roughness=0.85)

    # 树干（向上渐细的锥，z 从 0 到 0.6）
    trunk = add_cone("trunk", 0, 0, 0.30, 0.10, 0.18, 0.60, wood, segments=12)
    # 树冠（z 0.55 到 0.95，多球团）
    crown_positions = [
        (0, 0, 0.85, 0.28),       # 顶部主球
        (0.18, 0, 0.70, 0.22),    # 右上
        (-0.18, 0, 0.70, 0.22),   # 左上
        (0, 0.18, 0.75, 0.22),    # 前上
        (0, -0.18, 0.75, 0.22),   # 后上
    ]
    crown_mats = [leaves_lt, leaves, leaves_dk, leaves, leaves_dk]
    crown_objs = []
    for i, (x, y, z, r) in enumerate(crown_positions):
        c = add_sphere(f"crown{i}", x, y, z, r, crown_mats[i % 3], segments=12, rings=8)
        crown_objs.append(c)

    return join_objects([trunk] + crown_objs, "tree")


def make_stone():
    """石头：不规则多面体（细分 + 噪声置换）。"""
    stone = make_material("RockStone", PALETTE["stone"], roughness=0.9)
    stone_dk = make_material("RockStoneDk", PALETTE["stone_dark"], roughness=0.9)
    stone_lit = make_material("RockStoneLit", PALETTE["stone_lit"], roughness=0.85)

    # 主体（一大两小多面石）
    bpy.ops.mesh.primitive_ico_sphere_add(radius=0.5, subdivisions=3, location=(0, 0, 0))
    main = bpy.context.active_object
    main.name = "rock_main"
    # 噪声置换
    bpy.ops.object.modifier_add(type='DISPLACE')
    main.modifiers["Displace"].strength = 0.18
    main.modifiers["Displace"].mid_level = 0.3
    apply_mat(main, stone)

    # 顶部高光石（一小块 lighter）
    top = add_ico("rock_top", 0.05, -0.10, 0.18, 0.22, 2, stone_lit)
    # 暗部小石
    side = add_ico("rock_side", -0.25, 0.12, -0.05, 0.18, 2, stone_dk)

    return join_objects([main, top, side], "stone")


def add_ico(name, x, y, z, radius, subdivisions, mat):
    bpy.ops.mesh.primitive_ico_sphere_add(
        radius=radius, subdivisions=subdivisions, location=(x, y, z))
    obj = bpy.context.active_object
    obj.name = name
    apply_mat(obj, mat)
    return obj


def make_bush(berry=False):
    """灌木：多球丛；berry=True 时加红色浆果球。"""
    leaves = make_material("BushLeaves", PALETTE["leaves"], roughness=0.85)
    leaves_dk = make_material("BushLeavesDk", PALETTE["leaves_dk"], roughness=0.9)
    leaves_lt = make_material("BushLeavesLt", PALETTE["leaves_lt"], roughness=0.85)
    berry_mat = make_material("Berry", PALETTE["berry"], roughness=0.4)

    objs = []
    positions = [
        (0, 0, 0.0, 0.30, leaves),
        (0.18, 0.05, 0.05, 0.22, leaves_dk),
        (-0.15, -0.08, 0.05, 0.22, leaves_lt),
        (0.05, 0.18, 0.03, 0.20, leaves),
        (-0.10, 0.10, 0.10, 0.18, leaves_dk),
    ]
    for x, y, z, r, m in positions:
        objs.append(add_sphere(f"leaf_{x}_{y}", x, y, z, r, m, segments=10, rings=6))

    if berry:
        # 4 颗红浆果，散布在冠层表面
        berry_positions = [
            (0.15, 0.10, 0.20), (-0.12, -0.10, 0.18),
            (0.05, 0.15, 0.22), (-0.05, -0.15, 0.20),
        ]
        for x, y, z in berry_positions:
            objs.append(add_sphere(f"berry_{x}_{y}", x, y, z, 0.05, berry_mat, segments=8, rings=6))

    return join_objects(objs, "bush_with_berry" if berry else "bush")


# ─── 场景模板（统一相机/灯光/渲染） ───────────────────────

def setup_template(canvas_size, output_path):
    """为单张 sprite 设置相机/灯光/输出。

    相机策略：90° 正俯视 ortho（最稳定的 2D 顶视角方案）
    - 相机在 (0, 0, h)，朝 -Z 看（默认）
    - ortho_scale=1.6 视野宽度 1.6 单位，物体（0.5-1.4 单位）占 60-90%
    - 立体感靠 Sun 斜射（60°）+ Area 顶光共同制造阴影
    """
    # 相机（90° 正俯视，从 +Z 看下来）
    bpy.ops.object.camera_add(location=(0, 0, 5.0))
    cam = bpy.context.active_object
    cam.name = "SpriteCam"
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = 1.6
    cam.rotation_euler = (0, 0, 0)  # 朝 -Z
    bpy.context.scene.camera = cam

    # 主光：Sun（45° 斜射，从 +X+Y 方向打来，制造长投影）
    bpy.ops.object.light_add(type='SUN', location=(4, 4, 5))
    sun = bpy.context.active_object
    sun.name = "KeyLight"
    sun.data.energy = 4.5
    sun.data.color = (1.0, 0.93, 0.78)  # 暖色（饥荒式夕阳）
    sun.rotation_euler = (math.radians(45), math.radians(35), 0)
    sun.data.shadow_soft_size = 3.0

    # 补光：Area 顶光（冷蓝，对抗主光的暗面）
    bpy.ops.object.light_add(type='AREA', location=(0, 0, 5))
    area = bpy.context.active_object
    area.name = "FillLight"
    area.data.energy = 200
    area.data.size = 4.0
    area.data.color = (0.60, 0.70, 0.90)  # 冷蓝
    area.rotation_euler = (0, 0, 0)

    # 世界环境
    world = bpy.context.scene.world
    if not world:
        world = bpy.data.worlds.new("World")
        bpy.context.scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if not bg:
        bg = world.node_tree.nodes.new("ShaderNodeBackground")
        world.node_tree.links.new(bg.outputs["Background"], world.node_tree.nodes["World Output"].inputs["Surface"])
    bg.inputs["Color"].default_value = (0.0, 0.0, 0.0, 1.0)
    bg.inputs["Strength"].default_value = 0.5

    # 渲染设置
    scene = bpy.context.scene
    scene.render.engine = RENDER_ENGINE
    scene.render.resolution_x = canvas_size
    scene.render.resolution_y = canvas_size
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = FILM_TRANSPARENT
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.render.image_settings.compression = 15
    scene.render.filepath = str(output_path)
    # Eevee 设置（Blender 5.x：use_shadow 已移除，shadows 默认开启）
    if RENDER_ENGINE == "BLENDER_EEVEE":
        try:
            scene.eevee.taa_samples = RENDER_SAMPLES
        except AttributeError:
            pass  # 5.1+ 可能用其他属性名


def render_item(item, out_dir):
    """烘焙单条 manifest 项。

    format:
      - "png"  (默认) → 渲染 2D 透明 PNG
      - "gltf"        → 导出 .glb 3D 模型（Godot MeshInstance2D 用）
    """
    canvas_size = item.get("canvas_size", 96)
    fmt = item.get("format", "png")
    rel_path = item["output"]
    output_path = Path(out_dir) / rel_path
    output_path.parent.mkdir(parents=True, exist_ok=True)

    # 场景重置
    clear_scene()

    # 调模型
    fn_name = f"make_{item['model']}"
    fn = globals().get(fn_name)
    if fn is None:
        raise ValueError(f"Unknown model: {item['model']} (no {fn_name})")
    obj = fn(**item.get("args", {}))
    obj.location = (0, 0, 0)  # 居中
    bpy.context.view_layer.objects.active = obj

    # 应用 scale
    if "scale" in item:
        obj.scale = (item["scale"],) * 3

    if fmt == "gltf":
        _export_gltf(output_path, item)
    else:
        # PNG 模式：需要灯光/相机
        setup_template(canvas_size, output_path)
        bpy.ops.render.render(write_still=True)

    print(f"  ✓ {rel_path}  ({fmt})")
    return output_path


def _export_gltf(output_path, item):
    """导出 .glb：单 mesh + PBR 材质，Godot 自动导入为 Mesh 资源。"""
    bpy.ops.export_scene.gltf(
        filepath=str(output_path),
        export_format='GLB',
        export_apply=True,           # 应用 modifier（displace 等）
        export_materials='EXPORT',   # 导出材质
        export_normals=True,
        export_tangents=False,
        export_morph=False,
        export_skins=False,
        export_animations=False,
        export_yup=True,             # Y-up（Godot 期望）
        export_cameras=False,
        export_lights=False,
        use_selection=True,           # 只导出当前选中的 mesh
    )
    # 取消所有选中（清场前）
    bpy.ops.object.select_all(action='DESELECT')


# ─── 入口 ───────────────────────────────────────────────────

def main():
    if len(sys.argv) < 4:
        print("Usage: blender -b -P render_sprites.py -- manifest.json out_dir/")
        sys.exit(1)
    manifest_path = Path(sys.argv[-2])
    out_dir = Path(sys.argv[-1])

    if not manifest_path.exists():
        print(f"Manifest not found: {manifest_path}")
        sys.exit(1)
    out_dir.mkdir(parents=True, exist_ok=True)

    with open(manifest_path) as f:
        manifest = json.load(f)

    items = manifest["items"]
    print(f"烘焙 {len(items)} 个 sprite → {out_dir}")
    ok, fail = 0, 0
    for item in items:
        try:
            render_item(item, out_dir)
            ok += 1
        except Exception as e:
            print(f"  ✗ {item.get('output', '?')}: {e}")
            fail += 1
    print(f"\n完成: {ok} 成功, {fail} 失败")
    sys.exit(0 if fail == 0 else 1)


if __name__ == "__main__":
    main()
