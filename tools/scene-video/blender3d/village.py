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


def build():
    """마을을 짓는다. 반환 dict 의 값은 전부 bpy 오브젝트다."""
    wall = stage.world_mat((0.085, 0.085, 0.092))
    roof = stage.world_mat(earth=True)
    ground_m = stage.world_mat((0.030, 0.032, 0.038))
    stone = stage.world_mat((0.065, 0.066, 0.070))
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
    campfire = _cube((SPOTS['fire'].x, SPOTS['fire'].y, 0.10), (0.55, 0.55, 0.20), stone)

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
        'spots': [[round(s.x, 3), round(s.y, 3)] for s in SPOTS.values()],
        'materials': sorted(used),
        'meaning_materials': sorted(m for m in used if m.startswith('meaning_')),
        'instrument_materials': sorted(m for m in used if m.startswith('instrument')),
    }, open(REPORT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('[village] saved', OUT)
