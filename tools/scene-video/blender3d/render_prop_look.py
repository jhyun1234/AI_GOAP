"""소품 하나를 **주민 옆에 놓고** 가까이서 본다 — 「그게 무엇으로 읽히는가」 판정용.
   blender --background --factory-startup --python render_prop_look.py

🔑 주민을 꼭 같이 세운다. 소품 판정은 늘 축척 문제였다 — 혼자 보면 다 그럴듯하고,
   키 0.95 짜리 사람 옆에 두면 그제야 우물이 상자인지 우물인지가 보인다.
🔴 이건 **판정용**이지 회차 산출물이 아니다. 나오는 곳은 `3d/models/props/` 다.

    PROPS=fire,well python 으로 고르거나, 안 주면 다섯 전부.
"""
import bpy, sys, os, math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage, village, motions

OUT = os.path.join(stage.OUT_ROOT, 'models', 'props')
os.makedirs(OUT, exist_ok=True)
RES = (620, 620)

# 소품 → (볼 자리, 겨냥 높이, 주민이 설 자리(소품 기준 오프셋), 불 켬, 카메라 거리)
# 🔴 거리를 하나로 통일하지 마라. 2.35 로 다 찍었더니 집이 지붕만 화면에 꽉 차서
#    무엇인지 판정이 불가능했다 — 소품 크기가 열 배씩 차이 난다.
LOOKS = {
    'fire':  ((-0.20, 1.00), 0.35, (1.05, -0.35), True, 2.35),
    'well':  ((2.00, -1.20), 0.40, (1.10, -0.30), False, 2.35),
    'house': ((-3.40, 2.60), 1.00, (1.95, -1.55), False, 5.60),
    'tree':  ((-4.80, 0.60), 0.95, (1.15, -0.85), False, 4.20),
    'field': ((-2.70, -1.50), 0.20, (1.75, -0.30), False, 3.60),
}
WANT = [p for p in os.environ.get('PROPS', ','.join(LOOKS)).split(',') if p in LOOKS]

bpy.ops.wm.read_factory_settings(use_empty=True)
village.build()
fire = village.flame(village.SPOTS['fire'])
village.flicker(fire, 0.31)                      # 불꽃이 가장 큰 위상
cam = stage.light_camera(res=RES)

for name in WANT:
    (px, py), az, (ox, oy), lit, d = LOOKS[name]
    for f in fire:
        f.hide_render = not lit
    mesh, arm = stage.rigged(loc=(px + ox, py + oy, 0), rot_z=math.degrees(
        math.atan2(-(py + oy - py), -(px + ox - px))))     # 소품 쪽을 본다
    stage.pose(arm, motions.stop(0.4))
    # 사람 눈높이보다 조금 위에서 비스듬히 — 소품을 「지나가며 보는」 각도다
    loc = (px + d * 0.72, py - d * 0.78, az + 1.05)
    stage.key_from_view(loc, (px, py, az))
    stage.aim(cam, loc, (px, py, az))
    bpy.context.view_layer.update()
    bpy.context.scene.render.filepath = os.path.join(OUT, name + '.png')
    bpy.ops.render.render(write_still=True)
    print('[prop]', name)
    bpy.data.objects.remove(mesh, do_unlink=True)
    bpy.data.objects.remove(arm, do_unlink=True)

print('[prop] %d개 -> %s' % (len(WANT), OUT))
