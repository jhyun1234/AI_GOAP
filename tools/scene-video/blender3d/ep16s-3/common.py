"""ep16s-3 다섯 샷이 공유하는 것 — 마을·여섯·명부 격자·무덤 하나.

이 편이 맡은 사건: **명부를 만들었더니 한 사람이 통째로 덮었다.** 대본은
`../../notes/ep16s-대본-3D.md` §3 이고, 그림 계약(§0)은 다섯 편 공통이다:

  명부       = **공중 격자**(`instrument.build`) — 칸 하나가 기록 한 줄
  한 사람이 덮음 = **한 색이 아홉 칸을 연달아** 칠하는 것
  기록이 사라짐  = 사람이 무너질 때 그 위 칸도 **같이 꺼지는** 것
  묶기       = 아홉 칸이 **한 칸으로 접히는** 것
  수(아홉·열한) = 자막이 진다. 화면은 **칸의 개수**만 진다

🔴 격자는 3×4 열두 칸이다 — 아홉이 **판을 덮는 것**으로 보이려면 판이 아홉보다 조금 커야
   한다. 딱 아홉 칸짜리 판이면 「다 찼다」가 아니라 「채웠다」로 읽힌다.
🔴 뜻층은 **보라 하나**(그 사람에게 닿은 자리). 16-2 의 빨강과 갈리는 이유는 이 편의
   사건이 위험이 아니라 **한 사람의 흔적**이기 때문이다. 갈라서는 둘째 색(`need_work`)은
   계기층이라 예산 밖이다.
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

EP = 'ep16s-3'
FPS = 30

CAST = [
    ('water',  (-3.15, -1.15), 'field'),      # 밭 옆 사람 — S3 에서 물을 준다
    ('hammer', ( 0.95,  1.40), (0.2, 4.0)),   # 집 옆 사람 — S3 에서 망치질한다
    ('chop',   (-3.90,  0.35), (-4.8, 0.6)),
    ('draw',   ( 2.30, -0.90), 'well'),
    ('gather', ( 1.30, -3.30), 'bush'),
    ('stop',   (-1.20, -2.60), None),         # 게으름뱅이 — 일하러 안 가고 서 있다
]
IDLER = 5
PAIR = (0, 1)                                 # 「밭 완공 ↔ 집 완공」이 갈라서는 둘

COLS, ROWS = 4, 3
NINE = (0, 1, 2, 3, 4, 5, 6, 7, 8)            # 한 사람이 채우는 아홉 칸
FOUR = (0, 1, 2, 3)                           # 성격·직업·들어온 날·떠난 날

EYE = ((-0.20, -9.00, 4.85), (-0.40, -0.20, 1.85))    # 마을 + 격자
MID = ((-0.15, -7.40, 4.55), (-0.55, -0.10, 1.95))    # 격자 쪽으로
CLOSE = ((-0.15, -4.15, 4.40), (-0.35, 0.70, 2.45))   # 칸 몇 개
NEAR = ((-0.55, -5.65, 1.55), (-1.20, -2.60, 0.70))   # 게으름뱅이 · 무덤
WIDE_LENS, MID_LENS, CLOSE_LENS = 30, 34, 42


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
    return math.atan2(y - s[1], x - s[0])


def build(grave=False):
    """마을 + 여섯 + 격자 4×3 (+ 무덤). 반환 (village, arms, cells, grave_parts)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    v = village.build()
    arms = []
    for i, (_job, (x, y), _spot) in enumerate(CAST):
        _mesh, arm = stage.rigged(loc=(x, y, 0), rot_z=math.degrees(home_face(i)))
        arms.append(arm)
    village.outskirts()
    cells = instrument.build(COLS, ROWS)
    g = []
    if grave:
        x, y = CAST[IDLER][1]
        g = [(o, tuple(o.scale), o.location.z) for o in village.grave(x, y, -12)]
    return v, arms, cells, g


VIOLET = None
GREEN = None
CYAN = None


def mats():
    """뜻층 보라 + 계기 연두·시안. 🔴 프레임 안에서 만들지 마라 — 한 번만 만든다."""
    global VIOLET, GREEN, CYAN
    if VIOLET is None:
        VIOLET = stage.meaning_mat('violet', strength=0.90, albedo_scale=0.15)
        GREEN = stage.need_mat('need_work', strength=1.0)
        CYAN = stage.instrument_mat(strength=1.0)
    return VIOLET, GREEN, CYAN


def paint(cells, violet_idx=(), green_idx=()):
    v, g, c = mats()
    for n, cell in enumerate(cells):
        instrument.paint(cell, v if n in violet_idx else (g if n in green_idx else c))


def show(cells, on_idx=None):
    for n, cell in enumerate(cells):
        instrument.show(cell, True if on_idx is None else n in on_idx)


def fold(cells, keep, k):
    """접힘 — `keep` 만 남고 나머지가 **그 칸 쪽으로 줄어들며** 사라진다(k 0→1)."""
    kx, ky, kz = cells[keep].location
    for n, cell in enumerate(cells):
        if n == keep:
            continue
        s = max(1.0 - k, 1e-3)
        cell.scale = (s, s, s)
        instrument.show(cell, s > 0.06)


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
