"""ep16s-2 다섯 샷이 공유하는 것 — 마을·여섯·공중 격자·실.

이 편이 맡은 사건: **알림 칸이 한 개뿐이라 일곱 건이 서로를 지웠다.** 대본은
`../../notes/ep16s-대본-3D.md` §2 이고, 그림 계약(§0)은 다섯 편 공통이다:

  알림 칸 · 명부 = **공중 격자 한 칸이 한 사람**(`instrument.build`)
  켜졌다 곧 꺼짐 = 덮어쓰기 · 켜진 채 남음 = 상태 알림
  누구인지     = 칸에서 **실**이 그 사람에게 내려간다(`instrument.thread`)
  수(일곱·열네) = 자막이 진다. 화면은 **깜빡인 횟수와 칸의 개수**만 진다

🔴 **이 편에는 죽음이 없다.** 16-1 이 죽음을 맡았고 이 편은 **살아 있는 사람을 못 찾는**
   이야기다. 무덤을 끌고 오면 두 편이 같은 이야기로 읽힌다.
🔴 뜻층은 **빨강 하나**(지금 위험한 것). 격자·막대·실·원은 계기층이라 예산 밖이다.
🔴 실은 **다시 못 그린다**(`thread` 는 만들 때 두 점을 굳힌다). 그래서 실을 쓰는 샷에서는
   사람이 제자리에 선다 — 자리가 바뀌면 실이 허공을 가리킨다.
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

EP = 'ep16s-2'
FPS = 30

# ── 여섯이 무엇을 하며 어디 있나 ─────────────────────────
# 🔴 **굶는 사람이 겉으로 안 갈린다**가 이 편의 사건이다. 그래서 굶는 사람도 남들과 같은
#    자리·같은 크기로 두고, 다른 것은 **막대 하나**뿐이다(S3 에서만 보인다).
CAST = [
    ('farm',   (-3.20, -1.10), 'field'),
    ('chop',   (-3.90,  0.35), (-4.8, 0.6)),
    ('draw',   ( 2.30, -0.90), 'well'),
    ('gather', ( 1.30, -3.30), 'bush'),
    ('hammer', ( 0.90,  1.40), (0.2, 4.0)),
    ('eat',    (-1.20, -2.60), None),      # 굶던 사람 — S2 에서 이 사람이 먹는다
]
HUNGRY = 5
# S3 이 한 사람씩 세우는 넷 — 실이 내려갈 사람들. 🔴 굶는 사람이 그 안에 있어야 한다
LISTED = (5, 0, 3, 2)

# 🔴 **격자가 화면 위로 잘렸다**(첫 판 접점 시트). 계기가 집 꼭대기(1.85)보다 위(2.60)에
#    떠 있어서, 마을을 겨누면 격자가 띠 밖으로 나간다 — 겨냥을 **둘 사이(1.9~2.1)**에 둔다.
# 🔴 두 번째 판도 틀렸다 — 낮은 카메라에서 격자는 **눈높이로 눕는다.** 칸이 칸으로 안
#    읽히고 시안 띠 넷이 된다. 계기는 땅과 나란히 떠 있으니 **위에서 내려다봐야** 칸이다
#    (ep16s-1 아웃트로가 z 4.15 에서 제대로 나왔다).
EYE = ((-0.20, -9.00, 4.75), (-0.40, -0.20, 1.85))    # 마을 + 공중 격자가 한 프레임에
MID = ((-0.15, -7.40, 4.45), (-0.55, -0.10, 1.90))    # 격자 쪽으로
CLOSE = ((-0.13, -2.85, 4.35), (-0.13, 1.30, 2.55))   # 칸 하나(2번 칸 위에서)
NEAR = ((-0.45, -5.55, 1.55), (-1.20, -2.60, 0.75))   # 굶던 사람 발치
WIDE_LENS, MID_LENS, CLOSE_LENS = 30, 34, 44


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
    """그 샷 i 번째 자막 줄이 언제 시작하나(초). 🔴 몸짓 시각을 손으로 적지 마라."""
    return stage.shot_lines(EP, shot_id)[i][0]


def home_face(i):
    _job, (x, y), spot = CAST[i]
    if not spot:
        return math.radians(58)
    s = village.SPOTS[spot] if isinstance(spot, str) else spot
    return math.atan2(y - s[1], x - s[0])


def build():
    """마을 + 여섯 + 공중 격자 4×2. 반환 (village, arms, cells)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    v = village.build()
    arms = []
    for i, (_job, (x, y), _spot) in enumerate(CAST):
        _mesh, arm = stage.rigged(loc=(x, y, 0), rot_z=math.degrees(home_face(i)))
        arms.append(arm)
    village.outskirts()
    cells = instrument.build(4, 2)
    return v, arms, cells


RED = None
CYAN = None


def mats():
    """뜻층 빨강(위험) + 계기 시안. 🔴 **프레임 안에서 만들지 마라** — 한 번만 만든다."""
    global RED, CYAN
    if RED is None:
        RED = stage.meaning_mat('red', strength=0.95, albedo_scale=0.14)
        CYAN = stage.instrument_mat(strength=1.0)
    return RED, CYAN


def light(cells, on_idx):
    """켜진 칸만 빨강, 나머지는 빈 칸(시안 테두리)."""
    red, cyan = mats()
    for n, cell in enumerate(cells):
        instrument.show(cell, True)
        instrument.paint(cell, red if n in on_idx else cyan)


def work(arm, job, t):
    stage.pose(arm, motions.MOTIONS[job](t))


def all_work(arms, t, home, skip=()):
    for i, arm in enumerate(arms):
        if i in skip:
            continue
        arm.location = home[i]
        arm.rotation_euler = (0, 0, home_face(i))
        work(arm, CAST[i][0], t)


def stand(arm, i, home, t):
    """하던 일을 멈추고 **선다.** 「넷이 똑같이 서 있다」가 S3 의 첫 그림이다.
    🔴 `home` 은 **그 사람의 자리 하나**다(목록이 아니다)."""
    arm.location = home
    arm.rotation_euler = (0, 0, home_face(i))
    stage.pose(arm, motions.stop(t))


def walk_to(arm, home, target, t, t0, span):
    wt = min(max(t - t0, 0.0), span)
    ang = math.atan2(target[1] - home[1], target[0] - home[0])
    d = min(motions.WALK_SPEED * wt,
            math.hypot(target[0] - home[0], target[1] - home[1]))
    p = (home[0] + d * math.cos(ang), home[1] + d * math.sin(ang))
    arm.location = (p[0], p[1], 0)
    arm.rotation_euler = (0, 0, ang + math.pi)
    return p, wt


def walk_pose(arm, wt, blend=0.26):
    stage.pose(arm, motions.blend(motions.stop(wt), motions.walk(wt), ease(wt / blend)))


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
