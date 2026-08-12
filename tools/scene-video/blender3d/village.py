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
    for i in range(FIRE_STONES):
        a = 2 * math.pi * i / FIRE_STONES
        s = 0.15 + 0.045 * ((i * 3) % 4) / 3
        _cube((x + FIRE_R * math.cos(a), y + FIRE_R * math.sin(a), s * 0.42),
              (s, s * 0.86, s * 0.84), stone, rot_z=math.degrees(a) + i * 11)
    for i in range(FIRE_LOGS):
        a = 2 * math.pi * i / FIRE_LOGS + 0.4
        log = _cyl((x + 0.14 * math.cos(a), y + 0.14 * math.sin(a), 0.20), 0.048, 0.58, char)
        # 안쪽으로 기운 삼각대. Rx 가 꼭대기를 -Y 로 눕히므로 Z 를 a-90° 로 돌려 중심을 향한다
        log.rotation_euler = (math.radians(30), 0, a - math.pi / 2)
    return ash


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
        out.append(c)
    return out


def flicker(parts, t, k=1.0):
    """불이 **가만히 있으면 불이 아니다.** 정적 게이트에도 그대로 걸린다.
    k 는 불이 붙는 정도(0 이면 꺼진 것) — 인트로가 불을 지필 때 쓴다."""
    for i, o in enumerate(parts):
        p = 2 * math.pi * (FLICKER_HZ * t + i * 0.37)
        o.scale = (k * (1 + 0.10 * math.sin(p * 1.3)),
                   k * (1 + 0.10 * math.cos(p * 1.7)),
                   k * (1 + 0.22 * math.sin(p)))
        # 🔴 x·y 는 건드리지 마라 — 혀가 바깥으로 눕는 각이다(flame 이 넣어 둔다).
        o.rotation_euler = (o.rotation_euler.x, o.rotation_euler.y, 0.25 * math.sin(p * 0.8))
        o.hide_render = k <= 0.02          # 🔴 스케일 0 으로 끄면 밑색이 남는다(함정 ⑥)


def cold_front(width=0.95, span=30.0):
    """겨울이 마을을 가로질러 스친다. 반환: 띠 하나. 처음에는 꺼져 있다.

    🔴 한기(`chill`)는 **뜻층**이다 — 부르는 순간 그 프레임의 유채색 둘 중 하나를 쓴다.
       불과 같은 규약이고, 불이 켜져 있으면 그 프레임은 이미 상한이다.
    🔑 인트로의 초록 획과 **같은 도형**을 쓴다. 「무언가가 마을을 훑고 지나갔다」는 이
       파이프라인에서 이미 한 번 세운 어휘라, 다른 도형을 새로 만들면 어휘만 늘어난다."""
    # 🔴 **넓은 면에 strength 1.0 을 쓰지 마라**(stage.meaning_mat 주석). 첫 판은 화면을
    #    가로지르는 네온 띠가 돼서 마을보다 띠가 주인공이었다. 넓게·어둡게 깔아야
    #    「지나가는 한기」이지 「바닥에 그은 선」이 아니다.
    b = _cube((0, 0, 0.02), (width, span, 0.03),
              stage.meaning_mat('chill', strength=0.42, albedo_scale=0.32))
    b.hide_render = True
    return b


def sweep(front, u, a=-13.0, b=13.0, axis='x'):
    """u 0→1 로 마을을 가로지른다. 범위 밖이면 **렌더에서 뺀다**(함정 ⑥).

    🔑 `axis='y'` 면 띠를 눕혀 **앞뒤로** 밀어 준다. 카메라가 보는 쪽으로 다가와야
       「다가온다」가 되지, 화면 옆을 스치면 「지나간다」가 된다 — 둘은 다른 말이다."""
    front.hide_render = not (0.0 < u < 1.0)
    if axis == 'y':
        front.rotation_euler = (0, 0, math.pi / 2)
        front.location.y = a + (b - a) * u
    else:
        front.location.x = a + (b - a) * u


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
    _cone((x, y, 1.45), 1.25, 0.8, m['earth'], rot_z, verts=4)       # 꼭대기 1.45+0.4 = 1.85
    return body


def tree(x, y, s=1.0):
    """나무 — 둥근 기둥 + **층진 잎** 둘. 반환: 아래 잎.

    🔑 잎을 한 덩이로 두면 원뿔이고, 둘로 층지면 나무다. 크기를 넷 다 같게 두지 마라 —
       같은 나무 넷은 마을이 아니라 배경 텍스처로 보인다."""
    m = mats()
    _cyl((x, y, 0.33 * s), 0.085 * s, 0.66 * s, m['earth'])
    lower = _cone((x, y, 0.95 * s), 0.66 * s, 0.85 * s, m['leaf'], verts=6)
    _cone((x, y, 1.45 * s), 0.44 * s, 0.70 * s, m['leaf'], verts=6)
    return lower


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
    ground.scale = (26, 26, 1)
    ground.data.materials.append(m['ground'])

    houses = [house(x, y, rz) for x, y, rz in HOUSES]
    trees = [tree(x, y, TREE_SIZES[i % len(TREE_SIZES)]) for i, (x, y) in enumerate(TREES)]

    return {'ground': ground, 'houses': houses, 'trees': trees,
            'field': field(SPOTS['field']), 'well': well(SPOTS['well']),
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
