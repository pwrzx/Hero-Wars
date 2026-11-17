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
limestone = material("Keep_Limestone", (0.72, 0.69, 0.62))
ashlar = material("Keep_Ashlar", (0.32, 0.40, 0.46))
vermilion = material("Keep_Vermilion", (0.68, 0.20, 0.13))

def cut_stone(name, outline, front, back, mat, col, bevel=0.018):
    """Extrude an X/Z silhouette between two Y planes; negative Y faces the player."""
    n = len(outline)
    if sum(x * outline[(i+1) % n][1] - z * outline[(i+1) % n][0]
           for i, (x, z) in enumerate(outline)) < 0:
        outline = outline[::-1]
    vertices = [(x, y, z) for y in (front, back) for x, z in outline]
    faces = [tuple(range(n)), tuple(reversed(range(n, 2*n)))]
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, i+n, j+n, j))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    return finish(obj, name, mat, col, bevel)

def gallery_floor(col):
    box("Gallery floor", (0, 0, 0), (2.45, 1.45, 0.20), limestone, col, 0.035)
    box("Floor shadow course", (0, 0, -0.067), (2.49, 1.48, 0.068), ashlar, col, 0.018)

gallery_floor(room)
for side in (-1, 1):
    x = side * 1.10
    box("Gallery pier", (x, 0.02, 0.82), (0.23, 1.31, 1.46), ashlar, room)
    box("Pier foot", (x, -0.015, 0.18), (0.31, 1.39, 0.17), limestone, room, 0.025)
    box("Pier capital", (x, -0.02, 0.96), (0.33, 1.40, 0.13), limestone, room, 0.022)
    # The spandrel closes the corner above the arch without covering the fighter.
    cut_stone("Spandrel", [(side*0.20, 1.55), (side*1.21, 1.55),
              (side*1.21, 0.99), (side*1.02, 0.99), (side*0.77, 1.32)],
              -0.48, 0.64, ashlar, room)

def arch_point(angle, outer):
    width, rise = (1.20, 0.65) if outer else (0.98, 0.48)
    return (width * math.cos(angle), 0.98 + rise * math.sin(angle))

for index in range(7):
    left = math.pi * index / 7 + 0.005
    right = math.pi * (index + 1) / 7 - 0.005
    middle = (left + right) / 2
    outline = [arch_point(a, True) for a in (left, middle, right)]
    outline += [arch_point(a, False) for a in (right, middle, left)]
    cut_stone("Arch voussoir", outline, -0.68, -0.45, limestone, room, 0.012)

roof = collection("04_Architecture_TowerCrown")
box("Parapet cornice", (0,0,0.015), (2.64,1.62,0.21), limestone, roof)
box("Parapet wall", (0,0,0.19), (2.49,1.48,0.22), ashlar, roof, 0.025)
for x in (-1.04, -0.35, 0.35, 1.04):
    height = 0.76 if abs(x) > 0.9 else 0.63
    outline = [(x-0.21, 0.27), (x+0.21, 0.27), (x+0.21, height-0.08),
               (x+0.15, height), (x-0.15, height), (x-0.21, height-0.08)]
    cut_stone("Chamfered merlon", outline, -0.71, -0.27, limestone, roof)
    if abs(x) > 0.9:
        cut_stone("Return merlon", outline, 0.29, 0.69, ashlar, roof)
cut_stone("Shield mount", [(-0.19,0.29),(0.19,0.29),(0.16,0.02),(0,-0.09),(-0.16,0.02)],
          -0.83,-0.72, gold, roof)
cut_stone("Shield enamel", [(-0.13,0.24),(0.13,0.24),(0.10,0.05),(0,-0.025),(-0.10,0.05)],
          -0.849,-0.824, vermilion, roof, 0.006)
limb("Pennant staff", (-0.77,0.20,0.26), (-0.77,0.20,1.31), 0.033, gold, roof)
sphere("Staff finial", (-0.77,0.20,1.34), (0.066,0.066,0.076), gold, roof)
cut_stone("Swallowtail pennant", [(-0.75,1.28),(-0.50,1.29),(-0.23,1.22),(0.15,1.25),
          (-0.01,1.08),(0.14,0.97),(-0.23,0.94),(-0.51,1.01),(-0.75,0.98)],
          0.16,0.205,vermilion,roof,0.009)

foundation = collection("06_Architecture_TowerFoundation")
# Staggered oversize blocks read as masonry at phone scale. The shaft continues below the frame.
for row in range(12):
    top = -0.08 - row * 0.84
    seam = -0.31 if row % 2 == 0 else 0.41
    for start, end in ((-1.14, seam), (seam, 1.14)):
        box("Foundation ashlar", ((start+end)/2, 0.04, top-0.42),
            (end-start-0.008, 1.28, 0.837), ashlar, foundation, 0.018)
for side in (-1, 1):
    cut_stone("Battered buttress", [(side*0.87,-0.11),(side*1.12,-0.11),
              (side*1.12,-0.60),(side*1.24,-1.18),(side*1.24,-10.16),(side*0.87,-10.16)],
              -0.70,-0.42,ashlar,foundation,0.035)
cut_stone("Lancet surround", [(-0.27,-1.72),(0.27,-1.72),(0.27,-0.78),(0,-0.53),(-0.27,-0.78)],
          -0.634,-0.59,limestone,foundation)
cut_stone("Lancet reveal", [(-0.135,-1.65),(0.135,-1.65),(0.135,-0.84),(0,-0.70),(-0.135,-0.84)],
          -0.65,-0.626,stone,foundation,0.008)
box("Lancet sill", (0,-0.64,-1.73), (0.62,0.14,0.09), limestone,foundation,0.018)

balcony = collection("07_Architecture_HeroBalcony")
gallery_floor(balcony)
box("Low rear parapet", (0,0.59,0.22), (2.34,0.21,0.27), ashlar,balcony)
for x in (-1.05,1.05):
    box("Corner block", (x,0.04,0.27), (0.27,1.30,0.36), ashlar,balcony)
    box("Parapet cap", (x,0.04,0.46), (0.34,1.36,0.11), limestone,balcony,0.025)
cut_stone("Blue standard", [(-0.96,0.10),(-0.46,0.10),(-0.46,-0.58),(-0.71,-0.80),(-0.96,-0.58)],
          -0.79,-0.735,blue,balcony,0.012)
cut_stone("Standard device", [(-0.71,-0.15),(-0.61,-0.33),(-0.71,-0.51),(-0.81,-0.33)],
          -0.802,-0.785,gold,balcony,0.005)
box("Standard rail", (-0.71,-0.765,0.065), (0.59,0.10,0.075), gold,balcony,0.015)

# Keep one renderer per stone or fabric material when a room is instanced repeatedly on mobile.
for col in (room, roof, foundation, balcony):
    groups = {}
    for obj in list(col.objects):
        groups.setdefault(obj.data.materials[0].name, []).append(obj)
    for mat_name, group in groups.items():
        bpy.ops.object.select_all(action="DESELECT")
        for obj in group:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = group[0]
        bpy.ops.object.convert(target="MESH")
        if len(group) > 1:
            bpy.ops.object.join()
        merged = bpy.context.object
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        merged.name = col.name.split("_")[-1] + "_" + mat_name

props = collection("05_Props_Chest")
box("Chest body", (0,0,0.25), (0.75,0.50,0.48), fabric, props)
for x in (-0.26,0.26):
    box("Chest band", (x,0,0.26), (0.07,0.54,0.52), gold, props)
box("Chest lock", (0,-0.29,0.27), (0.14,0.07,0.18), gold, props)

assets = [(hero,"Knight"),(enemy,"Goblin"),(room,"TowerRoom"),(roof,"TowerCrown"),(props,"Chest"),
          (foundation,"TowerFoundation"),(balcony,"HeroBalcony")]
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
for (col,name), offset in zip(assets, [(-3,0,0),(-1,0,0),(2,0,0),(2,0,1.65),(0,-1.7,0),(2,0,0),(-3,0,-0.10)]):
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
text.write("HERO WARS / EDITABLE ASSET LIBRARY\n\nCollections 01-07 contain original starter models.\n"
           "Exported FBX files live in Assets/_Game/Resources/Models.\n"
           "The characters are starter meshes, not rigged production characters.\n"
           "Add future characters, environments, props and animation collections here.\n"
           "Keep Blender source outside Assets; Unity uses the exported FBX models.\n"
           "The generator script rebuilds this starter library; do not rerun over hand edits.\n")
bpy.ops.object.select_all(action="DESELECT")
scene.render.filepath = "//AssetLibrary_Preview.png"
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "HeroWars_AssetLibrary.blend"), compress=True)
bpy.ops.render.render(write_still=True)
print("ASSET_LIBRARY_READY", str(SOURCE / "HeroWars_AssetLibrary.blend"))
