"""ep15s-2 본문3 — 8 비트 · 11.14 초. **도망을 올렸더니 다섯이 먼저 뛴다. 하나만 남는다.**

  0.00~1.39  ① 조망으로 컷. 여섯이 다시 제 일을 하고 있다
  1.39~2.79  ② 덩이가 **아직 멀리** 있는데
  2.79~5.57  ③④ 다섯이 **차례로 먼저** 뛴다(겁이 많을수록 빠르다)
  5.57~8.36  ⑤⑥ 한 명만 남아 먹는다. 덩이는 그에게로 온다
  8.36~11.14 ⑦⑧ 닿는다(스쿼시) · 다섯은 이미 멀리 가 있다

🔑 훅과 **같은 사건을 다시 그린다.** 다른 것은 하나뿐이다 — 이번엔 다섯이 **덩이가 닿기
   한참 전에** 뛴다. 그 시차가 「도망을 배고픔 위에 놨다」의 3D 판이다.
🔑 뛰는 순서를 벌린 것이 「겁 많은 주민은 누구보다 먼저」다. 수는 자막이 말한다.
🔴 값을 그리지 않는다 — 화면에 있는 것은 **누가 언제 뛰기 시작하는가**뿐이다.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import instrument
import motions
import stage
import village

SHOT, SID = 'body3', 'S3'
OUT = C.out_dir(SHOT)
DUR = C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 8
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

# 🔑 **겁 축이 순서를 만든다.** 값은 안 그리고 시차로만 보인다(0.34초 간격).
RUN_AT = [2.05 * BEAT, 2.39 * BEAT, 2.73 * BEAT, 3.07 * BEAT, 3.41 * BEAT]
HIT_AT = 6.6 * BEAT
HOME_H = C.CAST[C.HUNGRY][1]
NEAR = C.behind(HOME_H, C.EYE[0], 0.95)

v, arms, fire, flakes = C.build()
HOME = [tuple(a.location) for a in arms]
# 🔴 **수치를 띄운다.** 뛰는 다섯과 남는 하나 위에 막대 둘(배고픔·도망)을 얹는다 —
#    다섯은 도망이 배고픔을 **넘고**, 남는 하나만 도로 아래로 내려앉는다(고집쟁이 95).
G = [C.gauges() for _ in arms]
RIGHT = C.cam_right(C.EYE[0], C.EYE[1])
LINES = stage.shot_lines(C.EP, SID)
FOCUS = {0: 1, 1: 1, 2: 1}      # 세 줄 다 **도망**을 부른다(105 · 먼저 · 95)
d = C.danger()
cam = stage.light_camera()
stage.key_from_view(*C.EYE)

import bpy
bpy.context.view_layer.update()
OBST = village.obstacles(exclude=(v['ground'],))
# 🔴 각을 손으로 정하지 않는다 — 소품과 서로를 피하는 각을 재서 고른다
ANG = list(C.flee_angles([HOME[i][:2] for i in range(5)], OBST))
print('[body3-2] %d비트 · %.2f초 · 뛰는 각 %s'
      % (BEATS, DUR, ' '.join('%.0f°' % math.degrees(x) for x in ANG)))


def draw(fi):
    t = fi / C.FPS

    for i, arm in enumerate(arms):
        job = C.CAST[i][0]
        if i == C.HUNGRY:
            continue
        if t < RUN_AT[i]:
            C.work(arm, job, t)
            arm.location = HOME[i]
        else:
            C.flee(arm, HOME[i], ANG[i], t, RUN_AT[i])
            C.flee_pose(arm, t, RUN_AT[i], job)

    # ── ⑤ 한 명만 남아 먹는다 → ⑦ 닿는다 ──
    hero = arms[C.HUNGRY]
    spec = motions.eat(t)
    if t >= HIT_AT:
        w = C.ease((t - HIT_AT) / 0.30)
        spec = motions.blend(spec, motions.huddle(t), w)
        spec = dict(spec)
        spec[motions.SQUASH] = (0.20 * math.exp(-((t - HIT_AT) / 0.22) ** 2), 0.0, 0.0)
    stage.pose(hero, spec)
    hero.location = HOME[C.HUNGRY]

    # ── 계기: 도망이 배고픔 위로 올라간다. 🔑 **고집쟁이만 도로 내려앉는다** ──
    rise = C.ease((t - 1.2 * BEAT) / (1.4 * BEAT))
    for i, g in enumerate(G):
        C.gauge_place(g, arms[i], RIGHT, z=0.95, emph=C.emphasis(LINES, t, FOCUS))
        # 🔑 계기는 **고르는 순간**에만 보인다. 뛰기 시작한 뒤에도 달고 있으면 화면이
        #    막대 숲이 되고, 정작 봐야 할 「누가 아직 안 뛰는가」가 안 보인다.
        gone = t > (RUN_AT[i] + 0.5) if i != C.HUNGRY else False
        C.gauge_show(g, 0.6 * BEAT < t and not gone)
        instrument.gauge_set(g[0], instrument.gauge_k(C.HUNGER))
        v_flee = (C.FLEE_STUBBORN if i == C.HUNGRY else C.FLEE_AFTER)
        k = instrument.gauge_k(C.FLEE_BEFORE) +             (instrument.gauge_k(v_flee) - instrument.gauge_k(C.FLEE_BEFORE)) * rise
        instrument.gauge_set(g[1], k)

    C.danger_at(d, (t - 1.0 * BEAT) / (HIT_AT - 1.0 * BEAT), NEAR, t)
    d.hide_render = t < 0.8 * BEAT

    # 카메라 — 조망에서 남는 한 명 쪽으로 천천히 밀고 들어간다
    u = C.ease((t - 4 * BEAT) / (3 * BEAT))
    (sx, sy, sz), (ax, ay, az) = C.EYE
    (cx, cy, cz), (bx, by, bz) = C.CLOSE
    stage.aim(cam, (sx + (cx - sx) * u * 0.72, sy + (cy - sy) * u * 0.72, sz + (cz - sz) * u * 0.72),
              (ax + (bx - ax) * u, ay + (by - ay) * u, az + (bz - az) * u))

    village.flicker(fire, t)
    village.snowfall(flakes, t, wind=0.2)


stage.bake(OUT, NF, C.FPS, draw, 'body3-2')
