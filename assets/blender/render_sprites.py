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
    """创建材质（Principled BSDF + Emission）。

    Godot 端 ModelLoader._SetUnshaded() 会强制材质为 Unshaded 模式，
    2D 场景中无 3D 光照环境下 Base Color + Emission 都直接可见。
    """
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = rgba
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Emission Color"].default_value = rgba
    bsdf.inputs["Emission Strength"].default_value = 0.5
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
    # crystal 加亮蓝辉光（Godot 端 Unshaded 后直接可见）
    bsdf = crystal.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Emission Color"].default_value = (0.4, 0.7, 1.0, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 1.5

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


# ─── 障碍物模型（程序化） ────────────────────────────────
# 命名规则：make_<kind>[_<variant>]，统一适配 ortho_scale=1.6 视野（约 1.0-1.4 单位宽）
# 所有函数返回 bpy.types.Object（joined 复合 mesh），居中在 X-Y 平面

def make_stone_block():
    """Solid variant 0/4：砖石墙段（横长方形，灰岩石砖错缝）"""
    stone = make_material("StoneBlock", PALETTE["stone"], roughness=0.9)
    stone_dk = make_material("StoneBlockDk", PALETTE["stone_dark"], roughness=0.95)
    stone_lit = make_material("StoneBlockLit", PALETTE["stone_lit"], roughness=0.85)
    # 主体墙段
    body = add_box("body", 0, 0, 0, 1.0, 0.4, 0.5, stone)
    # 顶面亮边
    top = add_box("top", 0, 0, 0.26, 1.0, 0.4, 0.04, stone_lit)
    # 暗底
    bot = add_box("bot", 0, 0, -0.26, 1.02, 0.42, 0.05, stone_dk)
    return join_objects([body, top, bot], "stone_block")


def make_tombstone_row():
    """Solid variant 1：墓碑排（并排 3 个圆顶墓碑 + 苔藓底座）"""
    stone = make_material("TombStone", PALETTE["stone"], roughness=0.9)
    stone_dk = make_material("TombStoneDk", PALETTE["stone_dark"], roughness=0.95)
    moss = make_material("TombMoss", (0.13, 0.19, 0.11, 1.0), roughness=0.95)
    objs = []
    # 苔藓底座
    objs.append(add_box("moss_base", 0, 0, -0.10, 1.0, 0.35, 0.05, moss))
    # 3 个墓碑
    for i, x in enumerate([-0.35, 0, 0.35]):
        objs.append(add_box(f"tomb_{i}", x, 0, 0.10, 0.18, 0.10, 0.35, stone))
        objs.append(add_cylinder(f"top_{i}", x, 0, 0.28, 0.09, 0.04, stone, segments=12))
    return join_objects(objs, "tombstone_row")


def make_pillar():
    """Solid variant 3：石柱（柱头 + 凹槽柱身 + 柱础）"""
    stone = make_material("PillarStone", PALETTE["stone"], roughness=0.85)
    stone_dk = make_material("PillarStoneDk", PALETTE["stone_dark"], roughness=0.9)
    stone_lit = make_material("PillarStoneLit", PALETTE["stone_lit"], roughness=0.85)
    objs = []
    # 柱础（方形底座）
    objs.append(add_box("base", 0, 0, -0.20, 0.45, 0.45, 0.10, stone_dk))
    # 柱身（凹槽圆柱）
    objs.append(add_cylinder("shaft", 0, 0, 0.0, 0.15, 0.6, stone, segments=16))
    # 柱头（方形顶）
    objs.append(add_box("cap", 0, 0, 0.35, 0.45, 0.45, 0.10, stone_lit))
    # 柱头小装饰
    objs.append(add_box("cap_top", 0, 0, 0.42, 0.5, 0.5, 0.04, stone))
    return join_objects(objs, "pillar")


def make_metal_bunker():
    """Solid variant 5：金属掩体（锈蚀钢板 + 警示条 + 铆钉）"""
    metal = make_material("BunkerMetal", PALETTE["metal"], roughness=0.5, metallic=0.5)
    metal_dk = make_material("BunkerMetalDk", PALETTE["metal_dark"], roughness=0.55, metallic=0.45)
    metal_lit = make_material("BunkerMetalLit", PALETTE["metal_lit"], roughness=0.45, metallic=0.55)
    warn = make_material("BunkerWarn", (0.95, 0.75, 0.10, 1.0), roughness=0.7)
    objs = []
    # 主体钢板
    objs.append(add_box("body", 0, 0, 0.0, 0.9, 0.5, 0.45, metal))
    # 顶面亮边
    objs.append(add_box("top", 0, 0, 0.24, 0.92, 0.52, 0.04, metal_lit))
    # 底面暗
    objs.append(add_box("bot", 0, 0, -0.24, 0.92, 0.52, 0.05, metal_dk))
    # 黄色警示条
    objs.append(add_box("warn", 0, 0, -0.05, 0.92, 0.52, 0.06, warn))
    return join_objects(objs, "metal_bunker")


def make_obelisk():
    """Solid variant 6：方尖碑（窄高梯形碑身 + 斜尖顶）"""
    stone = make_material("ObeliskStone", PALETTE["stone"], roughness=0.85)
    stone_dk = make_material("ObeliskStoneDk", PALETTE["stone_dark"], roughness=0.9)
    objs = []
    # 宽底座
    objs.append(add_box("base", 0, 0, -0.25, 0.35, 0.35, 0.10, stone_dk))
    # 主体（梯形：用两个 box 叠加）
    objs.append(add_box("body1", 0, 0, 0.0, 0.22, 0.22, 0.30, stone))
    objs.append(add_box("body2", 0, 0, 0.35, 0.16, 0.16, 0.30, stone))
    # 斜尖顶
    bpy.ops.mesh.primitive_cone_add(radius1=0.10, radius2=0, depth=0.20, vertices=4, location=(0, 0, 0.60))
    top = bpy.context.active_object
    top.name = "top"
    apply_mat(top, stone_dk)
    objs.append(top)
    return join_objects(objs, "obelisk")


def make_archway():
    """Solid variant 7：拱门（左右方柱 + 残缺横梁 + 地面碎石）"""
    stone = make_material("ArchStone", PALETTE["stone"], roughness=0.9)
    stone_dk = make_material("ArchStoneDk", PALETTE["stone_dark"], roughness=0.95)
    stone_lit = make_material("ArchStoneLit", PALETTE["stone_lit"], roughness=0.85)
    objs = []
    # 左柱
    objs.append(add_box("left", -0.30, 0, 0, 0.20, 0.20, 0.60, stone))
    # 右柱
    objs.append(add_box("right", 0.30, 0, 0, 0.20, 0.20, 0.60, stone))
    # 横梁（中间断开）
    objs.append(add_box("beam_l", -0.15, 0, 0.40, 0.30, 0.25, 0.20, stone_lit))
    objs.append(add_box("beam_r", 0.20, 0, 0.40, 0.20, 0.25, 0.20, stone_dk))
    # 拱顶（半圆柱顶）
    objs.append(add_cylinder("arch", 0, 0, 0.55, 0.30, 0.10, stone_lit, segments=16))
    return join_objects(objs, "archway")


def make_giant_tree():
    """Solid variant 8：枯巨树（粗树干 + 大团枯枝冠）"""
    wood = make_material("GiantWood", PALETTE["wood_dark"], roughness=0.9)
    wood_lit = make_material("GiantWoodLit", PALETTE["wood"], roughness=0.85)
    objs = []
    # 粗树干
    objs.append(add_cylinder("trunk", 0, 0, 0.10, 0.12, 0.60, wood, segments=12))
    # 树根加宽
    objs.append(add_cylinder("root", 0, 0, -0.20, 0.20, 0.10, wood, segments=8))
    # 枯枝（4 段，从树顶伸出）
    branch_pos = [
        (0.18, 0, 0.40, 0.30), (-0.18, 0, 0.40, 0.30),
        (0, 0.18, 0.35, 0.25), (0, -0.18, 0.35, 0.25),
    ]
    for i, (x, y, z, len) in enumerate(branch_pos):
        bpy.ops.mesh.primitive_cone_add(
            radius1=0.02, radius2=0.05, depth=len, vertices=6,
            location=(x * 0.5, y * 0.5, z + len * 0.3))
        branch = bpy.context.active_object
        branch.name = f"branch_{i}"
        # 旋转：向外倾斜
        branch.rotation_euler = (
            math.atan2(y * 0.5, len * 0.5) if y != 0 else 0,
            -math.atan2(x * 0.5, len * 0.5) if x != 0 else 0,
            0)
        apply_mat(branch, wood_lit)
        objs.append(branch)
    return join_objects(objs, "giant_tree")


def make_tower():
    """Solid variant 9：钟楼/塔楼（三层收分方塔 + 拱形钟窗 + 尖顶）"""
    stone = make_material("TowerStone", PALETTE["stone"], roughness=0.9)
    stone_dk = make_material("TowerStoneDk", PALETTE["stone_dark"], roughness=0.95)
    stone_lit = make_material("TowerStoneLit", PALETTE["stone_lit"], roughness=0.85)
    objs = []
    # 底层（最大）
    objs.append(add_box("base1", 0, 0, 0.0, 0.45, 0.45, 0.30, stone_dk))
    # 中层
    objs.append(add_box("base2", 0, 0, 0.35, 0.38, 0.38, 0.25, stone))
    # 顶层
    objs.append(add_box("base3", 0, 0, 0.55, 0.32, 0.32, 0.20, stone_lit))
    # 钟窗（拱形凹槽，用小 cylinder 模拟）
    objs.append(add_cylinder("bell", 0, 0.16, 0.50, 0.08, 0.04, stone_dk, segments=12))
    # 尖顶
    bpy.ops.mesh.primitive_cone_add(radius1=0.20, radius2=0, depth=0.25, vertices=4, location=(0, 0, 0.78))
    top = bpy.context.active_object
    top.name = "spire"
    apply_mat(top, stone_dk)
    objs.append(top)
    return join_objects(objs, "tower")


def make_wood_fence():
    """Destructible variant 1/2/3：木栅栏（横木 + 立柱）"""
    wood = make_material("FenceWood", PALETTE["wood"], roughness=0.85)
    wood_dk = make_material("FenceWoodDk", PALETTE["wood_dark"], roughness=0.9)
    objs = []
    # 立柱（3 根）
    for i, x in enumerate([-0.40, 0, 0.40]):
        objs.append(add_box(f"post_{i}", x, 0, 0, 0.05, 0.05, 0.30, wood_dk))
    # 横木（2 根）
    objs.append(add_box("rail1", 0, 0, 0.10, 0.90, 0.05, 0.05, wood))
    objs.append(add_box("rail2", 0, 0, -0.05, 0.90, 0.05, 0.05, wood))
    return join_objects(objs, "wood_fence")


def make_stone_rubble():
    """Destructible variant 4+：石堆（多块碎石叠放）"""
    stone = make_material("RubbleStone", PALETTE["stone"], roughness=0.95)
    stone_dk = make_material("RubbleStoneDk", PALETTE["stone_dark"], roughness=0.95)
    stone_lit = make_material("RubbleStoneLit", PALETTE["stone_lit"], roughness=0.9)
    objs = []
    # 主体大石
    objs.append(add_box("big1", -0.10, 0, -0.05, 0.30, 0.25, 0.20, stone))
    # 中石
    objs.append(add_box("mid", 0.15, 0.05, 0.0, 0.20, 0.20, 0.15, stone_lit))
    # 小石
    objs.append(add_box("small1", 0.0, -0.10, 0.05, 0.15, 0.12, 0.12, stone_dk))
    objs.append(add_box("small2", -0.20, 0.10, 0.0, 0.12, 0.10, 0.10, stone_dk))
    return join_objects(objs, "stone_rubble")


def make_explosive_barrel():
    """ExplosiveBarrel：红色桶身 + 黄黑警示条纹 + 顶盖"""
    barrel = make_material("BarrelRed", (0.70, 0.18, 0.12, 1.0), roughness=0.75)
    barrel_dk = make_material("BarrelRedDk", (0.42, 0.10, 0.06, 1.0), roughness=0.8)
    warn = make_material("BarrelWarn", (0.95, 0.75, 0.10, 1.0), roughness=0.7)
    metal = make_material("BarrelMetal", PALETTE["metal_dark"], roughness=0.4, metallic=0.6)
    objs = []
    # 桶身
    objs.append(add_cylinder("body", 0, 0, 0, 0.18, 0.45, barrel, segments=16))
    # 顶盖
    objs.append(add_cylinder("top", 0, 0, 0.24, 0.19, 0.03, barrel_dk, segments=16))
    # 底圈
    objs.append(add_cylinder("rim_bot", 0, 0, -0.20, 0.185, 0.04, metal, segments=16))
    objs.append(add_cylinder("rim_mid", 0, 0, 0.0, 0.185, 0.04, metal, segments=16))
    objs.append(add_cylinder("rim_top", 0, 0, 0.18, 0.185, 0.04, metal, segments=16))
    # 黄色警示带（中圈）
    objs.append(add_cylinder("warn", 0, 0, 0.06, 0.186, 0.04, warn, segments=16))
    return join_objects(objs, "explosive_barrel")


def make_water_puddle():
    """Low variant 1：水洼（蓝色半透明圆盘 + 高光）"""
    water = make_material("PuddleWater", (0.20, 0.30, 0.45, 0.85), roughness=0.2, metallic=0.0)
    objs = []
    # 主体圆盘（扁圆柱）
    objs.append(add_cylinder("puddle", 0, 0, -0.05, 0.40, 0.05, water, segments=20))
    # 边缘高光环
    objs.append(add_torus("highlight", 0, 0, -0.02, 0.35, 0.02, water))
    return join_objects(objs, "water_puddle")


def make_grass_low():
    """Low 其他：草丛（多束绿色尖草）"""
    grass = make_material("LowGrass", PALETTE["leaves"], roughness=0.85)
    grass_dk = make_material("LowGrassDk", PALETTE["leaves_dk"], roughness=0.9)
    objs = []
    # 5 束草（圆锥体）
    positions = [(0, 0), (0.20, 0.10), (-0.15, 0.05), (0.10, -0.15), (-0.10, -0.10)]
    mats = [grass, grass_dk, grass, grass_dk, grass]
    for i, ((x, y), m) in enumerate(zip(positions, mats)):
        bpy.ops.mesh.primitive_cone_add(
            radius1=0.03, radius2=0.01, depth=0.20, vertices=4,
            location=(x, y, 0.10))
        blade = bpy.context.active_object
        blade.name = f"blade_{i}"
        apply_mat(blade, m)
        objs.append(blade)
    return join_objects(objs, "grass_low")


def make_toxic_swamp():
    """Hazard variant 1/3：毒沼（绿色毒气 + 中心深潭）"""
    toxic = make_material("Toxic", (0.40, 0.65, 0.20, 0.75), roughness=0.5)
    toxic_dk = make_material("ToxicDk", (0.20, 0.40, 0.10, 0.85), roughness=0.5)
    bubble = make_material("Bubble", (0.70, 0.95, 0.40, 0.6), roughness=0.3)
    objs = []
    # 中心深潭
    objs.append(add_cylinder("pool", 0, 0, -0.10, 0.35, 0.08, toxic_dk, segments=20))
    # 毒气层（外圈）
    objs.append(add_cylinder("mist", 0, 0, 0.0, 0.45, 0.05, toxic, segments=20))
    # 气泡（3 颗）
    for i, (x, y) in enumerate([(0.15, 0.10), (-0.10, 0.15), (0, -0.18)]):
        objs.append(add_sphere(f"bubble_{i}", x, y, 0.04, 0.04, bubble, segments=10, rings=6))
    return join_objects(objs, "toxic_swamp")


def make_fire_ground():
    """Hazard variant 2/4：燃烧地带（火焰环 + 中心熔岩）"""
    fire = make_material("Fire", (1.0, 0.50, 0.10, 0.85), roughness=0.4)
    fire_dk = make_material("FireDk", (0.80, 0.20, 0.05, 0.9), roughness=0.4)
    lava = make_material("Lava", (0.95, 0.30, 0.05, 1.0), roughness=0.3)
    objs = []
    # 中心熔岩
    objs.append(add_cylinder("lava", 0, 0, -0.05, 0.25, 0.06, lava, segments=20))
    # 火焰锥（4 束绕中心）
    for i, a in enumerate([0, math.pi/2, math.pi, 3*math.pi/2]):
        x = math.cos(a) * 0.32
        y = math.sin(a) * 0.32
        bpy.ops.mesh.primitive_cone_add(
            radius1=0.08, radius2=0.02, depth=0.25, vertices=8,
            location=(x, y, 0.10))
        flame = bpy.context.active_object
        flame.name = f"flame_{i}"
        apply_mat(flame, fire)
        objs.append(flame)
    # 外圈暗火
    objs.append(add_torus("ring", 0, 0, 0.0, 0.40, 0.03, fire_dk))
    return join_objects(objs, "fire_ground")


# ─── 地面（程序化大纹理） ───────────────────────────────────
# 输出大 PNG（1920×1080），替换 ProceduralGround.cs 的程序化烘焙
# 参数：seed（噪声） + tint（基色）

def make_ground(seed=0, tint=(0.3, 0.3, 0.3, 1.0)):
    """地面 Plane：1.0×1.0 单位，高细分 + 噪声 displacement + tint 材质。
    适合 ortho_scale=1.0 + camera Z=0.5 渲染。
    """
    mat = make_material(f"Ground_{seed}", tint, roughness=0.95)
    # 高细分 Plane
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 0, 0))
    plane = bpy.context.active_object
    plane.name = "ground_plane"
    # Subdivision（提供顶点 displacement 用）
    bpy.ops.object.modifier_add(type='SUBSURF')
    plane.modifiers["Subdivision"].subdivision_type = 'CATMULL_CLARK'
    plane.modifiers["Subdivision"].levels = 6
    # 噪声 displacement
    bpy.ops.object.modifier_add(type='DISPLACE')
    plane.modifiers["Displace"].strength = 0.04
    plane.modifiers["Displace"].mid_level = 0.0
    # 复用 Blender 自带 stucci 纹理（不依赖外部）
    tex = bpy.data.textures.new(f"DisplaceTex_{seed}", type='STUCCI')
    tex.noise_scale = 0.3
    tex.noise_basis = 'BLENDER_ORIGINAL'
    plane.modifiers["Displace"].texture = tex
    apply_mat(plane, mat)
    return plane


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
    # ground 格式从 item 顶层取 seed/tint，其他用 args
    if fmt == "ground":
        obj = fn(seed=item.get("seed", 0), tint=tuple(item.get("tint", [0.3, 0.3, 0.3, 1.0])))
    else:
        obj = fn(**item.get("args", {}))
    obj.location = (0, 0, 0)  # 居中
    bpy.context.view_layer.objects.active = obj

    # 应用 scale
    if "scale" in item:
        obj.scale = (item["scale"],) * 3

    if fmt == "gltf":
        _export_gltf(output_path, item)
    elif fmt == "ground":
        # 已经用 fn(seed, tint) 创建好 obj，直接渲染
        _render_ground_setup(canvas_size, output_path)
        bpy.ops.render.render(write_still=True)
    else:
        # PNG 模式：需要灯光/相机
        setup_template(canvas_size, output_path)
        bpy.ops.render.render(write_still=True)

    print(f"  ✓ {rel_path}  ({fmt})")
    return output_path


def _render_ground_setup(canvas_size, output_path):
    """地面渲染设置：相机/灯光/输出（前提：场景中已有 ground mesh）。"""
    # 相机（正交 1.0 单位视野，Z=0.5 朝下）
    bpy.ops.object.camera_add(location=(0, 0, 0.5))
    cam = bpy.context.active_object
    cam.name = "GroundCam"
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = 1.0
    bpy.context.scene.camera = cam

    # 灯光：Sun 暖色斜射
    bpy.ops.object.light_add(type='SUN', location=(4, 4, 5))
    sun = bpy.context.active_object
    sun.name = "KeyLight"
    sun.data.energy = 4.5
    sun.data.color = (1.0, 0.93, 0.78)
    sun.rotation_euler = (math.radians(45), math.radians(35), 0)
    sun.data.shadow_soft_size = 3.0

    # 补光
    bpy.ops.object.light_add(type='AREA', location=(0, 0, 5))
    area = bpy.context.active_object
    area.name = "FillLight"
    area.data.energy = 200
    area.data.size = 4.0
    area.data.color = (0.60, 0.70, 0.90)
    area.rotation_euler = (0, 0, 0)

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
    if RENDER_ENGINE == "BLENDER_EEVEE":
        try:
            scene.eevee.taa_samples = RENDER_SAMPLES
        except AttributeError:
            pass


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
