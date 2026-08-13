"""ep16s-5 다섯 샷이 공유하는 것 — 마을·여섯·게으름뱅이 둘의 무덤·다섯 편의 계기.

이 편이 맡은 사건: **같은 판을 다시 켰더니 회상이 다른 문장이 됐다.** 대본은
`../../notes/ep16s-대본-3D.md` §5 이고, 그림 계약(§0)은 다섯 편 공통이다.

🔑 **이 편만 형제 넷의 어휘를 다 불러온다** — 비석 옆 도구(16-1) · 격자(16-2·16-3) ·
   실(16-4). 착지 편이라 그 모임 자체가 결론의 몸이다.
🔴 그래도 **새로 만드는 것은 0개**다. 원문이 말하는 것이 정확히 그것이다:
   '이미 콘솔에만 있던 것을 화면으로 옮긴 이야기'.
🔴 뜻층은 **보라 하나**(비석 옆 도구). 계기 셋(실·막대·격자)은 계기층이라 예산 밖이다.
"""
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import instrument       # noqa: E402
import motions          # noqa: E402
import stage            # noqa: E402
import village          # noqa: E402

EP = 'ep16s-5'
FPS = 30

# 🔴 게으름뱅이 둘은 **소품 없는 빈 땅**에 나란히 둔다 — 밭·덤불 옆에 두면 「논다」가
#    거짓말이 되고, 붙여 둬야 「둘 다」가 한 프레임에 들어온다(ep15s-5 가 배운 것).
#    🔑 단 이 편의 대사는 「집과 밭이 있었는데」라 **집과 밭이 같은 프레임에 보이는 자리**여야 한다.
CAST = [
    ('farm',   (-3.15, -1.25), 'field'),
    ('water',  (-2.05, -1.95), 'field'),
    ('hammer', ( 0.95,  1.40), (0.2, 4.0)),
    ('draw',   ( 2.30, -0.90), 'well'),
    ('eat',    (-0.95, -2.75), None),      # 게으름뱅이 ① — 간식
    ('rest',   (-0.05, -3.15), None),      # 게으름뱅이 ② — 여가
]
IDLERS = (4, 5)
TOOLS = ('wateringcan', 'hammer')          # 두 무덤 옆에 서는 도구 — 서로 달라야 한다

EYE = ((-0.20, -8.55, 3.25), (-0.45, -1.15, 1.05))     # 마을 전체
MID = ((0.35, -7.05, 2.35), (-0.55, -2.35, 0.90))      # 노는 둘 쪽으로
CLOSE = ((0.65, -6.05, 1.45), (-0.50, -2.95, 0.60))    # 무덤 둘 + 도구
HIGH = ((-0.25, -8.05, 4.55), (-0.45, -0.75, 1.85))    # 계기가 다 보이는 자리
WIDE_LENS, MID_LENS, CLOSE_LENS = 30, 36, 40


def lens(cam, mm):
    cam.data.lens = mm


def ease(u):
    u = max(0.0, min(1.0, u))
    return u * u * (3 - 2 * u)


def out_dir(shot):
    d = os.path.join(stage.OUT_ROOT, EP, shot, 'frames')
    os.makedirs(d, exist_ok=True)
    return d


def seconds(shot_id):
    return stage.shot_seconds(EP, shot_id)


def line_at(shot_id, i):
    return stage.shot_lines(EP, shot_id)[i][0]


def home_face(i):
    _job, (x, y), spot = CAST[i]
    if not spot:
        return math.radians(61)            # 누운 몸이 흰 공이 안 되는 각(ep15s-5 실측)
    s = village.SPOTS[spot] if isinstance(spot, str) else spot
    return math.atan2(y - s[1], x - s[0])


def build(graves=False):
    """마을 + 여섯 (+ 게으름뱅이 둘의 무덤). 반환 (village, arms, graves)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    v = village.build()
    arms = []
    for i, (_job, (x, y), _spot) in enumerate(CAST):
        _mesh, arm = stage.rigged(loc=(x, y, 0), rot_z=math.degrees(home_face(i)))
        arms.append(arm)
    village.outskirts()
    gs = []
    if graves:
        for n, i in enumerate(IDLERS):
            x, y = CAST[i][1]
            gs.append([(o, tuple(o.scale), o.location.z)
                       for o in village.grave(x, y, -14 + 26 * n)])
    return v, arms, gs


VIOLET = None
CYAN = None


def mats():
    global VIOLET, CYAN
    if VIOLET is None:
        VIOLET = stage.meaning_mat('violet', strength=0.90, albedo_scale=0.15)
        CYAN = stage.instrument_mat(strength=1.0)
    return VIOLET, CYAN


TOOL_OF = {'axe': village.axe, 'pickaxe': village.pickaxe,
           'hammer': village.hammer, 'wateringcan': village.wateringcan}


def tool_at(kind, x, y, face=0.0, s=1.5):
    """비석 옆에 선 도구(뜻층 보라). 🔴 1.0 배는 화면에서 보라 점이다."""
    v, _c = mats()
    root = TOOL_OF[kind]()
    for ch in root.children:
        ch.data.materials.clear()
        ch.data.materials.append(v)
    root.location = (x + 0.42, y - 0.20, 0.0)
    root.rotation_mode = 'XYZ'
    root.rotation_euler = (math.radians(90), 0.0, face)
    root.scale = (s, s, s)
    return root


def tool_show(root, on):
    for ch in root.children:
        ch.hide_render = not on


def grave_rise(parts, k):
    for o, sc, z0 in parts:
        o.hide_render = k <= 0.02
        o.scale = (sc[0], sc[1], max(sc[2] * k, 1e-4))
        o.location.z = z0 * k


def work(arm, job, t):
    stage.pose(arm, motions.MOTIONS[job](t))


def all_work(arms, t, home, skip=()):
    """🔴 이 편의 훅에서는 아무도 안 돌아본다 — 그게 사건이다(ep15s-5 계승)."""
    for i, arm in enumerate(arms):
        if i in skip:
            continue
        arm.location = home[i]
        arm.rotation_euler = (0, 0, home_face(i))
        work(arm, CAST[i][0], t)


def fall(arm, i, home, t, t0):
    arm.location = home
    arm.rotation_euler = (0, 0, home_face(i))
    if t < t0:
        work(arm, CAST[i][0], t)
        return
    stage.pose(arm, motions.blend(motions.MOTIONS[CAST[i][0]](t),
                                  motions.collapse((t - t0) * 1.25),
                                  ease((t - t0) / 0.24)))


def hide_person(arm, on):
    arm.hide_render = not on
    for ob in arm.children:
        ob.hide_render = not on


def lerp3(a, b, u):
    return tuple(p + (q - p) * u for p, q in zip(a, b))


def fly(cam, key, t):
    if t <= key[0][0]:
        loc, at = key[0][1], key[0][2]
    elif t >= key[-1][0]:
        loc, at = key[-1][1], key[-1][2]
    else:
        for (t0, l0, a0), (t1, l1, a1) in zip(key, key[1:]):
            if t0 <= t <= t1:
                u = ease((t - t0) / max(t1 - t0, 1e-6))
                loc, at = lerp3(l0, l1, u), lerp3(a0, a1, u)
                break
    stage.aim(cam, loc, at)
