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

## 이 스크립트가 **안** 하는 것

- `@root`(골반 이동): 이번 파일럿은 제자리 동작(`shoot`)으로 판정한다. 정면 축을 화면으로
  확인하기 전에 부호를 넣으면 이 프로젝트가 네 번 헛돈 그 자리다(`stage.pose` 주석).
  `--root` 를 주면 뽑되, **기본은 끈다.**
- `@squash`: 모캡에는 그 층이 없다. 합성은 양념 단계(`mocap.py`)의 몫이다.
"""
import json
import math
import os
import sys

import bpy
from mathutils import Quaternion

DIR = os.path.dirname(os.path.abspath(__file__))
MOCAP = os.path.join(DIR, 'mocap')
OUT_ROOT = os.environ.get('SCENE_3D_ROOT', r'D:\AI_GOAP-videos\3d')

# 🔴 `stage.py` 는 `models/villager_rigged.blend` 를 보는데 이 기기의 산출물은 `3d/` 바로
#    아래 있다. 산출물 경로는 기기마다 다를 수 있으므로 **찾아서** 쓴다 — 한 곳만 적으면
#    다른 기기에서 조용히 실패한다.
RIG_BLEND = next(
    (p for p in (os.path.join(OUT_ROOT, 'models', 'villager_rigged.blend'),
                 os.path.join(OUT_ROOT, 'villager_rigged.blend'))
     if os.path.exists(p)), None)

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


def our_rest():
    """우리 리그의 레스트 월드 회전 {뼈: Quaternion} — 아마추어 공간 기준."""
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


def bake(name, want_root=False):
    clear_scene()
    arm_ours, rest = our_rest()
    src = load_fbx(os.path.join(MOCAP, f'{name}.fbx'))

    scn = bpy.context.scene
    f0, f1 = int(scn.frame_start), int(scn.frame_end)
    if src.animation_data and src.animation_data.action:
        a0, a1 = src.animation_data.action.frame_range
        f0, f1 = int(a0), int(a1)
    fps = scn.render.fps

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

    for f in range(f0, f1 + 1):
        scn.frame_set(f)
        world = {}                                    # 우리 리그의 이번 프레임 월드 회전
        for n in bones:
            pb = src.pose.bones[BONE_MAP[n]]
            # 포즈도 같은 월드 축으로 (윗글 — `objq` 켤레). m_rest 는 이미 옮겨져 있다.
            d = facing @ (objq @ pb.matrix.to_quaternion()) @ m_rest[n].inverted()
            w = d @ rest[n]                                        # 우리 레스트에 먹인다
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

        if want_root:
            pb = src.pose.bones[BONE_MAP['hips']]
            roots.append(tuple(pb.matrix.translation - src.data.bones[
                BONE_MAP['hips']].matrix_local.translation))

    for n in bones:                                   # 채널마다 감김을 편다
        raw[n] = [unwrap(ch) for ch in raw[n]]

    frames = f1 - f0 + 1
    data = [{n: [raw[n][0][i], raw[n][1][i], raw[n][2][i]] for n in bones}
            for i in range(frames)]

    out = {'name': name, 'fps': fps, 'frames': frames, 'bones': bones, 'data': data}
    if want_root:
        # 우리 리그 크기로 환산 — 매직넘버 대신 **골반 높이 비**로 잰다.
        ours_h = arm_ours.data.bones['hips'].matrix_local.translation.z or 0.300
        mixa_h = src.data.bones[BONE_MAP['hips']].matrix_local.translation.z or 1.0
        out['rootScale'] = ours_h / mixa_h
        out['rootRaw'] = roots

    path = os.path.join(MOCAP, f'{name}.pose.json')
    with open(path, 'w', encoding='utf-8') as fp:
        json.dump(out, fp)
    print(f'✅ {name}: {frames}프레임 @{fps}fps · 뼈 {len(bones)}/{len(BONE_MAP)} → '
          f'{os.path.basename(path)}')
    return out


if __name__ == '__main__':
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    want_root = '--root' in argv
    names = [a for a in argv if not a.startswith('--')] or ['shoot']
    for n in names:
        bake(n, want_root)
