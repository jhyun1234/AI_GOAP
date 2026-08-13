"""ep15s-3 샷 일곱이 공유하는 것 — 마을·주민 자리·거리 계기·카메라.

🔴 **그림은 대본이 말하는 사건에서 나온다**(runbook §3-0). 2D 판 화면은 참고하지 않았다.
   이 편의 사건은 하나다: **닿아야 걸리는 부탁이, 거리가 벌어져서 영영 안 걸린다.**

🔑 그래서 이 편의 수치는 **거리**다. 계기 막대는 범주색이 아니라 **계기 시안**을 쓴다 —
   몸·일·위협 어느 것도 아니고 「재는 것」이기 때문이다. 막대에는 **문턱 표시**(6타일)가 있고,
   채움이 그 선을 넘는 순간이 이 편의 사건이다.
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
CARP_AWAY = math.radians(58)                    # 걸어 나가는 쪽 — 집 사이가 아니라 트인 쪽
HOUSE_AT = (-1.35, -3.05)                       # 부탁받은 집이 서는 자리

EYE = ((4.35, -6.55, 1.80), (-0.45, -1.20, 0.66))     # 마을 눈높이 조망
DEV = ((2.40, -6.40, 5.80), (-0.55, -1.30, 0.35))     # 개발자 시점
CLOSE = ((0.55, -5.05, 0.98), (-0.35, -2.15, 0.60))   # 집 없는 주민 앞


def ease(u):
    u = max(0.0, min(1.0, u))
    return u * u * (3 - 2 * u)


def out_dir(shot):
    d = os.path.join(stage.OUT_ROOT, EP, shot, 'frames')
    os.makedirs(d, exist_ok=True)
    return d


def seconds(shot_id):
    return stage.shot_seconds(EP, shot_id)


def build(snow=False):
    """마을 + 주민 여섯 + 모닥불. 반환 (village, arms, fire, flakes)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    v = village.build()
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
    flakes = village.snow() if snow else None
    return v, arms, fire, flakes


def cam_right(cam_loc, at):
    dx, dy = at[0] - cam_loc[0], at[1] - cam_loc[1]
    n = math.hypot(dx, dy) or 1.0
    return (-dy / n, dx / n)


def dist_gauge():
    """거리 막대 하나 + 문턱 표시(6타일). 🔴 **범주색이 아니라 계기 시안**이다."""
    g = instrument.gauge((0.0, 0.0, 0.95), color='instrument')
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


def walk_to(arm, home, target, t, t0, span):
    """t0 부터 span 초 동안 target 쪽으로 걷는다. 반환 (자리, 걸은 시간).
    🔴 나아가는 거리는 `motions.WALK_SPEED × 걸은 시간`이다 — 눈대중이면 발이 미끄러진다."""
    wt = min(max(t - t0, 0.0), span)
    ang = math.atan2(target[1] - home[1], target[0] - home[0])
    d = min(motions.WALK_SPEED * wt, math.hypot(target[0] - home[0], target[1] - home[1]))
    p = (home[0] + d * math.cos(ang), home[1] + d * math.sin(ang))
    arm.location = (p[0], p[1], 0)
    arm.rotation_euler = (0, 0, ang + math.pi)      # 규약: 정면은 (-cos θ, -sin θ)
    return p, wt


def walk_pose(arm, wt, blend=0.26):
    """걷기 ↔ 멈춤 이음새."""
    k = ease(wt / blend)
    stage.pose(arm, motions.blend(motions.stop(wt), motions.walk(wt), k))
