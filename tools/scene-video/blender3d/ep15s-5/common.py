"""ep15s-5 여섯 샷이 공유하는 것 — 마을·주민 자리·계기·카메라.

🔴 **2D 판은 참고하지 않았다**(사용자 지시). 대본도 그림도 `scene.json` 의 `source.note`
   가 적어 둔 사건 셋에서 다시 뽑았다:
   ① 성공 기준은 통과였다(성격 여섯이 열다섯 쌍 전부 다른 상위 목표 구성으로 갈렸다)
   ② 며칠 뒤 떠올린 것은 「게으름뱅이 둘 다 그냥 죽었어, 별 이야기가 없었다」뿐이었다
   ③ 진단 = 이야기가 없던 게 아니라 **로그에만** 있었다(노동 2% · 여가·배고픔·간식).

🔑 **3D 라서 새로 되는 것 셋이 이 편의 그림을 정했다.**
   ① 「열다섯 쌍이 다 달랐다」는 **여섯이 실제로 서로 다른 일을 하는 마을**로 보인다.
      수(15)는 자막이 지고 화면은 **다름**만 진다
   ② 「그냥 죽었다」는 **아무도 안 돌아보는 죽음**이다 — 이 파이프라인의 규칙(사건이 나면
      남이 반응한다)을 **일부러 뒤집은 자리**다. 반응이 없는 것이 이 편의 사건이다
   ③ 「로그에만 있었다」는 **쓰러진 몸 위로 떠오르는 막대 넷**이다. 화면에 없던 것이 값으로는
      다 있었다 — 계기층이 정확히 그 말이고, 2D 판에는 이 자리가 없었다

🔴 이 편은 계기가 **뜻층 예산을 안 먹는다**(전부 계기층 색). 모닥불 앰버 하나가 유일한 뜻색이다.
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

EP = 'ep15s-5'
FPS = 30

# ── 여섯이 무엇을 하며 어디 있나 ────────────────────────
# 🔑 **여섯이 서로 다른 일을 하는 것**이 이 편의 첫 그림이다. 넷은 일하고 둘은 논다 —
#    그 둘이 게으름뱅이이고, 이 편에서 죽는 사람들이다.
# 🔴 게으름뱅이 둘은 **소품 없는 빈 땅**에 둔다. 밭·덤불 옆에 두면 「논다」가 거짓말이 된다.
#    그리고 둘을 **붙여** 둔다 — 나란히 쓰러져야 「둘 다」가 한 프레임에 들어온다.
IDLERS = (4, 5)
CAST = [
    ('farm', (-2.7, -2.3), 'field'),
    ('draw', (2.6, -0.9), 'well'),
    ('chop', (-3.65, 0.45), (-4.8, 0.6)),
    ('gather', (1.3, -2.3), 'bush'),
    ('eat', (-0.55, -3.30), None),          # 게으름뱅이 ① — 간식
    ('rest', (1.55, -4.30), None),          # 게으름뱅이 ② — 여가
]

# 🔑 그 사람이 **주로 한 것**. 색은 행동 범주가 정하고(`palette.NEED_OF`), 길이는 사람마다
#    다르다 — 색과 길이가 같이 달라야 「열다섯 쌍 전부 달랐다」가 한 프레임에 읽힌다.
MAIN = [('need_work', 0.84), ('need_work', 0.61), ('need_work', 0.93),
        ('need_body', 0.55), ('need_body', 0.88), ('instrument', 0.74)]

# 🔴 쓰러진 사람의 **로그 한 줄** — 이 편이 화면에 올리는 유일한 수(2%)가 여기 있다.
#    막대 넷이 그 사람의 일생이다. 일만 바닥에 붙어 있는 것이 이 편의 그림 전부다.
LOG = [('need_work', 0.02), ('instrument', 0.62), ('need_body', 0.78), ('need_body', 0.45)]

EYE = ((4.20, -7.60, 3.20), (-0.60, -1.10, 0.90))     # 마을 여섯이 한 프레임에
MID = ((2.70, -7.00, 2.35), (-0.75, -2.40, 0.85))     # 노는 둘 쪽으로
CLOSE = ((1.36, -5.48, 1.25), (-0.55, -3.20, 0.50))   # 쓰러진 사람 + 막대 넷 (옆에서)
WIDE_LENS, MID_LENS, CLOSE_LENS = 32, 36, 42


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
        # 🔴 노는 둘의 각은 **재서 정했다.** 카메라를 정면으로 보게 두면 무너진 몸이
        #    머리 뒤에 통째로 가려 **흰 공 하나**가 된다(ep15s-2 에서 배운 것). 두 카메라
        #    (본문2 의 MID · 본문3 의 CLOSE) 양쪽에서 시선축과 50~70° 어긋나는 각이 61° 다.
        return math.radians(61)
    s = village.SPOTS[spot] if isinstance(spot, str) else spot
    return math.atan2(y - s[1], x - s[0])


def build():
    """마을 + 주민 여섯 + 모닥불 + 마을 바깥. 반환 (village, arms, fire)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    v = village.build()
    arms = []
    for i, (_job, (x, y), _spot) in enumerate(CAST):
        _mesh, arm = stage.rigged(loc=(x, y, 0), rot_z=math.degrees(home_face(i)))
        arms.append(arm)
    village.outskirts()
    fire = village.flame(village.SPOTS['fire'])
    return v, arms, fire


def cam_right(cam_loc, at):
    dx, dy = at[0] - cam_loc[0], at[1] - cam_loc[1]
    n = math.hypot(dx, dy) or 1.0
    return (-dy / n, dx / n)


def bars(spec, z=0.95):
    """색·값 목록으로 막대를 만든다. 반환 [(gauge, 값), ...]"""
    return [(instrument.gauge((0.0, 0.0, z), color=c), v) for c, v in spec]


def bars_at(gs, x, y, right, z=0.95, gap=None):
    """막대들을 (x, y) 위에 **화면 가로로** 나란히 놓는다."""
    # 🔴 `cam_right` 가 주는 벡터는 **화면 왼쪽**이다(시선을 +90° 돌린 것). 그대로 쓰면
    #    막대 순서가 좌우로 뒤집혀서, 자막이 먼저 부르는 「일」이 화면 맨 오른쪽에 선다.
    g_ = instrument.GAUGE_GAP if gap is None else gap
    for i, (g, _v) in enumerate(gs):
        off = -((i - (len(gs) - 1) / 2) * g_ * g['m'])
        instrument.gauge_at(g, x + right[0] * off, y + right[1] * off, z)


def bars_show(gs, on):
    for g, _v in gs:
        for key in ('track', 'fill', 'mark'):
            if g.get(key) is not None:
                g[key].hide_render = not on


def work(arm, job, t):
    stage.pose(arm, motions.MOTIONS[job](t))


def all_work(arms, t, home, skip=()):
    """🔴 이 편에서는 아무도 안 돌아본다 — 그게 사건이다. `watch` 같은 인자를 두지 않았다."""
    for i, arm in enumerate(arms):
        if i in skip:
            continue
        arm.location = home[i]
        arm.rotation_euler = (0, 0, home_face(i))
        work(arm, CAST[i][0], t)


COLLAPSE_RATE = 1.25       # 게으름뱅이의 죽음은 사건이 아니라서 빨리 지나간다


def die(arm, i, home, t, t0):
    """제 일을 하다 **그냥 무너진다.** 아무 예비 동작도, 아무 반응도 없다.

    🔴 `startle`(멈칫하고 돌아본다)을 여기 쓰면 안 된다 — 이 편의 뜻은 **아무 일도 안
       일어난 것처럼 죽는다**이고, 예비 동작은 그 자체가 사건 표시가 된다."""
    arm.location = home
    arm.rotation_euler = (0, 0, home_face(i))
    job = CAST[i][0]
    if t < t0:
        work(arm, job, t)
        return
    stage.pose(arm, motions.blend(motions.MOTIONS[job](t),
                                  motions.collapse((t - t0) * COLLAPSE_RATE),
                                  ease((t - t0) / 0.24)))
