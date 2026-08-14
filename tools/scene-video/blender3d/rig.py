# Shorts 모델에 뼈대를 심는다 → D:\AI_GOAP-videos\3d\models\villager_rigged.blend
#   blender --background --factory-startup --python rig.py
#
# 뼈 위치는 **모델을 실측해서** 잡았다(단면 폭을 높이별로 재서 팔·다리 갈래를 찾음).
# figure.js 의 mini 비율표를 그대로 베끼지 않았다 — 그 표는 SDF 도형용이고 이 메시는
# 실제 형상이 조금 다르다(머리 중심 0.82 · 어깨 0.585 · 손 0.30 · 무릎 0.17 · 발목 0.055).
#
# 🔑 자동 웨이트(뼈 히트)는 메시가 **닫혀 있어야** 잘 붙는다. 이 모델은 손이 골반에 붙어
#    있던 것을 떼고 물샐틈없게 만들어 둔 상태다 — 그 정리가 여기서 값을 한다.
# ⚠️ 팔이 몸통에 바짝 붙어 있어서 손↔골반 웨이트가 샐 수 있다. 샜는지는 **포즈를 줘서**
#    본다(pose_test.py). 눈으로 안 보고 통과시키지 마라.
import bpy, math, os, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage                       # 경로 상수를 한 곳에서만 정한다 — 두 벌 두면 한쪽만 고쳐진다

SRC = stage.MODEL
OUT = stage.RIG_BLEND
OUT_DIR = os.path.dirname(OUT)

# 발바닥 0 · 정수리 1 기준. 모델 원점은 몸 한가운데라 z 를 +0.5 해서 세운다.
H = 0.5
Y_LEG, Y_SH, Y_EL, Y_WR = 0.050, 0.110, 0.140, 0.160
BONES = [
    # (이름, 머리, 꼬리, 부모)
    ('hips',       (0, 0, 0.300), (0, 0, 0.440), None),
    ('spine',      (0, 0, 0.440), (0, 0, 0.585), 'hips'),
    ('neck',       (0, 0, 0.585), (0, 0, 0.660), 'spine'),
    ('head',       (0, 0, 0.660), (0, 0, 0.950), 'neck'),
]
for s, y in (('L', 1), ('R', -1)):
    BONES += [
        (f'shoulder.{s}', (0, 0.060 * y, 0.585), (0, Y_SH * y, 0.575), 'spine'),
        (f'upperarm.{s}', (0, Y_SH * y, 0.575), (0, Y_EL * y, 0.440), f'shoulder.{s}'),
        (f'forearm.{s}',  (0, Y_EL * y, 0.440), (0, Y_WR * y, 0.330), f'upperarm.{s}'),
        (f'hand.{s}',     (0, Y_WR * y, 0.330), (0, Y_WR * y, 0.270), f'forearm.{s}'),
        (f'thigh.{s}',    (0, Y_LEG * y, 0.300), (0, Y_LEG * y, 0.170), 'hips'),
        (f'shin.{s}',     (0, Y_LEG * y, 0.170), (0, Y_LEG * y, 0.055), f'thigh.{s}'),
        # 정면이 -X 라 발끝은 -X 로 뻗는다
        (f'foot.{s}',     (0, Y_LEG * y, 0.055), (-0.075, Y_LEG * y, 0.020), f'shin.{s}'),
    ]

os.makedirs(OUT_DIR, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
col = bpy.context.collection

with bpy.data.libraries.load(SRC, link=False) as (_s, dst):
    dst.objects = ['geometry_0']
mesh = dst.objects[0]
mesh.parent = None
mesh.matrix_parent_inverse.identity()
mesh.rotation_mode = 'XYZ'
mesh.location = (0, 0, H)          # 발바닥을 0 에 세운다
col.objects.link(mesh)
bpy.context.view_layer.objects.active = mesh
mesh.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
mesh.name = 'villager'

arm_data = bpy.data.armatures.new('villager_rig')
arm = bpy.data.objects.new('villager_rig', arm_data)
col.objects.link(arm)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
made = {}
for name, head, tail, parent in BONES:
    b = arm_data.edit_bones.new(name)
    b.head, b.tail = head, tail
    if parent:
        b.parent = made[parent]
        b.use_connect = (tuple(made[parent].tail) == tuple(head))
    made[name] = b

# 🔴 **뼈 축 규약을 하나로 고정한다.** roll 을 Blender 가 뼈마다 알아서 계산하게 두면
#    팔은 X+ 가 뒤로, 다리는 X+ 가 앞으로 가는 식으로 **뼈마다 축이 갈린다**(실측 확인:
#    models/axis_probe/axes.png). 그 상태로는 걷기 하나 적는 데도 뼈마다 부호를 외워야 한다.
#    로컬 Z 를 월드 +X(캐릭터 앞뒤축)에 맞추면 로컬 X 가 월드 -Y(좌우축)로 통일된다.
#    → **X = 앞뒤 스윙·끄덕 · Z = 좌우 벌림·갸웃 · Y = 비틀림.** 전 뼈 공통.
bpy.ops.armature.select_all(action='SELECT')
bpy.ops.armature.calculate_roll(type='GLOBAL_POS_X')
bpy.ops.object.mode_set(mode='OBJECT')

# ── 자동 웨이트 ──────────────────────────────────────
bpy.ops.object.select_all(action='DESELECT')
mesh.select_set(True)
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.parent_set(type='ARMATURE_AUTO')

vg = [g.name for g in mesh.vertex_groups]
print('[rig] bones %d · vertex groups %d' % (len(arm_data.bones), len(vg)))
missing = [n for n, *_ in BONES if n not in vg]
print('[rig] 웨이트가 안 붙은 뼈:', missing if missing else '없음')

bpy.ops.wm.save_as_mainfile(filepath=OUT)
print('[rig] saved', OUT)
