"""Mixamo 네이티브 — 캐릭터도 동작도 Mixamo 것을 그대로 쓴다 (M32 영상 트랙 · 2026-08-16).

    blender --background --python mixamo.py -- build      # 한 번 굽는다
    # 렌더 스크립트 안에서
    import mixamo
    mesh, arm = mixamo.spawn(loc=(0, 0, 0), rot_z=90)
    mixamo.play(arm, 'walk', t, dur=1.0)

## 왜 리타기팅이 없나

캐릭터와 동작이 **같은 뼈대**(`mixamorig:*`)를 쓴다. 옮길 것이 없으니 옮기는 코드도 없다.
앞 판이 겪은 것 — 오일러 짐벌 · roll 임의값 · 레스트 차이 43° · 자동 웨이트 폭발 —
은 전부 「남의 뼈대를 우리 뼈대로 옮긴다」에서 나온 문제였고, 그 전제를 지우면 같이 사라진다.

## 왜 씬 프레임을 안 쓰나 (fcurve 를 직접 읽는 이유)

`scene.frame_set()` 은 **전역**이다. 주민 여섯이 서로 다른 시간에 서 있어야 하는데
(갈라지는 장면이 이 트랙의 사건이다) 전역 프레임 하나로는 못 한다.
그래서 액션의 fcurve 를 뼈마다 직접 평가한다 — 아마추어별로 시간이 따로 흐른다.
🔑 이게 옛 `motions.py` 의 `f(t)` 계약을 그대로 잇는다. 대본이 길이를 정하는 규약이 산다.

## 산출물

`<SCENE_3D_ROOT>/models/ybot.blend` — 캐릭터 하나 + 액션 전부(가짜 사용자로 박제).
🔴 `.fbx` 는 gitignore 다 (Mixamo 재배포 금지). 각자 받아서 각자 굽는다.
"""
import math
import os
import re
import sys

DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, DIR)      # `--python mixamo.py` 로 직접 돌 때 옆 모듈이 안 보인다
SRC = os.path.join(DIR, 'mixamo')                 # 받아 온 원본 FBX
CLIPS = os.path.join(SRC, 'clips')
OUT_ROOT = os.environ.get('SCENE_3D_ROOT', r'D:\AI_GOAP-videos\3d')
BLEND = os.path.join(OUT_ROOT, 'models', 'ybot.blend')

CHAR = os.environ.get('MIXAMO_CHAR', os.path.join(SRC, 'ybot.fbx'))

# 🔴 키를 **굽는 쪽에서 고정**한다. Mixamo FBX 는 기기·내보내기 설정에 따라 cm(100배)로
#    들어오기도 한다 — 재서 맞추지 않으면 마을 크기가 기기마다 달라진다.
H = 1.70

ARM_NAME = 'villager_rig'      # 굽고 나서 붙이는 이름. 렌더 스크립트가 이 이름만 안다
MESH_NAME = 'villager'
PREFIX = 'mixamorig:'
HIPS = PREFIX + 'Hips'

# 쓸 구간 (클립 시작 1 기준 프레임, 양끝 포함). 🔴 모캡은 **한 동작이 아니라 한 연기**다 —
# 준비·연결·마무리가 다 들어 있어서 그대로 쓰면 우리가 안 부른 몸짓이 화면에 나온다.
# `shoot` 실측: 35~55 가 **화살 뽑기**라 판정 그림에서 「사람이 앞으로 굽는다」로 보였다.
# ⚠️ 이 구간은 양 끝 포즈가 달라서 `loop=True` 면 이음매가 한 번 튄다 — 화살은 한 발이
#    한 사건이니 `loop=False` 로 매 발 다시 부르는 것이 맞다.
TRIM = {'shoot': (58, 145)}

# 받은 파일 이름 → 우리 동작 이름. 🔑 Mixamo 는 「Breathing Idle.fbx」처럼 내려주고,
# 사람에게 매번 이름을 바꾸게 하면 언젠가 한 번은 안 바꾼다. 여기서 받아 준다.
# 🔴 소문자로 비교하고 `(1)` 같은 중복 내려받기 꼬리는 떼고 본다.
# 🔴 여기 없는 이름은 **파일 이름 그대로** 동작 이름이 된다 — 조용히 고쳐 주지 않는다.
#    잘못 넣은 클립이 다른 동작 행세를 하는 것이 이름이 안 맞는 것보다 나쁘다.
ALIAS = {
    'breathing idle': 'idle', 'idle': 'idle', 'standing idle': 'idle',
    'walking': 'walk', 'injured walking': 'limp', 'injured walk': 'limp',
    'running': 'run',
    'sword and shield slash': 'attack', 'great sword slash': 'attack',
    'standing melee attack downward': 'chop',
    'falling back death': 'death', 'standing death backward 01': 'death',
    'standing death forward 01': 'death',
    'standing react small from front': 'hit',
    'sword and shield block': 'block',
    'standing draw arrow': 'shoot', 'standing aim overdraw': 'shoot',
    'picking up': 'pick', 'sitting idle': 'sit',
    # 일상 — 마을이 무언가를 하고 있다는 것을 보이는 몸짓들
    'standing melee attack downward': 'work',   # 🔑 도끼·곡괭이·망치 **셋을 다 덮는다**
    'talking': 'talk', 'waving': 'wave',
    'looking around': 'look', 'look around': 'look',
    'watering': 'water',        # 🔑 Mixamo 에 **있다**. 없을 거라고 적었던 것이 틀렸다
    'sitting': 'sit', 'crouching': 'tend', 'crouch to stand': 'tend',
    'walking with a bag': 'carry', 'carrying': 'carry',
}


def clip_name(filename):
    """`mixamo/clips/<파일>.fbx` → 동작 이름."""
    stem = os.path.splitext(os.path.basename(filename))[0]
    stem = re.sub(r'\s*\(\d+\)\s*$', '', stem).strip()      # 「Running (1)」의 꼬리
    return ALIAS.get(stem.lower(), stem)

# 이 이상 골반이 수평으로 나아가면 「In Place 를 안 켰다」로 보고 경보한다(미터).
# 🔑 제자리 클립도 골반이 좌우로 흔들리므로 0 이 아니다 — 걸음 하나가 넘는 값으로 잡는다.
DRIFT_WARN = 0.30

# 🔴 **이동이 사건인 클립.** 여기 있으면 경보를 안 낸다.
#    넘어지는 사람은 넘어진 자리로 가야 하고(`death` 1.29m), 맞은 사람은 밀려야 한다.
#    그걸 「In Place 를 안 켰다」로 잡으면 사람이 못에 박힌 채 뒤로 눕는다.
# 🔑 자동 판별을 안 쓴다. 「많이 움직이면 걷기」 같은 문턱은 언젠가 이 둘을 삼킨다 —
#    걷기와 넘어짐은 **거리로는 구별이 안 된다.** 결정은 사람이 적는다.
MOVES = {'death', 'flinch', 'hit', 'attack'}
#         ^ `attack` 은 0.47m 나아가는데 그건 **찌르고 들어가는 발**이다(지면 속도는 0.00 m/s).
#           In Place 로 다시 받으면 그 발이 사라져 타격이 물러진다.


# 게임 `AnimKind` → (쓸 클립, 손에 쥘 도구). 🔑 **몸짓 하나에 도구 셋**이다 —
# 도끼질·곡괭이질·망치질은 같은 「내려치기」고, 무슨 일인지는 **손에 든 것이 말한다.**
# 그래서 클립을 셋 받지 않는다. 도구는 `village.axe()` 등으로 이미 다 있다.
# 🔴 이름을 게임과 갈라 놓지 마라 — 갈리면 같은 몸짓을 게임과 영상이 두 이름으로 부른다.
#    정본: `Assets/Scripts/M0/Data/ActionSO.cs` 의 `AnimKind`.
JOB = {
    'Chop':   ('work', 'axe'),
    'Mine':   ('work', 'pickaxe'),
    'Hammer': ('work', 'hammer'),
    'Water':  ('water', 'wateringcan'),
    'Attack': ('attack', 'sword'),
}


# ── 굽기 (블렌더 안에서만) ─────────────────────────────
def _import(path):
    """FBX 하나를 빈 씬에 들여온다. 반환 (armature, [mesh...])."""
    import bpy
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False)
    new = [o for o in bpy.data.objects if o not in before]
    arm = next(o for o in new if o.type == 'ARMATURE')
    return arm, [o for o in new if o.type == 'MESH']


def _normalize(arm, meshes):
    """캐릭터를 세우고(회전만 구워 넣는다) 키를 H 로 맞춘다.

    🔴 **스케일은 구워 넣지 마라.** 포즈 뼈의 `location`(= 골반 이동)은 아마추어 공간
       단위인데, 액션의 키 값은 안 따라 바뀐다 — 뼈만 100배가 되고 이동 키는 그대로라
       걸음마다 사람이 3 m 씩 튄다(실측: `hips` 가 −3.72 로 읽혔다).
       회전은 안전하다. 포즈 채널이 **뼈 로컬**이라 레스트가 같이 돌면 뜻이 안 변한다.
    🔑 그래서 이 함수가 남기는 것은 **객체 스케일 하나**다. 런타임은 그 값을 안 건드린다."""
    import bpy
    bpy.ops.object.select_all(action='DESELECT')
    for o in [arm] + meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    bpy.context.view_layer.update()

    # 🔴 메시가 있으면 **메시로 잰다.** 뼈 끝은 실루엣 밖으로 삐져나온다 —
    #    뼈로 재면 (`HeadTop_End` 등) 키가 과대평가돼 사람이 마을보다 작아진다.
    #    실측: 스킨 없는 클립 뼈대는 뼈 끝이 2.12, 눈에 보이는 키는 그보다 낮다.
    top = (max((m.matrix_world @ v.co).z for m in meshes for v in m.data.vertices)
           if meshes else
           max((arm.matrix_world @ b.tail_local).z for b in arm.data.bones))
    k = H / top
    arm.scale = tuple(s * k for s in arm.scale)
    arm.location = (0.0, 0.0, 0.0)
    bpy.context.view_layer.update()
    return H


def _paint(meshes):
    """Y Bot 은 **청록색**으로 온다. 그 색이 화면에 들어오면 뜻층 다섯과 경쟁한다
    (색 = 뜻이 이 트랙의 계약이다). 세계층 무채색으로 다시 칠한다 — 값은 palette 가 갖는다."""
    import bpy
    import palette
    mat = bpy.data.materials.new('villager_body')
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes['Principled BSDF']
    bsdf.inputs['Base Color'].default_value = (*palette.FIGURE_LIN, 1.0)
    bsdf.inputs['Roughness'].default_value = palette.FIGURE_ROUGHNESS
    for m in meshes:
        m.data.materials.clear()
        m.data.materials.append(mat)
    return mat


def _drift(act, scale):
    """골반이 한 축으로 움직인 최대 거리(미터). In Place 를 안 켠 클립을 잡는다.

    🔑 축이 뭔지 안 따진다. 제자리 클립은 어느 축으로도 한 걸음을 못 넘고(위아래 흔들림이
       가장 큰데 0.1 m 안쪽이다), 걷는 클립은 나아가는 축이 미터 단위로 뜬다 — 그 차이만
       보면 된다. 축을 맞히려다 틀리는 것보다 낫다.
    🔴 자동으로 빼지 않는다. 한 번 튀는 이동(`flinch` 의 밀림)은 드리프트가 아니라
       **사건**이라, 문턱으로 가르면 언젠가 그걸 삼킨다. 사람에게 말하고 사람이 정한다."""
    span = 0.0
    for fc in fcurves(act):
        if fc.data_path == f'pose.bones["{HIPS}"].location':
            vals = [k.co[1] for k in fc.keyframe_points]
            if vals:
                span = max(span, (max(vals) - min(vals)) * scale)
    return span


def build(char=None, out=None):
    """캐릭터 FBX + `mixamo/clips/*.fbx` → 하나의 `.blend`."""
    import bpy
    char = char or CHAR
    out = out or BLEND
    bpy.ops.wm.read_factory_settings(use_empty=True)

    arm, meshes = _import(char)
    if not meshes:
        raise SystemExit(f'🔴 {char} 에 메시가 없다 — Mixamo 에서 **With Skin** 으로 받아라')
    height = _normalize(arm, meshes)
    _paint(meshes)
    arm.name = arm.data.name = ARM_NAME
    for i, m in enumerate(meshes):
        m.name = MESH_NAME if i == 0 else f'{MESH_NAME}.{i}'
    arm.animation_data_clear()
    # 🔴 캐릭터 FBX 도 자기 액션(T 포즈 한 장)을 달고 온다. 안 지우면 동작 목록에
    #    「변하지 않는 동작」이 하나 섞여 들어온다 — 이름도 `Armature|mixamo.com|Layer0` 다.
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)

    clips = sorted(f for f in os.listdir(CLIPS) if f.lower().endswith('.fbx')) \
        if os.path.isdir(CLIPS) else []
    # 🔴 같은 이름이 둘이면 **뒤엣것이 앞엣것을 조용히 덮는다.** 실제로 `Injured Walk.fbx`
    #    (새로 받은 In Place 판)와 옛 `limp.fbx` 가 둘 다 `limp` 이 됐고, 정렬 순서 때문에
    #    나쁜 쪽이 이겼다. 조용히 이기게 두지 않는다 — 어느 파일을 지울지는 사람이 정한다.
    seen = {}
    for fn in clips:
        seen.setdefault(clip_name(fn), []).append(fn)
    dup = {k: v for k, v in seen.items() if len(v) > 1}
    if dup:
        lines = '\n'.join(f'   · {k}: ' + ' · '.join(v) for k, v in dup.items())
        raise SystemExit(f'🔴 같은 동작 이름의 클립이 둘 이상이다 — 하나만 남겨라\n{lines}')

    kept = []
    for fn in clips:
        name = clip_name(fn)
        src, src_meshes = _import(os.path.join(CLIPS, fn))
        act = src.animation_data and src.animation_data.action
        if act is None:
            print(f'⚠️ {fn}: 액션이 없다 — 건너뛴다')
        else:
            act.name = name
            act.use_fake_user = True        # 🔴 안 걸면 저장할 때 사라진다(사용자가 0 이라서)
            src.animation_data.action = None
            f0, f1 = act.frame_range
            if name in TRIM:
                act['trim'] = list(TRIM[name])   # 🔑 blend 에 같이 실린다 — 읽는 쪽이 안 찾아다닌다
                f0, f1 = TRIM[name]
            drift = _drift(act, arm.scale.x)
            kept.append((name, int(f1 - f0) + 1, drift, name in TRIM))
        for o in [src] + src_meshes:
            bpy.data.objects.remove(o, do_unlink=True)

    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=out)
    print(f'✅ {out}\n   캐릭터 {arm.name} · 뼈 {len(arm.data.bones)} · 키 {height:.3f}m')
    for name, n, drift, trimmed in kept:
        note = ' (구간 자름)' if trimmed else ''
        moves = ' · 이동은 사건' if name in MOVES else ''
        print(f'   · {name}: {n}프레임{note} · 골반 이동 {drift:.2f}m{moves}')
        if drift > DRIFT_WARN and name not in MOVES:
            print(f'     🔴 {name} 은 골반이 {drift:.2f}m 나아간다 — Mixamo 에서 '
                  f'**In Place 를 켜고 다시 받아라.** 안 그러면 무대 밖으로 걸어 나간다')
    if not kept:
        print('   ⚠️ 동작 없음 — mixamo/clips/*.fbx 에 넣어라')
    return out


# ── 무대에 세우기 ─────────────────────────────
_src = None


def spawn(col=None, loc=(0, 0, 0), rot_z=90):
    """주민 한 명. 반환 (meshes, armature).

    🔴 아마추어는 **한 명당 하나**여야 한다(메시 데이터는 공유). 여럿을 한 아마추어에
       묶으면 변형이 그 아마추어 공간에서 계산돼 전부 한 자리로 끌려간다.
    🔴 메시가 **여럿이다.** Y Bot 은 몸통과 관절이 따로 온다 — 하나만 복제하면 사람이
       반만 선다. 몇 개인지 세지 말고 있는 대로 다 데려온다.
    🔴 Mixamo 메시는 자기 스케일 0.01 을 **부모 역행렬 100 으로 상쇄**하고 있다. 그런데
       `mesh.parent = arm` 대입 자체가 그 역행렬을 단위로 리셋한다(블렌더 동작) — 그러면
       상쇄가 깨져 키가 1/100 이 된다(실측 0.017m). 그래서 복사해 두고 다시 넣는다."""
    import bpy
    global _src
    col = col or bpy.context.collection
    if _src is not None:
        try:
            _src[1].name                # 🔴 씬을 새로 열면 캐시가 죽은 참조가 된다.
        except ReferenceError:          #    안 잡으면 두 번째 씬에서 스폰이 통째로 터진다
            _src = None                 #    (`ReferenceError: StructRNA ... has been removed`)
    if _src is None:
        with bpy.data.libraries.load(BLEND, link=False) as (_s, dst):
            dst.objects = list(_s.objects)
            dst.actions = list(_s.actions)
        objs = [o for o in dst.objects if o]
        _src = ([o for o in objs if o.type == 'MESH'],
                next(o for o in objs if o.type == 'ARMATURE'))

    m0s, a0 = _src
    arm = a0.copy(); arm.data = a0.data.copy()      # 포즈는 객체별이라 데이터도 복사
    col.objects.link(arm)
    meshes = []
    for m0 in m0s:
        mesh = m0.copy()                            # 메시 데이터는 공유
        col.objects.link(mesh)
        pinv = m0.matrix_parent_inverse.copy()      # 🔴 아래 대입이 이걸 날린다
        mesh.parent = arm
        mesh.matrix_parent_inverse = pinv
        for md in mesh.modifiers:
            if md.type == 'ARMATURE':
                md.object = arm
        meshes.append(mesh)
    arm.rotation_mode = 'XYZ'
    arm.location = loc
    arm.rotation_euler = (0, 0, math.radians(rot_z))
    for pb in arm.pose.bones:
        pb.rotation_mode = 'QUATERNION'             # Mixamo 액션은 전부 쿼터니언이다
    return meshes, arm


def action(name):
    import bpy
    act = bpy.data.actions.get(name)
    if act is None:
        raise KeyError(f'🔴 동작 «{name}» 이 없다. 있는 것: {sorted(a.name for a in bpy.data.actions)}')
    return act


def fcurves(act):
    """액션의 fcurve 전부. 🔴 블렌더 4.4+ 는 **슬롯 액션**이라 `act.fcurves` 가 없다
    (5.2 에서 아예 사라졌다) — 레이어·스트립·채널백을 타고 들어가야 한다."""
    if getattr(act, 'fcurves', None) is not None:        # 옛 블렌더
        return list(act.fcurves)
    out = []
    for layer in act.layers:
        for strip in layer.strips:
            for cb in strip.channelbags:
                out.extend(cb.fcurves)
    return out


def span_frames(act):
    """이 액션에서 실제로 쓸 프레임 구간. `TRIM` 이 걸려 있으면 그것, 아니면 전체."""
    t = act.get('trim')
    return (float(t[0]), float(t[1])) if t else tuple(act.frame_range)


def seconds(name, fps=30.0):
    """구워진 길이(초). 대본이 `dur` 을 안 줄 때의 기본값."""
    a, b = span_frames(action(name))
    return (b - a) / fps


def play(arm, name, t, dur=None, loop=True, fps=30.0):
    """`t` 초에서의 포즈를 아마추어에 먹인다. **씬 프레임을 안 건드린다.**

    dur  : 한 번 도는 데 걸리는 초. None 이면 구운 길이 그대로.
    loop : True = 반복 · False = 1회 재생 후 마지막 프레임 유지.
    🔑 위상으로 정규화하므로 `dur` 이 자유 인자다 — 대본이 길이를 정하는 규약이 산다.
    """
    act = action(name)
    f0, f1 = span_frames(act)
    span = dur if dur and dur > 0 else (f1 - f0) / fps
    u = (t % span) / span if loop else min(max(t, 0.0), span) / span
    frame = f0 + u * (f1 - f0)

    for pb in arm.pose.bones:
        pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pb.location = (0.0, 0.0, 0.0)
        pb.scale = (1.0, 1.0, 1.0)
    for fc in fcurves(act):
        try:
            owner = arm.path_resolve(fc.data_path.rsplit('.', 1)[0])
        except ValueError:
            continue                                # 이 리그에 없는 뼈 — 조용히 건너뛴다
        prop = fc.data_path.rsplit('.', 1)[1]
        v = getattr(owner, prop)
        v[fc.array_index] = fc.evaluate(frame)
        setattr(owner, prop, v)

    # 🔴 성분을 fcurve 별로 **따로** 보간하면 노름이 1 을 벗어난다(실측 0.9954).
    #    회전이 아닌 것이 회전 자리에 앉으면 마디마다 살짝 찌그러진다 — 정규화한다.
    for pb in arm.pose.bones:
        pb.rotation_quaternion.normalize()
    return frame


# ── 걸음 ─────────────────────────────
TOES = (PREFIX + 'LeftToeBase', PREFIX + 'RightToeBase')
STANCE_Z = 0.02      # 가장 낮은 발끝에서 이만큼 안이면 「디뎠다」. 실측: 스탠스는 바닥
                     # +0.002 안쪽, 스윙은 +0.13 까지 뜬다 — 둘이 안 섞인다
_gait = {}


def forward(rot_z):
    """`spawn(rot_z=θ)` 로 세운 사람이 **바라보고 나아가는 방향**(단위 xy).

    🔴 실측이다. Mixamo 캐릭터는 rot_z=0 에서 **−Y** 를 본다 — 옛 리그(−X)와 다르다.
       (`walk` 에서 디딘 발이 +Y 로 흐르는 것을 재서 얻었다. 추측하지 마라, 이 프로젝트가
       부호로 네 번 헛돌았다.)"""
    a = math.radians(rot_z)
    return (math.sin(a), -math.cos(a))


def ground_speed(name, fps=30.0):
    """이 동작이 만드는 **지면 속도**(m/s). 무대는 이 속도로 사람을 앞으로 옮겨야 한다.

    In Place 클립은 사람이 제자리에서 돌고 **디딘 발이 뒤로 흐른다.** 그 흐름과 무대의
    이동이 어긋난 만큼이 곧 발 미끄러짐이다.

    🔴 상수로 적어 두지 않는다. 클립을 다시 받으면 값이 바뀌는데 상수는 안 바뀐다 —
       그러면 그 숫자가 조용히 거짓말이 된다. 여기서 **매번 재고 캐시한다.**
    🔴 디딘 발은 「더 뒤로 가는 발」이 아니라 **땅에 가까운 발**이다. 전자로 골랐다가
       한 판 틀렸다(공중에서 앞으로 휘두르는 발이라 부호가 반대다).
    🔴 **체공 프레임은 뺀다.** 달리기는 두 발이 다 뜬 순간이 있고, 그때는 「더 낮은 발」도
       공중이다. 섞으면 속도가 부푼다(실측 +4.09 → 뺀 뒤 +4.94 가 참값이다).
    """
    import bpy
    import statistics
    if name in _gait:
        return _gait[name]
    arm = next((o for o in bpy.context.scene.objects
                if o.type == 'ARMATURE' and TOES[0] in o.pose.bones), None)
    if arm is None:
        raise RuntimeError('🔴 지면 속도를 재려면 씬에 주민이 하나 서 있어야 한다')

    keep = (arm.location.copy(), arm.rotation_euler.copy())
    arm.location, arm.rotation_euler = (0, 0, 0), (0, 0, 0)
    f0, f1 = span_frames(action(name))
    n = int(f1 - f0) + 1
    span = n / fps
    track = []
    for i in range(n):
        play(arm, name, span * i / n, dur=span)
        bpy.context.view_layer.update()
        track.append([(arm.matrix_world @ arm.pose.bones[t].matrix.translation).copy()
                      for t in TOES])
    arm.location, arm.rotation_euler = keep

    rng = [max(p[k][ax] for p in track) - min(p[k][ax] for p in track)
           for ax in (0, 1) for k in (0, 1)]
    axis = 0 if max(rng[0], rng[1]) >= max(rng[2], rng[3]) else 1
    floor = min(p[k].z for p in track for k in (0, 1))
    # 🔴 **양쪽 프레임 모두** 디딘 발이어야 센다. 한쪽만 보면 발이 바뀌는 프레임에서
    #    「방금 디딘 발이 직전에 공중에서 움직인 거리」를 미끄러짐으로 센다 — 실측
    #    `limp` 이 중앙값 0.08cm 인데 최대만 16.23cm 로 튄 것이 전부 그 한 프레임이었다.
    vs = []
    for a, b in zip(track, track[1:]):
        k = 0 if a[0].z <= a[1].z else 1
        if a[k].z <= floor + STANCE_Z and b[k].z <= floor + STANCE_Z:
            vs.append((b[k][axis] - a[k][axis]) * fps)
    _gait[name] = abs(statistics.median(vs)) if vs else 0.0
    return _gait[name]


# ── 자체 점검 ─────────────────────────────
def demo():
    """`blender --background --python mixamo.py -- demo` — 구운 blend 가 계약을 지키는가."""
    import bpy
    bpy.ops.wm.read_factory_settings(use_empty=True)
    meshes, arm = spawn()
    assert len(arm.data.bones) > 20, f'뼈가 {len(arm.data.bones)} 개뿐이다'
    assert any(b.name.startswith(PREFIX) for b in arm.data.bones), 'Mixamo 뼈대가 아니다'
    assert meshes, '🔴 메시가 없다 — **With Skin** 으로 받은 캐릭터인가'
    bpy.context.view_layer.update()
    top = max((m.matrix_world @ v.co).z for m in meshes for v in m.data.vertices)
    assert abs(top - H) < 0.02, f'키가 {top:.3f}m — {H} 여야 한다'

    names = sorted(a.name for a in bpy.data.actions)
    assert names, '🔴 액션이 하나도 없다 — build 가 안 됐거나 clips/ 가 비었다'
    for nm in names:
        play(arm, nm, 0.0, dur=2.0)
        a = [tuple(pb.rotation_quaternion) for pb in arm.pose.bones]
        play(arm, nm, 1.0, dur=2.0)
        b = [tuple(pb.rotation_quaternion) for pb in arm.pose.bones]
        moved = max(abs(x - y) for qa, qb in zip(a, b) for x, y in zip(qa, qb))
        assert moved > 0.01, f'{nm}: 0초와 1초의 포즈가 같다 ({moved:.4f})'
        for pb in arm.pose.bones:                   # 쿼터니언이 살아 있는가
            n = sum(v * v for v in pb.rotation_quaternion) ** 0.5
            assert abs(n - 1.0) < 1e-3, f'{nm}.{pb.name}: 노름이 {n:.4f}'
        print(f'✅ {nm}: {seconds(nm):.2f}초 · 최대 변화 {moved:.3f}')
    print(f'✅ 캐릭터 {len(arm.data.bones)}뼈 · 키 {top:.3f}m · 동작 {len(names)}종')


if __name__ == '__main__':
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else ['build']
    if argv[0] == 'demo':
        demo()
    else:
        build()
