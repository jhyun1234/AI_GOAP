"""ep15s-3 샷 일곱이 공유하는 것 — 마을·주민 자리·거리 계기·카메라.

🔴 **그림은 대본이 말하는 사건에서 나온다**(runbook §3-0). 2D 판 화면은 참고하지 않았다.
   이 편의 사건은 하나다: **닿아야 걸리는 부탁이, 거리가 벌어져서 영영 안 걸린다.**

🔑 그래서 이 편의 수치는 **거리**다. 계기 막대는 범주색이 아니라 **계기 시안**을 쓴다 —
   몸·일·위협 어느 것도 아니고 「재는 것」이기 때문이다. 막대에는 **문턱 표시**(6타일)가 있고,
   채움이 그 선을 넘는 순간이 이 편의 사건이다.

🔴 **2026-08-13 판정 반영 — 「애니메이션이 하나도 정상적으로 동작하는 게 없다」.**
   일곱 줄 전부에 대해 「그 줄이 말할 때 화면에서 무슨 몸짓이 나는가」를 다시 적었다.
   무엇이 바뀌었는지는 각 샷 스크립트 맨 위에 줄 단위로 적혀 있다. 공통은 셋이다:
   ① **목수가 화면에 있다.** 앞 판은 훅·본문2·3 에서 목수를 `hide_render` 로 지워 놓고
      「없다」를 말했는데, 없는 사람은 아무 몸짓도 못 한다. 이제 **나가는 것을 보여 준다**.
   ② **카메라가 넓고 마을 바깥이 채워져 있다**(`village.outskirts`). 「멀리 떨어졌다」가
      사건인데 화면이 좁으면 멀어진 것을 보여 줄 자리가 없다.
   ③ **판정 반경을 땅에 그린다**(`instrument.ring`). 막대는 「지금 얼마나 먼가」를 말하고
      원은 **「어디까지가 안인가」**를 말한다 — 이 편은 둘 다 필요하다.
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

EP = 'ep15s-3'
FPS = 30

# 🔑 타일 → 미터. 원문의 수(6·38·8 타일)를 이 마을 크기에 맞춘 환산이다 —
#    38 타일이 9.9m 라 마을(10m 대) 밖으로 나가고, 6 타일은 1.6m 로 「손 닿는 거리」다.
TILE = 0.26
REACH_TILE, FAR_TILE, PULLED_TILE = 6, 38, 8
GAUGE_MAX = FAR_TILE * TILE                     # 막대 눈금 끝 = 38타일
REACH_R = REACH_TILE * TILE                     # 판정 반경 1.56m — 땅에 그리는 원

# 여섯 — 목수 하나, 집 없는 주민 하나, 나머지는 제 일을 한다
CARPENTER, HOMELESS = 0, 1
CAST = [
    ('hammer', (1.55, 0.55), 'fire'),          # 목수 — 마을 안쪽
    ('stop', (-0.35, -2.15), None),            # 집 없는 주민
    ('farm', (-2.7, -2.3), 'field'),
    ('draw', (2.6, -0.9), 'well'),
    ('gather', (1.3, -2.3), 'bush'),
    ('chop', (-3.65, 0.45), (-4.8, 0.6)),
]
CARP_HOME = (1.55, 0.55)                        # 목수가 처음 있는 자리(마을 안)
# 🔴 나가는 쪽은 **재서 골랐다** — 소품 열여섯의 반지름을 넣고 방위 360 개를 훑어
#    9m 까지 걸리는 것이 없는 각을 뽑았다. 마을이 빽빽해서 통로가 사실상 **하나**다:
#    집 (0.2, 4.0) 과 (3.6, 2.2) 사이의 골. 58°·60° 판은 둘 다 집 (3.6, 2.2) 을 뚫었고,
#    렌더에서는 **목수가 지붕 뒤로 사라졌다**(프레임 190 에서 머리만 삐져나왔다).
CARP_AWAY = math.radians(86)
# 🔴 38 타일(9.9m)까지 실제로 보낼 필요가 없다 — 사용자 지시: 「모여 있는 주민과 확실하게
#    멀리 떨어진 모습」이면 된다. 7.5m 판은 목수가 20m 밖이라 **알아볼 수 없는 점**이었다.
#    수(38)는 자막과 막대가 지고, 화면은 **혼자 떨어져 짓는다**만 진다.
# 🔴 6.0 은 나무 (3.1, 5.8) 에서 1.35m 라 목수 클로즈업에서 **나무가 화면 오른쪽을
#    통째로 덮었다**(프레임 95). 7.0 이면 2.03m 로 벌어진다.
FAR_D = 7.0                                     # 혼자 집을 짓는 거리(화면 안에 남는다)
FAR_AT = (CARP_HOME[0] + FAR_D * math.cos(CARP_AWAY),
          CARP_HOME[1] + FAR_D * math.sin(CARP_AWAY))
# 🔴 **훅이 뛰어 나가는 쪽과 본문2 가 눌러앉는 자리는 다른 자리다.** 나갈 수 있는 통로는
#    마을이 빽빽해서 북쪽 골 하나뿐인데(86°), 거기 그대로 서 있으면 남쪽 카메라에서
#    **마을 지붕들 사이에 낀 것**처럼 보인다 — 화면 좌우 거리가 0 이라 「떨어졌다」가 안 읽힌다.
#    그래서 통로로 나간 뒤 **동북쪽 빈 들로 더 흘러가** 거기 눌러앉는다. 떠돌이니까 그게 맞고,
#    화면에서는 무리와 목수 사이에 **빈 땅이 생긴다.** 소품에서 2.5m 이상 떨어진 자리다.
FAR_SETTLE = (5.60, 6.40)
_sd = math.hypot(FAR_SETTLE[0] - CARP_HOME[0], FAR_SETTLE[1] - CARP_HOME[1])
FAR_HOUSE = (FAR_SETTLE[0] + (FAR_SETTLE[0] - CARP_HOME[0]) / _sd * 1.5,
             FAR_SETTLE[1] + (FAR_SETTLE[1] - CARP_HOME[1]) / _sd * 1.5)
# 🔴 부탁받은 집의 자리는 **카메라에서 재서** 옮겼다. (-1.35, -3.05) 는 넓은 카메라에서
#    6.8m 앞이라 다 자란 집이 **화면 왼쪽 3분의 1을 통째로 덮었다**(프레임 215).
#    지금 자리는 9.0m 이고 시선축에서 15° 안이라 화면 안에 온전히 들어온다.
HOUSE_AT = (-1.60, -0.15)                       # 부탁받은 집이 서는 자리
# 🔴 고친 뒤 목수가 앉는 자리는 **재서 낸다** — 자막이 「8타일 안으로」라고 말하므로
#    집 없는 주민에게서 정확히 8타일(2.08m)이어야 한다. 눈대중으로 적으면 자막이 거짓말이 된다.
PULLED_A = math.radians(55)                     # 마을 안쪽 · 우물과 덤불 사이의 빈자리
PULLED = ((-0.35) + PULLED_TILE * TILE * math.cos(PULLED_A),
          (-2.15) + PULLED_TILE * TILE * math.sin(PULLED_A))
NEAR_HOUSE = (1.99, 0.51)                       # 떠돌이 목수가 마을 안에 지은 제 집
# 🔴 (-0.25, -0.95) 는 되돌아온 목수 자리에서 1.19m 라 **목수가 집 안에 서 있었다.**
#    이 집이 져야 하는 말은 「더 가깝다」이므로 기준은 목수가 아니라 **집 없는 주민**이다 —
#    거기서 6.5 타일(떠돌이의 8 타일보다 가깝다)이고 목수와는 3.6m 떨어져 있다.
#    ⚠️ (-1.55, -3.30) 은 넓은 카메라에서 5.9m 앞이라 다 자란 집이 **창도 문도 없는 회색
#       판때기**로 화면 왼쪽 가장자리를 덮었다. 6.9m 로 물리고 문 쪽을 카메라로 돌렸다.
HOMEBODY_HOUSE = (-1.20, -1.30)                 # 눌러사는 목수의 집 — **더 가깝다**
HOMEBODY_ROT = -30                              # 문·창이 카메라를 보게

# ── 카메라 ────────────────────────────────────────────
# 🔴 **넓게 잡는다.** 앞 판의 EYE 는 마을만 담아서 「밖으로 나갔다」를 보여 줄 자리가 없었다.
#    렌즈까지 같이 내린다 — 뒤로 물러나기만 하면 사람이 점이 되고, 렌즈만 넓히면 왜곡된다.
EYE = ((4.35, -6.55, 1.80), (-0.45, -1.20, 0.66))     # 마을 눈높이 조망 (좁다 — 근접 샷용)
WIDE = ((4.20, -7.05, 3.10), (0.75, 0.20, 0.85))      # 마을을 비스듬히 — 부탁·되돌아옴
# 🔴 **나가는 축 위에 카메라를 둔다.** 옆에서 보면 나가는 사람이 집 뒤로 들어가고,
#    화면에서는 「나갔다」가 아니라 「사라졌다」가 된다. 축 위에 서면 그가 **작아지며
#    멀어지는 것**이 그대로 보이고, 집들은 좌우로 비켜선다.
# 🔴 높이는 **하늘 넓이로** 정했다. 3.1m 판은 지평선이 화면 63% 자리라 위 35% 가
#    통째로 빈 하늘이었다 — 쇼츠에서 그건 그림을 3분의 1 버리는 것이다.
AXIS = ((1.30, -7.20, 3.60), (1.80, 1.60, 0.85))      # 훅 — 나가는 길을 정면으로
# 🔴 본문2 만 **높이 올라간다.** 눈높이에서는 목수의 새 집이 마을 지붕 뒤로 들어가 안 보였다.
#    6m 에서 내려다보면 시선이 지붕 위 2.2m 를 지나 그 집에 닿는다 — 재서 고른 높이다.
#    ❌ 서쪽에서 직교로 잡는 판도 해 봤는데, 마을 집들이 통째로 앞을 막고 여섯이 오른쪽
#       가장자리에 몰렸다. 좌우 거리는 **카메라가 아니라 목수의 자리**로 벌리는 게 맞았다.
FAR = ((1.60, -9.80, 6.00), (2.60, 2.20, 0.90))       # 본문2 — 무리와 혼자 나간 목수를 함께
DEV = ((2.40, -6.40, 5.80), (-0.55, -1.30, 0.35))     # 개발자 시점
CLOSE = ((0.75, -4.85, 1.05), (-0.35, -2.05, 0.62))   # 집 없는 주민 앞
WIDE_LENS, FAR_LENS = 32, 34
# 🔴 58mm 는 3m 앞에서 화면 폭이 1.88m 라 **머리 위 막대가 화면 밖으로 밀렸다.**
#    42mm 면 2.6m 라 사람과 계기가 같이 들어온다 — 계기는 사람 옆에 있어야 계기다.
CLOSE_LENS = 42                                        # 클로즈업은 눌러 잡는다


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
    """그 샷 i 번째 자막 줄이 **언제 시작하나**(초). 🔴 몸짓 시각을 손으로 적지 마라 —
    이 편의 판정은 「그 말을 할 때 그 몸짓이 나는가」라 대본이 바뀌면 같이 움직여야 한다."""
    return stage.shot_lines(EP, shot_id)[i][0]


def home_face(i):
    """CAST[i] 가 처음 향한 각(라디안). 🔑 `build` 와 **같은 식**이라야 돌아설 때 안 튄다."""
    _job, (x, y), spot = CAST[i]
    if not spot:
        return math.radians(-100)
    s = village.SPOTS[spot] if isinstance(spot, str) else spot
    return math.atan2(y - s[1], x - s[0])


def build(wide=True):
    """마을 + 주민 여섯 + 모닥불. 반환 (village, arms, fire, outskirts).

    🔴 `wide` 면 마을 **바깥**까지 심는다. 넓은 카메라가 빈 바닥을 비추면 「멀다」가 아니라
       「아무것도 없다」로 읽힌다."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    v = village.build()
    out = village.outskirts() if wide else []
    arms = []
    for job, (x, y), spot in CAST:
        if spot:
            s = village.SPOTS[spot] if isinstance(spot, str) else spot
            face = math.atan2(y - s[1], x - s[0])
        else:
            face = math.radians(-100)
        _mesh, arm = stage.rigged(loc=(x, y, 0), rot_z=math.degrees(face))
        arms.append(arm)
    fire = village.flame(village.SPOTS['fire'])
    return v, arms, fire, out


def face(arm, at, from_=None):
    """그 사람을 **무엇 쪽으로 돌려세운다.** 🔴 규약: 정면은 (-cos θ, -sin θ) 라 π 를 더한다.
    🔑 아무도 아무것도 안 보는 것이 앞 판의 큰 구멍이었다 — 사건이 나면 몸이 그쪽을 향한다."""
    p = from_ or tuple(arm.location)[:2]
    ang = math.atan2(at[1] - p[1], at[0] - p[0]) + math.pi
    arm.rotation_euler = (0, 0, ang)
    return ang          # 🔑 **적용된 회전값**을 준다 — 돌아서는 보간이 이 값을 쓴다


def cam_right(cam_loc, at):
    dx, dy = at[0] - cam_loc[0], at[1] - cam_loc[1]
    n = math.hypot(dx, dy) or 1.0
    return (-dy / n, dx / n)


def dist_gauge(color='instrument', mark=True):
    """거리 막대 하나 + 문턱 표시(6타일). 🔴 **범주색이 아니라 계기 시안**이다."""
    g = instrument.gauge((0.0, 0.0, 0.95), color=color)
    if mark:
        instrument.gauge_mark(g, REACH_TILE / float(FAR_TILE))
    return g


def dist_show(g, on):
    for key in ('track', 'fill', 'mark'):
        if g.get(key) is not None:
            g[key].hide_render = not on


def dist_set(g, a, b):
    """두 사람 사이 거리를 막대에 채운다. 반환: 타일 수."""
    d = math.hypot(a[0] - b[0], a[1] - b[1])
    instrument.gauge_set(g, d / GAUGE_MAX)
    return d / TILE


def gauge_over(g, arm_loc, right, dz=0.26):
    """막대를 그 사람 옆 머리 높이에 놓는다 — 매 프레임 부른다."""
    instrument.gauge_at(g, arm_loc[0] + right[0] * dz, arm_loc[1] + right[1] * dz, 1.0)


EMPH, EMPH_IN = 1.9, 0.22


def emphasis(lines, t, focus):
    """대본이 그 수치를 부르는 동안 막대를 키운다(ep15s-2 와 같은 규약)."""
    out = {}
    for i, (t0, dur) in enumerate(lines):
        which = focus.get(i)
        if which is None or not (t0 - EMPH_IN <= t <= t0 + dur + EMPH_IN):
            continue
        u = min((t - (t0 - EMPH_IN)) / EMPH_IN, ((t0 + dur + EMPH_IN) - t) / EMPH_IN, 1.0)
        for w in (which if isinstance(which, (list, tuple)) else [which]):
            out[w] = max(out.get(w, 1.0), 1.0 + (EMPH - 1.0) * ease(max(0.0, u)))
    return out


def walk_to(arm, home, target, t, t0, span, run=False):
    """t0 부터 span 초 동안 target 쪽으로 간다. 반환 (자리, 걸은 시간).
    🔴 나아가는 거리는 `WALK_SPEED × 걸은 시간`이다 — 눈대중이면 발이 미끄러진다."""
    wt = min(max(t - t0, 0.0), span)
    ang = math.atan2(target[1] - home[1], target[0] - home[0])
    speed = motions.RUN_SPEED if run else motions.WALK_SPEED
    d = min(speed * wt, math.hypot(target[0] - home[0], target[1] - home[1]))
    p = (home[0] + d * math.cos(ang), home[1] + d * math.sin(ang))
    arm.location = (p[0], p[1], 0)
    arm.rotation_euler = (0, 0, ang + math.pi)      # 규약: 정면은 (-cos θ, -sin θ)
    return p, wt


def walk_pose(arm, wt, blend=0.26, run=False):
    """걷기·뛰기 ↔ 멈춤 이음새."""
    k = ease(wt / blend)
    go = motions.run(wt) if run else motions.walk(wt)
    stage.pose(arm, motions.blend(motions.stop(wt), go, k))


def others(arms, t, home, skip=(CARPENTER, HOMELESS), watch=None, watch_at=0.0,
           watch_span=1.6):
    """나머지 주민은 제 일을 한다. `watch`(자리)가 오면 그 시각부터 **일을 멈추고 그쪽을 본다.**

    🔴 앞 판은 사건이 벌어지는 동안 나머지 넷이 **같은 주기를 계속 돌았다.** 그래서 화면이
       연기가 아니라 배경이었다(2026-08-13 판정 §0). 사건은 **남이 반응해야** 사건이다.
    🔑 반응은 두 겹이다 — **몸이 그쪽으로 돌고**(자리) **하던 일이 멈춘다**(동작).
       하나만 하면 「등 돌린 채 굳었다」거나 「일하면서 옆을 본다」가 된다."""
    for i, arm in enumerate(arms):
        if i in skip:
            continue
        arm.location = home[i]
        work = motions.MOTIONS[CAST[i][0]](t)
        if watch is None or t < watch_at:
            stage.pose(arm, work)
            continue
        # 🔑 넷이 **동시에** 돌면 군무다. 번호로 조금씩 어긋내 각자 알아차리게 한다
        d = watch_at + 0.18 * i
        k = ease((t - d) / 0.45)
        if k <= 0.0:
            stage.pose(arm, work)
            continue
        # 🔴 `arm.rotation_euler` 를 읽어서 보간하지 마라 — 앞 프레임 값이 누적돼
        #    결정성 게이트가 죽는다(같은 프레임을 두 번 그리면 값이 달라진다).
        a0 = home_face(i)
        a1 = math.atan2(watch[1] - home[i][1], watch[0] - home[i][0]) + math.pi
        d_ang = (a1 - a0 + math.pi) % (2 * math.pi) - math.pi
        arm.rotation_euler = (0, 0, a0 + d_ang * min(k, 1.0))
        stage.pose(arm, motions.blend(work, motions.stop(t - d), k))
