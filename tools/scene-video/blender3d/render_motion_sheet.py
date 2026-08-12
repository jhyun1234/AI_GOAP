"""동작 여덟의 컨택트 시트. **웨이트가 새는지 여기서 본다.**
   blender --background --factory-startup --python render_motion_sheet.py

🔴 `chop` 은 팔을 머리 위로 든다 — 골반이 딸려 오면 여기서 보인다. 이 모델은 손이 골반에
   붙어 있던 것을 떼어 물샐틈없게 만든 덕에 뼈 히트가 18본 전부 붙었지만, 붙었다는 것과
   깨끗하게 변형된다는 것은 다른 일이다."""
import bpy, sys, os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage, motions

OUT = os.path.join(stage.OUT_ROOT, 'models', 'motion_sheet')
os.makedirs(OUT, exist_ok=True)

# 동작마다 「가장 잘 보이는 순간」이 다르다. 주기 동작은 극점을, 한 번뿐인 것은 끝을 찍는다.
AT = {'look_up': 0.35, 'walk': 0.25 / motions.WALK_HZ, 'stop': 0.0,
      'farm': 0.35, 'chop': 0.62 * 1.1, 'draw': 0.4, 'reach': 0.9, 'freeze': 0.9}

bpy.ops.wm.read_factory_settings(use_empty=True)
# 🔴 rot_z 135 는 정면 벡터가 시선과 **정확히 수직**이라 완전한 옆모습이 나온다(실측).
#    카메라 방향(-0.70, -0.72)에 대해 정면이 3/4 로 열리는 자리가 80 이다.
mesh, arm = stage.rigged(loc=(0, 0, 0), rot_z=80)
cam = stage.light_camera(res=(560, 800))
stage.aim(cam, (-1.30, -1.34, 0.68), (0, 0, 0.50))   # 3/4 앞 — 팔과 골반이 같이 보이는 각도

for name in ('look_up', 'walk', 'stop', 'farm', 'chop', 'draw', 'reach', 'freeze'):
    stage.pose(arm, motions.MOTIONS[name](AT[name]))
    bpy.context.view_layer.update()
    bpy.context.scene.render.filepath = os.path.join(OUT, name + '.png')
    bpy.ops.render.render(write_still=True)
    print('[motion]', name)
