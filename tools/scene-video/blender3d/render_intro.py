"""인트로 — 마을이 지어지는 5.143 초. **45 편이 공유하므로 한 번만 렌더한다.**
   blender --background --factory-startup --python render_intro.py

  0.00~0.75  빈 땅. 초록 획(코드)이 화면을 가로지른다
  0.75~2.20  집 셋이 차례로 솟는다
  2.20~3.20  나무와 밭·우물·모닥불이 놓인다
  3.20~4.20  주민 여섯이 하나씩 놓인다
  4.25~4.70  여섯이 **동시에 고개를 든다**
  4.70~5.14  카메라가 훅의 시작 자리로 내려앉는다(브랜드 자막은 엔진이 얹는다)

🔴 첫 판은 3.543 초였고 사용자 판정이 「주민이 나온 다음에 바로 끝」이었다. 말이 끝난 뒤에도
   마을이 서고 여섯이 고개를 들 시간이 필요해서 S0 의 pauseAfter 를 1800 으로 늘렸다
   (쇼츠 길이 상한이 1 분대로 열린 뒤라 낼 수 있는 시간이다).

🔑 **인트로가 짓는 그 마을이 본문의 무대다.** 인트로가 장식이 아니라 무대 설치가 되고,
   끝나는 순간 카메라가 컷 없이 훅으로 내려간다. 마지막 비트가 훅의 「여섯이 동시에」와
   그대로 이어진다.

🔴 유채색은 **초록 하나뿐**이다(코드 = 시스템이 한 일). 설계 §2 의 「한 프레임에 둘까지」를
   인트로는 하나로 지킨다 — 여기서 색을 더 쓰면 본문이 쓸 예산을 미리 태운다.
"""
import bpy, sys, os, math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage, village, unison, motions

OUT = os.path.join(stage.OUT_ROOT, '_intro', 'frames')     # 회차 밖 — 45 편이 공유한다
FPS, DUR = 30, 5.143
NF = round(DUR * FPS) + 1
os.makedirs(OUT, exist_ok=True)


def ease(u):
    u = max(0.0, min(1.0, u))
    return u * u * (3 - 2 * u)


bpy.ops.wm.read_factory_settings(use_empty=True)
v = village.build()
arms = unison.place()
cam = stage.light_camera()
# 🔴 훅과 **같은 빛**이어야 한다. 착지하는 자리(HOOK_CAM)로 키라이트를 맞춘다 — 두 샷이
#    다른 태양 방위를 쓰면 컷 없이 이어지는 그 순간 밝기가 튄다.
stage.key_from_view(*unison.HOOK_CAM)

# 코드 획 — 초록. 땅을 가로지르며 **지나간 자리에서** 마을이 솟는다
sweep_mat = stage.meaning_mat('green', strength=1.0)
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0.02))
line = bpy.context.object
line.scale = (0.09, 30, 0.02)
line.data.materials.append(sweep_mat)

# 무엇이 언제 솟는가. 집 → 나무 → 살림 순서다(사람이 마을을 짓는 순서와 같다).
RISE = {}
for i, h in enumerate(v['houses']):
    RISE[h] = (0.75 + i * 0.46, 0.44)
for i, t_ in enumerate(v['trees']):
    RISE[t_] = (2.20 + i * 0.16, 0.36)
RISE[v['field']] = (2.55, 0.38)
RISE[v['well']] = (2.80, 0.34)
RISE[v['campfire']] = (3.00, 0.32)

# 🔴 집 몸통·지붕은 따로 만들어져 RISE 에 몸통만 들어 있다. 지붕이 공중에 남으면 안 되므로
#    같은 자리에서 솟는 것들을 **좌표로 묶는다** — village.build 가 이름을 안 돌려주기 때문이다.
for ob in bpy.context.scene.objects:
    if ob.type != 'MESH' or ob in RISE or ob is v['ground'] or ob is line:
        continue
    if ob.parent:            # 🔴 부모를 따라 솟는다. 여기 또 넣으면 두 번 움직인다
        continue
    near = min(RISE, key=lambda k: (k.location.x - ob.location.x) ** 2
               + (k.location.y - ob.location.y) ** 2)
    d2 = (near.location.x - ob.location.x) ** 2 + (near.location.y - ob.location.y) ** 2
    RISE[ob] = RISE[near] if d2 < 4.0 else (2.55, 0.38)

BASE_Z = {o: o.location.z for o in RISE}
BASE_SZ = {o: o.scale.z for o in RISE}

# 🔥 모닥불에 불이 붙는다 — 마을이 **살아나는** 마지막 손질이다.
# 🔴 RISE 를 다 세운 **뒤에** 만든다. 위 근접 묶기 루프가 불꽃도 땅에서 솟게 만들어 버린다.
# 🔴 앰버는 뜻층이라 예산 하나를 쓴다. 인트로의 「유채색은 초록 하나뿐」은 **프레임당** 규칙이고,
#    초록 획은 0.79 초에 사라지고 불은 3.35 초에 붙는다 — 한 프레임에 겹치지 않는다.
#    🔑 겹치게 만들지 마라. 겹치는 순간 인트로가 본문 예산을 미리 태운다.
FIRE_AT = 3.35
fire = village.flame(village.SPOTS['fire'])

for fi in range(NF):
    t = fi / FPS

    line.location.x = -14 + 28 * ease(t / 0.75)
    line.hide_render = t > 0.79

    for o, (t0, span) in RISE.items():
        k = ease((t - t0) / span)
        o.scale.z = max(BASE_SZ[o] * k, 1e-4)
        o.location.z = BASE_Z[o] * k
        o.hide_render = k <= 0.001

    village.flicker(fire, t, ease((t - FIRE_AT) / 0.40))

    for i, arm in enumerate(arms):
        t0 = 3.20 + i * 0.17
        k = ease((t - t0) / 0.20)
        hidden = k <= 0.001
        arm.hide_render = hidden
        for ob in arm.children:
            ob.hide_render = hidden
        arm.location.z = -0.55 * (1 - k)
        # 🔑 마지막 하나가 놓인 뒤 여섯이 **동시에** 고개를 든다 — 훅으로 이어지는 비트.
        #    위상을 어긋내면 군무가 아니다(unison.py).
        stage.pose(arm, motions.look_up(max(0.0, t - 4.25)))

    # 🔑 **훅이 시작하는 자리에 정확히 착지한다**(render_unison_proof 의 첫 프레임과 같은 값).
    #    그래야 「인트로가 끝나는 순간 컷 없이 훅으로 내려간다」가 말이 아니라 사실이 된다.
    u = ease(t / DUR)
    (ex, ey, ez), (ea, eb, ec) = unison.HOOK_CAM
    start_loc, start_at = (8.20, -9.60, 5.60), (0.00, 0.90, 0.60)
    loc = tuple(a + (b - a) * u for a, b in zip(start_loc, (ex, ey, ez)))
    at = tuple(a + (b - a) * u for a, b in zip(start_at, (ea, eb, ec)))
    stage.aim(cam, loc, at)

    bpy.context.view_layer.update()
    bpy.context.scene.render.filepath = os.path.join(OUT, '%04d.png' % fi)
    bpy.ops.render.render(write_still=True)

print('[intro] %d frames -> %s' % (NF, OUT))
