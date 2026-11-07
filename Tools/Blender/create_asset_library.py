"""Create the editable starter library and export its Unity-facing models.

Run with Blender --background --python this_file.py. Never touches an open file.
"""
import bpy
import math
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ArtSource" / "Blender"
EXPORT = ROOT / "Assets" / "_Game" / "Resources" / "Models"
SOURCE.mkdir(parents=True, exist_ok=True)
EXPORT.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
for collection in list(bpy.data.collections):
    if collection.name != "Collection":
        bpy.data.collections.remove(collection)
default = bpy.data.collections.get("Collection")
default.name = "00_Studio"

def material(name, color, metal=0.0, roughness=0.65):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Metallic"].default_value = metal
    shader.inputs["Roughness"].default_value = roughness
    return mat

stone = material("Stone_DeepTeal", (0.12, 0.24, 0.27))
edge = material("Stone_Trim", (0.28, 0.43, 0.44))
gold = material("Gold", (0.9, 0.57, 0.15), 0.65, 0.3)
blue = material("Knight_Blue", (0.08, 0.27, 0.64), 0.3)
steel = material("Steel", (0.57, 0.72, 0.8), 0.75, 0.28)
dark = material("Charcoal", (0.025, 0.05, 0.07))
skin = material("Skin", (0.72, 0.44, 0.27))
green = material("Goblin_Jade", (0.21, 0.53, 0.3))
purple = material("Goblin_Plum", (0.31, 0.10, 0.25))
glow = material("Eye_Amber", (1, 0.68, 0.1))
fabric = material("Cape", (0.08, 0.13, 0.3))

def collection(name):
    col = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(col)
    return col

def finish(obj, name, mat, col, bevel=0):
    obj.name = name
    for current in list(obj.users_collection):
        current.objects.unlink(obj)
    col.objects.link(obj)
    obj.data.materials.append(mat)
    if bevel:
        modifier = obj.modifiers.new("Soft edges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        obj.modifiers.new("Weighted normals", "WEIGHTED_NORMAL")
    return obj

def box(name, location, scale, mat, col, bevel=0.04):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, mat, col, bevel)

def sphere(name, location, scale, mat, col):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=1, location=location)
    obj = bpy.context.object
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, mat, col)

def limb(name, a, b, radius, mat, col, radius2=None):
    start, end = Vector(a), Vector(b)
    bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=radius, radius2=radius if radius2 is None else radius2,
                                    depth=(end-start).length, location=(start+end)/2)
    obj = bpy.context.object
    obj.rotation_euler = (end-start).to_track_quat("Z", "Y").to_euler()
    return finish(obj, name, mat, col)

hero = collection("01_Characters_Knight")
for x in (-0.16, 0.16):
    box("Boot", (x,-0.07,0.12), (0.23,0.39,0.24), dark, hero)
    limb("Leg", (x,0,0.22), (x,0,0.73), 0.10, steel, hero)
box("Armored torso", (0,0,0.95), (0.55,0.34,0.53), blue, hero,0.08)
box("Gold belt", (0,-0.01,0.72), (0.57,0.37,0.07), gold, hero,0.01)
box("Breastplate crest", (0,-0.19,1.01), (0.12,0.05,0.3), gold, hero,0.015)
box("Cape", (0,0.23,0.85), (0.61,0.07,0.83), fabric, hero)
sphere("Face", (0,-0.04,1.43), (0.24,0.22,0.27), skin, hero)
sphere("Helmet", (0,0.035,1.49), (0.29,0.25,0.29), steel, hero)
box("Visor", (0,-0.222,1.45), (0.38,0.035,0.12), dark, hero,0.025)
box("Helmet crest", (0,0.01,1.78), (0.07,0.3,0.12), blue, hero)
for x in (-0.13,0.13):
    box("Eye", (x,-0.247,1.46), (0.06,0.015,0.035), glow, hero,0.004)
for side in (-1,1):
    sphere("Pauldron", (side*0.35,0,1.15), (0.19,0.23,0.18), gold, hero)
    limb("Arm", (side*0.34,0,1.12), (side*0.48,-0.10,0.78), 0.105, blue, hero)
sphere("Sword hand", (0.48,-0.1,0.77), (0.12,0.12,0.12), steel, hero)
box("Sword grip", (0.48,-0.1,0.91), (0.07,0.08,0.28), dark, hero,0.01)
box("Sword guard", (0.48,-0.1,1.04), (0.36,0.1,0.07), gold, hero)
limb("Blade", (0.48,-0.1,1.08), (0.48,-0.1,1.78), 0.09, steel, hero,0)
shield = sphere("Shield", (-0.49,-0.18,0.85), (0.26,0.08,0.35), blue, hero)
box("Shield emblem", (-0.49,-0.27,0.85), (0.055,0.025,0.39), gold, hero,0.01)

enemy = collection("02_Characters_Goblin")
for x in (-0.15,0.15):
    box("Goblin boot", (x,-0.06,0.1), (0.23,0.32,0.2), dark, enemy)
    limb("Goblin leg", (x,0,0.18), (x,0,0.6), 0.10, purple, enemy)
box("Tunic", (0,0,0.79), (0.48,0.34,0.48), purple, enemy,0.07)
box("Goblin belt", (0,-0.01,0.62), (0.51,0.36,0.07), gold, enemy,0.01)
sphere("Goblin head", (0,-0.03,1.18), (0.31,0.24,0.3), green, enemy)
for side in (-1,1):
    limb("Pointed ear", (side*0.24,0,1.24), (side*0.49,0.02,1.37), 0.13, green, enemy,0)
    limb("Goblin arm", (side*0.28,0,0.95), (side*0.44,-0.06,0.63), 0.10, green, enemy)
    box("Glowing eye", (side*0.12,-0.258,1.23), (0.11,0.022,0.045), glow, enemy,0.01)
box("Brow", (0,-0.23,1.32), (0.45,0.06,0.055), dark, enemy)
box("Mouth", (0,-0.265,1.08), (0.22,0.025,0.055), dark, enemy,0.01)
limb("Club handle", (0.46,-0.08,0.5), (0.46,-0.08,1.25), 0.055, dark, enemy)
sphere("Club head", (0.46,-0.08,1.29), (0.17,0.15,0.22), steel, enemy)

room = collection("03_Architecture_TowerRoom")
box("Floor slab", (0,0,0), (2.45,1.45,0.2), edge, room)
box("Back wall", (0,0.6,0.87), (2.4,0.22,1.54), stone, room)
for x in (-1.12,1.12):
    box("Stone pillar", (x,0,0.85), (0.22,1.42,1.58), stone, room)
    box("Pillar trim", (x,-0.1,0.22), (0.3,1.45,0.15), edge, room)
for z in (0.4,0.8,1.2):
    box("Masonry course", (0,0.472,z), (2.15,0.014,0.018), edge, room,0)
box("Gold threshold", (0,-0.73,0.10), (2.4,0.04,0.045), gold, room,0.005)

roof = collection("04_Architecture_TowerCrown")
box("Crown ledge", (0,0,0.06), (2.65,1.64,0.23), edge, roof)
for x in (-1.03,-0.35,0.35,1.03):
    box("Battlement", (x,0.50,0.4), (0.39,0.42,0.5), stone, roof)
for x in (-1.06,1.06):
    box("Front merlon", (x,-0.5,0.4), (0.39,0.42,0.5), stone, roof)
box("Crown crest", (0,-0.83,0.10), (0.3,0.05,0.23), gold, roof)

props = collection("05_Props_Chest")
box("Chest body", (0,0,0.25), (0.75,0.50,0.48), fabric, props)
for x in (-0.26,0.26):
    box("Chest band", (x,0,0.26), (0.07,0.54,0.52), gold, props)
box("Chest lock", (0,-0.29,0.27), (0.14,0.07,0.18), gold, props)

assets = [(hero,"Knight"),(enemy,"Goblin"),(room,"TowerRoom"),(roof,"TowerCrown"),(props,"Chest")]
for col, name in assets:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in col.objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = list(col.objects)[0]
    bpy.ops.export_scene.fbx(filepath=str(EXPORT / (name+".fbx")), use_selection=True,
        object_types={"MESH"}, add_leaf_bones=False, bake_anim=False, axis_forward="-Z", axis_up="Y")
    for obj in col.objects:
        obj.asset_mark()
        obj.asset_data.description = "Original Hero Wars starter asset. Editable geometry; exported to Unity Resources/Models."

# Arrange an editable asset board after exporting everything with a local origin.
for (col,name), offset in zip(assets, [(-3,0,0),(-1,0,0),(2,0,0),(2,0,2.0),(0,-1.7,0)]):
    for obj in col.objects:
        obj.location += Vector(offset)

scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.world.color = (0.05,0.07,0.10)
scene.render.engine = "CYCLES"
scene.cycles.samples = 24
scene.render.resolution_x = 1400
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
bpy.ops.object.camera_add(location=(7,-13,7))
camera = bpy.context.object
camera.name = "AssetLibrary_PreviewCamera"
camera.rotation_euler = (Vector((0,0,1))-camera.location).to_track_quat("-Z","Y").to_euler()
camera.data.type = "ORTHO"
camera.data.ortho_scale = 8.8
scene.camera = camera
for location, energy, size in [((1,-5,7),1300,6),((-5,-1,4),800,5),((3,4,6),1600,4)]:
    bpy.ops.object.light_add(type="AREA",location=location)
    light=bpy.context.object
    light.data.energy=energy
    light.data.shape="DISK"
    light.data.size=size
    light.rotation_euler=(Vector((0,0,1))-light.location).to_track_quat("-Z","Y").to_euler()

text = bpy.data.texts.new("READ_ME")
text.write("HERO WARS / EDITABLE ASSET LIBRARY\n\nCollections 01-05 contain original starter models.\n"
           "Exported FBX files live in Assets/_Game/Resources/Models.\n"
           "The characters are starter meshes, not rigged production characters.\n"
           "Add future characters, environments, props and animation collections here.\n"
           "Keep Blender source outside Assets; Unity uses the exported FBX models.\n"
           "The generator script rebuilds this starter library; do not rerun over hand edits.\n")
bpy.ops.object.select_all(action="DESELECT")
scene.render.filepath = str(SOURCE / "AssetLibrary_Preview.png")
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "HeroWars_AssetLibrary.blend"), compress=True)
bpy.ops.render.render(write_still=True)
print("ASSET_LIBRARY_READY", str(SOURCE / "HeroWars_AssetLibrary.blend"))
