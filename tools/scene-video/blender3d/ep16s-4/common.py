"""ep16s-4 다섯 샷이 공유하는 것 — 마을·여섯·관계의 실·무덤 하나.

이 편이 맡은 사건: **죽음이 관계까지 지웠고, 고친 것은 지우는 함수가 아니었다.**
대본은 `../../notes/ep16s-대본-3D.md` §4 이고, 그림 계약(§0)은 다섯 편 공통이다:

  관계        = **두 사람을 잇는 실**(`instrument.thread`). 친하면 두 가닥
  끊김        = 실이 사라지는 것 — **양쪽 다**
  지우기 전 사진 = 실이 **보라로 굳어** 무덤 쪽에 남는 것
  수(두 가닥·한 장) = 자막이 진다. 화면은 **가닥 수**만 진다

🔴 **실은 만들 때 두 점이 굳는다.** 그래서 실이 걸린 사람은 제자리에 선다 — 움직이면
   실이 허공을 가리킨다. 이 편에서 자리를 옮기는 사람은 없다.
🔴 살아 있는 실은 **계기 시안**, 굳은 실은 **뜻층 보라**다. 색이 갈려야 「남긴 것」과
   「지워진 것」이 화면에서 갈린다. 뜻층은 보라 하나뿐이라 예산의 절반만 쓴다.
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

EP = 'ep16s-4'
FPS = 30

# 🔑 **짝이 둘이다.** 앞의 둘은 친해서(마주 서서 이야기) 두 가닥, 뒤의 둘은 사이가
#    나빠서(등지고 섬) 한 가닥이다 — 「사이가 나빠져도 줄은 남는다」가 그 그림이다.
CAST = [
    ('talk',   (-2.05, -2.85), (-1.05, -3.05)),   # A — 죽는 사람(단짝)
    ('talk',   (-1.05, -3.05), (-2.05, -2.85)),   # B — 남는 사람
    ('stop',   ( 1.55, -1.15), ( 2.55, -0.55)),   # C — 등지고 (아래 face 로 뒤집는다)
    ('stop',   ( 2.55, -0.55), ( 1.55, -1.15)),   # D
    ('farm',   (-3.40, -2.45), 'field'),
    ('chop',   (-3.90,  0.35), (-4.8, 0.6)),
]
A, B, C, D = 0, 1, 2, 3
BACK_TO_BACK = (C, D)      # 이 둘만 서로에게 **등을 돌린다**

# 🔴 짝을 밭 위에 두면 **무덤이 밭 안에 선다**(첫 판 접점 시트). 소품을 피해 남쪽으로 옮겼다.
EYE = ((-0.30, -8.35, 3.05), (-0.55, -1.55, 1.05))     # 마을 · 두 짝이 한 프레임에
MID = ((-0.85, -6.95, 2.15), (-1.55, -2.90, 0.95))     # 앞 짝 쪽으로
CLOSE = ((-0.75, -5.75, 1.35), (-1.55, -2.95, 0.80))   # 실 두 가닥
GRAVE_CAM = ((-0.95, -6.25, 1.55), (-2.05, -2.85, 0.70))
WIDE_LENS, MID_LENS, CLOSE_LENS = 30, 36, 42


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
        return math.radians(58)
    s = village.SPOTS[spot] if isinstance(spot, str) else spot
    a = math.atan2(y - s[1], x - s[0])
    return a + math.pi if i in BACK_TO_BACK else a      # 등지고 선 둘만 뒤집는다


def build(grave=False):
    """마을 + 여섯 (+ A 의 무덤). 반환 (village, arms, grave_parts)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    v = village.build()
    arms = []
    for i, (_job, (x, y), _spot) in enumerate(CAST):
        _mesh, arm = stage.rigged(loc=(x, y, 0), rot_z=math.degrees(home_face(i)))
        arms.append(arm)
    village.outskirts()
    g = []
    if grave:
        x, y = CAST[A][1]
        g = [(o, tuple(o.scale), o.location.z) for o in village.grave(x, y, -14)]
    return v, arms, g


VIOLET = None
CYAN = None


def mats():
    global VIOLET, CYAN
    if VIOLET is None:
        VIOLET = stage.meaning_mat('violet', strength=0.90, albedo_scale=0.15)
        CYAN = stage.instrument_mat(strength=1.0)
    return VIOLET, CYAN


def bond(i, j, mat, dz=(0.0, 0.0), w=0.016):
    """두 사람을 잇는 실 한 가닥. 🔴 자리는 여기서 굳는다."""
    ax, ay = CAST[i][1]
    bx, by = CAST[j][1]
    return instrument.thread((ax, ay, 0.92 + dz[0]), (bx, by, 0.92 + dz[1]), mat, w=w)


def grave_rise(parts, k):
    for o, sc, z0 in parts:
        o.hide_render = k <= 0.02
        o.scale = (sc[0], sc[1], max(sc[2] * k, 1e-4))
        o.location.z = z0 * k


def work(arm, job, t):
    stage.pose(arm, motions.MOTIONS[job](t))


def all_work(arms, t, home, skip=()):
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
                                  motions.collapse((t - t0) * 1.15),
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
