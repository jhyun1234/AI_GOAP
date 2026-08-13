"""ep15s-2 본문4 — 6 비트 · 7.98 초. **원칙을 고쳐 쓰자 여섯이 다 달아난다.**

  0.00~1.33  ① 조망. 여섯이 또 제 일을 하고 있다
  1.33~2.66  ② 덩이가 들어온다
  2.66~5.32  ③④ 여섯이 **차례로** 뛴다 — 먼저 뛰는 사람과 늦게 뛰는 사람이 갈린다
  5.32~6.65  ⑤ 마지막 한 명(전에 남아 있던 그 사람)도 뛴다
  6.65~7.98  ⑥ 마을이 빈다. 덩이만 남는다

🔑 **착지의 그림이다.** 원문의 개정 조항이 성향에게 남긴 것이 정확히 이것이다 —
   성향은 새 칸을 만들지 못하고 **인접한 칸의 경계를 넘나드는 진폭**만 갖는다.
   그래서 「달아나느냐 마느냐」가 아니라 **「얼마나 빨리」**로 갈린다.
🔴 이 샷에서 아무도 안 맞는다. 그게 고쳐졌다는 뜻이고, 자막이 말하는 것과 같다.
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

SHOT, SID = 'body4', 'S4'
OUT = C.out_dir(SHOT)
DUR = C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 6
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

# 🔑 여섯이 **다** 뛴다. 갈리는 것은 시각뿐이고, 마지막이 그 고집쟁이다(0.30초 간격).
RUN_AT = [2.00 * BEAT, 2.26 * BEAT, 2.52 * BEAT, 2.78 * BEAT, 3.04 * BEAT, 3.55 * BEAT]
HOME_H = C.CAST[C.HUNGRY][1]

v, arms, fire, flakes = C.build()
HOME = [tuple(a.location) for a in arms]
# 🔴 착지의 수치 — **여섯 다 도망이 배고픔 위에 있다.** 고집쟁이 것도 올라온다
#    (원칙이 바뀌었으므로 성향은 순서를 뒤집지 못하고 진폭만 갖는다).
G = [C.gauges() for _ in arms]
RIGHT = C.cam_right(C.EYE[0], C.EYE[1])
LINES = stage.shot_lines(C.EP, SID)
FOCUS = {0: 1, 1: 1}
d = C.danger()
cam = stage.light_camera()
stage.key_from_view(*C.EYE)

import bpy
bpy.context.view_layer.update()
OBST = village.obstacles(exclude=(v['ground'],))
# 🔴 각을 손으로 정하지 않는다 — 소품과 서로를 피하는 각을 재서 고른다
ANG = list(C.flee_angles([HOME[i][:2] for i in range(6)], OBST))
print('[body4-2] %d비트 · %.2f초 · 뛰는 각 %s'
      % (BEATS, DUR, ' '.join('%.0f°' % math.degrees(x) for x in ANG)))


def draw(fi):
    t = fi / C.FPS

    for i, arm in enumerate(arms):
        job = C.CAST[i][0]
        if t < RUN_AT[i]:
            C.work(arm, job, t)
            arm.location = HOME[i]
        else:
            C.flee(arm, HOME[i], ANG[i], t, RUN_AT[i])
            C.flee_pose(arm, t, RUN_AT[i], job)

    # ── 계기: 여섯 다 도망이 위. 🔑 갈리는 것은 **높이가 아니라 뛰는 시각**이다 ──
    rise = C.ease((t - 0.8 * BEAT) / (1.1 * BEAT))
    for i, g in enumerate(G):
        C.gauge_place(g, arms[i], RIGHT, z=0.95, emph=C.emphasis(LINES, t, FOCUS))
        C.gauge_show(g, 0.4 * BEAT < t < RUN_AT[i] + 0.5)
        instrument.gauge_set(g[0], instrument.gauge_k(C.HUNGER))
        k0 = instrument.gauge_k(C.FLEE_STUBBORN if i == C.HUNGRY else C.FLEE_AFTER)
        instrument.gauge_set(g[1], k0 + (instrument.gauge_k(C.FLEE_AFTER + 3) - k0) * rise
                             if i == C.HUNGRY else k0)

    C.danger_at(d, (t - 1.0 * BEAT) / (DUR - 1.6 * BEAT), C.behind(HOME_H, C.EYE[0], 0.9), t)
    d.hide_render = t < 0.8 * BEAT

    # 카메라 — 뒤로 물러나며 **빈 마을**을 보여 준다. 마지막 비트의 사건이 그것이다
    u = C.ease((t - 3.5 * BEAT) / (2.5 * BEAT))
    (sx, sy, sz), at = C.EYE
    stage.aim(cam, (sx + 1.35 * u, sy - 1.30 * u, sz + 0.55 * u), at)

    village.flicker(fire, t)
    village.snowfall(flakes, t, wind=0.2)


stage.bake(OUT, NF, C.FPS, draw, 'body4-2')
