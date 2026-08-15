"""Mixamo FBX → 우리 포즈 표 (M32 영상 트랙 · 2026-08-16).

    blender --background --python mocap2pose.py -- shoot flinch limp

읽는 것: `mocap/<이름>.fbx` (Mixamo, **Without Skin** 권장)
쓰는 것: `mocap/<이름>.pose.json` — `motions.MOTIONS` 와 **같은 계약**의 표
         `{fps, frames, bones, data[frame][bone] = (x, y, z) 라디안}`

## 왜 「월드 델타」인가 (단순 복사가 안 되는 이유)

두 리그의 **레스트 포즈가 다르다.** Mixamo 는 T 포즈(팔이 수평)고 우리 리그는 팔을 내리고
있다(`rig.py`: upperarm head 0.575 → tail 0.440 — 아래로 내려간다). 그래서 포즈 뼈의
`rotation_euler`(= 레스트 대비 로컬 회전)를 그대로 베끼면 **팔이 90° 틀어진 채로 움직인다.**

대신 뼈의 **월드 방향 변화량**만 옮긴다:

    D  = (미맥소 포즈 월드) · (미맥소 레스트 월드)⁻¹      ← 이 뼈가 얼마나 돌았나
    W  = D · (우리 레스트 월드)                          ← 그만큼을 우리 레스트에 먹인다
    basis = (W_부모 · L_레스트)⁻¹ · W                    ← 블렌더가 먹는 로컬 회전

🔴 **부모부터 순서대로** 푼다. `W_부모` 가 이미 포즈된 값이라, 자식이 부모 회전을 두 번
   먹는 사고(척추를 돌렸는데 목이 두 배로 돌아가는 것)가 구조적으로 안 난다.
🔑 회전만 다룬다(쿼터니언). 스케일·이동이 섞이면 리그 크기 차이(우리 0.95m vs 모캡 1.7m)가
   회전에 새어 들어온다.

## 척추 3 → 1

Mixamo 는 Spine/Spine1/Spine2 셋이고 우리는 `spine` 하나다. **Spine2 의 월드 델타**를 쓴다 —
그것이 가슴의 누적 방향이고, 우리 `spine` 은 골반과 목 사이의 유일한 뼈라 그 방향을 그대로
져야 한다. (위의 부모 순서 계산이 골반 몫을 자동으로 빼 준다.)

## `@root` (골반 이동) — 없으면 사람이 넘어진다

첫 파일럿을 골반 이동 없이 굽고 「부호를 화면으로 확인하기 전에는 안 넣는다」고 미뤘는데,
**그 판단이 반쯤 틀렸다**(2026-08-16). 모캡의 다리 회전은 **골반이 같이 이동하는 것을
전제**한다 — 골반을 못에 박고 다리 회전만 살리면 몸이 그 못을 축으로 앞으로 고꾸라진다.
`@root` 는 선택이 아니라 **모캡이 서 있기 위한 조건**이다.

축은 추측하지 않는다. `stage.pose` 가 실측해 적어 둔 것을 그대로 쓴다 —
**앞 = 월드 −X · 위 = +Z · 왼쪽 = +Y**, 그리고 `@root` 는 `(앞, 위, 왼쪽)` 미터다.

🔑 **수평 드리프트는 뺀다** (`INPLACE`). 걷는 클립은 골반이 앞으로 계속 나아가는데, 우리
   `walk` 은 제자리에서 흔들리고 **이동은 무대의 몫**이다(`MoveMotion`). 그대로 넣으면 둘이
   싸우고, 루프 이음매에서 순간이동한다. 뺀 총 이동량은 `travel` 로 남긴다.
🔴 한 번 튀는 이동(`flinch` 의 밀림)은 **빼면 안 된다** — 그건 드리프트가 아니라 사건이다.
   그래서 자동 판별이 아니라 **클립마다 적는다.**

## 이 스크립트가 **안** 하는 것

- `@squash`: 모캡에는 그 층이 없다. 합성은 양념 단계(`mocap.py`)의 몫이다.
"""
import json
import math
import os
import sys

import bpy
from mathutils import Vector

# 수평 이동을 뺄 클립 (윗글). 🔑 자동 판별을 안 쓰는 이유: 「많이 움직이면 걷기」 같은
# 문턱은 `flinch` 의 밀림을 언젠가 삼킨다. 결정은 사람이 적는다.
INPLACE = {'limp': True}

# 쓸 구간 (클립 시작 0 기준 프레임, 양끝 포함). 🔴 모캡은 **한 동작이 아니라 한 연기**다 —
# 준비·연결·마무리가 다 들어 있어서 그대로 쓰면 우리가 안 부른 몸짓이 화면에 나온다.
#
# `shoot` 실측 (2026-08-16, 채널 프로파일로 읽었다):
#   0~14 대기 · 15~30 1차 당김·놓음 · **35~55 화살 뽑기**(`spine.X` 가 +18.7° 까지 굽는다 —
#   파일럿에서 「사람이 앞으로 굽는다」로 보인 것이 이 구간이었다) · 58~75 2차 당김 ·
#   80~125 **겨눔 홀드**(`forearm.R` −122~−128 유지) · 130~142 놓음 · ~150 대기 복귀.
#   → 당김~놓음만 남긴다.
# ⚠️ 이 구간은 **양 끝의 포즈가 다르다**(`forearm.R` 시작 −72° 대 끝 −42°). 그래서 `loop=True`
#    로 반복하면 이음매가 한 번 튄다. 연사는 `loop=False` 로 **매 발 다시 부르는** 것이 맞다 —
#    화살은 한 발이 한 사건이다.
TRIM = {'shoot': (58, 145)}

DIR = os.path.dirname(os.path.abspath(__file__))
MOCAP = os.path.join(DIR, 'mocap')
OUT_ROOT = os.environ.get('SCENE_3D_ROOT', r'D:\AI_GOAP-videos\3d')

# 🔴 `stage.py` 는 `models/villager_rigged.blend` 를 보는데 이 기기의 산출물은 `3d/` 바로
#    아래 있다. 산출물 경로는 기기마다 다를 수 있으므로 **찾아서** 쓴다 — 한 곳만 적으면
#    다른 기기에서 조용히 실패한다.
RIG_BLEND = os.environ.get('RIG_BLEND') or next(
    (p for p in (os.path.join(OUT_ROOT, 'models', 'villager_rigged.blend'),
                 os.path.join(OUT_ROOT, 'villager_rigged.blend'))
     if os.path.exists(p)), None)

# 🔑 **T 포즈 리그면 팔 예외가 필요 없다.** `ARM_CHAIN`(기준면 D)은 레스트가 팔을 내린
#    옛 모델을 위한 보정이었다 — 레스트가 Mixamo 와 같아지면 기준면 A 가 그대로 맞는다.
#    `MOCAP_BASE=A` 로 끈다. 예외가 필요 없어지는 것이 새 모델의 값이다.
BASE_MODE = os.environ.get('MOCAP_BASE', 'D').upper()

# 우리 뼈 ← Mixamo 뼈. 🔑 `rig.py` 의 18뼈 중 **매핑되는 것만** 적는다 —
# 안 적힌 뼈는 표에서 빠지고 `stage.pose` 가 0 으로 되돌린다(중립).
BONE_MAP = {
    'hips':        'mixamorig:Hips',
    'spine':       'mixamorig:Spine2',   # 3 → 1 (윗글 참조)
    'neck':        'mixamorig:Neck',
    'head':        'mixamorig:Head',
    'shoulder.L':  'mixamorig:LeftShoulder',
    'upperarm.L':  'mixamorig:LeftArm',
    'forearm.L':   'mixamorig:LeftForeArm',
    'hand.L':      'mixamorig:LeftHand',
    'shoulder.R':  'mixamorig:RightShoulder',
    'upperarm.R':  'mixamorig:RightArm',
    'forearm.R':   'mixamorig:RightForeArm',
    'hand.R':      'mixamorig:RightHand',
    'thigh.L':     'mixamorig:LeftUpLeg',
    'shin.L':      'mixamorig:LeftLeg',
    'foot.L':      'mixamorig:LeftFoot',
    'thigh.R':     'mixamorig:RightUpLeg',
    'shin.R':      'mixamorig:RightLeg',
    'foot.R':      'mixamorig:RightFoot',
}

# 부모부터 푸는 순서 (rig.py 의 계층 그대로). 🔴 이 순서가 곧 정확성이다 — 섞으면
# 자식이 부모의 옛 월드값을 본다.
ORDER = ['hips', 'spine', 'neck', 'head',
         'shoulder.L', 'upperarm.L', 'forearm.L', 'hand.L',
         'shoulder.R', 'upperarm.R', 'forearm.R', 'hand.R',
         'thigh.L', 'shin.L', 'foot.L',
         'thigh.R', 'shin.R', 'foot.R']

PARENT = {'spine': 'hips', 'neck': 'spine', 'head': 'neck',
          'shoulder.L': 'spine', 'upperarm.L': 'shoulder.L',
          'forearm.L': 'upperarm.L', 'hand.L': 'forearm.L',
          'shoulder.R': 'spine', 'upperarm.R': 'shoulder.R',
          'forearm.R': 'upperarm.R', 'hand.R': 'forearm.R',
          'thigh.L': 'hips', 'shin.L': 'thigh.L', 'foot.L': 'shin.L',
          'thigh.R': 'hips', 'shin.R': 'thigh.R', 'foot.R': 'shin.R'}


def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for blk in (bpy.data.armatures, bpy.data.meshes, bpy.data.actions):
        for d in list(blk):
            blk.remove(d)


# 🔴 **T 포즈 기준면** (2026-08-16 세 번째 파일럿이 잡았다 — 이게 팔이 안 올라간 진짜 원인).
#
# 월드-델타는 「레스트로부터의 변화량」을 옮긴다. 그런데 두 리그의 레스트가 팔에서
# **80° 가까이 다르다** — Mixamo 는 T 포즈(팔 수평)고 우리는 팔을 내리고 있다.
# 아래로 향한 팔을 수직축으로 돌리면 **여전히 아래를 향한다.** 그래서 활 자세의 델타를
# 그대로 먹여도 팔이 43° 밖에 안 올라갔다(`upperarm.L` 쉴 때 −67.5° → 당길 때 −24°).
#
# 🔑 다리는 멀쩡했던 이유가 이걸 증명한다: 두 리그 모두 다리가 **아래로** 뻗어 레스트가
#    같다. 그래서 `limp` 는 첫 판부터 읽혔고 `shoot` 만 안 읽혔다.
#
# 🔴 **기준면 셋을 다 굽어 보고 A 를 쓴다** (2026-08-16 실측 — 화면으로 판정했다):
#
#   A `W = D · (우리 레스트)`   ← **채택.** `limp` 이 읽힌다. `shoot` 은 팔이 43° 모자라다
#   B `W = D · (T 포즈 방향)`   ← 기각. `shoot` 은 좋아졌는데 **`limp` 이 망가졌다** —
#       `rotation_difference` 가 방향만 맞추고 **뼈 축 둘레 뒤틀림(roll)을 임의로 남겨서**
#       걸을 때 팔이 옆으로 흔들리지 않고 앞으로 뻗었다
#   C `W = (모캡 포즈 월드)`    ← 기각. **전신이 망가진다.** 두 리그의 레스트 뒤틀림 규약이
#       달라 절대 방향을 강제하면 다리가 벌어지고 발이 뜬다
#
# 🔑 그래서 **D 를 쓴다: 팔 사슬만 C, 나머지는 A.**
#
#   C 가 통째로 망가진 것은 **다리·척추**였다(레스트 뒤틀림 규약이 달라 발이 뜬다). 그런데
#   **팔에서는 C 가 정확히 우리가 원하는 T 포즈 기준면**이다 — 모캡 레스트가 곧 T 포즈고,
#   뒤틀림이 애니메이션과 **같은 출처**에서 오므로 B 의 roll 임의성이 원천 봉쇄된다.
#
# 🔴 리그 픽스처(`villager_rigged.blend`)에 손으로 T 포즈를 저장하지 않는 이유: 그 파일은
#    `rig.py` 가 만드는 **산출물**이라(`stage.py` 주석 — "산출물이다 — D 드라이브") 다음
#    재생성에 지워진다. 기준면은 코드 쪽에 있어야 살아남는다.
ARM_CHAIN = {'shoulder.L', 'upperarm.L', 'forearm.L', 'hand.L',
             'shoulder.R', 'upperarm.R', 'forearm.R', 'hand.R'}


def our_rest():
    """우리 리그의 레스트 월드 회전 {뼈: Quaternion} — 아마추어 공간.

    로컬 basis 를 역산할 때 쓴다. 목표 월드 방향은 모캡 쪽에서 온다(윗글)."""
    with bpy.data.libraries.load(RIG_BLEND, link=False) as (_s, dst):
        dst.objects = ['villager_rig']
    arm = next(o for o in dst.objects if o.type == 'ARMATURE')
    bpy.context.collection.objects.link(arm)
    rest = {n: arm.data.bones[n].matrix_local.to_quaternion() for n in ORDER}
    return arm, rest


def load_fbx(path):
    """Mixamo FBX 하나를 들여와 아마추어를 돌려준다."""
    bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False)
    arms = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE'
            and any(b.name.startswith('mixamorig') for b in o.data.bones)]
    if not arms:
        raise RuntimeError(f'{os.path.basename(path)}: mixamorig 아마추어가 없다')
    return arms[0]


def unwrap(seq):
    """오일러 한 채널의 ±π 감김을 편다.

    🔴 왜 필요한가: 재생기가 프레임 사이를 **선형 보간**한다. +3.10 → −3.10 은 실제로는
    0.08 라디안 움직인 것인데 편 채로 두지 않으면 보간이 반대로 6.2 라디안을 돈다 —
    팔이 한 바퀴 휘두르는 사고가 그것이다."""
    out, off = [], 0.0
    for i, v in enumerate(seq):
        if i:
            d = (v + off) - out[-1]
            if d > math.pi:
                off -= 2 * math.pi
            elif d < -math.pi:
                off += 2 * math.pi
        out.append(v + off)
    return out


def bake(name):
    clear_scene()
    arm_ours, rest = our_rest()
    src = load_fbx(os.path.join(MOCAP, f'{name}.fbx'))

    scn = bpy.context.scene
    f0, f1 = int(scn.frame_start), int(scn.frame_end)
    if src.animation_data and src.animation_data.action:
        a0, a1 = src.animation_data.action.frame_range
        f0, f1 = int(a0), int(a1)
    fps = scn.render.fps

    # 구간 자르기 — 클립 시작을 0 으로 본 프레임 번호다(윗글 `TRIM`). 범위를 벗어나면
    # 조용히 통째로 쓴다: 클립을 바꿔 끼웠을 때 옛 번호가 엉뚱한 구간을 자르는 것보다
    # 전부 나오는 쪽이 눈에 띈다.
    trim = TRIM.get(name)
    if trim:
        clip0 = f0          # 🔴 `base`(T 포즈 기준면 딕셔너리)와 이름이 겹치면 안 된다
        a, b = clip0 + trim[0], clip0 + trim[1]
        if f0 <= a < b <= f1:
            f0, f1 = a, b
        else:
            print(f'  ⚠️ {name}: 구간 {trim} 이 클립 밖이다 — 통째로 굽는다')
            trim = None

    # 🔴 **월드 축으로 맞춘다** (2026-08-16 파일럿이 화면으로 잡았다).
    #    Mixamo FBX 는 아마추어 객체에 −90° X 를 달고 들어온다(Y-up → Z-up 환산). 그래서
    #    `pb.matrix`·`bone.matrix_local` 이 사는 **아마추어 공간의 위쪽이 우리와 다르다.**
    #    그 공간에서 잰 델타를 우리 공간에 그대로 먹이면 저쪽의 「앞으로 숙임」이 우리 쪽
    #    「위로 젖힘」이 된다 — 첫 파일럿에서 사람이 통째로 90° 누워 나온 원인이 이것이다.
    #    객체 회전으로 켤레(conjugate)를 취해 델타를 월드 축으로 옮긴다. 우리 리그는
    #    아마추어가 단위행렬이라 월드 = 우리 아마추어 공간이다.
    objq = src.matrix_world.to_quaternion()

    m_rest = {}
    for ours, mixa in BONE_MAP.items():
        if mixa not in src.data.bones:
            print(f'  ⚠️ 없는 뼈 건너뜀: {mixa} ({ours})')
            continue
        m_rest[ours] = objq @ src.data.bones[mixa].matrix_local.to_quaternion()

    bones = [n for n in ORDER if n in m_rest]
    raw = {n: ([], [], []) for n in bones}
    roots = []

    # 🔴 **클립의 정면을 뺀다** (2026-08-16 두 번째 파일럿이 수로 잡았다: `hips` 가 174° 를
    #    흔들고 있었다). 궁수는 표적에 **옆으로 선다** — 그 상수 요각이 골반 델타에 통째로
    #    실려 몸 전체를 휘두르고, 그러면 자세가 뭉개져 「쏜다」가 안 읽힌다.
    #    🔑 우리 규약에서 **정면은 동작이 아니라 무대의 몫**이다(`stage.rigged(rot_z=…)`).
    #       그래서 첫 프레임 골반 델타를 모든 뼈에서 빼 클립을 「0초에 정면」으로 맞춘다.
    #       빼는 것은 공통 회전 하나뿐이라 뼈 사이의 상대 자세는 그대로 남는다.
    scn.frame_set(f0)
    hips_pb = src.pose.bones[BONE_MAP['hips']]
    facing = ((objq @ hips_pb.matrix.to_quaternion())
              @ m_rest['hips'].inverted()).inverted()
    hips_rest_world = (src.matrix_world
                       @ src.data.bones[BONE_MAP['hips']].matrix_local).translation

    for f in range(f0, f1 + 1):
        scn.frame_set(f)
        world = {}                                    # 우리 리그의 이번 프레임 월드 회전
        for n in bones:
            pb = src.pose.bones[BONE_MAP[n]]
            # 포즈도 같은 월드 축으로 (윗글 — `objq` 켤레). m_rest 는 이미 옮겨져 있다.
            d = facing @ (objq @ pb.matrix.to_quaternion()) @ m_rest[n].inverted()
            # 기준면 D — 팔은 모캡 레스트(= T 포즈), 나머지는 우리 레스트 (윗글)
            w = d @ (m_rest[n] if (BASE_MODE == 'D' and n in ARM_CHAIN) else rest[n])
            world[n] = w
            p = PARENT.get(n)
            if p is None:                             # hips — 부모가 아마추어 자신
                basis = rest[n].inverted() @ w
            else:
                l_rest = rest[p].inverted() @ rest[n]              # 부모 대비 레스트
                basis = (world[p] @ l_rest).inverted() @ w
            e = basis.to_euler('XYZ')
            for i in range(3):
                raw[n][i].append(e[i])

        # 골반 이동 — 월드 좌표에서 재고(객체 스케일까지 먹은 `matrix_world`), 회전과
        # **같은 `facing` 켤레**를 벡터에도 먹인다. 안 그러면 자세만 정면을 보고 이동은
        # 옆으로 간다.
        hp = (src.matrix_world @ src.pose.bones[BONE_MAP['hips']].matrix).translation
        roots.append(facing @ (hp - hips_rest_world))

    for n in bones:                                   # 채널마다 감김을 편다
        raw[n] = [unwrap(ch) for ch in raw[n]]

    frames = f1 - f0 + 1

    # 우리 리그 크기로 환산 — 매직넘버 대신 **골반 높이 비**로 잰다(모캡 단위가 m 인지
    # cm 인지, 임포트 배율이 얼마인지 몰라도 맞는다).
    ours_h = arm_ours.data.bones['hips'].matrix_local.translation.z or 0.300
    scale = ours_h / (hips_rest_world.z or 1.0)
    roots = [v * scale for v in roots]

    travel = 0.0
    if INPLACE.get(name):
        # ①선형 램프 제거(첫→끝 이동) ②남은 상수 치우침 제거. 수평만 — 위아래 출렁임은
        # 걸음의 무게라 남긴다.
        d = roots[-1] - roots[0]
        travel = math.hypot(d.x, d.y)
        for i in range(frames):
            k = i / (frames - 1) if frames > 1 else 0.0
            roots[i] = Vector((roots[i].x - d.x * k, roots[i].y - d.y * k, roots[i].z))
        mx = sum(v.x for v in roots) / frames
        my = sum(v.y for v in roots) / frames
        roots = [Vector((v.x - mx, v.y - my, v.z)) for v in roots]

    # 🔴 축은 `stage.pose` 가 실측해 둔 것 그대로: 앞 = 월드 −X · 위 = +Z · 왼쪽 = +Y.
    #    `@root` 는 (앞, 위, 왼쪽) 미터다.
    data = []
    for i in range(frames):
        e = {n: [raw[n][0][i], raw[n][1][i], raw[n][2][i]] for n in bones}
        v = roots[i]
        e['@root'] = [-v.x, v.z, v.y]
        data.append(e)

    out = {'name': name, 'fps': fps, 'frames': frames, 'bones': bones,
           'rootScale': scale, 'inplace': bool(INPLACE.get(name)),
           'trim': list(trim) if trim else None,
           'travel': travel, 'data': data}

    path = os.path.join(MOCAP, f'{name}.pose.json')
    with open(path, 'w', encoding='utf-8') as fp:
        json.dump(out, fp)
    rmax = max(max(abs(c) for c in d['@root']) for d in data)
    print(f'✅ {name}: {frames}프레임 @{fps}fps · 뼈 {len(bones)}/{len(BONE_MAP)} · '
          f'배율 {scale:.3f} · @root 최대 {rmax:.3f}m' +
          (f' · 뺀 이동 {travel:.2f}m' if travel else '') +
          f' → {os.path.basename(path)}')
    return out


if __name__ == '__main__':
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    names = [a for a in argv if not a.startswith('--')] or ['shoot']
    for n in names:
        bake(n)
