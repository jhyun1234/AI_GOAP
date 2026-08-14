"""뜻층 다섯 + 계기층을 한 장에 찍는다. test_palette.py 가 이 그림을 잰다.
   blender --background --factory-startup --python render_palette_probe.py"""
import bpy, sys, os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'fixtures', 'palette_probe.png')
os.makedirs(os.path.dirname(OUT), exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
col = bpy.context.collection

for i, name in enumerate(stage.MEANING):
    bpy.ops.mesh.primitive_cube_add(size=1, location=((i - 2.5) * 0.95, 0, 0.32))
    bpy.context.object.scale = (0.62, 0.62, 0.62)
    bpy.context.object.data.materials.append(stage.meaning_mat(name))

# 계기층은 **공중에 뜬다** — 세계 물체에 안 붙는다는 규약을 그림으로도 지킨다
bpy.ops.mesh.primitive_cube_add(size=1, location=(2.85, 0, 0.32))
bpy.context.object.scale = (0.62, 0.62, 0.62)
bpy.context.object.data.materials.append(stage.instrument_mat())

cam = stage.light_camera(res=(700, 200))
stage.aim(cam, (0.24, -8.2, 1.1), (0.24, 0, 0.32))   # 여섯을 다 담는 자리
bpy.context.scene.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print('[palette] wrote', OUT)
