"""마을 — 전부 프리미티브. 주민(키 1.0)이 기준 축척이다.
   blender --background --factory-startup --python village.py

🔴 매끈하게 만들지 마라. 캐릭터가 플랫 셰이딩 6,786 면이다 — 소품만 매끈하면 그때 깨진다.
   그래서 원뿔·원기둥의 `vertices` 를 낮게 잡아 **각이 보이게** 한다.
🔴 유채색을 쓰지 마라. 마을은 **세계층**이고, 유채색은 뜻이 있을 때만 뜬다(설계 §2).
   모닥불이 앰버로 타는 것은 샷 스크립트가 얹을 일이지 마을이 할 일이 아니다.

A 안(군무·파열)은 여는 그림과 닫는 그림이 같은 무대여야 성립한다 — 그래서 장소를 안 늘린다.
"""
import bpy, sys, os, math, json
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage

OUT = os.path.join(stage.OUT_ROOT, 'models', 'village.blend')
REPORT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'fixtures', 'village_report.json')

HOUSES = [(-3.4, 2.6, 25), (0.2, 4.0, -10), (3.6, 2.2, -32)]     # x, y, 회전(도)
TREES = [(-4.8, 0.6), (5.0, 1.1), (-2.4, 5.4), (3.1, 5.8)]
TREE_SIZES = (1.00, 0.86, 1.12, 0.93)     # 넷이 같은 크기면 마을이 아니라 배경 무늬가 된다
HOUSE_TOP = 1.85                  # 지붕 꼭대기 — 주민 키 1.0 의 1.85 배

# 주민이 일하러 가는 자리. 궤적이 겹치는 것이 보여야 하므로 서로 충분히 떨어뜨린다.
SPOTS = {
    'field': Vector((-2.7, -1.5, 0)),
    'well':  Vector((3.0, -0.4, 0)),   # 🔴 (2.0,-1.2) 은 훅 카메라에서 모닥불과
                                       #    2.6° 밖에 안 떨어져 **불타는 우물**로 보였다
    'tree':  Vector((4.6, 1.1, 0)),
    'fire':  Vector((-0.2, 1.0, 0)),
    # 🔑 식량 자원 노드(게임 `ResourceType.RawFood`). 채집 동작이 설 자리다 —
    #    이 마을에 「캘 곳」이 없어서 채집이 허공을 젓고 있었다.
    # 🔴 **행진 통로에 놓지 마라.** 여섯은 x −1.31 줄로 서서 마을을 가로지른다.
    #    (−0.75, −3.5) 에 뒀더니 훅의 궤적 검사가 「0.34 m 뚫는다」로 막았다.
    #    그래서 통로 밖에 두고, **캐는 사람이 걸어온다**(render_outro). 자원이 사람을
    #    찾아가는 게 아니라 사람이 자원을 찾아가는 것이 이 게임이다.
    'bush':  Vector((1.1, -2.9, 0)),
}


# 모닥불 — 돌 테두리·재·장작. 🔴 **불은 여기 없다**(아래 `flame`).
FIRE_R, FIRE_STONES, FIRE_LOGS = 0.46, 7, 4
FLICKER_HZ = 3.1


def _cyl(loc, r, h, mat, verts=6):
    """각이 보이는 원기둥. verts 를 올리면 매끈해지고, 그 순간 캐릭터와 결이 갈린다."""
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=h, location=loc)
    o = bpy.context.object
    o.rotation_mode = 'XYZ'
    o.data.materials.append(mat)
    return o


def _cube(loc, scale, mat, rot_z=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.object
    o.scale = scale
    o.rotation_mode = 'XYZ'
    o.rotation_euler = (0, 0, math.radians(rot_z))
    o.data.materials.append(mat)
    return o


def _cone(loc, r, h, mat, rot_z=0.0, verts=6):
    """지붕·나무. verts 를 낮게 두면 각이 보인다 — 캐릭터의 패싯과 같은 결이다."""
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, depth=h, location=loc)
    o = bpy.context.object
    o.rotation_mode = 'XYZ'
    o.rotation_euler = (0, 0, math.radians(rot_z))
    o.data.materials.append(mat)
    return o


def _fire_pit(spot, stone, char):
    """모닥불 자리 — 재 · 돌 테두리 · 세워 놓은 장작. 반환: 재 바닥(솟기 애니메이션의 기준).

    🔴 **불 없이도 모닥불로 읽혀야 한다.** 첫 판은 돌 상자 하나였고 사용자 판정이
       「불이 없는 게 제일 이상해」였는데, 진짜 문제는 불이 아니라 그것이 모닥불처럼
       **생기지 않았다**는 것이었다. 돌 테두리와 장작은 세계층(무채색)이라 유채색 예산을
       한 톨도 안 쓴다 — 즉 이 고침은 모든 샷에 공짜로 적용된다.
    🔴 돌 크기를 다 같게 두지 마라. 일곱이 똑같으면 공산품으로 보인다. 난수는 안 쓴다
       (결정성 게이트가 죽는다) — 번호에서 뽑아 흔든다."""
    x, y = spot.x, spot.y
    ash = _cyl((x, y, 0.025), 0.40, 0.05, char)
    kids = []
    for i in range(FIRE_STONES):
        a = 2 * math.pi * i / FIRE_STONES
        s = 0.15 + 0.045 * ((i * 3) % 4) / 3
        kids.append(_cube((x + FIRE_R * math.cos(a), y + FIRE_R * math.sin(a), s * 0.42),
                          (s, s * 0.86, s * 0.84), stone, rot_z=math.degrees(a) + i * 11))
    for i in range(FIRE_LOGS):
        a = 2 * math.pi * i / FIRE_LOGS + 0.4
        log = _cyl((x + 0.14 * math.cos(a), y + 0.14 * math.sin(a), 0.20), 0.048, 0.58, char)
        # 안쪽으로 기운 삼각대. Rx 가 꼭대기를 -Y 로 눕히므로 Z 를 a-90° 로 돌려 중심을 향한다
        log.rotation_euler = (math.radians(30), 0, a - math.pi / 2)
        kids.append(log)
    # 🔴 **돌과 장작을 반환값에 안 넣던 것이 집 지붕과 같은 버그였다**(2026-08-13, ep15s-4
    #    프레임에서 잡았다). 부르는 쪽이 `ash.hide_render` 만 만지면 재만 사라지고 **돌 테두리
    #    아홉이 그대로 남아서**, 아직 짓지도 않은 모닥불 자리가 전부 화면에 보였다.
    #    자식으로 묶어 두고 `pit_show` 하나가 통째로 켜고 끄게 한다.
    for k in kids:
        k.parent = ash
        k.matrix_parent_inverse = ash.matrix_basis.inverted()
    return ash


def pit_show(ash, on):
    """모닥불 자리를 통째로 렌더에 넣고 뺀다. 🔴 `ash.hide_render` 만 만지지 마라 —
    돌 테두리와 장작이 남는다(부모의 hide_render 는 자식에게 안 물린다)."""
    ash.hide_render = not on
    for c in ash.children_recursive:
        c.hide_render = not on


# 불꽃 혀 — (중심에서의 x, y, 밑반지름, 높이). 🔴 **높이를 다 다르게** 둔다.
#    첫 판은 같은 축에 원뿔 셋을 쌓았더니 실루엣이 삼각형 하나로 뭉쳐서, 장작 삼각대 위에
#    불이 아니라 **빛나는 천막**이 얹힌 것처럼 보였다. 불로 읽히는 것은 색이 아니라
#    **들쭉날쭉한 윤곽**이다. 가장 큰 혀는 장작 꼭대기(0.45)보다 확실히 높아야 한다.
TONGUES = ((0.000, 0.000, 0.115, 0.52), (0.085, 0.050, 0.075, 0.37),
           (-0.075, 0.060, 0.062, 0.30), (0.030, -0.085, 0.068, 0.34))
FLAME_Z = 0.16


def flame(spot):
    """모닥불의 **불**. 반환: 불꽃 혀 넷.

    🔴 앰버는 **뜻층**이다(설계 §2 — 삶·온기). 이 함수를 부르는 순간 그 프레임의 유채색
       예산 둘 중 하나를 쓴다. 그래서 `build()` 가 안 부른다 — **샷이 정한다.**
    🔑 예산이 맞는지는 프레임마다 `probe.metrics(...)['chroma_hues']` 로 재라.
       ⚠️ 넓은 샷에서는 불이 화면의 0.07% 라 `MIN_HUE_FRAC` 문턱 아래로 떨어져 게이트에
       안 잡힌다. 안 잡히는 것과 안 쓰는 것은 다르다 — 가까운 샷에서는 반드시 잡힌다."""
    mat = stage.meaning_mat('amber', strength=1.0, albedo_scale=0.25)
    out = []
    for dx, dy, r, h in TONGUES:
        c = _cone((spot.x + dx, spot.y + dy, FLAME_Z + h / 2), r, h, mat, verts=4)
        c.rotation_euler = (dy * 1.6, -dx * 1.6, 0)      # 바깥으로 살짝 눕는다
        # 🔑 눕는 각을 **적어 둔다.** `flicker` 가 매 프레임 새로 계산하는데, 기울기를
        #    현재값에 더하면 프레임마다 쌓여서 불이 드러눕는다(바람 인자 lean 을 넣을 때 겪었다).
        c['_rot0'] = (c.rotation_euler.x, c.rotation_euler.y)
        out.append(c)
    return out


def flicker(parts, t, k=1.0, lean=0.0):
    """불이 **가만히 있으면 불이 아니다.** 정적 게이트에도 그대로 걸린다.
    k 는 불이 붙는 정도(0 이면 꺼진 것) — 인트로가 불을 지필 때 쓴다.

    🔑 `lean` 은 **바람**이다(+X 쪽으로 눕는다). 바람은 눈에 안 보이므로 불이 대신 보여 준다 —
       눕고, 낮아지고, 더 빨리 떤다. 한기를 색 면으로 그리는 대신 쓰는 것이 이것이다."""
    a = abs(lean)
    for i, o in enumerate(parts):
        rx0, ry0 = o.get('_rot0', (o.rotation_euler.x, o.rotation_euler.y))
        p = 2 * math.pi * ((FLICKER_HZ + 2.6 * a) * t + i * 0.37)     # 바람이 불면 빨라진다
        o.scale = (k * (1 + 0.10 * math.sin(p * 1.3)),
                   k * (1 + 0.10 * math.cos(p * 1.7)),
                   k * (1 + 0.22 * math.sin(p)) * (1 - 0.35 * a))     # 눕는 만큼 낮아진다
        # 🔴 x·y 는 **기록해 둔 원래 각에서** 다시 계산한다. 현재값에 더하면 매 프레임 쌓인다.
        o.rotation_euler = (rx0, ry0 - lean * 1.15, 0.25 * math.sin(p * 0.8))
        o.hide_render = k <= 0.02          # 🔴 스케일 0 으로 끄면 밑색이 남는다(함정 ⑥)


# ── 눈 ────────────────────────────────────────────────
# 🔴 눈은 **세계층**이다(무채색). 한기를 뜻층 색 면으로 그리던 것을 걷어낸 자리에 온다 —
#    색 예산을 안 쓰면서 「겨울」이 0.3 초 안에 읽히는 유일한 관습 기호다.
# 🔴 알베도는 지붕(0.28)보다 밝고 주민(sRGB 0.85)보다 어두워야 한다. 눈이 주인공보다
#    밝으면 마을을 보러 온 화면에서 눈이 주인공이 된다(README 의 EARTH_LEVEL 과 같은 이유).
SNOW_N = 140
SNOW_ALBEDO = 0.38
SNOW_BOX = (12.0, 12.0)         # 덮는 범위. 카메라가 조망으로 오르므로 마을보다 넉넉히
SNOW_TOP, SNOW_FALL = 4.4, 4.0  # 시작 높이 · 한 바퀴 도는 낙하 거리
SNOW_SIZE = 0.026
SNOW_SPEED = 0.52               # m/s — 눈은 느리게 내린다. 빠르면 비가 된다


def snow(center=(0.0, 1.2), n=SNOW_N):
    """눈발. 반환: `snowfall` 에 그대로 넘기는 목록 [(객체, x, y, 위상)].

    🔴 자리를 `random` 으로 뿌리지 마라 — **결정성 게이트가 깨진다**(같은 입력에 같은 프레임).
       무리수 배수의 소수부로 뿌린다(황금비 산포). 씨앗도 상태도 없다."""
    m = stage.world_mat((SNOW_ALBEDO,) * 3)
    out = []
    for i in range(n):
        gx, gy, gz = (i * 0.7548776662) % 1.0, (i * 0.5698402909) % 1.0, (i * 0.4114206) % 1.0
        x = center[0] + (gx - 0.5) * SNOW_BOX[0]
        y = center[1] + (gy - 0.5) * SNOW_BOX[1]
        f = _cube((x, y, SNOW_TOP - gz * SNOW_FALL), (SNOW_SIZE,) * 3, m)
        f.hide_render = True
        out.append((f, x, y, gz))
    return out


def snowfall(parts, t, wind=0.0, k=1.0):
    """눈을 내린다. `wind` 는 +X 쪽으로 미는 세기(불의 `lean` 과 같은 값을 준다).

    🔑 바람이 셀수록 **옆으로 눕고 빨리 지나간다.** 그게 「몰아친다」다 —
       같은 눈이 세로로만 떨어지면 아무리 많아도 조용한 눈이다."""
    for o, x, y, ph in parts:
        u = (ph + t * SNOW_SPEED * (1 + 1.8 * abs(wind)) / SNOW_FALL) % 1.0
        fall = u * SNOW_FALL
        o.location = (x + wind * fall * 1.5 + 0.10 * math.sin(9.1 * ph + 2.3 * t),
                      y + 0.08 * math.cos(7.7 * ph + 1.9 * t),
                      SNOW_TOP - fall)
        # 🔴 `o.scale = (k, k, k)` 로 쓰지 마라. `_cube` 는 **크기를 스케일로 준다** —
        #    덮어쓰면 눈송이가 기본 큐브 크기(1m)가 돼서 화면이 흰 상자로 덮인다(실제로 그랬다).
        o.scale = (SNOW_SIZE * k,) * 3
        o.hide_render = k <= 0.02


_MATS = None


def mats():
    """세계층 재질 표. 🔑 소품 함수를 **혼자서도** 부를 수 있게 여기 모아 둔다 —
    소품이 `build()` 안에 갇혀 있으면 다음 회차가 재사용할 수가 없다."""
    global _MATS
    if _MATS is None:
        _MATS = {
            'wall':   stage.world_mat((0.085, 0.085, 0.092)),
            'earth':  stage.world_mat(earth=True),
            'ground': stage.world_mat((0.030, 0.032, 0.038)),
            'stone':  stage.world_mat((0.065, 0.066, 0.070)),
            'char':   stage.world_mat((0.030, 0.028, 0.026)),  # 탄 나무·재. 돌보다 어두워야 그을음
            'soil':   stage.world_mat((0.042, 0.038, 0.033)),  # 갈아엎은 흙. 두둑보다 어둡다
            'tilled': stage.world_mat((0.055, 0.049, 0.041)),  # 볕 받는 두둑. 흙과 살짝만 차이 난다
            'leaf':   stage.world_mat((0.075, 0.092, 0.072)),  # 나무가 안 보이면 마을이 안 읽힌다
        }
    return _MATS


def _carve(body, cutters):
    """불리언으로 파낸다. 🔴 **뚫지 말고 파라** — 관통시키면 집 속이 비어 반대편이 보인다.
    파낸 자리는 빛이 안 들어 저절로 어두워지고, 그게 문이 문으로 읽히는 이유다.
    🔴 모디파이어를 남기지 말고 **적용**한다. 남기면 매 프레임 평가되고(로우폴리 실측과
       같은 이유로 느려지고) `.blend` 로 저장할 때 소품이 아니라 설정을 넘기게 된다."""
    before = len(body.data.vertices)
    bpy.context.view_layer.objects.active = body
    for i, c in enumerate(cutters):
        md = body.modifiers.new('cut%d' % i, 'BOOLEAN')
        md.operation, md.object = 'DIFFERENCE', c
        bpy.ops.object.modifier_apply(modifier=md.name)
    for c in cutters:
        bpy.data.objects.remove(c, do_unlink=True)
    assert len(body.data.vertices) > before, '불리언이 아무것도 안 팠다'
    return body


def house(x, y, rot_z=0.0):
    """집 — 벽 · 지붕 · **문과 창**. 반환: 몸통.

    🔴 문도 창도 없으면 그냥 상자다. 실루엣은 지붕이 만들지만, 「사람이 사는 곳」은
       **구멍**이 만든다 — 벽에 뚫린 자리가 있어야 안이 있다고 읽힌다.
    🔑 원점에서 파고 나서 옮긴다. 회전한 뒤에 파려면 커터 좌표를 손으로 회전시켜야 하고,
       그 각도는 언젠가 틀린다."""
    m = mats()
    body = _cube((0, 0, 0.55), (1.5, 1.3, 1.1), m['wall'])
    # (커터 자리, 커터 크기) — 앞면은 -Y 다. 깊이는 관통하지 않을 만큼만
    holes = [((0, -0.62, 0.33), (0.42, 0.30, 0.66)), ((0.48, -0.62, 0.76), (0.34, 0.22, 0.30))]
    _carve(body, [_cube(loc, sz, m['char']) for loc, sz in holes])
    # 🔴 파기만 하면 안 된다. 불리언이 만든 새 면은 **벽 재질을 물려받아** 그냥 회색 판으로
    #    보인다 — 첫 판의 문이 문이 아니라 벽에 붙인 패널로 읽힌 이유다. 안쪽을 대 준다.
    for (cx, cy, cz), (sx, sy, sz) in holes:
        back = _cube((cx, cy + sy / 2 - 0.012, cz), (sx, 0.02, sz), m['char'])
        back.parent = body
        back.matrix_parent_inverse = body.matrix_basis.inverted()
    body.location = (x, y, 0.55)
    body.rotation_euler = (0, 0, math.radians(rot_z))
    roof = _cone((x, y, 1.45), 1.25, 0.8, m['earth'], rot_z, verts=4)  # 꼭대기 1.45+0.4 = 1.85
    # 🔴 **지붕은 반환값에 안 들어 있었다.** 그래서 회차 스크립트가 `body` 만 켜고 끄고
    #    키우면, 아직 안 지은 집의 **지붕만 허공에 먼저 떠 있었다**(2026-08-13 사용자 판정:
    #    「주민 옆에 집 지붕 오브젝트가 먼저 보임」). 켜고 끄는 일을 부르는 쪽에 맡기지 말고
    #    여기서 조각을 기억한다 — 조각을 늘려도 부르는 쪽이 안 고쳐진다.
    _HOUSE_PARTS[body.name] = [(body, 0.55, body.scale.z), (roof, 1.45, roof.scale.z)]
    return body


# 집 하나가 몇 조각인가 — `house_show`·`house_grow` 가 읽는다. 벽에 낸 구멍의 뒷판은
# 몸통의 **자식**이라 몸통을 따라가므로 여기 안 적는다(자식은 `children_recursive` 로 찾는다).
_HOUSE_PARTS = {}


def house_parts(body):
    return [o for o, _z, _s in _HOUSE_PARTS[body.name]] + list(body.children_recursive)


def house_show(body, on):
    """집을 통째로 렌더에 넣고 뺀다. 🔴 `body.hide_render` 만 만지지 마라 — 지붕이 남는다."""
    for o in house_parts(body):
        o.hide_render = not on


def house_grow(body, k):
    """집이 땅에서 **자라 오른다**(k 0~1). 0 이면 렌더에서 빠진다.

    🔴 몸통만 키우면 지붕이 제 높이에 그대로 떠 있다 — 그게 「지붕이 먼저 보인다」의 정체다.
    🔑 조각마다 제 밑높이에 비례해 올라온다. 그래서 다 자란 순간이 원래 집과 정확히 같다."""
    k = max(0.0, min(1.0, k))
    house_show(body, k > 0.001)
    if k <= 0.001:
        return
    for o, z0, sz0 in _HOUSE_PARTS[body.name]:
        o.scale.z = max(sz0 * k, 1e-4)
        o.location.z = z0 * k


def tree(x, y, s=1.0):
    """나무 — 둥근 기둥 + **층진 잎** 둘. 반환: 아래 잎.

    🔑 잎을 한 덩이로 두면 원뿔이고, 둘로 층지면 나무다. 크기를 넷 다 같게 두지 마라 —
       같은 나무 넷은 마을이 아니라 배경 텍스처로 보인다."""
    m = mats()
    _cyl((x, y, 0.33 * s), 0.085 * s, 0.66 * s, m['earth'])
    lower = _cone((x, y, 0.95 * s), 0.66 * s, 0.85 * s, m['leaf'], verts=6)
    _cone((x, y, 1.45 * s), 0.44 * s, 0.70 * s, m['leaf'], verts=6)
    return lower


def grave(x, y, rot_z=0.0):
    """무덤 — 낮은 흙더미 + **아무것도 안 적힌 비석.** 반환: 조각 목록.

    🔴 조각 하나만 돌려주지 않는다 — `house` 의 지붕과 `_fire_pit` 의 돌 테두리로 **두 번**
       밟은 함정이다(부르는 쪽이 하나만 끄면 나머지가 화면에 남는다).
    🔑 **글자를 안 새긴다.** 이 세계에는 글자가 없고, 빈 비석이 곧 「이름이 없다」다 —
       ep15s-5 아웃트로가 다음 편(무덤에 이름이 없던 마을)으로 넘기는 자리가 그것이다.
    🔴 세계층이다(흙·돌). 유채색 예산을 한 톨도 안 쓴다."""
    m = mats()
    mound = _cyl((x, y, 0.055), 0.46, 0.11, m['earth'], verts=8)
    stone = _cube((x, y - 0.26, 0.30), (0.32, 0.09, 0.60), m['stone'], rot_z=rot_z)
    cap = _cone((x, y - 0.26, 0.65), 0.19, 0.13, m['stone'], rot_z=rot_z, verts=4)
    return [mound, stone, cap]


# ── 마을 바깥 ─────────────────────────────────────────
# 🔴 **카메라를 넓게 잡으면 마을이 작아지는 게 아니라 빈 땅이 커진다.** ep15s-3 은 「멀리
#    떨어져 나갔다」가 사건이라 카메라가 넓어야 하는데, 마을이 10m 대라 넓히는 순간
#    화면 절반이 아무것도 없는 바닥이 됐다(2026-08-13 사용자 판정: 「맵을 넓게 만들어라」).
# 🔴 **소품을 새로 만들지 않는다**(PROPS.md). 있는 나무·덤불을 바깥에 더 심을 뿐이다.
# 🔴 `build()` 에 넣지 마라 — `fixtures/village_report.json` 의 조각 수가 바뀐다.
#    바깥을 원하는 회차가 부른다.
# 🔴 자리는 **비워 둘 곳을 피해서** 골랐다: 목수가 마을 밖으로 나가는 길(약 60°)과
#    그가 혼자 집을 짓는 자리 둘레는 반경 3m 안에 아무것도 없다.
# 🔴 **카메라 앞을 비워 둔다.** 이 편의 카메라는 전부 남쪽(−Y)에서 마을을 본다 —
#    y < −4 이면서 x 가 −5~7 이면 그건 배경이 아니라 **렌즈를 막는 물건**이다
#    (첫 판에서 (3.4,−7.2) 나무가 화면 아래를 통째로 덮었다).
OUTSKIRT_TREES = [(-9.6, 3.2, 1.05), (-8.4, -3.4, 0.92), (-10.8, -7.4, 1.10),
                  (10.2, -6.6, 0.88), (9.4, -1.2, 1.02), (10.6, 5.2, 0.95),
                  (0.9, 11.4, 1.08), (-6.2, 9.4, 0.90), (5.4, 10.6, 0.86),
                  (-13.0, 1.0, 1.00), (13.2, 2.6, 0.94)]
OUTSKIRT_BUSHES = [(-7.0, 0.4, 0.90), (7.6, 1.4, 0.85), (-9.2, 5.6, 0.95),
                   (-6.6, -6.0, 0.82), (8.2, 7.0, 0.88)]


def outskirts():
    """마을 바깥의 나무·덤불. 넓게 잡은 카메라가 빈 바닥을 안 비추게 한다."""
    return ([tree(x, y, s) for x, y, s in OUTSKIRT_TREES]
            + [bush(x, y, s) for x, y, s in OUTSKIRT_BUSHES])


# ── 도구 ──────────────────────────────────────────────
# 🔴 `chop`·`mine`·`hammer` 는 **도구가 없으면 서로 구별이 안 된다.** 셋 다 「팔을 들었다
#    내린다」이기 때문이다. 게임의 `AnimKind` 가 이 넷을 이름으로 갈라 놓은 이유가 그것이다.
# 🔴 자루는 **+Y 를 따라 눕힌다** — `stage.hold` 가 뼈 로컬 +Y(손이 뻗은 방향)에 붙인다.
#    `_cyl` 은 Z 로 서므로 X 축 -90° 로 눕혀야 +Y 가 된다.
# 🔴 유채색을 쓰지 마라. 도구는 **세계층**이다(돌·나무).
HAFT_R = 0.016


def _haft(length, mat, y0=0.0):
    o = _cyl((0, y0 + length / 2, 0), HAFT_R, length, mat)
    o.rotation_euler = (math.radians(-90), 0, 0)
    return o


def _tool(name, parts):
    """부품들을 빈 오브젝트 하나에 매단다. `stage.hold` 는 이 하나만 손에 붙인다."""
    root = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(root)
    for p in parts:
        p.parent = root
        p.matrix_parent_inverse.identity()
    return root


def axe(s=1.0):
    """도끼 — Clear(벌목) · `AnimKind.Chop`. 자루 + 두꺼운 등 + **얇은 날.**

    🔴 머리를 **상자 하나로 두지 마라.** 옆에서 보면 주걱으로 읽힌다(첫 판이 그랬다) —
       이 문서 맨 위 규칙 그대로다. 도구 머리는 **등(두껍고 짧다) + 날(얇고 길다)** 둘로
       갈라야 그 도구가 된다. 아래 셋도 같은 규칙을 쓴다.
    🔑 날은 자루에 **직각으로** 뻗는다(+Z). 자루 축에 나란히 두면 몽둥이다.
    🔴 날을 **상자로 두지 마라.** 옆에서 보면 자루 위에 얹힌 T자 막대가 된다(두 판 그랬다).
       날은 **삼각 쐐기**여야 하고, 넓은 면이 스윙 평면(자루축 × 날 방향)을 향해야 한다.
       X 로 납작하게 눌러 그 면을 만든다 — 그래야 어느 각도에서든 「쐐기」로 읽힌다."""
    m = mats()
    poll = _cube((0, 0.290 * s, 0.005 * s), (0.038 * s, 0.060 * s, 0.052 * s), m['stone'])
    blade = _cone((0, 0.300 * s, 0.055 * s), 0.088 * s, 0.115 * s, m['stone'], verts=3)
    blade.scale = (0.17, 1.0, 1.0)              # 납작한 쐐기 — 넓은 면이 스윙 평면이다
    return _tool('axe', [_haft(0.38 * s, m['earth'], -0.06 * s), poll, blade])


def pickaxe(s=1.0):
    """곡괭이 — Clear(채석) · `AnimKind.Mine`. 자루 + **양쪽으로 뻗은 뿔 둘.**
    🔑 도끼와 갈리는 것은 **뾰족함과 좌우 대칭**이다. 날이 면이면 도끼, 뿔이면 곡괭이다."""
    m = mats()
    parts = [_haft(0.40 * s, m['earth'], -0.07 * s),
             _cube((0, 0.30 * s, 0), (0.030 * s, 0.042 * s, 0.042 * s), m['stone'])]
    for z, r, h, flip in ((0.105 * s, 0.030 * s, 0.130 * s, 0.0),
                          (-0.085 * s, 0.026 * s, 0.100 * s, math.pi)):
        horn = _cone((0, 0.30 * s, z), r, h, m['stone'], verts=4)
        horn.rotation_euler = (flip, 0, 0)
        parts.append(horn)
    return _tool('pickaxe', parts)


def hammer(s=1.0):
    """망치 — Build·Repair·Craft · `AnimKind.Hammer`. **짧은 자루 + 뭉툭한 머리.**
    🔑 도끼·곡괭이와 갈리는 것은 **자루가 짧다**는 것이다. 긴 자루에 뭉툭한 머리를 달면
       그건 망치가 아니라 메다 — 실루엣에서 자루 길이가 먼저 읽힌다."""
    m = mats()
    head = _cube((0, 0.215 * s, 0), (0.048 * s, 0.050 * s, 0.115 * s), m['stone'])
    claw = _cube((0, 0.215 * s, -0.080 * s), (0.030 * s, 0.036 * s, 0.050 * s), m['stone'])
    return _tool('hammer', [_haft(0.26 * s, m['earth'], -0.05 * s), head, claw])


def wateringcan(s=1.0):
    """물뿌리개 — Farm(Plant)·Tend · `AnimKind.Water`. 통 + **주둥이.**
    🔴 통만 만들면 양동이다. **주둥이가 이 물건을 물뿌리개로 만든다** — 기울였을 때
       물이 나가는 쪽이 보여야 「준다」가 된다.
    🔑 물은 안 그린다. 유채색이면 뜻층 예산을 쓰고, 무채색이면 안 보인다 —
       기울인 주둥이가 그 말을 대신한다."""
    m = mats()
    body = _cyl((0, 0.19 * s, 0), 0.072 * s, 0.135 * s, m['stone'], verts=8)
    spout = _cyl((0, 0.24 * s, 0.105 * s), 0.018 * s, 0.150 * s, m['stone'])
    spout.rotation_euler = (math.radians(-38), 0, 0)
    grip = _cube((0, 0.19 * s, 0.090 * s), (0.020 * s, 0.075 * s, 0.014 * s), m['earth'])
    return _tool('wateringcan', [body, spout, grip])


def _blob(loc, r, mat, squash=0.62):
    """저폴리 덩이 하나(20면). 🔴 부드럽게 만들지 마라 — 이 마을은 전부 플랫 셰이딩이다."""
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=r, location=loc)
    o = bpy.context.object
    o.scale = (1.0, 1.0, squash)
    o.data.materials.append(mat)
    return o


def bush(x, y, s=1.0):
    """덤불 — 게임의 **식량 자원 노드**(`ResourceType.RawFood`)다. 반환: 가운데 덩이.

    🔴 나무로 대신하지 마라. 이 마을 나무는 **층진 원뿔 둘, 침엽수**라 열매가 안 달린다.
       주민을 나무 옆에 세워 팔을 뻗게 했더니 「무엇을 향해 뻗었는지」가 없어서 동작이
       통째로 안 읽혔다 — 판정 한 판을 그렇게 버렸다.
    🔴 열매를 유채색 점으로 찍지 마라. 그 순간 뜻층 예산을 쓴다(설계 §2).
       덤불은 **세계층**이고, 「먹을 것이 있는 곳」은 자리와 크기로 말한다.
    🔑 높이는 주민 키(0.95)의 절반 안쪽 — 허리께다. 그래야 **굽혀서 따는** 동작이 산다.
       나무만 하게 만들면 또 팔을 머리 위로 들어야 하고, 이 캐릭터는 손이 정수리를 못 넘는다.
    🔴 덩이 셋을 붙여 놓으면 **이끼 낀 돌**로 읽힌다(첫 판이 그랬다). 나무가 층진 잎 둘로
       나무가 된 것과 같은 규칙이다 — 덩이를 **떼어 놓고 높이를 어긋내고**, 사이로 잔가지가
       삐져나와야 「덤불」이다. 매끈한 덩어리는 지형이지 식물이 아니다."""
    m = mats()
    _cyl((x, y, 0.05 * s), 0.05 * s, 0.10 * s, m['earth'])           # 밑동
    # 🔑 잔가지가 잎 사이로 **삐져나온다.** 이게 없으면 덩이 무더기다
    for dx, dy, tilt in ((-0.10, 0.13, 0.30), (0.15, 0.05, -0.26), (0.02, -0.15, 0.18)):
        tw = _cyl((x + dx * s, y + dy * s, 0.28 * s), 0.017 * s, 0.44 * s, m['earth'])
        tw.rotation_euler = (tilt, tilt * 0.7, 0)
    core = _blob((x, y, 0.28 * s), 0.27 * s, m['leaf'], squash=0.58)
    # 🔑 곁덩이는 **떼어 놓고 높이를 어긋낸다.** 붙이면 다시 한 덩어리가 된다
    _blob((x - 0.30 * s, y + 0.11 * s, 0.17 * s), 0.19 * s, m['leaf'], squash=0.66)
    _blob((x + 0.27 * s, y - 0.13 * s, 0.21 * s), 0.17 * s, m['leaf'], squash=0.54)
    return core


def well(spot):
    """우물 — 돌 테두리 · 기둥 둘 · 도르래 대 · 줄에 매달린 두레박. 반환: 테두리.

    🔴 첫 판은 돌 상자였다. 주민에게 `draw`(줄을 번갈아 당기는) 동작을 만들어 놓고
       **당길 줄을 안 만들었다** — 동작이 허공을 당기고 있었다. 소품이 동작을 받쳐야 한다."""
    m = mats()
    x, y = spot.x, spot.y
    rim = _cyl((x, y, 0.17), 0.42, 0.34, m['stone'], verts=8)   # 6 각이면 상자로 읽힌다
    _cyl((x, y, 0.335), 0.33, 0.04, m['char'], verts=8)                       # 물이 보이는 구멍
    for sx in (-0.34, 0.34):
        _cube((x + sx, y, 0.73), (0.08, 0.08, 0.86), m['earth'])
    _cube((x, y, 1.13), (0.84, 0.09, 0.09), m['earth'])              # 도르래 대
    _cube((x, y, 0.87), (0.025, 0.025, 0.44), m['char'])             # 줄
    _cyl((x, y, 0.58), 0.11, 0.16, m['earth'])                       # 두레박
    return rim


def field(spot, rows=6):
    """밭 — 갈아엎은 흙 위에 **둥근 이랑**. 반환: 흙 바닥.

    🔴 첫 판은 밝은 바닥에 어두운 각목을 얹어서 **나무 데크**로 읽혔다. 뒤집어야 한다 —
       바닥이 어둡고(갈아엎은 흙) 이랑이 밝아야(볕 받는 두둑) 밭고랑이 된다.
    🔴 이랑을 각지게 두지 마라. 누운 원기둥이라야 두둑이고, 상자면 각목이다.
    🔑 이랑이 **여섯**인 것은 이 회차에서 여섯이라는 수가 뜻을 지기 때문이다."""
    m = mats()
    x, y = spot.x, spot.y
    # 🔴 흙바닥을 **땅에 붙여라.** 0.05 로 띄웠더니 옆면이 테두리처럼 보여서 밭 전체가
    #    나무 쟁반에 담긴 꼴이었다. 밭은 파인 것이지 얹은 것이 아니다.
    base = _cube((x, y, 0.009), (2.35, 1.72, 0.018), m['soil'])
    # 🔴 두둑을 **땅에 파묻는다.** 통째로 얹으면 누운 통나무이고, 눌러서 납작하게 만들면
    #    윗면이 평평해져 **널빤지**가 된다(두 판 다 그렇게 읽혔다). 반쯤 묻어서 **둥근 윗면만**
    #    보이게 하는 것이 두둑이다 — 흙에서 솟은 것에는 평평한 면이 없다.
    # 🔴 색 차이도 줄인다. 밝은 띠가 어두운 바닥 위에 또렷하게 얹히면 그게 바로 널빤지다.
    for i in range(rows):
        r = _cyl((x - 0.9 + i * 1.8 / (rows - 1), y + 0.03 * ((i * 5) % 3 - 1), 0.012),
                 0.105, 1.36 + 0.08 * ((i * 3) % 3), m['tilled'])
        r.rotation_euler = (math.pi / 2, 0, 0)          # 눕혀서 Y 방향 두둑으로
    for i in range(5):                                               # 흙덩이 — 반듯하면 밭이 아니다
        a = 2.1 * i + 0.7
        _cube((x + 0.95 * math.cos(a), y + 0.72 * math.sin(a), 0.07),
              (0.10, 0.09, 0.08), m['soil'], rot_z=37 * i)
    return base


# 주민 몸 반지름 · 넘어갈 수 있는 턱 · 밑으로 지나갈 수 있는 높이
BODY_R, STEP, HEAD = 0.22, 0.20, 0.90


def _world_bounds(o):
    """월드 좌표계에서의 최소·최대. 🔴 `o.dimensions` 를 쓰지 마라 — **로컬** 바운딩박스에
    스케일만 곱한 값이라 **회전이 안 들어간다.** 눕힌 두둑(길이 1.36)이 세워진 채로 읽혀서
    높이 1.36 짜리 기둥으로 잡혔고, `view_layer.update()` 를 불러도 그대로였다.
    회전이 있는 물체를 잴 때는 bound_box 를 matrix_world 로 옮겨야 한다."""
    pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
    xs, ys, zs = [p.x for p in pts], [p.y for p in pts], [p.z for p in pts]
    return (min(xs), max(xs), min(ys), max(ys), min(zs), max(zs))


def obstacles(exclude=()):
    """주민이 뚫고 지나가면 안 되는 것들. 반환 [(x, y, 반지름, 이름), ...].

    🔑 **땅 위로 얼마나 솟았는가**로 고른다. 물체의 크기가 아니다.
       · 꼭대기가 무릎(0.20) 아래면 밟고 지나간다 — 밭바닥 0.02 · 두둑 0.12 · 재 0.05
       · 밑동이 머리(0.90) 위면 밑으로 지나간다 — 지붕 처마가 1.05 에서 시작한다
    🔴 부모가 있는 것은 뺀다(문 안쪽 판처럼 부모 안에 든 것들이다).
    ⚠️ 원으로 근사한다. 길쭉한 물체는 실제보다 크게 잡히니, 그런 것이 통로에 있으면
       이 함수가 아니라 **동선**을 고쳐라 — 그게 가로 대열을 접게 만든 검사다."""
    import bpy
    out = []
    for o in bpy.context.scene.objects:
        if o.type != 'MESH' or o in exclude or o.parent:
            continue
        x0, x1, y0, y1, z0, z1 = _world_bounds(o)
        if z1 < STEP or z0 >= HEAD:
            continue
        out.append(((x0 + x1) / 2, (y0 + y1) / 2, max(x1 - x0, y1 - y0) / 2, o.name))
    return out


def build():
    """마을을 짓는다. 반환 dict 의 값은 전부 bpy 오브젝트다."""
    m = mats()

    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 0, 0))
    ground = bpy.context.object
    # 🔴 26 은 **넓은 카메라에서 땅이 끝난다.** ep15s-3 은 「멀리 나갔다」가 사건이라
    #    카메라가 뒤로 물러나는데, 그 순간 지평선 대신 **바닥의 모서리**가 보였다.
    #    바닥은 무채색 한 장이라 넓혀도 유채색 예산도 렌더 시간도 안 먹는다(면 둘이다).
    # 🔴 60 도 모자랐다 — 30m 앞에서 땅이 끝나 화면 위 3분의 1 이 통째로 빈 하늘이었다.
    #    200 이면 바닥 모서리가 사실상 지평선이라 그 띠가 5분의 1 로 줄어든다.
    ground.scale = (200, 200, 1)
    ground.data.materials.append(m['ground'])

    houses = [house(x, y, rz) for x, y, rz in HOUSES]
    trees = [tree(x, y, TREE_SIZES[i % len(TREE_SIZES)]) for i, (x, y) in enumerate(TREES)]

    return {'ground': ground, 'houses': houses, 'trees': trees,
            'field': field(SPOTS['field']), 'well': well(SPOTS['well']),
            'bush': bush(SPOTS['bush'].x, SPOTS['bush'].y),
            'campfire': _fire_pit(SPOTS['fire'], m['stone'], m['char'])}


if __name__ == '__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    v = build()
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    os.makedirs(os.path.dirname(REPORT), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=OUT)
    # 🔴 유채색 재질을 하나도 안 썼다는 것을 **주장이 아니라 값**으로 남긴다.
    used = {m.name for o in bpy.context.scene.objects if o.type == 'MESH'
            for m in o.data.materials if m}
    json.dump({
        'houses': [h.name for h in v['houses']],
        'trees': [t.name for t in v['trees']],
        'house_height': HOUSE_TOP,
        'fire': {'stones': FIRE_STONES, 'logs': FIRE_LOGS, 'ash': v['campfire'].name},
        # 🔑 「소품이 몇 조각인가」를 값으로 남긴다. 상자 하나로 되돌아가는 것을 검사가 막는다.
        'house_verts': len(v['houses'][0].data.vertices),
        'parts': {n: sum(1 for o in bpy.context.scene.objects if o.type == 'MESH'
                         and (o.matrix_world.translation.xy - Vector((sp.x, sp.y))).length < 1.15)
                  for n, sp in SPOTS.items()},
        'spots': [[round(s.x, 3), round(s.y, 3)] for s in SPOTS.values()],
        'materials': sorted(used),
        'meaning_materials': sorted(m for m in used if m.startswith('meaning_')),
        'instrument_materials': sorted(m for m in used if m.startswith('instrument')),
    }, open(REPORT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('[village] saved', OUT)
