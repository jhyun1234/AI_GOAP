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
    # 🔑 덤불에서 캐는 자리 — **손이 닿는 거리**다. 「무엇을 향해 뻗었는가」가 없으면
    #    동작은 통째로 안 읽힌다(나무 옆에서 팔만 뻗은 판을 한 번 버렸다).
    # 🔴 주민을 **카메라와 소품 사이에 두지 마라.** 첫 판은 주민 뒤통수가 덤불을 다 가렸다.
    #    카메라가 (+0.72, -0.78) 쪽에 서므로 주민은 그것에 **수직인 (0.78, 0.72)** 쪽에
    #    나란히 세운다 — 둘이 같은 깊이에 서서 둘 다 보인다.
    # 🔴 자리를 소품 근처에 잡지 마라 — 카메라가 (+0.72, -0.78)·2.9m 뒤에 서므로 그쪽에
    #    나무가 있으면 **렌즈가 잎 속에 들어간다**(한 판 초록 벽만 나왔다). 마을 남쪽 빈 땅이다.
    # 🔑 거리는 **0.55m** 다(판정 확정). 0.66 은 팔이 안 닿고, 0.47 은 머리가 덤불에 겹친다.
    'bush': ((0.00, -6.00), 0.42, (0.40, 0.38), False, 2.90),
}

# `build()` 에 없는 소품은 여기서 세운다. 🔴 `build()` 안에 새로 넣지 마라 —
# 그러면 이미 구운 회차의 그림이 바뀐다.
EXTRA = {'bush': lambda: village.bush(0.00, -6.00)}
WANT = [p for p in os.environ.get('PROPS', ','.join(LOOKS)).split(',') if p in LOOKS]

# 🔑 자세를 고를 수 있다 — 소품만이 아니라 **소품 앞에서 하는 동작**도 여기서 판정한다.
#    POSE=gather POSE_AT=0.99 PROPS=tree_pick 처럼 쓴다.
POSE = os.environ.get('POSE', 'stop')
POSE_AT = float(os.environ.get('POSE_AT', '0.4'))
assert POSE in motions.MOTIONS, '모르는 동작: %s' % POSE

bpy.ops.wm.read_factory_settings(use_empty=True)
village.build()
fire = village.flame(village.SPOTS['fire'])
village.flicker(fire, 0.31)                      # 불꽃이 가장 큰 위상
cam = stage.light_camera(res=RES)

for name in WANT:
    if name in EXTRA:
        EXTRA[name]()
    (px, py), az, (ox, oy), lit, d = LOOKS[name]
    for f in fire:
        f.hide_render = not lit
    # 🔴 `atan2(-oy, -ox)` 가 아니다. 규약이 **정면 = (-cos θ, -sin θ)** 이므로 그 값은
    #    주민을 소품에 **등지게** 세운다. 대칭 소품(우물·불)만 판정해 와서 안 드러났고,
    #    덤불에서 캐는 자세를 판정하다 잡았다 — 팔은 뻗는데 딴 데를 보고 있었다.
    mesh, arm = stage.rigged(loc=(px + ox, py + oy, 0),
                             rot_z=math.degrees(math.atan2(oy, ox)))    # 소품 쪽을 본다
    stage.pose(arm, motions.MOTIONS[POSE](POSE_AT))
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
