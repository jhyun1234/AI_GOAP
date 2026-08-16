"""소품 하나를 **주민 옆에 놓고** 가까이서 본다 — 「그게 무엇으로 읽히는가」 판정용.
   blender --background --factory-startup --python render_prop_look.py

🔑 주민을 꼭 같이 세운다. 소품 판정은 늘 축척 문제였다 — 혼자 보면 다 그럴듯하고,
   사람 옆에 두면 그제야 우물이 상자인지 우물인지가 보인다.
🔴 이건 **판정용**이지 회차 산출물이 아니다. 나오는 곳은 `3d/models/props/` 다.

    PROPS=fire,well python 으로 고르거나, 안 주면 다섯 전부.
"""
import bpy, sys, os, math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mixamo, stage, village

OUT = os.path.join(stage.OUT_ROOT, 'models', 'props')
os.makedirs(OUT, exist_ok=True)
RES = (620, 620)

# 🔴 **자리를 여기 다시 적지 마라.** 옛 판은 우물을 (2.00,-1.20) 으로 적어 뒀는데
#    `village.SPOTS` 는 (3.0,-0.4) 로 옮긴 뒤였다(모닥불과 2.6° 라 「불타는 우물」로 보여서).
#    베낀 좌표는 원본이 움직이는 순간 조용히 틀리고, 마을을 사람 키로 키우자 그 어긋남이
#    2.3 m 로 벌어져 우물이 화면 밖으로 나갔다. 자리는 **마을이 갖는다.**
WHERE = {
    'fire':  village.SPOTS['fire'].xy,
    'well':  village.SPOTS['well'].xy,
    'field': village.SPOTS['field'].xy,
    'house': village.HOUSES[0][:2],
    'tree':  village.TREES[0],
    # 🔴 덤불만 예외다. `SPOTS['bush']` 는 **캐는 자리**고, 판정용 덤불은 마을 남쪽 빈 땅에
    #    따로 세운다 — 카메라가 (+0.72,-0.78) 뒤에 서는데 그쪽에 나무가 있으면
    #    렌즈가 잎 속에 들어간다(한 판 초록 벽만 나왔다).
    'bush':  (0.00, -6.00),
}

# 소품 → (겨냥 높이, 주민이 설 자리(소품 기준 오프셋), 불 켬, 카메라 거리)
# 🔴 거리를 하나로 통일하지 마라. 2.35 로 다 찍었더니 집이 지붕만 화면에 꽉 차서
#    무엇인지 판정이 불가능했다 — 소품 크기가 열 배씩 차이 난다.
LOOKS = {
    'fire':  (0.35, (1.05, -0.35), True, 2.35),
    'well':  (0.40, (1.10, -0.30), False, 2.35),
    'house': (1.00, (1.95, -1.55), False, 5.60),
    'tree':  (0.95, (1.15, -0.85), False, 4.20),
    'field': (0.20, (1.75, -0.30), False, 3.60),
    # 🔑 덤불에서 캐는 자리 — **손이 닿는 거리**다. 「무엇을 향해 뻗었는가」가 없으면
    #    동작은 통째로 안 읽힌다(나무 옆에서 팔만 뻗은 판을 한 번 버렸다).
    # 🔴 주민을 **카메라와 소품 사이에 두지 마라.** 첫 판은 주민 뒤통수가 덤불을 다 가렸다.
    #    카메라가 (+0.72, -0.78) 쪽에 서므로 주민은 그것에 **수직인 (0.78, 0.72)** 쪽에
    #    나란히 세운다 — 둘이 같은 깊이에 서서 둘 다 보인다.
    # 🔑 거리는 **0.55m** 다(판정 확정). 0.66 은 팔이 안 닿고, 0.47 은 머리가 덤불에 겹친다.
    'bush':  (0.42, (0.40, 0.38), False, 2.90),
}

# `build()` 에 없는 소품은 여기서 세운다. 🔴 `build()` 안에 새로 넣지 마라 —
# 그러면 이미 구운 회차의 그림이 바뀐다.
EXTRA = {'bush': lambda: village.add(village.bush, *WHERE['bush'])}
WANT = [p for p in os.environ.get('PROPS', ','.join(LOOKS)).split(',') if p in LOOKS]

# 🔑 자세를 고를 수 있다 — 소품만이 아니라 **소품 앞에서 하는 동작**도 여기서 판정한다.
#    POSE=shoot POSE_AT=0.99 PROPS=bush 처럼 쓴다.
# 🔴 기본은 **서 있는 것**이라야 한다. 소품 판정에 몸이 접힌 동작을 쓰면 무엇이 이상한지가
#    소품 탓인지 자세 탓인지 안 갈린다. `idle` 을 아직 안 받았으면 있는 것 중 하나로
#    돌리되 **무엇으로 돌렸는지 찍는다** — 조용히 다른 걸 쓰면 판정이 거짓말이 된다.
POSE = os.environ.get('POSE', 'idle')
POSE_AT = float(os.environ.get('POSE_AT', '0.4'))

# 🔴 아래 자리·거리는 옛 축척(주민 0.95m)에서 실측한 값이다. 마을이 사람 키로 커졌으니
#    같은 그림을 보려면 좌표와 거리를 같이 곱한다 — 소품만 커지고 카메라가 제자리면
#    렌즈가 소품 속으로 들어간다.
S = village.SCALE

bpy.ops.wm.read_factory_settings(use_empty=True)
village.build()
mixamo.spawn(loc=(0, 0, -99))          # 액션 목록을 씬으로 끌어온다(자리는 화면 밖)
if POSE not in {a.name for a in bpy.data.actions}:
    fallback = sorted(a.name for a in bpy.data.actions)[0]
    print(f'⚠️ 동작 «{POSE}» 이 없다 — «{fallback}» 으로 대신한다 (MIXAMO.md 1차 목록 참조)')
    POSE = fallback
fire = village.add(village.flame, village.SPOTS['fire'])
village.flicker(fire, 0.31)                      # 불꽃이 가장 큰 위상
cam = stage.light_camera(res=RES)

for name in WANT:
    if name in EXTRA:
        EXTRA[name]()
    (px, py), (az, (ox, oy), lit, d) = WHERE[name], LOOKS[name]
    for f in fire:
        f.hide_render = not lit
    # 🔴 `atan2(-oy, -ox)` 가 아니다. 규약이 **정면 = (-cos θ, -sin θ)** 이므로 그 값은
    #    주민을 소품에 **등지게** 세운다. 대칭 소품(우물·불)만 판정해 와서 안 드러났고,
    #    덤불에서 캐는 자세를 판정하다 잡았다 — 팔은 뻗는데 딴 데를 보고 있었다.
    meshes, arm = mixamo.spawn(loc=((px + ox) * S, (py + oy) * S, 0),
                               rot_z=math.degrees(math.atan2(oy, ox)))   # 소품 쪽을 본다
    mixamo.play(arm, POSE, POSE_AT, dur=1.0)
    # 사람 눈높이보다 조금 위에서 비스듬히 — 소품을 「지나가며 보는」 각도다
    loc = ((px + d * 0.72) * S, (py - d * 0.78) * S, (az + 1.05) * S)
    at = (px * S, py * S, az * S)
    stage.key_from_view(loc, at)
    stage.aim(cam, loc, at)
    bpy.context.view_layer.update()
    bpy.context.scene.render.filepath = os.path.join(OUT, name + '.png')
    bpy.ops.render.render(write_still=True)
    print('[prop]', name)
    for m in meshes:
        bpy.data.objects.remove(m, do_unlink=True)
    bpy.data.objects.remove(arm, do_unlink=True)

print('[prop] %d개 -> %s' % (len(WANT), OUT))
