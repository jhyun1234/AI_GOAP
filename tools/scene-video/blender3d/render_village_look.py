"""마을을 눈으로 보는 판. 주민 하나를 같이 세워 **축척**을 함께 본다.
   blender --background --factory-startup --python render_village_look.py"""
import bpy, sys, os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage, village

OUT_DIR = os.path.join(stage.OUT_ROOT, 'models')
os.makedirs(OUT_DIR, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
village.build()
_mesh, _arm = stage.rigged(loc=(0.0, -0.6, 0), rot_z=135)   # 축척 기준 — 주민 키가 1.0 이다
# 🔑 차렷으로 세우면 카메라 비교가 안 된다 — **동작 중인 순간**을 잡아야 한다.
#    도끼가 박히는 순간(스쿼시가 가장 깊은 자리)으로 세운다.
import motions
stage.pose(_arm, motions.chop(0.93 * 1.1))
cam = stage.light_camera()

# (볼 자리, 겨냥, 렌즈mm). 🔴 **렌즈가 시점의 절반이다.** 설계 §6-3 이 시점마다 렌즈를
#    정해 뒀는데(눈높이 35 · 조망 50 · 개발자 70) 코드는 오래 45 하나로만 굽고 있었다.
#    금지된 것은 **샷 안에서** 바꾸는 것뿐이다 — 샷마다 다른 렌즈는 규칙 위반이 아니다.
VIEWS = {
    'village_wide': ((7.0, -8.6, 4.4), (0, 1.2, 0.6), 50),      # 마을 전경
    'village_eye':  ((2.2, -4.4, 0.9), (0, 0.4, 0.55), 35),     # 주민 눈높이(설계 §6-3)
    'village_dev':  ((1.4, -6.2, 9.0), (0, 1.0, 0.0), 70),      # 개발자 시점
    # 🔑 비교용 — **같은 순간, 같은 주민**을 두 시점으로 본다. 「동작이 작게 느껴지는 것이
    #    동작 탓인가 카메라 탓인가」는 이 두 장을 나란히 놓으면 바로 갈린다.
    'cmp_far':      ((4.6, -6.4, 3.5), (0.0, -0.2, 0.7), 45),    # 지금 쓰는 조망(45mm)
    'cmp_eye':      ((1.35, -2.05, 0.62), (0.0, -0.2, 0.58), 35),  # 눈높이 35mm
}
for name, (loc, at, lens) in VIEWS.items():
    cam.data.lens = lens
    stage.aim(cam, loc, at)
    bpy.context.scene.render.filepath = os.path.join(OUT_DIR, name + '.png')
    bpy.ops.render.render(write_still=True)
    print('[look]', name)
