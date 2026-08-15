"""새 주민 — **사람 비율 · T 포즈**, 메시와 뼈를 같은 표에서 짓는다 (M32 · 2026-08-16).

    blender --background --factory-startup --python model.py
    → D:\\AI_GOAP-videos\\3d\\models\\villager2_rigged.blend  (villager · villager_rig)

## 왜 새로 만드나

옛 모델은 **3.3 두신 · 머리가 키의 30% · 팔이 사람의 3/4** 이고 레스트가 **팔을 내린 자세**다.
Mixamo 모캡은 7.5 두신 사람이 T 포즈 레스트에서 움직인 것이라, 리타기팅을 아무리 정확히 해도
**비율이 안 맞아 어색하다**(2026-08-16 사용자 판정 — 렌더를 눈으로 보고 내린 결론이다).
🔑 그때 배운 것: **기계 검사가 초록이어도 사람 눈이 아니라고 하면 아닌 것이다.**

## 이 파일의 규칙 하나

🔴 **비율표(`P`)가 유일한 출처다.** 메시도 뼈도 여기서 파생된다. 옛 `rig.py` 는 뼈 위치를
   **메시를 실측해서** 잡았는데(그 주석이 그렇게 적혀 있다), 그러면 모델을 고칠 때마다 뼈를
   손으로 다시 재야 하고 언젠가 한쪽만 고쳐진다. 여기서는 한 표를 둘이 읽는다.

## T 포즈인 이유

Mixamo 레스트가 T 포즈다. 레스트가 같으면 리타기팅이 **월드 델타 그대로**(기준면 A) 맞아서
`mocap2pose.ARM_CHAIN` 같은 팔 전용 예외가 필요 없어진다 — 예외는 언젠가 다른 데서 샌다.

## 키

🔴 `H` 는 **미터**다. 옛 주민이 0.95 였고 이 모델은 **1.70** 이라 화면에서 1.8 배 커진다.
   그래서 카메라 거리·소품 크기가 전부 다시 잡혀야 한다 — 부르는 쪽이 `model.H` 를 읽어
   **키의 배수로** 카메라를 놓게 한다(절대 미터를 적으면 다음에 키를 바꿀 때 또 어긋난다).
"""
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage

H = 1.70                       # 키 (m). 발바닥 0 · 정수리 H
OUT = os.path.join(stage.OUT_ROOT, 'models', 'villager2_rigged.blend')

# ── 비율표 — 전부 **키에 대한 비**다 (7.5 두신 표준 인체) ────────────────
# 세로 자리 (발바닥 0 · 정수리 1)
P = dict(
    crown=1.000, chin=0.867, shoulder=0.818, chest=0.800,
    waist=0.620, hip=0.520, knee=0.285, ankle=0.039,
    elbow_y=0.255, wrist_y=0.400, fingertip_y=0.470,   # T 포즈라 팔은 **가로(Y)** 로 뻗는다
    clav_y=0.030, shoulder_y=0.075,
    leg_y=0.055,                                       # 다리 좌우 간격(중심에서)
)
# 굵기 (키 대비 반지름)
R = dict(head=0.0665, neck=0.026, chest=0.082, waist=0.062, hip=0.072,
         upperarm=0.028, forearm=0.023, hand=0.021,
         thigh=0.045, shin=0.033, foot=0.030)

# 뼈 — (이름, 머리, 꼬리, 부모). 🔴 **T 포즈**: 팔이 ±Y 로 수평이다.
#    좌우는 `for s, y in (('L', 1), ('R', -1))` 로 찍어 낸다(옛 rig.py 규약 계승).
def bones():
    B = [('hips',  (0, 0, P['hip']),      (0, 0, P['waist']), None),
         ('spine', (0, 0, P['waist']),    (0, 0, P['chest']), 'hips'),
         ('neck',  (0, 0, P['chest']),    (0, 0, P['chin']),  'spine'),
         ('head',  (0, 0, P['chin']),     (0, 0, P['crown']), 'neck')]
    for s, y in (('L', 1), ('R', -1)):
        sh = P['shoulder']
        B += [
            (f'shoulder.{s}', (0, P['clav_y'] * y, sh), (0, P['shoulder_y'] * y, sh), 'neck'),
            (f'upperarm.{s}', (0, P['shoulder_y'] * y, sh), (0, P['elbow_y'] * y, sh), f'shoulder.{s}'),
            (f'forearm.{s}',  (0, P['elbow_y'] * y, sh), (0, P['wrist_y'] * y, sh), f'upperarm.{s}'),
            (f'hand.{s}',     (0, P['wrist_y'] * y, sh), (0, P['fingertip_y'] * y, sh), f'forearm.{s}'),
            (f'thigh.{s}',    (0, P['leg_y'] * y, P['hip']), (0, P['leg_y'] * y, P['knee']), 'hips'),
            (f'shin.{s}',     (0, P['leg_y'] * y, P['knee']), (0, P['leg_y'] * y, P['ankle']), f'thigh.{s}'),
            # 발은 **앞(−X)** 으로 뻗는다 — 정면이 −X 라는 무대 규약(stage.pose 주석)과 같은 축
            (f'foot.{s}',     (0, P['leg_y'] * y, P['ankle']), (-0.085, P['leg_y'] * y, 0.010), f'shin.{s}'),
        ]
    return B


# ── 메시 조각 ────────────────────────────────────────────
# 🔴 매끈하게 만들지 마라. 소품이 `verts=6~8` 로 각져 있다 — 사람만 매끈하면 결이 갈린다.
SEG = 8


def _seg(a, b, r0, r1, mat, verts=SEG):
    """두 점을 잇는 **테이퍼 원기둥** 한 마디. 팔·다리·몸통이 전부 이것 하나로 지어진다."""
    ax, ay, az = (v * H for v in a)
    bx, by, bz = (v * H for v in b)
    dx, dy, dz = bx - ax, by - ay, bz - az
    length = math.sqrt(dx * dx + dy * dy + dz * dz)
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r0 * H, radius2=r1 * H,
                                    depth=length,
                                    location=((ax + bx) / 2, (ay + by) / 2, (az + bz) / 2))
    o = bpy.context.object
    o.rotation_mode = 'XYZ'
    # +Z 로 선 것을 (a→b) 로 눕힌다.
    # 🔴 XYZ 오일러는 월드에서 **Rz·Ry·Rx** 로 곱해진다: (0,0,1) → (cosφ sinθ, sinφ sinθ, cosθ).
    #    그러므로 θ = acos(dz/L) · **φ = atan2(dy, dx)** 다. 첫 판은 φ 에 +π/2 를 붙였고,
    #    그 한 항 때문에 팔이 ±Y(T 포즈)가 아니라 ±X 로 뻗어 옆에서 보면 **토막으로 흩어졌다.**
    o.rotation_euler = (0, math.acos(max(-1.0, min(1.0, dz / length))),
                        math.atan2(dy, dx)) if length else (0, 0, 0)
    o.data.materials.append(mat)
    return o


def _ball(c, r, mat, seg=12, ring=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=ring, radius=r * H,
                                         location=tuple(v * H for v in c))
    o = bpy.context.object
    o.data.materials.append(mat)
    return o


def build_mesh(mat):
    """T 포즈 사람 하나. 반환: 합쳐진 메시 오브젝트 `villager`.

    🔴 **마디마다 뼈 하나에 100% 로 묶는다**(강체 결합). 자동 웨이트(`ARMATURE_AUTO`)를
       썼다가 움직이는 순간 **팔다리가 늘어나며 터졌다**(2026-08-16 렌더로 잡았다) — T 포즈는
       팔이 어깨와 같은 높이라 열지도가 팔 정점을 몸통에 물리고, 얇은 원뿔 몸통이라 그 오류가
       그대로 늘어난다. 각진 저폴리 인물은 **부드러운 웨이트가 필요 없고**, 마디가 딱 떨어지는
       쪽이 소품과도 결이 같다.
    🔑 정점 그룹은 **합치기 전에** 각 조각에 달아 둔다 — 합치면 블렌더가 이름으로 병합해 준다."""
    parts = []
    # 몸통 — 골반에서 가슴까지 두 마디(허리가 잘록해야 사람으로 읽힌다)
    parts.append(('hips', _seg((0, 0, P['hip'] - 0.03), (0, 0, P['waist']), R['hip'], R['waist'], mat)))
    parts.append(('spine', _seg((0, 0, P['waist']), (0, 0, P['chest']), R['waist'], R['chest'], mat)))
    parts.append(('neck', _seg((0, 0, P['chest']), (0, 0, P['chin'] - 0.02), R['neck'] * 1.6, R['neck'], mat)))
    parts.append(('head', _ball((0, 0, (P['chin'] + P['crown']) / 2), R['head'], mat)))
    for s_, y in (('L', 1), ('R', -1)):
        sh, ly = P['shoulder'], P['leg_y'] * y
        parts.append((f'shoulder.{s_}', _ball((0, P['shoulder_y'] * y, sh), R['upperarm'] * 1.25, mat)))
        parts.append((f'upperarm.{s_}', _seg((0, P['shoulder_y'] * y, sh), (0, P['elbow_y'] * y, sh),
                                             R['upperarm'], R['forearm'] * 1.05, mat)))
        parts.append((f'forearm.{s_}', _seg((0, P['elbow_y'] * y, sh), (0, P['wrist_y'] * y, sh),
                                            R['forearm'] * 1.05, R['hand'], mat)))
        parts.append((f'hand.{s_}', _seg((0, P['wrist_y'] * y, sh), (0, P['fingertip_y'] * y, sh),
                                         R['hand'], R['hand'] * 0.75, mat, verts=6)))
        parts.append((f'thigh.{s_}', _seg((0, ly, P['hip']), (0, ly, P['knee']),
                                          R['thigh'], R['shin'] * 1.1, mat)))
        parts.append((f'shin.{s_}', _seg((0, ly, P['knee']), (0, ly, P['ankle']),
                                         R['shin'] * 1.1, R['foot'] * 0.8, mat)))
        # 발 — 앞으로 뻗은 납작한 마디. 없으면 다리가 막대로 끝나 서 있는 느낌이 안 난다
        parts.append((f'foot.{s_}', _seg((0, ly, P['ankle']), (-0.085, ly, 0.012),
                                         R['foot'] * 0.8, R['foot'] * 0.55, mat, verts=6)))

    # 조각마다 제 뼈 이름의 정점 그룹을 100% 로 단다 (합치면 이름으로 병합된다)
    for bone, obj in parts:
        vg = obj.vertex_groups.new(name=bone)
        vg.add(list(range(len(obj.data.vertices))), 1.0, 'REPLACE')

    bpy.ops.object.select_all(action='DESELECT')
    for _b, p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0][1]
    bpy.ops.object.join()
    m = bpy.context.object
    m.name = 'villager'
    m.data.name = 'villager'
    # 🔴 플랫 셰이딩 — 소품과 같은 결. 스무스로 두면 사람만 매끈해서 따로 논다
    for poly in m.data.polygons:
        poly.use_smooth = False
    return m


def build_rig(mesh):
    bpy.ops.object.armature_add(location=(0, 0, 0))
    arm = bpy.context.object
    arm.name = 'villager_rig'
    arm.data.name = 'villager_rig'
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm.data.edit_bones
    for b in list(eb):
        eb.remove(b)
    made = {}
    for name, head, tail, parent in bones():
        b = eb.new(name)
        b.head = tuple(v * H for v in head)
        b.tail = tuple(v * H for v in tail)
        b.roll = 0.0
        if parent:
            b.parent = made[parent]
            b.use_connect = False
        made[name] = b
    bpy.ops.object.mode_set(mode='OBJECT')

    # 🔴 **`ARMATURE_AUTO` 가 아니다.** 메시가 이미 뼈 이름 그룹을 들고 있으므로 그대로 쓴다 —
    #    자동 웨이트는 T 포즈 팔을 몸통에 물려 터뜨렸다(build_mesh 주석).
    bpy.ops.object.select_all(action='DESELECT')
    mesh.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.parent_set(type='ARMATURE_NAME')
    return arm


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    # 🔴 알베도는 **선형**이다 — 마을 벽 0.085 · 눈 0.38 이 같은 자에 있다.
    #    0.80 을 주면 화면이 타서 실루엣이 사라진다(첫 판이 그랬다).
    mat = stage.world_mat((0.16, 0.16, 0.17))
    mesh = build_mesh(mat)
    arm = build_rig(mesh)
    missing = [n for n, *_ in bones() if n not in {g.name for g in mesh.vertex_groups}]
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=OUT)
    print(f'✅ villager2 — 키 {H} m · 뼈 {len(bones())} · 면 {len(mesh.data.polygons)} → {OUT}')
    if missing:
        print(f'  ⚠️ 웨이트가 안 붙은 뼈: {missing}')


if __name__ == '__main__':
    main()
