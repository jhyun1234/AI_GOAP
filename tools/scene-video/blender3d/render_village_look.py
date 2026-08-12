"""마을을 눈으로 보는 판. 주민 하나를 같이 세워 **축척**을 함께 본다.
   blender --background --factory-startup --python render_village_look.py"""
import bpy, sys, os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage, village

OUT_DIR = os.path.join(stage.OUT_ROOT, 'models')
os.makedirs(OUT_DIR, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
village.build()
stage.rigged(loc=(0.0, -0.6, 0), rot_z=135)      # 축척 기준 — 주민 키가 1.0 이다
cam = stage.light_camera()

VIEWS = {
    'village_wide': ((7.0, -8.6, 4.4), (0, 1.2, 0.6)),      # 마을 전경
    'village_eye':  ((2.2, -4.4, 0.9), (0, 0.4, 0.55)),     # 주민 눈높이(설계 §6-3)
    'village_dev':  ((1.4, -6.2, 9.0), (0, 1.0, 0.0)),      # 개발자 시점
}
for name, (loc, at) in VIEWS.items():
    stage.aim(cam, loc, at)
    bpy.context.scene.render.filepath = os.path.join(OUT_DIR, name + '.png')
    bpy.ops.render.render(write_still=True)
    print('[look]', name)
