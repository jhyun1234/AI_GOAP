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
HOUSE_TOP = 1.85                  # 지붕 꼭대기 — 주민 키 1.0 의 1.85 배

# 주민이 일하러 가는 자리. 궤적이 겹치는 것이 보여야 하므로 서로 충분히 떨어뜨린다.
SPOTS = {
    'field': Vector((-2.7, -1.5, 0)),
    'well':  Vector((2.0, -1.2, 0)),
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


def build():
    """마을을 짓는다. 반환 dict 의 값은 전부 bpy 오브젝트다."""
    wall = stage.world_mat((0.085, 0.085, 0.092))
    roof = stage.world_mat(earth=True)
    ground_m = stage.world_mat((0.030, 0.032, 0.038))
    stone = stage.world_mat((0.065, 0.066, 0.070))
    char = stage.world_mat((0.030, 0.028, 0.026))   # 탄 나무·재. 돌보다 어두워야 그을음이다
    leaf = stage.world_mat((0.075, 0.092, 0.072))   # 나무가 안 보이면 마을이 안 읽힌다

    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 0, 0))
    ground = bpy.context.object
    ground.scale = (26, 26, 1)
    ground.data.materials.append(ground_m)

    houses = []
    for x, y, rz in HOUSES:
        body = _cube((x, y, 0.55), (1.5, 1.3, 1.1), wall, rz)
        _cone((x, y, 1.45), 1.25, 0.8, roof, rz, verts=4)      # 꼭대기 1.45 + 0.4 = 1.85
        houses.append(body)

    trees = []
    for x, y in TREES:
        _cube((x, y, 0.32), (0.16, 0.16, 0.64), roof)
        trees.append(_cone((x, y, 1.05), 0.62, 1.15, leaf, verts=6))

    # 밭 — 낮은 이랑 **여섯**. 이 회차에서 여섯이라는 수가 뜻을 진다
    field = _cube((SPOTS['field'].x, SPOTS['field'].y, 0.03), (2.2, 1.6, 0.05), roof)
    for i in range(6):
        _cube((SPOTS['field'].x - 0.9 + i * 0.36, SPOTS['field'].y, 0.08),
              (0.12, 1.5, 0.09), stage.world_mat((0.048, 0.043, 0.037)))

    well = _cube((SPOTS['well'].x, SPOTS['well'].y, 0.22), (0.62, 0.62, 0.44), stone)
    campfire = _fire_pit(SPOTS['fire'], stone, char)

    return {'ground': ground, 'houses': houses, 'trees': trees,
            'field': field, 'well': well, 'campfire': campfire}


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
        'spots': [[round(s.x, 3), round(s.y, 3)] for s in SPOTS.values()],
        'materials': sorted(used),
        'meaning_materials': sorted(m for m in used if m.startswith('meaning_')),
        'instrument_materials': sorted(m for m in used if m.startswith('instrument')),
    }, open(REPORT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('[village] saved', OUT)
